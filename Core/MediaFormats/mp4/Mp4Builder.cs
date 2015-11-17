using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpRTMP.Common;
using CSharpRTMP.Core.MediaFormats.mp4.boxes;
using CSharpRTMP.Core.Protocols.Rtmfp;
using static CSharpRTMP.Core.MediaFormats.mp4.boxes.BaseAtom;

namespace CSharpRTMP.Core.MediaFormats.mp4
{
    public interface IFragmenter
    {
        long[] SampleNumbers(ITrack track);
    }

    public interface ISample
    {
        long Size { get; }
        byte[] ToBytes();
        void WriteTo(Stream s);
    }
    public class Mp4Builder
    {
        public Dictionary<ITrack, AtomSTCO> ChunkOffsetBoxes = new Dictionary<ITrack, AtomSTCO>();
        public HashSet<AtomSAIO> SampleAuxiliaryInformationOffsetsBoxes = new HashSet<AtomSAIO>();
        public Dictionary<ITrack, List<ISample>> Track2Sample = new Dictionary<ITrack, List<ISample>>();
        public Dictionary<ITrack, long[]> Track2SampleSizes = new Dictionary<ITrack, long[]>();
        public IFragmenter Fragmenter;

        public MP4Document Build(Movie movie)
        {
            if (Fragmenter == null)
            {
                Fragmenter = new TimeBasedFragmenter();
            }
            Track2Sample = movie.Tracks.ToDictionary(x => x, y => y.Samples);
            Track2SampleSizes = movie.Tracks.ToDictionary(x => x, y => y.Samples.Select(x => x.Size).ToArray());
            var meta = Variant.Get();
            MP4Document doc = new MP4Document(meta);
            doc.AddAtom(CreateFileTypeBox(movie));
            var chunks = movie.Tracks.ToDictionary(x => x, GetChunkSizes);
            var moov = CreateMovieBox(movie, chunks);
            doc.AddAtom(moov);
            var contentSize = moov.GetPath(TRAK, MDIA, MINF, STBL, STSZ).OfType<AtomSTSZ>().Sum(x => x.SampleSize);
            var mdat = new AtomMDAT(movie, chunks, contentSize);
            doc.AddAtom(mdat);
            /*
       dataOffset is where the first sample starts. In this special mdat the samples always start
       at offset 16 so that we can use the same offset for large boxes and small boxes
        */
            uint dataOffset = mdat.DataOffset;
            foreach (var chunkOffsetBox in ChunkOffsetBoxes.Values)
            {
                for (var i = 0; i < chunkOffsetBox.Entries.Count; i++)
                {
                    chunkOffsetBox.Entries[i] += dataOffset;
                }
            }
            foreach (var saio in SampleAuxiliaryInformationOffsetsBoxes)
            {
                long offset = saio.Size; // the calculation is systematically wrong by 4, I don't want to debug why. Just a quick correction --san 14.May.13
                offset += 4 + 4 + 4 + 4 + 4 + 24;
                // size of all header we were missing otherwise (moov, trak, mdia, minf, stbl)
                object b = saio;
                do
                {
                    BaseAtom current = (BaseAtom)b;
                    b = current.Parent;
                    offset += ((IBoxContainer)b).SubAtoms.TakeWhile(box => box != current).Sum(box => box.Size);
                } while (b is BoxAtom);

                long[] saioOffsets = saio.Offsets;
                for (int i = 0; i < saioOffsets.Length; i++)
                {
                    saioOffsets[i] += offset;
                }
            }
            return doc;
        }
        private AtomMOOV CreateMovieBox(Movie movie, Dictionary<ITrack, int[]> chunks)
        {
            var movieBox = new AtomMOOV();
            var mvhd = new AtomMVHD
            {
                Matrix = movie.Matrix,
                TimeScale = GetTimescale(movie),
                NextTrackId = movie.Tracks.Max(x => x.TrackMetaData.TrackId) + 1
            };
            // find the next available trackId
            mvhd.Duration = movie.Tracks.Max(track => track.Edits?.Count > 0 ? (uint)(track.Edits.Sum(edit => edit.SegmentDuration) * mvhd.TimeScale) : track.Duration * mvhd.TimeScale / track.TrackMetaData.Timescale);
            movieBox.AddAtom(mvhd);
            movie.Tracks.ForEach(track=>movieBox.AddAtom(CreateTrackBox(track,movie,chunks)));
            // metadata here
            var udta = CreateUdta(movie);
            if (udta != null)
            {
                movieBox.AddAtom(udta);
            }
            return movieBox;
        }

