using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CSharpRTMP.Common;
using CSharpRTMP.Core.MediaFormats;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Cluster;
using CSharpRTMP.Core.Protocols.Rtmp;
using CSharpRTMP.Core.Streaming;
using Newtonsoft.Json.Linq;
using static Core.Protocols.Rtmp.StreamMessageFactory;

namespace Core.Protocols.Rtmp
{
    public abstract class BaseOutNetRTMPStream : BaseOutNetStream<BaseRTMPProtocol>
    {
        public readonly uint RTMPStreamId;
        public uint ChunkSize;
        public bool CanDropFrames = true;

        private double _deltaVideoTime;
        private double _deltaAudioTime;
        private bool _useAudioTime;

        private double _pDeltaVideoTime
        {
            get { return _useAudioTime ? _deltaAudioTime : _deltaVideoTime; }
            set
            {
                if (_useAudioTime)
                    _deltaAudioTime = value;
                else
                    _deltaVideoTime = value;
            }
        }

        private double _pDeltaAudioTime
        {
            get { return _deltaAudioTime; }
            set { _deltaAudioTime = value; }
        }

        private double _seekTime;

        private bool _isFirstVideoFrame;
        private Header _videoHeader;


        private bool _isFirstAudioFrame;
        private Header _audioHeader;
        private MemoryStream _audioBucket;
        private MemoryStream _videoBucket;
        public MemoryStream OutputStream;
        private readonly Channel _pChannelAudio;
        private readonly Channel _pChannelVideo;
        private readonly Channel _pChannelCommands;
        public uint FeederChunkSize = 0xffffffff;

        private bool _audioCurrentFrameDropped;
        private bool _videoCurrentFrameDropped;
        private const uint _maxBufferSize = 65536*2;
        private ulong _attachedStreamType;
        private Variant _completeMetadata;
        private readonly string _clientId;
        private bool _paused;

        public bool SendOnStatusPlayMessages = true;

        private ulong _audioPacketsCount;
        private ulong _audioDroppedPacketsCount;
        private ulong _audioBytesCount;
        private ulong _audioDroppedBytesCount;
        private ulong _videoPacketsCount;
        private ulong _videoDroppedPacketsCount;
        private ulong _videoBytesCount;
        private ulong _videoDroppedBytesCount;
        public bool ReceiveAudio = true;
        public bool ReceiveVideo = true;

        protected BaseOutNetRTMPStream(BaseRTMPProtocol pProtocol, StreamsManager pStreamsManager, string name,
            uint rtmpStreamId, uint chunkSize)
            : base(pProtocol, pStreamsManager, name)
        {
            RTMPStreamId = rtmpStreamId;
            ChunkSize = chunkSize;
            _pChannelAudio = pProtocol.ReserveChannel();
            _pChannelVideo = pProtocol.ReserveChannel();
            _pChannelCommands = pProtocol.ReserveChannel();
            _clientId = $"{Protocol.Id}_{RTMPStreamId}_{Utils.Random.Next(10000000, 99999999)}";
            InternalReset();

        }

        public override void Dispose()
        {
            base.Dispose();
            Protocol.ReleaseChannel(_pChannelAudio);
            Protocol.ReleaseChannel(_pChannelVideo);
            Protocol.ReleaseChannel(_pChannelCommands);
            _audioBucket?.Dispose();
            _videoBucket?.Dispose();
            OutputStream?.Dispose();
        }

        public override void GetStats(Variant info, uint namespaceId)
        {
            base.GetStats(info, namespaceId);
            info["canDropFrames"] = CanDropFrames;
            info["audio", "packetsCount"] = _audioPacketsCount;
            info["audio", "droppedPacketsCount"] = _audioDroppedPacketsCount;
            info["audio", "bytesCount"] = _audioBytesCount;
            info["audio", "droppedBytesCount"] = _audioDroppedBytesCount;
            info["video", "packetsCount"] = _videoPacketsCount;
            info["video", "droppedPacketsCount"] = _videoDroppedPacketsCount;
            info["video", "bytesCount"] = _videoBytesCount;
            info["video", "droppedBytesCount"] = _videoDroppedBytesCount;
        }

