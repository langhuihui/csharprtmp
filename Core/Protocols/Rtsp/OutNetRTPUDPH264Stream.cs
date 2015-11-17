using System;
using System.IO;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Streaming;
using static CSharpRTMP.Common.Logger;
using static CSharpRTMP.Core.Streaming.StreamTypes;

namespace CSharpRTMP.Core.Protocols.Rtsp
{
    public class OutNetRTPUDPH264Stream:BaseOutNetRTPUDPStream
    {
        private bool _forceTcp;
        private uint _maxRTPPacketSize;
        public const uint MAX_RTP_PACKET_SIZE =  1350;
        private MsgHdr _videoData;
        private byte[] _sps;
        private int _spsLen;
        private byte[] _pps;
        private int _ppsLen;
        private MemoryStream _videoBuffer = Utils.Rms.GetStream();
        private MemoryStream _audioBuffer = Utils.Rms.GetStream();
        private MsgHdr _audioData;

        private ulong _audioPacketsCount;
        private ulong _audioDroppedPacketsCount;
        private ulong _audioBytesCount;

        private ulong _videoPacketsCount;
        private ulong _videoDroppedPacketsCount;
        private ulong _videoBytesCount;
        private uint _videoSsrc;
        private ushort _videoCounter;

        public OutNetRTPUDPH264Stream(RtspProtocol pProtocol, StreamsManager pStreamsManager, string name, bool forceTcp) : base(pProtocol, pStreamsManager, name)
        {
            _forceTcp = forceTcp;
            _maxRTPPacketSize = _forceTcp ? 1500 : MAX_RTP_PACKET_SIZE;
            _videoData = new MsgHdr { Buffers = new[] { new byte[14],new byte[0],  } };
            _videoData.Buffers[0][0] = 0x80;
            _videoData.Buffers[0].Write(8,VideoSSRC);
            _audioData = new MsgHdr { Buffers = new[] { new byte[14],new byte[16], null } };
            _audioData.Buffers[0][0] = 0x80;
            _audioData.Buffers[0][1] = 0xe0;
            _audioData.Buffers[0].Write(8, AudioSSRC);

        }

        public override void GetStats(Variant info, uint namespaceId)
        {
            info["audio","bytesCount"] = _audioBytesCount;
            info["audio","packetsCount"] = _audioPacketsCount;
            info["audio","droppedPacketsCount"] = _audioDroppedPacketsCount;
            info["video","bytesCount"] = _videoBytesCount;
            info["video","packetsCount"] = _videoPacketsCount;
            info["video","droppedPacketsCount"] = _videoDroppedPacketsCount;
        }

