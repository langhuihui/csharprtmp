using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Streaming;
using System.Threading.Tasks;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
     [StreamType(StreamTypes.ST_OUT_NET_RTMFP,StreamTypes.ST_IN_NET_CLUSTER, StreamTypes.ST_IN_NET_RTMP, StreamTypes.ST_IN_NET_LIVEFLV, StreamTypes.ST_IN_FILE_RTMP, StreamTypes.ST_IN_NET_MP3, StreamTypes.ST_IN_NET_RTMFP)]
    public class OutNetRtmfpStream:BaseOutNetStream<Session>
    {
        public bool ReceiveAudio = true;
        public bool ReceiveVideo = true;
        public bool AudioSampleAccess;
        public bool VideoSampleAccess;
        private uint _time;
        public uint Id;
        private uint _addingTime;
        private long _deltaTime = -1;
        private bool _firstVideo = true;
        private bool _firstAudio = true;
        public FlowWriter Writer;
        private StreamWriter _audioWriter;
        private StreamWriter _videoWriter;
        public bool Unbuffered;
        private uint _boundId;
         private bool _paused;

         public OutNetRtmfpStream(Session pProtocol, StreamsManager pStreamsManager, uint id, string name)
            : base(pProtocol, pStreamsManager, name)
        {
            Id = id;

        }
        public void Init(/*Peer client*/)
        {
            Writer.WriteStatusResponse("Play.Reset", "Playing and resetting " + Name);
            Writer.WriteStatusResponse("Play.Start", "Started playing " + Name);
            if (_audioWriter == null)
            {
                _audioWriter = Writer.NewStreamWriter(0x08);
                //_audioWriter.Client = client;
            }
            else
                Logger.WARN("Listener {0} audio track has already been initialized", Id);
            if (_videoWriter == null)
            {
                _videoWriter = Writer.NewStreamWriter(0x09);
                //_videoWriter.Client = client;
            }
            else
            {
                Logger.WARN("Listener {0} video track has already been initialized", Id);
            }
            WriteBounds();
        }
        //public override bool IsCompatibleWithType(ulong type)
        //{
        //    return type.TagKindOf(StreamTypes.ST_IN_NET_RTMP)
        //    || type.TagKindOf(StreamTypes.ST_IN_NET_LIVEFLV)
        //    || type.TagKindOf(StreamTypes.ST_IN_FILE_RTMP)
        //    || type.TagKindOf(StreamTypes.ST_IN_NET_MP3) 
        //    || type.TagKindOf(StreamTypes.ST_IN_NET_RTMFP);
        //}
        public override bool SignalPlay(ref double absoluteTimestamp, ref double length)
        {
            _paused = false;
            return true;
        }

        public override void SignalAttachedToInStream()
        {
            
        }

        public override void SignalDetachedFromInStream()
        {
            Writer.WriteStatusResponse("Play.Stop", "Stopped playing " + Name);
        }

        public override void SignalStreamCompleted()
        {
            Writer.WriteStatusResponse("Play.Completed", "Completed playing " + Name);
        }

         public override void SendStreamMessage(BufferWithOffset buffer)
         {
            int skipCount = buffer[2];
            skipCount =  ((skipCount << 8) | buffer[3]);
            buffer.Offset = skipCount + 4;
            Writer.WriteAMFPacket(Encoding.UTF8.GetString(buffer.Buffer, 4, skipCount)).Write(buffer.Buffer, buffer.Offset, buffer.Length);
         }

         public override bool FeedData(Stream pData, uint dataLength, uint processedLength, uint totalLength, uint absoluteTimestamp,
            bool isAudio)
        {
            if (isAudio && !ReceiveAudio || !isAudio && !ReceiveVideo) return true;
            var streamWriter = isAudio ? _audioWriter : _videoWriter;
            if (streamWriter.Reseted)
            {
                streamWriter.Reseted = false;
                WriteBounds();
            }
            streamWriter.Write(ComputeTime(absoluteTimestamp), pData, Unbuffered, processedLength == 0, (int)dataLength);
            //if (totalLength == dataLength + processedLength) streamWriter.Flush();
            //Writer.Flush(true);
            return true;
        }

        public void Flush()
        {
            _audioWriter?.Flush();
            _videoWriter?.Flush();
            Writer.Flush(true);
        }
        private void WriteBounds()
        {
            if (_videoWriter != null) WriteBound(_videoWriter);
            if (_audioWriter != null) WriteBound(_audioWriter);
            WriteBound(Writer);
            _boundId++;
        }

        private void WriteBound(FlowWriter writer)
        {
            var data = writer.WriterRawMessage();
            data.Write((ushort)0x22);
            data.Write(_boundId);
            data.Write(3);
        }
        public override void SendPublishNotify()
        {
            Writer.WriteStatusResponse("Play.PublishNotify", Name + " is now published");
        }

        public override void SendUnpublishNotify()
        {
            _deltaTime = -1;
            _addingTime = _time;
            Writer.WriteStatusResponse("Play.UnpublishNotify", Name + " is now published");
        }
        private uint ComputeTime(uint time)
        {
            if (_deltaTime < 0 || _deltaTime > time) _deltaTime = time;
            return _time = (uint)(time - _deltaTime + _addingTime);
        }

    }
}
