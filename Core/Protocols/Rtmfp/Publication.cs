using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Rtmp;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class Publication
    {
        public uint PublisherId;
        public string Name;
        public string Type;
        public Dictionary<uint, Listener> Listeners = new Dictionary<uint, Listener>();
        private Peer _publisher;
        private FlowWriter _controller;
        private bool _firstKeyFrame;
        public readonly MemoryStream AudioCodecBuffer = new MemoryStream();
        public readonly MemoryStream VideoCodecBuffer = new MemoryStream();
        public QualityOfService VideoQOS = new QualityOfService();
        public QualityOfService AudioQOS = new QualityOfService();

        public Publication(string name)
        {
            Name = name;
        }
        public Listener AddListener(Peer peer,uint id,FlowWriter writer,bool unbuffered)
        {
            if (Listeners.ContainsKey(id))
                return Listeners[id];
            var listener = new Listener(id, this, writer, unbuffered);
            string error;
            if (peer.OnSubscribe(listener,out error))
            {
                Listeners[id] = listener;
                writer.WriteStatusResponse("Play.Reset", "Playing and resetting " + Name);
                writer.WriteStatusResponse("Play.Start", "Started playing " + Name);
                listener.Init(peer);
                return listener;
            }
            if (string.IsNullOrEmpty(error))
            {
                error = "Not authorized to play " + Name;
            }
            writer.WriteStatusResponse("Play.Failed", error);
            throw new Exception(error);
        }

        public void RemoveListener(Peer peer,uint id)
        {
            if (Listeners.ContainsKey(id))
            {
                peer.OnUnsubscribe(Listeners[id]);
                Listeners[id].Dispose();
                Listeners.Remove(id);
            }
            else
            {
                Logger.WARN("Listener {0} is already unsubscribed of publication {1}",id,PublisherId);
            }
        }
        public void ClosePublisher(string code,string description)
        {
            if (PublisherId == 0)
            {
                return;
            }
            if (_controller != null)
            {
                if (!string.IsNullOrEmpty(code)) _controller.WriteStatusResponse(code, description);
                _controller.WriteAMFMessage("close");
            }
            else
            {
                Logger.WARN("Publisher {0} has no controller to close it", PublisherId);
            }
        }

      
        public void Start(Peer peer, uint publisherId, FlowWriter controller)
        {
            if (PublisherId != 0)
            {
                if (controller != null)
                {
                    controller.WriteStatusResponse("Publish.BadName", Name + "is already published");
                }
            }
            PublisherId = publisherId;
           
            string error;
            if (!peer.OnPublish(this, out error))
            {
                if (String.IsNullOrEmpty(error)) error = "Not allowed to publish " + Name;
            }
            _publisher = peer;
            _controller = controller;
            _firstKeyFrame = false;
            foreach (var listener in Listeners)
            {
                listener.Value.StartPublishing(Name);
            }
            Flush();
            if (controller != null)
            {
                controller.WriteStatusResponse("Publish.Start", Name + "is now published");
            }
        }

        public void Stop(Peer peer, uint publisherId)
        {
            if (publisherId == 0) return;
            if (PublisherId != publisherId)
            {
                Logger.WARN("Unpublish '{0}' operation with a {1} id different than its publisher {2} id", Name, publisherId, PublisherId);
                return;
            }
            foreach (var listener in Listeners)
            {
                listener.Value.StopPublishing(Name);
            }
            Flush();
            peer.OnUnpublish(this);
            VideoQOS.Reset();
            AudioQOS.Reset();
            PublisherId = 0;
            _publisher = null;
            _controller = null;
        }

        public void Flush()
        {
            foreach (var listener in Listeners)
            {
                listener.Value.Flush();
            }
        }

        public void PushDataPacket(string name, AMF0Reader message)
        {
            if (PublisherId == 0)
            {
                Logger.FATAL("Data packet pushed on a publication {0} who is idle", PublisherId);
                return;
            }
            //var pos = message.BaseStream.Position;
            foreach (var listener in Listeners)
            {
                listener.Value.PushDataPacket(name,message);
                //message.BaseStream.Position = pos;
            }
            _publisher.OnDataPacket(this,name,message);
        }

        public void PushAudioPacket(uint time, N2HBinaryReader packet, uint numberLostFragments)
        {
            if (PublisherId == 0)
            {
                Logger.FATAL("Audio packet pushed on a publication {0} who is idle", PublisherId);
                return;
            }
            var pos = packet.BaseStream.Position;
            if(numberLostFragments>0)Logger.INFO("");
            AudioQOS.Add(time,packet.Fragments,numberLostFragments,(uint) (packet.BaseStream.GetAvaliableByteCounts()+5),(uint) (_publisher!=null?_publisher.Ping:0));
            var temp = packet.ReadByte();
            var temp2 = packet.ReadByte();
            packet.BaseStream.Position = pos;
            if (((temp >> 4) == 0x0a) && temp2 == 0)
            {
                packet.BaseStream.CopyDataTo(AudioCodecBuffer);
                AudioCodecBuffer.Position = 0;
            }
            
            foreach (var listener in Listeners)
            {
                listener.Value.PushAudioPacket(time, packet);
                //packet.BaseStream.Position = pos;
            }
            _publisher.OnAudioPacket(this,time,packet);
        }

        public void PushVideoPacket(uint time, N2HBinaryReader packet, uint numberLostFragments)
        {
            if (PublisherId == 0)
            {
                Logger.FATAL("Video packet pushed on a publication {0} who is idle", PublisherId);
                return;
            }
            if (numberLostFragments > 0) _firstKeyFrame = false;
            VideoQOS.Add(time,packet.Fragments,numberLostFragments,(uint) (packet.BaseStream.GetAvaliableByteCounts()+5),(uint) (_publisher!=null?_publisher.Ping:0));
            if (numberLostFragments > 0)
                Logger.INFO("{0} video fragments lost on publication {1}", numberLostFragments, PublisherId);
            var pos = packet.BaseStream.Position;
            var temp = packet.ReadByte();
            var temp2 = packet.ReadByte();
            packet.BaseStream.Position = pos;
            if ((temp & 0xF0) == 0x10)
            {
                _firstKeyFrame = true;
                if (temp == 0x17 && temp2 == 0)
                {
                    packet.BaseStream.CopyDataTo(VideoCodecBuffer);
                    VideoCodecBuffer.Position = 0;
                }
            }
            if (!_firstKeyFrame)
            {
                VideoQOS.DroppedFrames++;
                return;
            }
           
            foreach (var listener in Listeners)
            {
                listener.Value.PushVideoPacket(time,packet);
                //packet.BaseStream.Position = pos;
            }
            _publisher.OnVideoPacket(this, time, packet);
        }
    }
}
