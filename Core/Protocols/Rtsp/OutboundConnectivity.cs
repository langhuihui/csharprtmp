using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using CSharpRTMP.Common;
using CSharpRTMP.Core.NetIO;
using static CSharpRTMP.Common.Logger;
namespace CSharpRTMP.Core.Protocols.Rtsp
{
    public struct RTPClient
    {
        public uint protocolId;
        public bool isUdp;

        public bool hasAudio;
        public IPEndPoint audioDataAddress;
        public IPEndPoint audioRtcpAddress;
        public uint audioPacketsCount;
        public uint audioBytesCount;
        public uint audioStartRTP;
        public double audioStartTS;
        public byte audioDataChannel;
        public byte audioRtcpChannel;

        public bool hasVideo;
        public IPEndPoint videoDataAddress;
        public IPEndPoint videoRtcpAddress;
        public uint videoPacketsCount;
        public uint videoBytesCount;
        public uint videoStartRTP;
        public double videoStartTS;
        public byte videoDataChannel;
        public byte videoRtcpChannel;

        static RTPClient CreateNew()
        {
            RTPClient result;
            result.protocolId = 0;
            result.isUdp = false;
            result.hasAudio = false;
            result.audioPacketsCount = 0;
            result.audioBytesCount = 0;
            result.audioStartRTP = 0xffffffff;
            result.audioStartTS = -1;
            result.audioDataChannel = 0xff;
            result.audioRtcpChannel = 0xff;
            result.hasVideo = false;
            result.videoPacketsCount = 0;
            result.videoBytesCount = 0;
            result.videoStartRTP = 0xffffffff;
            result.videoStartTS = -1;
            result.videoDataChannel = 0xff;
            result.videoRtcpChannel = 0xff;
            result.audioDataAddress = null;
            result.audioRtcpAddress = null;
            result.videoDataAddress = null;
            result.videoRtcpAddress = null;
            return result;
        }
    };

    public class OutboundConnectivity : BaseConnectivity
    {
        private bool _forceTcp;
        private RtspProtocol _rtspProtocol;
        public BaseOutNetRTPUDPStream OutStream;
        private MsgHdr _dataMessage;
        private MsgHdr _rtcpMessage;
        private BufferWithOffset _rtcpNTP;
        private BufferWithOffset _rtcpRTP;
        private BufferWithOffset _rtcpSPC;
        private BufferWithOffset _rtcpSOC;
        private DateTime _startupTime;
        private RTPClient _rtpClient;
        private bool _hasVideo;
        private Socket _videoDataSocket;
        private int _videoDataPort;
        private Socket _videoRTCPSocket;
        private int _videoRTCPPort;
        private NATTraversalProtocol _videoNATData;
        private NATTraversalProtocol _videoNATRTCP;
        private bool _hasAudio;
        private Socket _audioDataSocket;
        private int _audioDataPort;
        private Socket _audioRTCPSocket;
        private int _audioRTCPPort;
        private NATTraversalProtocol _audioNATData;
        private NATTraversalProtocol _audioNATRTCP;
        private uint _dummyValue;

        public OutboundConnectivity(bool forceTcp, RtspProtocol pRTSPProtocol)
        {
            _forceTcp = forceTcp;
            _rtspProtocol = pRTSPProtocol;
            _dataMessage = new MsgHdr();
            _rtcpMessage = new MsgHdr {Buffers = new[] {new byte[28]}};
            _rtcpMessage.Buffers[0][0] = 0x80;
            _rtcpMessage.Buffers[0][1] = 0xc8;
            _rtcpMessage.Buffers[0].Write(2, (ushort) 6);
            _rtcpNTP = new BufferWithOffset(_rtcpMessage.Buffers[0], 8);
            _rtcpRTP = new BufferWithOffset(_rtcpMessage.Buffers[0], 16);
            _rtcpSPC = new BufferWithOffset(_rtcpMessage.Buffers[0], 20);
            _rtcpSOC = new BufferWithOffset(_rtcpMessage.Buffers[0], 24);
            _startupTime = DateTime.Now;
        }

