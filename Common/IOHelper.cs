using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRTMP.Common
{
    public static class  IOHelper
    {
        public static uint ReadUInt(this Stream s) => (uint) (s.ReadByte()<<24 | s.ReadByte()<<16 | s.ReadByte()<<8 | s.ReadByte());
        public static uint ReadUInt(this byte[] buffer,int offset)=>(uint)(buffer[offset]<<24| buffer[offset+1] << 16 | buffer[offset + 2] << 8 | buffer[offset + 3]);
        public static ushort ReadUShort(this Stream s) => (ushort) (s.ReadByte() << 8 | s.ReadByte());
        public static ushort ReadUShort(this byte[] buffer,int offset) => (ushort) (buffer[offset] << 8 | buffer[offset+1]);
        public static uint Read7BitValue(this byte[] buffer,int offset)
        {
            byte n = 0;
            byte b = buffer[offset++];
            uint result = 0;
            while ((b & 0x80) != 0 && n < 3)
            {
                result <<= 7;
                result |= (uint)(b & 0x7F);
                b = buffer[offset++];
                ++n;
            }
            result <<= ((n < 3) ? 7 : 8); // Use all 8 bits from the 4th byte
            result |= b;
            return result;
        }
        public static uint Read7BitValue(this Stream s)
        {
            byte n = 0;
            byte b = (byte) s.ReadByte();
            uint result = 0;
            while ((b & 0x80) != 0 && n < 3)
            {
                result <<= 7;
                result |= (uint)(b & 0x7F);
                b = (byte)s.ReadByte();
                ++n;
            }
            result <<= ((n < 3) ? 7 : 8); // Use all 8 bits from the 4th byte
            result |= b;
            return result;
        }
        public static ulong Read7BitLongValue(this Stream s)
        {
            byte n = 0;
            byte b = (byte) s.ReadByte();
            ulong result = 0;
            while ((b & 0x80) != 0 && n < 8)
            {
                result <<= 7;
                result |= (ulong)(b & 0x7F);
                b = (byte)s.ReadByte();
                ++n;
            }
            result <<= ((n < 8) ? 7 : 8); // Use all 8 bits from the 4th byte
            result |= b;
            return result;
        }
        public static void Write(this byte[] buffer,int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[1 + offset] = (byte)(value >> 16);
            buffer[2 + offset] = (byte)(value >> 8);
            buffer[3 + offset] = (byte)(value);
        }

        public static void Write(this byte[] buffer, int offset, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                buffer[offset + i] = (byte) (value >> (56-i*8));
            }
        }
        public static void Write(this byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value >> 8);
            buffer[1 + offset] = (byte)(value);
        }
        public static void Write(this byte[] buffer, int offset, string value)
        {
            var charArray = value.ToCharArray();
           Buffer.BlockCopy(charArray, 0,buffer,offset, charArray.Length);
        }
        public static void Write(this Stream s, uint value)
        {
            s.WriteByte((byte)(value >> 24));
            s.WriteByte((byte)(value >> 16));
            s.WriteByte((byte)(value >> 8));
            s.WriteByte((byte)(value));
        }
        public static void Write(this Stream s, ushort value)
        {
            s.WriteByte((byte)(value >> 8));
            s.WriteByte((byte)(value));
        }
        public static void Write24(this Stream s, uint value)
        {
            s.WriteByte((byte)(value >> 16));
            s.WriteByte((byte)(value >> 8));
            s.WriteByte((byte)value);
        }

        public static void Write(this Stream s, string str)
        {
            Debug.WriteLine(str);
            var bytes = Encoding.UTF8.GetBytes(str);
            s.Write(bytes,0, bytes.Length);
        }
        public static void WriteLittleEndian(this Stream s, uint value)
        {
            s.WriteByte((byte)(value));
            s.WriteByte((byte)(value >> 8));
            s.WriteByte((byte)(value >> 16));
            s.WriteByte((byte)(value >> 24));
        }
    }
}
