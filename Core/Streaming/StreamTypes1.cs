using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.Streaming
{
    public static class StreamTypes
    {
        public static readonly ulong ST_NEUTRAL_RTMP = Utils.MakeTag('N', 'R');
        public static readonly ulong ST_IN = Utils.MakeTag('I');
        public static readonly ulong ST_IN_NET = Utils.MakeTag('I', 'N');
        public static readonly ulong ST_IN_NET_RTMP = Utils.MakeTag('I', 'N', 'R');
        public static readonly ulong ST_IN_NET_RTMFP = Utils.MakeTag('I', 'N', 'F');
        public static readonly ulong ST_IN_NET_LIVEFLV = Utils.MakeTag('I', 'N', 'L', 'F', 'L', 'V');
        public static readonly ulong ST_IN_NET_TS = Utils.MakeTag('I', 'N', 'T', 'S');
        public static readonly ulong ST_IN_NET_RTP = Utils.MakeTag('I', 'N', 'P');
        public static readonly ulong ST_IN_NET_RAW = Utils.MakeTag('I', 'N', 'W');
        public static readonly ulong ST_IN_NET_AAC = Utils.MakeTag('I', 'N', 'A');
        public static readonly ulong ST_IN_NET_MP3 = Utils.MakeTag('I', 'N', 'M');
        public static readonly ulong ST_IN_FILE = Utils.MakeTag('I', 'F');
        public static readonly ulong ST_IN_FILE_RTMP = Utils.MakeTag('I', 'F', 'R');
        public static readonly ulong ST_OUT = Utils.MakeTag('O');
        public static readonly ulong ST_OUT_NET = Utils.MakeTag('O', 'N');
        public static readonly ulong ST_OUT_NET_RTMP = Utils.MakeTag('O', 'N', 'R');
        public static readonly ulong ST_OUT_NET_RTMFP = Utils.MakeTag('O', 'N', 'F');
        public static readonly ulong ST_OUT_NET_RTMP_4_TS = Utils.MakeTag('O', 'N', 'R', '4', 'T', 'S');
        public static readonly ulong ST_OUT_NET_RTMP_4_RTMP = Utils.MakeTag('O', 'N', 'R', '4', 'R');
        public static readonly ulong ST_OUT_NET_RTP = Utils.MakeTag('O', 'N', 'P');
        public static readonly ulong ST_OUT_NET_RAW = Utils.MakeTag('O', 'N', 'W');
        public static readonly ulong ST_OUT_FILE = Utils.MakeTag('O', 'F');
        public static readonly ulong ST_OUT_FILE_RTMP = Utils.MakeTag('O', 'F', 'R');
        public static readonly ulong ST_OUT_FILE_RTMP_FLV = Utils.MakeTag('O', 'F', 'R', 'F', 'L', 'V');
        public static readonly ulong ST_OUT_FILE_HLS = Utils.MakeTag('O', 'F', 'H', 'L', 'S');
    }
}
