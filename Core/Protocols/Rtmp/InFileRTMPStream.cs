using System;
using System.IO;
using System.Linq;
using System.Net;
using CSharpRTMP.Common;
using CSharpRTMP.Core.MediaFormats;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Rtmp;
using CSharpRTMP.Core.Streaming;
using static CSharpRTMP.Common.Defines;
using static CSharpRTMP.Common.Logger;
using static CSharpRTMP.Core.Streaming.StreamTypes;

namespace Core.Protocols.Rtmp
{
    [StreamType(ST_IN_FILE_RTMP, ST_OUT_NET_RTMP,ST_OUT_NET_RTMFP)]
    public class InFileRTMPStream:BaseInFileStream
    {
        class BaseBuilder
        {
            public virtual bool BuildFrame(MediaFile file, MediaFrame mediaFrame, Stream buffer)
            {
                if (!file.SeekTo((long)mediaFrame.Start))
                {
                    FATAL("Unable to seek to position {0}", mediaFrame.Start);
                    return false;
                }
                //3. Read the data
                file.DataStream.CopyPartTo(buffer, (int)mediaFrame.Length);
                buffer.Position = 0;
                return true;
            }
        }
   
        class AVCBuilder : BaseBuilder
        {
            readonly byte[] _videoCodecHeaderInit = { 0x17, 0, 0, 0, 0 };
            readonly byte[] _videoCodecHeaderKeyFrame = { 0x17, 1 };
            readonly byte[] _videoCodecHeader = { 0x27, 1 };
            public override bool BuildFrame(MediaFile file, MediaFrame mediaFrame, Stream buffer)
            {
               
                if (mediaFrame.IsBinaryHeader)
                {
                    buffer.Write(_videoCodecHeaderInit,0, _videoCodecHeaderInit.Length);
                }
                else
                {
                    if (mediaFrame.IsKeyFrame)
                    {
                        // video key frame
                        buffer.Write(_videoCodecHeaderKeyFrame, 0,_videoCodecHeaderKeyFrame.Length);
                    }
                    else
                    {
                        //video normal frame
                        buffer.Write(_videoCodecHeader, 0, _videoCodecHeader.Length);
                    }
                    mediaFrame.CompositionOffset = IPAddress.HostToNetworkOrder(mediaFrame.CompositionOffset & 0x00ffffff) >> 8;
                    buffer.Write(BitConverter.GetBytes(mediaFrame.CompositionOffset), 0, 3);
                }

                return base.BuildFrame(file, mediaFrame, buffer);
            }
        }

        class AACBuilder : BaseBuilder
        {
            private readonly byte[] _audioCodecHeaderInit = { 0xaf, 0 };
            private readonly byte[] _audioCodecHeader = { 0xaf, 0x01 };
            public override bool BuildFrame(MediaFile file, MediaFrame mediaFrame, Stream buffer)
            {
               
                //1. add the binary header
                if (mediaFrame.IsBinaryHeader)
                {
                    buffer.Write(_audioCodecHeaderInit,0, _audioCodecHeaderInit.Length);
                }
                else
                {
                    buffer.Write(_audioCodecHeader, 0, _audioCodecHeader.Length);
                }
                return base.BuildFrame(file, mediaFrame, buffer);
            }
        }

        class MP3Builder : BaseBuilder
        {
            public override bool BuildFrame(MediaFile file, MediaFrame mediaFrame, Stream buffer)
            {
                buffer.WriteByte(0x2f);
                return base.BuildFrame(file, mediaFrame, buffer);
            }
        }


        BaseBuilder _pAudioBuilder;
        BaseBuilder  _pVideoBuilder;

 
        string _metadataName;
        Variant _metadataParameters = Variant.Get();

        public Variant CompleteMetadata;
        public InFileRTMPStream(BaseProtocol pProtocol, StreamsManager pStreamsManager, string name) : base(pProtocol, pStreamsManager, name)
        {
            
            base.ChunkSize = 4 * 1024 * 1024;
        }

        public override bool Initialize(int clientSideBufferLength)
        {
            if (! base.Initialize(clientSideBufferLength))
            {
                FATAL("Unable to initialize stream");
		        return false;
            }
            //2. Get stream capabilities
	        StreamCapabilities pCapabilities = Capabilities;
	        if (pCapabilities == null) {
		        FATAL("Invalid stream capabilities");
		        return false;
	        }
                    //3. Create the video builder
	      
	        switch (pCapabilities.VideoCodecId)
	        {
	            case VideoCodec.H264:
	                _pVideoBuilder = new AVCBuilder();
	                break;
	            case VideoCodec.PassThrough:
	                _pVideoBuilder = new BaseBuilder();
	                break;
	            case VideoCodec.Unknown:
	                WARN("Invalid video stream capabilities:{0}", pCapabilities.VideoCodecId);
	                break;
	            default:
	                FATAL("Invalid video stream capabilities:{0}", pCapabilities.VideoCodecId);
	                return false;
	        }

            //4. Create the audio builder
	        
	        switch (pCapabilities.AudioCodecId)
	        {
	            case AudioCodec.Aac:
	                _pAudioBuilder = new AACBuilder();
	                break;
	            case AudioCodec.Mp3:
	                _pAudioBuilder = new MP3Builder();
	                break;
	            case AudioCodec.PassThrough:
	                _pAudioBuilder = new BaseBuilder();
	                break;
                case AudioCodec.Unknown:
	                WARN("Invalid audio stream capabilities: {0}", pCapabilities.AudioCodecId);
	                break;
	            default:
	                FATAL("Invalid audio stream capabilities: {0}", pCapabilities.AudioCodecId);
	                return false;
	        }
            _amf0Reader = new AMF0Reader(_pFile.DataStream);
            _pFile.Br = _amf0Reader;
            return true;
        }

