using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Rtmp;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class Member
    {
        public uint Index;
        public FlowWriter Writer;
        public Member(uint index, FlowWriter writer)
        {
            Index = index;
            Writer = writer;
        }
    }
    public class Peer : Entity
    {
        public IPEndPoint Address;
        public object UserState;
      
        public BaseRtmfpProtocol Handler;
        public NameValueCollection Properties;

        public string Path;

        public List<IPEndPoint> Addresses = new List<IPEndPoint>();
        public bool Connected;
        public ushort Ping;
        public FlowWriter FlowWriter;
        private readonly Dictionary<Group, Member> _groups = new Dictionary<Group, Member>();
        public  string SWFUrl;
        public  string PageUrl;
        public  string FlashVer; 
        public T GetUserState<T>()
        {
            return (T) UserState;
        }

        public Peer(BaseRtmfpProtocol handler)
        {
            Handler = handler;
            Addresses.Add(null);
        }
        public void OnHandshake(uint attempts, List<string> addresses)
        {
            Handler.OnHandshake(Address, Path, Properties, attempts, addresses);
        }

        public void OnManage()
        {
            if (Connected)
            {
                Handler.OnManage(this);
            }
        }

        public void OnUnjoinGroup(Group group,Member member)
        {
            _groups.Remove(group);
            if (Connected) Handler.OnUnjoinGroup(this, group);
            if (group.Peers.Count == 0)
            {
                Handler.Groups.Remove(group.IdStr);
            }
            else if(group.Peers.ContainsKey(member.Index))
            {
                var followingPeer = group.Peers[member.Index];
                byte count = 6;
                
                foreach (var peer in @group.Peers.Values.Where(peer => --count == 0))
                {
                    peer.WriteId(group, followingPeer, null);
                    break;
                }
            }
        }

        private bool WriteId(Group @group, Peer peer, FlowWriter writer)
        {
            H2NBinaryWriter response;
            if (writer != null)
            {
                response = writer.WriterRawMessage(true);
            }
            else
            {
                if (!peer._groups.ContainsKey(@group) || peer._groups[@group].Writer == null)
                {
                    return false;
                }
                response = peer._groups[@group].Writer.WriterRawMessage(true);
            }
            response.Write((byte)0x0b);
            response.Write(peer.Id);
            return true;
        }

        public void JoinGroup(Group group, FlowWriter writer)
        {
            var count = 5;
            var random = group.Count>5?Utils.Random.Next(group.Count - 5):0;
            foreach (var peer in group)
            {
                if (count > 0)
                {
                    if (peer == this) continue;
                    if (!WriteId(group, peer, writer)) continue;
                    --count;
                }
                else
                {
                    random--;
                    if (random < 0)
                    {
                        break;
                    }
                    if (random == 0)
                    {
                        WriteId(group, peer, writer);
                    }
                }
            }
            if (_groups.Count>0&&_groups.Last().Key == group) return;
            var index = 0u;
            if (group.Peers.Count > 0)
            {
                index = group.Peers.Last().Key + 1;
                if (index < group.Peers.Last().Key)
                {
                    index = 0;
                    foreach (var item in group.Peers.Values.ToList())
                    {
                        group.Peers[index++] = item;
                    }
                }
            }
            group.Peers[index] = this;
            _groups[group] = new Member(index,writer);
           
        }
        public Group JoinGroup(byte[] groupId, FlowWriter writer)
        {
            var group = new Group() {Id = groupId};
            Handler.Groups.Add(group.IdStr,group);
            JoinGroup(group, writer);
            return group;
        }
        public void UnjoinGroup(Group group)
        {
            if(_groups.ContainsKey(group))OnUnjoinGroup(group,_groups[group]);
        }
        public void UnsubscribeGroups()
        {
            foreach (var @group in _groups.Keys.ToArray())
            {
                OnUnjoinGroup(@group, _groups[@group]);
            }
        }

        public bool OnConnection(Variant parameters,Session session, AMFObjectWriter response)
        {
            if (!Connected)
            {
                Connected = Handler.OnConnection(session, parameters, response);
                if (Connected)
                {
                    Handler.Peers.Add(IdStr,this);
                }
            }
            else
            {
                Logger.FATAL("Client {0} seems already connected!",Id);
            }
            return Connected;
        }
        public void OnDisconnection(Session session)
        {
            if (Connected)
            {
                Connected = false;
                if (!Handler.Peers.Remove(IdStr))
                     Logger.FATAL("Client {0} seems already disconnected!",Id);
                Handler.OnDisconnection(session);
            }
        }

        public void OnFailed(string error)
        {
            if (Connected) Handler.OnFailed(this,error);
        }

        public bool OnMessage(string name, AMF0Reader message)
        {
           
            return true;
        }

        //public bool OnPublish(Publication publication, out string error)
        //{
        //    if (Connected) return Handler.OnPublish(this,publication,out error);
        //    Logger.WARN("Publication client before connection");
        //    error = "Client must be connected before publication";
        //    return false;
        //}

        //public void OnUnpublish(Publication publication)
        //{
        //    if (Connected) Handler.OnUnpublish(this, publication);
        //}

        //public void OnUnsubscribe(Listener listener)
        //{
        //    if (Connected) Handler.OnUnsubscribe(this, listener);
        //}

        //public bool OnSubscribe(Listener listener, out string error)
        //{
        //    if (Connected) return Handler.OnSubscribe(this, listener, out error);
        //    Logger.WARN("Subscription client before connection");
        //    error = "Client must be connected before subscription";
        //    return false;
        //}

        //public void OnDataPacket(Publication publication, string name, AMF0Reader packet)
        //{
        //    if (Connected) Handler.OnDataPacket(this,publication,name,packet);
        //}

        //public void OnAudioPacket(Publication publication, uint time, N2HBinaryReader packet)
        //{
        //    if (Connected) Handler.OnAudioPacket(this, publication, time, packet);
        //}

        //public void OnVideoPacket(Publication publication, uint time, N2HBinaryReader packet)
        //{
        //    if (Connected) Handler.OnVideoPacket(this, publication, time, packet);
        //}

        public Peer Clone()
        {
            return new Peer(Handler)
            {
                Addresses = new List<IPEndPoint>(Addresses.ToArray()),
                Address = Address,
                Id = Id !=null?Id.Clone() as byte[]:null,
                Path = Path,
                Properties = Properties==null?null:new NameValueCollection(Properties)
            };
        }
    }
}
