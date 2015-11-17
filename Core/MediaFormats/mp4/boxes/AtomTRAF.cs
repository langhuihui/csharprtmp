using System.Collections.Generic;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomTRAF:BoxAtom
    {
        private AtomTFHD _atomTfhd;
        public List<AtomTRUN> Runs = new List<AtomTRUN>(); 
        public AtomTRAF(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public int Id => _atomTfhd?.TrackId??0;
        public override void AtomCreated(BaseAtom atom)
        {
            switch (atom.Type)
            {
                case TFHD:
                    _atomTfhd = (AtomTFHD) atom;
                    break;
                case TRUN:
                    Runs.Add(atom as AtomTRUN);
                    break;
            }
        }
    }
}