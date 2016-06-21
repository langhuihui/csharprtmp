using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Core.Protocols.Rtmp;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Streaming;
using Newtonsoft.Json.Linq;
using static CSharpRTMP.Common.Defines;
using static CSharpRTMP.Common.Logger;
using static CSharpRTMP.Core.Protocols.Rtmp.HeaderType;
using static CSharpRTMP.Core.Streaming.StreamTypes;

namespace CSharpRTMP.Core.Protocols.Rtmp
{
    public enum RTMPState
    {
        RTMP_STATE_NOT_INITIALIZED,
        RTMP_STATE_CLIENT_REQUEST_RECEIVED,
        RTMP_STATE_CLIENT_REQUEST_SENT,
        RTMP_STATE_SERVER_RESPONSE_SENT,
        RTMP_STATE_DONE
    }
    [AllowFarTypes(ProtocolTypes.PT_TCP ,ProtocolTypes.PT_RTMPE ,ProtocolTypes.PT_INBOUND_SSL, ProtocolTypes.PT_INBOUND_HTTP_FOR_RTMP,ProtocolTypes.PT_INBOUND_WEBSOCKET)]
    public abstract class BaseRTMPProtocol : BaseProtocol, IInFileRTMPStreamHolder
    {
        public static readonly byte[] GenuineFmsKey ={
	        0x47, 0x65, 0x6e, 0x75, 0x69, 0x6e, 0x65, 0x20,
	        0x41, 0x64, 0x6f, 0x62, 0x65, 0x20, 0x46, 0x6c,
	        0x61, 0x73, 0x68, 0x20, 0x4d, 0x65, 0x64, 0x69,
	        0x61, 0x20, 0x53, 0x65, 0x72, 0x76, 0x65, 0x72,
	        0x20, 0x30, 0x30, 0x31, // Genuine Adobe Flash Media Server 001
	        0xf0, 0xee, 0xc2, 0x4a, 0x80, 0x68, 0xbe, 0xe8,
	        0x2e, 0x00, 0xd0, 0xd1, 0x02, 0x9e, 0x7e, 0x57,
	        0x6e, 0xec, 0x5d, 0x2d, 0x29, 0x80, 0x6f, 0xab,
	        0x93, 0xb8, 0xe6, 0x36, 0xcf, 0xeb, 0x31, 0xae
        };// 68
        public static readonly byte[] GenuineFpKey ={
	        0x47, 0x65, 0x6E, 0x75, 0x69, 0x6E, 0x65, 0x20,
	        0x41, 0x64, 0x6F, 0x62, 0x65, 0x20, 0x46, 0x6C,
	        0x61, 0x73, 0x68, 0x20, 0x50, 0x6C, 0x61, 0x79,
	        0x65, 0x72, 0x20, 0x30, 0x30, 0x31, // Genuine Adobe Flash Player 001
	        0xF0, 0xEE, 0xC2, 0x4A, 0x80, 0x68, 0xBE, 0xE8,
	        0x2E, 0x00, 0xD0, 0xD1, 0x02, 0x9E, 0x7E, 0x57,
	        0x6E, 0xEC, 0x5D, 0x2D, 0x29, 0x80, 0x6F, 0xAB,
	        0x93, 0xB8, 0xE6, 0x36, 0xCF, 0xEB, 0x31, 0xAE
        }; // 62

