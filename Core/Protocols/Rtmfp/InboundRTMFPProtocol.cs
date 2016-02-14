using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using Core.Protocols.Rtmp;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Cluster;
using CSharpRTMP.Core.Protocols.Rtmp;
using CSharpRTMP.Core.Streaming;
using static CSharpRTMP.Common.Logger;
using Debug = System.Diagnostics.Debug;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class Attempt:IDisposable
    {
        private readonly Stopwatch _time = new Stopwatch();
        public uint Count;

        public Attempt()
        {
            _time.Start();
        }
        public bool Obsolete()
        {
            return _time.ElapsedMilliseconds > 120000000;
        }

        public void Dispose()
        {
            _time.Stop();
        }
    }

    public class HelloAttempt : Attempt
    {
        public Cookie Cookie;
        public Target Target;
    }

    [ProtocolType(ProtocolTypes.PT_INBOUND_RTMFP)]
    [AllowFarTypes(ProtocolTypes.PT_UDP)]
    public class InboundRTMFPProtocol : BaseRtmfpProtocol, IManage, IInFileRTMPStreamHolder
    {
        private readonly HandShake HandShakeSession;
        public Dictionary<uint, Session> Sessions = new Dictionary<uint, Session>();
        private Target _pCirrus;
        
        private bool _middle;
        // public Dictionary<string, Publication> Publications = new Dictionary<string, Publication>(); 
        public HashSet<uint> Streams = new HashSet<uint>();
        private uint _streamNextId = 0;
        public uint FarId;

        // private readonly Dictionary<byte[], HelloAttempt> _helloAttempts = new Dictionary<byte[], HelloAttempt>();
        public List<IPEndPoint> Addresses;
        public int KeepAliveServer = 15 * 1000;
        public int KeepAlivePeer = 10 * 1000;
        //public Dictionary<EndPoint,Session>  SessionsByAddress = new Dictionary<EndPoint, Session>();
        public event Action OnReadyForSend;
        public Dictionary<IPEndPoint, Middle2ServerRTMFPProtocol> Connections = new Dictionary<IPEndPoint, Middle2ServerRTMFPProtocol>();
        public override MemoryStream OutputBuffer { get; } = new MemoryStream();

        public override bool SignalInputData(InputStream inputStream, IPEndPoint address)
        {
            //inputStream.CopyDataTo(OutputBuffer, (int)inputStream.AvaliableByteCounts);
            //if (!Connections.ContainsKey(address))
            //{
            //    Connections[address] = new Middle2ServerRTMFPProtocol
            //    {
            //        Application = Application,
            //        LocalEndPoint = address,
            //        Middle = this,
            //        ServerEndPoint = new IPEndPoint(IPAddress.Parse("202.109.143.196"), 19352)
            //    };
            //    Connections[address].Start();
            //}
            //Connections[address].EnqueueForOutbound(OutputBuffer);

            var reader = inputStream.Reader;
            var id = reader.ReadUInt32() ^ reader.ReadUInt32() ^ reader.ReadUInt32();
            reader.BaseStream.Position = 4;
            var session = id == 0 ? HandShakeSession : Sessions.ContainsKey(id) ? Sessions[id] : null;
            if (session == null)
            {
                Debug.WriteLine($"unknown session {id}");
                FarProtocol.InputBuffer.Recycle(true);
                return true;
            }
            session.Decode(reader);

            if (!session.Checked)
            {
                session.Checked = true;
                var pCookieComputing = session.Peer.GetUserState<CookieComputing>();
                HandShakeSession.CommitCookie(pCookieComputing.Value);
            }
            //reader.Read24();
            //id = reader.ReadByte();
            //Logger.Debug("{0:X}", id);


            session.SetEndPoint(address);
            lock (session.OutputBuffer)
            {
                session.PacketHandler(reader);
            }
            FarProtocol.InputBuffer.Recycle(true);
            return true;
        }
        
        
        public InboundRTMFPProtocol()
        {
            HandShakeSession = new HandShake(this);
            //_pCirrus = new Target(new IPEndPoint(0, 0));
        }

        public override byte PerformHandshake(byte[] tag, H2NBinaryWriter response, IPEndPoint address, byte[] peerIdWanted)
        {
            var peerIdWantedStr = peerIdWanted.BytesToString();
            var sessionWanted = Sessions.Values.SingleOrDefault(x => x.Peer.IdStr == peerIdWantedStr);
            if (_pCirrus != null)
            {
                var session = Sessions.Values.SingleOrDefault(x => x.Peer.Address.Equals(address));
                if (session == null)
                {
                    FATAL("UDP Hole punching error : middle equivalence not found for session wanted");
                    return 0;
                }
                var request = (session as Middle).Handshaker;
                request.Write((byte)0x22);
                request.Write((byte)0x21);
                request.Write((byte)0x0F);
                request.Write(sessionWanted == null ? peerIdWanted : (session as Middle).Peer.Id, 0, 0x20);
                request.Write(tag);
                return 0;
            }
            if (sessionWanted == null)
            {
                Debug("UDP Hole punching : session {0} wanted not found", peerIdWantedStr);
                var addresses = new List<IPEndPoint>();
                if (addresses.Count == 0) return 0;
                var first = true;
                foreach (var _address in addresses)
                {
                    response.WriteAddress(_address, first);
                    first = false;
                }
                return 0x71;
            }
            if (sessionWanted.Failed())
            {
                Debug("UDP Hole punching : session wanted is deleting");
                return 0;
            }
            byte result = 0x00;
            if (_middle)
            {
                if (sessionWanted.Target != null)
                {
                    var attempt = HandShakeSession.GetHelloAttempt<HelloAttempt>(tag.BytesToString());
                    attempt.Target = sessionWanted.Target;
                    HandShakeSession.CreateCookie(response, attempt, tag, "");
                    response.Write(sessionWanted.Target.PublicKey);
                    result = 0x70;
                }
                else
                {
                    FATAL("Peer/peer dumped exchange impossible : no corresponding 'Target' with the session wanted");
                }
            }
            if (result == 0x00)
            {
                /// Udp hole punching normal process
                var times = sessionWanted.GetHelloAttempt<Attempt>(tag.BytesToString()).Count;
                sessionWanted.P2PHandshake(address, tag, times, (times > 0 || address.Address.Equals(sessionWanted.Peer.Address.Address)) ? Sessions.Values.SingleOrDefault(x => x.Peer.Address.Equals(address)) : null);
                var first = true;
                foreach (var ipEndPoint in sessionWanted.Peer.Addresses)
                {
                    if (ipEndPoint.Equals(address))
                        WARN("A client tries to connect to himself (same {0} address)", address);
                    response.WriteAddress(ipEndPoint, first);
                    Debug("P2P address initiator exchange, {0}:{1}", ipEndPoint.Address, ipEndPoint.Port);
                    first = false;
                }
                result = 0x71;
            }
            return result;
        }

        public override void ReadyForSend() => OnReadyForSend?.Invoke();

        public override uint CreateStream()
        {
            Streams.Add(++_streamNextId);
            this.Log().Debug("New stream {0}", _streamNextId);
            return _streamNextId;
        }

        public override void DestoryStream(uint sindex)
        {
            Streams.Remove(sindex);
        }
        public override Session CreateSession(Peer peer, Cookie cookie)
        {
            var target = _pCirrus;
            if (_middle)
            {
                if (cookie.Target == null)
                {
                    cookie.Target = new Target(peer.Address,cookie) {PeerId = peer.Id};
                    peer.Id = cookie.Target.Id;
                }
                else
                {
                    target = cookie.Target;
                }
            }
            Session session;
            if (target != null)
            {
                session = new Middle(peer, cookie.CookieComputing.DecryptKey, cookie.CookieComputing.EncryptKey, target)
                {
                    Handler = this,
                    FarId = cookie.FarId,
                    Application = Application
                };
                //if (_pCirrus == target) session.Target = cookie.Target;
                //session.Manage();
            }
            else
            {
                session = new Session(peer, cookie.CookieComputing.DecryptKey, cookie.CookieComputing.EncryptKey)
                {
                    Handler = this,
                    Target = cookie.Target,
                    FarId = cookie.FarId,
                    Application = Application
                };
            }
            Logger.Debug("FarId:{0}",session.FarId);
            Sessions[session.Id] = session;
            return session;
        }

        public void Manage()
        {
            HandShakeSession.Manage();
            foreach (var sessionKey in Sessions.Keys.ToArray())
            {
                Sessions[sessionKey].Manage();
                if (Sessions[sessionKey].IsEnqueueForDelete)
                {
                    Sessions.Remove(sessionKey);
                }
            }
        }
        #region 事件
        public override void OnHandshake(IPEndPoint address, string path, NameValueCollection properties, uint attempts, List<string> addresses)
        {
            // Logger.Debug(path);
        }
        public override void OnManage(Peer peer)
        {
            
        }

        public override void OnUnjoinGroup(Peer peer, Group @group)
        {
            
        }

        public override void OnDisconnection(Session session)
        {
            session.Application = null;
        }

        public override bool OnConnection(Session session, Variant parameters, AMFObjectWriter response)
        {
            string appName = parameters[0][Defines.RM_INVOKE_PARAMS_CONNECT_APP];
            //var parameters = pFrom.CustomParameters;
            //var instanceName = index == -1?"_default_": appName.Substring(index + 1);
            var oldApplication = Application;
            var newApp = ClientApplicationManager.SwitchRoom(this, appName, Application.Configuration);

            if (newApp != null && newApp != oldApplication)
            {
                var handler = newApp.GetProtocolHandler<BaseRtmfpAppProtocolHandler>(this);
                return handler.CurrentProtocol.OnConnection(session, parameters, response);
            }

            if (newApp == null || (newApp == oldApplication && !Application.OnConnect(session, parameters)))
            {
                return false;
            }

            return true;
        }

        public override void OnFailed(Peer peer, string error)
        {

        }

        //public bool OnPublish(Peer peer, Publication publication, out string error)
        //{
        //    error = null;
        //    return true;
        //}
        //public void OnUnpublish(Peer peer, Publication publication)
        //{
            
        //}
        //public void OnUnsubscribe(Peer peer, Listener listener)
        //{

        //}

        //public bool OnSubscribe(Peer peer, Listener listener, out string error)
        //{
        //    error = null; return true;
        //}

        //public void OnDataPacket(Peer peer, Publication publication, string name, AMF0Reader packet)
        //{

        //}
        //public void OnAudioPacket(Peer peer, Publication publication, uint time, N2HBinaryReader packet)
        //{
            
        //}

        //public void OnVideoPacket(Peer peer, Publication publication, uint time, N2HBinaryReader packet)
        //{

        //}
        #endregion

        public void SendStreamMessage()
        {
            
        }
       
        public override InNetRtmfpStream PublishStream(Peer peer, uint id, string name, string type, FlowWriter writer)
        {
            var session = writer.Band as Session;
            if (!session.Application.StreamsManager.StreamNameAvailable(name))
            {
                WARN(
                    "Stream name {0} already occupied and application doesn't allow duplicated inbound network streams",
                    name);
                writer.WriteStatusResponse("Publish.BadName", name + "is not available");
                return null;
            }
            var pInNetRtmpStream = new InNetRtmfpStream(session, session.Application.StreamsManager, name);
            session.Application.OnPublish(this, pInNetRtmpStream, type);
            pInNetRtmpStream.Start(peer, id, writer);
            return pInNetRtmpStream;
        }

        public override OutNetRtmfpStream SubScribeStream(Peer peer, uint id, string name, FlowWriter writer, double start, double length)
        {
            var session = writer.Band as Session;
            var outNetRtmfpStream = new OutNetRtmfpStream(session, session.Application.StreamsManager, id, name) { Writer = writer, Unbuffered = start == -3000 };
            outNetRtmfpStream.Init();
            var inBoundStream = session.Application.StreamsManager.FindByTypeByName(StreamTypes.ST_IN_NET, name, true, false).Select(x => x.Value).OfType<IInStream>().FirstOrDefault();
            
            switch ((int)start)
            {
                case -2000:
                    if (inBoundStream != null)
                    {
                        inBoundStream.Link(outNetRtmfpStream);
                    }
                    else
                    {
                        goto default;
                    }
                    break;
                case -1000:
                    if (inBoundStream != null)
                    {
                        inBoundStream.Link(outNetRtmfpStream);
                    }
                    else
                    {
                        goto case -999;
                    }
                    break;
                case -999:
                    if (ClientApplicationManager.ClusterApplication != null)
                    {
                        ClientApplicationManager.ClusterApplication.GetProtocolHandler<BaseClusterAppProtocolHandler>().PlayStream(session.Application.Id, name);
                    }
                    break;
                default:
                    var metadata = session.Application.StreamsManager.GetMetaData(name, true, session.Application.Configuration);
                    var pRtmpInFileStream = InFileRTMPStream.GetInstance(session, session.Application.StreamsManager, metadata);
                        if (pRtmpInFileStream == null)
                        {
                            WARN("Unable to get file stream. Metadata:\n{0}", metadata.ToString());
                            goto case -999;
                        }
                        if (!pRtmpInFileStream.Initialize(metadata[Defines.CONF_APPLICATION_CLIENTSIDEBUFFER]))
                        {
                            WARN("Unable to initialize file inbound stream");
                            pRtmpInFileStream.Dispose();
                            goto case -999;
                        }
                        if (!pRtmpInFileStream.Link(outNetRtmfpStream))
                        {
                            goto case -999;
                        }
                        if (!pRtmpInFileStream.Play(start, length))
                        {
                            FATAL("Unable to start the playback");
                            goto case -999;
                        }
                    break;
            }
            return outNetRtmfpStream;
        }

    }
}
