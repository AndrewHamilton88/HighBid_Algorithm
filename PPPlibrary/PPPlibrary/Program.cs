using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Reflection;
using System.Diagnostics;

namespace ParamicsPuppetMaster
{
    class Program
    {
        static void Main(string[] args)
        {
            Process.Start("../../RunParamics.bat");
            Console.WriteLine("Press return to continue:");
            Console.Read();
        }
    }
}