        public static byte[] HMACsha256(BufferWithOffset pData, uint dataLength, byte[] key, uint keyLength)
        {
            byte[] _key;
            if (keyLength != key.Length)
            {
                _key = new byte[keyLength];
                Buffer.BlockCopy(key, 0, _key, 0, (int) keyLength);
            }
            else
                _key = key;
            var hmac = new HMACSHA256(_key);
           return hmac.ComputeHash(pData.Buffer, pData.Offset, (int)dataLength);
        }
        public static byte[] HMACsha256(byte[] pData, uint dataLength, byte[] key, uint keyLength)
        {
            byte[] _key;
            if (keyLength != key.Length)
            {
                _key = new byte[keyLength];
                Buffer.BlockCopy(key, 0, _key, 0, (int)keyLength);
            }
            else
                _key = key;
            var hmac = new HMACSHA256(_key);
            return hmac.ComputeHash(pData, 0, (int)dataLength);
        }
        public const int MAX_CHANNELS_COUNT = 64 + 255;
        public const int RECEIVED_BYTES_COUNT_REPORT_CHUNK = 131072;
        public const int MAX_STREAMS_COUNT = 256;
        public const int MIN_AV_CHANNLES = 4;
        public const int MAX_AV_CHANNLES = MAX_CHANNELS_COUNT;
        protected bool _handshakeCompleted;
        protected RTMPState _rtmpState = RTMPState.RTMP_STATE_NOT_INITIALIZED;
        //protected readonly OutputStream _outputBuffer = new OutputStream();
        protected ulong _nextReceivedBytesCountReport = RECEIVED_BYTES_COUNT_REPORT_CHUNK;
        protected uint _winAckSize = RECEIVED_BYTES_COUNT_REPORT_CHUNK;
        protected Dictionary<uint, Channel> _channels = new Dictionary<uint, Channel>();
        protected Stack<uint> _channelPool = new Stack<uint>(); 
        protected int _selectedChannel = -1;
        protected uint _inboundChunkSize = 128;
        public uint _outboundChunkSize = 128;
        protected BaseRTMPAppProtocolHandler _pProtocolHandler;
        protected readonly RTMPProtocolSerializer _rtmpProtocolSerializer = new RTMPProtocolSerializer();
        protected readonly IStream[] _streams = new IStream[MAX_STREAMS_COUNT];
        public event Action OnReadyForSend;
       // public ConcurrentExclusiveSchedulerPair Cesp = new ConcurrentExclusiveSchedulerPair();
    
        protected ulong _rxInvokes;
        protected ulong _txInvokes;
        
        //public BatchBlock<Tuple<AmfMessage, bool>> SendMessagesBlock = new BatchBlock<Tuple<AmfMessage,bool>>(100);
        protected BaseRTMPProtocol()
        {
            //var sendMessagesBlock = new ActionBlock<Tuple<AmfMessage, bool>[]>(messages =>
            //{
            //    foreach (var amfMessage in messages)
            //    {
            //        _rtmpProtocolSerializer.Serialize(GetChannel(amfMessage.Item1.ChannelId), amfMessage.Item1, OutputBuffer,
            //            _outboundChunkSize);
            //        if(amfMessage.Item2)amfMessage.Item1.Body.Recycle();
            //        _txInvokes++;
            //    }
            //    EnqueueForOutbound(OutputBuffer);
            //},new ExecutionDataflowBlockOptions() {TaskScheduler = Cesp.ExclusiveScheduler });
            //SendMessagesBlock.LinkTo(sendMessagesBlock);
        }

        public override MemoryStream OutputBuffer { get; } = Utils.Rms.GetStream();

        public override BaseClientApplication Application
        {
            set
            {
                base.Application = value;
                _pProtocolHandler = value?.GetProtocolHandler<BaseRTMPAppProtocolHandler>(this);
            }
        }

        public override void Dispose()
        {
            for (var i = 0; i < MAX_STREAMS_COUNT; i++)
            {
                if (_streams[i] == null) continue;
                _streams[i].Dispose();
                _streams[i] = null;
            }
            //SendMessagesBlock.Complete();
            OutputBuffer.Dispose();
            base.Dispose();
        }

        public override void ReadyForSend() => OnReadyForSend?.Invoke();

