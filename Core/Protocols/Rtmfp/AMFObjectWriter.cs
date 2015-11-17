using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Rtmp;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public sealed class AMFObjectWriter:IDisposable
    {
        //private bool _end = true;
        private readonly Variant _buffer;
        private readonly AMF0Writer _writer;
        public AMFObjectWriter(AMF0Writer writer,Variant initVariant = null)
        {
            _writer = writer;
            _buffer = initVariant ?? Variant.Get();
        }

        public void Dispose()
        {
            //if (_end)
            //{
                _writer.WriteObject(_buffer,true);
              
           // }
        }

        public object this[string key]
        {
            set { _buffer[key] = Variant.Get(value); }
        }
    }
}
