using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpRTMP.Common
{
    public static class CryptLib
    {
        // The HiBITMASK used for computation, FIX FOR LATER: should change to fit your system e.g 64 bit CPUs.
        public const UInt32 _HIBITMASK_ = 0x80000000;
        // The maximum nr supported by the system, used to detect owerflows. 
        public const UInt32 _MAXINUMNR_ = 0xffffffff;
        // The maximum nr you can get using half the number of bits. 
        public const UInt32 _MAXHALFNR_ = 0x0000ffff;
        // the below function begin with BN prefix indicate Big Number

        //compute big numbers A and B's sum C,  viz C = A + B
        public static UInt32 BNAdd(UInt32[] C, UInt32[] A, int offset, UInt32[] B, int nSize)
        {
            UInt32 k = 0; //carry 进位
            for (var i = 0; i < nSize; i++)
            {
                C[i + offset] = A[i + offset] + k;
                if (C[i + offset] >= k)
                {
                    k = 0;
                }
                else
                {
                    k = 1;
                }

                C[i + offset] += B[i];
                if (C[i + offset] < B[i])
                {
                    k++;
                }
            }
            return k;
        }

        // set big number A = 0
        public static void BNSetZero(UInt32[] A, int nSize)
        {
            while (nSize-- > 0)
            {
                A[nSize] = 0;
            }
        }

        // set a = b
        public static void BNSetEqual(UInt32[] a, UInt32[] b, int nSize)
        {
            for (var i = 0; i < nSize; i++)
            {
                a[i] = b[i];
            }
        }

        // Makes a = d , where d is an normal UInt32
        public static void BNSetEqualdw(UInt32[] a, UInt32 d, int nSize)
        {
            if (nSize <= 0)
                return;
            BNSetZero(a, nSize);
            a[0] = d;

        }

        // compare two big numbers. if a > b, return 1; else if a < b, return -1; else if a== b return 0
        public static int BNCompare(UInt32[] a, UInt32[] b, int nSize)
        {
            if (nSize <= 0)
                return 0;

            while (nSize-- > 0)
            {
                if (a[nSize] > b[nSize])
                    return 1;  //grater than
                else if (a[nSize] < b[nSize])
                    return -1;

            }
            return 0;
        }


        //return size of significant digits in A

        public static int BNSizeof(UInt32[] A, int nSize)
        {
            while (nSize-- > 0)
            {
                if (A[nSize] != 0)
                {
                    return (++nSize);
                }
            }

            return 0;
        }

        //return number of significant bits of d

        public static int BNBitLength(UInt32[] d, int nSize)
        {
            int n, i, bits;
            UInt32 mask;

            if (d == null || nSize == 0)
            {
                return 0;
            }

            n = BNSizeof(d, nSize);
            if (n == 0)
                return 0;
            var nLastWord = d[n - 1];

            mask = _HIBITMASK_;

            for (i = 0; mask > 0; i++)
            {
                if ((nLastWord & mask) > 0)
                {
                    break;
                }

                mask = mask >> 1;
            }

            bits = n * (sizeof(UInt32)) * 8 - i;
            return bits;
        }

        public static int BNUiceil(double x)
        {
            UInt32 c;
            if (x < 0)
                return 0;
            c = (UInt32)x;
            if ((x - c) > 0.0)
                c++;
            return (int)c;
        }


        public static UInt32[] BNFromHex(string source )
        {
            int i, j;
            UInt32 t;
            var s = source.ToCharArray();
            var nStringLength=s.Length;

            //nNewLen = BNUiceil(nStringLength * 0.5); // log(16)/log(256)=0.5
            var nNewLen = nStringLength >> 1;
            var bNewDigits = new byte[nNewLen];

            //Array.Clear(bNewDigits, 0, nNewLen);

            for (i = 0; (i < nStringLength) && (s[i] > 0); i++)
            {
                t = s[i];
                if ((t >= '0') && (t <= '9'))
                {
                    t = t - '0';
                }
                else if ((t >= 'a') && (t <= 'f'))
                {
                    t = t - 'a' + 10;
                }
                else if ((t >= 'A') && (t <= 'F'))
                {
                    t = t - 'A' + 10;
                }
                else
                {
                    continue;
                }
                for (j = nNewLen; j > 0; j--)
                {
                    t = t + (UInt32)(bNewDigits[j - 1] << 4);
                    bNewDigits[j - 1] = (byte)(t & 0xFF);
                    t = t >> 8;
                }
            }
            return BNFromByte(bNewDigits);
        }

        public static uint[] BNFromByte(byte[] bytes)
        {
            var a = new uint[bytes.Length/4];
            int i, j;
            for ( i = 0, j = bytes.Length - 1; i < a.Length ; i++)
            {
                uint t = 0;
                for (var k = 0;  k < 32 && j>=0; j--, k += 8)
                {
                    UInt32 t2 = bytes[j];
                    t = t | (t2 << k);
                }
                a[i] = t;
            }
            return a;
        }


        /*  BNShiftLeft(DWORD a[], const DWORD *b, DWORD x, DWORD nSize)
        *	Computes a = b << x 
        *  returns carry 
        */

        public static UInt32 BNShiftLeft(UInt32[] a, UInt32[] b, int x, int nSize)
        {
            UInt32 mask, carry, nextcarry;
            var i = 0;

            if (x >= sizeof(UInt32) * 8)
                return 0;
            mask = _HIBITMASK_;
            for (i = 1; i < x; i++)
            {
                mask = (mask >> 1) | mask;
            }
            if (x == 0)
                mask = 0;
            var y = sizeof(UInt32) * 8 - x;
            carry = 0;
            for (i = 0; i < nSize; i++)
            {
                nextcarry = (b[i] & mask) >> y;
                a[i] = b[i] << x | carry;
                carry = nextcarry;
            }
            return carry;
        }

        /* BNShiftRight(DWORD a[], const DWORD *b, DWORD x, DWORD nSize)
        * Computes a = b >> x 
        * returns carry 
        *
        */

        public static UInt32 BNShiftRight(UInt32[] a, UInt32[] b, int x, int nSize)
        {
            UInt32 mask, carry, nextcarry;
            var i = 0;
            if (x >= sizeof(UInt32) * 8)
                return 0;
            mask = 0x1;
            for (i = 1; i < x; i++)
            {
                mask = (mask << 1) | mask;
            }
            if (x == 0)
                mask = 0x00;
            var y = sizeof(UInt32) * 8 - x;
            carry = 0;
            i = nSize;
            while (i-- > 0)
            {
                nextcarry = (b[i] & mask) << y;
                a[i] = b[i] >> x | carry;
                carry = nextcarry;
            }

            return carry;
        }


        /*
        *	 Function Helper for numeric Multiplication 
        *   Reference: Arbitrary Precision Computation
        *   Splits the x,y to half and performin the multplication of each half. 
        */
        public static int BNMultiplyHelper(out uint p0, out uint p1, UInt32 x, UInt32 y)
        {
            UInt32 carry;

            uint x0 = x & _MAXHALFNR_;
            uint x1 = x >> 16;
            uint y0 = y & _MAXHALFNR_;
            uint y1 = y >> 16;

            p0 = x0 * y0;
            uint t = x0 * y1;
            uint u = x1 * y0;
            t += u;
            if (t < u)
                carry = 1;
            else
                carry = 0;
            carry = (carry << 16) + (t >> 16);
            t = t << 16;

            p0 += t;

            if (p0 < t)
                carry++;
            p1 = x1 * y1;

            p1 += carry;

            return 0;
        }

        /*
        *	BNMultiply(DWORD C[], DWORD A[], DWORD B[], UINT nSize)
        -----------------------------------------------------
        *  Multiplication for very big numbers A,B,C
        *  Assumes that A, B  have the same size, and C have the size of 2*nSize; 
        *  nSize = number of bytes. 
        *  Calculates C = A - B where A >= B
        *  Reference  Knuth, Donald. 1968. The Art of Computer Programming
        *  Returns 0 if success 1 if overflow. 
        * v=B 
        * u=A
        */

        public static int BNMultiply(UInt32[] C, UInt32[] A, UInt32[] B, int nSize)
        {
            Array.Clear(C, 0, 2 * nSize);
            for (var j = 0; j < nSize; j++)
            {
                if (B[j] == 0)
                {
                    C[j + nSize] = 0;
                }
                else
                {
                    UInt32 k = 0;
                    
                    for (var i = 0; i < nSize; i++)
                    {
                        uint tmp0, tmp1;
                        BNMultiplyHelper(out tmp0, out tmp1, A[i], B[j]);
                        tmp0 += k;

                        if (tmp0 < k) //overflow
                            tmp1++;

                        tmp0 += C[i + j];
                        if (tmp0 < C[i + j])
                            tmp1++;
                        k = tmp1;
                        C[i + j] = tmp0;
                    }

                    C[j + nSize] = k;
                }
            }
            return 0;
        }

       
        /*
        * Compute w = w - qv
        * where w = (WnW[n-1]...W[0])
        *  return modified Wn.
        */
        public static UInt32 BNMultSub(UInt32 wn, UInt32[] w,int offset, UInt32[] v, UInt32 q, int nSize)
        {
            uint k = 0;
            if (q == 0)
                return wn;
            for (var i = 0; i < nSize; i++)
            {
                uint t0, t1;
                BNMultiplyHelper(out t0, out t1, q, v[i]);
                w[i + offset] = w[i + offset] - k;
                if (w[i + offset] > _MAXINUMNR_ - k)
                    k = 1;
                else
                    k = 0;
                w[i + offset] -= t0;
                if (w[i + offset] > _MAXINUMNR_ - t0)
                    k++;
                k += t1;

            }
            wn -= k;

            return wn;
        }

        /*	
        *  Function Helper 
        * Compute uu = uu - q(v1v0) 
        * 
        */
        public static void BNMultSubHelper(ref uint uu0, ref uint uu1, UInt32 qhat, UInt32 v1, UInt32 v0)
        {
            UInt32 p0, p1, t;
            p0 = qhat * v0;
            p1 = qhat * v1;

            t = p0 + ((p1 & _MAXHALFNR_) << 16);
            uu0 = uu0 - t;
            if (uu0 > _MAXINUMNR_ - t)
                uu1--;
            uu1 = uu1 - (p1 >> 16);
        }

        /*	Help function for BNDivide (For code cleaness) 
        * Returns true if Qhat is too big
        * i.e. if (Qhat * Vn-2) > (b.Rhat + Uj+n-2)
        * 
        */

        public static bool BNQhatTooBigHelper(UInt32 qhat, UInt32 rhat, UInt32 vn2, UInt32 ujn2)
        {
            uint t0, t1;
            BNMultiplyHelper(out t0, out t1, qhat, vn2);
            return t1 >= rhat && (t1 != rhat || t0 > ujn2);
        }

        /*
        *	Function Helper for numeric Multiplication 
        *  Computes quotient q = u / v, remainder r = u mod v
        *  u an DWORD[2].
        *  v,q,r are normal DWORDs. 
        *  Assumes that v1>=b/2 where b is the size of half DWORD
        *
        */
        public static UInt32 BNDivideHelper(ref UInt32 q, ref UInt32 r, uint u0, uint u1, UInt32 v)
        {
            UInt32 q2, qhat, rhat, t, v0, v1, u2, u3;
            uint uu0 = u1, uu1 = 0;
            var B = _MAXHALFNR_ + 1;

            if ((v & _HIBITMASK_) == 0)
            {
                q = 0;
                r = 0;
                return _MAXINUMNR_;
            }

            v0 = v & _MAXHALFNR_;
            v1 = v >> 16;
            u2 = u1 & _MAXHALFNR_;
            u3 = u1 >> 16;
            u1 = u0 >> 16;
            u0 = u0 & _MAXHALFNR_;
            if (u3 < v1)
                qhat = 0;
            else
                qhat = 1;

            if (qhat > 0)
            {
                rhat = u3 - v1;
                t = rhat << 16 | u2;
                if (v0 > t)
                    qhat--;
            }


            if (qhat > 0)
            {
                BNMultSubHelper(ref uu0, ref uu1, qhat, v1, v0);
                if (uu1 >> 16 != 0)
                {
                    uu0 += v;
                    uu1 = 0;
                    qhat--;
                }
            }
            q2 = qhat;
            t = uu0;
            qhat = t / v1;
            rhat = t - qhat * v1;
            t = rhat << 16 | u1;
            if ((qhat == B) || (qhat * v0 > t))
            {
                qhat--;
                rhat += v1;
                t = rhat << 16 | u1;
                if ((rhat < B) && (qhat * v0 > t))
                    qhat--;
            }

            uu1 = uu0 >> 16;
            uu0 = (uu0 & _MAXHALFNR_) << 16 | u1;
            BNMultSubHelper(ref uu0, ref uu1, qhat, v1, v0);

            if (uu1 >> 16 != 0)
            {
                qhat--;
                uu0 += v;
                uu1 = 0;
            }
            q = qhat << 16;
            t = uu0;
            qhat = t / v1;
            rhat = t - qhat * v1;
            t = rhat << 16 | u0;
            if ((qhat == B) || (qhat * v0 > t))
            {
                qhat--;
                rhat += v1;
                t = rhat << 16 | u0;
                if ((rhat < B) && (qhat * v0 > t))
                {
                    qhat--;
                }
            }

            uu1 = uu0 >> 16;
            uu0 = (uu0 & _MAXHALFNR_) << 16 | u0;
            BNMultSubHelper(ref uu0, ref uu1, qhat, v1, v0);
            if (uu1 >> 16 != 0)
            {
                qhat--;
                uu0 += v;
                uu1 = 0;
            }
            q = q | qhat & _MAXHALFNR_;
            r = uu0;
            return q2;
        }

        /* BNDivdw(DWORD q[], const DWORD a[], DWORD b, UINT nSize)
        *   Calculates quotient q = a div b
        *   Returns remainder r = a mod b
        *   a,q are big numbers of nSize
        *	 r, a are normal DWORDS.
        *
        */
        public static UInt32 BNDividedw(UInt32[] q, UInt32[] u, UInt32 v, int nSize)
        {
            int j, shift;
            UInt32 r, bitmask, overflow;
            if (nSize == 0)
                return 0;
            if (v == 0)
                return 0;

            bitmask = _HIBITMASK_;
            for (shift = 0; shift < 32; shift++)
            {
                if ((v & bitmask) != 0)
                    break;
                bitmask >>= 1;
            }

            v <<= shift;
            overflow = BNShiftLeft(q, u, shift, nSize);

            r = overflow;
            j = nSize;
            while (j-- > 0)
            {
                overflow = BNDivideHelper(ref q[j], ref r, q[j], r, v);
            }
            r >>= shift;
            return r;
        }


        /*
        *	BNDivide(DWORD q[], DWORD r[], const DWORD u[], UINT usize, DWORD v[], UINT vsize)
        -----------------------------------------------------
        *  Division for very big numbers 
        *
        * Computes quotient q = u / v and remainder r = u mod v
        * where q, r, u are multiple precision digits
        * all of udigits and the divisor v is vdigits.
        */

        public static int BNDivide(UInt32[] q, UInt32[] r, UInt32[] u, int usize, UInt32[] v, int vsize)
        {
            int shift, n, m, j;
            UInt32 bitmask, overflow, qhat = 0, rhat = 0;
           
            int qhatOK, cmp;

            Array.Clear(q,0,usize);
            Array.Clear(r,0,usize);
            //BNSetZero(q, usize);
            //BNSetZero(r, usize);

            n = BNSizeof(v, vsize);
            m = BNSizeof(u, usize);
            m -= n;

            if (n == 0)
                return -1;  //divide by zero
            if (n == 1)
            {
                r[0] = BNDividedw(q, u, v[0], usize);
                return 0;
            }
            if (m < 0)
            {
                BNSetEqual(r, u, usize);
                return 0;
            }
            if (m == 0)
            {
                cmp = BNCompare(u, v, n);
                if (cmp < 0)
                {
                    BNSetEqual(r, u, usize);
                    return 0;
                }
                else if (cmp == 0)
                {
                    BNSetEqualdw(q, 1, usize);
                    return 0;
                }

            }

            bitmask = _HIBITMASK_;
            for (shift = 0; shift < 32; shift++)
            {
                if ((v[n - 1] & bitmask) != 0)
                    break;
                bitmask >>= 1;
            }

            overflow = BNShiftLeft(v, v, shift, n);
            overflow = BNShiftLeft(r, u, shift, n + m);
            
            var t1 = overflow;
           
            for (j = m; j >= 0; j--)
            {
                qhatOK = 0;
               
                overflow = BNDivideHelper(ref qhat, ref rhat, r[j + n - 1], t1, v[n - 1]);
                if (overflow != 0)
                {
                    rhat = r[j + n - 1];
                    rhat += v[n - 1];
                    qhat = _MAXINUMNR_;
                    if (rhat < v[n - 1])
                        qhatOK = 1;

                }
                if ((qhat != 0) && (qhatOK == 0) && (BNQhatTooBigHelper(qhat, rhat, v[n - 2], r[j + n - 2]) ))
                {
                    rhat += v[n - 1];
                    qhat--;
                    if (rhat >= v[n - 1])
                    {
                        if (BNQhatTooBigHelper(qhat, rhat, v[n - 2], r[j + n - 2]) )
                            qhat--;
                    }
                }
                overflow = BNMultSub(t1, r,j, v, qhat, n);
                q[j] = qhat;
                if (overflow != 0)
                {
                    q[j]--;
                    overflow = BNAdd(r, r, j,v, n);
                }
                t1 = r[j + n - 1];
            }
            for (j = n; j < m + n; j++)
                r[j] = 0;
            BNShiftRight(r, r, shift, n);
            BNShiftRight(v, v, shift, n);
            return 0;
        }


        public static int BNModuloTmp(UInt32[] r, UInt32[] u, int nUSize, UInt32[] v, int nVSize, UInt32[] tqq, UInt32[] trr)
        {
            BNDivide(tqq, trr, u, nUSize, v, nVSize);
            BNSetEqual(r, trr, nVSize);
            return 0;
        }

        /*	
        * Computes sw = x * x
        * where:
        * 	x is a Big multiprecision Number
        *	w is a Big multiprecision Number of 2*nSize
        *	nSize is the size of the number in bytes. 
        */
        public static int BNSquare(UInt32[] w, UInt32[] x, int nSize)
        {
            int i, j, t, i2, cpos;
            UInt32 k, cbit, carry, u0, u1;

            t = nSize;
            i2 = t << 1;

            Array.Clear(w,0,i2);
            carry = 0;
            cpos = i2 - 1;

            for (i = 0; i < t; i++)
            {
                uint p0, p1;
                i2 = i << 1;
                BNMultiplyHelper(out p0, out p1, x[i], x[i]);
                p0 += w[i2];

                if (p0 < w[i2])
                    p1++;
                k = 0;
                if (i2 == cpos && (carry != 0))
                {
                    p1 += carry;
                    if (p1 < carry)
                        k++;
                    carry = 0;
                }

                u0 = p1;
                u1 = k;
                w[i2] = p0;

                for (j = i + 1; j < t; j++)
                {
                    BNMultiplyHelper(out p0, out p1, x[j], x[i]);

                    cbit = (UInt32)(((p0 & _HIBITMASK_) == 0) ? 0 : 1);
                    k = (UInt32)(((p1 & _HIBITMASK_) == 0) ? 0 : 1);
                    p0 <<= 1;
                    p1 <<= 1;
                    p1 |= cbit;

                    p0 += u0;
                    if (p0 < u0)
                    {
                        p1++;
                        if (p1 == 0)
                            k++;
                    }
                    p1 += u1;
                    if (p1 < u1)
                        k++;

                    p0 += w[i + j];
                    if (p0 < w[i + j])
                    {
                        p1++;
                        if (p1 == 0)
                            k++;
                    }
                    if ((i + j) == cpos && (carry != 0))
                    {
                        p1 += carry;
                        if (p1 < carry)
                            k++;
                        carry = 0;
                    }
                    u0 = p1;
                    u1 = k;
                    w[i + j] = p0;
                }

                carry = u1;
                w[i + t] = u0;
                cpos = i + t;
            }
            return 0;
        }

        public static byte[] BNToBytes(UInt32[] a)
        {
            int i, j, k;
            UInt32 t;
            var nSize = a.Length;
            var c = new byte[nSize<<2];

            for (i = 0, j = c.Length - 1; (i < nSize) && (j >= 0); i++)
            {
                t = a[i];
                for (k = 0; (j >= 0) && (k < 32); j--, k += 8)
                {
                    c[j] = (byte)(t >> k);
                }

            }
            return c;
        }

        /*	Computes y = x^e mod m */
        /*	Binary left-to-right method */
        public static UInt32[]  BNModExp( UInt32[] x, UInt32[] e, UInt32[] m, int nSize)
        {
            int nn = nSize <<1;
            UInt32 mask;
            var t1 = new UInt32[nn];
            var t2 = new UInt32[nn];
            var t3 = new UInt32[nn];
            var tm = m.Clone() as uint[];
            var y = new UInt32[nSize];

            BNSetEqual(tm, m, nSize);
            int n = BNSizeof(e, nSize);
            for (mask = _HIBITMASK_; mask > 0; mask >>= 1)
            {
                if ((e[n - 1] & mask) != 0)
                    break;
            }
            if (mask == 1)
            {
                mask = _HIBITMASK_;
                n--;
            }
            else
            {
                mask >>= 1;
            }

            BNSetEqual(y, x, nSize);

            while (n > 0)
            {
                BNSquare(t1, y, nSize);
                BNModuloTmp(y, t1, nn, tm, nSize, t2, t3);
                if ((mask & e[n - 1]) != 0)
                {
                    BNMultiply(t1, y, x, nSize);
                    BNModuloTmp(y, t1, nn, tm, nSize, t2, t3);
                }

                if (mask == 1)
                {
                    mask = _HIBITMASK_;
                    n--;
                }
                else
                {
                    mask >>= 1;
                }
            }
            return y;
        }
    }
}
