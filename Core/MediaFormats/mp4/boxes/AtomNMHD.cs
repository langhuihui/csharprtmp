using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{/// <summary>
/// NullMediaHeaderBox
/// </summary>
    public class AtomNMHD:VersionedAtom
    {
        public AtomNMHD(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomNMHD() : base(NMHD)
        {
        }

        public override void ReadData()
        {
            throw new NotImplementedException();
        }
    }
}
