using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class HandShake:Session
    {
        private readonly Dictionary<string,Cookie> _cookies = new Dictionary<string, Cookie>();
        private readonly byte[] _certificat;
        public static readonly byte[] CertificatInit = { 0x02, 0x15, 0x02, 0x02, 0x15, 0x05, 0x02, 0x15, 0x0E };
        public HandShake(BaseRtmfpProtocol handler)
            : base(new Peer(handler),Defines.RTMFP_SYMETRIC_KEY, Defines.RTMFP_SYMETRIC_KEY)
        {
            Handler = handler;
            Checked = true;
            _certificat = Utils.GenerateRandomBytes(77);
            _certificat[0] = 0x01;
            _certificat[1] = 0x0A;
            _certificat[2] = 0x41;
            _certificat[3] = 0x0E;
            Buffer.BlockCopy(CertificatInit, 0, _certificat, 68, 9);
        }
        public void CommitCookie(byte[] value)
        {
            var s = value.BytesToString();
            if (_cookies.ContainsKey(s))
            {
                EraseHelloAttempt(_cookies[s].Tag);
                _cookies.Remove(s);
            }
        }
        public override void PacketHandler(N2HBinaryReader reader)
        {
            var marker = reader.ReadByte();
            if (marker != 0x0b)
            {
                Logger.FATAL("Marker hand shake wrong:should be 0b and not {0}", marker);
                return;
            }
            var time = reader.ReadUInt16();
            var id = reader.ReadByte();
            var length = reader.ReadUInt16();
            reader.Shrink(length);
            var pos = Writer.BaseStream.Position;
            Writer.BaseStream.Position += 3;
            var idResponse = PerformHandshake(id, reader, pos);
            
            if (idResponse > 0)
            {
                Writer.BaseStream.Position = pos;
                Writer.Write(idResponse);
                Writer.Write((short)(Writer.BaseStream.GetAvaliableByteCounts() - 2));
                Flush(0x0b);
            }
            FarId = 0;
        }
        private byte PerformHandshake(byte id, N2HBinaryReader reader, long oldPos)
        {
            //Logger.Debug("PerformHandshake{0}", id);
            switch (id)
            {
                case 0x30:
                    reader.ReadByte();
                    var epdLen = reader.ReadByte() - 1;
                    var type = reader.ReadByte();
                    var epd = reader.ReadBytes(epdLen);
                    var tag = reader.ReadBytes(16);
                    Writer.Write((byte)tag.Length);
                    Writer.Write(tag);
                    if (type == 0x0F)
                        return Handler.PerformHandshake(tag, Writer,Peer.Address, epd);
                    if (type == 0x0a)
                    {

                        var tagstr = tag.BytesToString();
                        var attempt = GetHelloAttempt<HelloAttempt>(tagstr);

                        ushort port;
                        string host;
                        RtmfpUtils.UnpackUrl(epd.BytesToString(), out host, out port, out Peer.Path,out Peer.Properties);

                        var addresses = new List<string>();
                        Peer.OnHandshake(attempt.Count + 1, addresses);
                        if (addresses.Count > 0)
                        {
                            for (var i = 0; i < addresses.Count; i++)
                            {
                                if (addresses[i] == "again")
                                {
                                   addresses[i] = host + ":" + port;
                                    Writer.WriteAddress(new IPEndPoint(IPAddress.Parse(host),port),i==0 );
                                }
                            }
                      
                            return 0x71;
                        }
                        CreateCookie(Writer, attempt, tag, epd.BytesToString());
                        Writer.Write(_certificat);
                        return 0x70;
                    }
                    else
                    {
                        Logger.FATAL("Unkown handshake first way with '{0}' type", type);
                    }
                    return 0;
                case 0x38:
                    FarId = reader.ReadUInt32();
                   
                    if (reader.Read7BitLongValue() != CookieComputing.COOKIE_SIZE)
                    {
                        return 0;
                    }
                    var cookieKey = reader.ReadBytes(CookieComputing.COOKIE_SIZE).BytesToString();
                    reader.BaseStream.Position -= CookieComputing.COOKIE_SIZE;
                    if (!_cookies.ContainsKey(cookieKey))
                    {
                        Logger.WARN("Cookie {0} unknown, maybe already connected (udpBuffer congested?)", cookieKey);
                        return 0;
                    }
                    var cookie = _cookies[cookieKey];
                    cookie.PeerAddress = Peer.Address;
                    if (cookie.FarId == 0)
                    {
                        cookie.FarId = FarId;
                        reader.BaseStream.Position += CookieComputing.COOKIE_SIZE;
                        var size = reader.Read7BitLongValue();
                        var buffer = reader.ReadBytes((int) size);
                        uint tempSize = 0;
                        cookie.PeerId = Target.Sha256.ComputeHash(buffer, 0, (int)size);
                        //Native.EVP_Digest(buffer, (uint)size, cookie.PeerId, ref tempSize, Native.EVP_sha256(), IntPtr.Zero);
                        reader.BaseStream.Position -= (long) size;
                        var initiatorKeySize = (int) (reader.Read7BitValue() - 2);
                        reader.BaseStream.Position += 2;
                        cookie.CookieComputing.InitiatorKey = new byte[initiatorKeySize];
                        reader.BaseStream.Read(cookie.CookieComputing.InitiatorKey, 0, initiatorKeySize);
                        //cookie.CookieComputing.InitiatorKey = reader.ReadBytes((int) initiatorKeySize);
                        cookie.CookieComputing.InitiatorNonce = reader.ReadBytes((int) reader.Read7BitValue());
                        Writer.BaseStream.Position = oldPos;
                        tempSize = reader.ReadByte();//0x58
                        if(tempSize!=0x58)Logger.WARN("not 0x58!!");
                        cookie.ComputeKeys();
                    }
                    else if(cookie.Id>0)
                    {
                        cookie.Read(Writer.BaseStream);
                        return 0x78;
                    }
                    return 0;
                default:
                    Logger.FATAL("Unkown handshake packet id {0}", id);
                    return 0;
            }
        }

        public void CreateCookie(H2NBinaryWriter writer, HelloAttempt attempt, byte[] tag, string queryUrl)
        {
            var cookie = attempt.Cookie;
            if (cookie == null)
            {
                cookie = new Cookie(this,tag, queryUrl);
                _cookies[cookie.Value.BytesToString()] = cookie;
                attempt.Cookie = cookie;
            }
            writer.Write(CookieComputing.COOKIE_SIZE);
            writer.Write(cookie.Value);
        }

        public Session CreateSession(byte[] cookieValue)
        {
            var temp = cookieValue.BytesToString();
            if (!_cookies.ContainsKey(temp))
            {
                Logger.WARN("Creating session for an unknown cookie '{0}' (CPU congestion?)",temp);
                return null;
            }
            var cookie = _cookies[temp];
            Peer.Id = (byte[]) cookie.PeerId.Clone();
            RtmfpUtils.UnpackUrl(cookie.QueryUrl,out Peer.Path,out Peer.Properties);
            FarId = cookie.FarId;
            Peer.Address = cookie.PeerAddress;
            var session = Handler.CreateSession(Peer, cookie);
            cookie.Id = session.Id;
            cookie.Write();
            if(cookie.Target != null)
            {
                //cookie.Target.InitiatorNonce = cookie.CookieComputing.InitiatorNonce.Clone() as byte[];
                cookie.Target.SharedSecret = cookie.CookieComputing.SharedSecret;
            }
            Writer.Write((byte)0x78);
            Writer.Write(cookie.Length);
            cookie.Read(Writer.BaseStream);
            Flush(0x0b);
            FarId = 0;
            return session;
        }

        public override void Manage()
        {
            foreach (var cookie in _cookies.ToArray().Where(cookie => cookie.Value!=null && cookie.Value.Obsolete()))
            {
                EraseHelloAttempt(cookie.Value.Tag);
                _cookies.Remove(cookie.Key);
            }
        }
    }
}
