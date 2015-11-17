using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting.Services;
using System.Security.Cryptography;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.MediaFormats;
using CSharpRTMP.Core.NetIO;
using CSharpRTMP.Core.Streaming;
using static CSharpRTMP.Common.Logger;

namespace CSharpRTMP.Core.Protocols.Rtsp
{
    public class InboundConnectivity: BaseConnectivity,IDisposable
    {
        private RtspProtocol _rtsp;
        private InboundRtpProtocol _rtpVideo;
        private RtcpProtocol _rtcpVideo;
        private InboundRtpProtocol _rtpAudio;
        private RtcpProtocol _rtcpAudio;
        private InNetRTPStream _inStream;
        private bool _forceTcp;
        private byte[] _audioRR = new byte[60];
        private byte[] _videoRR = new byte[60];
        private string _streamName;
        private uint _bandwidthHint;
        private TimeSpan _rtcpDetectionInterval;
        private Variant _audioTrack;
        private Variant _videoTrack;
        private Dictionary<uint,BaseProtocol> _protocols = new Dictionary<uint, BaseProtocol>();
        private InputStream _inputBuffer = new InputStream();
        private IPEndPoint _dummyAddress;

        public InboundConnectivity(RtspProtocol rtsp, string streamName, uint bandwidthHint, TimeSpan rtcpDetectionInterval)
        {
            _rtsp = rtsp;
            _audioRR[0] = (byte) '$'; //marker
            _audioRR[1] = 0; //channel
            _audioRR[2] = 0; //size
            _audioRR[3] = 56; //size
            _audioRR[4] = 0x81; //V,P,RC
            _audioRR[5] = 0xc9; //PT
            _audioRR[6] = 0x00; //length
            _audioRR[7] = 0x07; //length
            _audioRR.Write(16,0x00FFFFFF);

            _audioRR[36] = 0x81; //V,P,RC
            _audioRR[37] = 0xca; //PT
            _audioRR[38] = 0x00; //length
            _audioRR[39] = 0x05; //length
            _audioRR[44] = 0x01; //type
            _audioRR[45] = 0x0d; //length

            _audioRR.Write(46,"machine.local");

            _videoRR[0] = (byte) '$'; //marker
            _videoRR[1] = 0; //channel
            _videoRR[2] = 0; //size
            _videoRR[3] = 56; //size
            _videoRR[4] = 0x81; //V,P,RC
            _videoRR[5] = 0xc9; //PT
            _videoRR[6] = 0x00; //length
            _videoRR[7] = 0x07; //length
            _videoRR.Write(16, 0x00FFFFFF);

            _videoRR[36] = 0x81; //V,P,RC
            _videoRR[37] = 0xca; //PT
            _videoRR[38] = 0x00; //length
            _videoRR[39] = 0x05; //length
            _videoRR[44] = 0x01; //type
            _videoRR[45] = 0x0d; //length

            _videoRR.Write(46, "machine.local");

            _streamName = streamName;
            _bandwidthHint = bandwidthHint;
            _rtcpDetectionInterval = rtcpDetectionInterval;
        }

        public void EnqueueForDelete()
        {
            Cleanup();
            _rtsp.EnqueueForDelete();
        }

