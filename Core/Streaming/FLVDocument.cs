using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.MediaFormats;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Core.Streaming
{
    public sealed class FLVDocument : BaseMediaDocument
    {
        public FLVDocument(Variant metaData)
            : base(metaData)
        {
            _streamCapabilities.AudioCodecId = AudioCodec.PassThrough;
            _streamCapabilities.VideoCodecId = VideoCodec.PassThrough;
        }

        protected override bool ParseDocument()
        {
            return true;
        }

        protected override bool BuildFrames()
        {
            var binaryHeaders = new List<MediaFrame>();
            //1. Go to the beginning of the file
            if (!MediaFile.SeekBegin())
            {
                Logger.FATAL("Unable to seek in file");
                return false;
            }

            //2. Ignore the flv header
            if (!MediaFile.SeekAhead(9))
            {
                Logger.FATAL("Unable to seek in file");
                return false;
            }

            //3. We are not interested in the previous tag size
            if (!MediaFile.SeekAhead(4))
            {
                Logger.FATAL("Unable to seek in file");
                return false;
            }

            //4. Build the frames
            while (MediaFile.Position != MediaFile.Length)
            {
                //5. Read the tag type
                byte tagType;
                if (!MediaFile.ReadUInt8(out tagType))
                {
                    Logger.WARN("Unable to read data");
                    break;
                }

                //6. Set the frame type based on the tag type
                //Also set the iskeyFrame property here
                var mustBreak = false;
                var frame = new MediaFrame();
                switch (tagType)
                {
                    case 8: //audio data
                        _audioSamplesCount++;
                        frame.Type = MediaFrameType.Audio;
                        break;
                    case 9: //video data
                        _videoSamplesCount++;
                        frame.Type = MediaFrameType.Video;
                        break;
                    case 15://message data
                        frame.Type = MediaFrameType.Message;
                        break;
                    case 18: //info data
                        frame.Type = MediaFrameType.Data;
                        break;
                    default:
                        Logger.WARN("Invalid tag type: {0} at cursor {1}", tagType, MediaFile.Position);
                        mustBreak = true;
                        break;
                }
                if (mustBreak) break;

                //7. Read the frame length
                uint tempLength = MediaFile.Br.ReadU24();
                //if (!MediaFile.ReadUInt24(out tempLength))
                //{
                //    Logger.WARN("Unable to read data");
                //    break;
                //}
                frame.Length = tempLength;

                //8. read the timestamp and set the timing on the frame
                var timestamp = MediaFile.Br.ReadSU32();
                //if (!MediaFile.ReadSUI32(out timestamp))
                //{
                //    Logger.WARN("Unable to read data");
                //    break;
                //}
                //TODO: correctly compute delta time
                frame.DeltaTime = 0;
                frame.AbsoluteTime = timestamp;

                //9. Ignore the stream ID
                if (!MediaFile.SeekAhead(3))
                {
                    Logger.WARN("Unable to seek in file");
                    break;
                }

                //10. Save the start of the data
                frame.Start = (uint)MediaFile.Position;

                //11. Set the isKeyFrame flag and the isBinary flag
                byte _byte;
                switch (frame.Type)
                {
                    case MediaFrameType.Video:
                        if (!MediaFile.PeekByte(out _byte))
                        {
                            Logger.FATAL("Unable to peek byte");
                            return false;
                        }
                        frame.IsKeyFrame = ((_byte >> 4) == 1);
                        if (frame.IsKeyFrame)
                        {
                            frame.IsBinaryHeader = ((_byte & 0x0f) == 7);
                            if (frame.IsBinaryHeader)
                            {
                                ulong dword;
                                if (!MediaFile.PeekUInt64(out dword))
                                {
                                    Logger.FATAL("Unable to peek byte");
                                    return false;
                                }
                                frame.IsBinaryHeader = (((dword >> 48) & 0xff) == 0);
                            }
                        }
                        else
                        {
                            frame.IsBinaryHeader = false;
                        }
                    
                        break;
                    case MediaFrameType.Audio:
                        frame.IsKeyFrame = true;
                        if (!MediaFile.PeekByte(out _byte))
                        {
                            Logger.FATAL("Unable to peek byte");
                            return false;
                        }
                        frame.IsBinaryHeader = ((_byte >> 4) == 10);
                        if (frame.IsBinaryHeader)
                        {
                            ushort word;
                            if (!MediaFile.PeekUInt16(out word))
                            {
                                Logger.FATAL("Unable to peek byte");
                                return false;
                            }
                            frame.IsBinaryHeader = ((word & 0x00ff) == 0);
                        }
                    
                        break;
                }
                if (frame.IsBinaryHeader)
                    Logger.WARN("frame: {0}", frame);

                if (!MediaFile.SeekAhead(frame.Length))
                {
                    Logger.WARN("Unable to seek in file");
                    break;
                }


                //13. We are not interested in the previous tag size
                //if (!MediaFile.SeekAhead(4))
                //{
                //    Logger.WARN("Unable to seek in file");
                //    break;
                //}
                var preTagSize = MediaFile.Br.ReadInt32();
                //14. Store it in the proper location and adjust the timestamp accordingly
                //if (frame.IsBinaryHeader)
                //{
                    //frame.AbsoluteTime = 0;
                    binaryHeaders.Insert(0, frame);
                    //binaryHeaders.Add(frame);

                //}
                //else
               // {
                    _frames.Add(frame);

               // }
            }
            //_frames.Sort(CompareFrames);

            //15. Add the binary headers
            //_frames.InsertRange(0, binaryHeaders);
            //for (var i = 0; i < binaryHeaders.Count; i++) {
            //    _frames.Insert(0, binaryHeaders[i]);

            //}

            return true;
        }

        protected override Variant GetRTMPMeta()
        {
            return MetaData;
        }
    }
}
