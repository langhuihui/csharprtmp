using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Streaming;
using static CSharpRTMP.Core.Protocols.Rtmp.HeaderType;

namespace CSharpRTMP.Core.Protocols.Rtmp
{
    public static class HeaderType
    {
        public const byte HT_FULL = 0;
        public const byte HT_SAME_STREAM = 1;
        public const byte HT_SAME_LENGTH_AND_STREAM = 2;
        public const byte HT_CONTINUATION = 3;
    }
    public struct AmfMessage
    {
        public Header Header;
        public Variant Body;
        public Variant InvokeParam => Body[Defines.RM_INVOKE, Defines.RM_INVOKE_PARAMS];

        public uint InvokeId
        {
            set { Body[Defines.RM_INVOKE, Defines.RM_INVOKE_ID] = value; }
            get { return Body[Defines.RM_INVOKE, Defines.RM_INVOKE_ID]; }
        }

        public string InvokeFunction
        {
            set { Body[Defines.RM_INVOKE, Defines.RM_INVOKE_FUNCTION] = value; }
            get { return Body[Defines.RM_INVOKE, Defines.RM_INVOKE_FUNCTION]; }
        }

        public byte HeaderType
        {
            set { Header.HeaderType = value; }
            get { return Header.HeaderType; }
        }
        public uint ChannelId
        {
            set { Header.ChannelId = value; }
            get { return Header.ChannelId; }
        }
        public uint StreamId
        {
            set { Header.StreamId = value; }
            get { return Header.StreamId; }
        }
        public byte MessageType
        {
            set { Header.MessageType = value; }
            get { return Header.MessageType; }
        }
        public uint MessageLength
        {
            set { Header.MessageLength = value; }
            get { return Header.MessageLength; }
        }
        public uint Timestamp
        {
            set { Header.TimeStramp = value; }
            get { return Header.TimeStramp; }
        }

        public bool Isabsolute
        {
            set { Header.IsAbsolute = value; }
            get { return Header.IsAbsolute; }
        }
    }
    public struct Header
    {
        public uint ChannelId;
        public byte HeaderType;
        public uint TimeStramp;
        public uint MessageLength;
        public byte MessageType;
        public uint StreamId;
        public bool ReadCompleted;
        public bool IsAbsolute;
        public bool Skip4Bytes;

        public void Reset(byte ht = 0, uint ci = 0, uint ts = 0, uint ml = 0, byte mt = 0, uint si = 0, bool ia = false)
        {
            HeaderType = ht;
            ChannelId = ci;
            TimeStramp = ts;
            MessageLength = ml;
            MessageType = mt;
            StreamId = si;
            ReadCompleted = false;
            IsAbsolute = ia;
            Skip4Bytes = false;
        }
        public bool Read(uint channelId, byte type, InputStream buffer, uint availableBytes)
        {
            HeaderType = type;
            ChannelId = channelId;
            var reader = buffer.Reader;
            //var temp = hf.datac;
            switch (HeaderType)
            {
                case HT_FULL:
                    IsAbsolute = true;
                    if (availableBytes < 11)
                    {
                        ReadCompleted = false;
                        return true;
                    }
                    TimeStramp = reader.ReadU24();
                    MessageLength = reader.ReadU24();
                    MessageType = (byte) buffer.ReadByte();
                    //StreamId = ((uint)buffer.ReadByte()) | ((uint)buffer.ReadByte() << 8) | ((uint)buffer.ReadByte() << 16) | ((uint)buffer.ReadByte() << 24);
                    StreamId = reader._ReadUInt32();
                    if (TimeStramp == 0x00ffffff)
                    {
                        Skip4Bytes = true;
                        if (availableBytes < 15)
                        {
                            ReadCompleted = false;
                            return true;
                        }
                        TimeStramp = reader.ReadUInt32();
                        ReadCompleted = true;
                        return true;
                    }
                    Skip4Bytes = false;
                    ReadCompleted = true;
                    return true;
                case HT_SAME_STREAM:
                    IsAbsolute = false;
                    if (availableBytes < 7)
                    {
                        ReadCompleted = false;
                        return true;
                    }
                    TimeStramp = reader.ReadU24();
                    MessageLength = reader.ReadU24();
                    MessageType = (byte) buffer.ReadByte();
                    //buffer.Read(temp, 1, 7);
                    //hf.datac = temp;
                    //ts = (uint)IPAddress.NetworkToHostOrder((int)ts) & 0x00ffffff;
                    //ml = (uint)IPAddress.NetworkToHostOrder((int)ml) >>8;
                    if (TimeStramp == 0x00ffffff)
                    {
                        Skip4Bytes = true;
                        if (availableBytes < 11)
                        {
                            ReadCompleted = false;
                            return true;
                        }
                        TimeStramp = reader.ReadUInt32();
                        ReadCompleted = true;
                        return true;
                    }
                    Skip4Bytes = false;
                    ReadCompleted = true;
                    return true;
                case HT_SAME_LENGTH_AND_STREAM:
                    IsAbsolute = false;
                    if (availableBytes < 3)
                    {
                        ReadCompleted = false;
                        return true;
                    }
                    TimeStramp = reader.ReadU24();
                    //buffer.Read(temp, 1, 3);
                    //hf.datac = temp;
                    //ts = (uint)IPAddress.NetworkToHostOrder((int)ts) & 0x00ffffff;

                    if (TimeStramp == 0x00ffffff)
                    {
                        Skip4Bytes = true;
                        if (availableBytes < 7)
                        {
                            ReadCompleted = false;
                            return true;
                        }
                        TimeStramp = reader.ReadUInt32();
                        ReadCompleted = true;
                        return true;
                    }
                    Skip4Bytes = false;
                    ReadCompleted = true;
                    return true;
                case HT_CONTINUATION:
                    IsAbsolute = false;
                    ReadCompleted = !Skip4Bytes || availableBytes >= 4;
                    return true;
                default:
                    Logger.FATAL("Invalid header type");
                    return false;
            }
        }

