namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomSMHD : VersionedAtom
    {
        private ushort _balance;
        private ushort _reserved;

        public AtomSMHD(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomSMHD() : base(SMHD)
        {
            
        }
        public override void ReadData()
        {
            _balance = Br.ReadUInt16();
            _reserved = Br.ReadUInt16();
        }
    }
}