        public bool Initialize()
        {
            if (_forceTcp)
            {
                _rtpClient.audioDataChannel = 0;
                _rtpClient.audioRtcpChannel = 1;
                _rtpClient.videoDataChannel = 2;
                _rtpClient.videoRtcpChannel = 3;
            }
            else
            {
                if (!InitializePorts(out _videoDataSocket,out _videoDataPort, ref _videoNATData,
                        out _videoRTCPSocket,out _videoRTCPPort, ref _videoNATRTCP))
                {
                    FATAL("Unable to initialize video ports");
                    return false;
                }
                if (!InitializePorts(out _audioDataSocket, out _audioDataPort, ref _audioNATData,
                        out _audioRTCPSocket, out _audioRTCPPort, ref _audioNATRTCP))
                {
                    FATAL("Unable to initialize audio ports");
                    return false;
                }
            }
            return true;
        }

        public string VideoPorts => $"{_videoDataPort}-{_videoRTCPPort}";
        public string AudioPorts => $"{_audioDataPort}-{_audioRTCPPort}";
        public string VideoChannels=> $"{_rtpClient.videoDataChannel}-{_rtpClient.videoRtcpChannel}";
        public string AudioChannels => $"{_rtpClient.audioDataChannel}-{_rtpClient.audioRtcpChannel}";
        public uint AudioSSRC => OutStream?.AudioSSRC ?? 0;
        public uint VideoSSRC => OutStream?.VideoSSRC ?? 0;
        public ushort LastVideoSequence => OutStream.VideoCounter;
        public ushort LastAudioSequence => OutStream.AudioCounter;

        public bool HasAudio
        {
            set
            {
                _hasVideo = value;
                OutStream.HasAudioVideo(_hasAudio,_hasVideo);
            }
        }
        public bool HasVideo
        {
            set
            {
                _hasVideo = value;
                OutStream.HasAudioVideo(_hasAudio, _hasVideo);
            }
        }

        public bool RegisterUDPVideoClient(uint rtspProtocolId, IPEndPoint data, IPEndPoint rtcp)
        {
            if (_rtpClient.hasVideo)
            {
                FATAL("Client already registered for video feed");
                return false;
            }
            _rtpClient.hasVideo = true;
            _rtpClient.isUdp = true;
            _rtpClient.videoDataAddress = data;
            _rtpClient.videoRtcpAddress = rtcp;
            _rtpClient.protocolId = rtspProtocolId;
            _videoNATData.OutboundAddress = _rtpClient.videoDataAddress;
            _videoNATRTCP.OutboundAddress = _rtpClient.videoRtcpAddress;
            return ((UDPCarrier) _videoNATData.IOHandler).StartAccept()
            &&((UDPCarrier)_videoNATRTCP.IOHandler).StartAccept();
        }


        public bool RegisterUDPAudioClient(uint rtspProtocolId, IPEndPoint data, IPEndPoint rtcp)
        {
            if (_rtpClient.hasAudio)
            {
                FATAL("Client already registered for audio feed");
                return false;
            }
            _rtpClient.hasAudio = true;
            _rtpClient.isUdp = true;
            _rtpClient.audioDataAddress = data;
            _rtpClient.audioRtcpAddress = rtcp;
            _rtpClient.protocolId = rtspProtocolId;
            _audioNATData.OutboundAddress = _rtpClient.audioDataAddress;
            _audioNATRTCP.OutboundAddress = _rtpClient.audioRtcpAddress;
            return ((UDPCarrier)_audioNATData.IOHandler).StartAccept()
            && ((UDPCarrier)_audioNATRTCP.IOHandler).StartAccept();
        }

        public bool RegisterTCPVideoClient(uint rtspProtocolId, byte data, byte rtcp)
        {
            if (_rtpClient.hasVideo)
            {
                FATAL("Client already registered for video feed");
                return false;
            }
            _rtpClient.hasVideo = true;
            _rtpClient.isUdp = false;
            _rtpClient.videoDataChannel = data;
            _rtpClient.videoRtcpChannel = rtcp;
            _rtpClient.protocolId = rtspProtocolId;
            return true;
        }
        public bool RegisterTCPAudioClient(uint rtspProtocolId, byte data, byte rtcp)
        {
            if (_rtpClient.hasAudio)
            {
                FATAL("Client already registered for audio feed");
                return false;
            }
            _rtpClient.hasAudio = true;
            _rtpClient.isUdp = false;
            _rtpClient.audioDataChannel = data;
            _rtpClient.audioRtcpChannel = rtcp;
            _rtpClient.protocolId = rtspProtocolId;
            return true;
        }