        public static BaseOutNetRTMPStream GetInstance(BaseRTMPProtocol pProtocol, StreamsManager pStreamsManager,
            string name, uint rtmpStreamId, uint chunkSize, ulong inStreamType)
        {
            BaseOutNetRTMPStream result = null;
            if (inStreamType.TagKindOf(StreamTypes.ST_IN_NET_RTMP)
                || inStreamType.TagKindOf(StreamTypes.ST_IN_NET_RTMFP)
                || inStreamType.TagKindOf(StreamTypes.ST_IN_NET_LIVEFLV)
                || inStreamType.TagKindOf(StreamTypes.ST_IN_FILE_RTMP)
                || inStreamType.TagKindOf(StreamTypes.ST_IN_NET_MP3)
                )
            {
                result = new OutNetRTMP4RTMPStream(pProtocol, pStreamsManager, name, rtmpStreamId, chunkSize);
            }
            else if (inStreamType.TagKindOf(StreamTypes.ST_IN_NET_TS)
                     || inStreamType.TagKindOf(StreamTypes.ST_IN_NET_RTP)
                     || inStreamType.TagKindOf(StreamTypes.ST_IN_NET_AAC))
            {
                result = new OutNetRTMP4TSStream(pProtocol, pStreamsManager, name, rtmpStreamId, chunkSize);
            }
            if (result._pChannelAudio != null && result._pChannelVideo != null && result._pChannelCommands != null)
                return result;
            Logger.FATAL("No more channels left");
            return null;
        }

        public override bool SignalStop()
        {
            return true;
        }

        public override bool SignalSeek(ref double absoluteTimestamp)
        {
            //var outputStream = Utils.Rms.GetStream();
            Protocol.SendMessages(
                //1. Stream eof
                GetUserControlStreamEof(RTMPStreamId),
                //2. Stream is recorded
                GetUserControlStreamIsRecorded(RTMPStreamId),
                //3. Stream begin
                GetUserControlStreamBegin(RTMPStreamId),
                //4. NetStream.Seek.Notify
                GetInvokeOnStatusStreamSeekNotify(
                    _pChannelAudio.id, RTMPStreamId, absoluteTimestamp, true, 0, "seeking...", Name,
                    _clientId),
                //5. NetStream.Play.Start
                GetInvokeOnStatusStreamPlayStart(
                    _pChannelAudio.id, RTMPStreamId, 0, false, 0, "start...", Name,
                    _clientId),
                //6. notify |RtmpSampleAccess
                GetNotifyRtmpSampleAccess(
                    _pChannelAudio.id, RTMPStreamId, 0, false, true, true),
                //7. notify onStatus code="NetStream.Data.Start"
                GetNotifyOnStatusDataStart(
                    _pChannelAudio.id, RTMPStreamId, 0, false)
                );

           // 8.notify onMetaData
            //if (_completeMetadata[Defines.META_RTMP_META] == VariantType.Map)
            //{
            //    Protocol.SendMessages(GetNotifyOnMetaData(_pChannelAudio.id,
            //        RTMPStreamId, 0, false, _completeMetadata[Defines.META_RTMP_META].Clone()));

            //}
            //else
            //{
            //    var pCapabilities = GetCapabilities();
            //    if (pCapabilities != null &&
            //        (pCapabilities.VideoCodecId == VideoCodec.H264 && ((pCapabilities.Avc._width != 0)
            //                                                                      && (pCapabilities.Avc._height != 0))))
            //    {

            //        Protocol.SendMessages(GetNotifyOnMetaData(_pChannelAudio.id,
            //            RTMPStreamId, 0, false,
            //            Variant.GetMap(new VariantMapHelper
            //            {
            //                {"width", pCapabilities.Avc._width},
            //                {"height", pCapabilities.Avc._height}
            //            })));

            //    }
            //}
            //outputStream.Dispose();
            InternalReset();
            FixTimeBase();
            _seekTime = absoluteTimestamp;
            return true;
        }

