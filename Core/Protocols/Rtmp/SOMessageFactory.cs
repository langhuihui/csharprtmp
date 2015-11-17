using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
namespace CSharpRTMP.Core.Protocols.Rtmp
{
    public static class SOMessageFactory
    {
        public static AmfMessage GetSharedObject(uint channelId, uint streamId,
        double timeStamp, bool isAbsolute, string name, uint version,
		bool persistent)
        {
            AmfMessage amfMessage;
            amfMessage.Header =
                GenericMessageFactory.VH(HeaderType.HT_FULL, channelId, (uint) timeStamp, 0,
                    Defines.RM_HEADER_MESSAGETYPE_SHAREDOBJECT, streamId, isAbsolute);
            amfMessage.Body = Variant.GetMap(new VariantMapHelper
            {
                {
                    Defines.RM_SHAREDOBJECT, Variant.GetMap(new VariantMapHelper
                    {
                        {Defines.RM_SHAREDOBJECT_NAME, name},
                        {Defines.RM_SHAREDOBJECT_VERSION, version},
                        {Defines.RM_SHAREDOBJECT_PERSISTENCE, persistent}
                    })
                }
            });
            return amfMessage;
        }
    }
}
