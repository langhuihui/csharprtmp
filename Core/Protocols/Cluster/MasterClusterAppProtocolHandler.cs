using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Rtmp;
using CSharpRTMP.Core.Streaming;
using Newtonsoft.Json.Bson;

namespace CSharpRTMP.Core.Protocols.Cluster
{
    public class MasterClusterAppProtocolHandler : BaseClusterAppProtocolHandler
    {
        public HashSet<InboundClusterProtocol> InboundClusters = new HashSet<InboundClusterProtocol>();
        public MasterClusterAppProtocolHandler(Variant configuration)
            : base(configuration)
        {
        }
        public override void RegisterProtocol(BaseProtocol protocol)
        {
            Logger.INFO("Incoming Cluster!");
            InboundClusters.Add(protocol as InboundClusterProtocol);
            foreach (var room in ClientApplicationManager.ApplicationByName.Values)
            {
                room.SOManager.RegisterProtocol(protocol as InboundClusterProtocol);
            }
        }
        public override void UnRegisterProtocol(BaseProtocol protocol)
        {
            Logger.INFO("Outgoing Cluster!");
            InboundClusters.Remove(protocol as InboundClusterProtocol);
            foreach (var so in (protocol as InboundClusterProtocol).SOs)
            {
                so.UnRegisterProtocol(protocol as InboundClusterProtocol);
            }
        }

        public override void OnSOCreated(SO so)
        {
            foreach (var inboundClusterProtocol in InboundClusters)
            {
                so.RegisterProtocol(inboundClusterProtocol);
            }
        }

        public override void Broadcast(uint appId, BaseProtocol pFrom, Variant invokeInfo)
        {
            foreach (var inboundClusterProtocol in InboundClusters.Where(inboundClusterProtocol => pFrom != inboundClusterProtocol))
            {
                inboundClusterProtocol.Broadcast(appId, pFrom, invokeInfo);
            }
        }

        public override void PublishStream(uint appId, IInStream inStream,string type="live")
        {
            foreach (var inboundClusterProtocol in InboundClusters.Where(x=>inStream.GetProtocol()!=x))
            {
                inboundClusterProtocol.PublishStream(appId, inStream,type);
            }
        }

        public override void PlayStream(uint appId, string streamName)
        {
            //foreach (var inboundClusterProtocol in InboundClusters)
            //{
            //    inboundClusterProtocol.PlayStream(streamName);
            //}
        }

        public override void CallAppFunction(uint appId, string functionName, Variant invoke)
        {
            foreach (var inboundClusterProtocol in InboundClusters)
            {
                var protocol = inboundClusterProtocol;
                inboundClusterProtocol.Send(ClusterMessageType.Call, o =>
                {
                    o.Write7BitValue(appId);
                    o.Write(functionName);
                    o.Write(invoke.ToBytes());
                });
            }
        }
    }
}
