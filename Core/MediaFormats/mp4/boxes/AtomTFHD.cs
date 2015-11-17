namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomTFHD:VersionedAtom
    {
        private long _baseDataOffset;
        private int _sampleDescriptionIndex;
        private int _defaultSampleDuration
            ;

        private int _defaultSampleSize;
        private int _defaultSampleFlags;

        public AtomTFHD(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public int TrackId { get;private set; }
        public long BaseDataOffset { get;private set; }
        public bool HasBaseDataOffset => (Flags & 0x01) != 0;
        public bool HasDefaultSampleFlags=> (Flags & 0x20) != 0;
        public bool HasDefaultSampleDuration=> (Flags & 0x08) != 0;
        public bool HasDefaultSampleSize => (Flags & 0x10) != 0;
        public bool HasSampleDescriptionIndex => (Flags & 0x02) != 0;
        public bool DurationIsEmpty => (Flags & 0x010000) != 0;
        public override void ReadData()
        {
            TrackId = Br.ReadInt32();
            if (HasBaseDataOffset) _baseDataOffset = Br.ReadInt64();
            if (HasSampleDescriptionIndex) _sampleDescriptionIndex = Br.ReadInt32();
            if (HasDefaultSampleDuration) _defaultSampleDuration = Br.ReadInt32();
            if (HasDefaultSampleSize) _defaultSampleSize = Br.ReadInt32();
            if (HasDefaultSampleFlags) _defaultSampleFlags = Br.ReadInt32();
        }
    }
}