        protected override bool FeedDataVideo(Stream pData, uint dataLength, uint processedLength, uint totalLength, uint absoluteTimestamp,
            bool isAudio)
        {
            var pos = pData.Position;
            _videoBytesCount += dataLength;
            _videoPacketsCount++;
            //1. Test and see if this is an inbound RTMP stream. If so,
            //we have to strip out the RTMP 9 bytes header
            var inStreamType = InStream.Type;
            if ((inStreamType == ST_IN_NET_RTMP)
                    || (inStreamType == ST_IN_NET_LIVEFLV))
            {
                //2. Test and see if we have a brand new packet
                if (processedLength == 0)
                {
                    //3.This must be a payload packet, not codec setup
                    pData.ReadByte();

                    if (pData.ReadByte() != 1)
                        return true;
                    //4. since this is a brand new packet, empty previous buffer
                    _videoBuffer.IgnoreAll();
                    pData.Position -= 2;
                }

                //5. Store the data into the buffer
                pData.CopyPartTo(_videoBuffer,(int) dataLength);

                //6. Test and see if this is the last chunk of the RTMP packet
                if (dataLength + processedLength == totalLength)
                {
                    //7. This is the last chunk. Get the pointer and length
                    pData = _videoBuffer;
                    pData.Position = 0;
                    dataLength = (uint) _videoBuffer.GetAvaliableByteCounts();

                    //8. We must have at least 9 bytes (RTMP header size)
                    if (dataLength < 9)
                    {
                        WARN("Bogus packet");
                        return true;
                    }

                    //9. Read the composition timestamp and add it to the
                    //absolute timestamp
                    pData.Position = 1;
                    var compositionTimeStamp = (pData.ReadUInt()) & 0x00ffffff;
                    absoluteTimestamp += compositionTimeStamp;
                    
                    //10. Ignore RTMP header and composition offset

                    dataLength -= 5;

                    uint nalSize = 0;
                    //uint32_t tsIncrement = 0;

                    //11. Start looping over the RTMP payload. Each NAL has a 4 bytes
                    //header indicating the length of the following NAL
                    while (dataLength >= 4)
                    {
                        //12. Read the nal size and compare it to the actual amount
                        //of data remaining on the buffer
                        nalSize = pData.ReadUInt();
                        pos = pData.Position;
                        if (nalSize > (dataLength - 4))
                        {
                            WARN("Bogus packet");
                            return true;
                        }

                        //13. skip theNAL size field
                       
                        dataLength -= 4;

                        //14. Is this a 0 sized NAL? if so, skip it
                        if (nalSize == 0)
                            continue;

                        //15. Feed the NAL unit using RTP FUA
                        if (!FeedDataVideoFUA(pData, nalSize, 0, nalSize,
                                absoluteTimestamp))
                        { //+ (double) tsIncrement / 90000.00)) {
                            FATAL("Unable to feed data");
                            return false;
                        }
                        //16. move to the next NAL
               
                        dataLength -= nalSize;
                    }
                }
                return true;
            }
            else
            {
                //17. This is NAL stream. Feed it as it is
                return FeedDataVideoFUA(pData, dataLength, processedLength, totalLength,absoluteTimestamp);
                        
            }
        }

        private bool FeedDataVideoFUA(Stream pData, uint dataLength, uint processedLength, uint totalLength, uint absoluteTimestamp)
        {
            uint sentAmount = 0;
            while (sentAmount != dataLength)
            {
                var chunkSize = dataLength - sentAmount;
                chunkSize = chunkSize < _maxRTPPacketSize ? chunkSize : _maxRTPPacketSize;
                //1. Flags
                _videoData.Buffers[0][1] = processedLength + sentAmount + chunkSize == totalLength ?(byte) 0xe1 : (byte)0x61;
                //2. counter
                _videoData.Buffers[0].Write(2, _videoCounter);
                _videoCounter++;
                //3. Timestamp
                _videoData.Buffers[0].Write(4, BaseConnectivity.ToRTPTS(absoluteTimestamp, 90000));
                if (chunkSize == totalLength)
                {
                    //4. No chunking
                    Array.Resize(ref _videoData.Buffers[0], 12);
                    Array.Resize(ref _videoData.Buffers[1], (int) chunkSize);
                    pData.Read(_videoData.Buffers[1], 0, (int) chunkSize);
                }
                else
                {
                    //5. Chunking
                    Array.Resize(ref _videoData.Buffers[0], 14);
                    if (processedLength + sentAmount == 0)
                    {
                        //6. First chunk
                        var firstByte = (byte) pData.ReadByte();
                        _videoData.Buffers[0][12] = (byte) (firstByte & 0xe0 | (byte) NaluType.NALU_TYPE_FUA);
                        _videoData.Buffers[0][13] = (byte) (firstByte & 0x1f | 0x80);
                        Array.Resize(ref _videoData.Buffers[1], (int) chunkSize - 1);
                        pData.Read(_videoData.Buffers[1], 0, (int) chunkSize - 1);
                    }
                    else
                    {
                        _videoData.Buffers[0][13] &= 0x1f;
                        if (processedLength + sentAmount + chunkSize == totalLength)
                        {
                            //7. Last chunk
                            _videoData.Buffers[0][13] |= 0x40;
                        }
                        Array.Resize(ref _videoData.Buffers[1], (int) chunkSize);
                        pData.Read(_videoData.Buffers[1], 0, (int) chunkSize);
                    }
                }
                Connectivity.FeedVideoData(ref _videoData, absoluteTimestamp);
                sentAmount += chunkSize;
            }
            return true;
        }

