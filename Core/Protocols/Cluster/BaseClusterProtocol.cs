using System;
using System.Collections.Generic;

using System.IO;
using System.Linq;
using Core.Protocols.Rtmp;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Rtmp;
using CSharpRTMP.Core.Streaming;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Core.Protocols.Cluster
{
    public class ReUseBsonReader : BsonReader
    {
        public ReUseBsonReader(Stream stream) : base(stream)
        {
            SupportMultipleContent = true;
        }

        public void Reset()
        {
            SetToken(JsonToken.None);
        }
    }
    public enum ClusterMessageType
    {
        Unknow,GetAppId, BroadCast, Play, Publish, StopPublish, Video, Audio, NoSubscriber, Call, StreamMessage, SharedObjectTrack
    }
    public abstract class BaseClusterProtocol:BaseProtocol
    {
        public readonly HashSet<SO> SOs = new HashSet<SO>();
        public Dictionary<uint, InClusterStream> InStreams = new Dictionary<uint, InClusterStream>();
        private uint _outStreamIdGenerator;
        private int _waitlength;
        private ClusterMessageType _currentType;
        public Variant Deserialize()
        {
           return (Variant)Utils.BinaryFormatter.Deserialize(InputBuffer);
        }
        public void Send(ClusterMessageType type,Action<H2NBinaryWriter> writeAction)
        {
            var ms = Utils.Rms.GetStream();
            using (var writer = new H2NBinaryWriter(ms))
            {
                writer.Write((byte)type);
                writer.Write((ushort)0);
                writeAction(writer);
                var length = ms.Length - 3;
                ms.Position = 1;
                writer.Write((ushort)length);
                EnqueueForOutbound(ms);
            }
        }
        public void Send(ClusterMessageType type)
        {
            Send(type, output => { });
        }
       
        public void Send(ClusterMessageType type, object message)
        {
            var v = Variant.Get(message);
            Send(type, output => output.Write(v.ToBytes()));
            v.Recycle();
        }

        public void Send(ClusterMessageType type, InputStream message)
        {
            Send(type, output => message.CopyDataTo(output.BaseStream));
        }
        public void Send(ClusterMessageType type, uint streamId, Stream pData, uint dataLength, uint processedLength, uint totalLength, uint absoluteTimestamp)
        {
            Send(type, output =>
            {
                output.Write7BitValue(streamId);
                output.Write7BitValue(dataLength);
                output.Write7BitValue(processedLength);
                output.Write7BitValue(totalLength);
                output.Write7BitValue(absoluteTimestamp);
                pData.CopyDataTo(output.BaseStream, (int)dataLength);
            });
        }
        public void PublishStream(uint appId, IInStream inNetStream,string type = "live")
        {
            _outStreamIdGenerator++;
            var outStream = new OutClusterStream(this, GetRoom(appId).StreamsManager, inNetStream.Name, _outStreamIdGenerator);
            outStream.Link(inNetStream);
            //Send(ClusterMessageType.Publish, new { AppId = appId, inNetStream.Name, inNetStream.Type, outStream.StreamId, inNetStream.ChunkSize ,PublishType=type});
            Send(ClusterMessageType.Publish, o =>
            {
                o.Write7BitValue(appId);
                o.Write(inNetStream.Name);
                o.Write(inNetStream.Type);
                o.Write7BitValue(outStream.StreamId);
                o.Write7BitValue(inNetStream.ChunkSize);
                o.Write(type);
            });
        }

        public void Broadcast(uint appId, BaseProtocol pFrom, Variant invokeInfo)
        {
            Logger.INFO("SendBroadcast to {1}:{0}",invokeInfo,appId);
            Send(ClusterMessageType.BroadCast, o =>
            {
                o.Write7BitValue(appId);
                o.Write(invokeInfo.ToBytes());
            });
        }

        public void PlayStream(uint appId, string streamName)
        {
            Send(ClusterMessageType.Play, output =>
            {
                output.Write7BitValue(appId);
                output.Write(streamName);
            });
        }
        
        public void SharedObjectTrack(BaseClientApplication app, string name, uint version, bool isPersistent, Variant primitives)
        {
            SlaveClusterAppProtocolHandler.GotAppIdDelegate task = appId =>
            {
                Send(ClusterMessageType.SharedObjectTrack, o =>
                {
                    o.Write7BitValue(appId);
                    o.Write(name);
                    o.Write7BitValue(version);
                    o.Write(isPersistent);
                    o.Write(primitives.ToBytes());
                });
                primitives.Recycle();
            };
            if (app.Id == 0)
            {
                var gotAppIdTasks = Application.GetProtocolHandler<SlaveClusterAppProtocolHandler>().GotAppIdTasks;
                if (gotAppIdTasks.ContainsKey(app.Name))
                {
                    gotAppIdTasks[app.Name] += task;
                }
                else
                {
                    gotAppIdTasks[app.Name] = task;
                }
            }
            else
            {
                task(app.Id);
            }
           
        }

        public override bool SignalInputData(int recAmount)
        {
            do
            {
                if (_waitlength == 0)
                {
                    _currentType = (ClusterMessageType) InputBuffer.ReadByte();
                    _waitlength = InputBuffer.Reader.ReadUInt16();
                }
                if (InputBuffer.AvaliableByteCounts >= _waitlength)
                {
                    var pos = InputBuffer.Position;
                    OnReceive(_currentType);
                    if (pos + _waitlength != InputBuffer.Position)
                        InputBuffer.Ignore((uint) (_waitlength + pos - InputBuffer.Position));
                    _waitlength = 0;
                }
            } while (_waitlength == 0 && InputBuffer.AvaliableByteCounts > 2);
            return true;
        }

        protected  virtual void FeedData(bool isAudio)
        {
            var streamId = InputBuffer.Reader.Read7BitValue();
            if (InStreams.ContainsKey(streamId))
            {
                var dataLength = InputBuffer.Reader.Read7BitValue();
                var processLength = InputBuffer.Reader.Read7BitValue();
                var totalLength = InputBuffer.Reader.Read7BitValue();
                var abs = InputBuffer.Reader.Read7BitValue();
                InStreams[streamId].FeedData(InputBuffer, dataLength, processLength, totalLength, abs, isAudio);
            }
        }
        protected virtual void OnReceive(ClusterMessageType type)
        {
            uint streamId;
            string roomName=null;
            Variant message;
            uint appId;
            switch (type)
            {
                case ClusterMessageType.SharedObjectTrack:
                    appId = InputBuffer.Reader.Read7BitValue();
                    var soName = InputBuffer.Reader.ReadString();
                    var version = InputBuffer.Reader.Read7BitValue();
                    var isPersistent = InputBuffer.Reader.ReadBoolean();
                    message = Deserialize();
                   
                    Logger.INFO("SharedObjectTrack:{0},{1}", appId, soName);
                    GetRoom(appId).SOManager.Process(this, soName, isPersistent, message);
                    break;
                case ClusterMessageType.BroadCast:

                    appId = InputBuffer.Reader.Read7BitValue();
                    message = Deserialize();
                    Logger.INFO("ReceiveBroadcast from {1}:{0}", message, appId);
                    GetRoom(appId).Broadcast(this, message);
                    break;
                case ClusterMessageType.Call:
                    appId = InputBuffer.Reader.Read7BitValue();
                    var functionName = InputBuffer.Reader.ReadString();
                    message = Deserialize();
                    GetRoom(appId).CallFunction(functionName, this, message);
                    break;
                case ClusterMessageType.Audio:
                    FeedData(true);
                    break;
                case ClusterMessageType.Video:
                    FeedData(false);
                    break;
                case ClusterMessageType.StopPublish:
                    streamId = InputBuffer.Reader.Read7BitValue();
                    Logger.INFO("StopPublish:" + streamId);
                    if (InStreams.ContainsKey(streamId))
                    {
                        InStreams[streamId].Dispose();
                        InStreams.Remove(streamId);
                    }
                    break;
                case ClusterMessageType.StreamMessage:
                    streamId = InputBuffer.Reader.Read7BitValue();
                    var length = (int)InputBuffer.Reader.Read7BitValue();
                    var buffer = new byte[length];
                    InputBuffer.Reader.Read(buffer, 0, length);
                    //message = Deserialize();
                    if (InStreams.ContainsKey(streamId))
                    {
                        InStreams[streamId].SendStreamMessage(new BufferWithOffset(buffer));
                    }
                    break;
                case ClusterMessageType.Unknow:
                    Logger.WARN("Unknow Type!");
                    break;
                default:
                    Logger.WARN("Unknow Type :"+type);
                    break;
            }
        }

        public BaseClientApplication GetRoom(uint appId)
        {
            return ClientApplicationManager.ApplicationById[appId];
        }
        public BaseClientApplication GetRoom(string appName)
        {
            return ClientApplicationManager.ApplicationByName[appName];
        }
    }
}
