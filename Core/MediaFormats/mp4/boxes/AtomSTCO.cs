using System.Collections.Generic;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    /// <summary>
    /// StaticChunkOffsetBox
    /// </summary>
    public class AtomSTCO:VersionedAtom
    {
        public List<uint> Entries = new List<uint>();
        public AtomSTCO(MP4Document document, uint type, long size, long start) : base(document, STCO, size, start)
        {
        }

        public AtomSTCO() : base(STCO)
        {
            
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