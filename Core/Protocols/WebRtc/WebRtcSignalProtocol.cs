using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.Protocols.WebRtc
{
    [ProtocolType(ProtocolTypes.PT_INBOUND_WEBRTC_SIGNAL)]
    [AllowFarTypes(ProtocolTypes.PT_INBOUND_WEBSOCKET)]
    public class WebRtcSignalProtocol:BaseProtocol
    {
        
    }
}
