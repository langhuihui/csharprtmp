using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Rtmp;
using CSharpRTMP.Core.Streaming;

namespace Core.Protocols.Rtmp
{
    [StreamType(StreamTypes.ST_OUT_NET_RTMP_4_RTMP, StreamTypes.ST_IN_NET_CLUSTER, StreamTypes.ST_IN_NET_RTMP, StreamTypes.ST_IN_NET_LIVEFLV, StreamTypes.ST_IN_FILE_RTMP, StreamTypes.ST_IN_NET_MP3, StreamTypes.ST_IN_NET_RTMFP)]
    public class OutNetRTMP4RTMPStream : BaseOutNetRTMPStream
    {
        public OutNetRTMP4RTMPStream(BaseRTMPProtocol pProtocol, StreamsManager pStreamsManager, string name, uint rtmpStreamId, uint chunkSize)
            : base(pProtocol, pStreamsManager, name, rtmpStreamId, chunkSize)
        {
        }

//        public override  bool IsCompatibleWithType(ulong type) {
//    return type.TagKindOf(StreamTypes.ST_IN_NET_RTMP)
//        || type.TagKindOf(StreamTypes.ST_IN_NET_RTMFP)
//            || type.TagKindOf(StreamTypes.ST_IN_NET_LIVEFLV)
//            || type.TagKindOf(StreamTypes.ST_IN_FILE_RTMP)
//            || type.TagKindOf(StreamTypes.ST_IN_NET_MP3);
//}
    }
}
