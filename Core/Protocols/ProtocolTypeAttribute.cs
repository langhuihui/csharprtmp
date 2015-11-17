using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpRTMP.Core.Protocols
{
    public class AllowTypesAttribute : Attribute
    {
        public ulong[] Types;
        public AllowTypesAttribute(params ulong[] allowTypes)
        {
            Types = allowTypes;
        }
    }
    [AttributeUsage(AttributeTargets.Class,AllowMultiple = false,Inherited = true)]
    public class AllowNearTypesAttribute : AllowTypesAttribute
    {
        public AllowNearTypesAttribute(params ulong[] allowTypes) : base(allowTypes)
        {
        }
    }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class AllowKindNearTypesAttribute : AllowTypesAttribute
    {
        public AllowKindNearTypesAttribute(params ulong[] allowTypes) : base(allowTypes)
        {
        }
    }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class AllowFarTypesAttribute : AllowTypesAttribute
    {
        public AllowFarTypesAttribute(params ulong[] allowTypes)
            : base(allowTypes)
        {
        }
    }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class AllowKindFarTypesAttribute : AllowTypesAttribute
    {
        public AllowKindFarTypesAttribute(params ulong[] allowTypes)
            : base(allowTypes)
        {
        }
    }
    [AttributeUsage(AttributeTargets.Class,AllowMultiple = false,Inherited = true)]
    public class ProtocolTypeAttribute:Attribute
    {
        public ulong Type;
        public ProtocolTypeAttribute(ulong type)
        {
            Type = type;
        }
    }
}
