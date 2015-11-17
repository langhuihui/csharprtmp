using System.Net;
using CSharpRTMP.Common;
using static CSharpRTMP.Common.Logger;
namespace CSharpRTMP.Core.Protocols.Rtsp
{
    [ProtocolType(ProtocolTypes.PT_RTP_NAT_TRAVERSAL)]
    [AllowFarTypes(ProtocolTypes.PT_UDP)]
    public class NATTraversalProtocol:BaseProtocol
    {
        public IPEndPoint OutboundAddress;
        public override bool SignalInputData(InputStream inputStream, IPEndPoint address)
        {
            inputStream.IgnoreAll();
            if (OutboundAddress == null) return true;
            if (!OutboundAddress.Address.Equals(address.Address))
            {
                WARN("Attempt to divert traffic. DoS attack!?");
                return true;
            }
            if (OutboundAddress.Port == address.Port)
            {
                INFO("The client has public endpoint: {0}:{1}",OutboundAddress.Address.ToString(),OutboundAddress.Port);
            }
            else
            {
                INFO("The client is behind firewall: {0}:{1} -> {0}:{2}", OutboundAddress.Address.ToString(), OutboundAddress.Port,address.Port);
                OutboundAddress.Port = address.Port;
            }
            OutboundAddress = null;
            return true;
        }
    }
}