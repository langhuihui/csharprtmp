using System.Collections.Generic;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomSTSS:VersionedAtom
    {
        public List<uint> Entries ; 
        public AtomSTSS(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
            Entries = new List<uint>();
        }

        public AtomSTSS(uint[] syncSamples) : base(STSS)
        {
            Entries = new List<uint>(syncSamples);
        }

        public override void ReadData()
        {
            var count = Br.ReadUInt32();
            for (int i = 0; i < count; i++)
            {
                Entries.Add(Br.ReadUInt32());
            }
        }
    }
}