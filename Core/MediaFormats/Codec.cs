using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRTMP.Core.MediaFormats
{
    public enum VideoCodec
    {
        Rgb,
        Rle,
        Sorenson,
        Screen1,
        Vp6,
        Vp6Alpha,
        Screen2,
        H264,
        H263,
        Mpeg42,
        Unknown = 15,
        PassThrough
    };

    public enum AudioCodec
    {
        Pcm,
        Adpcm,
        Mp3,
        PcmLittle,
        Nellymoser16,
        Nellymoser8,
        NellymoserAny,
        Alaw,
        Ulaw,
        Aac = 10,
        Speex,
        Mp38 = 14,
        Unknown, PassThrough
    };

    public enum AudioSampleSize
    {
        Bit8,Bit16
    }

    public enum AudioSampleType
    {
        Mono, Stereo
    }

    public enum VideoFrameType
    {
        Unknow,KeyFrame,InterFrame,DisposableInterFrame,GeneratedKeyFrame,InfoOrCommand
    }
   
    public static class Codec
    {
        public static readonly uint[] RateMap = {
            96000, 88200, 64000, 48000, 44100, 32000, 24000, 22050, 16000,
            12000, 11025, 8000, 7350
        };
    }
}
