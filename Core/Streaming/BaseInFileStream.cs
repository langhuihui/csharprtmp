using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CSharpRTMP.Common;
using CSharpRTMP.Core.MediaFormats;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Timer;
using Newtonsoft.Json.Linq;
using static CSharpRTMP.Common.Defines;

namespace CSharpRTMP.Core.Streaming
{
    
    public abstract class BaseInFileStream : BaseInStream<BaseProtocol>
    {
        private InFileStreamTimer _pTimer;
        protected MediaFile _pSeekFile, _pFile;
        protected uint _totalFrames, _currentFrameIndex;
        protected uint _totalSentTime, _totalSentTimeBase;
        protected MediaFrame _currentFrame;
        protected DateTime _startFeedingTime;
        protected int _clientSideBufferLength;
        protected MemoryStream _videoBuffer = Utils.Rms.GetStream();
        protected MemoryStream _audioBuffer = Utils.Rms.GetStream();
        protected bool Paused, _audioVideoCodecsSent;
        protected long _seekBaseOffset, _framesBaseOffset, _timeToIndexOffset;
        public override StreamCapabilities Capabilities { get; }= new StreamCapabilities();

#if HAS_MMAP
#else
        private static readonly Dictionary<string, MediaFile> FileCache = new Dictionary<string, MediaFile>();
#endif
        private double _playLimit;
        protected BaseInFileStream(BaseProtocol pProtocol, StreamsManager pStreamsManager, string name)
            : base(pProtocol, pStreamsManager, name)
        {
            if (!Type.TagKindOf(StreamTypes.ST_IN_FILE))
            {
                Logger.ASSERT("Incorrect stream type. Wanted a stream type in class {0} and got {1}", StreamTypes.ST_IN_FILE.TagToString(), Type.TagToString());
            }
            Paused = true;
            _audioVideoCodecsSent = false;
            Capabilities.Clear();
            _playLimit = -1;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (_pTimer != null)
            {
                _pTimer.ResetStream();
                _pTimer.EnqueueForDelete();
                _pTimer = null;
            }
            _audioBuffer.Dispose();
            _videoBuffer.Dispose();
            _pSeekFile.Dispose();
            _pFile.Dispose();
//            ReleaseFile(_pSeekFile);
  //          ReleaseFile(_pFile);
        }

        

        public static bool ResolveCompleteMetadata(ref Variant metaData)
        {
            if (metaData[CONF_APPLICATION_EXTERNSEEKGENERATOR]) return false;
            BaseMediaDocument pDocument;
            if (false)
            {

            }
#if HAS_MEDIA_FLV
            else if ((string)metaData[META_MEDIA_TYPE] == MEDIA_TYPE_FLV ||
            (string)metaData[META_MEDIA_TYPE] == MEDIA_TYPE_LIVE_OR_FLV)
            {
                pDocument = new FLVDocument(metaData);
            }
#endif
#if HAS_MEDIA_MP3
            else if ((string)metaData[META_MEDIA_TYPE] == MEDIA_TYPE_MP3)
            {
                pDocument = new MP3Document(metaData);
            }
#endif
#if HAS_MEDIA_MP4
            else if ((string)metaData[META_MEDIA_TYPE] == MEDIA_TYPE_MP4
            || (string)metaData[META_MEDIA_TYPE] == MEDIA_TYPE_M4A
            || (string)metaData[META_MEDIA_TYPE] == MEDIA_TYPE_M4V
            || (string)metaData[META_MEDIA_TYPE] == MEDIA_TYPE_MOV
            || (string)metaData[META_MEDIA_TYPE] == MEDIA_TYPE_F4V)
            {
                pDocument = new MP4Document(metaData);
            }
#endif
#if HAS_MEDIA_NSV
	else if (metaData[Defines.META_MEDIA_TYPE] == Defines.MEDIA_TYPE_NSV) {
		pDocument = new NSVDocument(metaData);
	}
#endif
            else
            {
                Logger.FATAL("File type not supported yet. Partial metadata:\n{0}",
                        metaData.ToString());
                return false;
            }
            //2. Process the document
            metaData.Log().Info("Processing file {0}", metaData[META_SERVER_FULL_PATH]);
            if (!pDocument.Process())
            {
                Logger.FATAL("Unable to process document");
                //pDocument.Dispose();
                if (metaData[CONF_APPLICATION_RENAMEBADFILES])
                {
                    File.Move((string) metaData[META_SERVER_FULL_PATH],
                            metaData[META_SERVER_FULL_PATH] + ".bad");
                }
                else
                {
                    Logger.WARN("File {0} will not be renamed",
                            metaData[META_SERVER_FULL_PATH]);
                }
                return false;
            }
            //3. Get the medatada
            metaData = pDocument.MetaData;
            return true;
        }

