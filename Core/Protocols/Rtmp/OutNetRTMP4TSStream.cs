using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Protocols.Rtmp;
using CSharpRTMP.Common;
using CSharpRTMP.Core.MediaFormats;
using CSharpRTMP.Core.Protocols.Rtsp;
using CSharpRTMP.Core.Streaming;
using static CSharpRTMP.Common.Logger;
namespace CSharpRTMP.Core.Protocols.Rtmp
{
    [StreamType(StreamTypes.ST_OUT_NET_RTMP_4_TS,StreamTypes.ST_IN_NET_RTP,StreamTypes.ST_IN_NET_TS)]
    public class OutNetRTMP4TSStream:BaseOutNetRTMPStream
    {
        private byte[] _pSPSPPS = new byte[1024];
        private uint _PPSStart;
        private bool _videoCodecSent;
        private bool _audioCodecSent;
        private bool _spsAvailable;
        private long _lastVideoTimestamp = -1;
        private bool _isKeyFrame;
        private MemoryStream _videoBuffer = Utils.Rms.GetStream();

        public OutNetRTMP4TSStream(BaseRTMPProtocol pProtocol, StreamsManager pStreamsManager, string name, uint rtmpStreamId, uint chunkSize) : base(pProtocol, pStreamsManager, name, rtmpStreamId, chunkSize)
        {
            _pSPSPPS[0] = 0x17; //0x10 - key frame; 0x07 - H264_CODEC_ID
            _pSPSPPS[1] = 0; //0: AVC sequence header; 1: AVC NALU; 2: AVC end of sequence
            _pSPSPPS[2] = 0; //CompositionTime
            _pSPSPPS[3] = 0; //CompositionTime
            _pSPSPPS[4] = 0; //CompositionTime
            _pSPSPPS[5] = 1; //version
            _pSPSPPS[9] = 0xff; //6 bits reserved (111111) + 2 bits nal size length - 1 (11)
            _pSPSPPS[10] = 0xe1; //3 bits reserved (111) + 5 bits number of sps (00001)
        }

        public override void SignalAttachedToInStream()
        {
            if (InStream == null) return;
            if (InStream.Type.TagKindOf(StreamTypes.ST_IN_NET_RTP))
            {
                _videoCodecSent = Capabilities.VideoCodecId != VideoCodec.H264;
            }
            base.SignalAttachedToInStream();
        }

        public override bool FeedData(Stream pData, uint dataLength, uint processedLength, uint totalLength, uint absoluteTimestamp,
            bool isAudio) => isAudio ? FeedAudioData(pData, dataLength, absoluteTimestamp) : FeedVideoData(pData, dataLength, absoluteTimestamp);

