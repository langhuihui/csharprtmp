using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Rtmp;

namespace Core.Protocols.Rtmp
{
    [ProtocolType(ProtocolTypes.PT_INBOUND_RTMP)]
    public class InboundRTMPProtocol : BaseRTMPProtocol
    {
        private RC4_KEY _pKeyIn;
        private RC4_KEY _pKeyOut;
        private byte[] _pOutputBuffer;
        private uint _currentFPVersion;
        private byte _validationScheme;


        protected override bool PerformHandshake(InputStream buffer)
        {
            switch (_rtmpState)
            {
                case RTMPState.RTMP_STATE_NOT_INITIALIZED:
                    if (buffer.AvaliableByteCounts < 1537) return true;
                    var handshakeType = buffer.ReadByte();
                    var temp = new byte[4];
                    _currentFPVersion = (uint)IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer.GetBuffer(), (int)(buffer.Position + 4)));
                    switch (handshakeType)
                    {
                        case 3: //plain

                            return PerformHandshake(buffer, false);

                        case 6: //encrypted

                            return PerformHandshake(buffer, true);

                        default:

                            Logger.FATAL("Handshake type not implemented: {0}", handshakeType);
                            return false;

                    }
                case RTMPState.RTMP_STATE_SERVER_RESPONSE_SENT:
                    if (buffer.AvaliableByteCounts < 1537) return true;
                    buffer.Ignore(1536);
                    _handshakeCompleted = true;
                    _rtmpState = RTMPState.RTMP_STATE_DONE;
                    if (_pKeyIn != null && _pKeyOut != null)
                    {
                        //insert the RTMPE protocol in the current protocol stack
                        BaseProtocol pFarProtocol = FarProtocol;
                        var pRTMPE = new RTMPEProtocol(_pKeyIn, _pKeyOut);
                        ResetFarProtocol();
                        pFarProtocol.NearProtocol = pRTMPE;
                        pRTMPE.NearProtocol = this;
                        this.Log().Info("New protocol chain: {0}", pFarProtocol);
                        //decrypt the leftovers
                        Utils.RC4(new BufferWithOffset(buffer), _pKeyIn, buffer.AvaliableByteCounts);
                    }
                    return true;
                default:

