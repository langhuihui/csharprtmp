using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.MediaFormats;
using CSharpRTMP.Core.Protocols;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Core.Streaming
{
    public sealed class MP3Document : BaseMediaDocument
    {
        private const int LAYER_1 = 3;
        private const int LAYER_2 = 2;
        private const int LAYER_3 = 1;
        private static readonly int[, ,] _bitRates =
        {
            { //MPEG Version 2.5
                //reserved layer
                {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},

                //Layer III
                {-1, 8000, 16000, 24000, 32000, 40000, 48000, 56000, 64000, 80000,
                    96000, 112000, 128000, 144000, 160000, -1},

                //Layer II
                {-1, 8000, 16000, 24000, 32000, 40000, 48000, 56000, 64000, 80000,
                    96000, 112000, 128000, 144000, 160000, -1},

                //Layer I
                {-1, 32000, 48000, 56000, 64000, 80000, 96000, 112000, 128000, 144000,
                    160000, 176000, 192000, 224000, 256000, -1},
            },
            { // Reserved
                {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
                {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
                {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
                {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}
            },
            { //MPEG Version 2 (ISO/IEC 13818-3)
                //reserved layer
                {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},

                //Layer III
                {-1, 8000, 16000, 24000, 32000, 40000, 48000, 56000, 64000, 80000,
                    96000, 112000, 128000, 144000, 160000, -1},

                //Layer II
                {-1, 8000, 16000, 24000, 32000, 40000, 48000, 56000, 64000, 80000,
                    96000, 112000, 128000, 144000, 160000, -1},

                //Layer I
                {-1, 32000, 48000, 56000, 64000, 80000, 96000, 112000, 128000, 144000,
                    160000, 176000, 192000, 224000, 256000, -1},
            },
            { //MPEG Version 1 (ISO/IEC 11172-3)
                //reserved layer
                {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},

                //Layer III
                {-1, 32000, 40000, 48000, 56000, 64000, 80000, 96000, 112000, 128000,
                    160000, 192000, 224000, 256000, 320000, -1},

                //Layer II
                {-1, 32000, 48000, 56000, 64000, 80000, 96000, 112000, 128000,
                    160000, 192000, 224000, 256000, 320000, 384000 ,- 1},

                //Layer I
                {-1, 32000, 64000, 96000, 128000, 160000, 192000, 224000, 256000, 288000,
                    320000, 352000, 384000, 416000, 448000 ,- 1},
            }};
        private static readonly int[,] _samplingRates =
        {
            {//MPEG Version 2.5
                11025, 12000, 8000, -1
            },
            {// Reserved
                -1, -1, -1, -1
            },
            {//MPEG Version 2 (ISO/IEC 13818-3)
                22050, 24000, 16000, -1
            },
            {//MPEG Version 1 (ISO/IEC 11172-3)
                44100, 48000, 32000, -1
            }
        };
        private static readonly string[] _versionNames ={
	"MPEG Version 2.5",
	"reserved",
	"MPEG Version 2 (ISO/IEC 13818-3)",
	"MPEG Version 1 (ISO/IEC 11172-3)"
};
        private static readonly string[] _layerNames = {
	"reserved",
	"Layer III",
	"Layer II",
	"Layer I",
};
        private static uint[, , , ,] _frameSizes;
           
        private Variant _tags;
        public MP3Document(Variant metaData)
            : base(metaData)
        {
            _streamCapabilities.AudioCodecId = AudioCodec.Mp3;
            _streamCapabilities.VideoCodecId = VideoCodec.Unknown;
        }

        protected override bool ParseDocument()
        {
            return true;
        }

        protected override bool BuildFrames()
        {
            //1. Build the map with frame sizes
            InitFrameSizes();

            //2. Go to the beginning of the file
            if (!MediaFile.SeekBegin())
            {
                Logger.FATAL("Unable to seek in file");
                return false;
            }

            if (!ParseMetadata())
            {
                Logger.WARN("Invalid metadata");
                if (!FindFrameData())
                {
                    Logger.FATAL("Unable to position on frame data");
                    return false;
                }
            }

            var firstBytes = new byte[4];

            double totalDuration = 0;
            var frame = new MediaFrame
            {
                Type = MediaFrameType.Audio,
                IsKeyFrame = true,
                DeltaTime = 0,
                IsBinaryHeader = false
            };

            while (MediaFile.Position < MediaFile.Length)
            {
                //3. Read the first 4 bytes
                if (!MediaFile.ReadBuffer(firstBytes, 4))
                {
                    Logger.FATAL("Unable to read 4 byte");
                    return false;
                }

                if ((firstBytes[0] == 0xff) &&
                        ((firstBytes[1] >> 5) == 7))
                {
                    //4. Possible frame. Read the header
                    byte version = (byte)((firstBytes[1] >> 3) & 0x03);
                    byte layer = (byte)((firstBytes[1] >> 1) & 0x03);
                    byte bitRateIndex = (byte)(firstBytes[2] >> 4);
                    byte sampleRateIndex = (byte)((firstBytes[2] >> 2) & 0x03);
                    byte paddingBit = (byte)((firstBytes[2] >> 1) & 0x01);

                    //5. get the freame length
                    frame.Start = (uint)(MediaFile.Position - 4);
                    frame.Length = _frameSizes[version,layer,bitRateIndex,sampleRateIndex,paddingBit];
                    if (frame.Length == 0)
                    {
                        Logger.FATAL("Invalid frame length: {0}:{1}:{2}:{3}:{4}; Cusror: {5}",
                                version, layer, bitRateIndex, sampleRateIndex,
                                paddingBit, MediaFile.Position);
                        return false;
                    }

                    //6. Compute the frame duration and save the frame start
                    var samplesCount = layer == LAYER_1 ? 384 : 1152;
                    frame.AbsoluteTime = (uint) (totalDuration * 1000);
                    totalDuration += samplesCount /
                            (double)(_samplingRates[version, sampleRateIndex]);

                    //7. Seek to the next frame
                    if (!MediaFile.SeekTo((long)(frame.Start + frame.Length)))
                    {
                        Logger.WARN("Unable to seek to {0}", frame.Start + frame.Length);
                        break;
                    }

                    //8. All good. Save the frame
                    _frames.Add(frame);
                }
                else
                {
                    break;
                }
            }


            return true;
        }

        private bool FindFrameData()
        {
            if (!MediaFile.SeekBegin())
            {
                Logger.FATAL("Unable to seek to the beginning of the file");
                return false;
            }

            var firstBytes = new byte[4];
            while (true)
            {
                //1. Read the first 4 bytes
                if (!MediaFile.PeekBuffer(firstBytes, 4))
                {
                    Logger.FATAL("Unable to read 4 bytes");
                    return false;
                }
                if ((firstBytes[0] != 0xff) || ((firstBytes[1] >> 5) != 7))
                {
                    MediaFile.SeekAhead(1);
                    continue;
                }

                //2. Split the flags
                byte version = (byte)((firstBytes[1] >> 3) & 0x03);
                byte layer = (byte)((firstBytes[1] >> 1) & 0x03);
                byte bitRateIndex = (byte)(firstBytes[2] >> 4);
                byte sampleRateIndex = (byte)((firstBytes[2] >> 2) & 0x03);
                byte paddingBit = (byte)((firstBytes[2] >> 1) & 0x01);

                //3. Compute the frame length from the flags
                var length = _frameSizes[version, layer, bitRateIndex, sampleRateIndex, paddingBit];
                if (length == 0)
                {
                    MediaFile.SeekAhead(1);
                    continue;
                }

                //4. Save the cursor value and seek ahead to the next frame
                long cursor = MediaFile.Position;
                MediaFile.SeekTo(cursor + (long) length);

                //5. Try to read 4 bytes again
                if (!MediaFile.PeekBuffer(firstBytes, 4))
                {
                    Logger.FATAL("Unable to read 4 bytes");
                    return false;
                }

                //6. Is this a frame start?
                if ((firstBytes[0] == 0xff) && ((firstBytes[1] >> 5) == 7)) return true;
                MediaFile.SeekTo(cursor + 1);

                //7. Jack pot!
            }
        }

        private bool ParseMetadata()
        {
            //1. pick up first 3 bytes they must be ID3
            var id3 = new byte[3];
            if (!MediaFile.ReadBuffer(id3, 3))
            {
                Logger.FATAL("Unable to read 3 bytes");
                return false;
            }
            if ((id3[0] != 'I') || (id3[1] != 'D') || (id3[2] != '3'))
            {
                Logger.WARN("ID3 not found");
                return false;
            }

            //2. pick up the major version
            byte majorVersion;
            byte minorVersion;
            if (!MediaFile.ReadUInt8(out majorVersion))
            {
                Logger.FATAL("Unable to read 1 byte");
                return false;
            }
            if (!MediaFile.ReadUInt8(out minorVersion))
            {
                Logger.FATAL("Unable to read 1 byte");
                return false;
            }

            //3. Instantiate the proper parser
            var pParser = new ID3Parser(majorVersion, minorVersion);

            //4. Parse
            _tags["tags"] = pParser.GetMetadata();

            return pParser.Parse(MediaFile);

            //5. Process the metadata
        }

        private void InitFrameSizes()
        {
            if (_frameSizes != null) return;
               
            //ver/layer/bitrate/samplerate/padding
            _frameSizes = new uint[4, 4, 15, 3, 2];
            for (byte ver = 0; ver < 4; ver++)
            {
                if (ver == 1)
                    continue;
                for (byte layer = 0; layer < 4; layer++)
                {
                    if (layer == 0)
                        continue;
                    for (byte bitRateIndex = 1; bitRateIndex < 15; bitRateIndex++)
                    {
                        for (byte sampleRateIndex = 0; sampleRateIndex < 3; sampleRateIndex++)
                        {
                            for (byte padding = 0; padding < 2; padding++)
                            {
                                var bitRate = _bitRates[ver, layer, bitRateIndex];
                                var sampleRate = _samplingRates[ver, sampleRateIndex];
                                int length;
                                if (layer == LAYER_1)
                                {
                                    length = (12 * bitRate / sampleRate + padding) * 4;
                                }
                                else
                                {
                                    length = 144 * bitRate / sampleRate + padding;
                                }
                                _frameSizes[ver, layer, bitRateIndex, sampleRateIndex, padding] = (uint) length;
                            }
                        }
                    }
                }
            }
        }

        protected override Variant GetRTMPMeta()
        {
            return _tags;
        }
    }
}
