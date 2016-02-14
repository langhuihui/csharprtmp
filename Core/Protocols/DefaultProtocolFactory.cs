using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Protocols.Rtmp;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Cluster;
using CSharpRTMP.Core.Protocols.Mp4;
using CSharpRTMP.Core.Protocols.Rtmfp;
using CSharpRTMP.Core.Protocols.Rtsp;
using CSharpRTMP.Core.Protocols.WebRtc;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Core.Protocols
{
    public class DefaultProtocolFactory : BaseProtocolFactory
    {
        public override HashSet<ulong> HandledProtocols { get; }=
       new HashSet<ulong>
            {

                ProtocolTypes.PT_TCP,
                ProtocolTypes.PT_UDP,
                ProtocolTypes.PT_INBOUND_SSL,
                ProtocolTypes.PT_OUTBOUND_SSL,
                ProtocolTypes.PT_OUTBOUND_CLUSTER,
                ProtocolTypes.PT_INBOUND_CLUSTER,
#if HAS_PROTOCOL_DNS
	ProtocolTypes.PT_INBOUND_DNS,
	ProtocolTypes.PT_OUTBOUND_DNS,
#endif
                ProtocolTypes.PT_TIMER,
#if HAS_PROTOCOL_TS
                ProtocolTypes.PT_INBOUND_TS,
#endif
#if HAS_PROTOCOL_RTMP
                ProtocolTypes.PT_INBOUND_RTMP,
                ProtocolTypes.PT_INBOUND_RTMFP,
                ProtocolTypes.PT_INBOUND_RTMPS_DISC,
                ProtocolTypes.PT_OUTBOUND_RTMP,
                ProtocolTypes.PT_MONITOR_RTMP,
                ProtocolTypes.PT_RTMPE,
#if HAS_PROTOCOL_HTTP
                ProtocolTypes.PT_INBOUND_HTTP_FOR_RTMP,
                ProtocolTypes.PT_OUTBOUND_HTTP_FOR_RTMP,
#endif
#endif
#if HAS_PROTOCOL_HTTP
                ProtocolTypes.PT_INBOUND_HTTP,
                ProtocolTypes.PT_OUTBOUND_HTTP,
                ProtocolTypes.PT_INBOUND_WEBSOCKET,
                ProtocolTypes.PT_INBOUND_WEBRTC_SIGNAL,
                ProtocolTypes.PT_INBOUND_MP4,
#endif
#if HAS_PROTOCOL_LIVEFLV
                ProtocolTypes.PT_INBOUND_LIVE_FLV,
                ProtocolTypes.PT_OUTBOUND_LIVE_FLV,
#endif
#if HAS_PROTOCOL_VAR
                ProtocolTypes.PT_BIN_VAR,
                ProtocolTypes.PT_XML_VAR,
#endif
#if HAS_PROTOCOL_RTP
                ProtocolTypes.PT_RTSP,
                ProtocolTypes.PT_RTCP,
                ProtocolTypes.PT_INBOUND_RTP,
                ProtocolTypes.PT_RTP_NAT_TRAVERSAL,
#endif
#if HAS_PROTOCOL_CLI
                ProtocolTypes.PT_INBOUND_JSONCLI,
                ProtocolTypes.PT_HTTP_4_CLI,
#endif
#if HAS_PROTOCOL_MMS
	ProtocolTypes.PT_OUTBOUND_MMS,
#endif
#if HAS_PROTOCOL_RAWHTTPSTREAM
	ProtocolTypes.PT_INBOUND_RAW_HTTP_STREAM,
#endif
            };
     

        public override HashSet<string> HandledProtocolChains { get; }=

            new HashSet<string>
            {
                Defines.CONF_PROTOCOL_INBOUND_CLUSTER,
                Defines.CONF_PROTOCOL_OUTBOUND_CLUSTER,
            #if HAS_PROTOCOL_DNS
	Defines.CONF_PROTOCOL_INBOUND_DNS,
	Defines.CONF_PROTOCOL_OUTBOUND_DNS,
#endif 
#if HAS_PROTOCOL_RTMP
	Defines.CONF_PROTOCOL_INBOUND_RTMP,
    Defines.CONF_PROTOCOL_INBOUND_RTMFP,
	Defines.CONF_PROTOCOL_OUTBOUND_RTMP,
#if HAS_PROTOCOL_HTTP
	Defines.CONF_PROTOCOL_INBOUND_RTMPS,
    Defines.CONF_PROTOCOL_INBOUND_RTMPT,
    Defines.CONF_PROTOCOL_INBOUND_WS_RTMP,
#endif 
#endif 
#if HAS_PROTOCOL_TS
	Defines.CONF_PROTOCOL_INBOUND_TCP_TS,
	Defines.CONF_PROTOCOL_INBOUND_UDP_TS,
#endif 
#if HAS_PROTOCOL_HTTP
	Defines.CONF_PROTOCOL_OUTBOUND_HTTP,
    Defines.CONF_PROTOCOL_INBOUND_WEBRTC,
    Defines.CONF_PROTOCOL_INBOUND_MP4,
#endif 
#if HAS_PROTOCOL_LIVEFLV
	Defines.CONF_PROTOCOL_INBOUND_LIVE_FLV,
	Defines.CONF_PROTOCOL_OUTBOUND_LIVE_FLV,
#endif 
#if HAS_PROTOCOL_VAR
	Defines.CONF_PROTOCOL_INBOUND_XML_VARIANT,
	Defines.CONF_PROTOCOL_INBOUND_BIN_VARIANT,
	Defines.CONF_PROTOCOL_OUTBOUND_XML_VARIANT,
	Defines.CONF_PROTOCOL_OUTBOUND_BIN_VARIANT,
#if HAS_PROTOCOL_HTTP
	Defines.CONF_PROTOCOL_INBOUND_HTTP_XML_VARIANT,
	Defines.CONF_PROTOCOL_INBOUND_HTTP_BIN_VARIANT,
	Defines.CONF_PROTOCOL_OUTBOUND_HTTP_XML_VARIANT,
	Defines.CONF_PROTOCOL_OUTBOUND_HTTP_BIN_VARIANT,
#endif 
#endif 
#if HAS_PROTOCOL_RTP
	Defines.CONF_PROTOCOL_INBOUND_RTSP,
	Defines.CONF_PROTOCOL_RTSP_RTCP,
	Defines.CONF_PROTOCOL_UDP_RTCP,
	Defines.CONF_PROTOCOL_INBOUND_RTSP_RTP,
	Defines.CONF_PROTOCOL_INBOUND_UDP_RTP,
	Defines.CONF_PROTOCOL_RTP_NAT_TRAVERSAL,
#endif 
#if HAS_PROTOCOL_CLI
	Defines.CONF_PROTOCOL_INBOUND_CLI_JSON,
#if HAS_PROTOCOL_HTTP
	Defines.CONF_PROTOCOL_INBOUND_HTTP_CLI_JSON,
#endif 
#endif 
#if HAS_PROTOCOL_MMS
	Defines.CONF_PROTOCOL_OUTBOUND_MMS,
#endif 
#if HAS_PROTOCOL_RAWHTTPSTREAM
	Defines.CONF_PROTOCOL_INBOUND_RAW_HTTP_STREAM,
	Defines.CONF_PROTOCOL_INBOUND_RAW_HTTPS_STREAM,
#endif 
	};
        

        public override List<ulong> ResolveProtocolChain(string name)
        {
            var result = new List<ulong>();
            switch (name)
            {
                case Defines.CONF_PROTOCOL_INBOUND_CLUSTER:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_INBOUND_CLUSTER);
                    break;
                case Defines.CONF_PROTOCOL_OUTBOUND_CLUSTER:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_OUTBOUND_CLUSTER);
                    break;
#if HAS_PROTOCOL_DNS
                case Defines.CONF_PROTOCOL_INBOUND_DNS:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_INBOUND_DNS);
                    break;
                case Defines.CONF_PROTOCOL_OUTBOUND_DNS:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_OUTBOUND_DNS);
                    break;