        protected override bool FeedDataAudio(Stream pData, uint dataLength, uint processedLength, uint totalLength, uint absoluteTimestamp,bool isAudio)
        {
            _audioBytesCount += dataLength;
            _audioPacketsCount++;
            return FeedDataAudioMPEG4Generic(pData, dataLength, processedLength, totalLength,absoluteTimestamp);
        }

        private bool FeedDataAudioMPEG4Generic(Stream pData, uint dataLength, uint processedLength, uint totalLength, uint absoluteTimestamp)
        {
            //1. Take care of chunked content first
            //this will update pData and dataLength if necessary
            if (dataLength != totalLength)
            {
                //2. This is chunked content. Test if this is the first chunk from the
                //packet
                if (processedLength == 0)
                {
                    //3. This is the first chunk of the packet.
                    //Empty the old buffer and store this new chunk
                    _audioBuffer.IgnoreAll();
                    pData.CopyDataTo(_audioBuffer,(int) dataLength);
                    return true;
                }
                else
                {
                    //4. This is not the first chunk. Test to see if this is
                    //the last chunk or not
                    if (dataLength + processedLength < totalLength)
                    {
                        //5. This is not the last chunk of the packet.
                        //Test and see if we have any previous data inside the buffer
                        //if we don't, that means we didn't catch the beginning
                        //of the packet so we discard everything
                        if (_audioBuffer.GetAvaliableByteCounts() == 0) return true;

                        //6. Store the data
                        pData.CopyDataTo(_audioBuffer, (int) dataLength);
                        //7. Done
                        return true;
                    }
                    else
                    {
                        //8. This is the last chunk of the packet.
                        //Test and see if we have any previous data inside the buffer
                        //if we don't, that means we didn't catch the beginning
                        //of the packet so we discard everything
                        if (_audioBuffer.GetAvaliableByteCounts() == 0) return true;
                        //9. Store the data
                        pData.CopyDataTo(_audioBuffer, (int)dataLength);

                        //10. Get the buffer and its length
                        pData = _audioBuffer;
                        dataLength = (uint) _audioBuffer.GetAvaliableByteCounts();

                        //11. Do a final test and see if we have all the data
                        if (dataLength != totalLength)
                        {
                            FATAL("Invalid data length");
                            return false;
                        }

                    }
                }
            }
            var inStreamType = InStream.Type;

            if ((inStreamType == ST_IN_NET_RTMP)
            || (inStreamType == ST_IN_NET_RTP)
            || (inStreamType == ST_IN_NET_LIVEFLV))
            {
                //2. Do we have enough data to read the RTMP header?
                if (dataLength <= 2)
                {
                    WARN("Bogus AAC packet");
                    _audioBuffer.IgnoreAll();
                    return true;
                }
                var firstByte = pData.ReadByte();
                var secondByte = pData.ReadByte();
                //3. Take care of the RTMP headers if necessary
                if (inStreamType == ST_IN_NET_RTMP
                        || inStreamType == ST_IN_NET_LIVEFLV)
                {
                    //3. Is this a RTMP codec setup? If so, ignore it
                    if (secondByte != 1)
                    {
                        _audioBuffer.IgnoreAll();
                        return true;
                    }
                }

                //4. Skip the RTMP header
                dataLength -= 2;
                
            }

            //4. Do we have enough data to detect the ADTS header presence?
            if (dataLength <= 2)
            {
                WARN("Bogus AAC packet");
                _audioBuffer.IgnoreAll();
                return true;
            }

            //4. The packet might start with an ADTS header. Remove it if necessary
            var adtsHeaderLength = 0;

            var temp = pData.ReadUShort();
            if ((temp >> 3) == 0x1fff)
            {
                adtsHeaderLength = 7;
            }

            /*
0                   1                   2                   3
0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|V=2|P|X|  CC   |M|     PT      |       sequence number         |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                           timestamp                           |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|           synchronization source (SSRC) identifier            |
+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
|            contributing source (CSRC) identifiers             |
|                             ....                              |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+

+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+- .. -+-+-+-+-+-+-+-+-+-+
|AU-headers-length|AU-header|AU-header|      |AU-header|padding|
|                 |   (1)   |   (2)   |      |   (n)   | bits  |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+- .. -+-+-+-+-+-+-+-+-+-+
 */

            //5. counter
            _audioData.Buffers[0].Write(2, AudioCounter++);
            //6. Timestamp
            _audioData.Buffers[0].Write(4, absoluteTimestamp*Capabilities.Samplerate/1000);
            _audioData.Buffers[0].Write(12,(ushort)16);
            var auHeader = (dataLength - adtsHeaderLength) << 3;
           
            Array.Resize(ref _audioData.Buffers[1], 2);
            _audioData.Buffers[1].Write(0, (ushort)auHeader);
            //7. put the actual buffer
            _audioData.Buffers[2] = new byte[dataLength - adtsHeaderLength];
            pData.Position += adtsHeaderLength;
            pData.Read(_audioData.Buffers[2], 0, _audioData.Buffers[2].Length);
            
            if (!Connectivity.FeedAudioData(ref _audioData, absoluteTimestamp))
            {
                FATAL("Unable to feed data");
                _audioBuffer.IgnoreAll();
                return false;
            }
            _audioBuffer.IgnoreAll();
            return true;
        }

