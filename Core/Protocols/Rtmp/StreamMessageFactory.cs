using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Rtmp;

namespace Core.Protocols.Rtmp
{
    public static class StreamMessageFactory
    {
        public static AmfMessage GetUserControlStream(ushort operation,uint streamId)
        {
            AmfMessage result;
            result.Header = GenericMessageFactory.VH(HeaderType.HT_FULL, 2, 0, 0, Defines.RM_HEADER_MESSAGETYPE_USRCTRL,
                0, true);
            result.Body = Variant.GetMap(new VariantMapHelper
            {
                {
                    Defines.RM_USRCTRL, Variant.GetMap(new VariantMapHelper
                    {
                        {Defines.RM_USRCTRL_TYPE, operation},
                        {Defines.RM_USRCTRL_TYPE_STRING, RTMPProtocolSerializer.GetUserCtrlTypeString(operation)},
                        {Defines.RM_USRCTRL_STREAMID, streamId}
                    })
                }
            });
            return result;
        }

        public static AmfMessage GetUserControlStreamBegin(uint streamId)
        {
            return GetUserControlStream(Defines.RM_USRCTRL_TYPE_STREAM_BEGIN, streamId);
        }

        public static AmfMessage GetUserControlStreamEof(uint streamId)
        {
            return GetUserControlStream(Defines.RM_USRCTRL_TYPE_STREAM_EOF, streamId);
        }

        public static AmfMessage GetUserControlStreamDry(uint streamId)
        {
            return GetUserControlStream(Defines.RM_USRCTRL_TYPE_STREAM_DRY, streamId);
        }

        public static AmfMessage GetUserControlStreamIsRecorded(uint streamId)
        {
            return GetUserControlStream(Defines.RM_USRCTRL_TYPE_STREAM_IS_RECORDED, streamId);
        }

        public static AmfMessage GetInvokeCreateStream()
        {
            //var createStream = Variant.CreateMap();
            //createStream[0] = new Variant();
            return GenericMessageFactory.GetInvoke(3, 0, 0, false, 1, Defines.RM_INVOKE_FUNCTION_CREATESTREAM,
                 Variant.GetList(Variant.Get()));
        }

        public static AmfMessage GetInvokeCloseStream(uint channelId, uint streamId)
        {
            //Variant closeStream= Variant.CreateMap();
            //closeStream[0] =new Variant();
            return GenericMessageFactory.GetInvoke(channelId, streamId, 0, false, 1, Defines.RM_INVOKE_FUNCTION_CLOSESTREAM,
              Variant.GetList(Variant.Get()));
        }
        public static AmfMessage GetInvokeDeleteStream(uint channelId, uint streamId)
        {
            return GenericMessageFactory.GetInvoke(channelId, streamId, 0, false, 1, Defines.RM_INVOKE_FUNCTION_DELETESTREAM,
               Variant.GetList(Variant.Get()));
        }
        public static AmfMessage GetInvokePublish(uint channelId, uint streamId, string streamName, string mode)
        {
            return GenericMessageFactory.GetInvoke(channelId, streamId, 0, false, 1, Defines.RM_INVOKE_FUNCTION_PUBLISH,
             Variant.GetList(Variant.Get(), streamName, mode ));
        }

        public static AmfMessage GetInvokePlay(uint channelId, uint streamId,
        string streamName, double start, double length)
        {
            return GenericMessageFactory.GetInvoke(channelId, streamId, 0, false, 1, Defines.RM_INVOKE_FUNCTION_PLAY,
              Variant.GetList(Variant.Get(), streamName, start, length ));
        }

        public static AmfMessage GetInvokeFCSubscribe(string streamName)
        {
            return GenericMessageFactory.GetInvoke(3, 0, 0, false, 1, Defines.RM_INVOKE_FUNCTION_FCSUBSCRIBE,
           Variant.GetList( Variant.Get(), streamName));
        }
        public static AmfMessage GetInvokeCreateStreamResult(AmfMessage request, double createdStreamId)
      
        {
            return GetInvokeCreateStreamResult(request.ChannelId, request.StreamId,
            request.Body[Defines.RM_INVOKE,Defines.RM_INVOKE_ID], createdStreamId);
        }
        public static AmfMessage GetInvokeCreateStreamResult(uint channelId, uint streamId, uint requestId, double createdStreamId)
        {
            return GenericMessageFactory.GetInvokeResult(channelId, streamId, requestId, Variant.Get(), Variant.Get(createdStreamId));
        }
        public static AmfMessage GetInvokeReleaseStreamResult(uint channelId, uint streamId, uint requestId, double releasedStreamId)
        {
            return GenericMessageFactory.GetInvokeResult(channelId, streamId, requestId, Variant.Get(), streamId != 0 ? Variant.Get(streamId) : Variant.Get());
        }