        public bool Write(Channel channel, Stream writer)
        { 
            
            if (channel.lastOutStreamId == StreamId)
            {
                if (IsAbsolute)
                {
                    if (channel.lastOutProcBytes == 0)
                    {
                        HeaderType = HT_FULL;
                        channel.lastOutAbsTs = TimeStramp;
                    }
                    else
                    {
                        HeaderType = HT_CONTINUATION;
                    }
                }
                else
                {
                    if (channel.lastOutProcBytes == 0)
                    {
                        HeaderType = HT_SAME_STREAM;
                        if (MessageType == channel.lastOutHeader.MessageType && MessageLength == channel.lastOutHeader.MessageLength)
                        {
                            HeaderType = HT_SAME_LENGTH_AND_STREAM;
                            if (TimeStramp == channel.lastOutHeader.TimeStramp)
                            {
                                HeaderType = HT_CONTINUATION;
                            }
                        }
                        channel.lastOutAbsTs += TimeStramp;
                    }
                    else
                    {
                        HeaderType = HT_CONTINUATION;
                    }
                }
            }
            else
            {
                HeaderType = HT_FULL;
                IsAbsolute = true;
                channel.lastOutAbsTs = TimeStramp;
                channel.lastOutStreamId = StreamId;
            }
            
            channel.lastOutHeader = this;
            return Write(writer);
        }

        public bool Write(Stream writer)
        {
                if (ChannelId < 64)
                {
                    writer.WriteByte((byte)((HeaderType << 6) | (byte)ChannelId));
                }
                else if (ChannelId < 319)
                {
                    writer.WriteByte((byte)(HeaderType << 6));
                }
                else if (ChannelId < 65599)
                {
                    writer.WriteByte((byte)((HeaderType << 6) | 0x01));
                    writer.Write((ushort)(ChannelId - 64));
                }
                else
                {
                    Logger.FATAL("Invalid channel index");
                    return false;
                }
                switch (HeaderType)
                {
                    case HT_FULL:
                        writer.Write24(TimeStramp);
                        writer.Write24(MessageLength);
                        writer.WriteByte(MessageType);
                        writer.WriteLittleEndian(StreamId);
                        goto case HT_CONTINUATION;
                    case HT_SAME_STREAM:
                        writer.Write24(TimeStramp);
                        writer.Write24(MessageLength);
                        writer.WriteByte(MessageType);
                        goto case HT_CONTINUATION;
                    case HT_SAME_LENGTH_AND_STREAM:
                        writer.Write24(TimeStramp);
                        goto case HT_CONTINUATION;
                    case HT_CONTINUATION:
                        if (TimeStramp >= 0x00ffffff)
                            writer.Write(TimeStramp);
                        return true;
                    default:
                        Logger.FATAL("Invalid header size");
                        return false;
                }
            
        }
    }
}
