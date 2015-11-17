using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Rtmp;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public static class RtmfpUtils
    {
        public const uint PACKETSEND_SIZE = 1300;
        public const ushort RTMFP_DEFAULT_PORT = 1935;
        public const int ID_SIZE = 0x20;
        public const byte KEY_SIZE = 0x80;
        public const int RTMFP_MAX_PACKET_LENGTH = 1192;
        public const int RTMFP_MIN_PACKET_LENGTH = 12;
        public static ushort TimeNow()
        {
            return (ushort) (DateTime.Now.SecondsFrom1970()/Defines.RTMFP_TIMESTAMP_SCALE);
        }

        public static ushort Time(TimeSpan timeSpan)
        {
            return (ushort)(timeSpan.TotalSeconds/ Defines.RTMFP_TIMESTAMP_SCALE);
        }

        public static DHWrapper BeginDiffieHellman(ref byte[] pubKey, bool initiator = false)
        {
            var dh = new DHWrapper();
            var size = dh.Keysize;
            byte[] newPubKey;
            if (initiator)
            {
                newPubKey = new byte[4 + size];
                newPubKey[0] = 0x81;
                newPubKey[1] = 0x02;
                newPubKey[2] = 0x1D;
                newPubKey[3] = 0x02;
                pubKey = newPubKey;
                Buffer.BlockCopy(dh.PublicKey, 0, newPubKey, 4, size);
                return dh;
            }
            var index = pubKey.Length;
            newPubKey = new byte[index + 4 + size];
            Buffer.BlockCopy(pubKey,0,newPubKey,0,index);
            var byte2 = (byte) (KEY_SIZE - size);
            if (byte2 > 2)
            {
                Logger.WARN("Generation DH key with less of 126 bytes!");
                byte2 = 2;
            }
            byte2 = (byte) (2 - byte2);
            newPubKey[index++] = 0x81;
            newPubKey[index++] = byte2;
            newPubKey[index++] = 0x0D;
            newPubKey[index++] = 0x02;
            Buffer.BlockCopy(dh.PublicKey, 0, newPubKey, index, size);
            pubKey = newPubKey;
            return dh;
        }
      

        public static void UnpackUrl(string url,out string path,
            out NameValueCollection properties)
        {
            string host;
            ushort port;
            UnpackUrl(url,out host,out port,out path,out properties);
        }
        public static void UnpackUrl(string url, out string host, out ushort port, out string path,
            out NameValueCollection properties)
        {
            var uri = new Uri(url);
            //uri.normalize
            path = uri.AbsolutePath;
            host = uri.Host;
            port =(ushort) uri.Port;
            properties = HttpUtility.ParseQueryString(uri.Query);
        }

        
        public static bool Decode(AESEngine aesDecrypt,N2HBinaryReader packet)
        {
            //var pos = packet.BaseStream.Position;
            //var buffer = packet.ReadBytes((int) packet.BaseStream.GetAvaliableByteCounts());
	// Decrypt
           // packet.BaseStream.Position = pos;
            aesDecrypt.Process(packet.BaseStream as MemoryStream);
            //packet.BaseStream.Write(buffer,0,buffer.Length);
	        return ReadCRC(packet);
        }
        public static bool ReadCRC(N2HBinaryReader packet)
        {
	        // Check the first 2 CRC bytes 
	        packet.BaseStream.Position = 4;
            UInt16 sum = packet.ReadUInt16();
	        return (sum == CheckSum(packet.BaseStream));
        }
        public static void EncodeAndPack(AESEngine aesEncrypt, H2NBinaryWriter writer, uint farId,int ignore = 0)
        {
            var s = writer.BaseStream;
            if (aesEncrypt.Type != AESEngine.AESType.EMPTY)
            {
                var paddingBytesLength = (0xFFFFFFFF - (int)s.Length+ignore+ 5) & 0x0F;
                s.Position =s.Length;
                for (var i = 0; i < paddingBytesLength; i++)
                {
                    writer.Write((byte)0xFF);
                }
                //writer.Write(Enumerable.Repeat((byte)0xFF, (int) paddingBytesLength).ToArray());
            }
            //writeCRC
            s.Position = 6 + ignore;
            var sum = CheckSum(s);
            s.Position = 4 + ignore;
            writer.Write(sum);
            //writeCRC end
            s.Position = 4 + ignore;

            aesEncrypt.Process(s);
            //pack
            s.Position = 4 + ignore;
            var result = s.ReadUInt() ^ s.ReadUInt() ^ farId;
            s.Position = ignore;
            writer.Write(result);
        }

        public static ushort CheckSum(Stream s)
        {
            int sum = 0;
            var position = (int)s.Position;
            var first = s.ReadByte();
            while (first != -1)
            {
                var second = s.ReadByte();
                if (second != -1)
                {
                    sum += (first << 8) +second;
                    first = s.ReadByte();
                }
                else
                {
                    sum += first;
                    first = -1;
                }
            }
            s.Position = position;
            sum = (sum >> 16) + (sum & 0xFFFF);
            sum += (sum >> 16);
            return (ushort) ~sum;
        }

        public static void ComputeAsymetricKeys(byte[] sharedSecret, byte[] initiatorNonce, byte[] responderNonce,out byte[] requestKey,out byte[] responseKey)
        {
            var hmac = new HMACSHA256(responderNonce);
            var md1 = hmac.ComputeHash(initiatorNonce, 0, initiatorNonce.Length);
            hmac = new HMACSHA256(initiatorNonce);
            var md2 = hmac.ComputeHash(responderNonce, 0, responderNonce.Length);
             hmac = new HMACSHA256(sharedSecret);
             requestKey = hmac.ComputeHash(md1, 0, md1.Length);
            responseKey = hmac.ComputeHash(md2, 0, md2.Length);
        }
    }
}
