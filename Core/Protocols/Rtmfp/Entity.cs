using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class Entity
    {
        public byte[] Id;
        public string IdStr
        {
            get { return Id.BytesToString(); }
            set { Id = value.ToBytes(); }
        }
    }
}
