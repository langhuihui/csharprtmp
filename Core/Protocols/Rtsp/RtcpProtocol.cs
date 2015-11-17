using System.Net;
using System.Net.Sockets;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.Protocols.Rtsp
{
    [ProtocolType(ProtocolTypes.PT_RTCP)]
    [AllowFarTypes(ProtocolTypes.PT_UDP)]
    public class RtcpProtocol:BaseProtocol
    {
        private byte[] _buff =new byte[32];
        private bool _isAudio;
        private uint _ssrc;
        private bool _validLastAddress;
        private IPEndPoint _lastAddress;
        private uint _lsr;
        private InboundConnectivity _pConnectivity;

        public RtcpProtocol()
        {
            _buff[0] = 0x81; //V,P,RC
            _buff[1] = 0xc9; //PT
            _buff[2] = 0x00; //length
            _buff[3] = 0x07; //length
            _buff.Write(4,Id); //SSRC of packet sender
            _ssrc = (uint)Utils.Random.Next();
            _ssrc ^= Id;
        }

        public IPEndPoint LastAddress => _validLastAddress ? _lastAddress : null;
        public uint SSRC => _ssrc;
        public uint LastSenderReport => _lsr;

        public void SetInbboundConnectivity(InboundConnectivity pConnectivity, bool isAudio)
        {
            _pConnectivity = pConnectivity;
            _isAudio = isAudio;
        }
        public override bool SignalInputData(int recAmount)
        {
            return true;
            //return SignalInputData(_input, _lastAddress);
        }

        public override bool SignalInputData(InputStream inputStream, IPEndPoint address)
        {
            if (address != _lastAddress)
            {
                _lastAddress = address;
                _validLastAddress = true;
            }
            var bufferLength = inputStream.AvaliableByteCounts;
            var pos = inputStream.Position;
            //1. Parse the SR
            if (bufferLength < 16) return true;
            inputStream.Reader.ReadByte();
            var PT = inputStream.Reader.ReadByte();
            var len = inputStream.Reader.ReadUInt16();
            len = (ushort) ((len + 1) * 4);
            if (len > bufferLength)
            {
                inputStream.IgnoreAll();
                return true;
            }
            switch (PT)
            {
                case 200:
                    if (len < 28)
                    {
                        Logger.WARN("Invalid RTCP packet length: {0}", len);
                        inputStream.IgnoreAll();
                        return true;
                    }
                    inputStream.Reader.ReadUInt32();
                    var ntpSec = inputStream.Reader.ReadUInt32()- 2208988800U;
                    var ntpFrac = inputStream.Reader.ReadUInt32();
                    ulong ntpMicroseconds = (ulong)((ntpFrac / (double)(0x100000000L))*1000000.0);
                    ntpMicroseconds += ((ulong)ntpSec) * 1000000;
                    var rtpTimestamp = inputStream.Reader.ReadUInt32();
                    _pConnectivity.ReportSR(ntpMicroseconds, rtpTimestamp, _isAudio);
                    break;
                default:
                    inputStream.IgnoreAll();
                    return true;
            }
            inputStream.Position = pos + 10;
            _lsr = inputStream.Reader.ReadUInt32();
            inputStream.IgnoreAll();
            //2. Send the RR
            if (_pConnectivity == null)
            {
                Logger.FATAL("no connectivity");
                return false;
            }
            if (!_pConnectivity.SendRR(_isAudio))
            {
                Logger.FATAL("Unable to send RR");
                _pConnectivity.EnqueueForDelete();
                _pConnectivity = null;
                return false;
            }

            return true;
        }
    }
}