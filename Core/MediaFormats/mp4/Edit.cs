using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRTMP.Core.MediaFormats.mp4
{
    public class Edit
    {
        public uint TimeScale;
        public double SegmentDuration;
        public uint MediaTime;
        public double MediaRate;

        public Edit(uint mediaTime, uint timeScale, double mediaRate, double segmentDurationInMs)
        {
            MediaTime = mediaTime;
            TimeScale = timeScale;
            MediaRate = mediaRate;
            SegmentDuration = segmentDurationInMs;
        }
    }
}
