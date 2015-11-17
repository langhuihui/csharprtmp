using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.MediaFormats.mp4
{
    public class IsoTypeWriter:H2NBinaryWriter
    {
        public IsoTypeWriter(Stream source) : base(source)
        {
        }

        public void WriteFixedPoint1616(double v) => Write((int)(v * 65536));

        public void WriteFixedPoint0230(double v) => Write((int) (v*(1 << 30)));
    }
}
