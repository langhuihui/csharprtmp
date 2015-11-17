using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpRTMP.Core
{
    [AttributeUsage(AttributeTargets.Class,AllowMultiple = true,Inherited = true)]
    public class AppProtocolHandlerAttribute:Attribute
    {
        public Type HandlerClass;
        public ulong[] Type;
        public bool NotInMaster;
        public bool NotInSlave;
        public AppProtocolHandlerAttribute(Type handlerClass,params ulong[] type)
        {
            Type = type;
            HandlerClass = handlerClass;
        }
    }
}