        public static AmfMessage GetInvokeReleaseStreamErrorNotFound(AmfMessage request)
        {
            return GenericMessageFactory.GetInvokeError(request.ChannelId, request.StreamId, request.InvokeId, Variant.Get(), Variant.GetMap(new VariantMapHelper
            {
                {Defines.RM_INVOKE_PARAMS_RESULT_LEVEL,Defines.RM_INVOKE_PARAMS_RESULT_LEVEL_ERROR},
                {Defines.RM_INVOKE_PARAMS_RESULT_CODE,"NetConnection.Call.Failed"},
                {Defines.RM_INVOKE_PARAMS_RESULT_DESCRIPTION,"Specified stream not found in call to releaseStream"}
            }));
        }

        public static AmfMessage GetInvokeOnFCPublish(uint channelId, uint streamId, double timeStamp, bool isAbsolute,
            double requestId, string code, string description)
        {
            return GenericMessageFactory.GetInvoke(channelId, streamId, timeStamp, isAbsolute, requestId, "onFCPublish",
                Variant.GetList(
                    Variant.Get(),Variant.GetMap(new VariantMapHelper{{Defines.RM_INVOKE_PARAMS_ONSTATUS_CODE,code},{Defines.RM_INVOKE_PARAMS_ONSTATUS_DESCRIPTION,description}})
                ));
        }