        public override bool SignalResume()
        {
            _paused = false;
            var message = GetInvokeOnStatusStreamUnpauseNotify(
                _pChannelAudio.id, RTMPStreamId, 0, false, 0, "Un-pausing...", Name,
                _clientId);
            if (Protocol.SendMessage( message, true)) return true;
            Protocol.EnqueueForDelete();
            return false;
        }

        public override bool SignalPause()
        {
            _paused = true;
            var message = GetInvokeOnStatusStreamPauseNotify(
                _pChannelAudio.id, RTMPStreamId, 0, false, 0, "Pausing...", Name,
                _clientId);
            if (Protocol.SendMessage( message, true)) return true;
            Protocol.EnqueueForDelete();
            return false;
        }

        public override bool SignalPlay(ref double absoluteTimestamp, ref double length)
        {
            _paused = false;
            return true;
        }

        public override bool FeedData(Stream pData, uint dataLength, uint processedLength, uint totalLength,
            uint absoluteTimestamp,
            bool isAudio)
        {
            if (_paused) return true;
            if (isAudio)
            {
                if (processedLength == 0) _audioPacketsCount++;
                _audioBytesCount += dataLength;
                if (_isFirstAudioFrame)
                {
                    _audioCurrentFrameDropped = false;
                    if (dataLength == 0) return true;
                    if (processedLength != 0)
                    {
                        Protocol.EnqueueForOutbound(OutputStream);
                        return true;
                    }
                    if (_pDeltaAudioTime < 0) _pDeltaAudioTime = absoluteTimestamp;
                    if (_pDeltaAudioTime > absoluteTimestamp)
                    {
                        Protocol.EnqueueForOutbound(OutputStream);
                        return true;
                    }
                    _audioHeader.IsAbsolute = true;
                    _audioHeader.TimeStramp = (uint) (absoluteTimestamp - _pDeltaAudioTime + _seekTime);
                    var firstByte = pData.ReadByte();
                    var secondByte = pData.ReadByte();
                    pData.Position -= 2;
                    _isFirstAudioFrame = (firstByte >> 4) == 10 && (secondByte == 0);
                }
                else
                {
                    if (
                        !AllowExecution(processedLength, dataLength, ref _audioDroppedBytesCount,
                            ref _audioDroppedPacketsCount, ref _audioCurrentFrameDropped)) return true;
                    _audioHeader.IsAbsolute = false;
                    if (processedLength == 0)
                    {
                        //Debug.WriteLine("absoluteTimestamp:{0},_pDeltaAudioTime:{1},_seekTime:{2},_pChannelAudio:{3}", absoluteTimestamp, _pDeltaAudioTime, _seekTime, _pChannelAudio.lastOutAbsTs);
                        _audioHeader.TimeStramp =
                            (uint) ((absoluteTimestamp - _pDeltaAudioTime + _seekTime) - _pChannelAudio.lastOutAbsTs);
                    }

                    //  _audioHeader.ts = 0;
                }
                _audioHeader.MessageLength = totalLength;
                return !ReceiveAudio ||
                       ChunkAndSend(pData, (int) dataLength, _audioBucket, ref _audioHeader, _pChannelAudio);
            }
            else
            {
                if (processedLength == 0) _videoPacketsCount++;
                _videoBytesCount += dataLength;
                if (_isFirstVideoFrame)
                {
                    _videoCurrentFrameDropped = false;
                    if (dataLength == 0) return true;
                    var firstByte = pData.ReadByte();
                    var secondByte = pData.ReadByte();
                    pData.Position -= 2;
                    if (processedLength != 0)
                    {
                        Protocol.EnqueueForOutbound(OutputStream);
                        return true;
                    }
                    if (_pDeltaVideoTime < 0) _pDeltaVideoTime = absoluteTimestamp;
                    if (_pDeltaVideoTime > absoluteTimestamp)
                    {
                        Protocol.EnqueueForOutbound(OutputStream);
                        return true;
                    }
                    _videoHeader.IsAbsolute = true;
                    _videoHeader.TimeStramp = (uint) (absoluteTimestamp - _pDeltaVideoTime + _seekTime);

                    _isFirstVideoFrame = firstByte == 0x17 && secondByte == 0;
                }
                else
                {
                    if (
                        !AllowExecution(processedLength, dataLength, ref _videoDroppedBytesCount,
                            ref _videoDroppedPacketsCount, ref _videoCurrentFrameDropped)) return true;
                    _videoHeader.IsAbsolute = false;
                    if (processedLength == 0)
                    {
                        //Debug.WriteLine("absoluteTimestamp:{0},_pDeltaVideoTime:{1},_seekTime:{2},_pChannelVideo:{3}", absoluteTimestamp, _pDeltaVideoTime, _seekTime, _pChannelVideo.lastOutAbsTs);
                        _videoHeader.TimeStramp =
                            (uint) ((absoluteTimestamp - _pDeltaVideoTime + _seekTime) - _pChannelVideo.lastOutAbsTs);
                        //Logger.Debug("{0}-{1}-{2}={3}", absoluteTimestamp, _pDeltaVideoTime, _pChannelVideo.lastOutAbsTs, _videoHeader.ts);
                    }
                }
                _videoHeader.MessageLength = totalLength;
                //Logger.INFO("{0}", _videoBytesCount);
                return !ReceiveVideo ||
                       ChunkAndSend(pData, (int) dataLength, _videoBucket, ref _videoHeader, _pChannelVideo);
            }
        }