#endif
#if HAS_PROTOCOL_RTMP
                case Defines.CONF_PROTOCOL_INBOUND_RTMP:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_INBOUND_RTMP);
                    break;
                case Defines.CONF_PROTOCOL_OUTBOUND_RTMP:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_OUTBOUND_RTMP);
                    break;
                case Defines.CONF_PROTOCOL_INBOUND_RTMPS:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_INBOUND_SSL);
                    result.Add(ProtocolTypes.PT_INBOUND_RTMPS_DISC);
                    break;
                case Defines.CONF_PROTOCOL_INBOUND_RTMFP:
                    result.Add(ProtocolTypes.PT_UDP);
                    result.Add(ProtocolTypes.PT_INBOUND_RTMFP);
                    break;
#if HAS_PROTOCOL_HTTP
                case Defines.CONF_PROTOCOL_INBOUND_RTMPT:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_INBOUND_HTTP);
                    result.Add(ProtocolTypes.PT_INBOUND_HTTP_FOR_RTMP);
                    break;
                case Defines.CONF_PROTOCOL_INBOUND_WS_RTMP:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_INBOUND_WEBSOCKET);
                    result.Add(ProtocolTypes.PT_INBOUND_RTMP);
                    break;
                case Defines.CONF_PROTOCOL_INBOUND_WEBRTC:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_INBOUND_WEBSOCKET);
                    result.Add(ProtocolTypes.PT_INBOUND_WEBRTC_SIGNAL);
                    break;
                case Defines.CONF_PROTOCOL_INBOUND_MP4:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_INBOUND_MP4);
                    break;
