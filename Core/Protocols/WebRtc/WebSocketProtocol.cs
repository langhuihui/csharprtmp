using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.Protocols.WebRtc
{
    [ProtocolType(ProtocolTypes.PT_INBOUND_WEBSOCKET)]
    [AllowFarTypes(ProtocolTypes.PT_TCP)]
    [AllowNearTypes(ProtocolTypes.PT_INBOUND_RTMP)]
    public class WebSocketProtocol:BaseProtocol
    {
        private readonly Regex _regex = new Regex(@"Sec\-WebSocket\-Key:(.*?)\r\n"); //查找"Abc"
        private string _key;
        public bool IsHandShaked;
        public bool IsUTF8String = false;
        public override MemoryStream OutputBuffer { get; } = Utils.Rms.GetStream();

        public override bool SignalInputData(int recAmount)
        {
            if (!IsHandShaked)
            {
                Handshake(recAmount);
                IsHandShaked = true;
            }
            else
            {
               
                if (recAmount < 2)
                {
                    return true;
                }
                var recBytes = InputBuffer.Reader.ReadBytes(recAmount);
                bool fin = (recBytes[0] & 0x80) == 0x80; // 1bit，1表示最后一帧  
                if (!fin)
                {
                    Logger.WARN("超过一帧");
                    return false;// 超过一帧暂不处理 
                }

                var maskFlag = (recBytes[1] & 0x80) == 0x80; // 是否包含掩码  
                if (!maskFlag)
                {
                    Logger.WARN("不包含掩码");
                    return false;// 不包含掩码
                }

                var payloadLen = recBytes[1] & 0x7F; // 数据长度  

                byte[] masks = new byte[4];
                byte[] payloadData;

                if (payloadLen == 126)
                {
                    Array.Copy(recBytes, 4, masks, 0, 4);
                    payloadLen = (ushort)(recBytes[2] << 8 | recBytes[3]);
                    payloadData = new byte[payloadLen];
                    Array.Copy(recBytes, 8, payloadData, 0, payloadLen);

                }
                else if (payloadLen == 127)
                {
                    Array.Copy(recBytes, 10, masks, 0, 4);
                    var uInt64Bytes = new byte[8];
                    for (int i = 0; i < 8; i++)
                    {
                        uInt64Bytes[i] = recBytes[9 - i];
                    }
                    var len = BitConverter.ToUInt64(uInt64Bytes, 0);

                    payloadData = new byte[len];
                    for (UInt64 i = 0; i < len; i++)
                    {
                        payloadData[i] = recBytes[i + 14];
                    }
                }
                else
                {
                    Array.Copy(recBytes, 2, masks, 0, 4);
                    payloadData = new byte[payloadLen];
                    Array.Copy(recBytes, 6, payloadData, 0, payloadLen);

                }
                for (var i = 0; i < payloadLen; i++)
                {
                    payloadData[i] = (byte)(payloadData[i] ^ masks[i % 4]);
                }
                InputBuffer.IgnoreAll();
                if (IsUTF8String)
                {
                    var rawString = Encoding.UTF8.GetString(payloadData);
                    InputBuffer.WriteBytes(rawString.Select(x => (byte)x).ToArray());
                    InputBuffer.Position = 0;
                    InputBuffer.Published = (uint)rawString.Length;
                }
                else
                {
                    InputBuffer.WriteBytes(payloadData);
                    InputBuffer.Position = 0;
                    InputBuffer.Published = (uint)payloadData.Length;
                }
                _nearProtocol.SignalInputData((int) InputBuffer.Published);
                InputBuffer.IgnoreAll();
            }
            return true;
        }

        public override bool EnqueueForOutbound(MemoryStream outputStream, int offset = 0)
        {
            if (IsHandShaked)
            {
                outputStream.Position = offset;
                var len = (int)outputStream.GetAvaliableByteCounts();
                byte[] content =  new byte[len];
                outputStream.Read(content, 0, len);
                if (IsUTF8String)
                {
                    content = Encoding.UTF8.GetBytes(content.Select(x=>(char)x).ToArray());
                    len = content.Length;
                }
                outputStream.IgnoreAll();
                if (len < 126)
                {
                    outputStream.WriteByte((byte) (IsUTF8String ? 0x81 : 0x82));
                    outputStream.WriteByte((byte)content.Length);
                }
                else if (len < 0xFFFF)
                {
                    outputStream.WriteByte((byte)(IsUTF8String ? 0x81 : 0x82));
                    outputStream.WriteByte(126);
                    outputStream.WriteByte((byte)(content.Length >> 8));
                    outputStream.WriteByte((byte)content.Length);
                }
                else
                {
                    outputStream.WriteByte((byte)(IsUTF8String ? 0x81 : 0x82));
                    outputStream.WriteByte(127);
                    for (var i = 63; i >= 0; i -= 8)
                    {
                        outputStream.WriteByte((byte)(content.Length >> i));
                    }
                }
                outputStream.Write(content, 0, content.Length);
                return base.EnqueueForOutbound(outputStream, 0);
            }
            
            return base.EnqueueForOutbound(outputStream, offset);
        }

        public void Handshake(int recAmount)
        {
            var str = Encoding.UTF8.GetString(InputBuffer.Reader.ReadBytes(recAmount));
            var m = _regex.Match(str); //设定要查找的字符串
            Debug.WriteLine(str);
            if (m.Success)
            {
                _key = m.Groups[1].Value.Trim();
            }
            string secKeyAccept = Convert.ToBase64String(SHA1.Create().ComputeHash(Encoding.ASCII.GetBytes(_key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            Console.WriteLine("服务器端生成的KEY：" + secKeyAccept);

            var responseBuilder = new StringBuilder();
            responseBuilder.AppendLine("HTTP/1.1 101 Switching Protocols");
            responseBuilder.AppendLine("Upgrade: websocket");
            responseBuilder.AppendLine("Connection: Upgrade");
            responseBuilder.AppendLine("Sec-WebSocket-Accept: " + secKeyAccept);
            responseBuilder.AppendLine("");
            Console.WriteLine("服务器端欲发送信息：\r\n" + responseBuilder.ToString());

            var handshakeText = Encoding.UTF8.GetBytes(responseBuilder.ToString());
            OutputBuffer.Write(handshakeText, 0, handshakeText.Length);
            base.EnqueueForOutbound(OutputBuffer);
        }
    }
}
