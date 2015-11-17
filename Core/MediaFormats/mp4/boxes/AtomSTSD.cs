namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomSTSD:VersionedBoxAtom
    {
        private AtomMP4A _atomMp4A;
        private AtomAVC1 _atomAvc1;
        public AtomSTSD(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
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
                case MP4A:
                    _atomMp4A = atom as AtomMP4A;
                    break;
                case AVC1:
                    _atomAvc1 = atom as AtomAVC1;
                    break;
            }
        }
    }
}