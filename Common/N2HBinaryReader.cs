using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static System.Net.IPAddress;

namespace CSharpRTMP.Common
{
    public class N2HBinaryReader:BinaryReader
    {
        public N2HBinaryReader(Stream input) : base(input)
        {
        }

        public N2HBinaryReader(Stream input, Encoding encoding) : base(input, encoding)
        {
        }

        public override short ReadInt16() => NetworkToHostOrder(base.ReadInt16());

        public override int ReadInt32() => NetworkToHostOrder(base.ReadInt32());

        public override long ReadInt64() => NetworkToHostOrder(base.ReadInt64());

        public override ushort ReadUInt16() => (ushort)NetworkToHostOrder(base.ReadInt16());

        public uint _ReadUInt32() => base.ReadUInt32();

        public override uint ReadUInt32() => (uint)NetworkToHostOrder(base.ReadInt32());

        public int Read24() => (ReadByte() << 16) | (ReadByte() << 8) | ReadByte();

        public uint ReadU24() => ((uint)ReadByte() << 16) | ((uint)ReadByte() << 8) | ReadByte();

        public uint ReadSU32() => (uint)(Read24()|(ReadByte()<<24));

        internal int _ReadInt32() => base.ReadInt32();

        public override ulong ReadUInt64() => (ulong)NetworkToHostOrder(base.ReadInt64());

        public byte this[int index]
        {
            get
            {
                if (BaseStream is MemoryStream)
                    return (BaseStream as MemoryStream).GetBuffer()[index + BaseStream.Position];
                var oldPosition = BaseStream.Position;
                BaseStream.Position += index;
                var result = ReadByte();
                BaseStream.Position = oldPosition;
                return result;
            }
        }

        public uint Read7BitValue()
        {
            byte n = 0;
            byte b = ReadByte();
            uint result = 0;
            while ((b & 0x80) != 0 && n < 3)
            {
                result <<= 7;
                result |= (uint)(b & 0x7F);
                b = ReadByte();
                ++n;
            }
            result <<= ((n < 3) ? 7 : 8); // Use all 8 bits from the 4th byte
            result |= b;
            return result;
        }
        public ulong Read7BitLongValue()
        {
            byte n = 0;
            byte b = ReadByte();
            ulong result = 0;
            while ((b & 0x80) != 0 && n < 8)
            {
                result <<= 7;
                result |= (ulong)(b & 0x7F);
                b = ReadByte();
                ++n;
            }
            result <<= ((n < 8) ? 7 : 8); // Use all 8 bits from the 4th byte
            result |= b;
            return result;
        }

        public string ReadString8()
        {
            var len = ReadByte();
            var buffer = ReadBytes(len);
            return Encoding.ASCII.GetString(buffer);
        }

        public void Shrink(uint rest)
        {
            if (rest > BaseStream.GetAvaliableByteCounts())
            {
                Logger.WARN("rest {0} more upper than available {1} bytes", rest, BaseStream.GetAvaliableByteCounts());
                rest = (uint)BaseStream.GetAvaliableByteCounts();
            }
            BaseStream.SetLength(BaseStream.Position + rest);
        }
    }
}
