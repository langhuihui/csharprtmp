using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CSharpRTMP.Common;
using CSharpRTMP.Core.MediaFormats;
using CSharpRTMP.Core.Streaming;
using static CSharpRTMP.Common.Logger;

namespace CSharpRTMP.Core.Protocols.Rtsp
{
    public enum RtcpPresence
    {
        Unknown,Available,Absent
    }
    [StreamType(StreamTypes.ST_IN_NET_RTP,StreamTypes.ST_OUT_NET_RTP,StreamTypes.ST_OUT_NET_RTMP_4_TS)]
    public class InNetRTPStream:BaseInNetStream<RtspProtocol>
    {
        private uint bandwidth;
        private TimeSpan _rtcpDetectionInterval;

        public override StreamCapabilities Capabilities { get; }
        private bool _hasAudio;
        private bool _hasVideo;

        private ushort _audioSequence;
        private ulong _audioPacketsCount;
        private ulong _audioDroppedPacketsCount;
        private ulong _audioBytesCount;
        private double _audioNTP;
        private double _audioRTP;
        private uint _audioLastTs;
        private uint _audioLastRTP;
        private uint _audioRTPRollCount;
        private double _audioFirstTimestamp = -1;
        private uint _lastAudioRTCPRTP;
        private uint _audioRTCPRTPRollCount;

        private readonly MemoryStream _currentNalu;
        private ushort _videoSequence;
        private ulong _videoPacketsCount;
        private ulong _videoDroppedPacketsCount;
        private ulong _videoBytesCount;
        private double _videoNTP;
        private double _videoRTP;
        private uint _videoLastTs;
        private uint _videoLastRTP;
        private uint _videoRTPRollCount;
        private double _videoFirstTimestamp = -1;
        private uint _lastVideoRTCPRTP;
        private uint _videoRTCPRTPRollCount;

        private RtcpPresence _rtcpPresence;
        private DateTime _rtcpDetectionStart;

        private bool _avCodecsSent;
        public InNetRTPStream(RtspProtocol _rtsp, StreamsManager streamsManager, string _streamName, StreamCapabilities _streamCapabilities, TimeSpan _rtcpDetectionInterval):base(_rtsp,streamsManager,_streamName)
        {
            bandwidth = _streamCapabilities.BandwidthHint;
            this._rtcpDetectionInterval = _rtcpDetectionInterval;
            Capabilities = _streamCapabilities;
            _currentNalu = Utils.Rms.GetStream();
            _hasAudio = _streamCapabilities.AudioCodecId != AudioCodec.Unknown;
            _hasVideo = _streamCapabilities.VideoCodecId != VideoCodec.Unknown;
            _rtcpPresence = RtcpPresence.Unknown;
        }

        public override void ReadyForSend()
        {
          
        }

        public override void SignalOutStreamAttached(IOutStream pOutStream)
        {
            base.SignalOutStreamAttached(pOutStream);
            if (_hasVideo && _hasAudio)
            {
                if (_videoLastTs != 0 && _audioLastTs != 0 && _videoLastTs < _audioLastTs)
                {
                    FeedVideoCodecSetup(pOutStream);
                    FeedAudioCodecSetup(pOutStream);
                    _avCodecsSent = true;
                }
            }
            else
            {
                if (_videoLastTs != 0)
                {
                    FeedVideoCodecSetup(pOutStream);
                    _avCodecsSent = true;
                }
                if (_audioLastTs != 0)
                {
                    FeedAudioCodecSetup(pOutStream);
                    _avCodecsSent = true;
                }
            }
        }

