using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomSGPD:VersionedAtom
    {
        public List<IGroupEntry> GroupEntries;

        public AtomSGPD(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomSGPD() : base(SGPD)
        {
        }

        public override void ReadData()
        {
            throw new NotImplementedException();
        }
    }
}
