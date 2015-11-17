using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomEDTS:BoxAtom
    {
        public AtomEDTS(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomEDTS() : base(EDTS)
        {
        }
    }
}