        public virtual bool Initialize(int clientSideBufferLength)
        {
            //1. Check to see if we have an universal seeking file
            var seekFilePath = Name + "." + MEDIA_TYPE_SEEK;
            if (!File.Exists(seekFilePath))
            {
                var meta =  Variant.GetMap(new VariantMapHelper {{ META_SERVER_FULL_PATH, Name}});
                if (!ResolveCompleteMetadata(ref meta))
                {
                    Logger.FATAL("Unable to generate metadata");
                    return false;
                }
            }
            //2. Open the seek file
            _pSeekFile = MediaFile.Initialize(seekFilePath);
            if (_pSeekFile == null)
            {
                Logger.FATAL("Unable to open seeking file {0}", seekFilePath);
                return false;
            }

            //3. read stream capabilities
            var streamCapabilitiesSize = _pSeekFile.Br.ReadUInt32();
            //var raw = new MemoryStream();
            //_pSeekFile.CopyPartTo(raw, (int)streamCapabilitiesSize);
           
            if (streamCapabilitiesSize<14||!StreamCapabilities.Deserialize(_pSeekFile.DataStream, Capabilities))
            {
                Logger.FATAL("Unable to deserialize stream Capabilities. Please delete {0} and {1} files so they can be regenerated",
                        Name + "." + MEDIA_TYPE_SEEK,
                        Name + "." + MEDIA_TYPE_META);
                return false;
            }
            //4. compute offsets
            _seekBaseOffset = _pSeekFile.Position;
            _framesBaseOffset = _seekBaseOffset + 4;
            //5. Compute the optimal window size by reading the biggest frame size
            //from the seek file.
            if (!_pSeekFile.SeekTo(_pSeekFile.Length - 8))
            {
                Logger.FATAL("Unable to seek to {0} position", _pSeekFile.Position - 8);
                return false;
            }
            ulong maxFrameSize = _pSeekFile.Br.ReadUInt64();
            if (!_pSeekFile.SeekBegin())
            {
                Logger.FATAL("Unable to seek to beginning of the file");
                return false;
            }
            //3. Open the media file
            var windowSize = (uint) maxFrameSize * 16;
            //windowSize = windowSize < 65536 ? 65536 : windowSize;
            //windowSize = (windowSize > (1024 * 1024)) ? (windowSize / 2) : windowSize;
            _pFile = MediaFile.Initialize(Name);
            //4. Read the frames count from the file
            if (!_pSeekFile.SeekTo(_seekBaseOffset))
            {
                Logger.FATAL("Unable to seek to _seekBaseOffset: {0}", _seekBaseOffset);
                return false;
            }
            _totalFrames = _pSeekFile.Br.ReadUInt32();
            _timeToIndexOffset = _framesBaseOffset + _totalFrames * MediaFrame.MediaFrameSize;

            //5. Set the client side buffer length
            _clientSideBufferLength = clientSideBufferLength;

            //6. Create the timer
            _pTimer = new InFileStreamTimer(this);
            _pTimer.EnqueueForTimeEvent((uint)(_clientSideBufferLength - _clientSideBufferLength / 3));

            //7. Done
            return true;
        }
        protected abstract bool BuildFrame(MediaFile pFile, MediaFrame mediaFrame, Stream buffer);
        protected abstract bool FeedMetaData(MediaFile pFile, MediaFrame mediaFrame);
#if HAS_MMAP
#else

        protected virtual MediaFile GetFile(string filePath)
        {
            MediaFile pResult;
            if (!FileCache.ContainsKey(filePath))
            {
                pResult = MediaFile.CacheMediaFile(filePath);
                if (pResult == null)
                {
                    return null;
                }
                pResult.UseCount = 1;
                FileCache[filePath] = pResult;
            }
            else
            {
                pResult = FileCache[filePath];
                FileCache[filePath].UseCount++;
            }
            return pResult;
        }
        private static void ReleaseFile(MediaFile pFile)
        {
            if (pFile == null) return;
            MediaFile cache;
            if (FileCache.TryGetValue(pFile.FilePath, out cache))
            {
                cache.UseCount--;
                if (cache.UseCount == 0)
                {
                    cache.Dispose();
                    FileCache.Remove(pFile.FilePath);
                }
            }
            else
            {
                Logger.WARN("You tryed to release a non-cached file:{0}", pFile.FilePath);
            }
        }
#endif
        public override bool SignalPlay(ref double absoluteTimestamp, ref double length)
        {
            //0. fix absoluteTimestamp and length
            absoluteTimestamp = absoluteTimestamp < 0 ? 0 : absoluteTimestamp;
            _playLimit = length;
            //1. Seek to the correct point
            if (!InternalSeek(ref absoluteTimestamp))
            {
                Logger.FATAL("Unable to seek to {0}", absoluteTimestamp);
                return false;
            }

            //2. Put the stream in active mode
            Paused = false;

            //3. Start the feed reaction
            ReadyForSend();

            //4. Done
            return true;
        }

