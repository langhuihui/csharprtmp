using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRTMP.Core.MediaFormats.mp4
{
    public class Matrix
    {
        double u, v, w;
        double a, b, c, d, tx, ty;

        public Matrix(double a, double b, double c, double d, double u, double v, double w, double tx, double ty)
        {
            this.u = u;
            this.v = v;
            this.w = w;
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;
            this.tx = tx;
            this.ty = ty;
        }


    public override bool Equals(object o)
        {
            if (this == o) return true;
            if (o == null ||GetType() != o.GetType()) return false;

            Matrix matrix = (Matrix)o;

        return matrix.a == a &&
               matrix.b == b &&
               matrix.c == c &&
               matrix.d == d &&
               matrix.tx == tx &&
               matrix.ty == ty &&
               matrix.u == u &&
               matrix.v == v &&
               matrix.w == w;
        }

    public override int GetHashCode()
        {
        long temp = Convert.ToInt64(u);
            var result = (int)(temp ^ (temp >> 32));
            temp = Convert.ToInt64(v);
            result = 31 * result + (int)(temp ^ (temp >> 32));
            temp = Convert.ToInt64(w);
            result = 31 * result + (int)(temp ^ (temp >> 32));
            temp = Convert.ToInt64(a);
            result = 31 * result + (int)(temp ^ (temp >> 32));
            temp = Convert.ToInt64(b);
            result = 31 * result + (int)(temp ^ (temp >> 32));
            temp = Convert.ToInt64(c);
            result = 31 * result + (int)(temp ^ (temp >> 32));
            temp = Convert.ToInt64(d);
            result = 31 * result + (int)(temp ^ (temp >> 32));
            temp = Convert.ToInt64(tx);
            result = 31 * result + (int)(temp ^ (temp >> 32));
            temp = Convert.ToInt64(ty);
            result = 31 * result + (int)(temp ^ (temp >> 32));
            return result;
        }


    public override string ToString()
        {
            if (Equals(ROTATE_0))
            {
                return "Rotate 0°";
            }
            if (Equals(ROTATE_90))
            {
                return "Rotate 90°";
            }
            if (Equals(ROTATE_180))
            {
                return "Rotate 180°";
            }
            if (Equals(ROTATE_270))
            {
                return "Rotate 270°";
            }
            return "Matrix{" +
                    "u=" + u +
                    ", v=" + v +
                    ", w=" + w +
                    ", a=" + a +
                    ", b=" + b +
                    ", c=" + c +
                    ", d=" + d +
                    ", tx=" + tx +
                    ", ty=" + ty +
                    '}';
        }

        public static readonly Matrix ROTATE_0 = new Matrix(1, 0, 0, 1, 0, 0, 1, 0, 0);
        public static readonly Matrix ROTATE_90 = new Matrix(0, 1, -1, 0, 0, 0, 1, 0, 0);
        public static readonly Matrix ROTATE_180 = new Matrix(-1, 0, 0, -1, 0, 0, 1, 0, 0);
        public static readonly Matrix ROTATE_270 = new Matrix(0, -1, 1, 0, 0, 0, 1, 0, 0);

        public static Matrix FromFileOrder(double a, double b, double u, double c, double d, double v, double tx, double ty, double w)
        {
            return new Matrix(a, b, c, d, u, v, w, tx, ty);
        }

        public static Matrix FromByteBuffer(Stream s)
        {
            IsoTypeReader isoTypeReader = new IsoTypeReader(s);
            return FromFileOrder(isoTypeReader.ReadFixedPoint1616(),
                isoTypeReader.ReadFixedPoint1616(),
                isoTypeReader.ReadFixedPoint0230(),
                isoTypeReader.ReadFixedPoint1616(),
                isoTypeReader.ReadFixedPoint1616(),
                isoTypeReader.ReadFixedPoint0230(),
                isoTypeReader.ReadFixedPoint1616(),
                isoTypeReader.ReadFixedPoint1616(),
                isoTypeReader.ReadFixedPoint0230());
        }

        public void GetContent(Stream s)
        {
            IsoTypeWriter isoTypeWriter = new IsoTypeWriter(s);
            isoTypeWriter.WriteFixedPoint1616(a);
            isoTypeWriter.WriteFixedPoint1616(b);
            isoTypeWriter.WriteFixedPoint0230(u);

            isoTypeWriter.WriteFixedPoint1616(c);
            isoTypeWriter.WriteFixedPoint1616(d);
            isoTypeWriter.WriteFixedPoint0230(v);

            isoTypeWriter.WriteFixedPoint1616(tx);
            isoTypeWriter.WriteFixedPoint1616(ty);
            isoTypeWriter.WriteFixedPoint0230(w);
        }
    }
}
