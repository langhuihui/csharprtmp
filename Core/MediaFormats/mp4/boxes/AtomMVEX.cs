using System.Collections.Generic;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomMVEX:BoxAtom
    {
        private Dictionary<uint,AtomTREX> _trex = new Dictionary<uint, AtomTREX>();
        public AtomMVEX(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public override void AtomCreated(BaseAtom atom)
        {
            switch (atom.Type)
            {
                case TREX:
                    AtomTREX pTemp = (AtomTREX)atom;
                    _trex[pTemp.TrackID] = pTemp;
                    break;
            }
        }
    }
}