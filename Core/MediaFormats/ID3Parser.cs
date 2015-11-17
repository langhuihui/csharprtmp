using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Core.MediaFormats
{
    public class ID3Parser
    {
        public ID3Parser(uint majorVersion, uint minorVersion)
        {
            
        }

        public Variant GetMetadata()
        {
            return null;
        }

        public bool Parse(MediaFile file)
        {
            return false;
        }
    }
}
