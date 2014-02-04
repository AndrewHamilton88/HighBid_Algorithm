using System;
using System.Collections.Generic;
using System.Text;
using Mapack;


namespace ParamincsSNMPcontrol
{
    public class Agents
    {
        //*class members
        public NetworkData NetDat;
        public Strategies Strat;
        public string Name;

        //*class construnctor
        public Agents(NetworkData A, Strategies B) { NetDat = A; Strat = B; }

    }

    public class LaneAgent : Agents
    {
        //*class members
        public List<BitOfRoad> RoadSegments = new List<BitOfRoad>();
        public List<double> SpeedList = new List<double>();
        public List<double> DistList = new List<double>();
        public double AvSpeed;
        public double AvDist;
        public int Count;
        public bool Duplicate;
        public string UpstreamAgents;
        public string feedPercentages;

        public Strategies.Bid LaneBid;

        //*Constructor
        public LaneAgent(NetworkData A, Strategies B) : base(A, B) { }

        //*function to return the total lane length in an area
        public double AreaLength()
        {
            double Len = 0;
            foreach (BitOfRoad b in RoadSegments)
            {
                Len += b.TotalLength;
            }
            return (Len);
        }
        public double geometricLength()
        {
            double minOff;
            double maxOff;
            double maxLen;

            minOff = RoadSegments[0].Offset;
            maxOff = RoadSegments[0].Offset;
            maxLen = RoadSegments[0].TotalLength;

            for (int i = 1; i < RoadSegments.Count; i++)
            {

                if (RoadSegments[i].Offset < minOff)
                {
                    minOff = RoadSegments[i].Offset;
                }
                else if (RoadSegments[i].Offset > maxOff)
                {
                    maxOff = RoadSegments[i].Offset;
                    maxLen = RoadSegments[i].TotalLength;
                }
                else
                {

                }
            }
            return (maxLen + maxOff - minOff);
        }

        //*Function to get vehicle data from the database.
        public void PullDataAtTime(int ToD)
        {
            SpeedList.Clear();
            DistList.Clear();
            AvSpeed = 0;
            AvDist = 0;
            Count = 0;

            TimeSpan TS = new TimeSpan(0, 0, ToD / 100);
            string TimeOfDay = TS.ToString();

            foreach (BitOfRoad BoR in RoadSegments)
            {
                string ConditionLine = "AtTime = '" + TimeOfDay + "' AND";
                ConditionLine += " OnLink = '" + BoR.StartNode + ":" + BoR.EndNode + "' AND ";
                ConditionLine += "LaneNum = " + BoR.LaneNum;
                List<double[]> SpeedDist = NetDat.PDB.GetSpeedAndDistane(ConditionLine);

                foreach (double[] SD in SpeedDist)
                {
                    AvSpeed += SD[0];
                    AvDist += SD[1] + BoR.Offset;
                    Count++;
                    SpeedList.Add(SD[0]);
                    DistList.Add(SD[1] + BoR.Offset);
                }
            }

            if (Count != 0)
            {
                AvSpeed = AvSpeed / Count;
                AvDist = AvDist / Count;
            }
        }

        //Function to generate a bid
        public void GenerateBid()
        {
            Strat.ProcessLane(this);
        }



        public class BitOfRoad
        {
            //*Class Members
            public string
                StartNode,
                EndNode;
            public int
                LaneNum,
                OfLanes;
            public double
                Offset,
                TotalLength;

            public BitOfRoad(string sn, string en, int ln, int ol, double os)
            {
                StartNode = sn; EndNode = en;
                LaneNum = ln; OfLanes = ol;
                Offset = os;
            }
        }

    }

    public class StageAgent : Agents
    {
        //*Class Members
        public List<LaneAgent> Lanes = new List<LaneAgent>();
        public List<double> Weights = new List<double>();
        public Strategies.Bid StageBid;

        //*Constructor
        public StageAgent(NetworkData A, Strategies B) : base(A, B) { }

        //*class function
        public void GenerateBid(int ToD)
        {
            foreach (LaneAgent LA in Lanes)
            {
                LA.PullDataAtTime(ToD);
                LA.GenerateBid();
            }
            Strat.ProcessStage(this);
        }

