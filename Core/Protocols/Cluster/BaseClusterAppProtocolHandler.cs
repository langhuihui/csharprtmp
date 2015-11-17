using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Rtmp;
using CSharpRTMP.Core.Streaming;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Core.Protocols.Cluster
{
    public abstract class BaseClusterAppProtocolHandler : BaseAppProtocolHandler
    {
        protected BaseClusterAppProtocolHandler(Variant configuration) : base(configuration)
        {
        }
        public bool Connected;
        public abstract void OnSOCreated(SO so);

        public abstract void PublishStream(uint appId, IInStream inStream,string type = "live");
        public abstract void PlayStream(uint appId, string streamName);
        public abstract void CallAppFunction(uint appId, string functionName, Variant invoke);

        public abstract void Broadcast(uint appId, BaseProtocol to, Variant invokeInfo);

        public override void SharedObjectTrack(BaseProtocol to, string name, uint version, bool isPersistent, Variant primitives)
        {
            throw new NotImplementedException();
        }
        public override void CallClient(BaseProtocol to, string functionName, Variant invoke)
        {
            throw new NotImplementedException();
        }

        public override void Broadcast(BaseProtocol @from, Variant invokeInfo)
        {
            throw new NotImplementedException();
        }
    }
}
