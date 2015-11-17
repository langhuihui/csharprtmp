using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using static System.Net.IPAddress;
using AddressFamily = System.Net.Sockets.AddressFamily;

namespace CSharpRTMP.Common
{
    public class H2NBinaryWriter:BinaryWriter
    {
        public H2NBinaryWriter(Stream source):base(source)
        {
            
        }
        public override void Write(short value) => base.Write(HostToNetworkOrder(value));

        public override void Write(ushort value) => base.Write(HostToNetworkOrder((short)value));

        public override void Write(int value) => base.Write(HostToNetworkOrder(value));

        public override void Write(uint value) => base.Write(HostToNetworkOrder((int)value));

        public override void Write(long value) => base.Write(HostToNetworkOrder(value));

        public override void Write(ulong value) => base.Write(HostToNetworkOrder((long)value));

        public void WriteBase(uint value) => base.Write(value);

        public void WriteBase(int value) => base.Write(value);

        public void Write24(int value)
        {
            //value = IPAddress.HostToNetworkOrder(value << 8);
            //var bytes = BitConverter.GetBytes(value);
            //if (BitConverter.IsLittleEndian)
            //{
            //    Array.Reverse(bytes);
            //}
            //Write(bytes, 1, 3);
            Write((byte)(value >> 16));
            Write((byte)(value >> 8));
            Write((byte)value);
        }

        public void Write24(uint value)
        {
            Write((byte)(value >> 16));
            Write((byte)(value >> 8));
            Write((byte)value);
        }

        public void WriteS32(uint value) => WriteS32((int) value);

        public void WriteS32(int value)
        {
            Write24(value);
            Write((sbyte)(value>>24));
        }
        
        public void WriteAddress(IPEndPoint ipEndPoint, bool publicFlag)
        {
            var flag = (byte)(publicFlag ? 0x02 : 0x01);
            if (ipEndPoint.AddressFamily == AddressFamily.InterNetworkV6)
            {
                flag |= 0x80;
            }
            Write(flag);
            Write(ipEndPoint.Address.GetAddressBytes());
            Write((ushort)ipEndPoint.Port);
        }

        public void Write7BitLongValue(ulong value)
        {
            var shift = (byte)((Get7BitValueSize(value) - 1) * 7);
            var max = shift >= 63;
            if (max) ++shift;
            while (shift >= 7)
            {
                Write((byte)(0x80 | ((value >> shift) & 0x7F)));
                shift -= 7;
            }
            Write((byte)(max ? value & 0xFF : value & 0x7F));
        }

        public void WriteString8(string value)
        {
            Write((byte)value.Length);
            Write(Encoding.ASCII.GetBytes(value));
        }

        public void WriteString16(string value)
        {
            Write((ushort)value.Length);
            Write(Encoding.ASCII.GetBytes(value));
        }
        public void Write7BitValue(uint value)
        {
            var shift = (byte)((Get7BitValueSize(value) - 1) * 7);
            var max = false;
            if (shift >= 21)
            {
                shift = 22;
                max = true;
            }
            while (shift >= 7)
            {
                Write((byte)(0x80 | ((value >> shift) & 0x7F)));
                shift -= 7;
            }
            Write((byte)(max ? value & 0xFF : value & 0x7F));
        }
        public static byte Get7BitValueSize(ulong value)
        {
            ulong limit = 0x80;
            byte result = 1;
            while (value >= limit)
            {
                limit <<= 7;
                ++result;
            }
            return result;
        }
    }
}