        private BaseAtom CreateUdta(Movie movie)
        {
            return null;
        }

        private AtomTRAK CreateTrackBox(ITrack track, Movie movie, Dictionary<ITrack, int[]> chunks)
        {
            AtomTRAK trackBox = new AtomTRAK();
            var tkhd = new AtomTKHD
            {
                IsEnabled = true,
                IsInMovie = true,
                IsInPoster = true,
                IsInPreview = true,
                Matrix = track.TrackMetaData.Matrix,
                AlternateGroup = track.TrackMetaData.Group,
                CreationTime = track.CreationTime,
                Duration = track.Edits?.Count > 0
                    ? (uint) (track.Edits.Sum(x => x.SegmentDuration)*track.TrackMetaData.Timescale)
                    : track.Duration*GetTimescale(movie)/track.TrackMetaData.Timescale,
                    Width = (uint) track.TrackMetaData.Width,
                    Height = (uint) track.TrackMetaData.Height,
Layer = track.TrackMetaData.Layer,
                ModificationTime = DateTime.Now.SecondsFrom1904(),
TrackId = track.TrackMetaData.TrackId,Volume = track.TrackMetaData.Volume
            };
            trackBox.AddAtom(tkhd);
            trackBox.AddAtom(CreateEdts(track, movie));

            AtomMDIA mdia = new AtomMDIA();
            trackBox.AddAtom(mdia);
            AtomMdhd mdhd = new AtomMdhd
            {
                CreationTime = track.TrackMetaData.CreationTime.SecondsFrom1904(),
                Duration = track.Duration,
                TimeScale = track.TrackMetaData.Timescale,
                Language = track.TrackMetaData.Language
            };

            mdia.AddAtom(mdhd);
            AtomHdlr hdlr = new AtomHdlr();
            mdia.AddAtom(hdlr);
            hdlr.ComonentType = track.Handler;

            AtomMINF minf = new AtomMINF();
            switch (track.Handler)
            {
                case "vide":
                    minf.AddAtom(new AtomVMHD());
                    break;
                case "soun":
                    minf.AddAtom(new AtomSMHD());
                    break;
                case "text":
                case "sbtl":
                    minf.AddAtom(new AtomNMHD());
                    break;
                case "subt":
                    minf.AddAtom(new AtomSTHD());
                    break;
                case "hint":
                    minf.AddAtom(new AtomHMHD());
                    break;
            }

            // dinf: all these three boxes tell us is that the actual
            // data is in the current file and not somewhere external
            AtomDINF dinf = new AtomDINF();
            AtomDREF dref = new AtomDREF();
            AtomURL url = new AtomURL {Flags = 1};
            dref.AddAtom(url);
            minf.AddAtom(dinf);
            //

            var stbl = CreateStbl(track, movie, chunks);
            minf.AddAtom(stbl);
            mdia.AddAtom(minf);
            return trackBox;
        }

        private BaseAtom CreateStbl(ITrack track, Movie movie, Dictionary<ITrack, int[]> chunks)
        {
            AtomSTBL stbl = new AtomSTBL();
            CreateStsd(track, stbl);
            CreateStts(track, stbl);
            CreateCtts(track, stbl);
            CreateStss(track, stbl);
            CreateSdtp(track, stbl);
            CreateStsc(track, chunks, stbl);
            CreateStsz(track, stbl);
            CreateStco(track, movie, chunks, stbl);
            Dictionary<string,List<IGroupEntry>> groupEntryFamilies = new Dictionary<string, List<IGroupEntry>>();
            foreach (var sg in track.SampleGroups)
            {
                var type = sg.Key.Type;
                var groupEntries = groupEntryFamilies[type];
                if (groupEntries == null)
                {
                    groupEntries = new List<IGroupEntry>() ;
                    groupEntryFamilies.Add(type, groupEntries);
                }
                groupEntries.Add(sg.Key);
            }
            foreach (var sg in groupEntryFamilies)
            {
                var sgdb = new AtomSGPD();
                var type = sg.Key;
                sgdb.GroupEntries = sg.Value;
                var sbgp = new AtomSBGP {GroupingType = type};
                AtomSBGP.Entry last = null;
                for (int i = 0; i < track.Samples.Count; i++)
                {
                    var index = 0;
                    for (int j = 0; j < sg.Value.Count; j++)
                    {
                        var sampleNums = track.SampleGroups[sg.Value[j]];
                        if (sampleNums.Contains(i))
                        {
                            index = j + 1;
                        }
                    }
                    if (last == null || last.GroupDescriptionIndex != index)
                    {
                        last = new AtomSBGP.Entry(1, index);
                        sbgp.Entries.Add(last);
                    }
                    else
                    {
                        last.SampleCount++;
                    }
                }
                stbl.AddAtom(sgdb);
                stbl.AddAtom(sbgp);
            }
      

        //if (track is CencEncryptedTrack) {
        //    createCencBoxes((CencEncryptedTrack)track, stbl, chunks[track]);
        //}
        
            CreateSubs(track, stbl);
            return stbl;
        }