        public void TrySetOutboundChunkSize(uint chunkSize)
        {
            if (_outboundChunkSize >= chunkSize) return;
            _outboundChunkSize = chunkSize;
            SendMessage(GenericMessageFactory.GetChunkSize(_outboundChunkSize));
#if PARALLEL
            _streams.Where(x => x != null && x.Type.TagKindOf(StreamTypes.ST_OUT_NET_RTMP)).Cast<BaseOutNetRTMPStream>().AsParallel().ForAll(
                 y => y.ChunkSize = _outboundChunkSize);
#else
            foreach (
                var baseStream in
                    _streams.Where(x => x != null && x.Type.TagKindOf(ST_OUT_NET_RTMP))
                        .Cast<BaseOutNetRTMPStream>())
                baseStream.ChunkSize = _outboundChunkSize;
#endif
        }

        public Channel GetChannel(uint id)
        {
            if (!_channels.ContainsKey(id))
            {
                Channel result;
                if (GlobalPool<Channel>.GetObject(out result, id))
                {
                    result.id = id;
                }
                _channels[id] = result;
            }
            return _channels[id];
        }
      
        private bool ProcessBytes(InputStream buffer)
        {
            while (true)
            {
                var availableBytesCount = buffer.AvaliableByteCounts;
                if (_selectedChannel < 0)
                {
                    if (availableBytesCount < 1) return true;
                    var temp = buffer.ReadByte();
                    switch (temp & 0x3f)
                    {
                        case 0:
                            if (availableBytesCount < 2)
                            {
                                FINEST("Not enough data");
                                return true;
                            }
                            _selectedChannel = 64 + buffer.ReadByte();
                            GetChannel((uint)_selectedChannel).lastInHeaderType = (byte) (temp >> 6);
                            availableBytesCount -= 2;
                            break;
                        case 1:
                            FATAL("The server doesn't support channel ids bigger than 319");
                            return false;
                        default:
                            _selectedChannel = temp & 0x3f;
                            GetChannel((uint)_selectedChannel).lastInHeaderType = (byte) (temp >> 6);
                            availableBytesCount -= 1;
                            break;
                    }
                }
                if (_selectedChannel >= MAX_CHANNELS_COUNT)
                {
                    FATAL("Bogus connection. Drop it like is hot");
                    return false;
                }
                var channel = GetChannel((uint)_selectedChannel);
                switch (channel.state)
                {
                    case Channel.CS_HEADER:
                        if (!channel.lastInHeader.Read((uint)_selectedChannel, channel.lastInHeaderType, buffer, availableBytesCount))
                        {
                            FATAL("Unable to read header");
                            return false;
                        }
                        if (!channel.lastInHeader.ReadCompleted) return true;
                   
                        var ts = channel.lastInHeader.TimeStramp;
                        switch (channel.lastInHeaderType)
                        {
                            case HT_FULL:
                                channel.lastInAbsTs = ts;
                                break;
                            case HT_SAME_STREAM:
                            case HT_SAME_LENGTH_AND_STREAM:
                                channel.lastInAbsTs += ts;
                                break;
                            case HT_CONTINUATION:
                                if (channel.lastInProcBytes == 0)
                                    channel.lastInAbsTs += ts;
                                break;
                        }
                        channel.state = Channel.CS_PAYLOAD;
                        goto case Channel.CS_PAYLOAD;
               
                    case Channel.CS_PAYLOAD:
                        var ml = channel.lastInHeader.MessageLength;
                        var si = channel.lastInHeader.StreamId;
                        var tempSize = ml - channel.lastInProcBytes;
                        tempSize = tempSize >= _inboundChunkSize ? _inboundChunkSize : tempSize;
                        if (tempSize > buffer.AvaliableByteCounts) return true;
                        channel.state = Channel.CS_HEADER;
                        _selectedChannel = -1;
                        var msgType = channel.lastInHeader.MessageType;
                        switch (msgType)
                        {
                            case RM_HEADER_MESSAGETYPE_VIDEODATA:
                                if (si >= MAX_STREAMS_COUNT)
                                {
                                    FATAL("The server doesn't support stream ids bigger than {0}", MAX_STREAMS_COUNT);
                                    return false;
                                }
                                if (_streams[si]?.Type == ST_IN_NET_RTMP)
                                {
                                    if (!_streams[si].FeedData(buffer,
                                        tempSize, channel.lastInProcBytes, ml, channel.lastInAbsTs, false))
                                    {
                                        FATAL("Unable to feed video");
                                        return false;
                                    }
                                }
                                channel.lastInProcBytes += tempSize;
                                if (ml == channel.lastInProcBytes)channel.lastInProcBytes = 0;
                                buffer.Ignore(tempSize);
                                break;
                            case RM_HEADER_MESSAGETYPE_AUDIODATA:
                                if (si >= MAX_STREAMS_COUNT)
                                {
                                    FATAL("The server doesn't support stream ids bigger than {0}",MAX_STREAMS_COUNT);
                                    return false;
                                }

                                if (_streams[si]?.Type == ST_IN_NET_RTMP)
                                {
                                    if (!_streams[si].FeedData(buffer,
                                        tempSize, channel.lastInProcBytes, ml, channel.lastInAbsTs, true))
                                    {
                                        FATAL("Unable to feed video");
                                        return false;
                                    }
                                }
                                channel.lastInProcBytes += tempSize;
                                if (ml == channel.lastInProcBytes)
                                {
                                    channel.lastInProcBytes = 0;
                                }
                                buffer.Ignore(tempSize);
                                break;
                            default:
                                buffer.CopyPartTo(channel.inputData.BaseStream, (int)tempSize);
                                buffer.Recycle();
                                channel.lastInProcBytes += tempSize;

                                if (ml == channel.lastInProcBytes)
                                {
                                    channel.lastInProcBytes = 0;
                                    if (_pProtocolHandler == null)
                                    {
                                        FATAL("RTMP connection no longer associated with an application");
                                        return false;
                                    }
                                    channel.inputData.BaseStream.Position = 0;
                                   
                                    if (msgType != 0)
                                    {
                                        var messageBody = _rtmpProtocolSerializer.Deserialize(msgType, channel.inputData);
                                        bool recycleBody;
                                        if (!_pProtocolHandler.InboundMessageAvailable(this, messageBody, channel, out recycleBody))
                                        {
                                            if (recycleBody) messageBody.Recycle();
                                            FATAL("Unable to send rtmp message to application");
                                            return false;
                                        }

                                        if (recycleBody) messageBody.Recycle();
                                        _rxInvokes++;
                                        if (channel.inputData.BaseStream.Position < channel.inputData.BaseStream.Length)
                                        {
                                            FATAL("Invalid message!!! We have leftovers:{0} bytes",
                                                channel.inputData.BaseStream.Position < channel.inputData.BaseStream.Length);
                                            return false;
                                        }
                                    }
                                    channel.inputData.BaseStream.Recycle();
                                }
                                break;
                        }
                        break;
                }
            }
        }

