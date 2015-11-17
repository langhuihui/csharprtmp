using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CSharpRTMP.Common
{
    public class BitReader:BinaryReader
    {
        private uint _cursor = 8;
        private byte _currentByte;
        public IEnumerator<int> BitsEnumerator; 
        
        public BitReader(Stream input) : base(input)
        {
            BitsEnumerator = Bits().GetEnumerator();
        }

        public int ReadBitsToInt(byte count = 32)
        {
            var result = 0;
            if (AvailableBits < count) throw new EndOfStreamException();
            for (var i = 0; i < count; i++)
            {
                BitsEnumerator.MoveNext();
                result = (result << 1) | BitsEnumerator.Current;
            }
            return result;
        }
        public short ReadBitsToShort(byte count = 16)
        {
            short result = 0;
            if (AvailableBits < count) throw new EndOfStreamException();
            for (var i = 0; i < count; i++)
            {
                BitsEnumerator.MoveNext();
                result = (short)((result << 1) | BitsEnumerator.Current);
            }
            return result;
        }
        public byte ReadBitsToByte(byte count = 8)
        {
            byte result = 0;
            if (AvailableBits<count) throw new EndOfStreamException();
            for (var i = 0; i < count; i++)
            {
                BitsEnumerator.MoveNext();
                result = (byte)((result << 1) | BitsEnumerator.Current);
            }
            return result;
        }

        public bool ReadBool() => BitsEnumerator.MoveNext() && BitsEnumerator.Current == 1;

        public IEnumerable<int> Bits()
        {
            var allbitscount = BaseStream.GetAvaliableByteCounts() * 8;
            for (int i = 0; i < allbitscount; i++)
            {
                if (_cursor == 8)
                {
                    _cursor = 0;
                   _currentByte = ReadByte();
                }
                _cursor++;
                yield return ((_currentByte >>8- (int)_cursor) & 1);
            }
        }
        public uint AvailableBits => (uint) ((BaseStream.GetAvaliableByteCounts()+1)*8 - _cursor);

        public ulong ReadExpGolomb(string name = "") {
		    ulong value = 1;
		    uint zeroBitsCount = 0;
		    while (true) {
			    if (AvailableBits == 0) {
                    throw new IOException("Unable to read " + name);
			    }
		        if (ReadBool())break;
		        zeroBitsCount++;
		    }
		    if (AvailableBits < zeroBitsCount) {
                throw new IOException("Unable to read " + name);
		    }
		    for (var i = 0; i < zeroBitsCount; i++)
		    {
		        BitsEnumerator.MoveNext();
			    value = (value << 1) | (byte) BitsEnumerator.Current;
		    }
		    value = value - 1;
            return value;
	    }
    }
}
