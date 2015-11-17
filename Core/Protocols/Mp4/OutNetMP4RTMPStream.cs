using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpRTMP.Common;
using CSharpRTMP.Core.MediaFormats;
using CSharpRTMP.Core.MediaFormats.mp4;
using CSharpRTMP.Core.MediaFormats.mp4.boxes;
using CSharpRTMP.Core.Streaming;

namespace CSharpRTMP.Core.Protocols.Mp4
{
    [StreamType(StreamTypes.ST_OUT_NET_MP4_4_RTMP,StreamTypes.ST_IN_NET_RTMP)]
    public class OutNetMP4RTMPStream:BaseOutNetStream<Mp4Protocol>
    {
        readonly MP4Document _doc = new MP4Document(Variant.Get());
        public StreamWriter Writer;

        public OutNetMP4RTMPStream(Mp4Protocol pProtocol, StreamsManager pStreamsManager, string name) : base(pProtocol, pStreamsManager, name)
        {
            var inStream = pStreamsManager.FindByTypeByName(StreamTypes.ST_IN_NET_RTMP, name, true, false).Values.FirstOrDefault() as IInStream;
            inStream?.Link(this);
           
        }

        public override void SignalAttachedToInStream()
        {
            var capabilities = Capabilities;
            var ftyp = new AtomFTYP(BaseAtom.MP42, 0, BaseAtom.MP42, BaseAtom.ISOM) {Document = _doc};
            ftyp.Write();

        }

        public override bool FeedData(Stream pData, uint dataLength, uint processedLength, uint totalLength, uint absoluteTimestamp,
            bool isAudio)
        {

            





            
            return true;
        }

        private void SendData(Stream s,int len)
        {
            Protocol.OutputBuffer.Write((ushort)len);
            Writer.WriteLine("");
            s.CopyPartTo(Protocol.OutputBuffer, len);
            Writer.WriteLine("");
            Protocol.EnqueueForOutbound(Protocol.OutputBuffer);
        }
        public override void SignalDetachedFromInStream()
        {
            Writer.WriteLine("0");
            Writer.WriteLine("");
            Protocol.EnqueueForOutbound(Protocol.OutputBuffer);
            Protocol.EnqueueForDelete();
        }
    }
}