        protected abstract bool PerformHandshake(InputStream buffer);


        public override bool SignalInputData(int recAmount)
        {
            if (_enqueueForDelete) return true;
            bool result;
            if (_handshakeCompleted)
            {
                result = ProcessBytes(InputBuffer);
                var decodedBytes = GetDecodedBytesCount();
                if (result && (decodedBytes >= _nextReceivedBytesCountReport))
                {
                    var _bytesReadMessage = GenericMessageFactory.GetAck(decodedBytes);
                    _nextReceivedBytesCountReport += _winAckSize;
                    if (!SendMessage(_bytesReadMessage))
                    {
                        FATAL("Unable to send\n{0}", _bytesReadMessage.ToString());
                        return false;
                    }
                }
            }
            else
            {
                result = PerformHandshake(InputBuffer);
                if (!result)
                {
                    FATAL("Unable to perform handshake");
                    return false;
                }
                if (_handshakeCompleted)
                {
                    result = SignalInputData(recAmount);
                    if (result && (Type == ProtocolTypes.PT_OUTBOUND_RTMP))
                    {
                        result = _pProtocolHandler.OutboundConnectionEstablished((OutboundRTMPProtocol)this);
                    }
                }
            }
            return result;
        }

        //public override bool AllowNearProtocol(ulong type)
        //{
        //    Logger.FATAL("This protocol doesn't allow any near protocols");
        //    return false;
        //}

