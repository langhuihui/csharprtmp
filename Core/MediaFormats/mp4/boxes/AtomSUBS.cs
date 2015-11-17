using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomSUBS:VersionedAtom
    {
        public AtomSUBS(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomSUBS() : base(SUBS)
        {
        }

        public override void ReadData()
        {
            throw new NotImplementedException();
        }
    }
}