        public override void SignalAttachedToInStream()
        {
            if (OutputStream == null) OutputStream = Utils.Rms.GetStream();
            //1. Store the attached stream type to know how we should proceed on detach
            _attachedStreamType = InStream.Type == StreamTypes.ST_IN_NET_CLUSTER
                ? (InStream as InClusterStream).ContentStreamType
                : InStream.Type;
            //2. Mirror the feeder chunk size
            FeederChunkSize = InStream.ChunkSize;
            if (FeederChunkSize == 0) FeederChunkSize = 0xffffffff;
            if (FeederChunkSize != ChunkSize)
            {
                if (_audioBucket == null) _audioBucket = Utils.Rms.GetStream();
                if (_videoBucket == null) _videoBucket = Utils.Rms.GetStream();
            }
            //3. Fix the time base
            FixTimeBase();
            //4. Store the metadata
            if (_attachedStreamType.TagKindOf(StreamTypes.ST_IN_FILE_RTMP))
            {
                _completeMetadata = (InStream as InFileRTMPStream).CompleteMetadata;
            }
            AmfMessage message;
            //5. Send abort messages on audio/video channels
            if (_pChannelAudio.lastOutProcBytes != 0)
            {
                message = GenericMessageFactory.GetAbortMessage(_pChannelAudio.id);
                if (!Protocol.SendMessage( message))
                {
                    Protocol.EnqueueForDelete();
                    return;
                }
                _pChannelAudio.Reset();
            }
            if (_pChannelVideo.lastOutProcBytes != 0)
            {
                message = GenericMessageFactory.GetAbortMessage(_pChannelVideo.id);
                if (!Protocol.SendMessage( message))
                {
                    Protocol.EnqueueForDelete();
                    return;
                }
                _pChannelVideo.Reset();
            }
            //6. Stream is recorded
            if (_attachedStreamType.TagKindOf(StreamTypes.ST_IN_FILE_RTMP))
            {
                message = GetUserControlStreamIsRecorded(RTMPStreamId);
                if (!Protocol.SendMessage( message))
                {
                    Protocol.EnqueueForDelete();
                    return;
                }
            }
            //7. Stream begin
            message = GetUserControlStreamBegin(RTMPStreamId);
            if (!Protocol.SendMessage( message))
            {
                Protocol.EnqueueForDelete();
                return;
            }

            if (SendOnStatusPlayMessages)
            {
                //8. Send NetStream.Play.Reset
                message = GetInvokeOnStatusStreamPlayReset(
                    _pChannelAudio.id, RTMPStreamId, 0, true, 0, "reset...", Name, _clientId);

                if (!Protocol.SendMessage( message))
                {
                    Protocol.EnqueueForDelete();
                    return;
                }
                //9. NetStream.Play.Start
                message = GetInvokeOnStatusStreamPlayStart(
                    _pChannelAudio.id, RTMPStreamId, 0, true, 0, "start...", Name, _clientId);
                if (!Protocol.SendMessage( message))
                {
                    Protocol.EnqueueForDelete();
                    return;
                }
                //10. notify |RtmpSampleAccess
                message = GetNotifyRtmpSampleAccess(
                    _pChannelAudio.id, RTMPStreamId, 0, true, true, true);
                if (!Protocol.SendMessage( message))
                {
                    Protocol.EnqueueForDelete();
                    return;
                }
            }
            else
            {
                this.Log().Info("Skip sending NetStream.Play.Reset, NetStream.Play.Start and notify |RtmpSampleAcces");
            }

            if (_attachedStreamType.TagKindOf(StreamTypes.ST_IN_FILE_RTMP))
            {
                //11. notify onStatus code="NetStream.Data.Start"
                message = GetNotifyOnStatusDataStart(
                    _pChannelAudio.id, RTMPStreamId, 0, true);
                if (!Protocol.SendMessage( message))
                {
                    Protocol.EnqueueForDelete();
                    return;
                }
                //12. notify onMetaData
                message = GetNotifyOnMetaData(
                    _pChannelAudio.id, RTMPStreamId, 0, true, _completeMetadata[Defines.META_RTMP_META]);
                if (!Protocol.SendMessage( message,false,false))
                {
                    Protocol.EnqueueForDelete();
                }
            }
            else
            {
                var pCapabilities = Capabilities;
                if (pCapabilities == null || pCapabilities.VideoCodecId != VideoCodec.H264)
                {
                    //Protocol.SendMessagesBlock.TriggerBatch();
                    Protocol.EnqueueForOutbound(Protocol.OutputBuffer);
                    return;
                }
                var meta = Variant.Get();
                if ((pCapabilities.Avc._widthOverride != 0)
                    && (pCapabilities.Avc._heightOverride != 0))
                {
                    meta["width"] = pCapabilities.Avc._widthOverride;
                    meta["height"] = pCapabilities.Avc._heightOverride;
                }
                else if ((pCapabilities.Avc.Width != 0)
                         && (pCapabilities.Avc.Height != 0))
                {
                    meta["width"] = pCapabilities.Avc.Width;
                    meta["height"] = pCapabilities.Avc.Height;
                }
                if (pCapabilities.BandwidthHint != 0)
                {
                    meta["bandwidth"] = pCapabilities.BandwidthHint;
                }
                message = GetNotifyOnMetaData(_pChannelAudio.id,RTMPStreamId, 0, false, meta);
                    

                if (!Protocol.SendMessage( message))
                {
                    Protocol.EnqueueForDelete();
                }
            }
            //Protocol.SendMessagesBlock.TriggerBatch();
            
            Protocol.EnqueueForOutbound(Protocol.OutputBuffer);
        }

