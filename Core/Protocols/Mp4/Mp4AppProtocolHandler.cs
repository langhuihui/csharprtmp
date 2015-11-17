using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.Protocols.Mp4
{
    public class Mp4AppProtocolHandler:BaseAppProtocolHandler
    {
        public Mp4AppProtocolHandler(Variant configuration) : base(configuration)
        {
        }

        public override void RegisterProtocol(BaseProtocol protocol)
        {
            //throw new NotImplementedException();
        }

        public override void UnRegisterProtocol(BaseProtocol protocol)
        {
            //throw new NotImplementedException();
        }

        public override void Broadcast(BaseProtocol @from, Variant invokeInfo)
        {
            throw new NotImplementedException();
        }

        public override void CallClient(BaseProtocol to, string functionName, Variant param)
        {
            throw new NotImplementedException();
        }

        public override void SharedObjectTrack(BaseProtocol to, string name, uint version, bool isPersistent, Variant primitives)
        {
            throw new NotImplementedException();
        }
    }
}