        public override bool SignalPause()
        {
            //1. Is this already paused
            if (Paused)
                return true;

            //2. Put the stream in paused mode
            Paused = true;

            //3. Done
            return true;
        }

        public override bool SignalResume()
        {
            //1. Is this already active
            if (!Paused)
                return true;

            //2. Put the stream in active mode
            Paused = false;

            //3. Start the feed reaction
            ReadyForSend();

            //5. Done
            return true;
        }

        public override bool SignalSeek(ref double absoluteTimestamp)
        {
            
                //1. Seek to the correct point
                if (!InternalSeek(ref absoluteTimestamp))
                {
                    Logger.FATAL("Unable to seek to {0}", absoluteTimestamp);
                    return false;
                }

                //2. If the stream is active, re-initiate the feed reaction
                if (!Paused)
                    ReadyForSend();

                //3. Done
                return true;
            
        }

        public override bool SignalStop() => Paused = true;

        public override void ReadyForSend()
        {
            lock (this)
            {
                if (!Feed())
                {
                    Logger.FATAL("Feed failed");
                    if (OutStreams.Any())
                        OutStreams.Last().EnqueueForDelete();
                }
                else
                {
                    Flush();
                }
            }
        }

        private bool SendCodecs()
        {
            //1. Read the first frame
            MediaFrame frame1;
            if (!_pSeekFile.SeekTo(_framesBaseOffset + 0 * MediaFrame.MediaFrameSize))
            {
                Logger.FATAL("Unablt to seek inside seek file");
                return false;
            }
            if (!MediaFrame.ReadFromMediaFile(_pSeekFile,out frame1))
            {
                Logger.FATAL("Unable to read frame from seeking file");
                return false;
            }

            //2. Read the second frame
            MediaFrame frame2;
            if (!_pSeekFile.SeekTo(_framesBaseOffset + 1 * MediaFrame.MediaFrameSize))
            {
                Logger.FATAL("Unablt to seek inside seek file");
                return false;
            }
            if (!MediaFrame.ReadFromMediaFile(_pSeekFile,out frame2))
            {
                Logger.FATAL("Unable to read frame from seeking file");
                return false;
            }

            //3. Read the current frame to pickup the timestamp from it
            MediaFrame currentFrame;
            if (!_pSeekFile.SeekTo(_framesBaseOffset + _currentFrameIndex * MediaFrame.MediaFrameSize))
            {
                Logger.FATAL("Unablt to seek inside seek file");
                return false;
            }
            if (!MediaFrame.ReadFromMediaFile(_pSeekFile, out currentFrame))
            {
                Logger.FATAL("Unable to read frame from seeking file");
                return false;
            }

            //4. Is the first frame a codec setup?
            //If not, the second is not a codec setup for sure
            if (!frame1.IsBinaryHeader)
            {
                _audioVideoCodecsSent = true;
                return true;
            }

            //5. Build the buffer for the first frame
            var buffer = Utils.Rms.GetStream();
            if (!BuildFrame(_pFile, frame1, buffer))
            {
                Logger.FATAL("Unable to build the frame");
                return false;
            }
            //6. Do the feedeng with the first frame
            FeedData(buffer, (uint) buffer.Length, 0, (uint) buffer.Length, currentFrame.AbsoluteTime,
                frame1.Type == MediaFrameType.Audio);

            //7. Is the second frame a codec setup?
            if (!frame2.IsBinaryHeader)
            {
                _audioVideoCodecsSent = true;
                return true;
            }

            //8. Build the buffer for the second frame
            buffer.IgnoreAll();
            if (!BuildFrame(_pFile, frame2, buffer))
            {
                Logger.FATAL("Unable to build the frame");
                return false;
            }
            //9. Do the feedeng with the second frame
            FeedData(buffer, (uint)buffer.Length, 0, (uint)buffer.Length, currentFrame.AbsoluteTime,
                 frame2.Type == MediaFrameType.Audio);

            //10. Done
            _audioVideoCodecsSent = true;
            return true;
        }