        public override void SignalDetachedFromInStream()
        {
            //1. send the required messages depending on the feeder
            AmfMessage message;
            if (_attachedStreamType.TagKindOf(StreamTypes.ST_IN_FILE_RTMP))
            {
                //2. notify onPlayStatus code="NetStream.Play.Complete", bytes=xxx, duration=yyy, level status
                message = GetNotifyOnPlayStatusPlayComplete(
                    _pChannelAudio.id, RTMPStreamId, 0, false,
                    _completeMetadata[Defines.META_FILE_SIZE],
                    (double) _completeMetadata[Defines.META_FILE_DURATION]/1000);
                if (!Protocol.SendMessage( message))
                {
                    Logger.FATAL("Unable to send message");
                    Protocol.EnqueueForDelete();
                    return;
                }
            }
            else
            {
                //3. Send the unpublish notify
                message = GetInvokeOnStatusStreamPlayUnpublishNotify(
                    _pChannelAudio.id, RTMPStreamId, 0, true, 0, "unpublished...", _clientId);
                if (!Protocol.SendMessage( message))
                {
                    Logger.FATAL("Unable to send message");
                    Protocol.EnqueueForDelete();
                    return;
                }
            }
            //4. NetStream.Play.Stop
            message = GetInvokeOnStatusStreamPlayStop(
                _pChannelAudio.id, RTMPStreamId, 0, false, 0, "stop...", Name,
                _clientId);
            //5. Stream eof
            message = GetUserControlStreamEof(RTMPStreamId);
            if (!Protocol.SendMessage( message))
            {
                Logger.FATAL("Unable to send message");
                Protocol.EnqueueForDelete();
                return;
            }
            //Protocol.SendMessagesBlock.TriggerBatch();
            Protocol.EnqueueForOutbound(Protocol.OutputBuffer);
            //6. Reset internally
            InternalReset();
        }