        //public override bool AllowFarProtocol(ulong type)
        //{
        //    return type == ProtocolTypes.PT_TCP || type == ProtocolTypes.PT_RTMPE || type == ProtocolTypes.PT_INBOUND_SSL ||
        //           type == ProtocolTypes.PT_INBOUND_HTTP_FOR_RTMP;
        //}
        public void ChunkAmfMessage( Header header,BufferWithOffset input, MemoryStream output)
        {
            var channel = GetChannel(header.ChannelId);
            long available;
            while ((available = input.Length) != 0)
            {
                header.Write(channel, output);
                if (available > _outboundChunkSize)
                {
                    available = _outboundChunkSize;
                }
                output.Write(input.Buffer, input.Offset, (int)available);
                channel.lastOutProcBytes += (uint)available;
                input.Offset += (int)available;
            }
            channel.lastOutProcBytes = 0;
        }

        

     
        public bool SendMessage(AmfMessage message, bool enqueueForOutbound = false, bool recycleMessageBody = true)
        {
            //SendMessagesBlock.Post(new Tuple<AmfMessage,bool>(message, recycleMessageBody));
            //if(enqueueForOutbound)SendMessagesBlock.TriggerBatch();
            //return true;
            lock (_rtmpProtocolSerializer)
            {
                if (!_rtmpProtocolSerializer.Serialize(GetChannel(message.ChannelId), message, OutputBuffer, _outboundChunkSize))
                {
                    FATAL("Unable to serialize RTMP message");
                    return false;
                }
                if (recycleMessageBody) message.Body.Recycle();
                _txInvokes++;
                return !enqueueForOutbound || EnqueueForOutbound(OutputBuffer);
            }
        }

        public bool SendMessages(params AmfMessage[] messages)
        {
            //foreach (var amfMessage in messages)
            //{
            //    SendMessagesBlock.Post(new Tuple<AmfMessage, bool>(amfMessage, true));
            //}
            //SendMessagesBlock.TriggerBatch();

            //return true;
            lock (_rtmpProtocolSerializer)
            {
                foreach (var amfMessage in messages)
                {
                    if (!_rtmpProtocolSerializer.Serialize(GetChannel(amfMessage.ChannelId), amfMessage, OutputBuffer, _outboundChunkSize))
                    {
                        FATAL("Unable to serialize RTMP message");
                        return false;
                    }
                    //ChunkAmfMessage( amfMessage.Header, _rtmpProtocolSerializer.InternalBuffer, output, _outboundChunkSize);
                    //_rtmpProtocolSerializer.InternalBuffer.SetLength(0);
                    amfMessage.Body.Recycle();
                    _txInvokes++;
                }
            }
            return EnqueueForOutbound(OutputBuffer);
        }
       
        public Channel ReserveChannel()
        {
            if (_channelPool.Count > 0)
            {
                return GetChannel(_channelPool.Pop());
            }
            for (uint i = MIN_AV_CHANNLES; i < MAX_CHANNELS_COUNT; i++)
            {
                if (_channels.ContainsKey(i)) continue;
                return GetChannel(i);
            }
            throw new Exception("no channel can use");
        }

        public void ReleaseChannel(Channel pChannel)
        {
            if (pChannel == null) return;
            _channels.Remove(pChannel.id);
            _channelPool.Push(pChannel.id);
            pChannel.ReturnPool();
        }

        public bool EnqueueForTimeEvent(uint seconds)
        {
            ASSERT("Operation not supported. Please use a timer protocol");
            return false;
        }

        public uint GetDHOffset(BufferWithOffset pBuffer, byte schemeNumber)
        {
            switch (schemeNumber)
            {
                case 0:
                    return GetDHOffset0(pBuffer);
                case 1:
                    return GetDHOffset1(pBuffer);
                default:
                    WARN("Invalid scheme number:{0}", schemeNumber);
                    return GetDHOffset0(pBuffer);
            }
        }

