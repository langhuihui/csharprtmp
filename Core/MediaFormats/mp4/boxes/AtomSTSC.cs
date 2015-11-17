using System.Collections.Generic;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    
    public class AtomSTSC:VersionedAtom
    {
        public class Entry
        {
            public uint FirstChunk;
            public uint SamplesPerChunk;
            public uint SampleDescriptionIndex;
        }
        public readonly List<Entry> Entries;

        private List<int> _normalizedEntries; 
        public AtomSTSC(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
            Entries = new List<Entry>();
        }

        public AtomSTSC(List<Entry> entries) : base(STSC)
        {
            Entries = entries;
        }
        public List<int> GetEntries(int totalChunksCount)
        {
            if (_normalizedEntries != null)
            {
                return _normalizedEntries;
            }
            _normalizedEntries=new List<int>();
            var samplesPerChunk = new List<uint>();
            for (var i = 0; i < Entries.Count- 1; i++)
            {
                for (var j = 0; j < Entries[i + 1].FirstChunk - Entries[i].FirstChunk; j++)
                {
                    samplesPerChunk.Add(Entries[i].SamplesPerChunk);
                }
            }
            for (int i = 0; i < samplesPerChunk.Count; i++)
            {
                for (int j = 0; j < samplesPerChunk[(int) i]; j++)
                {
                    _normalizedEntries.Add(i);
                }
            }
            return _normalizedEntries;
        } 
        public override void ReadData()
        {
            var count = Br.ReadUInt32();
            if (count == 0) return;
            for (int i = 0; i < count; i++)
            {
                Entry entry = new Entry
                {
                    FirstChunk = Br.ReadUInt32(),
                    SamplesPerChunk = Br.ReadUInt32(),
                    SampleDescriptionIndex = Br.ReadUInt32()
                };
                Entries.Add(entry);
            }
        }
    }
}