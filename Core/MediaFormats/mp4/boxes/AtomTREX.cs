namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomTREX:VersionedAtom
    {
        public uint TrackID { get;private set; }
        public uint DefaultSampleDescriptionIndex { get; private set; }
        public uint DefaultSampleDuration { get; private set; }
        public uint DefaultSampleSize { get; private set; }
        public uint DefaultSampleFlags { get; private set; }
        public AtomTREX(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public override void ReadData()
        {
            TrackID = Br.ReadUInt32();
            DefaultSampleDescriptionIndex = Br.ReadUInt32();
            DefaultSampleDuration = Br.ReadUInt32();
            DefaultSampleSize = Br.ReadUInt32();
            DefaultSampleFlags = Br.ReadUInt32();
        }
    }
}