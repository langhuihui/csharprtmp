using System.Collections.Generic;
using System.Linq;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Streaming;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Core.Protocols.Cluster
{
    [ProtocolType(ProtocolTypes.PT_INBOUND_CLUSTER)]
    [AllowFarTypes(ProtocolTypes.PT_TCP)]
    public class InboundClusterProtocol : BaseClusterProtocol
    {
        protected override void OnReceive(ClusterMessageType type)
        {
            string streamName;
            uint appId;
            StreamsManager streamManager;
            switch (type)
            {
                case ClusterMessageType.GetAppId:
                    var appName = InputBuffer.Reader.ReadString();
                    ClientApplicationManager.GetOrCreateRoom(appName);//创建房间自动广播appId
                    break;
                case ClusterMessageType.Publish:
                   
                    appId = InputBuffer.Reader.Read7BitValue();
                    streamName = InputBuffer.Reader.ReadString();
                    var streamType = InputBuffer.Reader.ReadUInt64();
                    var streamId = InputBuffer.Reader.Read7BitValue();
                    var chunkSize = InputBuffer.Reader.Read7BitValue();
                    var publishType = InputBuffer.Reader.ReadString();
                    
                    streamManager = GetRoom(appId).StreamsManager;
                    var getWaitings = streamManager.GetWaitingSubscribers(streamName, streamType);
                    var inStream = new InClusterStream(appId, this, streamName, streamType, chunkSize);
                    InStreams[streamId] = inStream;
                    foreach (var pBaseOutStream in getWaitings)
                    {
                        pBaseOutStream.Link(inStream);
                    }
                    if (publishType == "append" || publishType == "record")
                    {
                        streamManager.CreateOutFileStream(this, inStream, publishType == "append");
                    }
                    Application.GetProtocolHandler<BaseClusterAppProtocolHandler>().PublishStream(appId, inStream, publishType);
                    break;
                case ClusterMessageType.NoSubscriber:
                    appId = InputBuffer.Reader.Read7BitValue();
                    streamName = InputBuffer.Reader.ReadString();
                    streamManager = GetRoom(appId).StreamsManager;
                    foreach (var streams in streamManager.FindByTypeByName(StreamTypes.ST_OUT_NET_CLUSTER, streamName, false, false).Values)
                    {
                        streams.Dispose();
                    }
                    break;
                case ClusterMessageType.Play:
                    appId = InputBuffer.Reader.Read7BitValue();
                    streamName = InputBuffer.Reader.ReadString();
                    streamManager = GetRoom(appId).StreamsManager;
                    Logger.INFO(appId+":Play:"+streamName);
                    foreach (var streams in streamManager.FindByTypeByName(StreamTypes.ST_IN, streamName, true, false).Values)
                    {
                        PublishStream(appId, streams as IInStream);
                    }
                    break;
                default:
                    base.OnReceive(type);
                    break;
            }
        }
    }
}
