using System.IO;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Rtmp;
using CSharpRTMP.Core.Streaming;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace CSharpRTMP.Core.Protocols.Cluster
{
    [StreamType(StreamTypes.ST_OUT_NET_CLUSTER,StreamTypes.ST_IN_NET_CLUSTER, StreamTypes.ST_IN_NET_RTMP, StreamTypes.ST_IN_NET_LIVEFLV, StreamTypes.ST_IN_FILE_RTMP, StreamTypes.ST_IN_NET_MP3, StreamTypes.ST_IN_NET_RTMFP)]
    public class OutClusterStream : BaseOutNetStream<BaseClusterProtocol>
    {
        public uint StreamId;
        public OutClusterStream(BaseClusterProtocol pProtocol, StreamsManager pStreamsManager, string name,uint streamId)
            : base(pProtocol, pStreamsManager, name)
        {
            StreamId = streamId;
        }
        public override bool FeedData(Stream pData, uint dataLength, uint processedLength, uint totalLength, uint absoluteTimestamp,
            bool isAudio)
        {
            Protocol.Send(isAudio ? ClusterMessageType.Audio : ClusterMessageType.Video, StreamId, pData, dataLength, processedLength, totalLength, absoluteTimestamp);
            return true;
        }
        public override void SignalDetachedFromInStream()
        {
            Protocol.Send(ClusterMessageType.StopPublish, output => output.Write7BitValue(StreamId));
            Dispose();
        }

        public override void SendStreamMessage( BufferWithOffset buffer)
        {
            Protocol.Send(ClusterMessageType.StreamMessage, o =>
            {
                o.Write7BitValue(StreamId);
                o.Write7BitValue((uint) buffer.Length);
                o.Write(buffer.Buffer);
            });
        }
    }
}
