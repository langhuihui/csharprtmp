using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.MediaFormats;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Cluster;
using CSharpRTMP.Core.Protocols.Rtmp;
using CSharpRTMP.Core.Streaming;

namespace Core.Protocols.Rtmp
{
    [StreamType(StreamTypes.ST_IN_NET_RTMP,StreamTypes.ST_OUT_NET_CLUSTER, StreamTypes.ST_OUT_NET_RTMP_4_RTMP, StreamTypes.ST_OUT_NET_RTMFP, StreamTypes.ST_OUT_FILE_RTMP, StreamTypes.ST_OUT_NET_RTP, StreamTypes.ST_OUT_FILE_HLS)]
    public class InNetRTMPStream : BaseInNetStream<BaseRTMPProtocol>
    {
        public readonly uint RtmpStreamId;

        private readonly uint _channelId;
        private readonly string _clientId;
        private readonly MemoryStream _videoCodecInit = Utils.Rms.GetStream();
        private uint _lastVideoTime;
        private readonly MemoryStream _audioCodecInit = Utils.Rms.GetStream();
        private uint _lastAudioTime;
        private AmfMessage? _lastStreamMessage;
        
        private ulong _audioPacketsCount;
        private ulong _audioDroppedPacketsCount;
        private ulong _audioBytesCount;
        private ulong _audioDroppedBytesCount;
        private ulong _videoPacketsCount;
        private ulong _videoDroppedPacketsCount;
        private ulong _videoBytesCount;
        private ulong _videoDroppedBytesCount;
        public override StreamCapabilities Capabilities { get; } = new StreamCapabilities();
        public override void Dispose()
        {
            base.Dispose();
            _videoCodecInit.Dispose();
            _audioCodecInit.Dispose();
        }

        public override uint ChunkSize
        {
            get
            {
                return base.ChunkSize;
            }
            set
            {
                base.ChunkSize = value;
#if PARALLEL
                OutStreams.Where(temp => !temp.IsEnqueueForDelete() && temp.Type.TagKindOf(StreamTypes.ST_OUT_NET_RTMP)).Select(x=>x.Protocol).Cast<BaseRTMPProtocol>().AsParallel().ForAll(x=>x.TrySetOutboundChunkSize(value));
#else
                foreach (var temp in OutStreams.Where(temp => !temp.IsEnqueueForDelete() && temp.Type.TagKindOf(StreamTypes.ST_OUT_NET_RTMP)).Cast<BaseOutNetRTMPStream>())
                {
                    temp.TrySetOutboundChunkSize(value);
                }
#endif
            }
        }

        public InNetRTMPStream(BaseRTMPProtocol pProtocol, StreamsManager pStreamsManager, string name, uint rtmpStreamId, uint chunkSize, uint channelId)
            : base(pProtocol, pStreamsManager, name)
        {
            RtmpStreamId = rtmpStreamId;
            base.ChunkSize = chunkSize;
            _channelId = channelId;
            _clientId = $"{Protocol.Id}_{RtmpStreamId}_{DateTime.Now.Ticks.ToString("X")}";
        }

        public override void GetStats(Variant info, uint namespaceId)
        {
            base.GetStats(info, namespaceId);
            info["audio","packetsCount"] = _audioPacketsCount;
            info["audio","droppedPacketsCount"] = (ulong)0;
            info["audio","bytesCount"] = _audioBytesCount;
            info["audio","droppedBytesCount"] = (ulong)0;
            info["video","packetsCount"] = _videoPacketsCount;
            info["video","droppedPacketsCount"] = (ulong)0;
            info["video","bytesCount"] = _videoBytesCount;
            info["video","droppedBytesCount"] = (ulong)0;
        }

        public override bool FeedData(Stream pData, uint dataLength, uint processedLength, uint totalLength, uint absoluteTimestamp,
            bool isAudio)
        {
            if (isAudio)
            {
                _audioPacketsCount ++;
                _audioBytesCount += dataLength;
               
                if (processedLength == 0 && Capabilities.AudioCodecId == AudioCodec.Unknown)
                {
                    var firstByte = pData.ReadByte();
                    var secondByte = pData.ReadByte();
                    pData.Position -= 2;
                    Capabilities.AudioCodecId = (AudioCodec)(firstByte >> 4);
                    Capabilities.Samplerate = Codec.RateMap[(firstByte >> 2) & 3];
                    Capabilities.AudioSampleSize = (AudioSampleSize)((firstByte >> 1)&1);
                    Capabilities.AudioSampleType = (AudioSampleType)(firstByte & 1);
                    Debug.WriteLine(Capabilities.AudioCodecId);
                    if (Capabilities.AudioCodecId == AudioCodec.Aac && secondByte == 0)
                    if (!InitializeAudioCapabilities(pData, dataLength))
                    {
                        Logger.FATAL("Unable to initialize audio capabilities");
                        return false;
                    }
                }
                _lastAudioTime = absoluteTimestamp;
            }
            else
            {
                _videoPacketsCount++;
                _videoBytesCount += dataLength;
                if (processedLength == 0 && Capabilities.VideoCodecId == VideoCodec.Unknown)
                {
                    var firstByte = pData.ReadByte();
                    var secondByte = pData.ReadByte();
                    pData.Position -= 2;
                    Capabilities.VideoFrameType = (VideoFrameType)(firstByte >> 4);
                    Capabilities.VideoCodecId = (VideoCodec)(firstByte & 0xF);
                    if (firstByte == 0x17 && secondByte == 0)
                    if (!InitializeVideoCapabilities(pData, dataLength))
                    {
                        Logger.FATAL("Unable to initialize video capabilities");
                        return false;
                    }
                }
                _lastVideoTime = absoluteTimestamp;
            }
            //Logger.INFO("{0}", _videoBytesCount);
            return base.FeedData(pData,dataLength,processedLength,totalLength,absoluteTimestamp,isAudio);
        }

