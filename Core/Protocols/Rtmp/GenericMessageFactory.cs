using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.Protocols.Rtmp
{
    public static class GenericMessageFactory
    {
        public static Header VH(byte ht,uint cid,uint ts,uint ml,byte mt,uint si,bool ia)
        {
            var header = new Header();
            header.Reset(ht,cid,ts,ml,mt,si,ia);
            return header;
        }

        public static AmfMessage GetInvokeOnStatus(uint channelId, uint streamId, double timeStamp, bool isAbsolute,
            double requestId, Variant message)
        {
            return GetInvoke(channelId, streamId, timeStamp, isAbsolute, requestId, Defines.RM_INVOKE_FUNCTION_ONSTATUS,
                Variant.GetList(Variant.Get(), message));
        }

        public static AmfMessage GetNotify(uint channelId, uint streamId, double timeStamp, bool isAbsolute,
            string handlerName, Variant args)
        {
            AmfMessage result;
            result.Header =
                VH(HeaderType.HT_FULL, channelId, (uint) timeStamp, 0, Defines.RM_HEADER_MESSAGETYPE_NOTIFY,
                    streamId,
                    isAbsolute);
            result.Body = Variant.Get();
            result.Body[Defines.RM_NOTIFY, Defines.RM_NOTIFY_PARAMS] = Variant.GetList(handlerName);
            foreach (var item in args.Children.Values)
                result.Body[Defines.RM_NOTIFY, Defines.RM_NOTIFY_PARAMS].Add(item);
            return result;
        }
        public static AmfMessage GetInvoke(uint channelId, uint streamId, double timeStamp, bool isAbsolute,
            double requestId, string functionName, Variant parameters)
        {
            AmfMessage result;
            result.Header = VH(HeaderType.HT_FULL, channelId, (uint) timeStamp, 0, Defines.RM_HEADER_MESSAGETYPE_INVOKE,
                streamId, isAbsolute);
            result.Body = Variant.GetMap(new VariantMapHelper
            {
                {
                    Defines.RM_INVOKE, Variant.GetMap(new VariantMapHelper
                    {
                        {Defines.RM_INVOKE_ID, requestId},
                        {Defines.RM_INVOKE_FUNCTION, functionName},
                        {Defines.RM_INVOKE_PARAMS, parameters}
                    })
                }
            });
            return result;
        }

        public static AmfMessage GetInvokeResult(uint channelId, uint streamId, double requestId, Variant firstParam, Variant secondParam)
        {
            return GetInvoke(channelId, streamId, 0, false, requestId, Defines.RM_INVOKE_FUNCTION_RESULT, Variant.GetList( firstParam,secondParam));
        }

        public static AmfMessage GetInvokeError(uint channelId, uint streamId, double requestId, Variant firstParam, Variant secondParam)
        {
            return GetInvoke(channelId, streamId, 0, false, requestId, Defines.RM_INVOKE_FUNCTION_ERROR,  Variant.GetList( firstParam,secondParam));
        }

        public static AmfMessage GetInvokeCallFailedError(AmfMessage request)
        {
            return GetInvokeError(request.ChannelId, request.StreamId, request.InvokeId, Variant.Get(), Variant.GetMap(new VariantMapHelper
            {
                {Defines.RM_INVOKE_PARAMS_RESULT_LEVEL,Defines.RM_INVOKE_PARAMS_RESULT_LEVEL_ERROR},
                {Defines.RM_INVOKE_PARAMS_RESULT_CODE,"NetConnection.Call.Failed"},
                {Defines.RM_INVOKE_PARAMS_RESULT_DESCRIPTION,"call to function "+request.InvokeFunction+" failed"}
            }));
        }

        public static AmfMessage GetChunkSize(uint chunkSize)
        {
            AmfMessage result;
            result.Header = VH(HeaderType.HT_FULL, 2, 0, 0, Defines.RM_HEADER_MESSAGETYPE_CHUNKSIZE, 0, true);
            result.Body = Variant.GetMap(new VariantMapHelper {{Defines.RM_CHUNKSIZE, chunkSize}});
            return result;
        }

        public static AmfMessage GetAbortMessage(uint channelId)
        {
            AmfMessage result;
            result.Header = VH(HeaderType.HT_FULL, 2, 0, 0, Defines.RM_HEADER_MESSAGETYPE_ABORTMESSAGE, 0, true);
            result.Body = Variant.GetMap(new VariantMapHelper { { Defines.RM_ABORTMESSAGE, channelId } });
            return result;
        }

        public static AmfMessage GetAck(ulong amount)
        {
            AmfMessage result;
            result.Header = VH(HeaderType.HT_FULL, 2, 0, 0, Defines.RM_HEADER_MESSAGETYPE_ACK, 0, true);
            result.Body = Variant.GetMap(new VariantMapHelper { { Defines.RM_ACK, amount } });
            return result;
        }
        public static AmfMessage GetWinAckSize(uint value)
        {
            AmfMessage result;
            result.Header = VH(HeaderType.HT_FULL, 2, 0, 0, Defines.RM_HEADER_MESSAGETYPE_WINACKSIZE, 0, true);
            result.Body = Variant.GetMap(new VariantMapHelper { { Defines.RM_WINACKSIZE, value } });
            return result;
        }
        public static AmfMessage GetPeerBW(uint value, byte type)
        {
            AmfMessage result;
            result.Header = VH(HeaderType.HT_FULL, 2, 0, 0, Defines.RM_HEADER_MESSAGETYPE_PEERBW, 0, true);
            result.Body = Variant.GetMap(new VariantMapHelper{{Defines.RM_PEERBW, Variant.GetMap(new VariantMapHelper
                {
                    {Defines.RM_PEERBW_VALUE,value},
                    {Defines.RM_PEERBW_TYPE,type}
                })}});
            return result;
        }

        public static AmfMessage GetInvokeOnBWDone(int kbpsSpeed)
        {

            var parameters = Variant.GetList(
            
                Variant.Get(),
                kbpsSpeed
            );
            return GetInvoke(3, 0, 0, false, 0, Defines.RM_INVOKE_FUNCTION_ONBWDONE, parameters);
        }

        internal static AmfMessage GetInvokeResult(AmfMessage request, Variant parameters)
        {
            return GetInvoke(request.ChannelId, request.StreamId, 0, false, request.InvokeId,
                Defines.RM_INVOKE_FUNCTION_RESULT, parameters);
        }
    }
}
