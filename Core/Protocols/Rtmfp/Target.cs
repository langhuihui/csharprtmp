using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using CSharpRTMP.Common;
using System.Security.Cryptography;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class Target : Entity,IDisposable
    {
        public byte[] PublicKey;
        public DHWrapper DH;
        public byte[] InitiatorNonce;
        public byte[] SharedSecret;
        public byte[] PeerId;
        public IPEndPoint Address;
        public bool IsPeer;
        public static readonly SHA256 Sha256 = SHA256.Create();
        public Target(IPEndPoint address, Cookie cookie = null)
        {
            Address = address;
            IsPeer = cookie != null;
           
            if (address.Port == 0)
            {
                Address = new IPEndPoint(address.Address, RtmfpUtils.RTMFP_DEFAULT_PORT);
            }
            if (IsPeer)
            {
                DH = cookie.CookieComputing.DH;
                PublicKey = new byte[cookie.CookieComputing.Nonce.Length-7];
                Buffer.BlockCopy(cookie.CookieComputing.Nonce, 7, PublicKey,0,PublicKey.Length);
                PublicKey[3] = 0x1D;
               // uint s = 0;
                Id = Sha256.ComputeHash(PublicKey, 0, PublicKey.Length);
                //Native.EVP_Digest(PublicKey, (uint) PublicKey.Length, Id, ref s, Native.EVP_sha256(), IntPtr.Zero);
                cookie.CookieComputing.DH = null;
            }
        }

        public void Dispose()
        {
            //if (DH != null)
            //{
            //    RtmfpUtils.EndDiffieHellman(DH);
            //}
        }
    }
}
