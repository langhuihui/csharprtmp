using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CSharpRTMP.Common
{
	public static class Logger
	{
		public static void Log(string log) => Console.WriteLine(log);

	    public static void FATAL(string format, params object[] args)
        {
            Console.ForegroundColor = ConsoleColor.Red;
			Log (string.Format(format,args));
            Console.ForegroundColor = ConsoleColor.White;
            var frames = new StackTrace(1, true).GetFrames();
            for (var i = 0; i < frames.Length && i < 3; i++)
            {
                Log(frames[i].ToString());
            }
		}
		public static void ASSERT(string format,params object[] args){
            Console.ForegroundColor = ConsoleColor.DarkRed;
			Log (string.Format(format,args));
		}
        public static void WARN(string format, params object[] args)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
			Log (string.Format(format,args));
		}

	    public static void FINEST(string format, params object[] args)
        {
            Console.ForegroundColor = ConsoleColor.Green;
			Log (string.Format(format,args));
		}
        public static void INFO(string format, params object[] args)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
			Log (string.Format(format,args));
		}
        public static void Debug(string format, params object[] args)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Log(string.Format(format, args));
        }
	}
}

