using System.Collections.Generic;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomSTSZ:VersionedAtom
    {
        public long[] Entries = new long[1];
        public uint SampleSize;
        private uint _sampleCount;
        public AtomSTSZ(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomSTSZ() : base(STSZ)
        {
            
        }
        public override void ReadData()
        {
            SampleSize = Br.ReadUInt32();
            _sampleCount = Br.ReadUInt32();
            if (SampleSize != 0)
            {
                Entries[0]= SampleSize;
            }
            else
            {
                for (var i = 0; i < _sampleCount; i++)
                {
                    Entries[i] = Br.ReadUInt32();
                }
            }
        }
    }
}