using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRTMP.Core.MediaFormats.mp4
{
    public static class Math
    {
        public static uint Gcd(uint a, uint b)
        {
            while (b > 0)
            {
                uint temp = b;
                b = a % b; // % is remainder
                a = temp;
            }
            return a;
        }

        public static int Gcd(int a, int b)
        {
            while (b > 0)
            {
                int temp = b;
                b = a % b; // % is remainder
                a = temp;
            }
            return a;
        }

        public static uint Lcm(uint a, uint b) => a * (b / Gcd(a, b));

        public static uint Lcm(uint[] input) => input.Aggregate(Lcm);

        public static int Lcm(int a, int b) => a * (b / Gcd(a, b));
    }
}