#endif
#endif
#if HAS_PROTOCOL_TS
                case Defines.CONF_PROTOCOL_INBOUND_TCP_TS:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_INBOUND_TS);
                    break;
                case Defines.CONF_PROTOCOL_INBOUND_UDP_TS:
                    result.Add(ProtocolTypes.PT_UDP);
                    result.Add(ProtocolTypes.PT_INBOUND_TS);
                    break;
#endif
#if HAS_PROTOCOL_RTP
                case Defines.CONF_PROTOCOL_INBOUND_RTSP:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_RTSP);
                    break;
                case Defines.CONF_PROTOCOL_RTSP_RTCP:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_RTSP);
                    result.Add(ProtocolTypes.PT_RTCP);
                    break;
                case Defines.CONF_PROTOCOL_UDP_RTCP:
                    result.Add(ProtocolTypes.PT_UDP);
                    result.Add(ProtocolTypes.PT_RTCP);
                    break;
                case Defines.CONF_PROTOCOL_INBOUND_RTSP_RTP:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_RTSP);
                    result.Add(ProtocolTypes.PT_INBOUND_RTP);
                    break;
                case Defines.CONF_PROTOCOL_INBOUND_UDP_RTP:
                    result.Add(ProtocolTypes.PT_UDP);
                    result.Add(ProtocolTypes.PT_INBOUND_RTP);
                    break;
                case Defines.CONF_PROTOCOL_RTP_NAT_TRAVERSAL:
                    result.Add(ProtocolTypes.PT_UDP);
                    result.Add(ProtocolTypes.PT_RTP_NAT_TRAVERSAL);
                    break;
#endif
#if HAS_PROTOCOL_HTTP
                case Defines.CONF_PROTOCOL_OUTBOUND_HTTP:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_OUTBOUND_HTTP);
                    break;
#endif
#if HAS_PROTOCOL_LIVEFLV
                case Defines.CONF_PROTOCOL_INBOUND_LIVE_FLV:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_INBOUND_LIVE_FLV);
                    break;
#endif
#if HAS_PROTOCOL_VAR
                case Defines.CONF_PROTOCOL_INBOUND_XML_VARIANT:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_XML_VAR);
                    break;
                case Defines.CONF_PROTOCOL_INBOUND_BIN_VARIANT:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_BIN_VAR);
                    break;
                case Defines.CONF_PROTOCOL_OUTBOUND_XML_VARIANT:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_XML_VAR);
                    break;
                case Defines.CONF_PROTOCOL_OUTBOUND_BIN_VARIANT:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_BIN_VAR);
                    break;
#if HAS_PROTOCOL_HTTP
                case Defines.CONF_PROTOCOL_INBOUND_HTTP_XML_VARIANT:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_INBOUND_HTTP);
                    result.Add(ProtocolTypes.PT_XML_VAR);
                    break;
                case Defines.CONF_PROTOCOL_INBOUND_HTTP_BIN_VARIANT:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_INBOUND_HTTP);
                    result.Add(ProtocolTypes.PT_BIN_VAR);
                    break;
                case Defines.CONF_PROTOCOL_OUTBOUND_HTTP_XML_VARIANT:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_OUTBOUND_HTTP);
                    result.Add(ProtocolTypes.PT_XML_VAR);
                    break;
                case Defines.CONF_PROTOCOL_OUTBOUND_HTTP_BIN_VARIANT:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_OUTBOUND_HTTP);
                    result.Add(ProtocolTypes.PT_BIN_VAR);
                    break;