                    Logger.FATAL("Invalid RTMP state: {0}", _rtmpState);
                    return false;

            }
            return true;
        }
        bool ValidateClient(InputStream inputBuffer)
        {
            if (_currentFPVersion == 0)
            {
                Logger.WARN("This version of player doesn't support validation");
                return true;
            }
            if (ValidateClientScheme(inputBuffer, 0))
            {
                _validationScheme = 0;
                return true;
            }
            if (ValidateClientScheme(inputBuffer, 1))
            {
                _validationScheme = 1;
                return true;
            }
            Logger.FATAL("Unable to validate client");
            return false;
        }

        private bool ValidateClientScheme(InputStream inputBuffer, byte scheme)
        {
            var pBuffer = new BufferWithOffset(inputBuffer);
            var clientDigestOffset = GetDigestOffset(pBuffer, scheme);
            var pTempBuffer = new byte[1536 - 32];
            Buffer.BlockCopy(pBuffer.Buffer, pBuffer.Offset, pTempBuffer, 0, (int)clientDigestOffset);
            Buffer.BlockCopy(pBuffer.Buffer, (int)(pBuffer.Offset + clientDigestOffset + 32), pTempBuffer, (int)clientDigestOffset, (int)(1536 - clientDigestOffset - 32));
            var pTempHash = HMACsha256(pTempBuffer, 1536 - 32, GenuineFpKey, 30);//Hmacsha256.ComputeHash(pTempBuffer, 0, 1536 - 32);
            for (var i = 0; i < 32; i++)
                if (pBuffer[(int)(clientDigestOffset + i)] != pTempHash[i])
                    return false;
            return true;
        }

        bool PerformHandshake(InputStream buffer, bool encrypted)
        {
            if (!ValidateClient(buffer))
            {
                if (encrypted || _pProtocolHandler.ValidateHandshake)
                {
                    Logger.FATAL("Unable to validate client");
                    return false;
                }
                else
                {
                    Logger.WARN("Client not validated");
                    _validationScheme = 0;
                }
            }
            _pOutputBuffer = Utils.GenerateRandomBytes(3072);
            _pOutputBuffer.Write(0, (uint)DateTime.Now.SecondsFrom1970());
            _pOutputBuffer.Write(0, (uint)0);
            var serverBytes = Encoding.ASCII.GetBytes(Defines.HTTP_HEADERS_SERVER_US);
            for (var i = 0; i < 10; i++)
            {
                var index = Utils.Random.Next(0, 3072 - Defines.HTTP_HEADERS_SERVER_US_LEN);
                Buffer.BlockCopy(serverBytes, 0, _pOutputBuffer, index, serverBytes.Length);
            }

            var _pOutputBufferWithOffset = new BufferWithOffset(_pOutputBuffer);
            var pInputBuffer = new BufferWithOffset(buffer);
            var serverDHOffset = GetDHOffset(_pOutputBufferWithOffset, _validationScheme);
            var clientDHOffset = GetDHOffset(pInputBuffer, _validationScheme);
            var dhWrapper = new DHWrapper();
            var pubKeyIn = new byte[128];
            Buffer.BlockCopy(buffer.GetBuffer(), (int)(buffer.Position + clientDHOffset), pubKeyIn, 0, 128);
            var sharedkey = dhWrapper.CreateSharedKey(pubKeyIn);
            var pubKeyOut = dhWrapper.PublicKey;
            Buffer.BlockCopy(pubKeyOut, 0, _pOutputBuffer, (int)serverDHOffset, 128);
            if (encrypted)
            {
                _pKeyIn = new RC4_KEY();
                _pKeyOut = new RC4_KEY();
                Utils.InitRC4Encryption(sharedkey, pubKeyIn, pubKeyOut, _pKeyIn, _pKeyOut);
                var data = new byte[1536];
                Utils.RC4(data, _pKeyIn, 1536);
                Utils.RC4(data, _pKeyOut, 1536);
            }
            var serverDigestOffset = GetDigestOffset(_pOutputBufferWithOffset, _validationScheme);
            var pTempBuffer = new byte[1536 - 32];
            Buffer.BlockCopy(_pOutputBuffer, 0, pTempBuffer, 0, (int)serverDigestOffset);
            Buffer.BlockCopy(_pOutputBuffer, (int)serverDigestOffset + 32, pTempBuffer, (int)serverDigestOffset, (int)(1536 - serverDigestOffset - 32));
            var pTempHash = HMACsha256(pTempBuffer, 1536 - 32, GenuineFmsKey, 36);
            Buffer.BlockCopy(pTempHash, 0, _pOutputBuffer, (int)serverDigestOffset, 32);
            var keyChallengeIndex = GetDigestOffset(pInputBuffer, _validationScheme);
            pInputBuffer.Offset += (int)keyChallengeIndex;
            pTempHash = HMACsha256(pInputBuffer, 32, GenuineFmsKey, 68);
            Buffer.BlockCopy(_pOutputBuffer, 1536, pTempBuffer, 0, 1536 - 32);
            pTempBuffer = new HMACSHA256(pTempHash).ComputeHash(pTempBuffer, 0, 1536 - 32);
            Buffer.BlockCopy(pTempBuffer, 0, _pOutputBuffer, 1536 * 2 - 32, 32);
            OutputBuffer.WriteByte((byte)(encrypted ? 6 : 3));
            OutputBuffer.Write(_pOutputBuffer, 0, 3072);
            buffer.Recycle(true);
            if (!EnqueueForOutbound(OutputBuffer))
            {
                Logger.FATAL("Unable to signal outbound data");
                return false;
            }
            _rtmpState = RTMPState.RTMP_STATE_SERVER_RESPONSE_SENT;
            return true;
        }
    }
}