        public bool AddTrack(Variant track, bool isAduio)
        {
            var _track = isAduio ? _audioTrack : _videoTrack;
            var _oppositeTrack = isAduio ? _videoTrack : _audioTrack;
            
            var rr = isAduio ? _audioRR : _videoRR;
            if (_track != null) return false;
            var application = _rtsp.Application;
            if (application == null)
            {
                FATAL("RTSP protocol not yet assigned to an application");
                return false;
            }
            if (isAduio)  _audioTrack = track;
            else  _videoTrack= track;
            _track = track;
            if (_oppositeTrack != null)
            {
                if (_oppositeTrack["isTcp"] != _track["isTcp"])
                    return false;
            }
            _forceTcp = _track["isTcp"];
            var dummy = new Variant();
            var rtp = (InboundRtpProtocol)ProtocolFactoryManager.CreateProtocolChain(Defines.CONF_PROTOCOL_INBOUND_UDP_RTP, dummy);
            if (rtp == null)
            {
                FATAL("Unable to create the protocol chain");
                Cleanup();
                return false;
            }
            if (isAduio) _rtpAudio = rtp;
            else _rtpVideo = rtp;
            var rtcp = (RtcpProtocol)ProtocolFactoryManager.CreateProtocolChain(Defines.CONF_PROTOCOL_UDP_RTCP, dummy);
            if (rtcp == null)
            {
                FATAL("Unable to create the protocol chain");
                Cleanup();
                return false;
            }
            if (isAduio) _rtcpAudio = rtcp;
            else _rtcpVideo = rtcp;
            if (_track["isTcp"])
            {
                var dataIdx = 0u;
                var rtcpIdx = 0u;
                if (_track["portsOrChannels", "data"] && _track["portsOrChannels", "rtcp"])
                {
                    dataIdx = _track["portsOrChannels", "data"];
                    rtcpIdx = _track["portsOrChannels", "rtcp"];
                }
                else
                {
                    dataIdx =(uint) (_track["globalTrackIndex"]*2);
                    rtcpIdx = dataIdx + 1;
                }

                if ((dataIdx >= 256) || (rtcpIdx >= 256))
                {
                    FATAL("Invalid channel numbers");
                    return false;
                }
                if (_protocols.ContainsKey(dataIdx) || _protocols.ContainsKey(rtcpIdx))
                {
                    FATAL("Invalid channel numbers");
                    return false;
                }
                _protocols[dataIdx] = rtp;
                _protocols[rtcpIdx] = rtcp;
                rr.Write(8, rtp.SSRC);//SSRC of packet sender
                rr.Write(40, rtcp.SSRC); //SSRC of packet sender
             
                rr[1] = (byte) rtcpIdx;
            }
            else
            {
                if (!CreateCarriers(rtp, rtcp))
                {
                    FATAL("Unable to create carriers");
                    return false;
                }
            }
            rtp.Application = application;
            rtcp.Application = application;
            return true;
        }

        public bool Initialize()
        {
            if (_rtsp.Application == null)
            {
                FATAL("RTSP protocol not yet assigned to an application");
                return false;
            }
            //2. Compute the bandwidthHint
            uint bandwidth = 0;
            if (_videoTrack != null)
            {
                bandwidth += _videoTrack["bandwidth"];
            }
            if (_audioTrack != null)
            {
                bandwidth += _audioTrack["bandwidth"];
            }
            if (bandwidth == 0)
            {
                bandwidth = _bandwidthHint;
            }
            if (_streamName == "") _streamName = $"rtsp_{_rtsp.Id}";
            if (!_rtsp.Application.StreamNameAvailable(_streamName, _rtsp))
            {
                FATAL("Stream name {0} already taken", _streamName);
                return false;
            }
            var streamCapabilities = new StreamCapabilities {BandwidthHint = bandwidth };
            if (_videoTrack != null)
            {
                streamCapabilities.VideoCodecId = VideoCodec.H264;
                streamCapabilities.InitVideoH264(Utils.DecodeFromBase64(_videoTrack["h264SPS"]), Utils.DecodeFromBase64(_videoTrack["h264PPS"]));
            }
            if (_audioTrack != null)
            {
                streamCapabilities.AudioCodecId = (AudioCodec) (byte) _audioTrack["codec"];
                switch (streamCapabilities.AudioCodecId)
                {
                    case AudioCodec.Aac:
                        var aac = Utils.DecodeFromHex(_audioTrack["codecSetup"]);
                        streamCapabilities.InitAudioAAC(new MemoryStream(aac), aac.Length);
                        streamCapabilities.Samplerate = streamCapabilities.Aac._sampleRate;
                        break;
                    default:
                        streamCapabilities.Samplerate = _audioTrack["rate"];
                        break;
                }
            }
            _inStream = new InNetRTPStream(_rtsp, _rtsp.Application.StreamsManager,_streamName, streamCapabilities, _rtcpDetectionInterval);
            var session = _rtsp.CustomParameters;
            if (session["customParameters", "externalStreamConfig", "width"] != null &&
                session["customParameters", "externalStreamConfig", "height"] != null)
            {
                StreamCapabilities cap = _inStream.Capabilities;
                if(cap.VideoCodecId == VideoCodec.H264)
                {
                    cap.Avc._widthOverride = session["customParameters", "externalStreamConfig", "width"];
                    cap.Avc._heightOverride = session["customParameters", "externalStreamConfig", "height"];
                }
            }
            if (_rtpVideo != null)
            {
                _rtpVideo.SetStream(_inStream, false);
                _rtpVideo.InboundConnectivity = this;
                _rtcpVideo.SetInbboundConnectivity(this,false);
            }
            if (_rtpAudio != null)
            {
                _rtpAudio.SetStream(_inStream,true);
                _rtpAudio.InboundConnectivity = this;
                _rtcpAudio.SetInbboundConnectivity(this,true);
            }
            //7. Pickup all outbound waiting streams
           var subscribedOutStreams =
                    _rtsp.Application.StreamsManager.GetWaitingSubscribers(
                    _streamName, _inStream.Type);
            //FINEST("subscribedOutStreams count: %"PRIz"u", subscribedOutStreams.size());

            //8. Bind the waiting subscribers
            foreach (var subscribedOutStream in subscribedOutStreams)
            {
                subscribedOutStream.Link(_inStream);
            }

            return true;
        }

