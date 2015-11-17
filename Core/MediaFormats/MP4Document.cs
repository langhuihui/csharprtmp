using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.MediaFormats.mp4;
using CSharpRTMP.Core.MediaFormats.mp4.boxes;
using CSharpRTMP.Core.Streaming;
using Newtonsoft.Json.Linq;
using static CSharpRTMP.Common.Logger;
using static CSharpRTMP.Core.MediaFormats.mp4.boxes.BaseAtom;

namespace CSharpRTMP.Core.MediaFormats
{
    public class MP4Document : BaseMediaDocument, IBoxContainer
    {
        public List<BaseAtom> SubAtoms { get; } = new List<BaseAtom>();
        private AtomFTYP _atomFTYP;
        private AtomMOOV _atomMOOV;
        private readonly List<AtomMOOF> _moof = new List<AtomMOOF>();
        private List<BaseAtom> _topAtoms = new List<BaseAtom>();
        public IsoTypeReader Reader;
        public IsoTypeWriter Writer;
        public uint Type { get; } = 0xffffffff;
        public IBoxContainer Parent { get; set; }
        public MP4Document(Variant metaData): base(metaData)
        {
            MediaStream = new MemoryStream();
            Reader = new IsoTypeReader(MediaStream);
            Writer = new IsoTypeWriter(MediaStream);
        }

        public void AddAtom(BaseAtom atom)
        {
            atom.Document = this;
            atom.Parent = this;
            SubAtoms.Add(atom);
        }

