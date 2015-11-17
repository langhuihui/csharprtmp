using System.IO;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Streaming;

namespace CSharpRTMP.Core.Protocols.Rtsp
{
    [StreamType(StreamTypes.ST_OUT_NET_RTP,StreamTypes.ST_IN_NET_RTMP,StreamTypes.ST_IN_NET_RTP,StreamTypes.ST_IN_NET_CLUSTER)]
    public abstract class BaseOutNetRTPUDPStream:BaseOutNetStream<RtspProtocol>
    {
        private bool _hasVideo;
        private bool _hasAudio;
        public uint AudioSSRC;
        public uint VideoSSRC;
        public ushort VideoCounter;
        public ushort AudioCounter;
        public OutboundConnectivity Connectivity;

        protected BaseOutNetRTPUDPStream(RtspProtocol pProtocol, StreamsManager pStreamsManager, string name) : base(pProtocol, pStreamsManager, name)
        {
            AudioSSRC = (uint) (0x80000000 | (Utils.Random.Next() & 0x00ffffff));
            VideoSSRC = AudioSSRC + 1;
            VideoCounter = (ushort)Utils.Random.Next();
            AudioCounter = (ushort)Utils.Random.Next();
        }

        public void HasAudioVideo(bool hasAudio, bool hasVideo)
        {
            _hasAudio = hasAudio;
            _hasVideo = hasVideo;
        }

        public override void SignalDetachedFromInStream()
        {
            Connectivity.SignalDetachedFromInStream();
        }

        public override bool FeedData(Stream pData, uint dataLength, uint processedLength, uint totalLength, uint absoluteTimestamp,
            bool isAudio)
        {
            var result = true;
            var pos = pData.Position;
            if (isAudio)
            {
                if (_hasAudio)
                    result = FeedDataAudio(pData, dataLength, processedLength, totalLength,
                    absoluteTimestamp, true);
            }
            else
            {
                if (_hasVideo)
                    result = FeedDataVideo(pData, dataLength, processedLength, totalLength,
                    absoluteTimestamp, false);
            }
            pData.Position = pos;
            return result;
        }

        protected abstract bool FeedDataVideo(Stream pData, uint dataLength, uint processedLength, uint totalLength,
           uint absoluteTimestamp, bool isAudio);

        protected abstract bool FeedDataAudio(Stream pData, uint dataLength, uint processedLength, uint totalLength,
            uint absoluteTimestamp, bool isAudio);

    }
}