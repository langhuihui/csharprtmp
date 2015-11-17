using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.Protocols.Rtmp
{
    public class RTMPProtocolSerializer
    {
        public MemoryStream InternalBuffer = Utils.Rms.GetStream();
        private readonly AMF0Writer writer;
        public RTMPProtocolSerializer ()
        {
            writer = new AMF0Writer(InternalBuffer);
        }
        public static string GetUserCtrlTypeString(ushort type)
        {
            switch (type)
            {
                case Defines.RM_USRCTRL_TYPE_STREAM_BEGIN:
                    return "RM_USRCTRL_TYPE_STREAM_BEGIN";
                case Defines.RM_USRCTRL_TYPE_STREAM_EOF:
                    return "RM_USRCTRL_TYPE_STREAM_EOF";
                case Defines.RM_USRCTRL_TYPE_STREAM_DRY:
                    return "RM_USRCTRL_TYPE_STREAM_DRY";
                case Defines.RM_USRCTRL_TYPE_STREAM_IS_RECORDED:
                    return "RM_USRCTRL_TYPE_STREAM_IS_RECORDED";
                case Defines.RM_USRCTRL_TYPE_STREAM_SET_BUFFER_LENGTH:
                    return "RM_USRCTRL_TYPE_STREAM_SET_BUFFER_LENGTH";
                case Defines.RM_USRCTRL_TYPE_PING_REQUEST:
                    return "RM_USRCTRL_TYPE_PING_REQUEST";
                case Defines.RM_USRCTRL_TYPE_PING_RESPONSE:
                    return "RM_USRCTRL_TYPE_PING_RESPONSE";
                case Defines.RM_USRCTRL_TYPE_UNKNOWN1:
                    return "RM_USRCTRL_TYPE_UNKNOWN1";
                case Defines.RM_USRCTRL_TYPE_UNKNOWN2:
                    return "RM_USRCTRL_TYPE_UNKNOWN2";
                default:
                    return string.Format("#unknownUCT({0})", type);
            }
        }

        public static string GetSOPrimitiveString(byte type)
        {
            switch (type)
            {
                case Defines.SOT_CS_CONNECT:
                    return "SOT_CS_CONNECT";
                case Defines.SOT_CS_DISCONNECT:
                    return "SOT_CS_DISCONNECT";
                case Defines.SOT_CS_SET_ATTRIBUTE:
                    return "SOT_CS_SET_ATTRIBUTE";
                case Defines.SOT_SC_UPDATE_DATA:
                    return "SOT_SC_UPDATE_DATA";
                case Defines.SOT_SC_UPDATE_DATA_ACK:
                    return "SOT_SC_UPDATE_DATA_ACK";
                case Defines.SOT_BW_SEND_MESSAGE:
                    return "SOT_BW_SEND_MESSAGE";
                case Defines.SOT_SC_STATUS:
                    return "SOT_SC_STATUS";
                case Defines.SOT_SC_CLEAR_DATA:
                    return "SOT_SC_CLEAR_DATA";
                case Defines.SOT_SC_DELETE_DATA:
                    return "SOT_SC_DELETE_DATA";
                case Defines.SOT_CSC_DELETE_DATA:
                    return "SOT_CSC_DELETE_DATA";
                case Defines.SOT_SC_INITIAL_DATA:
                    return "SOT_SC_INITIAL_DATA";
                default:
                    return string.Format("#unknownSOP{0})", type);
            }
        }

        public Variant Deserialize(int msgType, AMF0Reader _amf0)
        {

            var messageBody = Variant.Get();
            var stream = _amf0.BaseStream;
            //messageBody[Defines.RM_HEADER] = header.GetVariant();
            switch (msgType)
            {
                case Defines.RM_HEADER_MESSAGETYPE_NOTIFY:
                    messageBody[Defines.RM_NOTIFY, Defines.RM_NOTIFY_PARAMS] = Variant.Get();
                    while (_amf0.Available)
                        messageBody[Defines.RM_NOTIFY, Defines.RM_NOTIFY_PARAMS].Add(_amf0.ReadVariant());
                    break;
                case Defines.RM_HEADER_MESSAGETYPE_FLEXSTREAMSEND:
                    messageBody[Defines.RM_FLEXSTREAMSEND, Defines.RM_FLEXSTREAMSEND_UNKNOWNBYTE] = stream.ReadByte();
                    messageBody[Defines.RM_FLEXSTREAMSEND, Defines.RM_FLEXSTREAMSEND_PARAMS] = Variant.Get();
                    while (_amf0.Available)
                        messageBody[Defines.RM_FLEXSTREAMSEND, Defines.RM_FLEXSTREAMSEND_PARAMS].Add(_amf0.ReadVariant());
                    break;
               
                case Defines.RM_HEADER_MESSAGETYPE_FLEX:
                    messageBody[Defines.RM_INVOKE, Defines.RM_INVOKE_IS_FLEX] = true;
                    stream.ReadByte();
                    goto case Defines.RM_HEADER_MESSAGETYPE_INVOKE;
                case Defines.RM_HEADER_MESSAGETYPE_INVOKE:
                    messageBody[Defines.RM_INVOKE, Defines.RM_INVOKE_FUNCTION] = _amf0.ReadShortString(true);
                    messageBody[Defines.RM_INVOKE, Defines.RM_INVOKE_ID] = _amf0.ReadAMFDouble(true);
                    messageBody[Defines.RM_INVOKE, Defines.RM_INVOKE_PARAMS] = Variant.Get();
                    while (_amf0.Available)
                        messageBody[Defines.RM_INVOKE, Defines.RM_INVOKE_PARAMS].Add(_amf0.ReadVariant());
                    break;
                case Defines.RM_HEADER_MESSAGETYPE_FLEXSHAREDOBJECT:
                    if (stream.ReadByte() != 0) throw new NotSupportedException();
                    goto case Defines.RM_HEADER_MESSAGETYPE_SHAREDOBJECT;
                case Defines.RM_HEADER_MESSAGETYPE_SHAREDOBJECT:
                    messageBody[Defines.RM_SHAREDOBJECT, Defines.RM_SHAREDOBJECT_NAME] = _amf0.ReadShortString();
                    messageBody[Defines.RM_SHAREDOBJECT, Defines.RM_SHAREDOBJECT_VERSION] = _amf0.ReadUInt32();
                    messageBody[Defines.RM_SHAREDOBJECT, Defines.RM_SHAREDOBJECT_PERSISTENCE] = _amf0.ReadUInt32()==2;
                    stream.Position += 4;
                    messageBody[Defines.RM_SHAREDOBJECT, Defines.RM_SHAREDOBJECT_PRIMITIVES] = Variant.Get();
                    while (_amf0.Available)
                    {
                        var primitive = Variant.Get();
                        primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_TYPE] = _amf0.ReadByte();
                        primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_STRTYPE] =
                             GetSOPrimitiveString(primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_TYPE]);
                        uint rawLength = 0;
                        primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_RAWLENGTH] = rawLength = _amf0.ReadUInt32();
                        switch ((byte)primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_TYPE])
                        {
                            case Defines.SOT_CS_CONNECT:
                            case Defines.SOT_CS_DISCONNECT:
                                break;
                            case Defines.SOT_CS_SET_ATTRIBUTE:
                                long read = 0;
                                while (read < rawLength)
                                {
                                    var afterRead = stream.Position;
                                    var key = _amf0.ReadShortString();
                                    primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_PAYLOAD, key] = _amf0.ReadVariant();
                                    read += stream.Position - afterRead;
                                }
                                break;
                            case Defines.SOT_CSC_DELETE_DATA:
                                primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_PAYLOAD] = _amf0.ReadShortString();
                                //read = 0;
                                //while (read < rawLength)
                                //{
                                //    var afterRead = stream.Position;
                                //    var key = _amf0.ReadShortString();
                                //    read += stream.Position - afterRead;
                                //    primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_PAYLOAD].Add(key);
                                //}
                                break;
                            default:
                                Logger.FATAL("Invalid SO primitive type. Partial result:{0}",messageBody.ToString());
                                break;
                        }
                        messageBody[Defines.RM_SHAREDOBJECT, Defines.RM_SHAREDOBJECT_PRIMITIVES].Add(primitive);
                    }
                    
                    break;
                case Defines.RM_HEADER_MESSAGETYPE_USRCTRL:
                    messageBody[Defines.RM_USRCTRL, Defines.RM_USRCTRL_TYPE] = _amf0.ReadUInt16();
                    messageBody[Defines.RM_USRCTRL, Defines.RM_USRCTRL_TYPE_STRING] =
                        GetUserCtrlTypeString(messageBody[Defines.RM_USRCTRL, Defines.RM_USRCTRL_TYPE]);
                    switch ((ushort)messageBody[Defines.RM_USRCTRL, Defines.RM_USRCTRL_TYPE])
                    {
                        case Defines.RM_USRCTRL_TYPE_STREAM_BEGIN:
                        case Defines.RM_USRCTRL_TYPE_STREAM_EOF:
                        case Defines.RM_USRCTRL_TYPE_STREAM_DRY:
                        case Defines.RM_USRCTRL_TYPE_STREAM_IS_RECORDED:
                            messageBody[Defines.RM_USRCTRL, Defines.RM_USRCTRL_STREAMID] = _amf0.ReadUInt32();
                            break;
                        case Defines.RM_USRCTRL_TYPE_STREAM_SET_BUFFER_LENGTH:
                            messageBody[Defines.RM_USRCTRL, Defines.RM_USRCTRL_STREAMID] = _amf0.ReadUInt32();
                            messageBody[Defines.RM_USRCTRL, Defines.RM_USRCTRL_BUFFLEN] = _amf0.ReadUInt32();
                            break;
                        case Defines.RM_USRCTRL_TYPE_PING_REQUEST:
                            messageBody[Defines.RM_USRCTRL, Defines.RM_USRCTRL_PING] = _amf0.ReadUInt32();
                            break;
                        case Defines.RM_USRCTRL_TYPE_PING_RESPONSE:
                            messageBody[Defines.RM_USRCTRL, Defines.RM_USRCTRL_PONG] = _amf0.ReadUInt32();
                            break;
                        case Defines.RM_USRCTRL_TYPE_UNKNOWN1:
                        case Defines.RM_USRCTRL_TYPE_UNKNOWN2:
                            messageBody[Defines.RM_USRCTRL_UNKNOWN_U32] = _amf0.ReadUInt32();
                            break;
                        
                        default:
                            throw new InvalidDataException("Invalid user control message:"+messageBody);
                    }
                    break;
                case Defines.RM_HEADER_MESSAGETYPE_CHUNKSIZE:
                    messageBody[Defines.RM_CHUNKSIZE] = _amf0.ReadUInt32();
                    break;
                case Defines.RM_HEADER_MESSAGETYPE_ACK:
                    messageBody[Defines.RM_ACK] = _amf0.ReadUInt32();
                    break;
                case Defines.RM_HEADER_MESSAGETYPE_WINACKSIZE:
                    messageBody[Defines.RM_WINACKSIZE] = _amf0.ReadUInt32();
                    break;
                case Defines.RM_HEADER_MESSAGETYPE_PEERBW:
                    messageBody[Defines.RM_PEERBW, Defines.RM_PEERBW_VALUE] = _amf0.ReadUInt32();
                    messageBody[Defines.RM_PEERBW, Defines.RM_PEERBW_TYPE] = stream.ReadByte();
                    break;
                case Defines.RM_HEADER_MESSAGETYPE_ABORTMESSAGE:
                    messageBody[Defines.RM_ABORTMESSAGE] = _amf0.ReadUInt32();
                    break;
                case 0:
                    break;
                default:
                    Logger.FATAL("Invalid message type");
                    break;
            }
            return messageBody;
        }

        public bool Serialize(Channel channel, AmfMessage message, Stream stream, uint chuckSize)
        {
            Serialize(ref message);
            var header = message.Header;
            long available;
            InternalBuffer.Position = 0;
            while ((available = InternalBuffer.Length - InternalBuffer.Position) != 0)
            {
                header.Write(channel, stream);
                if (available >= chuckSize)
                {
                    InternalBuffer.CopyPartTo(stream, (int)chuckSize);
                    channel.lastOutProcBytes += chuckSize;
                }
                else
                {
                    InternalBuffer.CopyPartTo(stream, (int)available);
                    channel.lastOutProcBytes += (uint)available;
                }
            }
            //Debug.WriteLine(channel.lastOutProcBytes);
            channel.lastOutProcBytes = 0;
            InternalBuffer.Recycle();
            return true;
        }

        public bool Serialize(ref AmfMessage message)
        {
                //var result = false;
            var messageBody = message.Body;
                switch ((uint) message.MessageType)
                {
                    case Defines.RM_HEADER_MESSAGETYPE_INVOKE:
                        writer.WriteShortString(message.InvokeFunction, true);
                        writer.WriteDouble(messageBody[Defines.RM_INVOKE, Defines.RM_INVOKE_ID], true);
                        foreach (var item in messageBody[Defines.RM_INVOKE, Defines.RM_INVOKE_PARAMS].Children)
                            writer.WriteVariant(item.Value);
                        break;
                    case Defines.RM_HEADER_MESSAGETYPE_NOTIFY:
                        foreach (var item in messageBody[Defines.RM_NOTIFY, Defines.RM_NOTIFY_PARAMS].Children)
                            writer.WriteVariant(item.Value);
                        break;
                    case Defines.RM_HEADER_MESSAGETYPE_FLEXSTREAMSEND:
                        InternalBuffer.WriteByte(
                            messageBody[Defines.RM_FLEXSTREAMSEND, Defines.RM_FLEXSTREAMSEND_UNKNOWNBYTE]);
                        foreach (
                            var item in messageBody[Defines.RM_FLEXSTREAMSEND, Defines.RM_FLEXSTREAMSEND_PARAMS].Children)
                            writer.WriteVariant(item.Value);
                        break;
                    case Defines.RM_HEADER_MESSAGETYPE_SHAREDOBJECT:
                        writer.WriteShortString(messageBody[Defines.RM_SHAREDOBJECT, Defines.RM_SHAREDOBJECT_NAME]);
                        writer.Write((uint)messageBody[Defines.RM_SHAREDOBJECT, Defines.RM_SHAREDOBJECT_VERSION]);
                        writer.Write(messageBody[Defines.RM_SHAREDOBJECT, Defines.RM_SHAREDOBJECT_PERSISTENCE] ? 2U : 0U);
                        writer.Write(0);
                        foreach (
                            var primitive in
                                messageBody[Defines.RM_SHAREDOBJECT, Defines.RM_SHAREDOBJECT_PRIMITIVES].Children.Values)
                        {
                            byte type = primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_TYPE];
                            InternalBuffer.WriteByte(type);
                            long rawLengthPosition;
                            long length;
                            switch (type)
                            {
                                case Defines.SOT_SC_UPDATE_DATA:
                                case Defines.SOT_SC_INITIAL_DATA:
                                    rawLengthPosition = InternalBuffer.Position;
                                    writer.Write(0);
                                    if (primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_PAYLOAD] == null) break;
                                    foreach (var item in primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_PAYLOAD].Children)
                                    {
                                        writer.WriteShortString(item.Key);
                                        writer.WriteVariant(item.Value);
                                    }
                                    length = InternalBuffer.Position - rawLengthPosition - 4;
                                    InternalBuffer.Seek(rawLengthPosition, SeekOrigin.Begin);
                                    writer.Write((uint) length);
                                    InternalBuffer.Seek(0, SeekOrigin.End);
                                    break;
                                case Defines.SOT_SC_CLEAR_DATA:
                                    writer.Write(0);
                                    break;
                                case Defines.SOT_SC_DELETE_DATA:
                            case Defines.SOT_SC_UPDATE_DATA_ACK:
                                    rawLengthPosition = InternalBuffer.Position;
                                    writer.Write(0);
                                    foreach (var item in primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_PAYLOAD].Children)
                                        writer.WriteShortString(item.Value);
                                    length = InternalBuffer.Position - rawLengthPosition - 4;
                                    InternalBuffer.Seek(rawLengthPosition, SeekOrigin.Begin);
                                    writer.Write((uint) length);
                                    InternalBuffer.Seek(0, SeekOrigin.End);
                                    break;
                            }
                        }
                        break;
                    case Defines.RM_HEADER_MESSAGETYPE_ACK:
                        writer.Write((uint)messageBody[Defines.RM_ACK]);
                        break;
                    case Defines.RM_HEADER_MESSAGETYPE_USRCTRL:
                        writer.Write((short)messageBody[Defines.RM_USRCTRL, Defines.RM_USRCTRL_TYPE]);
                        switch ((short)messageBody[Defines.RM_USRCTRL, Defines.RM_USRCTRL_TYPE])
                        {
                            case Defines.RM_USRCTRL_TYPE_STREAM_BEGIN:
                            case Defines.RM_USRCTRL_TYPE_STREAM_EOF:
                            case Defines.RM_USRCTRL_TYPE_STREAM_DRY:
                            case Defines.RM_USRCTRL_TYPE_STREAM_IS_RECORDED:
                                writer.Write((int)messageBody[Defines.RM_USRCTRL, Defines.RM_USRCTRL_STREAMID]);
                                break;
                            case Defines.RM_USRCTRL_TYPE_PING_RESPONSE:
                                writer.Write((int)messageBody[Defines.RM_USRCTRL, Defines.RM_USRCTRL_PONG]);
                                break;
                            default:
                                Logger.FATAL("Invalid user control message");
                                break;
                        }
                        break;
                    case Defines.RM_HEADER_MESSAGETYPE_CHUNKSIZE:
                        writer.Write((uint)messageBody[Defines.RM_CHUNKSIZE]);
                        break;
                    case Defines.RM_HEADER_MESSAGETYPE_WINACKSIZE:
                        writer.Write((uint)messageBody[Defines.RM_WINACKSIZE]);
                        break;
                    case Defines.RM_HEADER_MESSAGETYPE_ABORTMESSAGE:
                        writer.Write((uint)messageBody[Defines.RM_ABORTMESSAGE]);
                        break;
                    case Defines.RM_HEADER_MESSAGETYPE_PEERBW:
                        writer.Write((uint)messageBody[Defines.RM_PEERBW, Defines.RM_PEERBW_VALUE]);
                        writer.Write((byte)messageBody[Defines.RM_PEERBW, Defines.RM_PEERBW_TYPE]);
                        break;
                    default:
                        Logger.FATAL("Invalid message type:{0}", message);
                        return false;
                }
                message.MessageLength = (uint)InternalBuffer.Length;
                InternalBuffer.Position = 0;
                return true;
            
        }
    }
}
