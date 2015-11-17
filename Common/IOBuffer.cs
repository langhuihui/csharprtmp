using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Media;
using System.Net.Sockets;
using System.Text;

namespace CSharpRTMP.Common
{
    public struct BufferWithOffset
    {
        public byte[] Buffer;
        public int Offset;

        public int Length => _length - Offset;
        private readonly int _length;
        public BufferWithOffset(byte[] pBuffer, int offset = 0 ,int length = -1)
        {
            Buffer = pBuffer;
            Offset = offset;
            _length = length==-1?pBuffer.Length:length;
        }
        
        public static implicit operator byte[](BufferWithOffset v)
        {
            var result = new byte[v.Length];
            System.Buffer.BlockCopy(v.Buffer, v.Offset, result, 0, v.Length);
            return result;
        }
        public static implicit operator BufferWithOffset(byte[] v)
        {
            return new BufferWithOffset(v);
        }
       
        public BufferWithOffset(Stream s,bool forceRead = false , int length = -1)
        {
            if (s is MemoryStream && !forceRead)
            {
                Buffer = (s as MemoryStream).GetBuffer();
                Offset = (int) (s as MemoryStream).Position;
                //if (s is OutputStream) Offset += (int)(s as OutputStream).Consumed;
                _length = (length==-1?(int)s.GetAvaliableByteCounts():length) + Offset;
            }
            else
            {
                Buffer = new byte[length == -1?s.Length - s.Position:length];
                Offset = 0;
                s.Read(Buffer, 0, Buffer.Length);
                _length = Buffer.Length;
            }
        }
        public byte this[int index]
        {
            get { return Buffer[index+Offset]; }
            set { Buffer[index + Offset] = value; }
        }

        public override string ToString() => new string(Buffer.Skip(Offset).Take(Length).Select(x=>(char)x).ToArray());
    }

    public static class StreamUtil
    {
        
        public static long GetAvaliableByteCounts(this Stream s)
        {
            var stream = s as InputStream;
            if (stream != null)
            {
                return stream.AvaliableByteCounts;
            }
            return s.Length - s.Position;
        }

        public static void Recycle(this Stream s)
        {
            if (s.Position == s.Length) s.SetLength(0);
        }

        public static void IgnoreAll(this Stream s) => s.SetLength(0);

        public static string Dump(this MemoryStream s)
        {
            return string.Join(" ",s.GetBuffer().Take((s is InputStream?(int) (s as InputStream).Published:(int) s.Length)).Select(x => x.ToString("x2")));
        }
        public static void CopyPartTo(this Stream source, Stream target, int length)
        {
            byte[] buffer;
            int offset = 0;
            if (source is MemoryStream)
            {
                buffer = (source as MemoryStream).GetBuffer();
                offset = (int)source.Position;
                source.Position += length;
            }
            else
            {
                buffer = new byte[length];
                source.Read(buffer, 0, length);
            }
            //if (target is InputStream)
            //{
            //    var inputTarget = (InputStream) target;
            //    var pos = inputTarget.Position;
            //    inputTarget.Position = inputTarget.Published;
            //    target.Write(buffer, offset, length);
            //    inputTarget.Published += (uint)length;
            //    inputTarget.Position = pos;
            //}
            //else
                target.Write(buffer, offset, length);
        }

        public static void CopyDataTo(this Stream source, Stream target,int length = 0)
        {
            var sourcePos = source.Position;
            CopyPartTo(source, target, length == 0?(int) source.GetAvaliableByteCounts():length);
            source.Position = sourcePos;
        }
       
    }

    public class  InputStream: MemoryStream
    {
        public uint Published;
        public readonly N2HBinaryReader Reader;

        public InputStream()
        {
            Reader = new N2HBinaryReader(this);
        }

        public uint AvaliableByteCounts => (uint)(Published - Position);

        public void Recycle(bool force = false)
        {
            if (force ||(Position == Published))
            {
                SetLength(0);
                Published = 0;
            }
        }

        public override void SetLength(long value)
        {
            base.SetLength(value);
            if (Published > Length)
            {
                Published = (uint) Length;
            }
        }

        public void Ignore(uint size)
        {
            Position += size;
            Recycle();
        }
    }
}
