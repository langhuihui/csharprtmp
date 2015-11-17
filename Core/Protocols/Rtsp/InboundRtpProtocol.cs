using System.Diagnostics;
using System.Net;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.Protocols.Rtsp
{
    public struct RTPHeader
    {
        public uint Flags;
        public uint Timestamp;
        public uint SSRC;
        public override string ToString() => $"f: {Flags}; V: {Flags>>30}; P: {(Flags >> 29)&1}; X: {(Flags >> 28) & 1}; CC: {(Flags >> 24) & 0x0f}; M: {(Flags >> 23) & 1}; PT: {(byte)((Flags >> 16) & 0x7f)}; SEQ: {Flags & 0xffff}; TS: {Timestamp}; SSRC: {SSRC}";
        public ushort SEQ => (ushort) Flags;
        public byte CC => (byte)((Flags >> 24) & 0x0f) ;
        public bool M => (byte) ((Flags >> 23) & 1) != 0;
    }
    [ProtocolType(ProtocolTypes.PT_INBOUND_RTP)]
    [AllowFarTypes(ProtocolTypes.PT_RTSP,ProtocolTypes.PT_UDP)]
    public class InboundRtpProtocol:BaseProtocol
    {
        private InNetRTPStream _pInStream;
        private byte _spsPpsPeriod;
        private ushort _lastSeq;
        private ushort _seqRollOver;
        private bool _isAudio;
        private uint _packetsCount;
        private RTPHeader _rtpHeader;
#if RTP_DETECT_ROLLOVER
        _lastTimestamp = 0;
	_timestampRollover = 0;
#endif
        public InboundRtpProtocol()
        {
            _spsPpsPeriod = 0;
        
            _lastSeq = 0;
            _seqRollOver = 0;
            _isAudio = false;
            _packetsCount = 0;
        }

        public override bool SignalInputData(InputStream inputStream, IPEndPoint address)
        {
            var length = inputStream.AvaliableByteCounts;
            if (length < 12)
            {
                inputStream.IgnoreAll();
                return true;
            }

            _rtpHeader.Flags = inputStream.Reader.ReadUInt32();
            _rtpHeader.Timestamp = inputStream.Reader.ReadUInt32();
            _rtpHeader.SSRC = inputStream.Reader.ReadUInt32();
            if (_rtpHeader.SEQ < _lastSeq)
            {
                if (_lastSeq - _rtpHeader.SEQ > 0xff)
                {
                    _seqRollOver++;
                    _lastSeq = _rtpHeader.SEQ;
                }
                else
                {
                    inputStream.IgnoreAll();
                    return true;
                }
            }
            else
            {
                _lastSeq = _rtpHeader.SEQ;
            }
            if (length < 12 + _rtpHeader.CC*4 + 1)
            {
                inputStream.IgnoreAll();
                return true;
            }

#if RTP_DETECT_ROLLOVER
            if (_rtpHeader._timestamp < _lastTimestamp)
            {
                if ((((_rtpHeader._timestamp & 0x80000000) >> 31) == 0)
                        && (((_lastTimestamp & 0x80000000) >> 31) == 1))
                {
                    _timestampRollover++;
                    _lastTimestamp = _rtpHeader._timestamp;
                }
            }
            else
            {
                _lastTimestamp = _rtpHeader._timestamp;
            }
            _rtpHeader._timestamp = (_timestampRollover << 32) | _rtpHeader._timestamp;
#endif
            if (_pInStream != null)
            {
                if (_isAudio)
                {
                    if (!_pInStream.FeedAudioData(inputStream, length,ref _rtpHeader))
                    {
                        Logger.FATAL("Unable to stream data");
                        if (InboundConnectivity != null)
                        {
                            InboundConnectivity.EnqueueForDelete();
                            InboundConnectivity = null;
                        }
                        return false;
                    }
                }
                else
                {
                    if (!_pInStream.FeedVideoData(inputStream, length,ref _rtpHeader))
                    {
                        Logger.FATAL("Unable to stream data");
                        if (InboundConnectivity != null)
                        {
                            InboundConnectivity.EnqueueForDelete();
                            InboundConnectivity = null;
                        }
                        return false;
                    }
                }
            }
            //6. Ignore the data
            inputStream.IgnoreAll();

            //7. Increment the packets count
            _packetsCount++;


            //8. Send the RR if necesary
            if ((_packetsCount % 300) == 0)
            {

                if (InboundConnectivity != null)
                {
                    if (!InboundConnectivity.SendRR(_isAudio))
                    {
                        Logger.FATAL("Unable to send RR");
                        InboundConnectivity.EnqueueForDelete();
                        InboundConnectivity = null;
                        return false;
                    }
                }
            }

            //7. Done
            return true;
        }

        public uint SSRC => _rtpHeader.SSRC;
        public uint ExtendedSeq =>( ((uint) _seqRollOver) << 16) | _lastSeq;

        public void SetStream(InNetRTPStream inStream, bool isAudio)
        {
            _pInStream = inStream;
            _isAudio = isAudio;
        }

        public InboundConnectivity InboundConnectivity;
    }
}