        public string GetTransportHeaderLine(bool isAudio, bool isClient)
        {
            if (_forceTcp)
            {
                var protocol = isAudio ? _rtpAudio : _rtpVideo;
                var p = _protocols.SingleOrDefault(x => x.Value.Id == protocol.Id);
                return p.Value != null ? $"RTP/AVP/TCP;unicast;interleaved={p.Key}-{p.Key+1}" : "";
            }
            var track = isAudio ? _audioTrack : _videoTrack;
            var pRtp = isAudio ? _rtpAudio : _rtpVideo;
            var pRtcp = isAudio ? _rtcpAudio : _rtcpVideo;
            return isClient ? $"RTP/AVP;unicast;client_port={((UDPCarrier) pRtp.IOHandler).NearPort}-{((UDPCarrier) pRtcp.IOHandler).NearPort}" : $"RTP/AVP;unicast;client_port={track["portsOrChannels","all"]};server_port={((UDPCarrier)pRtp.IOHandler).NearPort}-{((UDPCarrier)pRtcp.IOHandler).NearPort}";
        }

        public bool FeedData(uint channelId, InputStream buffer,uint length)
        {
            //1. Is the chanel number a valid chanel?
            if (channelId >= 4)
            {
                FATAL("Invalid chanel number: {0}", channelId);
                return false;
            }
            if (!_protocols.ContainsKey(channelId))
            {
                FATAL("Invalid chanel number: {0}", channelId);
                return false;
            }
            _inputBuffer.IgnoreAll();
            buffer.CopyPartTo(_inputBuffer,(int)length);
            _inputBuffer.Published = length;
            _inputBuffer.Position = 0;
            //_protocols[channelId].InputBuffer.WriteBytes(buffer);
            return _protocols[channelId].SignalInputData(_inputBuffer, _dummyAddress);
        }

        public string AudioClientPorts => $"{((UDPCarrier)_rtpAudio.IOHandler).NearPort}-{((UDPCarrier)_rtcpAudio.IOHandler).NearPort}";
        public string VideoClientPorts=> $"{((UDPCarrier)_rtpVideo.IOHandler).NearPort}-{((UDPCarrier)_rtcpVideo.IOHandler).NearPort}";

