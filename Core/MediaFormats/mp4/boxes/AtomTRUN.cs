using System.Collections.Generic;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public struct TRUNSample
    {
        public uint Duration;
        public uint Size;
        public uint Flags;
        public uint CompositionTimeOffset;
        public long AbsoluteOffset;
        public override string ToString() => $"duration:{Duration}; size:{Size}; flags:{Flags}; CTO:{CompositionTimeOffset}";
    }
    public class AtomTRUN:VersionedAtom
    {
        public AtomTRUN(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        private uint _firstSampleFlags;
        public bool HasDataOffset => (Flags & 0x01) != 0;
        public bool HasFirstSampleFlags=> (Flags & 0x04) != 0;
        public bool HasSampleDuration=> (Flags & 0x0100) != 0;
        public bool HasSampleSize => (Flags & 0x0200) != 0;
        public bool HasSampleFlags => (Flags & 0x0400) != 0;
        public bool HasSampleCompositionTimeOffsets => (Flags & 0x0800) != 0;
        public int DataOffset;
        public List<TRUNSample> Samples = new List<TRUNSample>(); 
        public override void ReadData()
        {
            var sampleCount = Br.ReadUInt32();
            if (HasDataOffset)
                DataOffset = Br.ReadInt32();
            if (HasFirstSampleFlags)
                _firstSampleFlags = Br.ReadUInt32();
            for (var i = 0; i < sampleCount; i++)
            {
                TRUNSample sample;
                sample.Duration = HasSampleDuration ? Br.ReadUInt32() : 0;
                sample.Size = HasSampleSize ? Br.ReadUInt32() : 0;
                sample.Flags = HasSampleFlags? Br.ReadUInt32():0;
                sample.CompositionTimeOffset = HasSampleCompositionTimeOffsets?Br.ReadUInt32():0;
                sample.AbsoluteOffset = 0;
                Samples.Add(sample);
            }
        }
    }
}