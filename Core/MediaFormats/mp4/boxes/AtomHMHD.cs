using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomHMHD:VersionedAtom
    {
        public AtomHMHD(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomHMHD() : base(HMHD)
        {
        }

        public override void ReadData()
        {
            throw new NotImplementedException();
        }
    }
}