        public static AmfMessage GetInvokeOnStatusStreamPublishBadName(AmfMessage request, string streamName)
        {
            return GetInvokeOnStatusStreamPublishBadName(request.ChannelId, request.StreamId,request.InvokeId, streamName);
        }
        public static AmfMessage GetInvokeOnStatusStreamPublishBadName(uint channelId,
        uint streamId, double requestId, string streamName)
        {
            return GenericMessageFactory.GetInvokeOnStatus(channelId, streamId,
			0, false, requestId,  Variant.GetMap(new VariantMapHelper
			{
			    {Defines.RM_INVOKE_PARAMS_ONSTATUS_LEVEL,Defines.RM_INVOKE_PARAMS_RESULT_LEVEL_ERROR},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_CODE,"NetStream.Publish.BadName"},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_DESCRIPTION,streamName+" is not available"},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_DETAILS,streamName},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_CLIENTID,""}
			}));
        }

        public static AmfMessage GetInvokeOnStatusStreamPublished(uint channelId, uint streamId, double timeStamp,
            bool isAbsolute, double requestId, string level, string code, string description, string details,
            string clientId)
        {
            return GenericMessageFactory.GetInvokeOnStatus(channelId, streamId, timeStamp, isAbsolute, requestId,
                Variant.GetMap(new VariantMapHelper
                {
                    {Defines.RM_INVOKE_PARAMS_ONSTATUS_LEVEL,level},
                    {Defines.RM_INVOKE_PARAMS_ONSTATUS_CODE,code},
                    {Defines.RM_INVOKE_PARAMS_ONSTATUS_DESCRIPTION,description},
                    {Defines.RM_INVOKE_PARAMS_ONSTATUS_DETAILS,details},
                    {Defines.RM_INVOKE_PARAMS_ONSTATUS_CLIENTID,clientId}
                }));
        }

        public static AmfMessage GetInvokeOnStatusStreamPlayFailed(Variant request, string streamName)
        {
            return GetInvokeOnStatusStreamPlayFailed(request[Defines.RM_HEADER, Defines.RM_HEADER_CHANNELID], request[Defines.RM_HEADER, Defines.RM_HEADER_STREAMID], request[Defines.RM_INVOKE, Defines.RM_INVOKE_ID], streamName);
        }
        public static AmfMessage GetInvokeOnStatusStreamPlayFailed(uint channelId, uint streamId, double requestId, string streamName)
        {
            return GenericMessageFactory.GetInvokeOnStatus(channelId, streamId, 0, false, requestId,
                Variant.GetMap(new VariantMapHelper
                {
                    {Defines.RM_INVOKE_PARAMS_ONSTATUS_LEVEL,Defines.RM_INVOKE_PARAMS_RESULT_LEVEL_ERROR},
                    {Defines.RM_INVOKE_PARAMS_ONSTATUS_CODE,"NetStream.Play.Failed"},
                    {Defines.RM_INVOKE_PARAMS_ONSTATUS_DESCRIPTION,"Fail to play "+streamName},
                    {Defines.RM_INVOKE_PARAMS_ONSTATUS_DETAILS,streamName},
                    {Defines.RM_INVOKE_PARAMS_ONSTATUS_CLIENTID,""}
                }));
        }
        public static AmfMessage GetInvokeOnStatusStreamPlayReset(uint channelId,
        uint streamId, double timeStamp, bool isAbsolute, double requestId, string description, string details, string clientId)
        {
            return GenericMessageFactory.GetInvokeOnStatus(channelId, streamId,
            timeStamp, isAbsolute, requestId, Variant.GetMap(new VariantMapHelper
			{
			    {Defines.RM_INVOKE_PARAMS_ONSTATUS_LEVEL,Defines.RM_INVOKE_PARAMS_ONSTATUS_LEVEL_STATUS},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_CODE,Defines.RM_INVOKE_PARAMS_ONSTATUS_CODE_NETSTREAMPLAYRESET},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_DESCRIPTION,description},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_DETAILS,details},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_CLIENTID,clientId}
			}));
        }

        public static AmfMessage GetInvokeOnStatusStreamPlayStart(uint channelId,
            uint streamId, double timeStamp, bool isAbsolute, double requestId, string description, string details,
            string clientId)
        {
            return GenericMessageFactory.GetInvokeOnStatus(channelId, streamId,
            timeStamp, isAbsolute, requestId, Variant.GetMap(new VariantMapHelper
            {
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_LEVEL, Defines.RM_INVOKE_PARAMS_ONSTATUS_LEVEL_STATUS},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_CODE, Defines.RM_INVOKE_PARAMS_ONSTATUS_CODE_NETSTREAMPLAYSTART},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_DESCRIPTION, description},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_DETAILS, details},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_CLIENTID, clientId}
            }));
        }
        public static AmfMessage GetInvokeOnStatusStreamPlayStop(uint channelId,
            uint streamId, double timeStamp, bool isAbsolute, double requestId, string description, string details,
            string clientId)
        {
            return GenericMessageFactory.GetInvokeOnStatus(channelId, streamId,
            timeStamp, isAbsolute, requestId, Variant.GetMap(new VariantMapHelper
			{
			    {Defines.RM_INVOKE_PARAMS_ONSTATUS_LEVEL,Defines.RM_INVOKE_PARAMS_ONSTATUS_LEVEL_STATUS},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_CODE,"NetStream.Play.Stop"},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_DESCRIPTION,description},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_DETAILS,details},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_CLIENTID,clientId},
			    {
			        "reason",""
			    }
			}));
        }
        public static AmfMessage GetInvokeOnStatusStreamPlayUnpublishNotify(uint channelId,
            uint streamId, double timeStamp, bool isAbsolute, double requestId, string description, 
            string clientId)
        {
            return GenericMessageFactory.GetInvokeOnStatus(channelId, streamId,
            timeStamp, isAbsolute, requestId,Variant.GetMap(new VariantMapHelper
			{
			    {Defines.RM_INVOKE_PARAMS_ONSTATUS_LEVEL,Defines.RM_INVOKE_PARAMS_ONSTATUS_LEVEL_STATUS},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_CODE,"NetStream.Play.UnpublishNotify"},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_DESCRIPTION,description},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_CLIENTID,clientId}
			}));
        }
        public static AmfMessage GetInvokeOnStatusStreamSeekNotify(uint channelId,
            uint streamId, double timeStamp, bool isAbsolute, double requestId, string description, string details,
            string clientId)
        {
            return GenericMessageFactory.GetInvokeOnStatus(channelId, streamId,
            timeStamp, isAbsolute, requestId, Variant.GetMap(new VariantMapHelper
			{
			    {Defines.RM_INVOKE_PARAMS_ONSTATUS_LEVEL,Defines.RM_INVOKE_PARAMS_ONSTATUS_LEVEL_STATUS},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_CODE,Defines.RM_INVOKE_PARAMS_ONSTATUS_CODE_NETSTREAMSEEKNOTIFY},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_DESCRIPTION,description},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_DETAILS,details},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_CLIENTID,clientId}
			}));
        }
        public static AmfMessage GetInvokeOnStatusStreamPauseNotify(uint channelId,
            uint streamId, double timeStamp, bool isAbsolute, double requestId, string description, string details,
            string clientId)
        {
            return GenericMessageFactory.GetInvokeOnStatus(channelId, streamId,
            timeStamp, isAbsolute, requestId, Variant.GetMap(new VariantMapHelper
			{
			    {Defines.RM_INVOKE_PARAMS_ONSTATUS_LEVEL,Defines.RM_INVOKE_PARAMS_ONSTATUS_LEVEL_STATUS},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_CODE,Defines.RM_INVOKE_PARAMS_ONSTATUS_CODE_NETSTREAMPAUSENOTIFY},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_DESCRIPTION,description},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_DETAILS,details},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_CLIENTID,clientId}
			}));
        }
        public static AmfMessage GetInvokeOnStatusStreamUnpauseNotify(uint channelId,
            uint streamId, double timeStamp, bool isAbsolute, double requestId, string description, string details,
            string clientId)
        {
            return GenericMessageFactory.GetInvokeOnStatus(channelId, streamId,
            timeStamp, isAbsolute, requestId, Variant.GetMap(new VariantMapHelper
			{
			    {Defines.RM_INVOKE_PARAMS_ONSTATUS_LEVEL,Defines.RM_INVOKE_PARAMS_ONSTATUS_LEVEL_STATUS},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_CODE,Defines.RM_INVOKE_PARAMS_ONSTATUS_CODE_NETSTREAMUNPAUSENOTIFY},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_DESCRIPTION,description},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_DETAILS,details},
                {Defines.RM_INVOKE_PARAMS_ONSTATUS_CLIENTID,clientId}
			}));
        }
        public static AmfMessage GetNotifyRtmpSampleAccess(uint channelId, uint streamId, double timeStamp, bool isAbsolute,
        bool audioAccess, bool videoAccess)
        {
            return GenericMessageFactory.GetNotify(channelId, streamId, timeStamp, isAbsolute, "|RtmpSampleAccess",
             Variant.GetList( audioAccess, videoAccess ));
        }
        public static AmfMessage GetNotifyOnMetaData(uint channelId, uint streamId, double timeStamp, bool isAbsolute,
       Variant metadata)
        {
            metadata[Defines.HTTP_HEADERS_SERVER] = "C# Rtmp Server";
            return GenericMessageFactory.GetNotify(channelId, streamId, timeStamp, isAbsolute, "onMetaData",
              Variant.GetList( metadata));
        }
        public static AmfMessage GetNotifyOnPlayStatusPlayComplete(uint channelId, uint streamId, double timeStamp, bool isAbsolute,
        double bytes, double duration)
        {
            return GenericMessageFactory.GetNotify(channelId, streamId, timeStamp, isAbsolute, "onPlayStatus",
              Variant.GetList(Variant.GetMap(new VariantMapHelper
              {
                  {"bytes",bytes},{"duration",duration},{"level","status"},
                  {"code","NetStream.Play.Complete"}
              })));
        }

        public static AmfMessage GetNotifyOnStatusDataStart(uint channelId, uint streamId, double timeStamp,
            bool isAbsolute)
        {
            return GenericMessageFactory.GetNotify(channelId, streamId, timeStamp, isAbsolute, "onStatus",
              Variant.GetList(Variant.GetMap(new VariantMapHelper
              {
                  {"code","NetStream.Data.Start"}
              })));
        }

        public static AmfMessage GetFlexStreamSend(uint channelId, uint streamId, double timeStamp, bool isAbsolute,
            string function, Variant parameters)
        {
            AmfMessage result;
            result.Header =
               GenericMessageFactory.VH(HeaderType.HT_FULL, channelId, (uint) timeStamp, 0,
                    Defines.RM_HEADER_MESSAGETYPE_FLEXSTREAMSEND, streamId, isAbsolute);
            result.Body = Variant.Get();
            result.Body[Defines.RM_FLEXSTREAMSEND, Defines.RM_FLEXSTREAMSEND_UNKNOWNBYTE] = (byte)0;
            result.Body[Defines.RM_FLEXSTREAMSEND, Defines.RM_FLEXSTREAMSEND_PARAMS].Add(function);
            foreach (var parameter in parameters.Children.Values)
                result.Body[Defines.RM_FLEXSTREAMSEND, Defines.RM_FLEXSTREAMSEND_PARAMS].Add(parameter);
            return result;
        }

    }
}
