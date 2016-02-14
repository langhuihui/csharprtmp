using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core;
using CSharpRTMP.Core.NetIO;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Rtmp;

namespace Core.Protocols.Rtmp
{
    [ProtocolType(ProtocolTypes.PT_OUTBOUND_RTMP)]
    public class OutboundRTMPProtocol:BaseRTMPProtocol
    {
        private byte[] _pClientPublicKey;
        private byte[] _pOutputBuffer;
        private byte[] _pClientDigest;
        private byte _usedScheme;
        //private Stream _outputBuffer222;
        private DHWrapper _pDHWrapper;
        private RC4_KEY _pKeyIn;
        private RC4_KEY _pKeyOut;
       
        protected override bool PerformHandshake(InputStream buffer)
        {
            switch (_rtmpState)
            {
               case RTMPState.RTMP_STATE_NOT_INITIALIZED:
                    return PerformHandshakeStage1((string)CustomParameters[Defines.CONF_PROTOCOL] == Defines.CONF_PROTOCOL_OUTBOUND_RTMPE);
               case RTMPState.RTMP_STATE_CLIENT_REQUEST_SENT:
                    if (buffer.AvaliableByteCounts < 3073) return true;
                    _usedScheme = (byte) ((string) CustomParameters[Defines.CONF_PROTOCOL] ==
                                          Defines.CONF_PROTOCOL_OUTBOUND_RTMPE? 1: 0);
                    if (!PerformHandshakeStage2(buffer, _usedScheme==1))
                    {
				        Logger.FATAL("Unable to handshake");
				        return false;
			        }

                    if (!EnqueueForOutbound(OutputBuffer))
                    {
                        Logger.FATAL("Unable to signal output data");
                        return false;
                    }
                    if (_pKeyIn != null && _pKeyOut != null)
                    {
                        var pRTMPE = new RTMPEProtocol(_pKeyIn, _pKeyOut, (uint)OutputBuffer.Length);
                        ResetFarProtocol();
                        _farProtocol.NearProtocol = pRTMPE;
                        pRTMPE.NearProtocol = this;
                        this.Log().Info("New protocol chain:{0}",_farProtocol);
                    }
                    buffer.Ignore(3073);
                    _handshakeCompleted = true;
			return true;
                default:
            Logger.FATAL("Invalid RTMP state:{0}",_rtmpState);
				return false;
            }
        }

        private bool PerformHandshakeStage1(bool encrypted)
        {
            OutputBuffer.WriteByte((byte)(encrypted ? 6 : 3));
            _pOutputBuffer=new byte[1536];
            var rand = new Random();
            for (var i = 0; i < 1536; i++)
            {
                _pOutputBuffer[i] = (byte) (rand.Next()%256);
            }
            _pOutputBuffer[0] = _pOutputBuffer[1] = _pOutputBuffer[2] = _pOutputBuffer[3] = 0;
            // 5. Put the flash version. We impersonate with 9.0.124.2
            _pOutputBuffer[4] = 9;
            _pOutputBuffer[5] = 0;
            _pOutputBuffer[6] = 124;
            _pOutputBuffer[7] = 2;
            var clientDHOffset = GetDHOffset(_pOutputBuffer, _usedScheme);
            _pDHWrapper = new DHWrapper(1024);
            _pClientPublicKey = _pDHWrapper.PublicKey;
            Buffer.BlockCopy(_pClientPublicKey, 0, _pOutputBuffer, (int)clientDHOffset, 128);
            var clientDigestOffset = GetDigestOffset(_pOutputBuffer, _usedScheme);
            var pTempBuffer = new byte[1536 - 32];
            Buffer.BlockCopy(_pOutputBuffer, 0, pTempBuffer, 0, (int)clientDigestOffset);
            Buffer.BlockCopy(_pOutputBuffer, (int) (clientDigestOffset + 32), pTempBuffer, (int)clientDigestOffset, (int) (1536 - clientDigestOffset - 32));
            //var pTempHash = new byte[512];

            var pTempHash = HMACsha256(pTempBuffer,1536-32,GenuineFpKey,30 ); // Hmacsha256.ComputeHash(pTempBuffer, 0,1536 - 32);
            Buffer.BlockCopy(pTempHash, 0, _pOutputBuffer, (int) clientDigestOffset,32);
            _pClientDigest = new byte[32];
            Buffer.BlockCopy(pTempHash, 0, _pClientDigest, 0, 32);
            OutputBuffer.Write(_pOutputBuffer, 0, 1536);
            //_outputBuffer222.Write(_pOutputBuffer, 0, 1536);
            _pOutputBuffer = null;
            if (!EnqueueForOutbound(OutputBuffer))
            {
                Logger.FATAL("Unable to signal ouput data");
                return false;
            }
            _rtmpState = RTMPState.RTMP_STATE_CLIENT_REQUEST_SENT;

            return true;
        }

