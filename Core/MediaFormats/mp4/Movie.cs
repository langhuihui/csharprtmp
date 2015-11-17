using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRTMP.Core.MediaFormats.mp4
{
    public class Movie
    {
        public Matrix Matrix = Matrix.ROTATE_0;
        public List<ITrack> Tracks = new List<ITrack>(); 
        public uint GetNextTrackId() => Tracks.Any()?Tracks.Max(track => track.TrackMetaData.TrackId)+1:1;
        public ITrack GetTrackByTrackId(long trackId) => Tracks.FirstOrDefault(track => track.TrackMetaData.TrackId == trackId);
        public void AddTrack(ITrack nuTrack)
        {
            // do some checking
            // perhaps the movie needs to get longer!
            if (GetTrackByTrackId(nuTrack.TrackMetaData.TrackId) != null)
            {
                // We already have a track with that trackId. Create a new one
                nuTrack.TrackMetaData.TrackId = GetNextTrackId();
            }
            Tracks.Add(nuTrack);
        }
        public uint GetTimescale() => Tracks.Select(x=>x.TrackMetaData.Timescale).Aggregate((current, timescale) => Gcd(timescale, current));

        public static uint Gcd(uint a, uint b) => b == 0 ? a : Gcd(b, a % b);
    }
}