        public void Write()
        {
            foreach (var baseAtom in SubAtoms)
            {
                baseAtom.Write();
            }
        }
        public BaseAtom ReadAtom(IBoxContainer parentAtom)
        {
            BaseAtom atom = null;
            uint type = 0;
            var currentPos = MediaFile.Position;
            long size = MediaFile.Br.ReadUInt32();
            if (size == 0)
            {
                atom = new AtomNULL(this, type, size, currentPos) {Parent = parentAtom};
                return atom;
            }
            type = MediaFile.Br.ReadUInt32();
            if (size == 1)
            {
                size = MediaFile.Br.ReadInt64();
                if (size == 0)
                {
                    atom = new AtomNULL(this, type, size, currentPos) { Parent = parentAtom };
                    return atom;
                }
            }
            switch (type)
            {
                case FTYP:
                    atom = new AtomFTYP(this, size, currentPos);
                    break;
                case MOOV:
                    atom = new AtomMOOV(this, type, size, currentPos);
                    break;
                case MOOF:
                    atom = new AtomMOOF(this, type, size, currentPos);
                    break;
                case MVEX:
                    atom = new AtomMVEX(this, type, size, currentPos);
                    break;
                case MVHD:
                    atom = new AtomMVHD(this, type, size, currentPos);
                    break;
                case MFHD:
                    atom = new AtomMFHD(this, type, size, currentPos);
                    break;
                case TRAK:
                    atom = new AtomTRAK(this, type, size, currentPos);
                    break;
                case TRAF:
                    atom = new AtomTRAF(this, type, size, currentPos);
                    break;
                case TREX:
                    atom = new AtomTREX(this, type, size, currentPos);
                    break;
                case TRUN:
                    atom = new AtomTRUN(this, type, size, currentPos);
                    break;
                case TKHD:
                    atom = new AtomTKHD(this, type, size, currentPos);
                    break;
                case TFHD:
                    atom = new AtomTFHD(this, type, size, currentPos);
                    break;
                case MDIA:
                    atom = new AtomMDIA(this, type, size, currentPos);
                    break;
                case MDHD:
                    atom = new AtomMdhd(this, type, size, currentPos);
                    break;
                case HDLR:
                    atom = new AtomHdlr(this, type, size, currentPos);
                    break;
                case MINF:
                    atom = new AtomMINF(this, type, size, currentPos);
                    break;
                case SMHD:
                    atom = new AtomSMHD(this, type, size, currentPos);
                    break;
                case DINF:
                    atom = new AtomDINF(this, type, size, currentPos);
                    break;
                case STBL:
                    atom = new AtomSTBL(this, type, size, currentPos);
                    break;
                case VMHD:
                    atom = new AtomVMHD(this, type, size, currentPos);
                    break;
                case DREF:
                    atom = new AtomDREF(this, type, size, currentPos);
                    break;
                case STSD:
                    atom = new AtomSTSD(this, type, size, currentPos);
                    break;
                case STTS:
                    atom = new AtomSTTS(this, type, size, currentPos);
                    break;
                case STSC:
                    atom = new AtomSTSC(this, type, size, currentPos);
                    break;
                case STSZ:
                    atom = new AtomSTSZ(this, type, size, currentPos);
                    break;
                case STCO:
                    atom = new AtomSTCO(this, type, size, currentPos);
                    break;
                case CTTS:
                    atom = new AtomCTTS(this, type, size, currentPos);
                    break;
                case STSS:
                    atom = new AtomSTSS(this, type, size, currentPos);
                    break;
                case URL:
                    atom = new AtomURL(this, type, size, currentPos);
                    break;
                case MP4A:
                    atom = new AtomMP4A(this, type, size, currentPos);
                    break;
                case AVC1:
                    atom = new AtomAVC1(this, type, size, currentPos);
                    break;
                case ESDS:
                    atom = new AtomESDS(this, type, size, currentPos);
                    break;
                case AVCC:
                    atom = new AtomAVCC(this, type, size, currentPos);
                    break;
                case UDTA:
                    atom = new AtomUDTA(this, type, size, currentPos);
                    break;
                case WAVE:
                    atom = new AtomWAVE(this, type, size, currentPos);
                    break;
                case META:
                    atom = new AtomMETA(this, type, size, currentPos);
                    break;
                case NULL:
                    atom = new AtomNULL(this, type, size, currentPos);
                    break;
                case ILST:
                    atom = new AtomILST(this, type, size, currentPos);
                    break;
                case DATA:
                    atom = new AtomDATA(this, type, size, currentPos);
                    break;
                case CO64:
                    atom = new AtomCO64(this, type, size, currentPos);
                    break;
                case _COM:
                case NAME:
                case COVR:
                case AART:
                case _WRT:
                case _GRP:
                case _LYR:
                case _NAM:
                case _ART1:
                case _ART2:
                case _PRT:
                case _TOO:
                case _DAY:
                case _CMT:
                case _CPY:
                case _DES:
                case _ALB:
                case TRKN:
                case CPIL:
                case PGAP:
                case TMPO:
                case GNRE:
                case DISK:
                case _GEN:
                case DESC:
                case TVSH:
                case TVEN:
                case TVSN:
                case TVES:
                    atom = new AtomMetaField(this, type, size, currentPos);
                    break;
                default:
                    {
                        atom = new IgnoredAtom(this, type, size, currentPos);
                        break;
                    }   
            }
            atom.Parent = parentAtom;
            atom.Read();
            if (currentPos + atom.Size != MediaFile.Position)
            {
                FATAL("atom start:{0};Atom Size:{1};currentPostion:{2}",currentPos,atom.Size,MediaFile.Position);
            }
            return atom;
        }

        protected override bool ParseDocument()
        {
            if (!MediaFile.SeekBegin())
            {
                FATAL("Unable to seek to the beginning of file");
                return false;
            }
            while (MediaFile.DataStream.CanRead)
            {
                if (MediaFile.Position == MediaFile.DataStream.Length)
                {
                    return true;
                }
                var atom = ReadAtom(null);
                if (atom == null)
                {
                    FATAL("Unable to read atom");
                    return false;
                }
                if (!atom.IsIgnored)
                {
                    switch (atom.Type)
                    {
                        case FTYP:
                            _atomFTYP = (AtomFTYP) atom;
                            break;
                        case MOOV:
                            _atomMOOV = (AtomMOOV) atom;
                            break;
                        case MOOF:
                            _moof.Add((AtomMOOF)atom);
                            break;

                        default:
                            return false;
                    }
                }
                _topAtoms.Add(atom);
            }
            return true;
        }

