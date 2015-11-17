namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomMDIA:BoxAtom
    {
        private AtomMdhd _atomMDHD;
        private AtomHdlr _atomHDLR;
        private AtomMINF _atomMINF;
        private AtomDINF _atomDINF;
        private AtomSTBL _atomSTBL;

        public AtomMDIA(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomMDIA() : base(MDIA)
        {
            
        }
        public override void AtomCreated(BaseAtom atom)
        {
            switch (atom.Type)
            {
                case MDHD:
                    _atomMDHD = (AtomMdhd) atom;
                    return;
                case HDLR:
                    _atomHDLR = (AtomHdlr) atom;
                    return;
                case MINF:
                    _atomMINF = (AtomMINF) atom;
                    return;
                case DINF:
                    _atomDINF = (AtomDINF) atom;
                    return;
                case STBL:
                    _atomSTBL = (AtomSTBL) atom;
                    return;
            }
        }
    }
}