using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using CSharpRTMP.Common;

namespace Temp
{
    static class Program
    {
        
        static void Main(string[] args)
        {
           var m = new MemoryStream();
            var m0 = m;
            m0.Test();
        }

        public static void Test(this Stream s)
        {
            Debug.WriteLine("Test");
        }
        public static void Test(this MemoryStream s)
        {
            Debug.WriteLine("memory");
        }
    }
}
