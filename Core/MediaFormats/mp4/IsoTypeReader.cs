using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.MediaFormats.mp4
{
    public class IsoTypeReader:N2HBinaryReader
    {
        public IsoTypeReader(Stream input) : base(input)
        {
        }

        public IsoTypeReader(Stream input, Encoding encoding) : base(input, encoding)
        {
        }
        public double ReadFixedPoint1616() => ((double)ReadUInt32()) / 65536;
        public double ReadFixedPoint0230()=>((double)ReadUInt32()) / (1 << 30);
        public float ReadFixedPoint88()=> ((float)ReadUInt16()) / 256;
        public string ReadIso639()
        {
            int bits = ReadUInt16();
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < 3; i++)
            {
                int c = (bits >> (2 - i) * 5) & 0x1f;
                result.Append((char)(c + 0x60));
            }
            return result.ToString();
        }
        public string Read4cc() => Encoding.GetEncoding("ISO-8859-1").GetString(ReadBytes(4));
    }
}