#endif
#endif
#if HAS_PROTOCOL_CLI
                case Defines.CONF_PROTOCOL_INBOUND_CLI_JSON:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_INBOUND_JSONCLI);
                    break;
#if HAS_PROTOCOL_HTTP
                case Defines.CONF_PROTOCOL_INBOUND_HTTP_CLI_JSON:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_INBOUND_HTTP);
                    result.Add(ProtocolTypes.PT_HTTP_4_CLI);
                    result.Add(ProtocolTypes.PT_INBOUND_JSONCLI);
                    break;
#endif
#endif
#if HAS_PROTOCOL_MMS
                case Defines.CONF_PROTOCOL_OUTBOUND_MMS:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_OUTBOUND_MMS);
                    break;
#endif
#if HAS_PROTOCOL_RAWHTTPSTREAM
                case Defines.CONF_PROTOCOL_INBOUND_RAW_HTTP_STREAM:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_INBOUND_RAW_HTTP_STREAM);
                    break;
                case Defines.CONF_PROTOCOL_INBOUND_RAW_HTTPS_STREAM:
                    result.Add(ProtocolTypes.PT_TCP);
                    result.Add(ProtocolTypes.PT_INBOUND_SSL);
                    result.Add(ProtocolTypes.PT_INBOUND_RAW_HTTP_STREAM);
                    break;
#endif
                default:
                    Logger.FATAL("Invalid protocol chain: {0}.", name);
                    break;
            }
            return result;
        }

        public override BaseProtocol SpawnProtocol(ulong type, Variant parameters)
        {
            BaseProtocol pResult = null;
            switch (type)
            {
                case ProtocolTypes.PT_TCP:
                    pResult = new TCPProtocol();
                    break;
                case ProtocolTypes.PT_UDP:
                    pResult = new UDPProtocol();
                    break;
                case ProtocolTypes.PT_INBOUND_SSL:
                    pResult = new InboundSSLProtocol();
                    break;
                case ProtocolTypes.PT_OUTBOUND_SSL:
                    pResult = new OutboundSSLProtocol();
                    break;
                case ProtocolTypes.PT_INBOUND_RTMP:
                    pResult = new InboundRTMPProtocol();
                    break;
                case ProtocolTypes.PT_INBOUND_RTMPS_DISC:
                    break;
                case ProtocolTypes.PT_OUTBOUND_RTMP:
                    pResult = new OutboundRTMPProtocol();
                    break;
                case ProtocolTypes.PT_INBOUND_RTMFP:
                    pResult = new InboundRTMFPProtocol();
                    break;
                case ProtocolTypes.PT_INBOUND_CLUSTER:
                    pResult = new InboundClusterProtocol();
                    break;
                case ProtocolTypes.PT_OUTBOUND_CLUSTER:
                    pResult = new OutboundClusterProtocol();
                    break;
                case ProtocolTypes.PT_RTSP:
                    pResult = new RtspProtocol();
                    break;
                case ProtocolTypes.PT_RTP_NAT_TRAVERSAL:
                    pResult = new NATTraversalProtocol();
                    break;
                case ProtocolTypes.PT_INBOUND_RTP:
                    pResult = new InboundRtpProtocol();
                    break;
                case ProtocolTypes.PT_RTCP:
                    pResult = new RtcpProtocol();
                    break;
                case ProtocolTypes.PT_INBOUND_WEBSOCKET:
                    pResult = new WebSocketProtocol();
                    break;
                case ProtocolTypes.PT_INBOUND_WEBRTC_SIGNAL:
                    pResult = new WebRtcSignalProtocol();
                    break;
                case ProtocolTypes.PT_INBOUND_MP4:
                    pResult = new Mp4Protocol();
                    break;
                default:
                    Logger.FATAL("Spawning protocol {0} not yet implemented",
                        type.TagToString());
                    break;
            }
            if (pResult != null)
            {
                if (!pResult.Initialize(parameters))
                {
                    Logger.FATAL("Unable to initialize protocol {0}",
                            type.TagToString());

                    pResult = null;
                }
            }
            return pResult;
        }
    }
}