        public override void SignalStreamCompleted()
        {
            //1. notify onPlayStatus code="NetStream.Play.Complete", bytes=xxx, duration=yyy, level status
            var message = GetNotifyOnPlayStatusPlayComplete(
                _pChannelAudio.id, RTMPStreamId, 0, false, (double) _completeMetadata[Defines.META_FILE_SIZE],
                (double) _completeMetadata[Defines.META_FILE_DURATION]/1000);
            if (!Protocol.SendMessage( message))
            {
                Logger.FATAL("Unable to send message");
                Protocol.EnqueueForDelete();
                return;
            }
            //2. NetStream.Play.Stop
            message = GetInvokeOnStatusStreamPlayStop(
                _pChannelAudio.id, RTMPStreamId, 0, false, 0, "stop...", Name, _clientId);
            if (!Protocol.SendMessage( message))
            {
                Logger.FATAL("Unable to send message");
                Protocol.EnqueueForDelete();
                return;
            }
            //3. Stream eof
            message = GetUserControlStreamEof(RTMPStreamId);
            if (!Protocol.SendMessage( message))
            {
                Logger.FATAL("Unable to send message");
                Protocol.EnqueueForDelete();
                return;
            }
            Protocol.EnqueueForOutbound(Protocol.OutputBuffer);
            //Protocol.SendMessagesBlock.TriggerBatch();
            InternalReset();
        }

        public override void SendStreamMessage(BufferWithOffset buffer)
        {
            var header = new Header();
            header.Reset(0, 3, 0, (uint)buffer.Length, Defines.RM_HEADER_MESSAGETYPE_FLEXSTREAMSEND, RTMPStreamId);
            Protocol.ChunkAmfMessage(header, buffer, OutputStream);
            Protocol.EnqueueForOutbound(OutputStream);
        }

        private void FixTimeBase()
        {
            //3. Fix the time base
            if (InStream != null)
            {
                var attachedStreamType = InStream.Type;
                if (attachedStreamType.TagKindOf(StreamTypes.ST_IN_FILE_RTMP)
                    || attachedStreamType.TagKindOf(StreamTypes.ST_IN_NET_RTMP)
                    || attachedStreamType.TagKindOf(StreamTypes.ST_IN_NET_LIVEFLV)
                    || attachedStreamType.TagKindOf(StreamTypes.ST_IN_NET_RTP)
                    || attachedStreamType.TagKindOf(StreamTypes.ST_IN_NET_MP3)
                    || attachedStreamType.TagKindOf(StreamTypes.ST_IN_NET_AAC)
                    )
                {
                    //RTMP streams are having the same time base for audio and video
                    _useAudioTime = true;
                }
                else
                {
                    //otherwise consider them separate
                    _useAudioTime = false;
                }
            }
            else
            {
                _useAudioTime = false;
            }
        }

        public uint CommandsChannelId => 3;

