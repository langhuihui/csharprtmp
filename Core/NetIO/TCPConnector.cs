using System.Net;
using System.Net.Sockets;
using CSharpRTMP.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpRTMP.Core.Protocols;

namespace CSharpRTMP.Core.NetIO
{
    public class TCPConnector<T> : IOHandler
    {
        private bool _success;
        private bool _closeSocket;
        private readonly Variant _customParameters;
        private readonly List<ulong> _protocolChain;
        private readonly static Func<BaseProtocol, Variant,bool> SignalProtocolCreated =
                Delegate.CreateDelegate(typeof(Func<BaseProtocol, Variant, bool>),
                    typeof(T).GetMethod("SignalProtocolCreated")) as Func<BaseProtocol, Variant, bool>;

        public TCPConnector(Socket socket, List<ulong> protocolChain, Variant customParameters)
            : base(IOHandlerType.IOHT_TCP_CONNECTOR)
        {
            Socket = socket;
            _protocolChain = protocolChain;
            _customParameters = customParameters;
            _closeSocket = true;
        }

        public override void Dispose()
        {
            if (!_success)
            {
                SignalProtocolCreated(null, _customParameters);
            }
            if (_closeSocket)
            {
                Socket.Close();
            }
	        if (Protocol != null) {
		        Protocol.IOHandler = null;
		        Protocol.EnqueueForDelete();
		        Protocol = null;
	        }
	        this.UnRegisterIOHandler();
        }

        public bool Connect(EndPoint endPoint)
        {
           
                //InboundFd.Connect(ip, port);
                var saea = this.CreateOrGetSocketAsyncEventArgs();
                saea.RemoteEndPoint = endPoint;
                _closeSocket = !Socket.ConnectAsync(saea);
            if (_closeSocket) return false;
            WriteEnabled = true;
          
            return true;
        }
        public static bool Connect(EndPoint endpoint, List<ulong> protocolChain, Variant customParameters)
        {
            try
            {
                var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                var connector = new TCPConnector<T>(socket, protocolChain, customParameters);
                if (!connector.Connect(endpoint))
                {
                    IOHandlerManager.EnqueueForDelete(connector);
                }
            }
            catch (SocketException)
            {
                return SignalProtocolCreated(null, customParameters);
            }
            return true;
        }

        public override bool OnEvent(SocketAsyncEventArgs e)
        {
            e.ReturnPool();
            if (e.SocketError != SocketError.Success) return false;
            var protocol = ProtocolFactoryManager.CreateProtocolChain(_protocolChain, _customParameters);
            if (protocol == null)
            {
                Logger.FATAL("Unable to create protocol chain");
                _closeSocket = true;
                return false;
            }
            var tcpCarrier = new TCPCarrier(Socket) { Protocol = protocol.FarEndpoint };
            protocol.FarEndpoint.IOHandler = tcpCarrier;
            tcpCarrier.ReadEnabled = true;
            if (!SignalProtocolCreated(protocol, _customParameters))
            {
                protocol.Dispose();
                _closeSocket = true;
                return false;
            }
            _success = true;
            _closeSocket = false;
            IOHandlerManager.EnqueueForDelete(this);
            return true;
        }

        public override void GetStats(Variant variant, uint namespaceId)
        {
            //throw new NotImplementedException();
        }
    }
}
