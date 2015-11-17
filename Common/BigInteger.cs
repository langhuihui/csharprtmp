//************************************************************************************
// BigInteger Class Version 1.03
//
// Copyright (c) 2002 Chew Keong TAN
// All rights reserved.
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, provided that the above
// copyright notice(s) and this permission notice appear in all copies of
// the Software and that both the above copyright notice(s) and this
// permission notice appear in supporting documentation.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
// OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT
// OF THIRD PARTY RIGHTS. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR
// HOLDERS INCLUDED IN THIS NOTICE BE LIABLE FOR ANY CLAIM, OR ANY SPECIAL
// INDIRECT OR CONSEQUENTIAL DAMAGES, OR ANY DAMAGES WHATSOEVER RESULTING
// FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT,
// NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION
// WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
//
//
// Disclaimer
// ----------
// Although reasonable care has been taken to ensure the correctness of this
// implementation, this code should never be used in any application without
// proper verification and testing.  I disclaim all liability and responsibility
// to any person or entity with respect to any loss or damage caused, or alleged
// to be caused, directly or indirectly, by the use of this BigInteger class.
//
// Comments, bugs and suggestions to
// (http://www.codeproject.com/csharp/biginteger.asp)
//
//
// Overloaded Operators +, -, *, /, %, >>, <<, ==, !=, >, <, >=, <=, &, |, ^, ++, --, ~
//
// Features
// --------
// 1) Arithmetic operations involving large signed integers (2's complement).
// 2) Primality test using Fermat little theorm, Rabin Miller's method,
//    Solovay Strassen's method and Lucas strong pseudoprime.
// 3) Modulo exponential with Barrett's reduction.
// 4) Inverse modulo.
// 5) Pseudo prime generation.
// 6) Co-prime generation.
//
//
// Known Problem
// -------------
// This pseudoprime passes my implementation of
// primality test but failed in JDK's isProbablePrime test.
//
//       byte[] pseudoPrime1 = { (byte)0x00,
//             (byte)0x85, (byte)0x84, (byte)0x64, (byte)0xFD, (byte)0x70, (byte)0x6A,
//             (byte)0x9F, (byte)0xF0, (byte)0x94, (byte)0x0C, (byte)0x3E, (byte)0x2C,
//             (byte)0x74, (byte)0x34, (byte)0x05, (byte)0xC9, (byte)0x55, (byte)0xB3,
//             (byte)0x85, (byte)0x32, (byte)0x98, (byte)0x71, (byte)0xF9, (byte)0x41,
//             (byte)0x21, (byte)0x5F, (byte)0x02, (byte)0x9E, (byte)0xEA, (byte)0x56,
//             (byte)0x8D, (byte)0x8C, (byte)0x44, (byte)0xCC, (byte)0xEE, (byte)0xEE,
//             (byte)0x3D, (byte)0x2C, (byte)0x9D, (byte)0x2C, (byte)0x12, (byte)0x41,
//             (byte)0x1E, (byte)0xF1, (byte)0xC5, (byte)0x32, (byte)0xC3, (byte)0xAA,
//             (byte)0x31, (byte)0x4A, (byte)0x52, (byte)0xD8, (byte)0xE8, (byte)0xAF,
//             (byte)0x42, (byte)0xF4, (byte)0x72, (byte)0xA1, (byte)0x2A, (byte)0x0D,
//             (byte)0x97, (byte)0xB1, (byte)0x31, (byte)0xB3,
//       };
//
//
// Change Log
// ----------
// 1) September 23, 2002 (Version 1.03)
//    - Fixed operator- to give correct data length.
//    - Added Lucas sequence generation.
//    - Added Strong Lucas Primality test.
//    - Added integer square root method.
//    - Added setBit/unsetBit methods.
//    - New isProbablePrime() method which do not require the
//      confident parameter.
//
// 2) August 29, 2002 (Version 1.02)
//    - Fixed bug in the exponentiation of negative numbers.
//    - Faster modular exponentiation using Barrett reduction.
//    - Added getBytes() method.
//    - Fixed bug in ToHexString method.
//    - Added overloading of ^ operator.
//    - Faster computation of Jacobi symbol.
//
// 3) August 19, 2002 (Version 1.01)
//    - Big integer is stored and manipulated as unsigned integers (4 bytes) instead of
//      individual bytes this gives significant performance improvement.
//    - Updated Fermat's Little Theorem test to use a^(p-1) mod p = 1
//    - Added isProbablePrime method.
//    - Updated documentation.
//
// 4) August 9, 2002 (Version 1.0)
//    - Initial Release.
//
//
// References
// [1] D. E. Knuth, "Seminumerical Algorithms", The Art of Computer Programming Vol. 2,
//     3rd Edition, Addison-Wesley, 1998.
//
// [2] K. H. Rosen, "Elementary Number Theory and Its Applications", 3rd Ed,
//     Addison-Wesley, 1993.
//
// [3] B. Schneier, "Applied Cryptography", 2nd Ed, John Wiley & Sons, 1996.
//
// [4] A. Menezes, P. van Oorschot, and S. Vanstone, "Handbook of Applied Cryptography",
//     CRC Press, 1996, www.cacr.math.uwaterloo.ca/hac
//
// [5] A. Bosselaers, R. Govaerts, and J. Vandewalle, "Comparison of Three Modular
//     Reduction Functions," Proc. CRYPTO'93, pp.175-186.
//
// [6] R. Baillie and S. S. Wagstaff Jr, "Lucas Pseudoprimes", Mathematics of Computation,
//     Vol. 35, No. 152, Oct 1980, pp. 1391-1417.
//
// [7] H. C. Williams, "Édouard Lucas and Primality Testing", Canadian Mathematical
//     Society Series of Monographs and Advance Texts, vol. 22, John Wiley & Sons, New York,
//     NY, 1998.
//
// [8] P. Ribenboim, "The new book of prime number records", 3rd edition, Springer-Verlag,
//     New York, NY, 1995.
//
// [9] M. Joye and J.-J. Quisquater, "Efficient computation of full Lucas sequences",
//     Electronics Letters, 32(6), 1996, pp 537-538.
//
//************************************************************************************

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace CSharpRTMP.Common
{
    public interface IBigInteger
    {
        uint this[int pos] { get; set; }
    }
    public struct BigIntegerShell : IBigInteger
    {
        private readonly uint[] _data;
        public int Length;                 
        public int Offset;

        public uint this[int pos]
        {
            get { return _data[Offset + pos]; }
            set { _data[Offset + pos] = value; }
        }

        public BigIntegerShell(BigInteger bi,int offset,int length)
        {
            _data = bi.Data;
            Offset = offset;
            Length = length;
        }
        public static BigInteger operator *(BigIntegerShell bi1, BigInteger bi2)
        {
            var result = new BigInteger(0);
            // multiply the absolute values
            try
            {
                for (int i = 0; i < bi1.Length; i++)
                {
                    if (bi1[i] == 0) continue;

                    ulong mcarry = 0;
                    for (int j = 0, k = i; j < bi2.Length; j++, k++)
                    {
                        // k = i + j
                        ulong val = ((ulong)bi1[i] * (ulong)bi2.Data[j]) +
                                    (ulong)result.Data[k] + mcarry;

                        result.Data[k] = (uint)(val & 0xFFFFFFFF);
                        mcarry = (val >> 32);
                    }

                    if (mcarry != 0)
                        result.Data[i + bi2.Length] = (uint)mcarry;
                }
            }
            catch (Exception)
            {
                throw (new ArithmeticException("Multiplication overflow."));
            }
            result.Length = bi1.Length + bi2.Length;
            while (result.Length > 1 && result.Data[result.Length - 1] == 0)
                result.Length--;
            return result;
        }
    }
    public struct BigInteger : IBigInteger
    {
        // maximum length of the BigInteger in uint (4 bytes)
        // change this to suit the required level of precision.

        private const int maxLength = 70;
        public static readonly ConcurrentBag<uint[]> Pool = new ConcurrentBag<uint[]>();
        public readonly uint[] Data;             // stores bytes from the Big Integer
        public int Length;                 // number of actual chars used
        public uint this[int pos]
        {
            get { return Data[pos]; }
            set { Data[pos] = value; }
        }

        //public void CopyFrom(BigInteger bi)
        //{
        //    dataLength = bi.dataLength;
        //    Array.Copy(bi.data,data,maxLength);
        //}
       
        public void Recycle()
        {
            Clear();
            Pool.Add(Data);
        }
        public void Clear()
        {
            Array.Clear(Data,0,maxLength);
            Length = 1;
        }

        //***********************************************************************
        // Constructor (Default value for BigInteger is 0
        //***********************************************************************

        //public BigInteger()
        //{
        //    data = new uint[maxLength];
        //    dataLength = 1;
        //}


        //***********************************************************************
        // Constructor (Default value provided by long)
        //***********************************************************************
        
        public BigInteger(long value)
        {
            if (!Pool.TryTake(out Data))
            {
                Data = new uint[maxLength];
            }
            long tempVal = value;

            // copy bytes from long to BigInteger without any assumption of
            // the length of the long datatype

            Length = 0;
            while(value != 0 && Length < maxLength)
            {
                Data[Length] = (uint)value;
                value >>= 32;
                Length++;
            }

            if(tempVal > 0)         // overflow check for +ve value
            {
                if(value != 0 || (Data[maxLength-1] & 0x80000000) != 0)
                    throw(new ArithmeticException("Positive overflow in constructor."));
            }
            else if(tempVal < 0)    // underflow check for -ve value
            {
                if(value != -1 || (Data[Length-1] & 0x80000000) == 0)
                    throw(new ArithmeticException("Negative underflow in constructor."));
            }

            if(Length == 0)
                Length = 1;
        }


        //***********************************************************************
        // Constructor (Default value provided by ulong)
        //***********************************************************************

        public BigInteger(ulong value)
        {
            if (!Pool.TryTake(out Data))
            {
                Data = new uint[maxLength];
            }

            // copy bytes from ulong to BigInteger without any assumption of
            // the length of the ulong datatype

            Length = 0;
            while(value != 0 && Length < maxLength)
            {
                Data[Length] = (uint)value;
                value >>= 32;
                Length++;
            }

            if(value != 0 || (Data[maxLength-1] & 0x80000000) != 0)
                throw(new ArithmeticException("Positive overflow in constructor."));

            if(Length == 0)
                Length = 1;
        }



        //***********************************************************************
        // Constructor (Default value provided by BigInteger)
        //***********************************************************************

        public BigInteger(BigInteger bi)
        {
            if (!Pool.TryTake(out Data))
            {
                Data = new uint[maxLength];
            }
            Length = bi.Length;

            for(int i = 0; i < Length; i++)
                Data[i] = bi.Data[i];
        }




        //***********************************************************************
        // Constructor (Default value provided by an array of bytes)
        //
        // The lowest index of the input byte array (i.e [0]) should contain the
        // most significant byte of the number, and the highest index should
        // contain the least significant byte.
        //
        // E.g.
        // To initialize "a" with the default value of 0x1D4F in base 16
        //      byte[] temp = { 0x1D, 0x4F };
        //      BigInteger a = new BigInteger(temp)
        //
        // Note that this method of initialization does not allow the
        // sign to be specified.
        //
        //***********************************************************************

        public BigInteger(byte[] inData)
        {
            Length = inData.Length >> 2;

            int leftOver = inData.Length & 0x3;
            if(leftOver != 0)         // length not multiples of 4
                Length++;


            if(Length > maxLength)
                throw(new ArithmeticException("Byte overflow in constructor."));

            if (!Pool.TryTake(out Data))
            {
                Data = new uint[maxLength];
            }

            for(int i = inData.Length - 1, j = 0; i >= 3; i -= 4, j++)
            {
                Data[j] = (uint)((inData[i-3] << 24) + (inData[i-2] << 16) +
                                 (inData[i-1] <<  8) + inData[i]);
            }

            if(leftOver == 1)
                Data[Length-1] = (uint)inData[0];
            else if(leftOver == 2)
                Data[Length-1] = (uint)((inData[0] << 8) + inData[1]);
            else if(leftOver == 3)
                Data[Length-1] = (uint)((inData[0] << 16) + (inData[1] << 8) + inData[2]);


            while(Length > 1 && Data[Length-1] == 0)
                Length--;

            //Console.WriteLine("Len = " + dataLength);
        }


        //***********************************************************************
        // Overloading of the typecast operator.
        // For BigInteger bi = 10;
        //***********************************************************************

        public static implicit operator BigInteger(long value) => new BigInteger(value);

        public static implicit operator BigInteger(ulong value) => new BigInteger(value);

        public static implicit operator BigInteger(int value) => new BigInteger(value);

        public static implicit operator BigInteger(uint value) => new BigInteger((ulong)value);


        public void Subtract(BigInteger bi2)
        {
            if (Length < bi2.Length) Length = bi2.Length;

            long carryIn = 0;
            for (int i = 0; i < Length; i++)
            {
                long diff = (long)Data[i] - (long)bi2.Data[i] - carryIn;
                Data[i] = (uint)diff;
                carryIn = diff < 0 ? 1 : 0;
            }

            // roll over to negative
            if (carryIn != 0)
            {
                for (int i = Length; i < maxLength; i++)
                    Data[i] = 0xFFFFFFFF;
                Length = maxLength;
            }

            // fixed in v1.03 to give correct datalength for a - (-b)
            while (Length > 1 && Data[Length - 1] == 0)
                Length--;
        }

        //***********************************************************************
        // Overloading of multiplication operator
        //***********************************************************************

        public static BigInteger operator *(BigInteger bi1, BigInteger bi2)
        {
            var result = new BigInteger(0);
            Multiply(bi1, bi2, ref result);
            return result;
        }

        public static void Multiply(BigInteger bi1, ulong bi2, ref BigInteger result)
        {
            const int lastPos = maxLength - 1;
            result.Clear();
            uint x1= (uint)bi2, x2= (uint) (bi2 >> 32);
            int bi2Length = x2 == 0 ? 1 : 2;
            for (int i = 0; i < bi1.Length; i++)
            {
                if (bi1.Data[i] == 0) continue;

                ulong mcarry = 0;
                for (int j = 0, k = i; j < bi2Length; j++, k++)
                {
                    
                    // k = i + j
                    ulong val = ((ulong)bi1.Data[i] * (ulong)(j==0?x1:x2)) +
                                (ulong)result.Data[k] + mcarry;

                    result.Data[k] = (uint)val;
                    mcarry = (val >> 32);
                }

                if (mcarry != 0)
                    result.Data[i + bi2Length] = (uint)mcarry;
            }
            result.Length = bi1.Length + bi2Length;
            if (result.Length > maxLength)
                result.Length = maxLength;

            while (result.Length > 1 && result.Data[result.Length - 1] == 0)
                result.Length--;
        }

        public static void Multiply(BigInteger bi1, BigInteger bi2,ref BigInteger result)
        {
            const int lastPos = maxLength - 1;
            bool bi1Neg = false, bi2Neg = false;

            // take the absolute value of the inputs
            try
            {
                if ((bi1.Data[lastPos] & 0x80000000) != 0)     // bi1 negative
                {
                    bi1Neg = true; bi1 = -bi1;
                }
                if ((bi2.Data[lastPos] & 0x80000000) != 0)     // bi2 negative
                {
                    bi2Neg = true; bi2 = -bi2;
                }
            }
            catch (Exception) { }

            result.Clear();

            // multiply the absolute values
            try
            {
                for (int i = 0; i < bi1.Length; i++)
                {
                    if (bi1.Data[i] == 0) continue;

                    ulong mcarry = 0;
                    for (int j = 0, k = i; j < bi2.Length; j++, k++)
                    {
                        // k = i + j
                        ulong val = ((ulong)bi1.Data[i] * (ulong)bi2.Data[j]) +
                                    (ulong)result.Data[k] + mcarry;

                        result.Data[k] = (uint)val;
                        mcarry = (val >> 32);
                    }

                    if (mcarry != 0)
                        result.Data[i + bi2.Length] = (uint)mcarry;
                }
            }
            catch (Exception)
            {
                throw (new ArithmeticException("Multiplication overflow."));
            }


            result.Length = bi1.Length + bi2.Length;
            if (result.Length > maxLength)
                result.Length = maxLength;

            while (result.Length > 1 && result.Data[result.Length - 1] == 0)
                result.Length--;

            // overflow check (result is -ve)
            if ((result.Data[lastPos] & 0x80000000) != 0)
            {
                if (bi1Neg != bi2Neg && result.Data[lastPos] == 0x80000000)    // different sign
                {
                    // handle the special case where multiplication produces
                    // a max negative number in 2's complement.

                    if (result.Length == 1)
                        return;
                    else
                    {
                        bool isMaxNeg = true;
                        for (int i = 0; i < result.Length - 1 && isMaxNeg; i++)
                        {
                            if (result.Data[i] != 0)
                                isMaxNeg = false;
                        }

                        if (isMaxNeg)
                            return;
                    }
                }

                throw (new ArithmeticException("Multiplication overflow."));
            }

            // if input has different signs, then result is -ve
            if (bi1Neg != bi2Neg) result.Negative();
            return;
        }

        //***********************************************************************
        // Overloading of unary << operators
        //***********************************************************************

        public static BigInteger operator <<(BigInteger bi1, int shiftVal)
        {
            var result = new BigInteger(bi1);
            result.Length = shiftLeft(result.Data, shiftVal);

            return result;
        }


        // least significant bits at lower part of buffer

        private static int shiftLeft(uint[] buffer, int shiftVal)
        {
            int shiftAmount = 32;
            int bufLen = buffer.Length;

            while(bufLen > 1 && buffer[bufLen-1] == 0)
                bufLen--;

            for(int count = shiftVal; count > 0;)
            {
                if(count < shiftAmount)
                    shiftAmount = count;

                //Console.WriteLine("shiftAmount = {0}", shiftAmount);

                ulong carry = 0;
                for(int i = 0; i < bufLen; i++)
                {
                    ulong val = ((ulong)buffer[i]) << shiftAmount;
                    val |= carry;

                    buffer[i] = (uint)(val & 0xFFFFFFFF);
                    carry = val >> 32;
                }

                if(carry != 0)
                {
                    if(bufLen + 1 <= buffer.Length)
                    {
                        buffer[bufLen] = (uint)carry;
                        bufLen++;
                    }
                }
                count -= shiftAmount;
            }
            return bufLen;
        }


        private static int shiftRight(uint[] buffer, int shiftVal)
        {
            int shiftAmount = 32;
            int invShift = 0;
            int bufLen = buffer.Length;

            while(bufLen > 1 && buffer[bufLen-1] == 0)
                bufLen--;

            //Console.WriteLine("bufLen = " + bufLen + " buffer.Length = " + buffer.Length);

            for(int count = shiftVal; count > 0;)
            {
                if(count < shiftAmount)
                {
                    shiftAmount = count;
                    invShift = 32 - shiftAmount;
                }

                //Console.WriteLine("shiftAmount = {0}", shiftAmount);

                ulong carry = 0;
                for(int i = bufLen - 1; i >= 0; i--)
                {
                    ulong val = ((ulong)buffer[i]) >> shiftAmount;
                    val |= carry;

                    carry = ((ulong)buffer[i]) << invShift;
                    buffer[i] = (uint)(val);
                }

                count -= shiftAmount;
            }

            while(bufLen > 1 && buffer[bufLen-1] == 0)
                bufLen--;

            return bufLen;
        }

        //***********************************************************************
        // Overloading of the NEGATE operator (2's complement)
        //***********************************************************************

        public static BigInteger operator -(BigInteger bi1)
        {
            // handle neg of zero separately since it'll cause an overflow
            // if we proceed.

            if(bi1.Length == 1 && bi1.Data[0] == 0)
                return (new BigInteger(0));

            BigInteger result = new BigInteger(bi1);

            // 1's complement
            for(int i = 0; i < maxLength; i++)
                result.Data[i] = (uint)(~(bi1.Data[i]));

            // add one to result of 1's complement
            long val, carry = 1;
            int index = 0;

            while(carry != 0 && index < maxLength)
            {
                val = (long)(result.Data[index]);
                val++;

                result.Data[index] = (uint)val;
                carry = val >> 32;

                index++;
            }

            if((bi1.Data[maxLength-1] & 0x80000000) == (result.Data[maxLength-1] & 0x80000000))
                throw (new ArithmeticException("Overflow in negation.\n"));

            result.Length = maxLength;

            while(result.Length > 1 && result.Data[result.Length-1] == 0)
                result.Length--;
            return result;
        }

        public void Negative()
        {
            if (Length == 1 && Data[0] == 0)return;
            // 1's complement
            for (int i = 0; i < maxLength; i++)
                Data[i] = ~Data[i];
            // add one to result of 1's complement
            long carry = 1;
            int index = 0;
            while (carry != 0 && index < maxLength)
            {
                long val = Data[index];
                val++;
                Data[index] = (uint)val;
                carry = val >> 32;
                index++;
            }
            Length = maxLength;
            while (Length > 1 && Data[Length - 1] == 0)
                Length--;
        }

        //***********************************************************************
        // Overloading of equality operator
        //***********************************************************************

        public static bool operator ==(BigInteger bi1, BigInteger bi2) => bi1.Equals(bi2);


        public static bool operator !=(BigInteger bi1, BigInteger bi2) => !(bi1.Equals(bi2));


        public override bool Equals(object o)
        {
            BigInteger bi = (BigInteger)o;

            if(this.Length != bi.Length)
                return false;

            for(int i = 0; i < this.Length; i++)
            {
                if(this.Data[i] != bi.Data[i])
                    return false;
            }
            return true;
        }


        public override int GetHashCode() => this.ToString().GetHashCode();


        //***********************************************************************
        // Overloading of inequality operator
        //***********************************************************************

        public static bool operator >(BigInteger bi1, BigInteger bi2)
        {
            int pos = maxLength - 1;

            // bi1 is negative, bi2 is positive
            if((bi1.Data[pos] & 0x80000000) != 0 && (bi2.Data[pos] & 0x80000000) == 0)
                return false;

                // bi1 is positive, bi2 is negative
            else if((bi1.Data[pos] & 0x80000000) == 0 && (bi2.Data[pos] & 0x80000000) != 0)
                return true;

            // same sign
            int len = (bi1.Length > bi2.Length) ? bi1.Length : bi2.Length;
            for(pos = len - 1; pos >= 0 && bi1.Data[pos] == bi2.Data[pos]; pos--);

            if(pos >= 0)
            {
                if(bi1.Data[pos] > bi2.Data[pos])
                    return true;
                return false;
            }
            return false;
        }


        public static bool operator <(BigInteger bi1, BigInteger bi2)
        {
            int pos = maxLength - 1;

            // bi1 is negative, bi2 is positive
            if((bi1.Data[pos] & 0x80000000) != 0 && (bi2.Data[pos] & 0x80000000) == 0)
                return true;

                // bi1 is positive, bi2 is negative
            else if((bi1.Data[pos] & 0x80000000) == 0 && (bi2.Data[pos] & 0x80000000) != 0)
                return false;

            // same sign
            int len = (bi1.Length > bi2.Length) ? bi1.Length : bi2.Length;
            for(pos = len - 1; pos >= 0 && bi1.Data[pos] == bi2.Data[pos]; pos--);

            if(pos >= 0)
            {
                if(bi1.Data[pos] < bi2.Data[pos])
                    return true;
                return false;
            }
            return false;
        }


        public static bool operator >=(BigInteger bi1, BigInteger bi2) => bi1 == bi2 || bi1 > bi2;


        public static bool operator <=(BigInteger bi1, BigInteger bi2) => bi1 == bi2 || bi1 < bi2;

        public void DivideMultiByte(BigInteger bi2)
        {
            int remainderLen = Length + 1;

            uint mask = 0x80000000;
            uint val = bi2.Data[bi2.Length - 1];
            int shift = 0, resultPos = 0;

            while (mask != 0 && (val & mask) == 0)
            {
                shift++; mask >>= 1;
            }
            var remainder = new BigInteger(this) {Length = remainderLen};
            shiftLeft(remainder.Data, shift);
            bi2 = bi2 << shift;

            int j = remainderLen - bi2.Length;
            int pos = remainderLen - 1;

            ulong firstDivisorByte = bi2.Data[bi2.Length - 1];
            ulong secondDivisorByte = bi2.Data[bi2.Length - 2];

            int divisorLen = bi2.Length + 1;
            var kk = new BigInteger(0) {Length = divisorLen};
            var ss = new BigInteger(0);
            while (j > 0)
            {
                ulong dividend = ((ulong)remainder.Data[pos] << 32) + (ulong)remainder.Data[pos - 1];

                ulong q_hat = dividend / firstDivisorByte;
                ulong r_hat = dividend % firstDivisorByte;
                bool done = false;
                while (!done)
                {
                    done = true;

                    if (q_hat == 0x100000000 ||
                       (q_hat * secondDivisorByte) > ((r_hat << 32) + remainder.Data[pos - 2]))
                    {
                        q_hat--;
                        r_hat += firstDivisorByte;

                        if (r_hat < 0x100000000)
                            done = false;
                    }
                }
                kk.Length = divisorLen;
                Array.Copy(remainder.Data, pos - divisorLen + 1 ,kk.Data, 0, divisorLen);
                while (kk.Length > 1 && kk.Data[kk.Length - 1] == 0)
                    kk.Length--;
                
               // BigInteger ss = bi2 * (long)q_hat;
                Multiply(bi2,q_hat,ref ss);
                while (ss > kk)
                {
                    q_hat--;
                    ss.Subtract(bi2);
                }
                kk.Subtract(ss);
                
                Array.Copy(kk.Data,0, remainder.Data, pos - divisorLen + 1, divisorLen);
 
                Data[resultPos++] = (uint)q_hat;
                pos--;
                j--;
            }
            Length = resultPos;
            Array.Reverse(Data,0,Length);
            Array.Clear(Data,Length,maxLength-Length);
            while (Length > 1 && Data[Length - 1] == 0)
                Length--;
            if (Length == 0)
                Length = 1;
            kk.Recycle();
            ss.Recycle();
            remainder.Recycle();
        }

        public BigInteger GetRemainderMultiByte(BigInteger bi2)
        {
            var remainder = new BigInteger(this);
            remainder.Length++;
            uint mask = 0x80000000;
            uint val = bi2.Data[bi2.Length - 1];
            int shift = 0;

            while (mask != 0 && (val & mask) == 0)
            {
                shift++; mask >>= 1;
            }

            shiftLeft(remainder.Data, shift);
            bi2 = bi2 << shift;

            int j = remainder.Length - bi2.Length;
            int pos = remainder.Length - 1;

            ulong firstDivisorByte = bi2.Data[bi2.Length - 1];
            ulong secondDivisorByte = bi2.Data[bi2.Length - 2];

            int divisorLen = bi2.Length + 1;
          
            var kk = new BigInteger(0) { Length = divisorLen };
            var ss = new BigInteger(0);
            while (j > 0)
            {
                ulong dividend = ((ulong)remainder.Data[pos] << 32) + (ulong)remainder.Data[pos - 1];
                //Console.WriteLine("dividend = {0}", dividend);

                ulong q_hat = dividend / firstDivisorByte;
                ulong r_hat = dividend % firstDivisorByte;

                //Console.WriteLine("q_hat = {0:X}, r_hat = {1:X}", q_hat, r_hat);

                bool done = false;
                while (!done)
                {
                    done = true;

                    if (q_hat == 0x100000000 ||
                       (q_hat * secondDivisorByte) > ((r_hat << 32) + remainder.Data[pos - 2]))
                    {
                        q_hat--;
                        r_hat += firstDivisorByte;

                        if (r_hat < 0x100000000)
                            done = false;
                    }
                }

                kk.Length = divisorLen;
                Array.Copy(remainder.Data, pos - divisorLen + 1, kk.Data, 0, divisorLen);
                while (kk.Length > 1 && kk.Data[kk.Length - 1] == 0)kk.Length--;
                //var ss = bi2 * (long)q_hat;
                Multiply(bi2, q_hat, ref ss);
                while (ss > kk)
                {
                    q_hat--;
                    ss.Subtract(bi2);
                }
                kk.Subtract(ss);
                Array.Copy(kk.Data, 0, remainder.Data, pos - divisorLen + 1, divisorLen);
                pos--;
                j--;
            }
            ss.Recycle();
            kk.Recycle();
            remainder.Length = shiftRight(remainder.Data, shift);
            return remainder;
        }

    
        public void DivideSingleByte(BigInteger bi2)
        {
            int resultPos = 0;

            ulong divisor = (ulong)bi2.Data[0];
            int pos = Length - 1;
            ulong dividend = (ulong)Data[pos];

            var remainder = new BigInteger(this);
         
            if (dividend >= divisor)
            {
                ulong quotient = dividend / divisor;
                Data[resultPos++] = (uint)quotient;
                remainder.Data[pos] = (uint)(dividend % divisor);
            }
            pos--;
            while (pos >= 0)
            {
                //Console.WriteLine(pos);
                dividend = ((ulong)remainder.Data[pos + 1] << 32) + (ulong)remainder.Data[pos];
                ulong quotient = dividend / divisor;
                Data[resultPos++] = (uint)quotient;
                remainder.Data[pos + 1] = 0;
                remainder.Data[pos--] = (uint)(dividend % divisor);
                //Console.WriteLine(">>>> " + bi1);
            }
            Length = resultPos;
            remainder.Recycle();
        }

        public BigInteger GetRemainderSingleByte(BigInteger bi2)
        {
            var result = new BigInteger(this);

            ulong divisor = (ulong)bi2.Data[0];
            int pos = Length - 1;
            ulong dividend = (ulong)Data[pos];

            //var outRemainder = new uint[dataLength];
            //Array.Copy(data, outRemainder, dataLength);
            if (dividend >= divisor)
            {
                //ulong quotient = dividend / divisor;
                //data[resultPos++] = (uint)quotient;
                result.Data[pos] = (uint)(dividend % divisor);
            }
            pos--;
            while (pos >= 0)
            {
                //Console.WriteLine(pos);
                dividend = ((ulong)result.Data[pos + 1] << 32) + (ulong)result.Data[pos];
               // ulong quotient = dividend / divisor;
                //data[resultPos++] = (uint)quotient;
                result.Data[pos + 1] = 0;
                result.Data[pos--] = (uint)(dividend % divisor);
                //Console.WriteLine(">>>> " + bi1);
            }
            while (result.Length > 1 && result.Data[Length - 1] == 0)
                result.Length--;

            if (result.Length == 0)
                result.Length = 1;
            return result;
        }
       
        public void Divide(BigInteger bi2)
        {
            const int lastPos = maxLength - 1;
            bool divisorNeg = false, dividendNeg = false;

            if ((Data[lastPos] & 0x80000000) != 0)     // bi1 negative
            {
                Negative();
                dividendNeg = true;
            }
            if ((bi2.Data[lastPos] & 0x80000000) != 0)     // bi2 negative
            {
                bi2 = -bi2;
                divisorNeg = true;
            }

            if (this < bi2)
            {
                Clear();
            }

            else
            {
                if (bi2.Length == 1)
                    DivideSingleByte(bi2);
                    //singleByteDivide(this, bi2, ref this,ref remainder);
                else
                    //multiByteDivide(this, bi2, quotient, remainder);
                    DivideMultiByte(bi2);
                if (dividendNeg != divisorNeg)
                    Negative();
            }
        }

        //***********************************************************************
        // Overloading of modulus operator
        //***********************************************************************

        public static BigInteger operator %(BigInteger bi1, BigInteger bi2)
        {
            const int lastPos = maxLength-1;
            bool dividendNeg = false;
            if((bi1.Data[lastPos] & 0x80000000) != 0)     // bi1 negative
            {
                bi1 = -bi1;
                dividendNeg = true;
            }
            if((bi2.Data[lastPos] & 0x80000000) != 0)     // bi2 negative
                bi2 = -bi2;

            if(bi1 < bi2)
            {
                return new BigInteger(bi1);
            }
            var remainder = bi2.Length == 1 ? bi1.GetRemainderSingleByte(bi2) : bi1.GetRemainderMultiByte(bi2);
            if(dividendNeg)remainder.Negative();
            return remainder;
        }

        //***********************************************************************
        // Modulo Exponentiation
        //***********************************************************************

        public BigInteger ModPow(BigInteger exp, BigInteger n)
        {
            if((exp.Data[maxLength-1] & 0x80000000) != 0)
                throw (new ArithmeticException("Positive exponents only."));

            BigInteger resultNum = 1;
            BigInteger tempNum;
            bool thisNegative = false;

            if((this.Data[maxLength-1] & 0x80000000) != 0)   // negative this
            {
                tempNum = -this % n;
                thisNegative = true;
            }
            else
                tempNum = this % n;  // ensures (tempNum * tempNum) < b^(2k)

            if((n.Data[maxLength-1] & 0x80000000) != 0)   // negative n
                n = -n;

            // calculate constant = b^(2k) / m
            var constant = new BigInteger(0);

            int i = n.Length << 1;
            constant.Data[i] = 0x00000001;
            constant.Length = i + 1;
            constant.Divide(n);
            //constant /= n;
            int totalBits = exp.bitCount();
            int count = 0;
            var temp = new BigInteger(0);
            // perform squaring and multiply exponentiation
            for(var pos = 0; pos < exp.Length; pos++)
            {
                uint mask = 0x01;
                //Console.WriteLine("pos = " + pos);

                for(var index = 0; index < 32; index++)
                {
                   
                    if ((exp.Data[pos] & mask) != 0)
                    {
                        BarrettReduction(ref resultNum, tempNum, n, constant, ref temp);
                    }
                    mask <<= 1;
                    BarrettReduction(ref tempNum, tempNum, n, constant, ref temp);

                    if(tempNum.Length == 1 && tempNum.Data[0] == 1)
                    {
                        if(thisNegative && (exp.Data[0] & 0x1) != 0)    //odd exp
                            resultNum.Negative();
                        return resultNum;
                    }
                    count++;
                    if(count == totalBits)
                        break;
                }
            }
            constant.Recycle();
            temp.Recycle();
            if(thisNegative && (exp.Data[0] & 0x1) != 0)    //odd exp
                resultNum.Negative();

            return resultNum;
        }



        //***********************************************************************
        // Fast calculation of modular reduction using Barrett's reduction.
        // Requires x < b^(2k), where b is the base.  In this case, base is
        // 2^32 (uint).
        //
        // Reference [4]
        //***********************************************************************

        private void BarrettReduction(ref BigInteger x1,BigInteger x2, BigInteger n, BigInteger constant,ref BigInteger temp)
        {
            Multiply(x1, x2, ref temp);
            int k = n.Length,
                kPlusOne = k+1,
                kMinusOne = k-1;

            //var q1 = new BigInteger(0);

            //// q1 = x / b^(k-1)

            //for (int i = kMinusOne, j = 0; i < x.Length; i++, j++)
            //    q1.Data[j] = x.Data[i];
            //q1.Length = x.Length - kMinusOne;
            //if(q1.Length <= 0)
            //    q1.Length = 1;

            //var q2 = q1 * constant;
            //q1.Recycle();
            //q1 = q2;

            var q1 = new BigIntegerShell(temp, kMinusOne, temp.Length - kMinusOne) * constant;
            // r1 = x mod b^(k+1)
            // i.e. keep the lowest (k+1) words
            //var r1 = new BigInteger(0);
            //int lengthToCopy = (x.Length > kPlusOne) ? kPlusOne : x.Length;
            temp.Length = (temp.Length > kPlusOne) ? kPlusOne : temp.Length;
            //for(int i = 0; i < lengthToCopy; i++)
            //    r1.Data[i] = x.Data[i];
            //r1.Length = lengthToCopy;

            // r2 = (q3 * n) mod b^(k+1)
            // partial multiplication of q3 and n

            var r2 = new BigInteger(0);
            for (int i = kPlusOne; i < q1.Length; i++)
            {
                if(q1.Data[i] == 0)     continue;

                ulong mcarry = 0;
                int t = i - kPlusOne;
                for(int j = 0; j < n.Length && t < kPlusOne; j++, t++)
                {
                    // t = i + j
                    ulong val = (q1.Data[i] * (ulong)n.Data[j]) +
                                r2.Data[t] + mcarry;

                    r2.Data[t] = (uint)val;
                    mcarry = (val >> 32);
                }

                if(t < kPlusOne)
                    r2.Data[t] = (uint)mcarry;
            }
            r2.Length = kPlusOne;
            while(r2.Length > 1 && r2.Data[r2.Length-1] == 0)
                r2.Length--;

            temp.Subtract(r2);
            r2.Recycle();
            if ((temp.Data[maxLength - 1] & 0x80000000) != 0)        // negative
            {
                ulong carry = 1;
                for (var i = kPlusOne; carry != 0 && i<maxLength; i++)
                {
                    var sum = temp.Data[i] + carry;
                    carry = sum >> 32;
                    temp.Data[i] = (uint)sum;
                }
            }

            while (temp >= n)
                temp.Subtract(n);
            q1.Recycle();
            q1 = temp;
            temp = x1;
            x1 = q1;
        }
    
        //***********************************************************************
        // Returns the position of the most significant bit in the BigInteger.
        //
        // Eg.  The result is 0, if the value of BigInteger is 0...0000 0000
        //      The result is 1, if the value of BigInteger is 0...0000 0001
        //      The result is 2, if the value of BigInteger is 0...0000 0010
        //      The result is 2, if the value of BigInteger is 0...0000 0011
        //
        //***********************************************************************

        public int bitCount()
        {
            while(Length > 1 && Data[Length-1] == 0)
                Length--;

            uint value = Data[Length - 1];
            uint mask = 0x80000000;
            int bits = 32;

            while(bits > 0 && (value & mask) == 0)
            {
                bits--;
                mask >>= 1;
            }
            bits += ((Length - 1) << 5);

            return bits;
        }


        //***********************************************************************
        // Returns the value of the BigInteger as a byte array.  The lowest
        // index contains the MSB.
        //***********************************************************************

        public byte[] GetBytes(int keySize =0 )
        {
            int numBits = bitCount();

            int numBytes = numBits >> 3;
            if((numBits & 0x7) != 0)
                numBytes++;

            byte[] result = new byte[keySize==0?numBytes:keySize];

            //Console.WriteLine(result.Length);

            int pos = 0;
            uint tempVal, val = Data[Length - 1];

            if((tempVal = (val >> 24 & 0xFF)) != 0)
                result[pos++] = (byte)tempVal;
            if((tempVal = (val >> 16 & 0xFF)) != 0)
                result[pos++] = (byte)tempVal;
            if((tempVal = (val >> 8 & 0xFF)) != 0)
                result[pos++] = (byte)tempVal;
            if((tempVal = (val & 0xFF)) != 0)
                result[pos++] = (byte)tempVal;

            for(int i = Length - 2; i >= 0; i--, pos += 4)
            {
                val = Data[i];
                result[pos+3] = (byte)(val & 0xFF);
                val >>= 8;
                result[pos+2] = (byte)(val & 0xFF);
                val >>= 8;
                result[pos+1] = (byte)(val & 0xFF);
                val >>= 8;
                result[pos] = (byte)(val & 0xFF);
            }
            Recycle();
            return result;
        }

    }
}
