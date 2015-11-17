using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpRTMP.Core
{
    [AttributeUsage(AttributeTargets.Method,AllowMultiple = false)]
    public class CustomFunctionAttribute:Attribute
    {
        public CustomFunctionAttribute(string name)
        {
            Name = name;
        }
        public string Name { get; set; }
    }
}
