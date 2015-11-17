using System;
using System.Collections.Generic;
using System.IO;
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
    public class Middle2ServerRTMFPProtocol : BaseProtocol
    {
        public MemoryStream Buffer = new MemoryStream();
        public IPEndPoint ServerEndPoint;
        public IPEndPoint LocalEndPoint;
        public BaseProtocol Middle;

        public void Start()
        {
            var udpProtocol = new UDPProtocol { NearProtocol = this };
            UDPCarrier.Create("", 0, this);
            udpProtocol.IOHandler.Socket.Connect(ServerEndPoint);
            udpProtocol.IOHandler.ReadEnabled = true;
        }
        public override bool EnqueueForOutbound(MemoryStream outputStream, int offset = 0)
        {
            //Handler.CurrentOutputBuffer = _outputBuffer;
            outputStream.Position = offset;
            return FarEndpoint.IOHandler.SignalOutputData(ServerEndPoint, outputStream);
        }
        public override bool SignalInputData(InputStream inputStream, IPEndPoint address)
        {
            inputStream.CopyPartTo(Buffer, (int)inputStream.AvaliableByteCounts);
            Buffer.Position = 0;
            Middle.FarProtocol.IOHandler.SignalOutputData(LocalEndPoint, Buffer);
            return true;
        }
    }
    [ProtocolType(ProtocolTypes.PT_INBOUND_RTMFP)]
    [AllowFarTypes(ProtocolTypes.PT_UDP)]
    public class MiddleRTMFPProtocol : BaseProtocol
    {
        public Dictionary<IPEndPoint, Middle2ServerRTMFPProtocol> Connections = new Dictionary<IPEndPoint, Middle2ServerRTMFPProtocol>(); 
        public MemoryStream Buffer = new MemoryStream();
        
        public override bool SignalInputData(InputStream inputStream, IPEndPoint address)
        {
            inputStream.CopyPartTo(Buffer,(int) inputStream.AvaliableByteCounts);
            if (!Connections.ContainsKey(address))
            {
                Connections[address] = new Middle2ServerRTMFPProtocol
                {
                    Application = Application,
                    LocalEndPoint = address,
                    ServerEndPoint = new IPEndPoint(IPAddress.Parse("202.109.143.196"), 19352)
                };
            }
            Connections[address].EnqueueForOutbound(Buffer);
            return true;
        }
        
    }
}