        //private void createCencBoxes(CencEncryptedTrack track, AtomSTBL stbl, int[] ints)
        //{
            
        //}

        class ChunksComparer : IComparer<ITrack>
        {
            public int Compare(ITrack x, ITrack y) => (int)(x.TrackMetaData.TrackId - y.TrackMetaData.TrackId);
        }
        private void CreateStco(ITrack targetTrack, Movie movie, Dictionary<ITrack, int[]> chunks, AtomSTBL stbl)
        {
            if (ChunkOffsetBoxes[targetTrack] == null)
            {
                // The ChunkOffsetBox we create here is just a stub
                // since we haven't created the whole structure we can't tell where the
                // first chunk starts (mdat box). So I just let the chunk offset
                // start at zero and I will add the mdat offset later.

                uint offset = 0;
                // all tracks have the same number of chunks
                //LOG.logDebug("Calculating chunk offsets for track_" + targetTrack.getTrackMetaData().getTrackId());

                List<ITrack> tracks = new List<ITrack>(chunks.Keys);
                tracks.Sort(new ChunksComparer());
                Dictionary<ITrack, int> trackToChunk = new Dictionary<ITrack, int>();
                Dictionary<ITrack, int> trackToSample = new Dictionary<ITrack, int>();
                Dictionary<ITrack, double> trackToTime = new Dictionary<ITrack, double>();
                foreach (ITrack track in tracks)
                {
                    trackToChunk.Add(track, 0);
                    trackToSample.Add(track, 0);
                    trackToTime.Add(track, 0.0);
                    ChunkOffsetBoxes.Add(track, new AtomSTCO());
                }

                while (true)
                {
                    ITrack nextChunksTrack = null;
                    foreach (ITrack track in tracks)
                    {
                        // This always chooses the least progressed track
                        if ((nextChunksTrack == null || trackToTime[track] < trackToTime[nextChunksTrack]) &&
                            // either first OR track's next chunk's starttime is smaller than nextTrack's next chunks starttime
                            // AND their need to be chunks left!
                            (trackToChunk[track] < chunks[track].Length))
                        {
                            nextChunksTrack = track;
                        }
                    }
                    if (nextChunksTrack == null)
                    {
                        break; // no next
                    }
                    // found the next one
                    AtomSTCO chunkOffsetBox = ChunkOffsetBoxes[nextChunksTrack];
                    chunkOffsetBox.Entries.Add(offset);
                    int nextChunksIndex = trackToChunk[nextChunksTrack];

                    int numberOfSampleInNextChunk = chunks[nextChunksTrack][nextChunksIndex];
                    int startSample = trackToSample[nextChunksTrack];
                    double time = trackToTime[nextChunksTrack];

                    var durs = nextChunksTrack.SampleDurations;
                    for (int j = startSample; j < startSample + numberOfSampleInNextChunk; j++)
                    {
                        offset += (uint)Track2SampleSizes[nextChunksTrack][j];
                        time += (double)durs[j] / nextChunksTrack.TrackMetaData.Timescale;
                    }
                    trackToChunk.Add(nextChunksTrack, nextChunksIndex + 1);
                    trackToSample.Add(nextChunksTrack, startSample + numberOfSampleInNextChunk);
                    trackToTime.Add(nextChunksTrack, time);
                }
            }
            stbl.AddAtom(ChunkOffsetBoxes[targetTrack]);
        }

        private void CreateStsz(ITrack track, AtomSTBL stbl)
        {
            AtomSTSZ stsz = new AtomSTSZ {Entries = Track2SampleSizes[track]};
            stbl.AddAtom(stsz);
        }

