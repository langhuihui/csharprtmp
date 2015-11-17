namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomMETA : VersionedBoxAtom
    {
        private AtomHdlr _atomHDLR;
        private AtomILST _atomILST;

        public AtomMETA(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public override void Read()
        {
            if (Parent?.Parent == null || Parent.Type != UDTA || Parent.Parent.Type!=MOOV)
            {
                SkipRead(false);
                return;
            }
           
            base.Read();
        }

        public override void ReadData()
        {
            
        }

        public override void AtomCreated(BaseAtom atom)
        {
            switch (atom.Type)
            {
                case HDLR:
                    _atomHDLR = (AtomHdlr) atom;
                    return;
                case ILST:
                    _atomILST = (AtomILST)atom;
                    return;
            }
        }
    }
}