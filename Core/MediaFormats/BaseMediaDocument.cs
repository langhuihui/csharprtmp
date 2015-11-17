using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Streaming;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Core.MediaFormats
{
    public abstract class BaseMediaDocument:IDisposable
    {
        public MediaFile MediaFile { get; protected set; }
        protected List<MediaFrame> _frames = new List<MediaFrame>();
        protected uint _audioSamplesCount;
        protected uint _videoSamplesCount;
        public readonly Variant MetaData;
        private string _mediaFilePath;
        private string _seekFilePath;
        private string _metaFilePath;
        private bool _keyframeSeek;
        private uint _seekGranularity;
        protected StreamCapabilities _streamCapabilities = new StreamCapabilities();
        public Stream MediaStream;
        protected BaseMediaDocument(Variant metaData)
        {
            MetaData = metaData;
        }

        public bool Process()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            //1. Compute the names
	_mediaFilePath =  MetaData[Defines.META_SERVER_FULL_PATH];
            _metaFilePath = _mediaFilePath + "." + Defines.MEDIA_TYPE_META;
    _seekFilePath = _mediaFilePath + "." + Defines.MEDIA_TYPE_SEEK;
    _keyframeSeek = MetaData[Defines.CONF_APPLICATION_KEYFRAMESEEK];
    _seekGranularity = MetaData[Defines.CONF_APPLICATION_SEEKGRANULARITY];
            	//1. Open the media file
#if HAS_MMAP
	if (!_mediaFile.Initialize(_mediaFilePath, 4 * 1024 * 1024, true)) {
		FATAL("Unable to open media file: %s", STR(_mediaFilePath));
		return false;
	}
#else
            try
            {
                MediaFile = MediaFile.Initialize(_mediaFilePath);
                MediaStream = MediaFile.DataStream;
            }
            catch (Exception ex)
            {
                Logger.FATAL("Unable to open media file: {0}", _mediaFilePath);
                return false;
            }
#endif
            //4. Read the document
            if (!ParseDocument())
            {
                Logger.FATAL("Unable to parse document");
                return false;
            }

            //5. Build the frames
            if (!BuildFrames())
            {
                Logger.FATAL("Unable to build frames");
                return false;
            }

            //6. Save the seek file
            if (!SaveSeekFile())
            {
                Logger.FATAL("Unable to save seeking file");
                return false;
            }

            //7. Build the meta
            if (!SaveMetaFile())
            {
                Logger.FATAL("Unable to save meta file");
                return false;
            }
            stopwatch.Stop();
            Logger.INFO("{0} frames computed in {1} seconds at a speed of {2} FPS",
                _frames.Count, stopwatch.Elapsed.TotalSeconds, _frames.Count / stopwatch.Elapsed.TotalSeconds);
            if (_frames.Any())
            {
                var totalSeconds = ((uint) _frames[_frames.Count - 1].AbsoluteTime) / 1000;
		        var hours = totalSeconds / 3600;
		        var minutes = (totalSeconds - hours * 3600) / 60;
		        var seconds = (totalSeconds - hours * 3600 - minutes * 60);
		        Logger.INFO("File size: {0} bytes; Duration: {1}:{2}:{3} ({4} sec); Optimal bandwidth: {5} kb/s",
				        MediaFile.FileInfo.Length,
				        hours, minutes, seconds,
				        totalSeconds,
				        _streamCapabilities.BandwidthHint);
            }
            if (File.Exists(_seekFilePath)) File.Delete(_seekFilePath);
            File.Move(_seekFilePath + ".tmp", _seekFilePath);
            if (File.Exists(_metaFilePath)) File.Delete(_metaFilePath);
            File.Move(_metaFilePath + ".tmp", _metaFilePath);
            File.SetAttributes(_seekFilePath,FileAttributes.Normal);
            File.SetAttributes(_metaFilePath, FileAttributes.Normal);
            return true;
        }

        public static int CompareFrames(MediaFrame frame1,MediaFrame frame2)
        {
            return (int) (frame1.AbsoluteTime == frame2.AbsoluteTime
                ? frame1.Start - frame2.Start
                : frame1.AbsoluteTime - frame2.AbsoluteTime);
        }

        private bool SaveSeekFile()
        {
            if (_frames.Count <= 2)
            {
                Logger.FATAL("No frames found");
                return false;
            }
            //1. Open the file
            var seekFile= MediaFile.Initialize(_seekFilePath+".tmp",FileMode.Create,FileAccess.Write);
            if (seekFile == null)
            {
                Logger.FATAL("Unable to open seeking file {0}",_seekFilePath);
                return false;
            }
            //2. Setup the bandwidth hint in bytes/second
            var totalSeconds = (_frames[_frames.Count - 1].AbsoluteTime) / 1000.0;
            _streamCapabilities.BandwidthHint =
                    (uint)(MediaFile.Length / totalSeconds / 1024.0 * 8.0);
            var raw = Utils.Rms.GetStream();
            using (var writer = new H2NBinaryWriter(raw))
            {
                if (!_streamCapabilities.Serialize(writer))
                {
                    Logger.FATAL("Unable to serialize stream capabilities");
                    return false;
                }
                seekFile.Bw.Write((uint) raw.Length);
                raw.WriteTo(seekFile.DataStream);
            }
            seekFile.Bw.Write(_frames.Count);
            //3. Write the frames
            var hasVideo = false;
            ulong maxFrameSize = 0;
            foreach (var mediaFrame in _frames)
            {
                if (maxFrameSize < mediaFrame.Length) maxFrameSize = mediaFrame.Length;
                hasVideo |= mediaFrame.Type == MediaFrameType.Video;
                seekFile.Bw.Write(mediaFrame.GetBytes());
                //if (seekFile.WriteBuffer(mediaFrame.GetBytes())) continue;
                //Logger.FATAL("Unable to write frame");
                //return false;
            }
            _keyframeSeek &= hasVideo;

            //4. Write the seek granularity
            seekFile.Bw.Write(_seekGranularity);
            //4. create the time to frame index table. First, see what is the total time
            if (_frames.Count >= 1)
            {
                var totalTime = _frames[_frames.Count - 1].AbsoluteTime;

                //5. build the table
                int frameIndex = 0;
                int seekPoint = 0;
                for (double i = 0; i <= totalTime; i += _seekGranularity)
                {
                    while (_frames[frameIndex].AbsoluteTime < i)
                    {
                        frameIndex++;
                        if (frameIndex >= _frames.Count)
                            break;

                        if (_keyframeSeek)
                        {
                            if ((_frames[frameIndex].Type == MediaFrameType.Video)
                                    && (_frames[frameIndex].IsKeyFrame))
                            {
                                seekPoint = frameIndex;
                            }
                        }
                        else
                        {
                            seekPoint = frameIndex;
                        }
                    }
                    if (frameIndex >= _frames.Count) break;
                    seekFile.Bw.Write(seekPoint);
                }
            }
            //6. Save the max frame size
            seekFile.Bw.Write(maxFrameSize);
            seekFile.Bw.Dispose();
            //7. Done
            return true;
        }
        private bool SaveMetaFile()
        {
            MetaData[Defines.META_AUDIO_FRAMES_COUNT] = _audioSamplesCount;
            MetaData[Defines.META_VIDEO_FRAMES_COUNT] = _videoSamplesCount;
            MetaData[Defines.META_TOTAL_FRAMES_COUNT] = _frames.Count;

            MetaData[Defines.META_FILE_SIZE] = MediaFile.Length;
            if (_frames.Count > 0)
            {
                MetaData[Defines.META_FILE_DURATION] = _frames[_frames.Count - 1].AbsoluteTime;
                MetaData[Defines.META_FILE_BANDWIDTH] = _streamCapabilities.BandwidthHint;
            }
            else
            {
                MetaData[Defines.META_FILE_DURATION] = 0;
            }
            if (MetaData[Defines.META_RTMP_META] != null)
            {
                MetaData[Defines.META_RTMP_META].Recycle();
                MetaData[Defines.META_RTMP_META] = null;
            }
            MetaData[Defines.META_RTMP_META] = GetRTMPMeta().Clone();
            MetaData[Defines.META_RTMP_META,"duration"] = MetaData[Defines.META_FILE_DURATION] / 1000.00;
            MetaData[Defines.META_RTMP_META,Defines.META_FILE_BANDWIDTH] = _streamCapabilities.BandwidthHint;
            MetaData.SerializeToFile(_metaFilePath + ".tmp");
            return true;
        }

        protected abstract bool ParseDocument();
        protected abstract bool BuildFrames();
        protected abstract Variant GetRTMPMeta();

        public void Dispose()
        {
            MediaFile.Dispose();
        }
    }
}
