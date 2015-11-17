using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpRTMP.Core.MediaFormats.mp4.boxes;
using CSharpRTMP.Core.Protocols.Rtmfp;

namespace CSharpRTMP.Core.MediaFormats.mp4
{
    public interface ITrack
    {
        TrackMetaData TrackMetaData { get; set; }
        uint[] SyncSamples { get; set; }
        uint[] SampleDurations { get; set; }
        List<ISample> Samples { get; set; }
        List<Edit> Edits { get; set; }
        uint Duration { get; }
        uint CreationTime { get; set; }
        string Handler { get; set; }
        AtomSTSD SampleDescriptionBox { get; set; }
        List<AtomCTTS.Entry> CompositionTimeEntries { get; set; }
        List<AtomSDTP.Entry> SampleDependencies { get; set; }
        AtomSUBS SubsampleInformationBox { get; set; }
        Dictionary<IGroupEntry,long[]> SampleGroups { get; set; }
    }

    public class TrackMetaData
    {
        public String Language = "eng";
        public uint Timescale;
        public DateTime ModificationTime = new DateTime();
        public DateTime CreationTime = new DateTime();
        public Matrix Matrix = Matrix.ROTATE_0;
        public double Width;
        public double Height;
        public float Volume;
        public uint TrackId = 1; // zero is not allowed
        public ushort Group = 0;
        public ushort Layer;
    }

    internal abstract class AbstractTrack: ITrack
    {
        public string Name;
        public abstract TrackMetaData TrackMetaData { get; set; }
        public uint[] SyncSamples { get; set; }
        public uint[] SampleDurations { get; set; }
        public List<ISample> Samples { get; set; }
        public List<Edit> Edits { get; set; } = new List<Edit>();
        public uint Duration { get; }
        public uint CreationTime { get; set; }
        public string Handler { get; set; }
        public AtomSTSD SampleDescriptionBox { get; set; }
        public List<AtomCTTS.Entry> CompositionTimeEntries { get; set; }
        public List<AtomSDTP.Entry> SampleDependencies { get; set; }
        public AtomSUBS SubsampleInformationBox { get; set; }
        public Dictionary<IGroupEntry, long[]> SampleGroups { get; set; }
    }
}
