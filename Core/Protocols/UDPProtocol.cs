using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.NetIO;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Core.Protocols
{
    [ProtocolType(ProtocolTypes.PT_UDP)]
    public class UDPProtocol:BaseProtocol
    {
        public override InputStream InputBuffer { get; } = new InputStream();
        private IOHandler _carrier;
        public ulong DecodedBytesCount { private set; get; }

        public override void Dispose()
        {
            base.Dispose();
            if (IOHandler == null) return;
            IOHandler.Protocol = null;
            IOHandler.Dispose();
        }
        public override IOHandler IOHandler
        {
            set
            {
                if (value != null)
                {
                    if (value.Type != IOHandlerType.IOHT_UDP_CARRIER)
                    {
                        Logger.ASSERT("This protocol accepts only UDP carriers");
                    }
                }
                _carrier = value;
            }
            get { return _carrier; }
        }
        public override bool SignalInputData(InputStream inputStream, IPEndPoint address)
        {
            DecodedBytesCount += inputStream.AvaliableByteCounts;
            return _nearProtocol?.SignalInputData(inputStream, address)??false;
        }
        public override bool AllowNearProtocol(ulong type) => true;

        public override bool AllowFarProtocol(ulong type)
        {
            Logger.WARN("This protocol doesn't accept any far protocol");
            return false;
        }
    
        public override bool EnqueueForOutbound(MemoryStream outputStream,int offset = 0)
        {
            if (IOHandler != null) return IOHandler.SignalOutputData();
            Logger.FATAL("UDPProtocol has no carrier");
            return false;
        }
    }
}
