using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Streaming;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class BaseRtmfpProtocol:BaseProtocol
    {
       
        public virtual byte PerformHandshake(byte[] tag, H2NBinaryWriter response, IPEndPoint address, byte[] peerIdWanted)
        {
            //throw new NotImplementedException();
            return 0;
        }

        public virtual uint CreateStream()
        {
            //throw new NotImplementedException();
            return 0;
        }

        public virtual void DestoryStream(uint sindex)
        {
            //throw new NotImplementedException();
        }

        public virtual Session CreateSession(Peer peer, Cookie cookie)
        {
            //throw new NotImplementedException();
            return null;
        }

        public virtual InNetRtmfpStream PublishStream(Peer peer, uint id, string name, string type, FlowWriter writer)
        {
            //throw new NotImplementedException();
            return null;
        }

        public virtual OutNetRtmfpStream SubScribeStream(Peer peer, uint id, string name, FlowWriter writer, double start, double length)
        {
            //throw new NotImplementedException();
            return null;
        }

        public virtual void OnDisconnection(Session session)
        {
            //throw new NotImplementedException();
        }

        public virtual bool OnConnection(Session session, Variant parameters, AMFObjectWriter response)
        {
            //throw new NotImplementedException();
            return true;
        }

        public virtual void OnFailed(Peer peer, string error)
        {
            //throw new NotImplementedException();
        }

        public virtual void OnHandshake(IPEndPoint address, string path, NameValueCollection properties, uint attempts, List<string> addresses)
        {
            //throw new NotImplementedException();
        }

        public virtual void OnManage(Peer peer)
        {
            //throw new NotImplementedException();
        }

        public virtual void OnUnjoinGroup(Peer peer, Group @group)
        {
            //throw new NotImplementedException();
        }

        public virtual Dictionary<string, Peer> Peers { get; } = new Dictionary<string, Peer>();
        public virtual Dictionary<string, Group> Groups { get; } = new Dictionary<string, Group>();
        public virtual StreamsManager StreamsManager=>Application?.StreamsManager;
    }
}