        private bool FeedDataAudioMPEG4Generic_aggregate(Stream pData, uint dataLength, uint processedLength, uint totalLength, uint absoluteTimestamp)
        {
            //1. We only support frame-by-frame approach
            if (dataLength != totalLength)
            {
                WARN("Chunked mode not yet supported");
                return true;
            }
            //2. Test if we need to send what we have so far
            if (((14 + _audioData.Buffers[1].Length + _audioBuffer.GetAvaliableByteCounts() + 2 + dataLength - 7) > _maxRTPPacketSize)
                    || (_audioData.Buffers[1].Length == 16))
            {
                //3. counter
                _audioData.Buffers[0].Write(2, AudioCounter);
                AudioCounter++;

                //4. Timestamp
                _audioData.Buffers[0].Write(4,
                        BaseConnectivity.ToRTPTS(absoluteTimestamp,
                        Capabilities.Aac._sampleRate));

                //6. put the actual buffer
             
                Array.Resize(ref _audioData.Buffers[2], (int) _audioBuffer.GetAvaliableByteCounts());
                _audioBuffer.Read(_audioData.Buffers[2], 0, _audioData.Buffers[2].Length);
                _audioData.Buffers[0].Write(12, (ushort)(_audioData.Buffers[1].Length * 8));
              
                Connectivity.FeedAudioData(ref _audioData, absoluteTimestamp);
                Array.Resize(ref _audioData.Buffers[1],0);
            }
            //3. AU-Header
            var auHeader = (dataLength - 7) << 3;
            auHeader = auHeader | (byte)(_audioData.Buffers[1].Length/2);
            Array.Resize(ref _audioData.Buffers[1], _audioData.Buffers[1].Length + 2);
            _audioData.Buffers[1].Write(_audioData.Buffers[1].Length-2,(ushort)auHeader);
            //4. Save the buffer
            pData.Position += 7;
            pData.CopyDataTo(_audioBuffer,(int) (dataLength-7));
            pData.Position -= 7;
            return true;
        }

        public override void SignalAttachedToInStream()
        {
            var capablitities = Capabilities;
            _spsLen = capablitities.Avc.SpsLength + 12;
            _sps = new byte[_spsLen];
            _sps[0] = 0x80;
            _sps[1] = 0xE1;
            _sps.Write(8, _videoSsrc);
            Buffer.BlockCopy(capablitities.Avc.SPS,0,_sps,12, capablitities.Avc.SpsLength);
            _ppsLen = capablitities.Avc.PpsLength + 12;
            _pps = new byte[_ppsLen];
            _pps[0] = 0x80;
            _pps[1] = 0xE1;
            Buffer.BlockCopy(capablitities.Avc.PPS, 0, _pps, 12, capablitities.Avc.PpsLength);
        }
    }
}