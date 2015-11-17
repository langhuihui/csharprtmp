using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    /// <summary>
    /// SampleToGroupBox
    /// </summary>
    public class AtomSBGP:VersionedAtom
    {
        public class Entry
        {
            public int GroupDescriptionIndex;
            public long SampleCount;
            public Entry(long sampleCount, int groupDescriptionIndex)
            {
                SampleCount = sampleCount;
                GroupDescriptionIndex = groupDescriptionIndex;
            }
        }
        public string GroupingType;
        public List<Entry> Entries = new List<Entry>();

        public AtomSBGP(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomSBGP() : base(SBGP)
        {
        }

        public override void ReadData()
        {
            throw new NotImplementedException();
        }
    }
}
