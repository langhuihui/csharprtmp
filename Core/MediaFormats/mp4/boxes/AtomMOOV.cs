using System.Collections.Generic;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomMOOV : BoxAtom
    {
        private AtomMVEX _atomMVEX;
        private AtomMVHD _atomMVHD;
        private AtomUDTA _atomUDTA;
        private AtomMETA _atomMETA;
        public List<AtomTRAK> Tracks = new List<AtomTRAK>();

        public AtomMOOV(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomMOOV() : base(MOOV)
        {
            
        }
        public override void AtomCreated(BaseAtom atom)
        {
            switch (atom.Type)
            {
                case MVEX:
                    _atomMVEX = (AtomMVEX) atom;
                    return;
                case MVHD:
                    _atomMVHD = (AtomMVHD) atom;
                    return;
                case UDTA:
                    _atomUDTA = (AtomUDTA) atom;
                    return;
                case TRAK:
                    Tracks.Add((AtomTRAK)atom);
                    return;
                case META:
                    _atomMETA = (AtomMETA) atom;
                    return;
            }
        }
    }
}