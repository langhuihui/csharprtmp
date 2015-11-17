using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using OpenSSL.Core;

namespace OpenSSL.Crypto
{
    public class SHA256 : IDisposable
    {
        private IntPtr _ctx;
        private static int contextsize;

        static SHA256()
        {
            contextsize = Marshal.SizeOf(typeof (SHA256_CTX));
        }

        public SHA256()
        {
            _ctx = Marshal.AllocHGlobal(contextsize);
        }

        ~SHA256()
        {
            Dispose(false);
        }

        public void Init()
        {
            Native.ExpectSuccess(Native.SHA256_Init(_ctx));
        }

        public void Update(byte[] data)
        {
            Update(data, data.Length);
        }

        public void Update(byte[] data, int length)
        {
            Native.ExpectSuccess(Native.SHA256_Update(_ctx, data, length));
        }

        public unsafe void Update(byte[] data, int offset, int length)
        {
            if (offset == 0)
                Update(data, length);
            else
            {
                fixed (byte* p = &data[offset])
                {
                    Native.ExpectSuccess(Native.SHA256_Update(_ctx, (IntPtr)p, length));
                }
            }
        }

        public byte[] GetContext()
        {
            byte[] result = new byte[contextsize];
            Marshal.Copy(_ctx, result, 0, contextsize);

            return result;
        }

        public void SetContext(byte[] context)
        {
            Marshal.Copy(context, 0, _ctx, contextsize);
        }

        public byte[] Final()
        {
            byte[] hash = new byte[32];
            Native.ExpectSuccess(Native.SHA256_Final(hash, _ctx));

            return hash;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_ctx != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_ctx);
                _ctx = IntPtr.Zero;
            }
        }

        // This is just here for reference
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHA256_CTX
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public uint[] h;

            public uint Nl;
            public uint Nh;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public uint[] data;

            public uint num;
            public uint md_len;
        }
    }
}
