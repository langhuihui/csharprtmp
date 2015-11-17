namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomSTBL:BoxAtom
    {
        private AtomSTSD _atomSTSD;
        private AtomSTTS _atomSTTS;
        private AtomSTSC _atomSTSC;
        private AtomSTSZ _atomSTSZ;
        private AtomSTCO _atomSTCO;
        private AtomSTSS _atomSTSS;
        private AtomCO64 _atomCO64;
        private AtomCTTS _atomCTTS;

        public AtomSTBL(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomSTBL() : base(STBL)
        {
            
        }
        public override void AtomCreated(BaseAtom atom)
        {
            switch (atom.Type)
            {
                case STSD:
                    _atomSTSD = (AtomSTSD) atom;
                    break;
                case STTS:
                    _atomSTTS = (AtomSTTS) atom;
                    break;
                case STSC:
                    _atomSTSC = (AtomSTSC) atom;
                    break;
                case STSZ:
                    _atomSTSZ = (AtomSTSZ) atom;
                    break;
                case STCO:
                    _atomSTCO = (AtomSTCO) atom;
                    break;
                case CO64:
                    _atomCO64 = (AtomCO64) atom;
                    break;
                case CTTS:
                    _atomCTTS = (AtomCTTS) atom;
                    break;
                case STSS:
                    _atomSTSS = (AtomSTSS) atom;
                    break;
            }
        }
    }
}