using System.Collections.Generic;
using System.Linq;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomSTTS:VersionedAtom
    {
        public class Entry
        {
            public uint Count;
            public uint Delta;
        }
        public List<Entry> SttsEntries = new List<Entry>(); 
        public AtomSTTS(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomSTTS() : base(STTS)
        {
            
        }
        public List<uint> Entries => SttsEntries.Select(x => x.Delta).ToList(); 
        public override void ReadData()
        {
            var entryCount = Br.ReadUInt32();
            for (int i = 0; i < entryCount; i++)
            {
                SttsEntries.Add(new Entry()
                {
                    Count = Br.ReadUInt32(),
                    Delta = Br.ReadUInt32()
                });
            }
        }
    }
}