        protected override bool BuildFrames()
        {
            _frames.Clear();
            AtomTRAK track;
            AtomAVCC avcc = null;
            if (null!= (track = GetTRAK(false)) )
            {
                avcc = (AtomAVCC)track.GetPath(6,MDIA,MINF,
				STBL, STSD, AVC1, AVCC);
            }
            AtomESDS esds = null;
            if (null!= (track = GetTRAK(true)) )
            {
                esds = (AtomESDS)track.GetPath(6, MDIA, MINF,
                    STBL, STSD, MP4A, ESDS) ?? (AtomESDS)track.GetPath(7, MDIA, MINF,
                        STBL, STSD, MP4A, WAVE, ESDS);
            }
            if (avcc != null)
            {
                if (!BuildMOOVFrames(false)) return false;
            }
            foreach (var moof in _moof)
            {
                if (!BuildMOOFFrames(moof, true)) {
                    FATAL("Unable to build audio frames from MOOF");
                    return false;
                }
                if (!BuildMOOFFrames(moof, false)) {
                    FATAL("Unable to build video frames from MOOF");
                    return false;
                }
            }
            _frames.Sort(CompareFrames);
            if (esds != null)
            {
                MediaFrame audioHeader;
                audioHeader.Type = MediaFrameType.Audio;
                audioHeader.IsBinaryHeader = true;
                audioHeader.IsKeyFrame = true;
                audioHeader.Length = esds.ExtraDataLength;
                audioHeader.AbsoluteTime = 0;
                audioHeader.Start = (uint) esds.ExtraDataStart;
                audioHeader.DeltaTime = 0;
                audioHeader.CompositionOffset = 0;
                MediaFile.SeekTo(audioHeader.Start);
                var raw = MediaFile.Br.ReadBytes((int) audioHeader.Length);
                _streamCapabilities.InitAudioAAC(new MemoryStream(raw), raw.Length);
                 _frames.Add(audioHeader);
            }
            else
            {
                if ((track = GetTRAK(true)) != null)
                {
                    var mp3 = track.GetPath(5, MDIA, MINF,
                        STBL, STSD, MP3);
                    if (mp3 != null)
                    {
                        _streamCapabilities.AudioCodecId = AudioCodec.Mp3;
                    }
                }
            }
            if (avcc != null)
            {
                MediaFrame videoHeader;
                videoHeader.Type = MediaFrameType.Video;
                videoHeader.IsBinaryHeader = true;
                videoHeader.IsKeyFrame = true;
                videoHeader.Length = (uint) avcc.ExtraDataLength;
                videoHeader.AbsoluteTime = 0;
                videoHeader.Start = (uint) avcc.ExtraDataStart;
                videoHeader.DeltaTime = 0;
                videoHeader.CompositionOffset = 0;
                 MediaFile.SeekTo(videoHeader.Start);
                var raw = MediaFile.Br.ReadBytes((int) videoHeader.Length);
                if (raw.Length < 8)
                {
                    FATAL("Invalid AVC codec bytes");
                    return false;
                }
                var spsLength = raw.ReadUShort( 6);
                var ppsLength =  raw.ReadUShort(8+spsLength+1);
                var psps = new byte[spsLength];
                Buffer.BlockCopy(raw,8,psps,0,(int) spsLength);
                var pps = new byte[ppsLength];
                Buffer.BlockCopy(raw, 8 + spsLength + 3,psps,0,ppsLength);
                _streamCapabilities.InitVideoH264(psps,pps);
                 _frames.Add(videoHeader);
            }
           
            return true;
        }