        public uint GetDHOffset0(BufferWithOffset pBuffer)
        {
            var offset = (uint)(pBuffer[1532] + pBuffer[1533] + pBuffer[1534] + pBuffer[1535]);
            offset = offset % 632;
            offset = offset + 772;
            if (offset + 128 >= 1536) ASSERT("Invalid DH offset");
            return offset;
        }
        public uint GetDHOffset1(BufferWithOffset pBuffer)
        {
            var offset = (uint)(pBuffer[768] + pBuffer[769] + pBuffer[770] + pBuffer[771]);
            offset = offset % 632;
            offset = offset + 8;
            if (offset + 128 >= 1536)
            {
                ASSERT("Invalid DH offset");
            }
            return offset;
        }

        public uint GetDigestOffset(BufferWithOffset pBuffer, byte schemeNumber)
        {
            switch (schemeNumber)
            {
                case 0:
                    return GetDigestOffset0(pBuffer);
                case 1:
                    return GetDigestOffset1(pBuffer);
                default:
                    WARN("Invalid scheme number: {0}. Defaulting to 0", schemeNumber);
                    return GetDigestOffset0(pBuffer);
            }
        }
        public uint GetDigestOffset0(BufferWithOffset pBuffer)
        {
            var offset = (uint)(pBuffer[8] + pBuffer[9] + pBuffer[10] + pBuffer[11]);
            offset = offset % 728;
            offset = offset + 12;
            if (offset + 32 >= 1536)
            {
                ASSERT("Invalid digest offset");
            }
            return offset;
        }

        public uint GetDigestOffset1(BufferWithOffset pBuffer)
        {
            var offset = (uint)(pBuffer[772] + pBuffer[773] + pBuffer[774] + pBuffer[775]);
            offset = offset % 728;
            offset = offset + 776;
            if (offset + 32 >= 1536)
            {
                ASSERT("Invalid digest offset");
            }
            return offset;
        }

        public void SetWinAckSize(uint winAckSize)
        {
            _nextReceivedBytesCountReport -= _winAckSize;
            _winAckSize = winAckSize;
            _nextReceivedBytesCountReport += _winAckSize;
        }

        public bool ResetChannel(uint channelId)
        {
            if (channelId >= MAX_CHANNELS_COUNT)
            {
                FATAL("Invalid channel id in reset message: {0}", channelId);
                return false;
            }
            _channels[channelId].Reset();
            return true;
        }

        public bool CloseStream(uint streamId, bool createNeutralStream)
        {
            //1. Validate request
            if (streamId == 0 || streamId >= MAX_STREAMS_COUNT)
            {
                FATAL("Invalid stream id: {0}", streamId);
                return false;
            }

            if (_streams[streamId] == null)
            {
                FATAL("Try to close a NULL stream");
                return false;
            }

            if (_streams[streamId].Type.TagKindOf(ST_OUT_NET_RTMP))
            {
                //2. Remove it from signaled streams
                //var temp = _pSignaledRTMPOutNetStream.FirstOrDefault(x => x.RTMPStreamId == streamId);
                //_pSignaledRTMPOutNetStream.Remove(temp);
              
                //3. If this is an outbound network stream and his publisher
                //is a file, close that as well
                var pBaseOutNetRTMPStream = (BaseOutNetRTMPStream)_streams[streamId];
                if (pBaseOutNetRTMPStream.InStream != null)
                {
                    if (pBaseOutNetRTMPStream.InStream.Type.TagKindOf(ST_IN_FILE_RTMP))
                        RemoveIFS((InFileRTMPStream)pBaseOutNetRTMPStream.InStream);
                }
            }

            //4. Delete the stream and replace it with a neutral one

            _streams[streamId].Dispose();

            _streams[streamId] = createNeutralStream ? RTMPStream.I : null;

            return true;
        }

        private void RemoveIFS(InFileRTMPStream pIFS) => pIFS.Dispose();

