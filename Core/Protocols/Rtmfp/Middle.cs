using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.NetIO;
using CSharpRTMP.Core.Protocols.Rtmp;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class MiddleHandshake : OutboundHandshake
    {
        public Middle MiddleSession;
        public MiddleHandshake(BaseRtmfpProtocol handler) : base(handler)
        {
        }

        public override void PacketHandler(N2HBinaryReader reader)
        {
            if (!Checked)
                base.PacketHandler(reader);
            else
                MiddleSession.SendStream(reader.BaseStream, (int) (reader.BaseStream as InputStream).Published);
        }

        public override void SendStream(Stream stream,int length)
        {
            var marker = stream.ReadByte() | 0xF0;
            var echoTime = marker == (Target == null ? 0xFE : 0xFD);
            stream.ReadUShort();
            if (echoTime) stream.ReadUShort();
            var type = stream.ReadByte();
            if (type == 0x10)
            {
                var sizePos = stream.Position;
                var size = stream.ReadUShort();
                var flags = stream.ReadByte();
                var idFlow = stream.Read7BitLongValue();
                var stage = stream.Read7BitLongValue();
                if (idFlow == 2 && stage == 1)
                {
                    var deltaNAck = stream.Read7BitLongValue();
                    var len = (ushort) stream.ReadByte();
                    stream.Position += len;
                    stream.ReadByte();
                    stream.ReadByte();//type
                    stream.ReadUInt();//timestamp
                    var amfReader = new AMF0Reader(stream);
                    var str = amfReader.ReadShortString(true);
                    var num = amfReader.ReadAMFDouble(true);
                    var pos = stream.Position;
                    var connectionInfo = amfReader.ReadVariant();
                    connectionInfo["tcUrl"] = MiddleSession.QueryUrl;
                    connectionInfo["app"] = MiddleSession.QueryUrl.Split('/').Last();
                    stream.Position = pos;
                    var amfWriter = new AMF0Writer(stream);
                    amfWriter.WriteObject(connectionInfo, true);
                    length = (int) stream.Position;
                    len = (ushort) (stream.Position - sizePos-2);
                    stream.Position = sizePos;
                    stream.Write(len);
                }
            }
            stream.Position = 6;
            base.SendStream(stream,length);
        }
    }
    public class Middle:Session
    {
        public string QueryUrl;
        private readonly OutboundRTMFPProtocol _outboundRtmfpProtocol;
        private readonly List<MemoryStream> _buffer = new List<MemoryStream>(); 
        public Middle(Peer peer, byte[] decryptKey, byte[] encryptKey, Target target)
            : base(peer, decryptKey, encryptKey)
        {
            QueryUrl = "rtmfp://202.109.143.196:555/live2";
            _outboundRtmfpProtocol = new OutboundRTMFPProtocol();
            _outboundRtmfpProtocol.OnConnect += () =>
            {
                foreach (var memoryStream in _buffer)
                {
                   
                    _outboundRtmfpProtocol.Session.SendStream(memoryStream,(int) memoryStream.Length);
                }
            };
            _outboundRtmfpProtocol.Session = new MiddleHandshake(_outboundRtmfpProtocol) { MiddleSession = this };
            _outboundRtmfpProtocol.Session.Connect(QueryUrl);
        }

        public override BaseClientApplication Application
        {
            set { _outboundRtmfpProtocol.Application = base.Application = value; }
        }

        public H2NBinaryWriter Handshaker
        {
            get
            {
                Writer.Clear(12);
                return Writer;
            }
        }
        public override void PacketHandler(N2HBinaryReader reader)
        {
            reader.BaseStream.Position = 0;
            var ms = new MemoryStream();
            reader.BaseStream.CopyPartTo(ms, (int)reader.BaseStream.GetAvaliableByteCounts());
            ms.Position = 6;
            if (_outboundRtmfpProtocol.Session.Checked)
            {
                _outboundRtmfpProtocol.Session.SendStream(ms, (int)ms.Length);
            }
            else
            {
                _buffer.Add(ms);
            }
        }
    }
}