        private bool BuildMOOFFrames(AtomMOOF pMOOF, bool audio)
        {
            var pTraf = GetTRAF(pMOOF, audio);
	        if (pTraf == null) {
		        WARN("No {0} fragmented track found", audio ? "audio" : "video");
		        return true;
	        }
            var pTfhd = (AtomTFHD ) pTraf.GetPath(1, TFHD);
	        if (pTfhd == null) {
		        FATAL("Invalid track. No TFHD atom");
		        return false;
	        }
            var pTrack = GetTRAK(audio);
	        if (pTrack == null) {
		        FATAL("no {0} track", audio ? "Audio" : "Video");
		        return false;
	        }
	        var pMDHD = (AtomMdhd ) pTrack.GetPath(2, MDIA, MDHD);
	        if (pMDHD == null) {
		        FATAL("no MDHD");
		        return false;
	        }
            var timeScale = pMDHD.TimeScale;
	        ulong totalTime = 0;
            var absoluteOffset = pTfhd.BaseDataOffset;

            var runs = pTraf.Runs;
            foreach (var pRun in runs)
            {
                var samples = pRun.Samples;
                ulong runSize = 0;
                foreach (var pSample in samples)
                {
                    MediaFrame frame;

                    frame.Start = (uint) (absoluteOffset + pRun.DataOffset + (long) runSize);
                    if (pSample.CompositionTimeOffset != 0) {
                        var doubleVal = (pSample.CompositionTimeOffset / (double) timeScale)*1000.00;
                        frame.CompositionOffset = (int) doubleVal;
                    } else {
                        frame.CompositionOffset = 0;
                    }

                    if (!audio) {
                        frame.IsKeyFrame = ((pSample.Flags & 0x00010000) == 0);
                    } else {
                        frame.IsKeyFrame = false;
                    }
                    frame.Length = pSample.Size;
                    frame.Type = audio ? MediaFrameType.Audio : MediaFrameType.Video;
                    frame.DeltaTime = (pSample.Duration / (double) timeScale)*1000.00;
                    frame.AbsoluteTime = (uint) (( totalTime / timeScale)* 1000);
                    frame.IsBinaryHeader = false;
                    totalTime += pSample.Duration;
                    _frames.Add(frame);
                    runSize += pSample.Size;
                }
            }
            return true;
        }

        private AtomTRAF GetTRAF(AtomMOOF pMoof, bool audio)
        {
             var pTrak = GetTRAK(audio);
	        if (pTrak == null) {
		        FATAL("No track found");
		        return null;
	        }
	        var trackId = (int) pTrak.Id;
	        if (trackId == 0) {
		        FATAL("No track found");
		        return null;
	        }

            var trafs = pMoof.Trafs;
            if (trafs.ContainsKey(trackId)) return trafs[trackId];
            FATAL("No track found");
            return null;
        }

