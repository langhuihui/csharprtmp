using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols;

namespace Core.Protocols.Rtmp
{
    [ProtocolType(ProtocolTypes.PT_RTMPE)]
    [AllowFarTypes(ProtocolTypes.PT_INBOUND_HTTP_FOR_RTMP, ProtocolTypes.PT_TCP)]
    [AllowNearTypes(ProtocolTypes.PT_INBOUND_RTMP, ProtocolTypes.PT_OUTBOUND_RTMP)]
   public  class RTMPEProtocol:BaseProtocol
    {
       private RC4_KEY _pKeyIn;
       private RC4_KEY _pKeyOut;
       private uint _skipBytes;
       //public InputStream InputBuffer;
       //public OutputStream OutputBuffer;
       public RTMPEProtocol(RC4_KEY pKeyIn, RC4_KEY pKeyOut, uint skipBytes = 0)
       {
           _pKeyIn = pKeyIn;
           _pKeyOut = pKeyOut;
           _skipBytes = skipBytes;
       }

       public override bool SignalInputData(int recAmount)
       {
           var datas = new byte[InputBuffer.Length - InputBuffer.Position];
           Utils.RC4(new BufferWithOffset(InputBuffer), _pKeyIn, datas.Length);
           return _nearProtocol == null || _nearProtocol.SignalInputData(recAmount);
       }

       public override bool EnqueueForOutbound(MemoryStream outputStream,int offset = 0)
       {
           var pOutputBuffer = _nearProtocol.OutputBuffer;
           if (pOutputBuffer == null) return true;
           var buffer = new BufferWithOffset(pOutputBuffer,true) {Offset = (int) _skipBytes};
           Utils.RC4(buffer, _pKeyOut, buffer.Length);
           _skipBytes = 0;
           buffer.Offset = 0;
           OutputBuffer.Write(buffer.Buffer, buffer.Offset, buffer.Length);
           return _farProtocol == null || _farProtocol.EnqueueForOutbound(outputStream);
       }

       
       //public static string RC4(string input, string key)
       //{
       //    var result = new StringBuilder();
       //    int x, y, j = 0;
       //   var box = new int[256];

       //    for (var i = 0; i < 256; i++)
       //    {
       //        box[i] = i;
       //    }

       //    for (var i = 0; i < 256; i++)
       //    {
       //        j = (key[i % key.Length] + box[i] + j) % 256;
       //        x = box[i];
       //        box[i] = box[j];
       //        box[j] = x;
       //    }

       //    for (var i = 0; i < input.Length; i++)
       //    {
       //        y = i % 256;
       //        j = (box[y] + j) % 256;
       //        x = box[y];
       //        box[y] = box[j];
       //        box[j] = x;

       //        result.Append((char)(input[i] ^ box[(box[y] + box[j]) % 256]));
       //    }
       //    return result.ToString();
       //}
    }
}
