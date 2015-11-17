using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Rtmp;
using CSharpRTMP.Core.Streaming;

namespace Core.Protocols.Rtmp
{
    [StreamType(StreamTypes.ST_NEUTRAL_RTMP)]
    public class RTMPStream:BaseStream<BaseRTMPProtocol>
    {
        public static readonly RTMPStream I = new RTMPStream();
        public override bool IsCompatibleWithType(ulong type)
        {
            return false;
        }
        public override void Dispose()
        {
            
        }
    }
}
