using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using CSharpRTMP.Common;
using CSharpRTMP.Core.NetIO;
using CSharpRTMP.Core.Protocols.Rtmp;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    [ProtocolType(ProtocolTypes.PT_RTMFP_SESSION)]
    public class Session : BaseProtocol, IInFileRTMPStreamHolder
    {
        public BaseRtmfpProtocol Handler;

        public bool Checked;
        public Peer Peer;
        public uint FarId ;
        protected readonly Stopwatch RecTimestamp = new Stopwatch();
        protected ushort TimeSent;
        public AESEngine.AESType PrevAesType = AESEngine.AESType.DEFAULT;
        public readonly Dictionary<string,Attempt> HelloAttempts = new Dictionary<string, Attempt>();
        protected readonly Dictionary<ulong, FlowWriter> FlowWriters = new Dictionary<ulong, FlowWriter>();
        protected readonly Dictionary<ulong, Flow> Flows = new Dictionary<ulong, Flow>();
        public Target Target;
        protected bool _failed;
        public AESEngine AesEncrypt;
        public AESEngine AesDecrypt;
        private byte _timesKeepalive;
        private byte _timesFailed;
        private FlowWriter _lastFlowWriter;
        private ulong _nextFlowWriterId;
        private readonly MemoryStream _outputBuffer = Utils.Rms.GetStream();
        public Dictionary<long, Action<Flow, Variant>> CallBacks = new Dictionary<long, Action<Flow, Variant>>();
        public Dictionary<uint, HashSet<Flow>> FlowSynchronization = new Dictionary<uint, HashSet<Flow>>();
        public uint KeepAliveServer = 10000;
        public double PushCallBack(Action<Flow, Variant> callback)
        {
            for (int i = 1; i < 100; i++)
            {
                if (!CallBacks.ContainsKey(i))
                {
                    CallBacks[i] = callback;
                    return i;
                }
            }
            return 0;
        }

        public void RemoveCallBack(long callbackId)
        {
            if (CallBacks.ContainsKey(callbackId))
            {
                CallBacks.Remove(callbackId);
            }
        }
        public override MemoryStream OutputBuffer => _outputBuffer;

        public Session()
        {
            Writer = new RtmfpWriter(_outputBuffer) { BufferSize = RtmfpUtils.RTMFP_MAX_PACKET_LENGTH };
            AesEncrypt = new AESEngine(Defines.RTMFP_SYMETRIC_KEY, AESEngine.Direction.ENCRYPT);
            AesDecrypt = new AESEngine(Defines.RTMFP_SYMETRIC_KEY);
        }
        public Session(Peer peer,byte[] decryptKey, byte[] encryptKey)
        {
            Writer = new RtmfpWriter(_outputBuffer) {BufferSize = RtmfpUtils.RTMFP_MAX_PACKET_LENGTH};
            AesEncrypt = new AESEngine(encryptKey,AESEngine.Direction.ENCRYPT);
            AesDecrypt = new AESEngine(decryptKey);
            Peer = peer.Clone();
            Peer.Addresses[0] = peer.Address;
        }

        public T GetHelloAttempt<T>(string tag) where T:Attempt,new ()
        {
            if (HelloAttempts.ContainsKey(tag))
            {
                HelloAttempts[tag].Count++;
                return (T)HelloAttempts[tag];
            }
            var attempt = new T();
            HelloAttempts.Add(tag,attempt);
            return attempt;
        }
        
        public override bool SignalInputData(int recAmount)
        {
            PacketHandler(InputBuffer.Reader);
            return true;
        }
        public bool SetEndPoint(IPEndPoint address)
        {
            if (address.Equals(Peer.Address)) return false;
            Peer.Address = address;
            Peer.Addresses[0] = address;
            return true;
        }

        public void EraseHelloAttempt(string tag)
        {
            if (HelloAttempts.ContainsKey(tag))
                HelloAttempts.Remove(tag);
            else
            {
                Logger.WARN("Hello attempt {0} unfound, deletion useless", tag);
            }
        }

        public virtual void Decode(N2HBinaryReader reader)
        {
            var type = FarId == 0 ? AESEngine.AESType.SYMMETRIC : AESEngine.AESType.DEFAULT;
            RtmfpUtils.Decode(AesDecrypt.Next(type), reader);
            PrevAesType = type;
        }
        public virtual void PacketHandler(N2HBinaryReader reader)
        {
            if (IsEnqueueForDelete) return;
            RecTimestamp.Restart();
            var marker = reader.ReadByte() | 0xF0;
            TimeSent = reader.ReadUInt16();
            if (marker == (Target == null?0xFD:0xFE))
            {
                var time = RtmfpUtils.TimeNow();
                var timeEcho = reader.ReadUInt16();
                if (timeEcho > time)
                {
                    if (timeEcho - time < 30) time = 0;
                    else time += (ushort)(0xFFFF - timeEcho);
                    timeEcho = 0;
                }
                Peer.Ping = (ushort) ((time - timeEcho)*Defines.RTMFP_TIMESTAMP_SCALE);
            }else if (marker != (Target == null ? 0xF9 : 0xFA))
            {
                Logger.WARN("Packet marker unknown:{0}", marker);
                return;
            }
            byte flags = 0;
            Flow flow = null;
            ulong stage = 0;
            ulong deltaNAck = 0;
            var type = reader.BaseStream.GetAvaliableByteCounts() > 0 ? reader.ReadByte() : (byte) 0xFF;
            //Debug.WriteLine("rec:{0:x}",type);
            while (type != 0xFF)
            {
                var size = reader.ReadUInt16();
                long nextPos = reader.BaseStream.Position + size;
                var oldPublished = (reader.BaseStream as InputStream).Published;
                (reader.BaseStream as InputStream).Published = (uint) nextPos;
                switch (type)
                {
                    case 0x0c:
                        Fail("failed on client side");
                        break;
                    case 0x4c:
                        _failed = true;
                        EnqueueForDelete();
                        return;
                    case 0x01:
                        if (!Peer.Connected) Fail("Timeout connection client");
                        else WriteMessage(0x41, 0);
                        goto case 0x41;
                    case 0x41:
                        Logger.INFO("keepAlive!");
                        _timesKeepalive = 0;
                        break;
                    case 0x5e:
                        var idFlow = reader.Read7BitLongValue();
                        if (!FlowWriters.ContainsKey(idFlow))
                        {
                            Logger.WARN("FlowWriter {0} unfound for acknowledgment on session {1}", idFlow, Id);
                        }
                        else
                        {
                            var flowWriter = FlowWriters[idFlow];
                            flowWriter.Fail("flowWriter rejected on session " + Id);
                        }
                        break;
                    case 0x18:
                        Fail("ack negative from server");
                        break;
                    case 0x51:
                        //Acknowledgment
                        idFlow = reader.Read7BitLongValue();
                        if (FlowWriters.ContainsKey(idFlow))
                            FlowWriters[idFlow].Acknowledgment(reader);
                        else Logger.WARN("FlowWriter {0} unfound for acknowledgment on session {1}", idFlow, Id);
                        break;
                    case 0x10:
                        flags = reader.ReadByte();
                        idFlow = reader.Read7BitLongValue();
                        stage = reader.Read7BitLongValue() - 1;
                        deltaNAck = reader.Read7BitLongValue() - 1;
                        //Debug.WriteLine("10:{0},{1},{2}",idFlow,stage,deltaNAck);
                        if (_failed) break;
                        if (Flows.ContainsKey(idFlow))
                        {
                            flow = Flows[idFlow];
                        }
                        if ((flags & FlowWriter.MESSAGE_HEADER)!=0)
                        {
                            var signature = reader.ReadString8();
                            ulong assocFlowId = 0;
                            if (reader.ReadByte() > 0)
                            {
                                if (reader.ReadByte() != 0x0A)
                                {
                                    Logger.WARN("Unknown fullduplex header part for the flow {0}", idFlow);
                                }
                                else
                                {
                                    assocFlowId = reader.Read7BitLongValue();
                                }
                                var length = reader.ReadByte();
                                while (length > 0 && reader.BaseStream.GetAvaliableByteCounts()>0)
                                {
                                    Logger.WARN("Unknown message part on flow {0}",idFlow);
                                    reader.BaseStream.Position += length;
                                    length = reader.ReadByte();
                                }
                                if (length > 0)
                                {
                                    Logger.FATAL("Bad header message part, finished before scheduled");
                                }
                            }
                            if (flow == null)
                                flow = CreateFlow(idFlow, signature, assocFlowId);
                        }
                        if (flow == null)
                        {
                            
                        }
                        goto case 0x11;
                      
                    case 0x11:
                        ++stage;
                        ++deltaNAck;
                        if (type == 0x11) flags = reader.ReadByte();
                        if (flow != null)
                        {
                            flow.FragmentHandler(stage,deltaNAck, reader.BaseStream, flags);
                            if (!string.IsNullOrEmpty(flow.Error))
                            {
                                Fail(flow.Error);
                                flow = null;
                            }
                        }
                        break;
                    default:
                        Logger.FATAL("Message type {0} unknown",type);
                        break;
                }
                
                reader.BaseStream.Position = nextPos;
                (reader.BaseStream as InputStream).Published = oldPublished;
                type = (byte) (reader.BaseStream.GetAvaliableByteCounts() > 0 ? reader.ReadByte() : 0xFF);
                if (flow != null && type != 0x11)
                {
                    flow.Commit();
                    if (flow.Completed)
                    {
                        Flows.Remove(flow.Id);
                        flow.Dispose();
                    }
                    flow = null;
                }
                //else
                //{
                //    Debug.WriteLine("no commit:{0},{1:X}",flow?.Id.ToString() ?? "no flow",type);
                //}
            }
            SFlush(true);
        }

        public virtual Flow CreateFlow(ulong id, string signature,ulong assocFlowId)
        {
            if (IsEnqueueForDelete)
            {
                
            }
            if (Flows.ContainsKey(id))
            {
                return Flows[id];
            }

            Flow flow = null;
            FlowWriter localFlow = null;
            if (FlowWriters.ContainsKey(assocFlowId))
            {
                localFlow = FlowWriters[assocFlowId];
            }
            switch (signature)
            {
                case FlowConnection.Signature:
                    Logger.Debug("New FlowConnection {0} on session {1}", id, Id);
                    flow = new FlowConnection(id,Peer,Handler,this, localFlow);
                    break;
                case FlowGroup.Signature:
                    Logger.Debug("New FlowGroup {0} on session {1}", id, Id);
                    flow = new FlowGroup(id, Peer, Handler, this, localFlow);
                    break;
                default:
                    if (signature.StartsWith(FlowStream.Signature))
                    {
                        Logger.Debug("New FlowStream {0} on session {1}", id, Id);
                        flow = new FlowStream(id, signature, Peer, Handler, this, localFlow);
                    }
                    else
                        Logger.FATAL("New unknown flow {0} on session {1}", signature, Id);
                    break;
            }
            if (flow != null && id!=0)
            {
               
                Flows[id] = flow;
            }
            return flow;
        }

    

        public void InitFlowWriter(FlowWriter flowWriter)
        {
            while (++_nextFlowWriterId == 0 || FlowWriters.ContainsKey(_nextFlowWriterId))
            {
            }
            flowWriter.Id = _nextFlowWriterId;
           // if (Flows.Count > 0) flowWriter.FlowId = Flows.First().Value.Id;
            FlowWriters[_nextFlowWriterId] = flowWriter;
            if (!string.IsNullOrEmpty(flowWriter.Signature))
            {
                Logger.Debug("New flowWriter {0} on session {1}",flowWriter.Id,Id);
            }
        }

        public void ResetFlowWriter(FlowWriter flowWriter)
        {
            FlowWriters[flowWriter.Id] = flowWriter;
        }


        public bool Failed()
        {
            return _failed;
        }

        public bool CanWriterFollowing(FlowWriter flowWriter) => _lastFlowWriter == flowWriter;

        public RtmfpWriter Writer { get; set; }

        public H2NBinaryWriter WriteMessage(byte type, ushort length, FlowWriter flowWriter = null)
        {
            if (_failed)
            {
                Writer.Clear(11);
                return Writer;
            }
            _lastFlowWriter = flowWriter;
            var size = length + 3;
            if (size > Writer.AvaliableBufferCounts)
            {
                SFlush();
                if (size > Writer.AvaliableBufferCounts)
                {
                    Logger.INFO("Message truncated because exceeds maximum UDP packet size on session {0}",Id);
                    size = (int)Writer.AvaliableBufferCounts;
                }
                _lastFlowWriter = null;
            }
            Writer.Write(type);
            Writer.Write(length);
            return Writer;
        }
        public virtual void SFlush(bool echoTime = false) => Flush((byte) (Target==null?0x4a:0x89), echoTime, PrevAesType);

        public virtual void Flush(byte marker) => Flush(marker, false, PrevAesType);

        public void Flush(bool echoTime = false) => Flush((byte)(Target == null ? 0x4a : 0x89), echoTime, AESEngine.AESType.DEFAULT);

        public void Flush(byte marker, bool echoTime, AESEngine.AESType aesType)
        {
            _lastFlowWriter = null;
            if (IsEnqueueForDelete) return;
            var outputBuffer = Writer.BaseStream;
            if (outputBuffer.Length >= Defines.RTMFP_MIN_PACKET_SIZE)
            {
                //Debug.WriteLine(outputBuffer.Length);
                if (RecTimestamp.Elapsed > TimeSpan.FromSeconds(30))
                    echoTime = false;
                var offset = 0;
                if (echoTime) marker += 4;
                else offset = 2;
                var timeStamp = RtmfpUtils.TimeNow();
                //_outputBuffer.Ignore(offset);
                outputBuffer.Position = 6 + offset;
                outputBuffer.WriteByte(marker);
                Writer.Write(timeStamp);
                if (echoTime)
                    Writer.Write((ushort)(TimeSent + RtmfpUtils.Time(RecTimestamp.Elapsed)));
                RtmfpUtils.EncodeAndPack(AesEncrypt.Next(aesType), Writer, FarId, offset);
                EnqueueForOutbound(outputBuffer as MemoryStream, offset);
                Writer.Clear(11);
            }
        }
        public virtual void SendStream(Stream stream,int len)
        {
            var marker = stream.ReadByte() | 0xF0;
            var echoTime = marker == (Target == null ? 0xFE : 0xFD);
            stream.ReadUShort();
            if (echoTime) stream.ReadUShort();
            var type = stream.ReadByte();
            var size = stream.ReadUShort();
            
            switch (type)
            {
                case 0x51:
                    var idFlow = stream.Read7BitLongValue();
                    var bufferSize = stream.Read7BitLongValue();
                    var stageReaden = stream.Read7BitLongValue();
                    var tail = "";
                    while (stream.GetAvaliableByteCounts() > 0)
                    {
                        tail+=" "+stream.Read7BitLongValue();
                    }
                    Debug.WriteLine("from {1}:{0:X} ack {2} on flow {3} {4}", type, Target == null ? "server" : "client", stageReaden, idFlow,tail);
                    break;
                case 0x10:
                    var flags = stream.ReadByte();
                     idFlow = stream.Read7BitLongValue();
                    var stage = stream.Read7BitLongValue();
                    var deltaNAck = stream.Read7BitLongValue();
                    Debug.WriteLine("from {1}:{0:X} stage {2} deltaNAck {4} on flow {3}", type, Target == null ? "server" : "client", stage, idFlow,deltaNAck);
                    if ((flags & FlowWriter.MESSAGE_HEADER) != 0)
                    {
                        var signturelen = stream.ReadByte();
                    stream.Position += signturelen;
                    if (stream.ReadByte() > 0)
                    {
                        if (stream.ReadByte() != 0x0A)
                        {
                            Logger.WARN("Unknown fullduplex header part for the flow {0}", idFlow);
                        }
                        else
                        {
                            var assocFlowId = stream.Read7BitLongValue();
                        }
                        var length = stream.ReadByte();
                        while (length > 0 && stream.GetAvaliableByteCounts() > 0)
                        {
                            Logger.WARN("Unknown message part on flow {0}", idFlow);
                            stream.Position += length;
                            length = stream.ReadByte();
                        }
                        if (length > 0)
                        {
                            Logger.FATAL("Bad header message part, finished before scheduled");
                        }
                    }
                    var stype = stream.ReadByte();//type
                    //var timestamp = stream.ReadUInt();//timestamp
                    switch (stype)
                    {
                        case 0x08:
                        case 0x09:
                            break;
                        case Defines.RM_HEADER_MESSAGETYPE_FLEX:
                            stream.ReadByte();
                            goto case Defines.RM_HEADER_MESSAGETYPE_INVOKE;
                        case Defines.RM_HEADER_MESSAGETYPE_INVOKE:
                            var timestamp = stream.ReadUInt();//timestamp
                            var amfReader = new AMF0Reader(stream);
                            var str = amfReader.ReadShortString(true);
                            Logger.Debug("from {1}:{0:X} to {2:X} on flow {3}", type, Target == null ? "server" : "client", str, idFlow);
                            break;
                        default:
                            Logger.Debug("from {1}:{0:X} to {2:X} on flow {3}", type, Target == null ? "server" : "client", stype, idFlow);
                            break;
                    }
                    }
                    break;
                default:
                    Logger.Debug("from {1}:{0:X}", type, Target == null ? "server" : "client");
                    break;
            }
            

            stream.Position = 0;
            _outputBuffer.Position = 0;
            stream.CopyPartTo(_outputBuffer, len);
            RtmfpUtils.EncodeAndPack(AesEncrypt.Next(PrevAesType), Writer, FarId);
            EnqueueForOutbound(_outputBuffer);
        }
        public override bool EnqueueForOutbound(MemoryStream outputStream , int offset = 0)
        {
            //Handler.CurrentOutputBuffer = _outputBuffer;
            outputStream.Position = offset;
            return Handler.FarEndpoint.IOHandler?.SignalOutputData(Peer.Address, outputStream)??false;
        }

        public void P2PHandshake(IPEndPoint address, byte[] tag, uint times, Session session)
        {
            if (_failed) return;
            Logger.Debug("Peer newcomer address send to peer {0} connected",Id);
            ushort size = 0x36;
            byte index = 0;
            IPEndPoint pAddress = null;
            if (session != null && Peer.Addresses.Count > 0)
            {
                if (session.Peer.Addresses[0].Address.Equals(Peer.Addresses[0].Address))
                    times++;
                index = (byte) (times%session.Peer.Addresses.Count);
                pAddress = session.Peer.Addresses[index];
                size += (ushort)pAddress.Address.GetAddressBytes().Length;
            }
            else
            {
                size += (ushort)(address.AddressFamily == AddressFamily.InterNetworkV6 ? 16 : 4);
            }
            var writer = WriteMessage(0x0F, size);
            writer.Write((byte)0x22);
            writer.Write((byte)0x21);
            writer.Write((byte)0x0F);
            writer.Write(Peer.Id, 0,RtmfpUtils.ID_SIZE);
            if (pAddress != null)
            {
                writer.WriteAddress(pAddress, index == 0);
            }
            else
            {
                writer.WriteAddress(address, true);
            }
            writer.Write(tag);
            SFlush(true);
        }

       

        public bool KeepAlive
        {
            get
            {
                if (!Peer.Connected)
                {
                    Fail("Timeout connection client");
                    return false;
                }
                if (_timesKeepalive == 10)
                {
                    Fail("Timeout keepalive attempts");
                    return false;
                }
                ++_timesKeepalive;
                WriteMessage(0x01, 0);
                return true;
            }
        }
        public virtual void Manage()
        {
            if (IsEnqueueForDelete) return;
            foreach (var helloAttempt in HelloAttempts.Keys.Where(helloAttempt => HelloAttempts[helloAttempt].Obsolete()).ToArray())
            {
                HelloAttempts[helloAttempt].Dispose();
                HelloAttempts.Remove(helloAttempt);
            }
            if (_failed)
            {
                FailSignal();
                return;
            }
            if (RecTimestamp.ElapsedMilliseconds > 6*60*1000)
            {
                Fail("Timeout no client message");
                return;
            }
            if (RecTimestamp.ElapsedMilliseconds > 2*60*1000 && !KeepAlive)
            {
                return;
            }
            foreach (var flowWriterKey in FlowWriters.Keys.ToArray())
            {
                var flowWriter = FlowWriters[flowWriterKey];
                try
                {
                    flowWriter.Manage();
                }
                catch (Exception ex)
                {
                    if (flowWriter.Critical)
                    {
                        Fail(ex.Message);
                        break;
                    }
                    continue;
                }
                if (flowWriter.Consumed)
                {
                    FlowWriters.Remove(flowWriterKey);
                }
            }

            if (!_failed) Peer.OnManage();
            SFlush(echoTime: true);
        }

        protected void Fail(string error)
        {
            if (_failed) return;
            Writer.Clear(11);
            foreach (var flowWriter in FlowWriters)
            {
                flowWriter.Value.Clear();
            }
            Peer.FlowWriter = null;
            Peer.UnsubscribeGroups();
            _failed = true;
            if (!string.IsNullOrEmpty(error))
            {
                Peer.OnFailed(error);
                FailSignal();
            }
        }

        private void FailSignal()
        {
            _failed = true;
            if (IsEnqueueForDelete) return;
            _timesFailed++;
            Writer.Write((byte)0x0C);
            Writer.Write((ushort)0);
            SFlush();
            if (_timesFailed == 10 || RecTimestamp.ElapsedMilliseconds > 6 * 60 * 1000) EnqueueForDelete();
        }

        public override void EnqueueForDelete()
        {
            if (!_failed) FailSignal();
            if (IsEnqueueForDelete) return;
            Peer.FlowWriter = null;
            Peer.UnsubscribeGroups();
            foreach (var flow in Flows)
            {
                flow.Value.Dispose();
            }
            Flows.Clear();
            Peer.OnDisconnection(this);
            foreach (var flowWriter in FlowWriters)
            {
                flowWriter.Value.Dispose();
            }
            FlowWriters.Clear();
            base.EnqueueForDelete();

        }

        public event Action OnReadyForSend;
    }
}