        //*class function
        public void NeutralWeight()
        {
            for (int i = 0; i < Lanes.Count; i++)
            {
                Weights.Add(1);
            }
        }
    }

    public class JunctionAgent : Agents
    {
        //*Class Members
        public List<StageAgent> Stages = new List<StageAgent>();
        public string SignalNode;
        public int NoOfStages;
        public int NextStage;

        //*Class Constructor
        public JunctionAgent(NetworkData A, Strategies B, string SN, int NoS)
            : base(A, B)
        {

            SignalNode = SN;
            NoOfStages = NoS;
            NextStage = 1;
        }

        //*Function for mediating the auction
        public void MediateAuction(int ToD)
        {
            foreach (StageAgent SA in Stages)
            {
                SA.GenerateBid(ToD);
            }
            Strat.ProcessJunction(this);

        }

        //*Function for building lifetime data string
        public string BuildLifeTimeString()
        {
            string Condition = "";
            foreach (StageAgent SA in this.Stages)
            {
                foreach (LaneAgent LA in SA.Lanes)
                {
                    if (LA.Duplicate == false)
                    {
                        foreach (LaneAgent.BitOfRoad BoR in LA.RoadSegments)
                        {
                            if (BoR.LaneNum == 0)
                            {
                                Condition += "OnLink = '" + BoR.StartNode + ":" + BoR.EndNode + "' OR ";
                            }
                        }

                    }
                }
            }
            return (Condition);

        }

        public string BuildLifetimeCondition(string ToD)
        {
            string Condition = "AtTime = '" + ToD + "' AND (";
            Condition += BuildLifeTimeString();
            Condition = Condition.Remove((Condition.Length - 3));
            Condition += ")";
            return (Condition);
        }

    }

    public class ZoneAgent : Agents
    {
        //**Class Members
        public List<JunctionAgent> Junctions = new List<JunctionAgent>();
        public int[] NextStages;


        //Constructor
        public ZoneAgent(NetworkData A, Strategies B)
            : base(A, B)
        {

        }

        //function for coordinating junction
        public void CoordinateJunctions(int ToD)
        {
            foreach (JunctionAgent JA in Junctions)
            {
                JA.MediateAuction(ToD);
            }
            Strat.ProcessZone(this, ToD);
            //WriteBidsDataBase(ToD);
            //WriteSITDataBase(ToD);
        }

        /*public string BuildLifetimeCondition(string ToD)
{
string Condition = "AtTime = '" + ToD + "' AND (";
foreach (JunctionAgent JA in Junctions)
{
Condition += JA.BuildLifeTimeString();
}
Condition = Condition.Remove((Condition.Length - 3));
Condition += ")";
return (Condition);
}

private void WriteBidsDataBase(int ToD)
{
List<double> BidsList = new List<double>();
foreach (JunctionAgent JA in Junctions)
{
foreach (StageAgent SA in JA.Stages)
{
foreach (LaneAgent LA in SA.Lanes)
{
BidsList.Add(LA.LaneBid.Scalar);//TODO this only works for bids of type 'double'
}
}
}
double[] Bids = new double[BidsList.Count];
for (int i = 0; i < Bids.Length; i++)
{
Bids[i] = BidsList[i];
}

double LifeTime = 0;
int Vcount = 0;

TimeSpan TS = new TimeSpan(0, 0, ToD / 100);
string TimeOfDay = TS.ToString();
string Condition = BuildLifetimeCondition(TimeOfDay);
NetDat.PDB.GetVLifeTime(Condition, ref LifeTime, ref Vcount);


NetDat.RecordDecision(TimeOfDay, Bids, NextStages, Vcount, LifeTime);
}

private void WriteSITDataBase(int ToD)
{
TimeSpan TS = new TimeSpan(0, 0, ToD / 100);
string TimeOfDay = TS.ToString();
int Znum = 0;
foreach (JunctionAgent JA in Junctions)
{
foreach (StageAgent SA in JA.Stages)
{
foreach (LaneAgent LA in SA.Lanes)
{
//NetDat.SIT.AddSITLine(TimeOfDay, Znum, LA.Count, LA.AvSpeed, LA.AvDist);
Znum++;
}
}
}
}*/


    }

}