        public bool SendRR(bool isAudio)
        {
            if (_forceTcp)  return true;
            /*
			0                   1                   2                   3
			0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
		   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
	header |V=2|P|    RC   |   PT=RR=201   |             length            |0
		   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
		   |                     SSRC of packet sender                     |4
		   +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
	report |                 SSRC_1 (SSRC of first source)                 |8
	block  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
	  1    | fraction lost |       cumulative number of packets lost       |12
		   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
		   |           extended highest sequence number received           |16
		   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
		   |                      interarrival jitter                      |20
		   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
		   |                         last SR (LSR)                         |24
		   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
		   |                   delay since last SR (DLSR)                  |28
		   +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
		   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
	header |V=2|P|    SC   |  PT=SDES=202  |             length            |
		   +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
	chunk  |                          SSRC/CSRC_1                          |
	  1    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
		   |                           SDES items                          |
		   |                              ...                              |
		   +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
	chunk  |                          SSRC/CSRC_2                          |
	  2    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
		   |                           SDES items                          |
		   |                              ...                              |
		   +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
	 */
            var rtp = isAudio ? _rtpAudio : _rtpVideo;
            var rtcp = isAudio ? _rtcpAudio : _rtcpVideo;
            var buffer = isAudio ? _audioRR : _videoRR;
            //1. prepare the buffer
            buffer.Write(12,rtp.SSRC); //SSRC_1 (SSRC of first source)
            buffer.Write(20,rtp.ExtendedSeq);//extended highest sequence number received
            buffer.Write(28,rtcp.LastSenderReport);//last SR (LSR)
            if (_forceTcp)
            {
                return _rtsp.SendRaw(buffer);
            }
            else
            {
                if (rtcp.LastAddress != null)
                {
                    
                    if (rtcp.IOHandler.Socket.SendTo(buffer, 4, 56, SocketFlags.None, rtcp.LastAddress) != 56)
                    {
                        FATAL("Unable to send data");
                        return false;
                    }
                }
                else
                {
                    //WARN("Skip this RR because we don't have a valid address yet");
                }
                return true;
            }
        }

        public void ReportSR(ulong ntpMicroseconds, uint rtpTimestamp, bool isAudio) => _inStream?.ReportSR(ntpMicroseconds, rtpTimestamp, isAudio);

        public void Cleanup()
        {
            _audioTrack = null;
            _videoTrack = null;
            _protocols.Clear();
            if (_rtpVideo != null)
            {
                _rtpVideo.SetStream(null, false);
                _rtpVideo.EnqueueForDelete();
                _rtpVideo = null;
            }
            if (_rtpAudio != null)
            {
                _rtpAudio.SetStream(null, true);
                _rtpAudio.EnqueueForDelete();
                _rtpAudio = null;
            }
            if (_rtcpVideo != null)
            {
                _rtcpVideo.EnqueueForDelete();
                _rtcpVideo = null;
            }
            if (_rtcpAudio != null)
            {
                _rtcpAudio.EnqueueForDelete();
                _rtcpAudio = null;
            }
        }

        public bool CreateCarriers(InboundRtpProtocol rtp, RtcpProtocol rtcp)
        {
            UDPCarrier pCarrier1 = null;
            UDPCarrier pCarrier2 = null;
            for (var i = 0; i < 10; i++)
            {
                if (pCarrier1 != null)
                {
                    pCarrier1.Dispose();
                    pCarrier1 = null;
                }
                if (pCarrier2 != null)
                {
                    pCarrier2.Dispose();
                       pCarrier2 = null;
                }

                pCarrier1 = UDPCarrier.Create("0.0.0.0", 0);
                if (pCarrier1 == null)
                {
                    WARN("Unable to create UDP carrier for RTP");
                    continue;
                }

                pCarrier2 = (pCarrier1.NearPort % 2) == 0 ? UDPCarrier.Create("0.0.0.0",pCarrier1.NearPort + 1) : UDPCarrier.Create("0.0.0.0", pCarrier1.NearPort - 1);

                if (pCarrier2 == null)
                {
                    WARN("Unable to create UDP carrier for RTCP");
                    continue;
                }

                if (pCarrier1.NearPort > pCarrier2.NearPort)
                {
                    WARN("Switch carriers");
                    UDPCarrier pTemp = pCarrier1;
                    pCarrier1 = pCarrier2;
                    pCarrier2 = pTemp;
                }
                pCarrier1.Protocol = rtp.FarEndpoint;
                rtp.FarEndpoint.IOHandler = pCarrier1;
                pCarrier2.Protocol = rtcp.FarEndpoint;
                rtcp.FarEndpoint.IOHandler = pCarrier2;
                return pCarrier1.StartAccept() | pCarrier2.StartAccept();
            }

            if (pCarrier1 != null)
            {
                pCarrier1.Dispose();
                pCarrier1 = null;
            }
            if (pCarrier2 != null)
            {
                pCarrier2.Dispose();
                pCarrier2 = null;
            }

            return false;
        }

        public void Dispose() => Cleanup();
    }
}