using System.Collections.Generic;
using System.Linq;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    

    public class AtomCTTS:VersionedAtom
    {
        public struct Entry
        {
            public uint SampleCount;
            public int SampleOffset;
        }

        public List<Entry> Entries;
        public List<int>  NormalizedEntries;
        public AtomCTTS(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
            Entries = new List<Entry>();
        }

        public AtomCTTS(List<Entry> compositionTimeToSampleEntries) : base(CTTS)
        {
            Entries = compositionTimeToSampleEntries;
        }

        public List<int> GetEntries()
        {
            if (NormalizedEntries!=null) return NormalizedEntries;
            NormalizedEntries = Entries.Select(x => x.SampleOffset).ToList();
            return NormalizedEntries;
        }
        public override void ReadData()
        {
            var count = Br.ReadUInt32();
            for (int i = 0; i < count; i++)
            {
                Entry entry;
                entry.SampleCount = Br.ReadUInt32();
                entry.SampleOffset = Br.ReadInt32();
                Entries.Add(entry);
            }
        }
    }
}