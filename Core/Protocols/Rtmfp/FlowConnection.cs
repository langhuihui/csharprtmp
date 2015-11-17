using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Rtmp;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class FlowConnection:Flow
    {
        public const string Signature = "\x00\x54\x43\x04\x00";
        public const string _Name = "NetConnection";
        public FlowConnection(ulong id, Peer peer, BaseRtmfpProtocol handler, Session band,FlowWriter flowWriter)
            : base(id, Signature, _Name, peer, handler, band, flowWriter)
        {
            Writer.Critical = true;
        }

        
        private readonly HashSet<uint> _streamIndex = new HashSet<uint>(); 
        protected override void MessageHandler(string name, Variant param)
        {
           this.Log().Info(name);
            AMF0Writer response3;
            switch (name)
            {
                case "_result":
                    //Logger.Debug("{0}", param.ToString());
                    uint handler = (uint) Writer.CallbackHandle;
                    Band.CallBacks[handler](this,param);
                    
                    break;
                case "connect":
                    //message.Referencing = false;
                    //obj = message.ReadVariant();
                    //message.Referencing = true;
                    var obj = param[0];
                    Peer.SWFUrl = obj["swfUrl"];
                    Peer.PageUrl = obj["pageUrl"];
                    Peer.FlashVer = obj["flashVer"];
                    if (obj["objectEncoding"]!=null && (double)obj["objectEncoding"] == 0)
                    {
                        Writer.WriteErrorResponse("Connect.Rejected", "ObjectEncoding client must be in a AMF3 format (not AMF0)");
                        return;
                    }
                    Peer.FlowWriter = Writer;
                    Writer.BeginTransaction();
                    bool accept;
                    
                    using (var response = Writer.WriteSuccessResponse("Connect.Success", "Connection succeeded"))
                    {
                        response["objectEncoding"] = 3.0;
                        accept = Peer.OnConnection(param, Band as Session, response);
                    }
                    if (!accept)
                    {
                        Writer.EndTransaction(1);
                        Writer.WriteAMFMessage("close");
                        Writer.Close();
                    }
                    else
                    {
                        Writer.EndTransaction();
                    }
                    break;

                case "setPeerInfo":
                    Peer.Addresses.RemoveRange(1,Peer.Addresses.Count-1);
                    foreach (var value in param.Children.Values.Skip(1))
                    {
                        string address = value;
                        var index = address.LastIndexOf(':');
                        Peer.Addresses.Add(new IPEndPoint(IPAddress.Parse(address.Substring(0, index)), Convert.ToInt32(address.Substring(index + 1))));
                    }

                    //while (message.Available)
                    //{
                    //    var address = message.Read<string>();
                    //    var index = address.LastIndexOf(':');
                    //    Peer.Addresses.Add(new IPEndPoint(IPAddress.Parse(address.Substring(0,index)), Convert.ToInt32(address.Substring(index+1))));
                    //}
                    var response2 = Writer.WriterRawMessage();
                    response2.Write((short)0x29);

                    //response2.Write(Handler.KeepAliveServer);
                    response2.Write(15 * 1000);
                    //response2.Write(Handler.KeepAlivePeer);
                    response2.Write(10 * 1000);
                    break;
                case "initStream":
                    break;
                case "createStream":
                    response3 = Writer.WriteAMFResult();
                    var streamId = Handler.CreateStream();
                    if (response3.AMF0Preference)
                        response3.WriteDouble(streamId, true);
                    else
                    {
                        response3.Write(AMF0Serializer.AMF0_AMF3_OBJECT);
                        response3.Write(AMF3Serializer.AMF3_INTEGER);
                        response3.Write7BitValue(streamId);
                        //new AMF3Writer(response3.BaseStream).WriteInterger(streamId, true);
                    }
                    break;
                case "deleteStream":
                    var sindex = (uint)param[1];
                    _streamIndex.Remove(sindex);
                    Handler.DestoryStream(sindex);
                    break;
                default:
                    var info = Band.Application.CallCustomFunction(Band,name, param);
                    if (info != null)
                    {
                        response3 = Writer.WriteAMFResult();
                        response3.WriteVariant(info);
                    }
                   
                    //response3.WriteObject(info, true);
                    //response3.Write((byte)1);
                    //if(!Peer.OnMessage(name,message))
                    //    Writer.WriteErrorResponse("Call.Failed","Method '"+name+"' not found");
                    break;
            }
        }

        //protected override void RawHandler(byte type, Stream data)
        //{
        //    var flag = data.ReadUShort();
        //    if (flag != 0x03)
        //    {
        //        base.RawHandler(type,data);
        //        return;
        //    }
        //    var streamId = data.ReadUInt();
        //    if (streamId > 0)
        //    {
        //        Logger.INFO("setBufferTime {0} on stream {1}", data.ReadUInt(), streamId);
        //        var raw = Writer.WriterRawMessage();
        //        raw.Write((ushort)0);
        //        raw.Write(streamId);
        //    }
        //}

        public override void Dispose()
        {
            foreach (var streamIndex in _streamIndex)
            {
                
            }
            base.Dispose();
        }

      
    }
}
