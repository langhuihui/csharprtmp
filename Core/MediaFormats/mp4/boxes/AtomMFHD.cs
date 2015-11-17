namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomMFHD:VersionedAtom
    {
        private uint _sequenceNumber;

        public AtomMFHD(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public override void ReadData()
        {
            _sequenceNumber = Br.ReadUInt32();
        }
    }
}