        public override bool FeedData(Stream pData, uint dataLength, uint processedLength, uint totalLength, uint absoluteTimestamp,bool isAudio)
        {
            switch (_rtcpPresence)
            {
                case RtcpPresence.Unknown:
                    if (_rtcpDetectionInterval == TimeSpan.Zero)
                    {
                        WARN("RTCP disabled on stream {0}({1}) with name {2}. A/V drifting may occur over long periods of time", Type.TagToString(), UniqueId, Name);
                        _rtcpPresence = RtcpPresence.Absent;
                        return true;
                    }
                    if (_rtcpDetectionStart == DateTime.MinValue)
                    {
                        _rtcpDetectionStart = DateTime.Now;
                        return true;
                    }
                    if (DateTime.Now - _rtcpDetectionStart > _rtcpDetectionInterval)
                    {
                        WARN("Stream {0}({1}) with name {2} doesn't have RTCP. A/V drifting may occur over long periods of time", Type.TagToString(), UniqueId, Name);
                        _rtcpPresence = RtcpPresence.Absent;
                        return true;
                    }

                    var audioRTCPPresent = !_hasAudio || _audioNTP != 0;
                    var videoRTCPPresent = !_hasVideo || _videoNTP != 0;
                    if (audioRTCPPresent && videoRTCPPresent)
                    {
                        _rtcpPresence = RtcpPresence.Available;
                    }
                    return true;
                case RtcpPresence.Available:
                    var rtp = isAudio ? _audioRTP : _videoRTP;
                    var ntp = isAudio ? _audioNTP : _videoNTP;
                    absoluteTimestamp = (uint)(ntp + absoluteTimestamp - rtp);

                    break;
                case RtcpPresence.Absent:
                    var firstTimestamp = isAudio ? _audioFirstTimestamp : _videoFirstTimestamp;
                    if (firstTimestamp < 0)
                        firstTimestamp = absoluteTimestamp;
                    absoluteTimestamp -= (uint)firstTimestamp;
                    break;
                default:
                    return false;
            }
            var lastTs = isAudio ? _audioLastTs : _videoLastTs;
            if (-1.0 < lastTs * 100.0 - absoluteTimestamp * 100.0 && lastTs * 100.0 - absoluteTimestamp * 100.0 < 1.0)
            {
                absoluteTimestamp = lastTs;
            }
            if (lastTs > absoluteTimestamp)
            {
                WARN("Back time on {0} ATS:{1} LTS:{2} D:{3}", Name, absoluteTimestamp, lastTs, lastTs - absoluteTimestamp);
                return true;
            }
            if (isAudio)
                _audioLastTs = absoluteTimestamp;
            else
                _videoLastTs = absoluteTimestamp;
            if (!_avCodecsSent)
            {
                foreach (var temp in OutStreams.Where(x => !x.IsEnqueueForDelete()))
                {
                    SignalOutStreamAttached(temp);
                }

                if (!_avCodecsSent)
                {
                    return true;
                }
            }
            if (_hasAudio && _hasVideo && ((_audioLastTs == 0) || (_videoLastTs == 0)))
            {
                return true;
            }

            return base.FeedData(pData, dataLength, processedLength, totalLength, absoluteTimestamp, isAudio);
        }

