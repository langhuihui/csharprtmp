using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Rtmp;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public struct Fragment
    {
        public byte Flags;
        public MemoryStream Data;
        public ulong Stage;
        public Fragment(Stream data, byte flags,ulong stage)
        {
            Flags = flags;
            Stage = stage;
            Data = Utils.Rms.GetStream();
            data.CopyPartTo(Data,(int) data.GetAvaliableByteCounts());
            Data.Position = 0;
        }
    }

    public class Packet:IDisposable
    {
        private readonly MemoryStream _buffer;
        private bool _released;
        public uint Fragments = 1;
        public Packet(Stream fragment)
        {
            _buffer = Utils.Rms.GetStream();
            fragment.CopyDataTo(_buffer);
           // var buffer = new BufferWithOffset(fragment.BaseStream);
            //_buffer.Write(buffer.Buffer, buffer.Offset, buffer.Length);
            //_buffer.Read(buffer.Buffer, buffer.Offset, buffer.Length);
           // _buffer.Position = 0;
        }

        public void Add(Stream fragment)
        {
            //var buffer = new BufferWithOffset(fragment.BaseStream);
           // _buffer.Write(buffer.Buffer, buffer.Offset, buffer.Length);
            fragment.CopyDataTo(_buffer);
            Fragments++;
        }

        public Stream Release()
        {
            if (_released)
            {
                Logger.FATAL("Packet already released!");
                return _buffer;
            }
            _buffer.Position = 0;
            _released = true;
            return _buffer;
        }
        public void Dispose()
        {
            _buffer.Dispose();
        }
    }
    public class Flow : IDisposable
    {
        public ulong Id;
        protected Peer Peer;
        protected BaseRtmfpProtocol Handler;
        public readonly Session Band;
        public ulong Stage;
        public bool Completed;
        public FlowWriter Writer;
        public string Error;
        private readonly List<Fragment> _fragments = new List<Fragment>();
        private Packet _packet;
        public readonly uint StreamId;
        protected readonly RTMPProtocolSerializer RtmpProtocolSerializer = new RTMPProtocolSerializer();
        public bool IsWaitingSync;
        public Queue<Stream> SyncMessageQueue = new Queue<Stream>(); 
        public Flow(ulong id, string signature, string name, Peer peer, BaseRtmfpProtocol handler, Session band,FlowWriter flowWriter)
        {
            Id = id;
            StreamId = signature.ToBytes().Read7BitValue(4);
            Peer = peer;
            Handler = handler;
            Band = band;
            if (flowWriter == null || flowWriter.Signature!=signature)
                Writer = new FlowWriter(signature, band, id) {Obj = name};
            else
            {
                Writer = flowWriter;
                Writer.FlowId = id;
            }
        }

        public virtual void Dispose()
        {
            Complete();
            Writer.Close();
        }

        private void Complete()
        {
            if (Completed) return;
            if(!string.IsNullOrEmpty(Writer.Signature))Logger.Debug("Flow {0} consumed",Id);
            foreach (var fragment in _fragments)
            {
                fragment.Data.Dispose();
            }
            _fragments.Clear();

        }

        protected virtual void MessageHandler(string name, Variant param)
        {
            Logger.Debug("MessageHandler：{0}",name);
        }

        protected virtual void RawHandler(byte type, Stream data)
        {
            Logger.FATAL("Raw message {0} unknown for flow {1}", data, Id);
        }

        protected virtual void AudioHandler(Stream packet)
        {
            Logger.FATAL("audio packet untreated");
        }

        protected virtual void VideoHandler(Stream packet)
        {
            Logger.FATAL("video packet untreated");
        }

        protected virtual void LostFragmentsHandler(uint count)
        {
            Logger.INFO("{0} fragments lost on flow {1}",count,Id);
        }

        public void FragmentHandler(ulong stage, ulong deltaNAck, Stream fragment, byte flags)
        {
            //Logger.Debug("Stage {0} on flow {1} received", stage, Id);
            if (Completed) return;
            var nextStage = Stage + 1;
            if (stage < nextStage)
            {
                Logger.Debug("Stage {0} on flow {1} has already been received",stage,Id);
                return;
            }
            if (deltaNAck > stage)
            {
                Logger.WARN("DeltaNAck {0} superior to stage {1} on flow {2}",deltaNAck,stage,Id);
                deltaNAck = stage;
            }
            if (Stage < stage - deltaNAck)
            {
                foreach (var it in _fragments.OrderBy(x=>x.Stage).TakeWhile(it => it.Stage <= stage).ToList())
                {
                    FragmentSortedHandler(it.Stage, it.Data, it.Flags);
                    if ((it.Flags & FlowWriter.MESSAGE_END)!=0)
                    {
                        Complete();
                        return;
                    }
                    it.Data.Dispose();
                    _fragments.Remove(it);
                }
                nextStage = stage;
            }
            if (stage > nextStage)
            {
                if (_fragments.All(x => x.Stage != stage))
                {
                    _fragments.Add(new Fragment(fragment, flags,stage));
                    if (_fragments.Count > 100)
                    {
                        Logger.Debug("_fragments.Count={0}", _fragments.Count);
                    }
                    else
                    {
                        //Logger.Debug("nextStage{0},now receive {1}", nextStage,stage);
                    }
                }
                else
                {
                    Logger.Debug("Stage {0} on flow {1} has already been received",stage,Id);
                }
            }
            else
            {
                FragmentSortedHandler(nextStage++, fragment, flags);
                if ((flags & FlowWriter.MESSAGE_END) != 0) Complete();
                if(_fragments.Count>0)
                foreach (var fragment1 in _fragments.OrderBy(x => x.Stage).ToList())
                {
                    if (fragment1.Stage > nextStage) break;
                    FragmentSortedHandler(nextStage++, fragment1.Data, fragment1.Flags);
                    if ((fragment1.Flags & FlowWriter.MESSAGE_END) != 0)
                    {
                        Complete();
                        return;
                    }
                    fragment1.Data.Dispose();
                    _fragments.Remove(fragment1);
                }
            }
        }

        private void FragmentSortedHandler(ulong stage, Stream fragment, byte flags)
        {
            if (stage <= Stage)
            {
                Logger.FATAL("Stage {0} not sorted on flow {1}",stage,Id);
                return;
            }
            if (stage > Stage + 1)
            {
                var lostCount = (uint)(stage - Stage - 1);
                Stage = stage;
                if (_packet != null)
                {
                    _packet.Dispose();
                    _packet = null;
                }
                if ((flags & FlowWriter.MESSAGE_WITH_BEFOREPART) != 0)
                {
                    LostFragmentsHandler(lostCount+1);
                    return;
                }
                LostFragmentsHandler(lostCount);
            }
            else
            {
                Stage = stage;
            }
            if ((flags & FlowWriter.MESSAGE_ABANDONMENT) != 0)
            {
                if (_packet != null)
                {
                    _packet.Dispose();
                    _packet = null;
                }
            }
            Stream message = null;
            if ((flags & FlowWriter.MESSAGE_WITH_BEFOREPART) != 0)
            {
                if (_packet == null)
                {
                    Logger.WARN("A received message tells to have a 'beforepart' and nevertheless partbuffer is empty, certainly some packets were lost");
                    LostFragmentsHandler(1);
                    _packet?.Dispose();
                    _packet = null;
                    return;
                }
               _packet.Add(fragment);
               if ((flags & FlowWriter.MESSAGE_WITH_AFTERPART) != 0)   return;
                message = _packet.Release();
               
            }
            else if ((flags & FlowWriter.MESSAGE_WITH_AFTERPART) != 0)
            {
                if (_packet != null)
                {
                    Logger.FATAL("A received message tells to have not 'beforepart' and nevertheless partbuffer exists");
                    LostFragmentsHandler(_packet.Fragments);
                    _packet.Dispose();
                }
                _packet = new Packet(fragment);
                return;
            }
            _packet = null;
            if (IsWaitingSync)
            {
                if (message == null)
                {
                    message = Utils.Rms.GetStream();
                    fragment.CopyPartTo(message,(int) fragment.GetAvaliableByteCounts());
                    message.Position = 0;
                }
                SyncMessageQueue.Enqueue(message);
                return;
            }
            HandlerMessage(message??fragment,message != null);
        }

        private void HandlerMessage(Stream message,bool needDispose = true)
        {
            var type = message.ReadByte();
            AMF0Reader amf = new AMF0Reader(message);
            switch (type)
            {
                case Defines.RM_HEADER_MESSAGETYPE_INVOKE:
                case Defines.RM_HEADER_MESSAGETYPE_FLEX:
                case Defines.RM_HEADER_MESSAGETYPE_FLEXSTREAMSEND:
                    amf._ReadUInt32();//skip timestamp
                    goto default;
                case Defines.RM_HEADER_MESSAGETYPE_VIDEODATA:
                    VideoHandler(message);
                    break;
                case Defines.RM_HEADER_MESSAGETYPE_AUDIODATA:
                    AudioHandler(message);
                    break;
                case Defines.RM_HEADER_MESSAGETYPE_CHUNKSIZE:
                    RawHandler(1, message);
                    break;
                case Defines.RM_HEADER_MESSAGETYPE_USRCTRL:
                    amf._ReadUInt32();//skip timestamp
                    var usrCtrlType = amf.ReadUInt16();
                    switch (usrCtrlType)
                    {
                        case 0x29:
                            var keepAliveServer = amf.ReadUInt32();
                            var keepAlivePeer = amf.ReadUInt32();
                            Logger.Debug("keepAliveServer:{0},keepAlivePeer:{1}", keepAliveServer, keepAlivePeer);
                            Band.KeepAliveServer = keepAliveServer;
                            break;
                        case 0x22:
                            var syncID = amf.ReadUInt32();
                            var count = amf.ReadUInt32();
                            if (Band.FlowSynchronization.ContainsKey(syncID))
                            {
                                if (Band.FlowSynchronization[syncID].Count + 1 == count)
                                {
                                    foreach (var flow in Band.FlowSynchronization[syncID])
                                        flow.SyncDone();
                                    Band.FlowSynchronization[syncID].Clear();
                                }
                                else
                                {
                                    Band.FlowSynchronization[syncID].Add(this);
                                    IsWaitingSync = true;
                                }
                            }
                            else
                            {
                                Band.FlowSynchronization[syncID] = new HashSet<Flow> {this};
                                IsWaitingSync = true;
                            }
                            //Logger.Debug("syncID:{0},count:{1}", syncID, count);
                            break;
                    }
                    break;
                default:
                    try
                    {
                        var messageBody = RtmpProtocolSerializer.Deserialize(type, amf);
                        switch (type)
                        {
                            case Defines.RM_HEADER_MESSAGETYPE_INVOKE:
                            case Defines.RM_HEADER_MESSAGETYPE_FLEX:
                                string functionName = messageBody[Defines.RM_INVOKE, Defines.RM_INVOKE_FUNCTION];
                                Writer.CallbackHandle = messageBody[Defines.RM_INVOKE, Defines.RM_INVOKE_ID];
                                MessageHandler(functionName, messageBody[Defines.RM_INVOKE, Defines.RM_INVOKE_PARAMS]);
                                break;
                            case Defines.RM_HEADER_MESSAGETYPE_NOTIFY:
                            case Defines.RM_HEADER_MESSAGETYPE_FLEXSTREAMSEND:
                                break;
                            default:
                                Logger.WARN("type:{0}\r\n{1}", type, messageBody.ToString());
                                break;
                        }
                    }
                    catch (Exception)
                    {
                        //do nothing
                    }
                    break;
            }
            Writer.CallbackHandle = 0;
           if(needDispose) message.Dispose();
        }
        public void SyncDone()
        {
            IsWaitingSync = false;
            while (SyncMessageQueue.Count > 0)
            {
                var message = SyncMessageQueue.Dequeue();
                HandlerMessage(message);
            }
        }
        public void Commit()
        {
            uint size = 0;
            var lost = new List<ulong>();
            var current = Stage;
            uint count = 0;
            var it = _fragments.OrderBy(x=>x.Stage).GetEnumerator();
            var isNotEnd = it.MoveNext();
            while (isNotEnd)
            {
                current = it.Current.Stage - current - 2;
                size += H2NBinaryWriter.Get7BitValueSize(current);
                lost.Add(current);
                current = it.Current.Stage;
                while ((isNotEnd = it.MoveNext()) && it.Current.Stage == (++current)) ++count;
                size += H2NBinaryWriter.Get7BitValueSize(count);
                lost.Add(count);
                --current;
                count = 0;
            }
            var bufferSize = _packet == null ? 0x7F : (_packet.Fragments > 0x3F00 ? 0 : 0x3F00 - _packet.Fragments);
            if (string.IsNullOrEmpty(Writer.Signature)) bufferSize = 0;
            var ack = Band.WriteMessage(0x51,
                (ushort) (H2NBinaryWriter.Get7BitValueSize(Id) + H2NBinaryWriter.Get7BitValueSize(bufferSize) +
                          H2NBinaryWriter.Get7BitValueSize(Stage) + size));
            var pos = ack.BaseStream.Position;
            ack.Write7BitLongValue(Id);
            ack.Write7BitValue(bufferSize);
            ack.Write7BitLongValue(Stage);
            //Debug.Write($"commit:{Id},stage:{Stage},writerId:{Writer.Id}");
            foreach (var l in lost)
            {
                //Debug.Write("lost"+l);
                ack.Write7BitLongValue(l);
            }
           // Debug.WriteLine("");
            CommitHandler();
            Writer.Flush();
        }

        protected virtual void CommitHandler()
        {
            //throw new NotImplementedException();
        }
    }
}
