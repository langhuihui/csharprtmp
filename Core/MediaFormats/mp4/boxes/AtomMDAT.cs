using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomMDAT:BaseAtom
    {
        public AtomMDAT(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomMDAT(Movie document, Dictionary<ITrack, int[]> chunks, long contentSize) : base(MDAT)
        {
            throw new NotImplementedException();
        }

        public uint DataOffset;

        public override string Hierarchy(int indent)
        {
            throw new NotImplementedException();
        }

        public override void Read()
        {
            throw new NotImplementedException();
        }
    }
}