        protected virtual bool Feed()
        {
            //2. First, send audio and video codecs
            if (!_audioVideoCodecsSent && !SendCodecs())
            {
                Logger.FATAL("Unable to send audio codec");
                return false;
            }
            while (!Paused && OutStreams.Count != 0)
            {
                //2. Determine if the client has enough data on the buffer and continue
                //or stay put
                var elapsedTime = (int)(DateTime.Now - _startFeedingTime).TotalSeconds;
                if ((int) _totalSentTime - elapsedTime >= _clientSideBufferLength)return true;
                    
                //3. Test to see if we have sent the last frame
                if (_currentFrameIndex >= _totalFrames || _playLimit >= 0 && _playLimit < _totalSentTime)
                {
                    this.Log().Info("Done streaming file");
                    OutStreams.Last().SignalStreamCompleted();
                    Paused = true;
                    return true;
                }

                //4. Read the current frame from the seeking file
                if (!_pSeekFile.SeekTo(_framesBaseOffset + _currentFrameIndex * MediaFrame.MediaFrameSize))
                {
                    Logger.FATAL("Unablt to seek inside seek file");
                    return false;
                }
                if (!MediaFrame.ReadFromMediaFile(_pSeekFile, out _currentFrame))
                {
                    Logger.FATAL("Unable to read frame from seeking file");
                    return false;
                }
                Stream buffer = null;
                //Debug.WriteLine("{2},{0}:{1}", _currentFrame.AbsoluteTime, _currentFrame.Type, _currentFrameIndex);
                switch (_currentFrame.Type)
                {
                    case MediaFrameType.Data:
                        _currentFrameIndex++;
                        if (!FeedMetaData(_pFile, _currentFrame))
                        {
                            Logger.FATAL("Unable to feed metadata");
                            return false;
                        }
                        break;

                    case MediaFrameType.Audio:
                        buffer = _audioBuffer;
                        goto case MediaFrameType.Video;
                    case MediaFrameType.Video:
                        if (buffer == null) buffer = _videoBuffer;
                        //7. Build the frame
                        if (!BuildFrame(_pFile, _currentFrame, buffer))
                        {
                            Logger.FATAL("Unable to build the frame");
                            return false;
                        }

                        //8. Compute the timestamp
                        _totalSentTime = _currentFrame.AbsoluteTime / 1000 - _totalSentTimeBase;
                        //9. Do the feedeng
                        FeedData(buffer, (uint)buffer.Length, 0, (uint)buffer.Length, _currentFrame.AbsoluteTime, _currentFrame.Type == MediaFrameType.Audio);
                        //10. Discard the data
                        buffer.IgnoreAll();
                        //11. Increment the frame index
                        _currentFrameIndex++;
                        //12. Done. We either feed again if frame length was 0
                        //or just return true
                        //return _currentFrame.Length != 0 || Feed();
                        break;
                    default:
                        if (!FeedOtherType()) return false;
                        break;
                }
            }
            return true;
        }

        protected virtual bool FeedOtherType() => true;

        private bool InternalSeek(ref double absoluteTimestamp)
        {
            //0. We have to send codecs again
            _audioVideoCodecsSent = false;

            //1. Switch to millisecond.FrameIndex table
            if (!_pSeekFile.SeekTo(_timeToIndexOffset))
            {
                Logger.FATAL("Failed to seek to ms.FrameIndex table");
                return false;
            }

            //2. Read the sampling rate
            var samplingRate = _pSeekFile.Br.ReadUInt32();

            //3. compute the index in the time2frameindex
            var tableIndex = (uint)(absoluteTimestamp / samplingRate);

            //4. Seek to that corresponding index
            _pSeekFile.SeekAhead(tableIndex * 4);

            //5. Read the frame index
            var frameIndex = _pSeekFile.Br.ReadUInt32();

            //7. Position the seek file to that particular frame
            if (!_pSeekFile.SeekTo(_framesBaseOffset + frameIndex * MediaFrame.MediaFrameSize))
            {
                Logger.FATAL("Unablt to seek inside seek file");
                return false;
            }

            //8. Read the frame
            if (!MediaFrame.ReadFromMediaFile(_pSeekFile, out _currentFrame))
            {
                Logger.FATAL("Unable to read frame from seeking file");
                return false;
            }

            //9. update the stream counters
            _startFeedingTime = DateTime.Now;
            _totalSentTime = 0;
            _currentFrameIndex = frameIndex;
            _totalSentTimeBase = (uint)(_currentFrame.AbsoluteTime / 1000);
            absoluteTimestamp = _currentFrame.AbsoluteTime;
            
            //10. Go back on the frame of interest
            if (!_pSeekFile.SeekTo(_framesBaseOffset + frameIndex * MediaFrame.MediaFrameSize))
            {
                Logger.FATAL("Unablt to seek inside seek file");
                return false;
            }

            //11. Done
            return true;
        }
        private sealed class InFileStreamTimer : BaseTimerProtocol
        {
            private BaseInFileStream _pInFileStream;

            public InFileStreamTimer(BaseInFileStream pInFileStream)
            {
                _pInFileStream = pInFileStream;
            }

            public void ResetStream() => _pInFileStream = null;

            public override bool TimePeriodElapsed()
            {
                try
                {
                    _pInFileStream?.ReadyForSend();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
    }

   
}
