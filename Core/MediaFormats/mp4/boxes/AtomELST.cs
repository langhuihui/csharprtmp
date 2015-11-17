using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomELST:VersionedAtom
    {
        public List<Entry> Entries;

        public AtomELST(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomELST() : base(ELTS)
        {
        }

        public override void ReadData()
        {
            throw new NotImplementedException();
        }

        public class Entry
        {
            public AtomELST EditListBox;
            public long SegmentDuration;
            public long MediaTime;
            public double MediaRate;

            public Entry(AtomELST editListBox ,long segmentDuration, long mediaTime, double mediaRate)
            {
                this.EditListBox = editListBox;
                SegmentDuration = segmentDuration;
                MediaTime = mediaTime;
                MediaRate = mediaRate;
            }

            public Entry(AtomELST editListBox, IsoTypeReader itr)
            {
                if (editListBox.Version== 1)
                {
                    SegmentDuration = itr.ReadInt64();
                    MediaTime = itr.ReadInt64();
                }
                else
                {
                    SegmentDuration = itr.ReadUInt32();
                    MediaTime = itr.ReadUInt32();
                }
                MediaRate = itr.ReadFixedPoint1616();
                this.EditListBox = editListBox;
            }
        }
    }
}
