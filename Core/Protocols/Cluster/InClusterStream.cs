using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Streaming;

namespace CSharpRTMP.Core.Protocols.Cluster
{
    [StreamType(StreamTypes.ST_IN_NET_CLUSTER, StreamTypes.ST_OUT_NET_CLUSTER, StreamTypes.ST_OUT_NET_RTMP_4_RTMP, StreamTypes.ST_OUT_NET_RTMFP, StreamTypes.ST_OUT_FILE_RTMP, StreamTypes.ST_OUT_NET_RTP, StreamTypes.ST_OUT_FILE_HLS)]
    public class InClusterStream : BaseInNetStream<BaseClusterProtocol>
    {
        public readonly ulong ContentStreamType;
        public uint AppId;

        public override StreamCapabilities Capabilities { get; }= new StreamCapabilities();

        public InClusterStream(uint appId, BaseClusterProtocol pProtocol, string name, ulong contentStreamType, uint chunkSize)
            : base(pProtocol, pProtocol.GetRoom(appId).StreamsManager, name)
        {
            AppId = appId;
            ContentStreamType = contentStreamType;
            base.ChunkSize = chunkSize;
        }

        public override void SignalOutStreamDetached(IOutStream pOutStream)
        {
            if (OutStreams.Count == 0)
            {
                Protocol.Send(ClusterMessageType.NoSubscriber, o =>
                {
                    o.Write7BitValue(AppId);
                    o.Write(Name);
                });
                Dispose();
            }
            base.SignalOutStreamDetached(pOutStream);
        }
    }
}
