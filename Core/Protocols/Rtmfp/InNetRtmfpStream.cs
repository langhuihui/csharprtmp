using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Streaming;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    [StreamType(StreamTypes.ST_IN_NET_RTMFP, StreamTypes.ST_OUT_NET_CLUSTER, StreamTypes.ST_OUT_NET_RTMP_4_RTMP, StreamTypes.ST_OUT_NET_RTMFP, StreamTypes.ST_OUT_FILE_RTMP, StreamTypes.ST_OUT_NET_RTP, StreamTypes.ST_OUT_FILE_HLS)]
    public class InNetRtmfpStream:BaseInNetStream<Session>
    {
        public readonly MemoryStream AudioCodecBuffer = Utils.Rms.GetStream();
        public readonly MemoryStream VideoCodecBuffer = Utils.Rms.GetStream();
        
        public uint PublisherId;
        private FlowWriter _controller;
        private bool _firstKeyFrame;
        private Peer _publisher;
        private uint _lastAudioTime;
        private uint _lastVideoTime;
        public override StreamCapabilities Capabilities { get; }=new StreamCapabilities();
        public InNetRtmfpStream(Session pProtocol, StreamsManager pStreamsManager, string name)
            : base(pProtocol, pStreamsManager, name)
        {
        }
        
        public void Start(Peer peer, uint publisherId, FlowWriter controller)
        {
            if (PublisherId != 0)
            {
                if (controller != null)
                {
                    controller.WriteStatusResponse("Publish.BadName", Name + "is already published");
                }
            }
            PublisherId = publisherId;

            //string error;
            //if (!peer.OnPublish(this, out error))
            //{
            //    if (String.IsNullOrEmpty(error)) error = "Not allowed to publish " + Name;
            //}
            _publisher = peer;
            _controller = controller;
            _firstKeyFrame = false;
            foreach (var baseOutStream in OutStreams.OfType<IOutNetStream>())
            {
                baseOutStream.SendPublishNotify();
            }
            if (controller != null)
            {
                controller.WriteStatusResponse("Publish.Start", Name + "is now published");
            }
        }

        public override bool Stop()
        {
            if (PublisherId == 0) return true;
            foreach (var baseOutStream in OutStreams.OfType<IOutNetStream>())
            {
                baseOutStream.SendUnpublishNotify();
            }
            _controller.WriteStatusResponse("Unpublish.Success", Name + " is now unpublished");
            Flush();
            PublisherId = 0;
            _publisher = null;
            return true;
        }

        public override void Dispose()
        {
            Stop();
            base.Dispose();
        }

      

        public override bool FeedData(Stream pData, uint dataLength, uint processedLength, uint totalLength, uint absoluteTimestamp,
            bool isAudio)
        {
            var pos = pData.Position;
            var firstByte = pData.ReadByte();
            var secondByte = pData.ReadByte();
            pData.Position = pos;
            if (isAudio)
            {
                if ((firstByte >> 4) == 10 && secondByte == 0)
                {
                    pData.CopyDataTo(AudioCodecBuffer);
                    AudioCodecBuffer.Position = 0;
                    if (!Capabilities.InitAudioAAC(AudioCodecBuffer,(int) (AudioCodecBuffer.Length - 2)))
                    {
                        Logger.FATAL("InitAudioAAC failed");
                        return false;
                    }
                    AudioCodecBuffer.Position = 0;
                } 
                _lastAudioTime = absoluteTimestamp;
            }
            else
            {
                if (firstByte == 0x17 && secondByte == 0)
                {
                    pData.CopyDataTo(VideoCodecBuffer);
                    VideoCodecBuffer.Position = 0;
                    var reader = new N2HBinaryReader(VideoCodecBuffer);
                    var spsLength = reader.ReadUInt16();
                    var pSPS = reader.ReadBytes(spsLength);
                    reader.ReadByte();
                    var ppsLength = reader.ReadUInt16();
                    var pPPS = reader.ReadBytes(ppsLength);
                    if (!Capabilities.InitVideoH264(pSPS, pPPS))
                    {
                        Logger.FATAL("InitVideoH264 failed");
                        return false;
                    }
                    VideoCodecBuffer.Position = 0;
                }
                _lastVideoTime = absoluteTimestamp;
            }
            //var temp = OutStreams.Where(x => !x.IsEnqueueForDelete() && !x.FeedData(pData, dataLength, processedLength, totalLength, absoluteTimestamp, isAudio));
            //foreach (var baseOutNetRtmpStream in temp)
            //{
            //    this.Log().Info("Unable to feed OS:" + baseOutNetRtmpStream);
            //    baseOutNetRtmpStream.EnqueueForDelete();
            //    if (Protocol == baseOutNetRtmpStream.GetProtocol()) return false;
            //}
            base.FeedData(pData, dataLength, processedLength, totalLength, absoluteTimestamp, isAudio);
            return true;
        }

        //public override bool IsCompatibleWithType(ulong type)
        //{
        //    return type.TagKindOf(StreamTypes.ST_OUT_NET_RTMP_4_RTMP)
        //        || type.TagKindOf(StreamTypes.ST_OUT_NET_RTMFP)
        //           || type.TagKindOf(StreamTypes.ST_OUT_FILE_RTMP)
        //           || type.TagKindOf(StreamTypes.ST_OUT_NET_RTP)
        //           || type.TagKindOf(StreamTypes.ST_OUT_FILE_HLS);
        //}

        public override void SignalOutStreamAttached(IOutStream pOutStream)
        {
            if (VideoCodecBuffer.Length > 0
                && !pOutStream.FeedData(VideoCodecBuffer, (uint)VideoCodecBuffer.Length, 0, (uint)VideoCodecBuffer.Length, _lastVideoTime, false))
            {
                this.Log().Info("Unable to feed OS: {0}", pOutStream.UniqueId);
                pOutStream.EnqueueForDelete();
            }
            if (AudioCodecBuffer.Length > 0
                && !pOutStream.FeedData(AudioCodecBuffer, (uint)AudioCodecBuffer.Length, 0, (uint)AudioCodecBuffer.Length, _lastAudioTime, false))
            {
                this.Log().Info("Unable to feed OS: {0}", pOutStream.UniqueId);
                pOutStream.EnqueueForDelete();
            }
            //if (_lastStreamMessage != null
            //    && pOutStream.Type.TagKindOf(StreamTypes.ST_OUT_NET_RTMP)
            //    && !(pOutStream as BaseOutNetRTMPStream).SendStreamMessage(_lastStreamMessage))
            //{
            //    Logger.FATAL("Unable to send notify on stream. The connection will go down");
            //    pOutStream.EnqueueForDelete();
            //}
           
            base.SignalOutStreamAttached(pOutStream);
        }

        public override void SignalOutStreamDetached(IOutStream pOutStream)
        {
            base.SignalOutStreamDetached(pOutStream);
            //OnFlush -= ((OutNetRtmfpStream)pOutStream).Flush;
            this.Log().Info("outbound stream {0} detached from inbound stream {1}", pOutStream.UniqueId, UniqueId);
        }

        
    }
}
