namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomMINF:BoxAtom
    {
        private AtomSMHD _atomSMHD;
        private AtomDINF _atomDINF;
        private AtomSTBL _atomSTBL;
        private AtomVMHD _atomVMHD;
        private AtomHdlr _atomHDLR;

        public AtomMINF(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomMINF() : base(MINF)
        {
            
        }
        public override void AtomCreated(BaseAtom atom)
        {
            switch (atom.Type)
            {
                case SMHD:
                    _atomSMHD = (AtomSMHD) atom;
                    return;
                case DINF:
                    _atomDINF = (AtomDINF) atom;
                    return;
                case STBL:
                    _atomSTBL = (AtomSTBL)atom;
                    return;
                case VMHD:
                    _atomVMHD = (AtomVMHD)atom;
                    return;
                case HDLR:
                    _atomHDLR = (AtomHdlr)atom;
                    return;
            }
        }
    }
}