        public bool FeedVideoData(ref MsgHdr message, double absoluteTimestamp)
        {
            if (!FeedData(ref message, absoluteTimestamp, false))
            {
                FATAL("Unable to feed video UDP clients");
                return false;
            }
            return true;
        }
        public bool FeedAudioData(ref MsgHdr message,double absoluteTimestamp)
        
        {
            if (!FeedData(ref message, absoluteTimestamp, true))
            {
                FATAL("Unable to feed audio UDP clients");
                return false;
            }
            return true;
        }

        public bool InitializePorts(out Socket socket,out int port,ref NATTraversalProtocol ppNATData,out Socket rtcp,out int rtcpPort,ref NATTraversalProtocol ppNATRTCP)
        {
            UDPCarrier carrier1 = null;
            UDPCarrier carrier2 = null;
            for (int i = 0; i < 10; i++)
            {
                if (carrier1 != null)
                {
                    carrier1.Dispose();
                    carrier1 = null;
                }
                if (carrier2 != null)
                {
                    carrier2.Dispose();
                    carrier2 = null;
                }

                carrier1 = UDPCarrier.Create("0.0.0.0", 0);
                if (carrier1 == null)
                {
                    WARN("Unable to create UDP carrier for RTP");
                    continue;
                }

                carrier2 = (carrier1.NearPort % 2) == 0 ? UDPCarrier.Create("0.0.0.0", carrier1.NearPort + 1) : UDPCarrier.Create("0.0.0.0", carrier1.NearPort - 1);

                if (carrier2 == null)
                {
                    WARN("Unable to create UDP carrier for RTCP");
                    continue;
                }

                if (carrier1.NearPort > carrier2.NearPort)
                {
                    WARN("Switch carriers");
                    UDPCarrier pTemp = carrier1;
                    carrier1 = carrier2;
                    carrier2 = pTemp;
                }

                Variant dummy = Variant.Get();
                socket = carrier1.Socket;
                port = carrier1.NearPort;
                ppNATData = (NATTraversalProtocol)ProtocolFactoryManager.CreateProtocolChain(Defines.CONF_PROTOCOL_RTP_NAT_TRAVERSAL, dummy);
                if (ppNATData == null)
                {
                    rtcp = null;
                    rtcpPort = 0;
                    FATAL("Unable to create the protocol chain {0}", Defines.CONF_PROTOCOL_RTP_NAT_TRAVERSAL);
                    return false;
                }
                carrier1.Protocol = ppNATData.FarEndpoint;
                ppNATData.FarEndpoint.IOHandler = carrier1;
                rtcp = carrier2.Socket;
                rtcpPort = carrier2.NearPort;
                ppNATRTCP = (NATTraversalProtocol)ProtocolFactoryManager.CreateProtocolChain(Defines.CONF_PROTOCOL_RTP_NAT_TRAVERSAL, dummy);
                if (ppNATRTCP == null)
                {
                    FATAL("Unable to create the protocol chain {0}", Defines.CONF_PROTOCOL_RTP_NAT_TRAVERSAL);
                    ppNATData.EnqueueForDelete();
                    return false;
                }
                carrier2.Protocol = ppNATRTCP.FarEndpoint;
                ppNATRTCP.FarEndpoint.IOHandler = carrier2;
                return true;
            }
            if (carrier1 != null)
            {
                carrier1.Dispose();
                carrier1 = null;
            }
            if (carrier2 != null)
            {
                carrier2.Dispose();
                carrier2 = null;
            }
            socket = null;
            port = 0;
            rtcp = null;
            rtcpPort = 0;
            return false;
        }

        public void SignalDetachedFromInStream()
        {
            var protocol = ProtocolManager.GetProtocol(_rtpClient.protocolId);
            protocol?.EnqueueForDelete();
            _rtspProtocol = null;
        }

