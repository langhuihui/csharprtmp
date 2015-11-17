using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CSharpRTMP.Common;
using CSharpRTMP.Core.NetIO;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Core.Protocols
{
    [ProtocolType(ProtocolTypes.PT_TCP)]
    [AllowNearTypes]
    public class TCPProtocol:BaseProtocol
    {
        public override InputStream InputBuffer { get; } = new InputStream();
        private IOHandler _carrier;
        public ulong DecodedBytesCount { private set; get; }
       
        public override void Dispose()
        {
            base.Dispose();
            if (IOHandler != null)
            {
                IOHandler.Protocol = null;
                IOHandler.Dispose();
            }
        }

        public override IOHandler IOHandler
        {
            set
            {
                if (value != null)
                {
                    if (value.Type != IOHandlerType.IOHT_TCP_CARRIER && value.Type!=IOHandlerType.IOHT_STDIO)
                    {
                        Logger.ASSERT("This protocol accepts only TCP carriers");
                    }
                }
                _carrier = value;
            }
            get { return _carrier; }
        }

        public override bool SignalInputData(int recAmount)
        {
            DecodedBytesCount += (uint)recAmount;
            return _nearProtocol.SignalInputData(recAmount);
        }

        //public override bool AllowNearProtocol(ulong type) => true;

        public override bool AllowFarProtocol(ulong type)
        {
            Logger.WARN("This protocol doesn't accept any far protocol");
            return false;
        }

        public override bool EnqueueForOutbound(MemoryStream outputStream,int offset = 0)
        {
            if (IOHandler == null)
            {
                Logger.FATAL("TCPProtocol has no carrier");
                return false;
            }
            lock (IOHandler)
            {
                 outputStream.Position = offset;
                 IOHandler.SignalOutputData(outputStream);
            }
            return true;
        }
    }
}
