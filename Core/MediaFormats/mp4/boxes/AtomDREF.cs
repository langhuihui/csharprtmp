using System.Collections.Generic;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomDREF:VersionedBoxAtom
    {
        private List<AtomURL> _urls= new List<AtomURL>(); 
        public AtomDREF(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomDREF() : base(DREF)
        {
            
        }
        public override void ReadData()
        {
            Br.ReadUInt32();
        }

        public override void AtomCreated(BaseAtom atom)
        {
            switch (atom.Type)
            {
                case URL:
                    _urls.Add(atom as AtomURL);
                    break;
            }
        }
    }
}