        public bool FeedData(ref MsgHdr message, double absoluteTimestamp, bool isAudio)
        {
            if (absoluteTimestamp == 0)return true;
                
            double rate = isAudio ? OutStream.Capabilities.Samplerate : 90000.0;
            var ssrc = isAudio ? OutStream.AudioSSRC : OutStream.VideoSSRC;
            var messageLength = message.Buffers.Sum(t => t.Length);

            if (!_rtpClient.hasAudio &&!_rtpClient.hasVideo) return true;
            var packetsCount = isAudio ? _rtpClient.audioPacketsCount : _rtpClient.videoPacketsCount;
            var bytesCount = isAudio ? _rtpClient.audioBytesCount : _rtpClient.videoBytesCount;
            var startRTP = isAudio ? _rtpClient.audioStartRTP : _rtpClient.videoStartRTP;

            if (startRTP == 0xffffffff)
            {
                startRTP = message.Buffers[0].ReadUInt(4);
                if (isAudio) _rtpClient.audioStartRTP = startRTP;
                else _rtpClient.videoStartRTP = startRTP;

                if (isAudio) _rtpClient.audioStartTS = absoluteTimestamp;
                else _rtpClient.videoStartTS = absoluteTimestamp;
            }

            if ((packetsCount % 500) == 0)
            {
                //FINEST("Send %c RTCP: %u", isAudio ? 'A' : 'V', packetsCount);
                _rtcpMessage.Buffers[0].Write(4,ssrc);
                //NTP
                var integerValue = (uint)(absoluteTimestamp / 1000.0);
                double fractionValue = (absoluteTimestamp / 1000.0 - ((uint)(absoluteTimestamp / 1000.0))) * 4294967296.0;
                var ntpVal = (ulong)(_startupTime.SecondsFrom1970() + integerValue + 2208988800UL) << 32;
                ntpVal |= (uint)fractionValue;
                _rtcpNTP.Buffer.Write(_rtcpNTP.Offset,ntpVal);
        
                //RTP
                var rtp = (ulong)((integerValue + fractionValue / 4294967296.0) * rate);
                rtp &= 0xffffffff;
                _rtcpRTP.Buffer.Write(_rtcpRTP.Offset, rtp);
                //packet count
                _rtcpSPC.Buffer.Write(_rtcpSPC.Offset, packetsCount);
                _rtcpSOC.Buffer.Write(_rtcpSOC.Offset, bytesCount);
                //octet count
                //			FINEST("\n%s", STR(IOBuffer::DumpBuffer(((uint8_t *) _rtcpMessage.MSGHDR_MSG_IOV[0].IOVEC_IOV_BASE),
                //					_rtcpMessage.MSGHDR_MSG_IOV[0].IOVEC_IOV_LEN)));

                if (_rtpClient.isUdp)
                {
                    var rtcpSocket = isAudio ? _audioRTCPSocket : _videoRTCPSocket;
                    var rtcpAddress = isAudio ? _rtpClient.audioRtcpAddress : _rtpClient.videoRtcpAddress;

                    if (rtcpSocket.SendTo(_rtcpMessage.TotalBuffer, SocketFlags.None, rtcpAddress) < 0)
                    {
                        FATAL("Unable to send message");
                        return false;
                    }
                }
                else
                {
                    if (_rtspProtocol != null)
                    {
                        if (!_rtspProtocol.SendRaw(_rtcpMessage,ref _rtpClient, isAudio, false))
                        {
                            FATAL("Unable to send raw rtcp audio data");
                            return false;
                        }
                    }
                }
            }


            if (_rtpClient.isUdp)
            {
                var dataFd = isAudio ? _audioDataSocket : _videoDataSocket;
                var dataAddress = isAudio ? _rtpClient.audioDataAddress : _rtpClient.videoDataAddress;
                
                if ( dataFd.SendTo(message.TotalBuffer, SocketFlags.None, dataAddress) < 0)
                {
                    FATAL("Unable to send message");
                    return false;
                }
            }
            else
            {
                if (_rtspProtocol != null)
                {
                    if (!_rtspProtocol.SendRaw(message,ref _rtpClient,isAudio, true))
                    {
                        FATAL("Unable to send raw rtcp audio data");
                        return false;
                    }
                }
            }

            packetsCount++;
            if (isAudio) _rtpClient.audioPacketsCount = packetsCount;
            else _rtpClient.videoPacketsCount = packetsCount;
            bytesCount += (uint)messageLength;
            if (isAudio) _rtpClient.audioBytesCount = bytesCount;
            else _rtpClient.videoBytesCount = bytesCount;
            return true;

        }
    }
}