        public RTMPStream CreateNeutralStream(ref uint streamId)
        {
            if (streamId == 0)
            {
                //Automatic allocation
                for (uint i = 1; i < MAX_STREAMS_COUNT; i++)
                {
                    if (_streams[i] == null)
                    {
                        streamId = i;
                        break;
                    }
                }

                if (streamId == 0)
                {
                    return null;
                }
            }
            else
            {
                if (streamId == 0 || streamId >= MAX_STREAMS_COUNT)
                {
                    FATAL("Invalid stream id: {0}", streamId);
                    return null;
                }
                if (_streams[streamId] != null)
                {
                    FATAL("Try to create a neutral stream on a non NULL placeholder");
                    return null;
                }

            }
            var pStream = RTMPStream.I;
            
            _streams[streamId] = pStream;

            return pStream;
        }

        public InNetRTMPStream CreateINS(uint channelId, uint streamId, string streamName)
        {
            if (streamId == 0 || streamId >= MAX_STREAMS_COUNT)
            {
                FATAL("Invalid stream id: {0}", streamId);
                return null;
            }
            if (_streams[streamId] == null)
            {
                FATAL("Try to publish a stream on a NULL placeholder");
                return null;
            }
            if (_streams[streamId].Type != ST_NEUTRAL_RTMP)
            {
                FATAL("Try to publish a stream over a non neutral stream");
                return null;
            }
            _streams[streamId].Dispose();
            return (InNetRTMPStream)(_streams[streamId] = new InNetRTMPStream(this,
                Application.StreamsManager, streamName, streamId,
                _inboundChunkSize, channelId));
        }

        public BaseOutNetRTMPStream CreateONS(uint streamId, string streamName, ulong inStreamType)
        {
            if (streamId == 0 || streamId >= MAX_STREAMS_COUNT)
            {
                FATAL("Invalid stream id: {0}", streamId);
                return null;
            }
            if (_streams[streamId] == null)
            {
                FATAL("Try to play a stream on a NULL placeholder");
                return null;
            }
            if (_streams[streamId].Type != ST_NEUTRAL_RTMP)
            {
                FATAL("Try to play a stream over a non neutral stream");
                return null;
            }
            _streams[streamId].Dispose();
            return (BaseOutNetRTMPStream)(_streams[streamId] = BaseOutNetRTMPStream.GetInstance(this,
                Application.StreamsManager, streamName, streamId,
                _outboundChunkSize, inStreamType));
        }

        //public void SignalONS(BaseOutNetRTMPStream pONS)
        //{
        //    if (_pSignaledRTMPOutNetStream.Contains(pONS)) return;
        //    _pSignaledRTMPOutNetStream.AddLast(pONS);
        //}

        public InFileRTMPStream CreateIFS(Variant metadata)
        {
            var pRtmpInFileStream = InFileRTMPStream.GetInstance(this, Application.StreamsManager, metadata);
            if (pRtmpInFileStream == null)
            {
                WARN("Unable to get file stream. Metadata:\n{0}", metadata.ToString());
                return null;
            }
            if (!pRtmpInFileStream.Initialize(metadata[CONF_APPLICATION_CLIENTSIDEBUFFER]))
            {
                WARN("Unable to initialize file inbound stream");
                pRtmpInFileStream.Dispose();
                return null;
            }
           // _inFileStreams.Add(pRtmpInFileStream);
            return pRtmpInFileStream;
        }


        public bool SetInboundChunkSize(uint chunkSize)
        {
            _inboundChunkSize = chunkSize;
#if PARALLEL
            _streams.Where(x => x != null && x.Type.TagKindOf(StreamTypes.ST_IN_NET_RTMP)).OfType<InNetRTMPStream>().AsParallel().ForAll(x => x.ChunkSize = chunkSize);
#else
            foreach (var baseStream in _streams.Where(x => x != null && x.Type.TagKindOf(ST_IN_NET_RTMP)).OfType<InNetRTMPStream>())
            {
                baseStream.ChunkSize = _inboundChunkSize;
            }
#endif
            return true;
        }
    }
}
