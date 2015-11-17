using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Rtmp;
using CSharpRTMP.Core.Streaming;

namespace Core.Protocols.Rtmp
{
    public static class ConnectionMessageFactory
    {
        public static AmfMessage GetPong()
        {
            var ts = (uint)(DateTime.Now.MilliSecondsFrom1970());
            AmfMessage result;
            result.Header =
                GenericMessageFactory.VH(HeaderType.HT_FULL, 2, 0, 0, Defines.RM_HEADER_MESSAGETYPE_USRCTRL, 0, true);
            result.Body = Variant.GetMap(new VariantMapHelper{{Defines.RM_USRCTRL, Variant.GetMap(new VariantMapHelper{
            {Defines.RM_USRCTRL_TYPE, (ushort)Defines.RM_USRCTRL_TYPE_PING_RESPONSE},
                    {Defines.RM_USRCTRL_TYPE_STRING,RTMPProtocolSerializer.GetUserCtrlTypeString(Defines.RM_USRCTRL_TYPE_PING_RESPONSE)},
                    {Defines.RM_USRCTRL_PONG,ts}})}});
            return result;
        }

        public static AmfMessage GetInvokeConnectResult(AmfMessage request, string level = Defines.RM_INVOKE_PARAMS_RESULT_LEVEL_STATUS, string code = Defines.RM_INVOKE_PARAMS_RESULT_CODE_NETCONNECTIONCONNECTSUCCESS, string description = Defines.RM_INVOKE_PARAMS_RESULT_DESCRIPTION_CONNECTIONSUCCEEDED)
        {
            double objectEncoding = (int)request.InvokeParam[0][Defines.RM_INVOKE_PARAMS_RESULT_OBJECTENCODING];
            return GetInvokeConnectResult(request.ChannelId,request.StreamId,request.InvokeId,level,code,description,objectEncoding);
        }

        private static AmfMessage GetInvokeConnectResult(uint channelId, uint streamId, uint requestId, string level, string code, string decription, double objectEncoding)
        {
            var firstParams = Variant.GetMap(new VariantMapHelper
            {
                {"fmsVer", "FMS/3,0,1,123"},
                {"capabilities", 31.0}
            });
            var secordParams = Variant.GetMap(new VariantMapHelper
            {
                {Defines.RM_INVOKE_PARAMS_RESULT_LEVEL, level},
                {Defines.RM_INVOKE_PARAMS_RESULT_CODE, code},
                {Defines.RM_INVOKE_PARAMS_RESULT_DESCRIPTION, decription},
                {Defines.RM_INVOKE_PARAMS_RESULT_OBJECTENCODING, objectEncoding}
            });
            return GenericMessageFactory.GetInvokeResult(channelId, streamId, requestId, firstParams, secordParams);
        }

        public static AmfMessage GetInvokeConnect(string appName, string tcUrl, double audioCodecs, double capabilities, string flashVer, bool fPad,
        string pageUrl, string swfUrl, double videoCodecs, double videoFunction,
        double objectEncoding)
        {
            var connectRequest0 =Variant.Get();
            connectRequest0[Defines.RM_INVOKE_PARAMS_CONNECT_APP] = appName;
            connectRequest0[Defines.RM_INVOKE_PARAMS_CONNECT_AUDIOCODECS] = audioCodecs;
            connectRequest0[Defines.RM_INVOKE_PARAMS_CONNECT_FLASHVER] = flashVer;
            connectRequest0[Defines.RM_INVOKE_PARAMS_CONNECT_FPAD] = (bool)fPad;
            connectRequest0[Defines.RM_INVOKE_PARAMS_CONNECT_PAGEURL] = pageUrl??"";
            connectRequest0[Defines.RM_INVOKE_PARAMS_CONNECT_SWFURL] = swfUrl ?? "";
            connectRequest0[Defines.RM_INVOKE_PARAMS_CONNECT_TCURL] = tcUrl??"";
            connectRequest0[Defines.RM_INVOKE_PARAMS_CONNECT_VIDEOCODECS] = videoCodecs;
            connectRequest0[Defines.RM_INVOKE_PARAMS_CONNECT_VIDEOFUNCTION] = videoFunction;
            connectRequest0["objectEncoding"] = objectEncoding;
            return GenericMessageFactory.GetInvoke(3, 0, 0, false, 1, Defines.RM_INVOKE_FUNCTION_CONNECT, Variant.GetList(connectRequest0));
        }

        internal static AmfMessage GetInvokeClose()
        {
            return GenericMessageFactory.GetInvoke(3, 0, 0, false, 0, Defines.RM_INVOKE_FUNCTION_CLOSE,
                Variant.GetList(Variant.Get()));
        }

        public static AmfMessage GetInvokeConnectError(uint channelId, uint streamId, uint requestId, string level, string code, string decription)
        {
            return GenericMessageFactory.GetInvokeError(channelId, streamId, requestId, Variant.Get(), Variant.GetMap(new VariantMapHelper
            {
                {Defines.RM_INVOKE_PARAMS_RESULT_LEVEL,level},
                {Defines.RM_INVOKE_PARAMS_RESULT_CODE, code},
                {Defines.RM_INVOKE_PARAMS_RESULT_DESCRIPTION, decription}
            }));
        }
        internal static AmfMessage GetInvokeConnectError(AmfMessage message, string description, string level = Defines.RM_INVOKE_PARAMS_RESULT_LEVEL_ERROR, string code = Defines.RM_INVOKE_PARAMS_RESULT_CODE_NETCONNECTIONCONNECTREJECTED)
        {
            return GetInvokeConnectError(message.ChannelId,message.StreamId, message.InvokeId, level, code,
                description);
        }
    }
}
