using System.Collections.Generic;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomMOOF:BoxAtom
    {
        public Dictionary<int, AtomTRAF> Trafs = new Dictionary<int, AtomTRAF>();
        private AtomMFHD _atomMFHD;

        public AtomMOOF(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public override void AtomCreated(BaseAtom atom)
        {
            switch (atom.Type)
            {
                case MFHD:
                    _atomMFHD = (AtomMFHD) atom;
                    return;
                case TRAF:
                    Trafs[((AtomTRAF)atom).Id] = (AtomTRAF)atom;
                    return;
            }
        }
    }
}