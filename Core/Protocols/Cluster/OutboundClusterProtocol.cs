using System.Collections.Generic;
using System.Linq;
using System.Net;
using Core.Protocols.Rtmp;
using CSharpRTMP.Common;
using CSharpRTMP.Core.NetIO;
using CSharpRTMP.Core.Streaming;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Core.Protocols.Cluster
{
    [ProtocolType(ProtocolTypes.PT_OUTBOUND_CLUSTER)]
    [AllowFarTypes(ProtocolTypes.PT_TCP)]
    public class OutboundClusterProtocol : BaseClusterProtocol
    {
        protected override void OnReceive(ClusterMessageType type)
        {
            switch (type)
            {
                case ClusterMessageType.Publish:
                    var appId = InputBuffer.Reader.Read7BitValue();
                    var streamName = InputBuffer.Reader.ReadString();
                    Logger.INFO(appId + ":Publish:" + streamName);
                    var streamType = InputBuffer.Reader.ReadUInt64();
                    var streamId = InputBuffer.Reader.Read7BitValue();
                    var chunkSize = InputBuffer.Reader.Read7BitValue();
                    var publishType = InputBuffer.Reader.ReadString();
                    var streamManager = GetRoom(appId).StreamsManager;
                    var getWaitings = streamManager.GetWaitingSubscribers(streamName, streamType).ToArray();
                    if (getWaitings.Length>0)
                    {
                        var inStream = new InClusterStream(appId, this, streamName, streamType, chunkSize);
                        InStreams[streamId] = inStream;
                        foreach (var pBaseOutStream in getWaitings)
                        {
                            pBaseOutStream.Link(inStream);
                        }
                    }
                    else
                    {
                        if (publishType == "append" || publishType == "record")
                        {
                            var inStream = new InClusterStream(appId, this, streamName, streamType, chunkSize);
                            InStreams[streamId] = inStream;
                            streamManager.CreateOutFileStream(this, inStream, publishType == "append");
                        }
                        else
                            Send(ClusterMessageType.NoSubscriber, o =>
                            {
                                o.Write7BitValue(appId);
                                o.Write(streamName);
                            });
                    }
                    break;
                default:
                    base.OnReceive(type);
                    break;
            }
        }

        public static bool SignalProtocolCreated(BaseProtocol protocol, Variant customParameters)
        {
            var application = ClientApplicationManager.FindAppByName(customParameters[Defines.CONF_APPLICATION_NAME]);

            if (application == null)
            {
                Logger.FATAL("Application {0} not found", customParameters[Defines.CONF_APPLICATION_NAME]);
                return false;
            }
            if (protocol == null)
            {
                Logger.WARN("OutboundCluster Connection failed:{0}", customParameters.ToString());

                application.GetProtocolHandler<SlaveClusterAppProtocolHandler>().ReconnectTimer.Start();
                return false;
            }
            protocol.CustomParameters = customParameters;
            protocol.Application = application;
            return true;
        }
        
    }
}