        private bool VerifyServer(InputStream inputBuffer)
        {
            var pBuffer = new BufferWithOffset(inputBuffer);
            pBuffer.Offset++;
            var serverDigestPos = GetDigestOffset(pBuffer, _usedScheme);
            var pTempBuffer = new byte[1536 - 32];
            Buffer.BlockCopy(inputBuffer.GetBuffer(), pBuffer.Offset, pTempBuffer, 0, (int)serverDigestPos);
            Buffer.BlockCopy(inputBuffer.GetBuffer(), (int)(pBuffer.Offset+serverDigestPos + 32), pTempBuffer, (int)serverDigestPos, (int)(1536 - serverDigestPos - 32));
            var pDigest = HMACsha256(pTempBuffer, 1536 - 32, GenuineFmsKey, 36);
            for (var i = 0; i < 32; i++)
            {
                if (pDigest[i] != pBuffer[(int) (i + serverDigestPos)])
                {
                    Logger.FATAL("Server not verified");
                    return false;
                }
            }
            pBuffer.Offset += 1536;
            var pChallange = HMACsha256(_pClientDigest, 32, GenuineFmsKey, 68);
            pDigest = new HMACSHA256(pChallange).ComputeHash(pBuffer.Buffer, pBuffer.Offset, 1536 - 32);
            for (var i = 0; i < 32; i++)
            {
                if (pDigest[i] != pBuffer[i + 1536 - 32])
                {
                    Logger.FATAL("Server not verified");
                    return false;
                }
            }
            return true;
        }

        private bool PerformHandshakeStage2(InputStream inputBuffer, bool encrypted)
        {
            if (encrypted || _pProtocolHandler.ValidateHandshake)
            {
                if (!VerifyServer(inputBuffer))
                {
                    Logger.FATAL("Unable to verify server");
                    return false;
                }
            }
            var pBuffer = new BufferWithOffset(inputBuffer);
            pBuffer.Offset++;
            var serverDHOffset = GetDHOffset(pBuffer, _usedScheme);
            if (_pDHWrapper == null)
            {
               Logger.FATAL("dh wrapper not initialized");
                return false;
            }
            var pubKey= new byte[128];
            Buffer.BlockCopy(pBuffer.Buffer, (pBuffer.Offset + (int)serverDHOffset), pubKey, 0, 128);

            var secretKey = _pDHWrapper.CreateSharedKey(pubKey);
	        
            if (encrypted)
            {
                _pKeyIn = new RC4_KEY();
                _pKeyOut = new RC4_KEY();
                var pubKeyIn = new byte[128];
                Buffer.BlockCopy(pBuffer.Buffer,(int) (pBuffer.Offset+serverDHOffset),pubKeyIn,0,128);
                Utils.InitRC4Encryption(secretKey, pubKeyIn, _pClientPublicKey,_pKeyIn,_pKeyOut);
            }
            var serverDigestOffset = GetDigestOffset(pBuffer, _usedScheme);
            _pOutputBuffer = Utils.GenerateRandomBytes(1536);

            pBuffer.Offset += (int)serverDigestOffset;
            var pChallangeKey = HMACsha256(pBuffer, 32, GenuineFpKey, 62);//Hmacsha256.ComputeHash(pBuffer.Buffer, pBuffer.Offset, 32);
            var pDigest = new HMACSHA256(pChallangeKey).ComputeHash(_pOutputBuffer, 0, 1536 - 32);
            Buffer.BlockCopy(pDigest,0,_pOutputBuffer,1536-32,32);
            OutputBuffer.Write(_pOutputBuffer, 0, 1536);
            _pOutputBuffer = null;
            _rtmpState = RTMPState.RTMP_STATE_DONE;
            return true;
        }

        public static bool Connect(EndPoint endpoint, Variant parameters)
        {
            var chain = ProtocolFactoryManager.ResolveProtocolChain(Defines.CONF_PROTOCOL_OUTBOUND_RTMP);
            TCPConnector<OutboundRTMPProtocol>.Connect(endpoint, chain, parameters);
            return true;
        }

        public static bool SignalProtocolCreated(BaseProtocol protocol, Variant customParameters)
        {
            var application = ClientApplicationManager.FindAppByName(customParameters[Defines.CONF_APPLICATION_NAME]);
            if (application == null)
            {
                Logger.FATAL("Application {0} not found",customParameters[Defines.CONF_APPLICATION_NAME]);
                return false;
            }
            if (protocol == null)
            {
                Logger.FATAL("Connection failed:{0}", customParameters.ToString());
                return application.OutboundConnectionFailed(customParameters);
            }
            protocol.Application = application;
            var outboundRTMPProtocol = protocol as OutboundRTMPProtocol;
            outboundRTMPProtocol.CustomParameters = customParameters;
            return outboundRTMPProtocol.SignalInputData(0);
        }
    }
}
