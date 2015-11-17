using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols;

namespace CSharpRTMP.Core.NetIO
{
    public class IOTimer:IOHandler
    {
        public IOTimer() : base(IOHandlerType.IOHT_TIMER)
        {
            //_outboundFd = _inboundFd = ++_idGenerator;
        }
        
   

        public override bool OnEvent(SocketAsyncEventArgs e)
        {
            if (Protocol.IsEnqueueForDelete || Protocol.TimePeriodElapsed()) return true;
            Logger.FATAL("Unable to handle TimeElapsed event");
            IOHandlerManager.EnqueueForDelete(this);
            return false;
        }
       
        public override void GetStats(Variant variant, uint namespaceId)
        {
            
        }

        public bool EnqueueForTimeEvent(uint seconds)
        {
            return this.EnableTimer(seconds);
        }

    }
}
