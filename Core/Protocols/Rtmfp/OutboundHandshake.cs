using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class OutboundHandshake:Session
    {
        public static readonly byte[] CertificatInit = { 0x02, 0x1D, 0x02, 0x41, 0x0E, 0x03, 0x1A, 0x02, 0x0A, 0x02, 0x1E, 0x02};
        private byte[] _certificat;
        private DHWrapper _dh;
        private readonly System.Timers.Timer _handshakeTimeoutTimer = new System.Timers.Timer(1000);
        private int _step;
        private Action _handshake;
        public OutboundHandshake(BaseRtmfpProtocol handler)
            : base(new Peer(handler),Defines.RTMFP_SYMETRIC_KEY, Defines.RTMFP_SYMETRIC_KEY)
        {
            Handler = handler;
            _certificat = Utils.GenerateRandomBytes(76);
            Buffer.BlockCopy(CertificatInit, 0, _certificat, 0, 5);
            Buffer.BlockCopy(CertificatInit, 5, _certificat, 69, 7);
            _handshakeTimeoutTimer.Elapsed += (o, args) =>
            {
                if (_handshakeTimeoutTimer.Interval >= 3000)
                {
                    _handshakeTimeoutTimer.Interval = 1000;
                    _certificat = Utils.GenerateRandomBytes(76);
                    Buffer.BlockCopy(CertificatInit, 0, _certificat, 0, 5);
                    Buffer.BlockCopy(CertificatInit, 5, _certificat, 69, 7);
                    var rand = Utils.GenerateRandomBytes(16);
                    _handshake = () => HandShake30(rand);
                    HandShake30(rand);
                }
                else
                {
                    _handshake();
                    _handshakeTimeoutTimer.Interval += 1000;
                }
            };
        }

        public string URL;
        public void Connect(string url)
        {
            URL = url;
            var uri = new Uri(url);
            Target = new Target(new IPEndPoint(Dns.GetHostAddresses(uri.Host).First(x=>x.AddressFamily == AddressFamily.InterNetwork), uri.Port < 0 ? 1935 : uri.Port));
            Peer.Address = Target.Address;
            Handler.FarProtocol.IOHandler.Socket.Connect(Target.Address);
            Handler.FarProtocol.IOHandler.ReadEnabled = true;
            var rand = Utils.GenerateRandomBytes(16);
            HandShake30(rand);
            _handshakeTimeoutTimer.Start();
            _handshake = () => HandShake30(rand);
        }

        public void HandShake30(byte[] randBytes)
        {
            Writer.Clear(12);
            Writer.Write((byte)(URL.Length + 2));
            Writer.Write((byte)(URL.Length + 1));
            Writer.Write((byte)0x0A);//连接服务器模式
            Writer.Write(URL.ToBytes());
            Writer.Write(randBytes);
            Flush(0x30);
        }

        public void HandShake38(byte[] cookieBytes,byte[] nonce)
        {
            Writer.Clear(12);
            Writer.Write(Id);
            Writer.Write((byte)cookieBytes.Length);
            Writer.Write(cookieBytes);
            Writer.Write7BitValue((uint)nonce.Length);
            Writer.Write(nonce);
            Writer.Write7BitValue((uint)_certificat.Length);
            Writer.Write(_certificat);
            Writer.Write((byte)0x58);
            Flush(0x38);
        }
        public override void Flush(byte type)
        {
            Writer.BaseStream.Position = 6;
            Writer.Write((byte)0x0b);
            Writer.Write(RtmfpUtils.TimeNow());
            Writer.Write(type);
            Writer.Write((short)(Writer.BaseStream.GetAvaliableByteCounts() - 2));
            var encoder = AesEncrypt.Next(AESEngine.AESType.SYMMETRIC);
            RtmfpUtils.EncodeAndPack(encoder, Writer, 0);
            EnqueueForOutbound(OutputBuffer);
            Writer.Clear(11);
        }

        public override void PacketHandler(N2HBinaryReader reader)
        {
            if(Checked){
                lock (Writer)
                {
                    base.PacketHandler(reader);
                }
                return;
            }
            var marker = reader.ReadByte();
            if (marker != 0x0b)
            {
                Logger.FATAL("Marker hand shake wrong:should be 0b and not {0:X}", marker);
                return;
            }
            var time = reader.ReadUInt16();
            var type = reader.ReadByte();
            var length = reader.ReadUInt16();
            byte[] tag;
            Logger.Debug("handshake {0:X} len:{1}",type,length);
            switch (type)
            {
                case 0x70:
              
                    tag = reader.ReadBytes(reader.ReadByte());
                    var cookieBytes = reader.ReadBytes(reader.ReadByte());
                    var targetCertificat = reader.ReadBytes((int)reader.BaseStream.GetAvaliableByteCounts());
                    var nonce = new byte[0];
                    _dh = RtmfpUtils.BeginDiffieHellman(ref nonce, true);
                    Peer.Id = Target.Sha256.ComputeHash(nonce, 0, nonce.Length);
                    HandShake38(cookieBytes, nonce);
                    _handshake = () => HandShake38(cookieBytes, nonce);
                    break;
                case 0x71:
                    tag = reader.ReadBytes(reader.ReadByte());
                    var flag = reader.ReadByte();
                    var address = new IPEndPoint(new IPAddress(reader.ReadBytes(4)), reader.ReadInt16());
                    Target.Address.Port = address.Port;
                    Logger.Debug("redirect to {0}",address.ToString());
                    Handler.FarProtocol.IOHandler.Socket.Connect(Target.Address);
                    _handshake();
                    break;
                case 0x78:
                  
                    FarId = reader.ReadUInt32();
                    var targetNonce = reader.ReadBytes((int)reader.Read7BitLongValue());
                    var must58 = reader.ReadByte();
                    Debug.WriteLineIf(must58!=0x58,$"must58!{must58}");
                    var key = new byte[RtmfpUtils.KEY_SIZE];
                    Buffer.BlockCopy(targetNonce, targetNonce.Length - RtmfpUtils.KEY_SIZE, key, 0, RtmfpUtils.KEY_SIZE);
                    var  sharedSecret = _dh.CreateSharedKey(key);
                    byte[] decryptKey;
                    byte[] encryptKey;
                    RtmfpUtils.ComputeAsymetricKeys(sharedSecret, _certificat, targetNonce, out encryptKey, out decryptKey);
                    Checked = true;
                    _handshakeTimeoutTimer.Stop();
                    AesEncrypt = new AESEngine(encryptKey, AESEngine.Direction.ENCRYPT);
                    AesDecrypt = new AESEngine(decryptKey);
                    PrevAesType = AESEngine.AESType.DEFAULT;
                    Application = Handler.Application;
                    Handler.CreateSession(Peer, null);

                    break;
                default:

                    break;
            }

        }
        public override void Manage()
        {
            if (IsEnqueueForDelete) return;
            lock (Writer)
            {
            if (Checked && RecTimestamp.ElapsedMilliseconds > KeepAliveServer)
            {
                WriteMessage(0x01, 0);
                Flush(0x09, true, PrevAesType);
                Logger.Debug("keepAlive?");
                RecTimestamp.Restart();
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
            try
            {
                SFlush(true);
            }
            catch (Exception)
            {
                Writer.Clear(11);
            }
            }
        }
    }
}
