using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using Microsoft.IO;

namespace CSharpRTMP.Core.Protocols.Rtmp
{
    public class Channel
    {
        public const uint CS_HEADER = 0;
        public const uint CS_PAYLOAD = 1;
        public uint id;
        public uint state = CS_HEADER;
        public AMF0Reader inputData = new AMF0Reader(Utils.Rms.GetStream());
        public Header lastInHeader;
        public byte lastInHeaderType;
        public uint lastInProcBytes;
        public uint lastInAbsTs;
        public uint lastInStreamId = 0xffffffff;
        public Header lastOutHeader;
        public byte lastOutHeaderType;
        public uint lastOutProcBytes;
        public uint lastOutAbsTs;
        public uint lastOutStreamId = 0xffffffff;

        public Channel()
        {
            throw new Exception();
        }
        public Channel(uint id)
        {
            this.id = id;
        }
        public void Reset()
        {
            state = CS_HEADER;
            inputData.BaseStream.SetLength(0);
            lastInHeader.Reset();
            lastInHeaderType = 0;
            lastInProcBytes = 0;
            lastInAbsTs = 0;
            lastInStreamId = 0xffffffff;
            lastOutHeader.Reset();
            lastOutHeaderType = 0;
            lastOutProcBytes = 0;
            lastOutAbsTs = 0;
            lastOutStreamId = 0xffffffff;
        }
    }
}