        private bool ChunkAndSend(Stream pData, int length, Stream bucket, ref Header header, Channel channel)
        {
            //Debug.WriteLine("length:{0},ci:{6},si:{1},ht:{2},mt:{3},ml:{4},ts:{5}", length,header.StreamId,header.HeaderType,header.MessageType,header.MessageLength,header.TimeStramp,header.ChannelId);
            if (header.MessageLength == 0)
            {
                header.Write(channel, OutputStream);
                return Protocol.EnqueueForOutbound(OutputStream);
            }
            if (bucket == null)
            {
                header.Write(channel, OutputStream);
                pData.CopyDataTo(OutputStream, length);
                if (!Protocol.EnqueueForOutbound(OutputStream))
                {
                    Logger.FATAL("Unable to feed data");
                    return false;
                }
                channel.lastOutProcBytes += (uint) length;
                channel.lastOutProcBytes %= header.MessageLength;
                return true;
            }

            var availableDataInBuffer = bucket.GetAvaliableByteCounts();

            var totalAvailableBytes = availableDataInBuffer + length;

            long leftBytesToSend = header.MessageLength - channel.lastOutProcBytes;
            Debug.WriteLineIf(channel.lastOutProcBytes!=0, $"{ header.MessageLength},{channel.lastOutProcBytes},{availableDataInBuffer}" );
            if (totalAvailableBytes < ChunkSize && totalAvailableBytes != leftBytesToSend)
            {
                pData.CopyDataTo(bucket, length);
                //bucket.Write(pData.Buffer,pData.Offset, (int) length);
                return true;
            }
            var pos = pData.Position;
            if (availableDataInBuffer != 0)
            {
                //Send data
                bucket.Position = 0;
                header.Write(channel, OutputStream);
                bucket.CopyDataTo(OutputStream, (int) availableDataInBuffer);
                if (!Protocol.EnqueueForOutbound(OutputStream))
                {
                    Logger.FATAL("Unable to feed data");
                    return false;
                }

                //cleanup buffer
                bucket.IgnoreAll();
                //update counters
                totalAvailableBytes -= availableDataInBuffer;
                leftBytesToSend -= availableDataInBuffer;
                channel.lastOutProcBytes += (uint) availableDataInBuffer;
                var leftOvers = ChunkSize - availableDataInBuffer;
                //availableDataInBuffer = 0;

                //bite from the pData
                leftOvers = leftOvers <= length ? leftOvers : length;
                pData.CopyDataTo(OutputStream, (int) leftOvers);

                pData.Position += leftOvers;
                //update the counters
                //pData.Offset += (int)leftOvers;
                length -= (int) leftOvers;
                totalAvailableBytes -= leftOvers;
                leftBytesToSend -= leftOvers;
                channel.lastOutProcBytes += (uint) leftOvers;
                Debug.WriteLineIf(leftBytesToSend != header.MessageLength - channel.lastOutProcBytes,$"{leftBytesToSend},{header.MessageLength},{channel.lastOutProcBytes}");
            }
            while (totalAvailableBytes >= ChunkSize)
            {
                header.Write(channel, OutputStream);
                pData.CopyDataTo(OutputStream, (int) ChunkSize);
                totalAvailableBytes -= ChunkSize;
                leftBytesToSend -= ChunkSize;
                channel.lastOutProcBytes += ChunkSize;
                length -= (int) ChunkSize;
                pData.Position += ChunkSize;
                Debug.WriteLineIf(leftBytesToSend != header.MessageLength - channel.lastOutProcBytes, $"1 {leftBytesToSend},{header.MessageLength},{channel.lastOutProcBytes}");
            }
            if (totalAvailableBytes > 0 && totalAvailableBytes == leftBytesToSend)
            {
                header.Write(channel, OutputStream);
                pData.CopyDataTo(OutputStream, (int) leftBytesToSend);

                totalAvailableBytes -= leftBytesToSend;
                channel.lastOutProcBytes += (uint) leftBytesToSend;
                length -= (int) leftBytesToSend;
                pData.Position += leftBytesToSend;
                leftBytesToSend -= leftBytesToSend;
                Debug.WriteLineIf(leftBytesToSend != header.MessageLength - channel.lastOutProcBytes, $"2 {leftBytesToSend},{header.MessageLength},{channel.lastOutProcBytes}");
            }
            Debug.WriteLineIf(leftBytesToSend != header.MessageLength - channel.lastOutProcBytes, $"3 {leftBytesToSend},{header.MessageLength},{channel.lastOutProcBytes}");
            Protocol.EnqueueForOutbound(OutputStream);
            if (length > 0)
            {
                pData.CopyDataTo(bucket, length);
                //bucket.Write(pData.Buffer,pData.Offset, (int)length);
            }
            pData.Position = pos;
            if (leftBytesToSend == 0)
            {
                //Debug.WriteLine($"{channel.lastOutProcBytes},{header.MessageLength}");
                if (channel.lastOutProcBytes != header.MessageLength)
                {
                    Logger.WARN("{0},{1}", channel.lastOutProcBytes, header.MessageLength);
                }
                //Debug.Assert(channel.lastOutProcBytes == header.MessageLength);
                channel.lastOutProcBytes = 0;
            }
            else
            {
                Debug.WriteLine("还有剩余{0}", leftBytesToSend);
            }
            return true;
        }

