namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomDINF:BoxAtom
    {
        private AtomDREF _atomDREF;

        public AtomDINF(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomDINF() : base(DINF)
        {
            
        }
        public override void AtomCreated(BaseAtom atom)
        {
            switch (atom.Type)
            {
                case DREF:
                    _atomDREF = (AtomDREF)atom;
                    break;
            }
        }
    }
}