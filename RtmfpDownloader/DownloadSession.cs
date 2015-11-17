using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Core.Protocols.Rtmp;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Rtmfp;

namespace RtmfpDownloader
{
    public class DownloadSession:OutboundHandshake
    {
        private OutFileRTMPFLVStream _flvStream;
       
        public List<FlowStream> DowloadStreams = new List<FlowStream>();
        public long TotalBytes => DowloadStreams.Count>0?DowloadStreams.Sum(x => x.TotalBytes):0;
        //private FlowConnection _flowConnection;
        public DownloadSession(BaseRtmfpProtocol handler) : base(handler)
        {
        }
        public void PlayStream(string name,ulong flowId,uint streamId, OutFileRTMPFLVStream flvStream)
        {
            if (_flvStream != null) return;
            var flowStream = new FlowWriter(FlowStream.Signature + (char)streamId,this, flowId);
            flowStream.Play(name);
            _flvStream = flvStream;
            _flvStream.SignalAttachedToInStream();
        }
    
        private void FlowStream_OnStatus(CSharpRTMP.Common.Variant obj)
        {
            (Handler as IDownload).Log += obj[1]["code"];
            //Logger.Debug("{0}",obj.ToString());
        }

        public override void EnqueueForDelete()
        {
            base.EnqueueForDelete();
            _flvStream?.Dispose();
        }

        public override Flow CreateFlow(ulong id, string signature,ulong asscoId)
        {
            var flow = base.CreateFlow(id, signature, asscoId);
            var item = flow as FlowStream;
            if (item == null) return flow;
            DowloadStreams.Add(item);
            item.OnStatus += FlowStream_OnStatus;
            item.OutStream = _flvStream;
            return flow;
        }
        

    }
}
