using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Rtmp;
using CSharpRTMP.Core.Streaming;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class FlowStream:Flow
    {
        enum State
        {
            Publishing,Playing,Idle
        }
        //private Publication _publication;
        public const string Signature = "\x00\x54\x43\x04";
        public const string _Name = "NetStream";
        private State _state = State.Idle;
        private uint _numberLostFragments;
        public string Name;
       // private Listener _listener;
        private InNetRtmfpStream _publisher;
        private OutNetRtmfpStream _listener;
        public IOutStream OutStream;
        public long TotalBytes;
        private bool _firstKeyFrame;
        public event Action<Variant> OnStatus;
        public FlowStream(ulong id, string signature, Peer peer, BaseRtmfpProtocol handler, Session band,FlowWriter localFlow)
            : base(id, signature, _Name, peer, handler, band, localFlow)
        {
            if (handler.StreamsManager?.StreamsByType.ContainsKey(StreamTypes.ST_IN_NET_RTMFP)??false)
            _publisher = handler.StreamsManager.StreamsByType[StreamTypes.ST_IN_NET_RTMFP].Select(x => x.Value as InNetRtmfpStream).SingleOrDefault(x=>x.PublisherId==StreamId);
            //_publication = handler.Publications.Values.SingleOrDefault(x => x.PublisherId == _index);
        }

        public override void Dispose()
        {
            switch (_state)
            {
                case State.Publishing:
                    _publisher.Dispose();
                    //Handler.UnpublishStream(Peer,_index,Name);
                    break;
                case State.Playing:
                    _listener.Dispose();
                    //Handler.UnsubscribeStream(Peer, _index, Name);
                    break;
            }
            _listener = null;
            _state = State.Idle;
        }

        protected override void AudioHandler(Stream packet)
        {
            if (_publisher != null && _publisher.PublisherId == StreamId)
            {
                var time = packet.ReadUInt();
                var length = (uint)packet.GetAvaliableByteCounts();
               // _publication.PushAudioPacket(packet.ReadUInt32(), packet, _numberLostFragments);
                _publisher.FeedData(packet, length, 0, length, time, true);
                _numberLostFragments = 0;
            }else if (OutStream != null)
            {
                var time = packet.ReadUInt();
                var length = (uint)packet.GetAvaliableByteCounts();
                TotalBytes += length;
                OutStream.FeedData(packet, length, 0, length, time, true);
            }
        }
        protected override void VideoHandler(Stream packet)
        {
            var time = packet.ReadUInt();
            var length = (uint)packet.GetAvaliableByteCounts();
            if (_numberLostFragments > 0)
                _firstKeyFrame = false;
            if ((packet.ReadByte() & 0xF0) == 0x10)
                _firstKeyFrame = true;
            packet.Position--;
            if (!_firstKeyFrame)
            {
                //丢失关键帧
                return;
            }
            _numberLostFragments = 0;
            if (_publisher != null && _publisher.PublisherId == StreamId)
            {
                //_publication.PushVideoPacket(packet.ReadUInt32(),packet,_numberLostFragments);
                _publisher.FeedData(packet, length, 0, length, time, false);
            }
            else if (OutStream != null)
            {
                TotalBytes += length;
                OutStream.FeedData(packet, length, 0, length, time, false);
            }
        }

        protected override void CommitHandler()
        {
            if (_publisher != null && _publisher.PublisherId == StreamId)
            {
                _publisher.Flush();
            }
        }

        protected override void RawHandler(byte type, Stream data)
        {
            var flag = data.ReadUShort();
            if (flag == 0x22) return;
            base.RawHandler(type,data);
        }

        protected override void LostFragmentsHandler(uint count)
        {
            _numberLostFragments += count;
            base.LostFragmentsHandler(count);
        }

        
        protected override void MessageHandler(string action,Variant param)
        {
            this.Log().Info("{0},{1}",Id,action);
            switch (action)
            {
                case "play":
                    Dispose();
                    Name = param[1];
                    double start = param[2]?? - 2000;
                    double length = param[3]??- 1000;
                    try
                    {
                        _listener = Handler.SubScribeStream(Peer, StreamId, Name, Writer, start, length);
                        _state = State.Playing;
                    }
                    catch (Exception ex)
                    {
                        Logger.ASSERT("{0}",ex);
                    }
                    var raw = Writer.WriterRawMessage();
                    raw.Write((ushort)0);
                    raw.Write(3);
                    raw.Write(34);
                    Writer.Flush(true);
                    break;
                case "closeStream":
                    Dispose();
                    break;
                case "publish":
                    Dispose();
                    Name = param[1];
                    var type = param[2]??"live";
                    //if (message.Available)
                    //{
                    //    type = message.Read<string>();
                    //}
                     _publisher =  Handler.PublishStream(Peer, StreamId, Name,type, Writer);
                     if(_publisher!=null)   _state = State.Publishing;
                    break;
                case "receiveAudio":
                    if(_listener!=null)
                        _listener.ReceiveAudio = param[1];
                    break;
                case "receiveVideo":
                    if (_listener != null)
                        _listener.ReceiveVideo = param[1];
                    break;
                case "onStatus":
                    var obj = param[1];
                    this.Log().Info(obj["code"]);
                    OnStatus?.Invoke(param);
                    break;
                default:

                    if (_state == State.Publishing)
                    {
                        //var streamMessage = Variant.Get();
                        //var pos = message.BaseStream.Position;
                        //streamMessage[Defines.RM_FLEXSTREAMSEND, Defines.RM_FLEXSTREAMSEND_UNKNOWNBYTE] = 0;
                        //streamMessage[Defines.RM_FLEXSTREAMSEND, Defines.RM_FLEXSTREAMSEND_PARAMS] = Variant.Get();
                        //streamMessage[Defines.RM_FLEXSTREAMSEND, Defines.RM_FLEXSTREAMSEND_PARAMS].Add(action);
                        //while (message.Available)
                        //{
                        //    streamMessage[Defines.RM_FLEXSTREAMSEND, Defines.RM_FLEXSTREAMSEND_PARAMS].Add(message.ReadVariant());
                        //}
                        using (var tempms = Utils.Rms.GetStream()) { 
                            tempms.WriteByte(0);
                            tempms.WriteByte(AMF0Serializer.AMF0_SHORT_STRING);
                            var buffer = Encoding.UTF8.GetBytes(action);
                            tempms.Write((ushort)buffer.Length);
                            tempms.Write(buffer,0,buffer.Length);
                            //message.BaseStream.Position = pos;
                            //////////message.BaseStream.CopyTo(tempms);
                            tempms.Position = 0;
                            _publisher.SendStreamMessage( new BufferWithOffset(tempms));
                        //Handler.SendStreamMessage();
                        }
                    }
                    else
                    {
                        base.MessageHandler(action,param);
                    }
                    break;
            }
        }
    }
}
