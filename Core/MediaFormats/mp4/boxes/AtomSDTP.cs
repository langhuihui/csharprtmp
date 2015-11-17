using System.Collections.Generic;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomSDTP:VersionedAtom
    {
        public class Entry
        {
            public  Entry(int value)
            {
                Value = value;
            }
            public int Value;
            public int IsLeading
            {
                get{ return (Value >> 6) & 0x03;}
                set { Value = (value & 0x03) << 6 | Value & 0x3f; }
            }

            public int SampleDependsOn
            {
                get { return (Value >> 4) & 0x03; }
                set { Value = (value & 0x03) << 4 | Value & 0xcf; }
            }

            public int SampleIsDependentOn
            {
               get { return (Value >> 2) & 0x03; }
                set { Value = (value & 0x03) << 2 | Value & 0xf3; }
            }

            public int SampleHasRedundancy
            {
                get { return Value & 0x03; }
                set { Value = value & 0x03 | Value & 0xfc; }
            }
        }

        public List<Entry> Entries;
        
        public AtomSDTP(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomSDTP(List<Entry> sampleDependencies) : base(SDTP)
        {
            Entries = sampleDependencies;
        }

        public override void ReadData()
        {
            while (Br.BaseStream.GetAvaliableByteCounts() > 0)
                Entries.Add(new Entry(Br.ReadByte()));
        }

        public override void Write()
        {
            base.Write();
            foreach (var entry in Entries)
            {
                Wr.Write((byte)entry.Value);
            }
        }
    }
}