        public bool SendOnStatusStreamPublished()
        {
            var response = StreamMessageFactory.GetInvokeOnStatusStreamPublished(
                    _channelId,  RtmpStreamId,  0, false,0,
                    Defines.RM_INVOKE_PARAMS_ONSTATUS_LEVEL_STATUS,
                    Defines.RM_INVOKE_PARAMS_ONSTATUS_CODE_NETSTREAMPUBLISHSTART,
                $"Stream `{Name}` is now published",
                    Name,  _clientId);
            if (Protocol.SendMessage(response, true)) return true;
            Logger.FATAL("Unable to send message");
            return false;
        }


        public override void SignalOutStreamAttached(IOutStream pOutStream)
        {
            if (_videoCodecInit.Length>0
                && !pOutStream.FeedData(_videoCodecInit, (uint)_videoCodecInit.Length, 0, (uint)_videoCodecInit.Length, _lastVideoTime, false))
            {
                this.Log().Info("Unable to feed OS: {0}",pOutStream.UniqueId);
                pOutStream.EnqueueForDelete();
            }
            if (_audioCodecInit.Length > 0
                && !pOutStream.FeedData(_audioCodecInit, (uint)_audioCodecInit.Length, 0, (uint)_audioCodecInit.Length, _lastAudioTime, true))
            {
                this.Log().Info("Unable to feed OS: {0}", pOutStream.UniqueId);
                pOutStream.EnqueueForDelete();
            }
           
            //if (_lastStreamMessage != null &&pOutStream.Type.TagKindOf(StreamTypes.ST_OUT_NET_RTMP))
            //{
            //    (pOutStream as IOutNetStream).SendStreamMessage(_lastStreamMessage.Value.Body, _lastStreamMessage.Value.MessageLength);
            //}
            base.SignalOutStreamAttached(pOutStream);
        }

        public override void SignalOutStreamDetached(IOutStream pOutStream)
        {
            base.SignalOutStreamDetached(pOutStream);
            this.Log().Info("outbound stream {0} detached from inbound stream {1}",pOutStream.UniqueId,UniqueId);
        }

        bool InitializeAudioCapabilities(Stream pData, uint length)
        {
            if (length < 4)
            {
                Logger.FATAL("Invalid length");
                return false;
            }
            pData.CopyDataTo(_audioCodecInit, (int)length);
            //Buffer.BlockCopy(pData.Buffer, pData.Offset, _audioCodecInit, 0, (int)length);
            if (!Capabilities.InitAudioAAC(_audioCodecInit, (int)length - 2))
            {
                Logger.FATAL("InitAudioAAC failed");
                return false;
            }
            _audioCodecInit.Position = 0;
            return true;
        }
        bool InitializeVideoCapabilities(Stream pData, uint length) {
	        if (length == 0) return false;
            pData.CopyDataTo(_videoCodecInit, (int) length);
            //_videoCodecInit.Position = 0;
            //Buffer.BlockCopy(pData.Buffer,pData.Offset,_videoCodecInit,0,(int)length);
            _videoCodecInit.Position = 11;
            var spsLength = _videoCodecInit.ReadUShort();
            var pSPS = new byte[spsLength];
            _videoCodecInit.Read(pSPS, 0, spsLength);
            _videoCodecInit.ReadByte();
            var ppsLength = _videoCodecInit.ReadUShort();
            var pPPS = new byte[ppsLength];
            _videoCodecInit.Read(pPPS, 0, ppsLength);
            
                if (!Capabilities.InitVideoH264(pSPS, pPPS))
                {
                    Logger.FATAL("InitVideoH264 failed");
                    return false;
                }
            _videoCodecInit.Position = 0;
	        //	FINEST("Cached the h264 video codec initialization: %u",
	        //			GETAVAILABLEBYTESCOUNT(_videoCodecInit));
	        return true;
        }
    }
}
