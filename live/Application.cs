using System;
using System.Collections.Generic;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Mp4;
using CSharpRTMP.Core.Protocols.Rtmfp;
using CSharpRTMP.Core.Protocols.Rtmp;
using CSharpRTMP.Core.Protocols.Rtsp;
using CSharpRTMP.Core.Protocols.WebRtc;

namespace live
{
    [AppProtocolHandler(typeof(BaseRTMPAppProtocolHandler), ProtocolTypes.PT_INBOUND_RTMP, ProtocolTypes.PT_OUTBOUND_RTMP)]
    [AppProtocolHandler(typeof(BaseRtmfpAppProtocolHandler), ProtocolTypes.PT_INBOUND_RTMFP, ProtocolTypes.PT_RTMFP_SESSION)]
    [AppProtocolHandler(typeof(BaseRtspAppProtocolHandler), ProtocolTypes.PT_RTSP,ProtocolTypes.PT_INBOUND_RTP,ProtocolTypes.PT_RTCP)]
    [AppProtocolHandler(typeof(WebRtcAppProtocolHandler),ProtocolTypes.PT_INBOUND_WEBSOCKET)]
    public class Application : BaseClientApplication
    {
        public Application(Variant configuration) : base(configuration)
        {
        }

        [CustomFunction("test")]
        public Variant _Test(BaseProtocol protocol,Variant param)
        {
            return param;
        }
    }
}