        private AMF0Reader _amf0Reader;
        protected override bool FeedOtherType()
        {
            if (_currentFrame.Type == MediaFrameType.Message && OutStreams.Last() is BaseOutNetRTMPStream)
            {
                if (!_pFile.SeekTo(_currentFrame.Start))
                {
                    FATAL("Unable to seek to position {0}", _currentFrame.Start);
                    return false;
                }
                var buffer = new BufferWithOffset(_amf0Reader.BaseStream, false, (int) _currentFrame.Length);
                SendStreamMessage(buffer);
                _pFile.Position = _currentFrame.Start + _currentFrame.Length;
                //message.Recycle();
                _currentFrameIndex++;
                return true;
            }
            //todo 这里会导致播放中断，对于其他协议要修改
            Paused = true;
            return base.FeedOtherType();
        }

        public static InFileRTMPStream GetInstance(BaseProtocol pRTMPProtocol, StreamsManager pStreamsManager,
            Variant metadata)
        {
            metadata[META_RTMP_META,HTTP_HEADERS_SERVER]= HTTP_HEADERS_SERVER_US;
	        if (!File.Exists(metadata[META_SERVER_FULL_PATH])) {
		        WARN("File not found. fullPath: `{0}`",metadata[META_SERVER_FULL_PATH]);
		        return null;
	        }
	        InFileRTMPStream pResult = null;
	        switch ((string)metadata[META_MEDIA_TYPE])
	        {
	            case MEDIA_TYPE_FLV:
	            case MEDIA_TYPE_LIVE_OR_FLV:
	            case MEDIA_TYPE_MP3:
	            case MEDIA_TYPE_MP4:
	            case MEDIA_TYPE_M4A:
	            case MEDIA_TYPE_M4V:
	            case MEDIA_TYPE_MOV:
	                pResult = new InFileRTMPStream(pRTMPProtocol,
	                    pStreamsManager,metadata[META_SERVER_FULL_PATH]);
	                break;
	            default:
	                FATAL("File type not supported yet. Metadata:\n{0}",
	                    metadata);
	                break;
	        }

	        if (pResult != null) {
		        pResult.CompleteMetadata = metadata;
	        }

	        return pResult;
        }
        //public override bool IsCompatibleWithType(ulong type)
        //{
        //    return type.TagKindOf(StreamTypes.ST_OUT_NET_RTMP);
        //}

        public override void SignalOutStreamAttached(IOutStream pOutStream)
        {
           //2. Set a big chunk size on the corresponding connection
	        if (pOutStream.Type.TagKindOf(ST_OUT_NET_RTMP))
	        {
                ((BaseOutNetRTMPStream)pOutStream).TrySetOutboundChunkSize(ChunkSize);
               
	        }
            base.SignalOutStreamAttached(pOutStream);
            ((IInFileRTMPStreamHolder)Protocol).OnReadyForSend += ReadyForSend;
        }

        public override void SignalOutStreamDetached(IOutStream pOutStream)
        {
            base.SignalOutStreamDetached(pOutStream);
            ((IInFileRTMPStreamHolder)Protocol).OnReadyForSend -= ReadyForSend;
            FINEST("outbound stream {0} detached from inbound stream {1}",pOutStream.UniqueId,UniqueId);
            if (OutStreams.Count == 0)
            {
                Dispose();
            }
        }

        protected override bool BuildFrame(MediaFile pFile, MediaFrame mediaFrame, Stream buffer)
        {
           switch (mediaFrame.Type) {
		        case MediaFrameType.Audio:
			        return _pAudioBuilder == null || _pAudioBuilder.BuildFrame(pFile, mediaFrame, buffer);
               case MediaFrameType.Video:
			        return _pVideoBuilder == null || _pVideoBuilder.BuildFrame(pFile, mediaFrame, buffer);
               default:
			        return true;
	        }
        }

        protected override bool FeedMetaData(MediaFile pFile, MediaFrame mediaFrame)
        {
            //1. Seek into the data file at the correct position
	        if (!pFile.SeekTo( mediaFrame.Start)) {
		        FATAL("Unable to seek to position {0}", mediaFrame.Start);
		        return false;
	        }
            var endPosition = pFile.Position + (long)mediaFrame.Length;
	        //2. Read the data
            //_metadataBuffer.IgnoreAll();
            //if (!_metadataBuffer.ReadFromFs(pFile, (int) mediaFrame.Length)) {
            //    Logger.FATAL("Unable to read {0} bytes from offset {1}", mediaFrame.Length, mediaFrame.Start);
            //    return false;
            //}
            
	        //3. Parse the metadata
	        _metadataName = "";
	        _metadataParameters.SetValue();

            var _tempVariant = _amf0Reader.ReadVariant();
            //if (!_amfSerializer.Read(_metadataBuffer, _tempVariant)) {
            //    Logger.WARN("Unable to read metadata");
            //    return true;
            //}
	        if (_tempVariant != VariantType.String) {
		        WARN("Unable to read metadata");
		        return true;
	        }
            _metadataName = _tempVariant;

            while (pFile.Position < endPosition)
                _metadataParameters.Add(_amf0Reader.ReadVariant());

            var message = GenericMessageFactory.GetNotify(
			        ((BaseOutNetRTMPStream ) OutStreams.Last()).CommandsChannelId,
			        ((BaseOutNetRTMPStream ) OutStreams.Last()).RTMPStreamId,
			        mediaFrame.AbsoluteTime,
			        true,
			        _metadataName,
			        _metadataParameters);

	        //5. Send it
	        return ((BaseRTMPProtocol ) Protocol).SendMessage(message,true);
        }
    }
}