        private bool BuildMOOVFrames(bool audio)
        {
            var track = GetTRAK(audio);
            if (track == null)
            {
                FATAL("no track");
                return false;
            }
            var stsz = (AtomSTSZ) track.GetPath(4, MDIA, MINF, STBL,
                STSZ);
            if (stsz == null)
            {
                FATAL("no STSZ");
                return false;
            }
            AtomCO64 co64 = null;
            var stco = (AtomSTCO) track.GetPath(4, MDIA, MINF, STBL,
                STCO);
            if (stco == null)
            {
                co64 = (AtomCO64)track.GetPath(4, MDIA, MINF, STBL, CO64);
                if (co64 == null)
                {
                    FATAL("no CO64");
                    return false;
                }
            }
            
	        //4. Get the atom containing the distribution of samples per corresponding
	        //chunks
	        var pSTSC = (AtomSTSC ) track.GetPath(4, MDIA, MINF, STBL,
			        STSC);
	        if (pSTSC == null) {
		        FATAL("no STSC");
		        return false;
	        }
            	//5. Get the atom containing the delta time of each sample
	        var pSTTS = (AtomSTTS )  track.GetPath(4, MDIA, MINF, STBL,
			        STTS);
	        if (pSTTS == null) {
		        FATAL("no STTS");
		        return false;
	        }

	        //6. Get the atom containing the time scale of each delta time
	        var pMDHD = (AtomMdhd )  track.GetPath(2, MDIA, MDHD);
	        if (pMDHD == null) {
		        FATAL("no MDHD");
		        return false;
	        }
                    //7. Get the table containing the samples marked as key frames. It can be null
	        //It can be null
	        var pSTSS = (AtomSTSS ) track.GetPath(4, MDIA, MINF, STBL,
			        STSS);

	        //8. Get the composition timestamps
	        var pCTSS = (AtomCTTS ) track.GetPath(4, MDIA, MINF, STBL,
			CTTS);
            var sampleSize = stsz.Entries;
            if (audio)
            {
                _audioSamplesCount = (uint) sampleSize.Length;
            }else _videoSamplesCount =(uint) sampleSize.Length;
            var sampleDeltaTime = pSTTS.Entries;
            var chunckOffsets = stco?.Entries.Cast<ulong>().ToList() ?? co64.Entries;
            var sample2Chunk = pSTSC.GetEntries( chunckOffsets.Count);
            List<uint> keyFrames = null;
            if (pSTSS != null)
            {
                keyFrames = pSTSS.Entries;
            }
            List<int> compositionOffsets = null;
            if (pCTSS != null)
            {
                compositionOffsets = pCTSS.GetEntries();
                if (sampleSize.Length == compositionOffsets.Count)
                {
                    for (var i = compositionOffsets.Count; i < sampleSize.Length; i++)
                    {
                        compositionOffsets.Add(0);
                    }
                }
            }
            var timeScale = pMDHD.TimeScale;
            ulong totalTime = 0;
            uint localOffset = 0;
            var startIndex =  _frames.Count;
            for (var i = 0; i < sampleSize.Length; i++)
            {
                MediaFrame frame;
                frame.Start = (uint) (chunckOffsets[sample2Chunk[i]] + localOffset);
                if (pSTSS != null)
                {
                    var doubleVal = ((double) compositionOffsets[i]/(double) timeScale)*(double) 1000.00;
                    frame.CompositionOffset = (int) doubleVal;
                }
                else
                {
                    frame.CompositionOffset = 0;
                }
                if (i <= sampleSize.Length - 2)
                {
                    //not the last frame
			var currentChunck = sample2Chunk[i];
			var nextChunck = sample2Chunk[i + 1];


			if (currentChunck == nextChunck) {
				//not changing the chunk
				localOffset += (uint) sampleSize[i];
			} else {
				//changing the chunck
				localOffset = 0;
			}
                }
                else
                {
                    localOffset += (uint)sampleSize[i];
                }
                frame.Length =  (uint)sampleSize[i];
                frame.Type = audio ? MediaFrameType.Audio : MediaFrameType.Video;
                frame.IsKeyFrame = pSTSS == null;
                frame.DeltaTime= (sampleDeltaTime[i] / (double) timeScale)*1000.00;
                frame.AbsoluteTime= (uint) ((totalTime /  timeScale)*1000);
                frame.IsBinaryHeader = false;
                totalTime += sampleDeltaTime[i];
                _frames.Add(frame);
            }
            if (pSTSS != null)
            foreach (var keyFrame in keyFrames)
            {
                var x = _frames[(int) (startIndex + keyFrame - 1)];
                x.IsKeyFrame = true;
                _frames[(int) (startIndex + keyFrame - 1)] = x;
            }
            return true;
        }

        protected override Variant GetRTMPMeta()
        {
           var result = new Variant();

	        var pVideoTrack = GetTRAK(false);
            AtomTKHD pTKHD = (AtomTKHD) pVideoTrack?.GetPath(1, TKHD);
            if (pTKHD != null) {
                result["width"] = pTKHD.Width;
                result["height"] = pTKHD.Height;
            }

            if (_atomMOOV != null) {
		        var pILST = (AtomILST) _atomMOOV.GetPath(3, UDTA, META, ILST);

		        if (pILST != null) {
			        result["tags"] = pILST.Variant;
		        } else {
			        WARN("No ilst atom present");
		        }
	        }

	         return result;
        }

        private AtomTRAK GetTRAK(bool audio)
        {
            if (_atomMOOV == null) {
		        FATAL("Unable to find moov");
		        return null;
	        }
                    var tracks = _atomMOOV.Tracks;
	        if (tracks.Count== 0) {
		        FATAL("No tracks defined");
		        return null;
	        }
	        foreach (var t in tracks)
	        {
	            var pHDLR = (AtomHdlr) t.GetPath(2, MDIA, HDLR);
	            if (audio && (pHDLR.ComponentSubType == SOUN))
	                return t;
	            if ((!audio) && (pHDLR.ComponentSubType== VIDE))
	                return t;
	        }
	        return null;
        }
    }
}
