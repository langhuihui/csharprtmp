using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRTMP.Core.MediaFormats.mp4
{
    class TimeBasedFragmenter:IFragmenter
    {
        private readonly double _fragmentLength;

        public TimeBasedFragmenter(double fragmentLength = 2)
        {
            _fragmentLength = fragmentLength;
        }
        public long[] SampleNumbers(ITrack track)
        {
            var segmentStartSamples =new List<long>() {1};
            var sampleDurations = track.SampleDurations;
            var syncSamples = track.SyncSamples;
            long timescale = track.TrackMetaData.Timescale;
            double time = 0;
            for (uint i = 0; i < sampleDurations.Length; i++)
            {
                time += (double)sampleDurations[i] / timescale;
                if (time >= _fragmentLength &&
                        (syncSamples == null || syncSamples.Contains(i+1)))
                {
                    if (i > 0)
                    {
                        segmentStartSamples.Add(i+1);
                    }
                    time = 0;
                }
            }
            return segmentStartSamples.ToArray();
        }
    }
}
