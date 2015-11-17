using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Rtmp;
using CSharpRTMP.Core.Streaming;

namespace Core.Protocols.Rtmp
{
    [StreamType(StreamTypes.ST_OUT_FILE_RTMP_FLV, StreamTypes.ST_IN_NET_CLUSTER, StreamTypes.ST_IN_NET_RTMP,StreamTypes.ST_IN_NET_RTMFP)]
    public class OutFileRTMPFLVStream:BaseOutFileStream<BaseProtocol>
    {
        private double? _timeBase;
        //对于append模式使用
        private double _timeOffset = 0;
        //private uint _preTagSize;
        private MediaFile _file;

        public bool Appending;

        private readonly MemoryStream _audioBuffer = Utils.Rms.GetStream();
        private readonly MemoryStream _videoBuffer = Utils.Rms.GetStream();
        public OutFileRTMPFLVStream(BaseProtocol pProtocol, StreamsManager pStreamsManager,string filePath, string name) : base(pProtocol, pStreamsManager, filePath, name)
        {
            
        }

        public override void Dispose()
        {
            base.Dispose();
            _file?.Dispose();
        }

        public override void SendStreamMessage(BufferWithOffset buffer)
        {
            _file.Bw.Write(Defines.RM_HEADER_MESSAGETYPE_FLEXSTREAMSEND);
            _file.Bw.Write24(buffer.Length);
            _file.Bw.WriteS32(0);
            _file.Bw.Write24(0);
            _file.DataStream.Write(buffer.Buffer,buffer.Offset,buffer.Length);
            _file.Bw.Write((uint)buffer.Length + 11);
        }

        public override bool FeedData(Stream pData, uint dataLength, uint processedLength, uint totalLength, uint absoluteTimestamp,
            bool isAudio)
        {
            if (!_timeBase.HasValue) _timeBase = absoluteTimestamp;
            var buffer = isAudio ? _audioBuffer : _videoBuffer;
            pData?.CopyDataTo(buffer,(int)dataLength);
            if (buffer.Length> totalLength)
            {
                Logger.FATAL("Invalid video input");
                return false;
            }
            if (buffer.Length < totalLength)  return true;
            TotalBytes += dataLength;
            _file.WriteFlvTag(buffer, (int)(absoluteTimestamp - _timeBase + _timeOffset), isAudio);

            return true;
        }

        public override void SignalAttachedToInStream()
        {
            if (Appending && File.Exists(FilePath))
            {
                var file = MediaFile.Initialize(FilePath);
                file.DataStream.Seek(-4, SeekOrigin.End);
                var lastTagSize = file.Br.ReadUInt32();
                file.DataStream.Seek(-lastTagSize - 4, SeekOrigin.End);
                var type = file.Br.ReadByte();
                var tagDataLength = 0;
                file.ReadInt24(out tagDataLength);
                var timeStamp = file.Br.ReadSU32();
                //file.ReadSUI32(out timeStamp);
                _timeOffset = timeStamp;
                file.Dispose();
                _file = MediaFile.Initialize(FilePath, FileMode.Append, FileAccess.Write);
            }
            else
            {
                //1. Initialize the file
                _file = MediaFile.Initialize(FilePath, FileMode.Create, FileAccess.Write);
                if (_file == null)
                {
                    Logger.FATAL("Unable to initialize file {0}", FilePath);
                    Protocol.EnqueueForDelete();
                    return;
                }
                try
                {
                    _file.WriteFlvHead();
                    
                }
                catch (Exception ex)
                {
                    Logger.FATAL("Unable to initialize file {0}", ex.ToString());
                    Protocol.EnqueueForDelete();
                }
                _timeOffset = 0;
            }
            //8. Set the timebase to unknown value
            _timeBase = null;
        }

        public override void SignalDetachedFromInStream()
        {
            if (_file != null)
            {
                this.Log().Info("dispose file writer:{0}", _file.FilePath);
                _file.Dispose();
            }
        }

    }
}
