using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpRTMP.Common;
using CSharpRTMP.Core.MediaFormats;
using CSharpRTMP.Core.MediaFormats.mp4;

namespace CSharpRTMP.Core.Protocols.Mp4
{
    [ProtocolType(ProtocolTypes.PT_INBOUND_MP4)]
    [AllowFarTypes(ProtocolTypes.PT_TCP)]
    public class Mp4Protocol:BaseProtocol
    {
        public override MemoryStream OutputBuffer { get; } = Utils.Rms.GetStream();

        public override bool SignalInputData(int recAmount)
        {
            var reader = new StreamReader(InputBuffer);
            var firstLine = reader.ReadLine();
            var ss = firstLine.Split(' ')[1].Split('/');
            ClientApplicationManager.SwitchRoom(this, ss[1] + (ss.Length == 4?"/" + ss[2]:""), Application.Configuration);
            var name = ss.Last().Split('.')[0];
            var pss = ss.Last().Split('?');
            if (pss.Length == 2)
            {
                var ps = pss[1].GetURLParam();
            }
            var writer = new StreamWriter(OutputBuffer);
            writer.WriteLine("HTTP/1.1 200 OK");
            writer.WriteLine("Content-Type: video/mp4");
            writer.WriteLine("Connection = Keep-Alive");
            writer.WriteLine("Transfer-Encoding = chunked");
            writer.WriteLine("");
            var stream = new OutNetMP4RTMPStream(this, Application.StreamsManager, name) {Writer = writer};
            InputBuffer.IgnoreAll();
            return true;
        }
    }
}
