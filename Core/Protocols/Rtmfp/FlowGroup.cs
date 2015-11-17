using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class FlowGroup:Flow
    {
        private Group _group;
        public const string Signature="\x00\x47\x43";
        public const string _Name = "NetGroup";

        public FlowGroup(ulong id, Peer peer, BaseRtmfpProtocol handler, Session band, FlowWriter flowWriter)
            : base(id, Signature, _Name, peer, handler, band, flowWriter)
        {
        }

        public override void Dispose()
        {
            base.Dispose();
            if (_group!=null)Peer.UnjoinGroup(_group); 
        }

        protected override void RawHandler(byte type, Stream data)
        {
            if (type == 1)
            {
                if (data.GetAvaliableByteCounts() > 0)
                {
                    int size = (int) (data.Read7BitValue() - 1);
                    var flag = data.ReadByte();
                    byte[] groupId;
                    if (flag == 0x10)
                    {
                        var groupIdVar = new byte[size];
                        data.Read(groupIdVar, 0, size);
                        groupId = Target.Sha256.ComputeHash(groupIdVar, 0, size);
                    }
                    else
                    {
                        groupId = new byte[RtmfpUtils.ID_SIZE];
                        data.Read(groupId, 0, RtmfpUtils.ID_SIZE);
                    }
                    var groupIdStr =groupId.BytesToString();
                    if (Handler.Groups.ContainsKey(groupIdStr))
                    {
                        _group = Handler.Groups[groupIdStr];
                        Peer.JoinGroup(_group,Writer);
                    }
                    else
                    {
                        _group = Peer.JoinGroup(groupId, Writer);
                    }
                }
            }
            else base.RawHandler(type, data);
        }
    }
}
