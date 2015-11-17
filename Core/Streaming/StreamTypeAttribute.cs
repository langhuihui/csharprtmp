using System;

namespace CSharpRTMP.Core.Streaming
{
    [AttributeUsage(AttributeTargets.Class)]
    public class StreamTypeAttribute:Attribute
    {
        public ulong Type;
        public ulong[] Compat;
        public StreamTypeAttribute(ulong type, params ulong[] compat)
        {
            Type = type;
            Compat = compat;
        }
    }
}
