namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomMdhd: HeaderAtom
    {
        public uint TimeScale;
        public string Language;
        private ushort _quality;

        public AtomMdhd(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomMdhd() : base(MDHD)
        {
            
        }
        public override void ReadData()
        {
            if (Version == 1)
            {
                CreationTime = Br.ReadUInt64();
                ModificationTime = Br.ReadUInt64();
                TimeScale = Br.ReadUInt32();
                Duration = Br.ReadUInt64();
                Language = Br.ReadIso639();
                _quality = Br.ReadUInt16();
            }
            else
            {
                CreationTime = Br.ReadUInt32();
                ModificationTime = Br.ReadUInt32();
                TimeScale = Br.ReadUInt32();
                Duration = Br.ReadUInt32();
                Language = Br.ReadIso639();
                _quality = Br.ReadUInt16();
            }
        }
    }
}