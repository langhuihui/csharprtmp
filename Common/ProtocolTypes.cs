
namespace CSharpRTMP.Common
{
    public static class ProtocolTypes
    {
        //carrier protocols
        public const ulong PT_TCP = 6071784683555782656;
        public const ulong PT_UDP = 6144123752570421248;
		public const ulong PT_INBOUND_CLUSTER = 5279063188208353280;
		public const ulong PT_OUTBOUND_CLUSTER = 5711408752435920896;
        //variant protocols
        public const ulong PT_BIN_VAR = 4780079874943483904;
        public const ulong PT_XML_VAR = 6365346943777898496;
        //RTMP protocols
        public const ulong PT_INBOUND_RTMP = 5283285312859013120; 
        public const ulong PT_INBOUND_RTMPS_DISC = 5283376572324118528;
        public const ulong PT_OUTBOUND_RTMP = 5715630877086580736;
        public const ulong PT_MONITOR_RTMP = 5571515689010724864;
        public const ulong PT_INBOUND_RTMFP = 5283362278672957440;
        public const ulong PT_RTMFP_SESSION = 5931240709246943232;
        //encryption protocols
        public const ulong PT_RTMPE = 5928144484503126016;
        public const ulong PT_INBOUND_SSL = 5283658373718343680;
        public const ulong PT_OUTBOUND_SSL = 5716003937945911296;

        //Async DNS protocols
        public const ulong PT_INBOUND_DNS = 5279430781574316032;
        public const ulong PT_OUTBOUND_DNS = 5711776345801883648;

        //MPEG-TS protocol
        public const ulong PT_INBOUND_TS = 5283939522277539840;
		public const ulong PT_INBOUND_MP4 = 5281966122243981312;
        //HTTP protocols
        public const ulong PT_INBOUND_HTTP = 5280563282845892608;
        public const ulong PT_INBOUND_HTTP_FOR_RTMP = 5280528089883869184;
        public const ulong PT_OUTBOUND_HTTP = 5712908847073460224;
        public const ulong PT_OUTBOUND_HTTP_FOR_RTMP = 5712873654111436800;
		public const ulong PT_INBOUND_WEBSOCKET = 5284783947207671808;
		public const ulong PT_INBOUND_WEBRTC_SIGNAL = 5284783209602809856;

        //Timer protocol
        public const ulong PT_TIMER = 6074601632346144768;

        //Live FLV protocols
        public const ulong PT_INBOUND_LIVE_FLV = 5281673755230208000;
        public const ulong PT_OUTBOUND_LIVE_FLV = 5714019319457775616;

        //RTP/RTPS protocols
        public const ulong PT_RTSP = 5932458212216274944;
        public const ulong PT_RTCP = 5932440620030230528;
        public const ulong PT_INBOUND_RTP = 5283378015433129984;
        public const ulong PT_OUTBOUND_RTP = 5715723579660697600;
        public const ulong PT_RTP_NAT_TRAVERSAL = 5930749589735866368;

        //MMS
        public const ulong PT_OUTBOUND_MMS = 5570199573592276992;

        //CLI protocols
        public const ulong PT_INBOUND_JSONCLI = 5281125113125882953;
        public const ulong PT_HTTP_4_CLI = 5202857136798826496;

        //Raw HTTP stream
        public const ulong PT_INBOUND_RAW_HTTP_STREAM = 5283364834178498560;
    }
}
