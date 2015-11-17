using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using CSharpRTMP.Common;
using Microsoft.IO;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class Cookie
    {
        public CookieComputing CookieComputing;
        public uint Id;
        public uint FarId;
        public readonly string Tag;
        public readonly string QueryUrl;
        public byte[] PeerId = new byte[0x20];
        public IPEndPoint PeerAddress;
        private readonly DateTime _createdTimestamp= DateTime.Now;
        private byte[] _buffer;
    
       
        private readonly H2NBinaryWriter _writer = new H2NBinaryWriter(Utils.Rms.GetStream());
        public Cookie(HandShake handshake, byte[] tag, string queryUrl)
        {
            CookieComputing = new CookieComputing(handshake);
            QueryUrl = queryUrl;
            Tag = tag.BytesToString();
            ComputeKeys();
        }
        public Cookie(OutboundHandshake handshake, byte[] tag, Target target)
        {
            Id = handshake.Id;
            CookieComputing = new CookieComputing(handshake);
            Target = target;
            PeerId = Target.Sha256.ComputeHash(CookieComputing.Nonce, 0, CookieComputing.Nonce.Length);
        }
        public byte[] Value => CookieComputing.Value;

        public ushort Length => (ushort)_writer.BaseStream.Length;

        public Target Target;

        public void Write()
        {
            if (_writer.BaseStream.Length == 0)
            {
                _writer.Write(Id);
                if (Target != null)
                {
                    _writer.Write7BitLongValue((ulong)Value.Length);

                    _writer.Write(Value);

                    _writer.Write7BitValue((uint)CookieComputing.Nonce.Length);

                    _writer.Write(CookieComputing.Nonce);

                    _writer.Write7BitValue((uint)CookieComputing.InitiatorNonce.Length);

                    _writer.Write(CookieComputing.InitiatorNonce);
                }
                else
                {
                    _writer.Write7BitLongValue((ulong)CookieComputing.Nonce.Length);

                    _writer.Write(CookieComputing.Nonce);
                }

                _writer.Write((byte)0x58);
            }
        }

        public ushort Read(Stream stream)
        {
            _writer.BaseStream.Position = 0;
            _writer.BaseStream.CopyTo(stream);
            return Length;
        }

        public void ComputeKeys() => CookieComputing.Run();

        public bool Obsolete() => (DateTime.Now - _createdTimestamp).TotalMinutes > 2;
    }
    public class CookieComputing:IDisposable
    {
        public const byte COOKIE_SIZE = 0x40;
        public byte[] Value;
        public byte[] Nonce;
        public DHWrapper DH;
        public byte[] InitiatorKey;
        public byte[] InitiatorNonce;
        public byte[] DecryptKey;
        public byte[] EncryptKey;
        public byte[] SharedSecret;
        private readonly HandShake _handshake;
        private static readonly byte[] InitBuffer = { 0x03, 0x1A, 0x00, 0x00, 0x02, 0x1E, 0x00 };
        public CookieComputing(HandShake handshake)
        {
            _handshake = handshake;
            Value = Utils.GenerateRandomBytes(COOKIE_SIZE);
            if (handshake == null)
            {
                Nonce = Utils.GenerateRandomBytes(73);
                Buffer.BlockCopy(InitBuffer, 0, Nonce, 0, 7);
                Nonce[7] = 0x41;
                Nonce[8] = 0x0E;
            }
            else
            {
                Nonce = InitBuffer.Clone() as byte[];
            }
        }

        public CookieComputing(OutboundHandshake handshake)
        {
            Nonce = new byte[0];
            DH = RtmfpUtils.BeginDiffieHellman(ref Nonce, true);
            
        }
        public void Run()
        {
            if(DH == null) {
		        DH = RtmfpUtils.BeginDiffieHellman(ref Nonce);
		        return;
	        }
	        // Compute Diffie-Hellman secret
            SharedSecret = DH.CreateSharedKey(InitiatorKey);
             // Compute Keys
            RtmfpUtils.ComputeAsymetricKeys(SharedSecret, InitiatorNonce, Nonce, out DecryptKey,out EncryptKey);
            var pSession = _handshake?.CreateSession(Value);
            if (pSession != null)
            {
                pSession.Peer.UserState = this;
            }
        }
        public void Dispose()
        {
        }
    }
    
}
