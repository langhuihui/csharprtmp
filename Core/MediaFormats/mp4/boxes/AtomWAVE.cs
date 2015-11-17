namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomWAVE:BoxAtom
    {
        private AtomMP4A _atomMp4A;
        private AtomESDS _atomEsds;
        public AtomWAVE(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public override void AtomCreated(BaseAtom atom)
        {
            switch (atom.Type)
            {
                case MP4A:
                    _atomMp4A = atom as AtomMP4A;
                    return;
                case ESDS:
                    _atomEsds = atom as AtomESDS;
                    return;
                case NULL:
                    return;
                default:
                    return;
            }
        }
    }
}