using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class RtmfpWriter:H2NBinaryWriter
    {
        public uint BufferSize;
        public RtmfpWriter(Stream source) : base(source)
        {
            Clear(11);
        }
        public void Clear(int pos)
        {
            BaseStream.SetLength(pos);
            BaseStream.Position = pos;
        }
        public long AvaliableBufferCounts => BufferSize - BaseStream.Position;
    }
}