        private bool AllowExecution(uint totalProcessed, uint dataLength, ref ulong bytesCounter,
            ref ulong packetsCounter, ref bool currentFrameDropped)
        {
            if (!CanDropFrames) return true;

            //we are allowed to drop frames
            //var bytesCounter = isAudio ? _audioDroppedBytesCount : _videoDroppedBytesCount;
            //var packetsCounter = isAudio ? _audioDroppedPacketsCount : _videoDroppedPacketsCount;
            //var currentFrameDropped = isAudio ? _audioCurrentFrameDropped : _videoCurrentFrameDropped;

            if (currentFrameDropped)
            {
                //current frame was dropped. Test to see if we are in the middle
                //of it or this is a new one
                if (totalProcessed != 0)
                {
                    //we are in the middle of it. Don't allow execution
                    bytesCounter += dataLength;
                    return false;
                }
                //this is a new frame. We will detect later if it can be sent
                currentFrameDropped = false;
            }

            if (totalProcessed != 0)
            {
                //we are in the middle of a non-dropped frame. Send it anyway
                return true;
            }
            //do we have any data?
            if (OutputStream == null)
            {
                //no data in the output buffer. Allow to send it
                return true;
            }

            //we have some data in the output buffer
            if (OutputStream.Length > _maxBufferSize)
            {
                //we have too many data left unsent. Drop the frame
                packetsCounter++;
                bytesCounter += dataLength;
                currentFrameDropped = true;

                return false;
            }
            //we can still pump data
            return true;
        }

        private void InternalReset()
        {
            if (_pChannelAudio == null || _pChannelVideo == null || _pChannelCommands == null) return;
            _deltaAudioTime = _deltaVideoTime = -1;
            _useAudioTime = false;
            _seekTime = 0;
            _videoCurrentFrameDropped = false;
            _isFirstVideoFrame = true;
            _videoHeader.ChannelId = _pChannelVideo.id;
            _videoHeader.MessageType = Defines.RM_HEADER_MESSAGETYPE_VIDEODATA;
            _videoHeader.StreamId = RTMPStreamId;
            _videoHeader.ReadCompleted = false;
            _videoBucket?.IgnoreAll();
            _audioCurrentFrameDropped = false;
            _isFirstAudioFrame = true;
            _audioHeader.ChannelId = _pChannelAudio.id;
            _audioHeader.MessageType = Defines.RM_HEADER_MESSAGETYPE_AUDIODATA;
            _audioHeader.StreamId = RTMPStreamId;
            _audioHeader.ReadCompleted = false;
            _audioBucket?.IgnoreAll();
            _attachedStreamType = 0;
            _completeMetadata = InStream != null && InStream.Type.TagKindOf(StreamTypes.ST_IN_FILE_RTMP)
                ? (InStream as InFileRTMPStream).CompleteMetadata
                : Variant.Get();
            OutputStream?.SetLength(0);
        }

        public void TrySetOutboundChunkSize(uint chunkSize)
        {
            Protocol.TrySetOutboundChunkSize(ChunkSize);
            FeederChunkSize = ChunkSize;
            CanDropFrames = false;
        }
    }
}
