using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Streaming;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class BaseRtmfpAppProtocolHandler : BaseAppProtocolHandler
    {
        public InboundRTMFPProtocol CurrentProtocol;
        public HashSet<Session> Sessions = new HashSet<Session>(); 
        public BaseRtmfpAppProtocolHandler(Variant configuration) : base(configuration)
        {
       
        }

        public override void RegisterProtocol(BaseProtocol protocol)
        {
            if (protocol is Session) Sessions.Add(protocol as Session);
            else
            CurrentProtocol = protocol as InboundRTMFPProtocol;
        }

        public override void UnRegisterProtocol(BaseProtocol protocol)
        {
            if (protocol is Session) Sessions.Remove(protocol as Session);
            else
            CurrentProtocol = null;
        }

        public override void Broadcast(BaseProtocol from,Variant invokeInfo)
        {
            foreach (var session in Sessions)
            {
                
            }
        }

        public override void CallClient(BaseProtocol to, string functionName, Variant param)
        {
            //throw new NotImplementedException();
        }

        public override void SharedObjectTrack(BaseProtocol to, string name, uint version, bool isPersistent, Variant primitives)
        {
            //throw new NotImplementedException();
        }
    }
}