        private void CreateStsc(ITrack track, Dictionary<ITrack, int[]> chunks, AtomSTBL stbl)
        {
            var tracksChunkSizes = chunks[track];
            var stsc = new AtomSTSC(new List<AtomSTSC.Entry>());
            var lastChunkSize = long.MinValue; // to be sure the first chunks hasn't got the same size
            for (var i = 0; i < tracksChunkSizes.Length; i++)
            {
                // The sample description index references the sample description box
                // that describes the samples of this chunk. My Tracks cannot have more
                // than one sample description box. Therefore 1 is always right
                // the first chunk has the number '1'
                if (lastChunkSize != tracksChunkSizes[i])
                {
                    stsc.Entries.Add(new AtomSTSC.Entry()
                    {
                        FirstChunk = (uint) (i+1),SamplesPerChunk = (uint) tracksChunkSizes[i],SampleDescriptionIndex = 1
                    });
                    lastChunkSize = tracksChunkSizes[i];
                }
            }
            stbl.AddAtom(stsc);
        }

        private void CreateSubs(ITrack track, AtomSTBL stbl)
        {
            if (track.SubsampleInformationBox != null)
            {
                stbl.AddAtom(track.SubsampleInformationBox);
            }
        }

        private void CreateSdtp(ITrack track, AtomSTBL stbl)
        {
            if (track.SampleDependencies?.Count()>0)
            {
                AtomSDTP sdtp = new AtomSDTP(track.SampleDependencies);
                stbl.AddAtom(sdtp);
            }
        }

        private void CreateStss(ITrack track, AtomSTBL stbl)
        {
            if (track.SyncSamples?.Count()>0)
            {
                AtomSTSS stss = new AtomSTSS(track.SyncSamples);
                stbl.AddAtom(stss);
            }
        }

        private void CreateCtts(ITrack track, AtomSTBL stbl)
        {
            List<AtomCTTS.Entry> compositionTimeToSampleEntries = track.CompositionTimeEntries;
            if (compositionTimeToSampleEntries?.Count>0)
            {
                AtomCTTS ctts = new AtomCTTS(compositionTimeToSampleEntries);
                stbl.AddAtom(ctts);
            }
        }

        private void CreateStts(ITrack track, AtomSTBL stbl)
        {
            AtomSTTS.Entry lastEntry = null;
            AtomSTTS stts = new AtomSTTS();
            var entries = stts.SttsEntries;

            foreach (var delta in track.SampleDurations)
            {
                if (lastEntry != null && lastEntry.Delta == delta)
                {
                    lastEntry.Count++;
                }
                else
                {
                    lastEntry = new AtomSTTS.Entry
                    {
                        Count = 1,
                        Delta = delta
                    };
                    entries.Add(lastEntry);
                }

            }
            stbl.AddAtom(stts);
        }

        private void CreateStsd(ITrack track, AtomSTBL stbl) => stbl.AddAtom(track.SampleDescriptionBox);


        private AtomEDTS CreateEdts(ITrack track, Movie movie)
        {
            if (track.Edits?.Count>0)
            {
                AtomELST elst = new AtomELST {Version = 0};
                // quicktime won't play file when version = 1
                elst.Entries = track.Edits.Select(x => new AtomELST.Entry(elst,(long) System.Math.Round(x.SegmentDuration*movie.GetTimescale()) ,x.MediaTime*track.TrackMetaData.Timescale/x.TimeScale,x.MediaRate )).ToList();
                AtomEDTS edts = new AtomEDTS();
                edts.AddAtom(elst);
                return edts;
            }
            return null;
        }

        public uint GetTimescale(Movie movie) => movie.Tracks.Select(x=>x.TrackMetaData.Timescale).Aggregate(Math.Lcm);

        protected AtomFTYP CreateFileTypeBox(Movie movie) => new AtomFTYP(MP42, 0, MP42, ISOM);
        int[] GetChunkSizes(ITrack track)
        {
            long[] referenceChunkStarts = Fragmenter.SampleNumbers(track);
            return referenceChunkStarts.Select((v,i) =>
            {
                var end = referenceChunkStarts.Length == i + 1 ? track.Samples.Count : referenceChunkStarts[i + 1] - 1;
                return (int)(end - v - 1);
            }).ToArray();
        }
    }
}
