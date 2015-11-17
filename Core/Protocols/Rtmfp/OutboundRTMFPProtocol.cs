using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CSharpRTMP.Common;
using CSharpRTMP.Core.NetIO;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    [ProtocolType(ProtocolTypes.PT_INBOUND_RTMFP)]
    [AllowFarTypes(ProtocolTypes.PT_UDP)]
    public class OutboundRTMFPProtocol : BaseRtmfpProtocol
    {
        public OutboundHandshake Session;
        public Action OnConnect;
        public OutboundRTMFPProtocol()
        {
            var udpProtocol = new UDPProtocol { NearProtocol = this };
            UDPCarrier.Create("", 0, this);
        }
        public override bool SignalInputData(InputStream inputStream, IPEndPoint address)
        {
            var reader = inputStream.Reader;
            var id = reader.ReadUInt32() ^ reader.ReadUInt32() ^ reader.ReadUInt32();
            reader.BaseStream.Position = 4;
            lock (Session.OutputBuffer)
            {
                Session.Decode(reader);
                Session.SetEndPoint(address);
                Session.PacketHandler(reader);
            }
            inputStream.Recycle(true);
            return true;
        }

        public override BaseClientApplication Application {  set { Session.Application = base.Application = value; } }
        public override Session CreateSession(Peer peer, Cookie cookie)
        {
            OnConnect?.Invoke();
            return Session;
        }
    }
}
