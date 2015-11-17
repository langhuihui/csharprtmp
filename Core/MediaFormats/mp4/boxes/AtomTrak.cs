namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomTRAK:BoxAtom
    {
        private AtomTKHD _atomTkhd;
        private AtomMDIA _atomMdia;
        private AtomHdlr _atomHdlr;
        private AtomMINF _atomMinf;
        private AtomDINF _atomDinf;
        private AtomSTBL _atomStbl;
        private AtomUDTA _atomUdta;
        private AtomMETA _atomMeta;
        public AtomTRAK(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomTRAK() : base(TRAK)
        {
            
        }
        public uint Id => _atomTkhd?.TrackId??0;
        public override void AtomCreated(BaseAtom atom)
        {
            switch (atom.Type)
            {
                case TKHD:
                    _atomTkhd = (AtomTKHD)atom;
                    break;
                case MDIA:
                    _atomMdia = (AtomMDIA)atom;
                    break;
                case HDLR:
                    _atomHdlr = (AtomHdlr)atom;
                    break;
                case MINF:
                    _atomMinf = (AtomMINF)atom;
                    break;
                case DINF:
                    _atomDinf = (AtomDINF)atom;
                    break;
                case STBL:
                    _atomStbl = (AtomSTBL)atom;
                    break;
                case UDTA:
                    _atomUdta = (AtomUDTA)atom;
                    break;
                case META:
                    _atomMeta = (AtomMETA)atom;
                    break;
            }
        }
    }
}