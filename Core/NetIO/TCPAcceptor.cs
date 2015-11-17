using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CSharpRTMP.Common;

using CSharpRTMP.Core.Protocols;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Core.NetIO
{
    public class TCPAcceptor:IOHandler
    {
        private BaseClientApplication _pApplication;
        private readonly List<ulong> _protocolChain;
        public readonly Variant Parameters;
        private bool _enabled;
        private int _acceptedCount;
        private int _droppedCount;
        private readonly string _ipAddress;
        private readonly ushort _port;

        public BaseClientApplication Application {
            get { return _pApplication; }
            set { _pApplication = value; }
        }

        public TCPAcceptor(string ipAddress, ushort port, Variant parameters, List<ulong> protocolChain)
            : base(IOHandlerType.IOHT_ACCEPTOR)
        {
            _ipAddress = ipAddress;
            _port = port;
            Parameters = parameters;
            _protocolChain = protocolChain;
        }
        public override bool AcceptEnabled
        {
        
            set
            {
                if (!base.AcceptEnabled)
                {
                    var saea = this.CreateOrGetSocketAsyncEventArgs();
                    saea.AcceptSocket = null;
                    try
                    {
                        if (!Socket.AcceptAsync(saea))
                        {
                            OnEvent(saea);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.FATAL(ex.Message);
                    }
                }
                base.AcceptEnabled = value;
            }
        }
        public bool Bind()
        {
            try
            {
                var localEndPoint = new IPEndPoint(IPAddress.Any, _port);
                Socket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                Socket.Bind(localEndPoint);

                Socket.Listen(100);
                _enabled = true;
            }
            catch (Exception ex)
            {
                Logger.FATAL(ex.Message);
                return false;
            }
            return true;
        }

        public bool StartAccept()
        {
            return AcceptEnabled = true;
        }

        public override bool OnEvent(SocketAsyncEventArgs socketAsyncEvent)
        {
            if (socketAsyncEvent.SocketError != SocketError.Success)
            {
                Logger.FATAL(socketAsyncEvent.SocketError.ToString());
                return false;
            }
            return OnConnectionAvailable(socketAsyncEvent) && IsAlive();
        }

        public override void GetStats(Variant info, uint namespaceId)
        {
            info.SetValue(Parameters);
            info.Add("id",(((ulong)namespaceId)<<32)| Id);
            info.Add("enabled",_enabled);
            info.Add("acceptedConnectionsCount", _acceptedCount);
            info.Add("droppedConnectionsCount", _droppedCount);
            if (_pApplication != null)
            {
                info.Add("appId", (((ulong) namespaceId) << 32) | Application.Id);
                info.Add("appName", Application.Name);
            }
            else
            {
                info.Add("appId",((ulong) namespaceId) << 32);
                info.Add("appName", "");
            }
        }

        public bool OnConnectionAvailable(SocketAsyncEventArgs socketAsyncEvent)
        {
            if (_pApplication == null) return Accept(socketAsyncEvent);
            _pApplication.AcceptTCPConnection(this, socketAsyncEvent);
            return true;
        }

        public bool Accept(SocketAsyncEventArgs socketAsyncEvent)
        {
            if (!_enabled)
            {
                Logger.WARN("Acceptor is not enabled.");
                _droppedCount++;
                return true;
            }
            Logger.INFO("Client connected:{0}:{1} -> {2}:{3}", (socketAsyncEvent.AcceptSocket.RemoteEndPoint as IPEndPoint).Address.ToString(), (socketAsyncEvent.AcceptSocket.RemoteEndPoint as IPEndPoint).Port, _ipAddress, _port);
            BaseProtocol pProtocol = ProtocolFactoryManager.CreateProtocolChain(_protocolChain, Parameters);
            if (pProtocol == null)
            {
                Logger.FATAL("Unable to create protocol chain");
                socketAsyncEvent.AcceptSocket.Close();
                return false;
            }
            var pTcpCarrier = new TCPCarrier(socketAsyncEvent.AcceptSocket)
            {
                Protocol = pProtocol.FarEndpoint,
                ReadEnabled = true
            };

            pProtocol.FarEndpoint.IOHandler = pTcpCarrier;

            //6. Register the protocol stack with an application
            if (Application != null)
            {
                pProtocol = pProtocol.NearEndpoint;
                pProtocol.Application = Application;
            }

            //if (pProtocol.NearEndpoint.OutputBuffer != null)
            //    pProtocol.NearEndpoint.EnqueueForOutbound();

            if (AcceptEnabled)
            {
                socketAsyncEvent.AcceptSocket = null;
                try
                {
                    if (!Socket.AcceptAsync(socketAsyncEvent))
                    {
                        OnEvent(socketAsyncEvent);
                    }
                }
                catch (Exception ex)
                {
                    Logger.FATAL(ex.Message);
                    return false;
                }
            }
            else
            {
               socketAsyncEvent.ReturnPool();
              
            }
            _acceptedCount++;
            
           
            return true;
        }

        public bool IsAlive()
        {
            Logger.WARN("IsAlive not yet implemented");
            return true;
        }
    }
}
