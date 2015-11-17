using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Resources;
using System.Text;
using System.Threading;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Rtmp;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class StreamWriter : FlowWriter
    {
        private readonly byte _type;
        public QualityOfService QOS = new QualityOfService();
        private H2NBinaryWriter _out;
        private uint _firstTime;
        public bool Reseted;
        //public Peer Client;
        public StreamWriter(byte type,string signature, Session band, ulong flowId)
            : base(signature,band,flowId)
        {
            _type = type;
        }

        public void Write(uint time, Stream data, bool unbuffered, bool first,int length = 0)
        {
            //Logger.INFO(_type == 0x09 ? "Video timestamp : {0}" : "Audio timestamp : {0}", time);
            if (unbuffered)
            {
                if (data.Position >= 5)
                {
                    data.Position -= 5;
                    data.WriteByte(_type);
                    data.Write(time);
                    WriteUnbufferedMessage(data as MemoryStream, data as MemoryStream);
                }
            }
            if (first)
            {
                Flush();
                _firstTime = time;
                _out = WriterRawMessage(true);
                _out.Write(_type);
                _out.Write(time);
            }
            if (time >= _firstTime) data.CopyDataTo(_out.BaseStream, length);
            
        }
    }
    public class FlowWriter:IDisposable
    {
        public ulong Id;
        public bool Critical;
        public bool Reliable = true;
        public bool Closed;
        //public ulong Stage;
        private bool _transaction;
       
        public const byte MESSAGE_HEADER = 0x80;
        public const byte MESSAGE_WITH_AFTERPART = 0x10;
        public const byte MESSAGE_WITH_BEFOREPART = 0x20;
        public const byte MESSAGE_ABANDONMENT = 0x02;
        public const byte MESSAGE_END = 0x01;
        private ulong _stageAck;
        private uint _ackCount;
        private ulong _stage;
        private readonly Queue<Message> _messages = new Queue<Message>();
        private readonly Queue<Message> _tempMessages = new Queue<Message>();
        private readonly LinkedList<Message> _messagesSent = new LinkedList<Message>();
        private static readonly MessageNull _MessageNull = new MessageNull();
        private uint _lostCount;
        private uint _repeatable;
        private readonly Trigger _trigger = new Trigger();
        private uint _resetCount;
        public string Obj;
        public double CallbackHandle;
        public string Signature;
        public readonly Session Band;
        public ulong FlowId;
        public void Reset(ulong id)
        {
            Id = id;
            _stage = 0;
            _stageAck = 0;
        }
        public FlowWriter(string signature, Session band,ulong flowId)
        {
            FlowId = flowId;
            Signature = signature;
            Band = band;
            Band.InitFlowWriter(this);
        }
        public FlowWriter(FlowWriter flowWriter)
        {
            Id = flowWriter.Id;
            _stage = flowWriter._stage;
            _stageAck = flowWriter._stageAck;
            _ackCount = flowWriter._ackCount;
            _lostCount = flowWriter._lostCount;
            Reliable = flowWriter.Reliable;
            Band = flowWriter.Band;
            FlowId = flowWriter.FlowId;
            Signature = flowWriter.Signature;
            Close();
        }

        public virtual void Clear()
        {
            Message pMessage;
            while (_messages.Count>0)
            {
                pMessage = _messages.Dequeue();
                _lostCount += (uint)pMessage.Fragments.Count;
                pMessage.Recycle();
               // _messages.Remove(pMessage);
            }
            while (_messagesSent.Count > 0)
            {
                pMessage = _messagesSent.First.Value;
                _lostCount += (uint)pMessage.Fragments.Count;
                if (pMessage.Repeatable) --_repeatable;
                pMessage.Recycle();
                _messagesSent.RemoveFirst();
            }
            if (_stage > 0)
            {
                CreateBufferedMessage();
                Flush();
                _trigger.Stop();
            }
        }

        public virtual void Manage()
        {
            if (!Consumed && !Band.Failed())
            {
                try
                {
                    if (_trigger.Raise())RaiseMessage();
                }
                catch (Exception ex)
                {
                    Fail("FlowWriter can't deliver its data, " + ex);
                    throw;
                }
            }
            if (Critical && Closed)
            {
                throw new Exception("Main flow writer closed, session is closing");
            }
            Flush();
        }

        private void RaiseMessage()
        {
            var header = true;
            var stop = true;
            var sent = false;
           // var stage = _stageAck + 1;
            foreach (var message in _messagesSent)
            {
                if (!message.Repeatable)
                {
                    //stage += (ulong) message.Fragments.Count;
                    header = true;
                    continue;
                }
                if (stop)
                {
                    Band.Flush();
                    //HeaderFlag = true;
                    stop = false;
                }
                uint available;
                var content = message.GetReader(out available);
                var itFrag = message.Fragments.GetEnumerator();
                var notTheEnd = itFrag.MoveNext();
                while(notTheEnd)
                {
                    var contentSize = available;
                    var fragmentOffset = itFrag.Current.Offset;
                    var stage = itFrag.Current.Stage;
                    notTheEnd = itFrag.MoveNext();
                    byte flags = 0;
                    if (itFrag.Current.Offset > 0) flags |= MESSAGE_WITH_BEFOREPART;
                    if (notTheEnd)
                    {
                        flags |= MESSAGE_WITH_AFTERPART;
                        contentSize = itFrag.Current.Offset - fragmentOffset;
                    }
                    var size = contentSize + 4;
                    if (header) size += HeaderSize(stage);
                    if (size > Band.Writer.AvaliableBufferCounts)
                    {
                        if(!sent)Logger.FATAL("Raise messages on flowWriter {0} without sending!",Id);
                        Logger.Debug("Raise message on flowWriter {0} finishs on stage {1}",Id, stage);
                        return;
                    }
                    sent = true;
                    size -= 3;
                    Debug.WriteLine("raise message on flowWriter {0},stage {1}",Id, stage);
                    Flush(Band.WriteMessage((byte) (header?0x10:0x11),(ushort)size), stage++, flags,header,content,(ushort) contentSize);
                    available -= contentSize;
                    header = false;
                }
            }
            if(stop)_trigger.Stop();
        }

        
        public bool Consumed => _messages.Count == 0 && Closed;

        private MessageBuffered CreateBufferedMessage()
        {
            if (Closed || string.IsNullOrEmpty(Signature) || Band.Failed()) return _MessageNull;
            MessageBuffered message;
            if (GlobalPool<MessageBuffered>.GetObject(out message,Reliable))
            {
                message.Repeatable = Reliable;
            }
            if(_transaction)_tempMessages.Enqueue(message);
            else _messages.Enqueue(message);
            return message;
        }
        public H2NBinaryWriter WriterRawMessage(bool withoutHeader = false)
        {
            var message = CreateBufferedMessage();
            if (!withoutHeader)
            {
                message.RawWriter.Write(Defines.RM_HEADER_MESSAGETYPE_USRCTRL);
                message.RawWriter.Write(0);//timestamp must be 0
            }
            return message.RawWriter;
        }

        public void Close()
        {
            if (Closed) return;
            if (_stage > 0 || _messages.Count > 0) CreateBufferedMessage();
            Closed = true;
            Flush();
        }

        public void Flush(bool full = false)
        {
            //if (_messagesSent.Count > 100)
            //{
                
            //}
          
            var outputWriter =  Band.Writer;
            var header = !Band.CanWriterFollowing(this);
            while (_messages.Count>0)
            {
                var message = _messages.Dequeue();
                if (message.Repeatable)
                {
                    ++_repeatable;
                    _trigger.Start();
                }
                uint fragments = 0;
                uint available;
                var content = message.GetReader(out available);
                
                do
                {
                    ++_stage;
                    var outputBufferCount = outputWriter.AvaliableBufferCounts;
                    var headerSize = (header && outputBufferCount < 62) ? HeaderSize(_stage) : 0;
                    var contentSize = available;
                    if (outputBufferCount < headerSize + 12)
                    {
                        Band.Flush();
                        header = true;
                        outputBufferCount = outputWriter.AvaliableBufferCounts;
                    }
                    headerSize = header ? (headerSize > 0 ? headerSize + 4 : HeaderSize(_stage) + 4) : 4;
                    byte flags = 0;
                    if (fragments > 0) flags |= MESSAGE_WITH_BEFOREPART;
                    var head = header;
                    if (headerSize + contentSize > outputBufferCount)
                    {
                        flags |= MESSAGE_WITH_AFTERPART;
                        contentSize = (uint)(outputBufferCount - headerSize);
                        header = true;
                    }
                    else header = false;
                    Flush(Band.WriteMessage((byte) (head ? 0x10 : 0x11), (ushort) (headerSize + contentSize - 3),this),
                        _stage, flags, head, content, (ushort) contentSize);
                    message.Fragments.Add(new Message.FragmentInfo (fragments ,_stage ));
                    available -= contentSize;
                    fragments += contentSize;
                } while (available > 0);
                _messagesSent.AddLast(message);
                // _messages.Remove(message);
            }
            if (full) Band.Flush(true);
        }

        public void Flush(H2NBinaryWriter writer, ulong stage, byte flags, bool header, N2HBinaryReader reader, ushort size)
        {
            Debug.WriteLine("sent:{0} stage {1}",Id, stage);
            if (_stageAck == 0 && header)
            {
                flags |= MESSAGE_HEADER;
            }
            if (size == 0) flags |= MESSAGE_ABANDONMENT;
            if (Closed && _messages.Count == 1) flags |= MESSAGE_END;
            writer.Write(flags);
            if (header)
            {
                writer.Write7BitLongValue(Id);
                writer.Write7BitLongValue(stage);
                writer.Write7BitLongValue(stage - _stageAck);
                if (_stageAck == 0)
                {
                    writer.WriteString8(Signature);
                    if (FlowId > 0)
                    {
                        writer.Write((byte)(1 + H2NBinaryWriter.Get7BitValueSize(FlowId)));
                        writer.Write((byte)0x0a);
                        writer.Write7BitLongValue(FlowId);
                    }
                    writer.Write((byte)0);
                }
            }
            if (size > 0)
            {
                reader.BaseStream.CopyPartTo(writer.BaseStream,size);
            }
        }
        public void Acknowledgment(N2HBinaryReader reader)
        {
            var bufferSize = reader.Read7BitLongValue();
            if (bufferSize == 0)
            {
                Fail("Negative acknowledgment");
                return;
            }
            ulong stageAckPrec = _stageAck;
            var stageReaden = reader.Read7BitLongValue();
            Debug.WriteLine("ack:id:{0},stage:{1},accId:{2}",Id, stageReaden,FlowId);
            var stage = _stageAck + 1;
            if (stageReaden > _stage)
            {
                Logger.FATAL(
                    "Acknowledgment received {0} superior than the current sending stage {1} on flowWriter {2}",
                    stageReaden, _stage, Id);
                _stageAck = _stage;
            }
            else if (stageReaden <= _stageAck)
            {
                if (reader.BaseStream.GetAvaliableByteCounts() == 0)
                    Logger.Debug("Acknowledgment {0} obsolete on flowWriter {1}", stageReaden, Id);
            }
            else
            {
                _stageAck = stageReaden;
            }

            var maxStageRecv = stageReaden;
            var pos = reader.BaseStream.Position;
            while (reader.BaseStream.GetAvaliableByteCounts() > 0)
            {
                maxStageRecv += reader.Read7BitLongValue() + reader.Read7BitLongValue() + 2;
            }
            if (pos != reader.BaseStream.Position)
            {
                reader.BaseStream.Position = pos;
            }
            ulong lostCount = 0;
            ulong lostStage = 0;
            bool repeated = false;
            bool header = true;
            bool stop = false;
            var messageNode = _messagesSent.First;
            while (messageNode!=null)
            {
               var message = messageNode.Value;
               //if (stop) break;
               // if (message.Fragments.Count == 0) continue;
                //var fragmentsLen = message.Fragments.Count;

                    for (var itFrag = 0; itFrag < message.Fragments.Count;)
                    {
                        if (_stageAck >= message.Fragments[itFrag].Stage)
                        {
                            stage++;
                            _ackCount++;
                            message.Fragments.RemoveAt(itFrag);
                            continue;
                        }
                        while (!stop)
                        {
                            if (lostCount == 0)
                            {
                                if (reader.BaseStream.GetAvaliableByteCounts() > 0)
                                {
                                    lostCount = reader.Read7BitLongValue() + 1;
                                    lostStage = stageReaden + 1;
                                    stageReaden = lostStage + lostCount + reader.Read7BitLongValue();
                                }
                                else
                                {
                                    stop = true;
                                    break;
                                }
                            }
                            if (lostStage > _stage)
                            {
                                Logger.FATAL("Lost information received {0} have not been yet sent on flowWriter {1}",
                                    lostStage, Id);
                                stop = true;
                            }
                            else if (lostStage <= _stageAck)
                            {
                                --lostCount;
                                ++lostStage;
                                continue;
                            }
                            break;
                        }
                        if (stop) break;
                        if (lostStage != stage)
                        {
                            if (repeated)
                            {
                                ++stage;
                                ++itFrag;
                                header = true;
                            }
                            else
                            {
                                _stageAck = stage;
                            }
                            continue;
                        }
                        if (!message.Repeatable)
                        {
                            if (repeated)
                            {
                                itFrag++;
                                stage++;
                                header = true;
                            }
                            else
                            {
                                Logger.INFO("FlowWriter {0} : message {1} lost", Id, stage);
                                --_ackCount;
                                ++_lostCount;
                                _stageAck = stage;
                            }
                            --lostCount;
                            ++lostStage;
                            continue;
                        }
                        repeated = true;
                        if (message.Fragments[itFrag].Stage >= maxStageRecv)
                        {
                            ++stage;
                            header = true;
                            --lostCount;
                            ++lostStage;
                            itFrag++;
                            continue;
                        }
                        Logger.Debug("FlowWriter {0} : stage {1} reapeated", Id, stage);
                        uint available;
                        var fragmentOffset = message.Fragments[itFrag].Offset;
                        var content = message.GetReader(fragmentOffset, out available);

                        message.Fragments[itFrag] = new Message.FragmentInfo(fragmentOffset, _stage);
                        var contentSize = available;
                        itFrag++;

                        byte flags = 0;
                        if (fragmentOffset > 0) flags |= MESSAGE_WITH_BEFOREPART;
                        if (itFrag != message.Fragments.Count)
                        {
                            flags |= MESSAGE_WITH_AFTERPART;
                            contentSize = message.Fragments[itFrag].Offset - fragmentOffset;
                        }
                        var size = contentSize + 4;
                        var bandWriter = Band.Writer;
                        if (!header && size > bandWriter.AvaliableBufferCounts)
                        {
                            Band.Flush();
                            header = true;
                        }
                        if (header) size += HeaderSize(stage);
                        if (size > bandWriter.AvaliableBufferCounts) Band.Flush();
                        size -= 3;
                        Flush(Band.WriteMessage((byte)(header ? 0x10 : 0x11), (ushort)size), stage, flags, header,
                            content, (ushort)contentSize);
                        available -= contentSize;
                        header = false;
                        --lostCount;
                        ++lostStage;
                        ++stage;
                    }
                if (message.Fragments.Count == 0)
                {
                    if (message.Repeatable) --_repeatable;
                    if (_ackCount > 0)
                    {
                        uint available;
                        uint size;
                        var reader1 = message.MemAck(out available, out size);
                        AckMessageHandler(_ackCount, _lostCount, reader1, available, size);
                        _ackCount = _lostCount = 0;
                    }
                    _messagesSent.Remove(messageNode);
                    Debug.WriteLine("sentremove{0} on flowWriter {1}", _stageAck, Id);
                    message.Recycle();
                }
                messageNode = messageNode.Next;
            }
            if (lostCount > 0 && reader.BaseStream.GetAvaliableByteCounts() > 0)
                Logger.FATAL("Some lost information received have not been yet sent on flowWriter {0}", Id);

            // rest messages repeatable?
            if (_repeatable == 0)
                _trigger.Stop();
            else if (_stageAck > stageAckPrec || repeated)
                _trigger.Reset();
        }

        public void Fail(string error)
        {
            if (Closed) return;
            Logger.WARN("FlowWriter {0} has failed : {1}",Id,error);
            Clear();
            _stage = _stageAck = _lostCount = _ackCount = 0;
            Band.ResetFlowWriter(new FlowWriter(this));
            Band.InitFlowWriter(this);
            Reset(++_resetCount);
        }

        protected virtual void Reset(uint count)
        {
            
        }
        protected virtual void AckMessageHandler(uint ackCount, uint lostCount, object reader1, uint available, uint size)
        {
           
        }

        private uint HeaderSize(ulong stage)
        {
            var size = H2NBinaryWriter.Get7BitValueSize(Id);
            size += H2NBinaryWriter.Get7BitValueSize(stage);
            if (_stageAck > stage)
            {
                Logger.INFO("stageAck {0} superior to stage {1} on flowWriter {2}", _stageAck, stage,Id);
            } 
            size += H2NBinaryWriter.Get7BitValueSize(stage-_stageAck);
            if (_stageAck <= 0) size += (byte)(Signature.Length + (FlowId == 0 ? 2 : (4 + H2NBinaryWriter.Get7BitValueSize(FlowId))));
            return size;
        }

        public void Dispose()
        {
            Closed = true;
            EndTransaction();
            Clear();
            if (!string.IsNullOrEmpty(Signature))
            {
                Logger.Debug("FlowWriter {0} consumed",Id);
            }
        }

        public void EndTransaction(uint numberOfCancel = 0)
        {
            foreach (var tempMessage in _tempMessages)
            {
                if ((numberOfCancel--) > 0) tempMessage.Dispose();
                else _messages.Enqueue(tempMessage);
            }
            _tempMessages.Clear();
            _transaction = false;
        }

        public void WriteErrorResponse(string code, string description) => WriteAMFResponse("_error", code, description).Dispose();

        public AMFObjectWriter WriteAMFResponse(string name, string code, string description)
        {
            var message = CreateBufferedMessage();
            WriteResponseHeader(message.Writer,name,CallbackHandle);
            var entireCode = Obj;
            if (!string.IsNullOrEmpty(code))
            {
                entireCode += "." + code;
                this.Log().Info(entireCode);
            }
            var precValue = message.Writer.AMF0Preference;
            message.Writer.AMF0Preference = true;
            var writer = new AMFObjectWriter(message.Writer)
            {
                ["level"] = name == "_error" ? "error" : "status",
                ["code"] = entireCode
            };
            if (!string.IsNullOrEmpty(description)) writer["description"] = description;
            message.Writer.AMF0Preference = precValue;
            return writer;
        }

        private void WriteResponseHeader(AMF0Writer writer, string name, double callbackHandle)
        {
            writer.Write(Defines.RM_HEADER_MESSAGETYPE_INVOKE);
            writer.Write(0);
            writer.WriteShortString(name, true);
            writer.WriteDouble(callbackHandle, true);
            writer.WriteNull();
        }
        private void WriteRequetHeader(AMF0Writer writer, string name, double callbackHandle)
        {
            writer.Write(Defines.RM_HEADER_MESSAGETYPE_INVOKE);
            writer.Write(0);
            writer.WriteShortString(name, true);
            writer.WriteDouble(callbackHandle, true);
        }
        public void BeginTransaction()
        {
            if (_transaction)
            {
                Logger.WARN("beginTransaction seems have been called without have call a endTransaction after");
            }
            _transaction = true;
        }

        public AMFObjectWriter WriteSuccessResponse(string code, string description) => WriteAMFResponse("_result", code, description);

        public AMF0Writer WriteAMFMessage(string name,double callBackHandle = 0)
        {
            var message = CreateBufferedMessage();
            WriteResponseHeader(message.Writer, name, callBackHandle);
            return message.Writer;
        }

        public AMF0Writer WriteFlexMessage(string name, double callBackHandle = 0)
        {
            var message = CreateBufferedMessage();
            message.Writer.Write(Defines.RM_HEADER_MESSAGETYPE_FLEX);
            message.Writer.Write(0);
            message.Writer.Write((byte)0);
            message.Writer.WriteShortString(name, true);
            message.Writer.WriteDouble(callBackHandle, true);
            message.Writer.WriteNull();
            return message.Writer;
        }
        public AMF0Writer WriteAMFRequestMessage(string name, double callBackHandle = 0)
        {
            var message = CreateBufferedMessage();
            WriteRequetHeader(message.Writer, name, callBackHandle);
            return message.Writer;
        }
        public AMF0Writer WriteAMFResult()
        {
            var message = CreateBufferedMessage();
            WriteResponseHeader(message.Writer, "_result", CallbackHandle);
            return message.Writer;
        }

        public AMF0Writer WriteAMFPacket(string name)
        {
            var message = CreateBufferedMessage();
            var writer = message.RawWriter;
            writer.Write(Defines.RM_HEADER_MESSAGETYPE_FLEXSTREAMSEND);
            writer.Write((byte)0);
            writer.Write(0);
            writer.Write(AMF0Serializer.AMF0_SHORT_STRING);
            writer.WriteString16(name);
            return message.Writer;
        }

        public void WriteStatusResponse(string code, string description)
        {
            WriteAMFResponse("onStatus", code, description).Dispose();
        }

        public void WriteUnbufferedMessage(MemoryStream bufferWithOffset,MemoryStream memAck = null)
        {
            if (Closed || string.IsNullOrEmpty(Signature) || Band.Failed()) return;
            var message = GlobalPool<MessageUnbuffered>.GetObject(bufferWithOffset, memAck);
            _messages.Enqueue(message);
            Flush();
        }
        public void Connect(Variant connectArgs, Action<Flow,Variant> callback)
        {
            AMF0Writer request = WriteAMFRequestMessage("connect", Band.PushCallBack(callback));
            var amfwriter = new AMFObjectWriter(request, connectArgs);
            amfwriter.Dispose();
            Flush(true);
        }

        public void CreateStream(Action<Flow, Variant> callback)
        {
            AMF0Writer request = WriteAMFMessage("createStream", Band.PushCallBack(callback));
            Flush(true);
        }
        public void SetPeerInfo(IPEndPoint ipEndPoint)
        {
            AMF0Writer request = WriteFlexMessage("setPeerInfo");
            request.WriteShortString(ipEndPoint.ToString(), true);
            Flush(true);
        }
        public void Play(string name)
        {
            var request = WriteFlexMessage("play");
            request.WriteShortString(name, true);
            Flush();
            Band.Flush();
        }
        public StreamWriter NewStreamWriter(byte type)
        {
            return new StreamWriter(type, Signature,Band,FlowId);
        }
    }
}