        private bool FeedVideoData(Stream pData, uint dataLength, uint absoluteTimestamp)
        {
            var nalType = (NaluType)pData.ReadByte();
            var oldPosition = pData.Position;
            switch (nalType)
            {
               case NaluType.NALU_TYPE_SPS:
                    //1. Prepare the SPS part from video codec
                    if (dataLength > 128)
                    {
                        FATAL("SPS too big");
                        return false;
                    }
                    pData.Read(_pSPSPPS, 6, 3);
                    _pSPSPPS.Write(11,(ushort)dataLength);
                    pData.Position = oldPosition;
                    pData.Read(_pSPSPPS, 13, (int) dataLength);
                    _PPSStart = 13 + dataLength;
                    _spsAvailable = true;
                    pData.Position = oldPosition;
                    return true;
              case NaluType.NALU_TYPE_PPS:
                    //2. Prepare the PPS part from video codec
                    if (dataLength > 128)
                    {
                        FATAL("PPS too big");
                        return false;
                    }
                    if (!_spsAvailable)
                    {
                        WARN("No SPS available yet");
                        return true;
                    }
                    _pSPSPPS[_PPSStart] = 1;
         
                    _pSPSPPS.Write((int) (_PPSStart+1), (ushort)dataLength);
                    pData.Position = oldPosition;
                    pData.Read(_pSPSPPS, (int)(_PPSStart + 3), (int)dataLength);
                    _spsAvailable = false;
                    //3. Send the video codec
                    if (!base.FeedData(
                            new MemoryStream(_pSPSPPS), //pData
                            _PPSStart + 1 + 2 + dataLength, //dataLength
                            0, //processedLength
                            _PPSStart + 1 + 2 + dataLength, //totalLength
                            absoluteTimestamp, //absoluteTimestamp
                            false //isAudio
                            ))
                    {
                        FATAL("Unable to send video codec setup");
                        return false;
                    }
                    _videoCodecSent = true;
                    pData.Position = oldPosition;
                    return true;
                default:
                    //1. Create timestamp reference
                    if (_lastVideoTimestamp < 0)
                        _lastVideoTimestamp = absoluteTimestamp;
                    //2. Send over the accumulated stuff if this is a new packet from a
                    //brand new sequence of packets
                    if (_lastVideoTimestamp != absoluteTimestamp)
                    {
                        if (!base.FeedData(
                                _videoBuffer, //pData
                                (uint) _videoBuffer.GetAvaliableByteCounts(), //dataLength
                                0, //processedLength
                                (uint) _videoBuffer.GetAvaliableByteCounts(), //totalLength
                                (uint) _lastVideoTimestamp, //absoluteTimestamp
                                false //isAudio
                                ))
                        {
                            FATAL("Unable to send video");
                            return false;
                        }
                        _videoBuffer.IgnoreAll();
                        _isKeyFrame = false;
                    }
                    _lastVideoTimestamp = absoluteTimestamp;
                    //put the 5 bytes header
                    if (_videoBuffer.GetAvaliableByteCounts() == 0)
                    {
                        _videoBuffer.WriteByte(0);
                        _videoBuffer.WriteByte(1);
                        _videoBuffer.WriteByte(0);
                        _videoBuffer.WriteByte(0);
                        _videoBuffer.WriteByte(0);
                    }
                    if (nalType == NaluType.NALU_TYPE_IDR || nalType == NaluType.NALU_TYPE_SLICE ||
                        nalType == NaluType.NALU_TYPE_SEI)
                    {//put the length
                        _videoBuffer.Write(dataLength);
                        //put the data
                        pData.CopyTo(_videoBuffer);
                        //setup the frame type
                        _isKeyFrame |= (nalType == NaluType.NALU_TYPE_IDR);
                        var temp = _videoBuffer.GetBuffer();
                        temp[0] = (byte) (_isKeyFrame? 0x17: 0x27);

                    }//6. make sure the packet doesn't grow too big
                    if (_videoBuffer.GetAvaliableByteCounts() >= 4*1024*1024)
                    {
                        WARN("Big video frame. Discard it");
                        _videoBuffer.IgnoreAll();
                        _isKeyFrame = false;
                        _lastVideoTimestamp = -1;
                    }
                    pData.Position = oldPosition;
                    return true;
            }

        }

        private bool FeedAudioData(Stream pData, uint dataLength, uint absoluteTimestamp)
        {
            var oldPosition = pData.Position;
            if (!_videoCodecSent) return true;
            //the payload here respects this format:
            //6.2  Audio Data Transport Stream, ADTS
            //iso13818-7 page 26/206

            //1. Send the audio codec setup if necessary
            if (!_audioCodecSent)
            {
                if (Capabilities.AudioCodecId == AudioCodec.Aac)
                {
                    var codecSetup = Utils.Rms.GetStream();
                    codecSetup.WriteByte(0xaf);
                    codecSetup.WriteByte(0x00);
                    codecSetup.Write(Capabilities.Aac._pAAC,0,(int) Capabilities.Aac._aacLength);
                    codecSetup.Position = 0;
                    if (
                        !base.FeedData(codecSetup, (uint) codecSetup.Length, 0, (uint) codecSetup.Length,
                            absoluteTimestamp, true))
                    {
                        FATAL("Unable to send audio codec setup");
                        return false;
                    }
                }
                _audioCodecSent = true;
            }
            if (InStream.Type.TagKindOf(StreamTypes.ST_IN_NET_RTP))
            {
                var codec = (byte) Capabilities.AudioCodecId;
                codec = (byte) (codec << 4 | 0x0f);
                pData.Position = oldPosition;
                pData.WriteByte(codec);
                pData.WriteByte(0x01);
                pData.Position = oldPosition;
                return base.FeedData(
                    pData, //pData
                    dataLength, //dataLength
                    0, //processedLength
                    dataLength, //totalLength
                    absoluteTimestamp, //absoluteTimestamp
                    true //isAudio
                );
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