        public bool FeedVideoData(InputStream pData, uint dataLength,ref RTPHeader rtpHeader)
        {
            var firstByte = pData.Reader.ReadByte();
            var secondByte = pData.Reader.ReadByte();
            pData.Position -= 1;
            //1. Check the counter first
            if (_videoSequence == 0)
            {
                //this is the first packet. Make sure we start with a M packet
                if (!rtpHeader.M)
                {
                    return true;
                }
                _videoSequence = rtpHeader.SEQ;
                return true;
            }
         
            if ((ushort)(_videoSequence + 1) != (ushort)rtpHeader.SEQ)
            {
                WARN("Missing video packet. Wanted: {0}; got: {1} on stream: {2}",
                    (ushort)(_videoSequence + 1),rtpHeader.SEQ, Name);
                _currentNalu.IgnoreAll();
                _videoDroppedPacketsCount++;
                _videoSequence = 0;
                return true;
            }
            _videoSequence++;
            //2. get the nalu
            var rtpTs = ComputeRTP(ref rtpHeader.Timestamp,ref _videoLastRTP,ref _videoRTPRollCount);
            uint ts = (uint) (rtpTs* 1000 / Capabilities.Avc.Rate);
            var naluType = (byte)(firstByte & 0x1f);
            if (naluType <= 23)
            {
                //3. Standard NALU
                //FINEST("V: %08"PRIx32, rtpHeader._timestamp);
                _videoPacketsCount++;
                _videoBytesCount += dataLength;
                pData.Position -= 1;
                return FeedData(pData, dataLength, 0, dataLength, ts, false);
            }
            else switch ((NaluType)naluType)
            {
                case NaluType.NALU_TYPE_FUA:
                    if (_currentNalu.GetAvaliableByteCounts() == 0)
                    {
                        _currentNalu.IgnoreAll();
                        if (secondByte >> 7 == 0)
                        {
                            WARN("Bogus nalu");
                            _currentNalu.IgnoreAll();
                            _videoSequence = 0;
                            return true;
                        }
                        secondByte = (byte) ((firstByte & 0xe0) | (secondByte & 0x1f));
                    
                        pData.WriteByte(secondByte);
                        pData.Position -= 1;
                        pData.WriteTo(_currentNalu);
                    
                        return true;
                    }
                    else
                    {
                        pData.Position += 1;
                        pData.WriteTo(_currentNalu);
                        if (((secondByte >> 6) & 0x01) == 1)
                        {
                            //FINEST("V: %08"PRIx32, rtpHeader._timestamp);
                            _videoPacketsCount++;
                            _videoBytesCount += (ulong) _currentNalu.GetAvaliableByteCounts();
                            if (!FeedData(_currentNalu,
                                (uint) _currentNalu.GetAvaliableByteCounts(),
                                0,
                                (uint) _currentNalu.GetAvaliableByteCounts(),
                                ts,
                                false))
                            {
                                FATAL("Unable to feed NALU");
                                return false;
                            }
                            _currentNalu.IgnoreAll();
                        }
                        return true;
                    }
                case NaluType.NALU_TYPE_STAPA:
                    var index = 1;
                    while (index + 3 < dataLength)
                    {
                        var length = pData.Reader.ReadUInt16();
                        index += 2;
                        if (index + length > dataLength)
                        {
                            WARN("Bogus STAP-A");
                            _currentNalu.IgnoreAll();
                            _videoSequence = 0;
                            return true;
                        }
                        _videoPacketsCount++;
                        _videoBytesCount += length;
                        if (!FeedData(pData,
                            length, 0,
                            length,
                            ts, false))
                        {
                            FATAL("Unable to feed NALU");
                            return false;
                        }
                        index += length;
                    }
                    return true;
                default:
                    WARN("invalid NAL: {0}", naluType);
                    _currentNalu.IgnoreAll();
                    _videoSequence = 0;
                    return true;
            }
        }
        public bool FeedAudioData(InputStream pData, uint dataLength,ref RTPHeader rtpHeader)
        {
            if (_audioSequence == 0)
            {
                //this is the first packet. Make sure we start with a M packet
                if (!rtpHeader.M)
                {
                    return true;
                }
                _audioSequence = rtpHeader.SEQ;
                return true;
            }
            else
            {
                if (_audioSequence + 1 != rtpHeader.SEQ)
                {
                    WARN("Missing audio packet. Wanted: {0}; got: {1} on stream: {2}",
                            (_audioSequence + 1), rtpHeader.SEQ,Name);
                    _audioDroppedPacketsCount++;
                    _audioSequence = 0;
                    return true;
                }
                else
                {
                    _audioSequence++;
                }
            }
            //1. Compute chunks count
            var chunksCount = pData.Reader.ReadUInt16();
            if ((chunksCount % 16) != 0)
            {
                FATAL("Invalid AU headers length: {0}", chunksCount);
                return false;
            }
            chunksCount = (ushort) (chunksCount >>4);

            //3. Feed the buffer chunk by chunk
            var cursor = 2u + 2u* chunksCount;
            var rtpTs = ComputeRTP(ref rtpHeader.Timestamp,ref _audioLastRTP,ref _audioRTPRollCount);
            for (var i = 0; i < chunksCount; i++)
            {
                uint chunkSize;
                if (i != (chunksCount - 1))
                {
                    chunkSize = (uint) ((pData.Reader.ReadUInt16()) >> 3);
                }
                else
                {
                    chunkSize = dataLength - cursor;
                }
                uint ts = (uint)((rtpTs + (ulong)i * 1024ul) * 1000 / Capabilities.Samplerate);
                if ((cursor + chunkSize) > dataLength)
                {
                    FATAL("Unable to feed data: cursor:{0}; chunkSize: {1}; dataLength: {2}; chunksCount: {3}",
                            cursor, chunkSize, dataLength, chunksCount);
                    return false;
                }
                _audioPacketsCount++;
                _audioBytesCount += chunkSize;
                if (!FeedData(pData,
                        chunkSize + 2,
                        0,
                        chunkSize + 2,
                        ts, true))
                {
                    FATAL("Unable to feed data");
                    return false;
                }
                cursor += chunkSize;

            }

            return true;
        }
        public void ReportSR(ulong ntpMicroseconds, uint rtpTimestamp, bool isAudio)
       
