using System;
using System.Linq;
using System.Net;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.Protocols.Rtsp
{
    public struct MsgHdr
    {
        public byte[][] Buffers;

        public byte[] TotalBuffer
        {
            get
            {
                if (Buffers.Length == 1) return Buffers[0];
                var buffer = new byte[Buffers.Sum(x=>x.Length)];
                var offset = 0;
                foreach (var b in Buffers)
                {
                    Buffer.BlockCopy(b,0,buffer,offset,b.Length);
                    offset += b.Length;
                }
                return buffer;
            }
        }
    }
}