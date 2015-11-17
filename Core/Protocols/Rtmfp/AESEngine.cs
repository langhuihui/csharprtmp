using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using CSharpRTMP.Common;


namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public partial class AESEngine
    {
        public const int AES_KEY_SIZE = 0x20;
        public enum AESType
        {
            DEFAULT = 0,EMPTY,SYMMETRIC
        }
        public enum Direction
        {
            DECRYPT = 0,ENCRYPT
        };

        public AESType Type;
        public uint[] _key;
        private Direction _direction;
        private bool _canRecycle;
        private static readonly AESEngine s_aesDecrypt = new AESEngine(Defines.RTMFP_SYMETRIC_KEY);
        private static readonly AESEngine s_aesEncrypt = new AESEngine(Defines.RTMFP_SYMETRIC_KEY, Direction.ENCRYPT);
        public AESEngine(byte[] key = null,Direction direction = Direction.DECRYPT)
        {
            _direction = direction;
            Type = key == null ? AESType.EMPTY : AESType.DEFAULT;
            _key = _direction == Direction.DECRYPT ? GetDecryptKey(key) : GetEncryptKey(key);
        }

        public AESEngine()
        {
            throw new Exception();
        }
        public AESEngine(AESEngine other, AESType type)
        {
            Type = other.Type == AESType.EMPTY ? AESType.EMPTY : type;
            _key = other._key;
            _direction = other._direction;
        }
        public AESEngine Next(AESType t)
        {
            AESEngine next;
            if (GlobalPool<AESEngine>.GetObject(out next, this, t))
            {
                next.Type = Type == AESType.EMPTY ? AESType.EMPTY : t;
                next._key = _key;
                next._direction = _direction;
            }
            else
            {
                next._canRecycle = true;
            }
            return next;
        }
        public void Process(Stream outStream)
        {
            if (Type == AESType.EMPTY) return;
            if (Type == AESType.SYMMETRIC)
            {
                if (_direction == Direction.DECRYPT)
                {
                    s_aesDecrypt.Process(outStream);
                }
                else
                {
                    s_aesEncrypt.Process(outStream);
                }
                return;
            }
            
            if (_direction == Direction.ENCRYPT)
            {
                Encrypt(new BufferWithOffset(outStream));
            }
            else
            {
                Decrypt(new BufferWithOffset(outStream));
            }
            if (_canRecycle)this.ReturnPool();
        }
    }
}