        {
            if (isAudio)
            {
                _audioRTP = (double)ComputeRTP(ref rtpTimestamp,ref _lastAudioRTCPRTP,
                       ref _audioRTCPRTPRollCount) / Capabilities.Samplerate * 1000.0;
                _audioNTP = ntpMicroseconds / 1000.0;
            }
            else
            {
                _videoRTP = (double)ComputeRTP(ref rtpTimestamp,ref _lastVideoRTCPRTP,
                       ref _videoRTCPRTPRollCount) / Capabilities.Avc.Rate * 1000.0;
                _videoNTP = ntpMicroseconds / 1000.0;
            }
        }

        void FeedVideoCodecSetup(IOutStream pOutStream)
        {
            if (!pOutStream.FeedData(
                Utils.Rms.GetStream("sps", Capabilities.Avc.SPS, 0, Capabilities.Avc.SPS.Length),

                    Capabilities.Avc.SpsLength,
                    0,
                    Capabilities.Avc.SpsLength,
                    _videoLastTs,
                    false))
            {
                FATAL("Unable to feed stream");
                if (pOutStream.GetProtocol() != null)
                {
                    pOutStream.GetProtocol().EnqueueForDelete();
                }
            }
            if (!pOutStream.FeedData(
                     Utils.Rms.GetStream("pps", Capabilities.Avc.PPS,0, Capabilities.Avc.PPS.Length),
                    Capabilities.Avc.PpsLength,
                    0,
                    Capabilities.Avc.PpsLength,
                    _videoLastTs,
                    false))
            {
                FATAL("Unable to feed stream");
                if (pOutStream.GetProtocol() != null)
                {
                    pOutStream.GetProtocol().EnqueueForDelete();
                }
            }
        }

        void FeedAudioCodecSetup(IOutStream pOutStream)
        {
            if (Capabilities.AudioCodecId == AudioCodec.Aac)
            {
                if (!pOutStream.FeedData(
                   Utils.Rms.GetStream("aac", Capabilities.Aac._pAAC, 0, Capabilities.Aac._pAAC.Length),
                   Capabilities.Aac._aacLength,
                   0,
                   Capabilities.Aac._aacLength,
                   _audioLastTs,
                   true))
                {
                    FATAL("Unable to feed stream");
                    if (pOutStream.GetProtocol() != null)
                    {
                        pOutStream.GetProtocol().EnqueueForDelete();
                    }
                }
            }
        }
        private ulong ComputeRTP(ref uint currentRtp,ref uint lastRtp,ref uint rtpRollCount)
        {
            if (lastRtp > currentRtp)
            {
                if (((lastRtp >> 31) == 0x01) && ((currentRtp >> 31) == 0x00))
                {
                    FINEST("RollOver");
                    rtpRollCount++;
                }
            }
            lastRtp = currentRtp;
            return (((ulong)rtpRollCount) << 32) | currentRtp;
        }
        public override void GetStats(Variant info, uint namespaceId)
        {
            base.GetStats(info, namespaceId);
            info["audio","bytesCount"] = _audioBytesCount;
            info["audio","packetsCount"] = _audioPacketsCount;
            info["audio","droppedPacketsCount"] = _audioDroppedPacketsCount;
            info["video","bytesCount"] = _videoBytesCount;
            info["video","packetsCount"] = _videoPacketsCount;
            info["video","droppedPacketsCount"] = _videoDroppedPacketsCount;
        }
    }
}