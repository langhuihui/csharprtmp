using System.Collections.Generic;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomCO64:VersionedAtom
    {
        public List<ulong> Entries = new List<ulong>();

        public AtomCO64(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public override void ReadData()
        {
            var count = Br.ReadUInt32();
            for (int i = 0; i < count; i++)
            {
                Entries.Add(Br.ReadUInt64());
            }
        }
    }
}