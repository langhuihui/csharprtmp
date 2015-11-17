using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.MediaFormats;
using CSharpRTMP.Core.NetIO;
using CSharpRTMP.Core.Streaming;
using static CSharpRTMP.Common.Defines;
using static CSharpRTMP.Common.Logger;

namespace CSharpRTMP.Core.Protocols.Rtsp
{
    public class BaseRtspAppProtocolHandler:BaseAppProtocolHandler
    {
        private string _usersFile;
        private Variant _realms;
        private DateTime _lastUsersFileUpdate;

        public BaseRtspAppProtocolHandler(Variant configuration) : base(configuration)
        {
        }

        public override bool ParseAuthenticationNode(Variant node, Variant result)
        {
            //1. Users file validation
            string usersFile = node[CONF_APPLICATION_AUTH_USERS_FILE];
            if ((usersFile[0] != '/') && (usersFile[0] != '.'))
            {
                usersFile = (string)Configuration[CONF_APPLICATION_DIRECTORY] + usersFile;
            }
            if (!File.Exists(usersFile))
            {
                FATAL("Invalid authentication configuration. Missing users file: {0}", (usersFile));
                return false;
            }
            _usersFile = usersFile;

            if (!ParseUsersFile())
            {
                FATAL("Unable to parse users file {0}", usersFile);
                return false;
            }

            return true;
        }

        private bool ParseUsersFile()
        {
            /*
            //1. get the modification date
            var modificationDate = new FileInfo(_usersFile).LastWriteTime;
        

            //2. Do we need to re-parse everything?
            if (modificationDate == _lastUsersFileUpdate)
                return true;

            //3. Reset realms
            _realms.SetValue();

            Variant users;
            //4. Read users
            if (!ReadLuaFile(_usersFile, "users", users))
            {
                FATAL("Unable to read users file: `%s`", STR(_usersFile));
                _realms.Reset();
                return false;
            }

            FOR_MAP(users, string, Variant, i) {
                if ((VariantType)MAP_VAL(i) != V_STRING)
                {
                    FATAL("Invalid user detected");
                    _realms.Reset();
                    return false;
                }
            }

            //5. read the realms
            Variant realms;
            if (!ReadLuaFile(_usersFile, "realms", realms))
            {
                FATAL("Unable to read users file: `%s`", STR(_usersFile));
                _realms.Reset();
                return false;
            }

            if (realms != V_MAP)
            {
                FATAL("Invalid users file. Realms section is bogus: `%s`", STR(_usersFile));
                _realms.Reset();
                return false;
            }

            FOR_MAP(realms, string, Variant, i) {
                Variant & realm = MAP_VAL(i);
                if ((!realm.HasKeyChain(V_STRING, true, 1, "name"))
                        || ((string)realm["name"] == "")
                        || (!realm.HasKeyChain(V_STRING, true, 1, "method"))
                        || (((string)realm["method"] != "Basic") && ((string)realm["method"] != "Digest"))
                        || (!realm.HasKeyChain(V_MAP, true, 1, "users"))
                        || (realm["users"].MapSize() == 0))
                {
                    FATAL("Invalid users file. Realms section is bogus: `%s`", STR(_usersFile));
                    _realms.Reset();
                    return false;
                }
                _realms[realm["name"]]["name"] = realm["name"];
                _realms[realm["name"]]["method"] = realm["method"];

                FOR_MAP(realm["users"], string, Variant, i) {
                    if (!users.HasKey(MAP_VAL(i)))
                    {
                        FATAL("Invalid users file. Realms section is bogus: `%s`", STR(_usersFile));
                        _realms.Reset();
                        return false;
                    }
                    _realms[realm["name"]]["users"][MAP_VAL(i)] = users[MAP_VAL(i)];
                }
            }

            _lastUsersFileUpdate = modificationDate;
            */
            return true;
        }

        public override void RegisterProtocol(BaseProtocol protocol)
        {
            //1. Is this a client RTSP protocol?
            if (protocol.Type != ProtocolTypes.PT_RTSP) return;
            if (protocol.CustomParameters["isClient"] == null || !protocol.CustomParameters["isClient"])return;
            if (protocol.CustomParameters["forceTcp"] != null)
            {
                if (protocol.CustomParameters["forceTcp"] != VariantType.Boolean)
                {
                    FATAL("Invalid forceTcp flag detected");
                    protocol.EnqueueForDelete();
                    return;
                }
            }
            else
            {
                protocol.CustomParameters["forceTcp"] = false;
            }
            //可能用不到了
            //var rtsp = protocol as RtspProtocol;
            //if ((parameters.HasKeyChain(V_MAP, true, 2, "customParameters", "externalStreamConfig"))
            //|| (parameters.HasKeyChain(V_MAP, true, 2, "customParameters", "localStreamConfig")))
            //{
            //    //5. Start play
            //    if (!TriggerPlayOrAnnounce(pRTSPProtocol))
            //    {
            //        FATAL("Unable to initiate play on uri %s",
            //                (parameters["uri"]));
            //        pRTSPProtocol.EnqueueForDelete();
            //        return;
            //    }
            //}
            //else
            //{
            //    WARN("Bogus connection. Terminate it");
            //    pProtocol.EnqueueForDelete();
            //}
        }

        public override void UnRegisterProtocol(BaseProtocol protocol)
        {
            
        }

        public bool HandleRTSPRequest(RtspProtocol from,Variant requestHeaders,string requestContent)
        {
            string method = requestHeaders[RTSP_FIRST_LINE, RTSP_METHOD];
            //1. we need a CSeq
            if (requestHeaders[RTSP_HEADERS, RTSP_HEADERS_CSEQ] == null)
            {
                FATAL("Request doesn't have {0}:\n{1}", RTSP_HEADERS_CSEQ, requestHeaders);
                return false;
            }
            //2. Validate session id
            string wantedSessionId = from.SessionId;
            if (!string.IsNullOrEmpty(wantedSessionId))
            {
                var requestSessionId = "";
                if (requestHeaders[RTSP_HEADERS, RTSP_HEADERS_SESSION] == null)
                {
                    FATAL("No session id");
                    return false;
                }
                requestSessionId = requestHeaders[RTSP_HEADERS, RTSP_HEADERS_SESSION];
                var parts = requestSessionId.Split(';');
                if (parts.Length >= 1)
                {
                    requestSessionId = parts[0];
                }
                if (requestSessionId != wantedSessionId)
                {
                    FATAL("Invalid session ID. Wanted: `{0}`; Got: `{1}`",
                            (wantedSessionId),
                            (requestSessionId));
                    return false;
                }
            }
            //4. Prepare a fresh new response. Add the sequence number
            from.ClearResponseMessage();
            from.PushResponseHeader(RTSP_HEADERS_CSEQ, requestHeaders[RTSP_HEADERS, RTSP_HEADERS_CSEQ]);
            //5. Do we have authentication? We will authenticate everything except "OPTIONS"
            if ( !string.IsNullOrEmpty(_usersFile) && NeedAuthentication(from,requestHeaders,requestContent))
            {
                //6. Re-parse authentication file if necessary
                if (!ParseUsersFile())
                {
                    FATAL("Unable to parse authentication file");
                    return false;
                }
                //7. Get the real name to use it further in authentication process
                string realmName = GetAuthenticationRealm(from, requestHeaders,
                        requestContent);
                //8. Do we have that realm?
                if (_realms[realmName]==null)
                {
                    FATAL("Realm `{0}` not found", (realmName));
                    return false;
                }
                Variant realm = _realms[realmName];
                //9. Is the user even trying to authenticate?
                if (requestHeaders[RTSP_HEADERS, RTSP_HEADERS_AUTHORIZATION] == null)
                {
                    return SendAuthenticationChallenge(from, realm);
                }
                else
                {
                    //14. The client sent us some response. Validate it now
                    //Did we ever sent him an authorization challange?
                    if (from.CustomParameters["wwwAuthenticate"] ==null)
                    {
                        FATAL("Client tried to authenticate and the server didn't required that");
                        return false;
                    }

                    //15. Get the server challenge
                    string wwwAuthenticate = from.CustomParameters["wwwAuthenticate"];

                    //16. Get the client response
                    string authorization = requestHeaders[RTSP_HEADERS, RTSP_HEADERS_AUTHORIZATION];

                    //17. Try to authenticate
                    if (!HTTPAuthHelper.ValidateAuthRequest(wwwAuthenticate,
                            authorization,
                            method,
                            (string)requestHeaders[RTSP_FIRST_LINE,RTSP_URL],
                            realm))
                    {
                        WARN("Authorization failed: challenge: {0}; response: {1}",
                                wwwAuthenticate, authorization);
                        return SendAuthenticationChallenge(from, realm);
                    }

                    //18. Success. User authenticated
                    //INFO("User authenticated: %s", (authorization));
                }
            }
            switch (method)
            {
                case RTSP_METHOD_OPTIONS:
                    from.PushResponseFirstLine(RTSP_VERSION_1_0, 200, "OK");
                    from.PushResponseHeader(RTSP_HEADERS_PUBLIC, "DESCRIBE, OPTIONS, PAUSE, PLAY, SETUP, TEARDOWN, ANNOUNCE, RECORD");
                    return from.SendResponseMessage();
                case RTSP_METHOD_DESCRIBE:
                    //1. get the stream name
                    Uri uri = new Uri(requestHeaders[RTSP_FIRST_LINE,RTSP_URL]);
                    
                    string streamName = (uri.Segments.LastOrDefault(x=>!x.EndsWith("/"))??"")+uri.Query;
                    if (streamName == "")
                    {
                        FATAL("Invalid stream name");
                        return false;
                    }

                    //2. Get the inbound stream capabilities
                    IInNetStream pInStream = GetInboundStream(streamName);

                    //3. Prepare the body of the response
                    string outboundContent = ComputeSDP(from, streamName, "", "0.0.0.0");
                    if (outboundContent == "")
                    {
                        FATAL("Unable to compute SDP");
                        return false;
                    }

                    //4. Save the stream id for later usage
                    from.CustomParameters["streamId"] = pInStream.UniqueId;

                    //5. Mark this connection as outbound connection
                    from.CustomParameters["isInbound"] = false;

                    //6. prepare the response
                    from.PushResponseFirstLine(RTSP_VERSION_1_0, 200, "OK");
                    from.PushResponseHeader(RTSP_HEADERS_CONTENT_TYPE, RTSP_HEADERS_ACCEPT_APPLICATIONSDP);
                    from.PushResponseContent(outboundContent, false);

                    //7. Done
                    return from.SendResponseMessage();
                case RTSP_METHOD_SETUP:
                    if (from.CustomParameters["isInbound"] != VariantType.Boolean)
                    {
                        FATAL("Invalid state");
                        return false;
                    }

                    return @from.CustomParameters["isInbound"] ? HandleRTSPRequestSetupInbound(@from, requestHeaders, requestContent) : HandleRTSPRequestSetupOutbound(@from, requestHeaders, requestContent);
                case RTSP_METHOD_PLAY:
                    return HandleRTSPRequestPlay(from, requestHeaders, requestContent);
                case RTSP_METHOD_TEARDOWN:
                    from.EnqueueForDelete();
                    return true;
                case RTSP_METHOD_ANNOUNCE:
                    return HandleRTSPRequestAnnounce(from, requestHeaders, requestContent);
                case RTSP_METHOD_RECORD:
                    //1. Make sure we have everything and we are in the proper state
                    if ((from.CustomParameters["isInbound"] != VariantType.Boolean)
                            || ((bool)from.CustomParameters["isInbound"] != true))
                    {
                        FATAL("Invalid state");
                        return false;
                    }

                    if (from.CustomParameters["pendingTracks"] != VariantType.Map)
                    {
                        FATAL("Invalid state");
                        return false;
                    }

                    //3. Get the inbound connectivity
                    InboundConnectivity pConnectivity = from.InboundConnectivity;
                    if (pConnectivity == null)
                    {
                        FATAL("Unable to get inbound connectivity");
                        return false;
                    }
                    if (!pConnectivity.Initialize())
                    {
                        FATAL("Unable to initialize inbound connectivity");
                        return false;
                    }

                    //4. Send back the response
                    from.PushResponseFirstLine(RTSP_VERSION_1_0, 200, "OK");
                    return from.SendResponseMessage();
                case RTSP_METHOD_PAUSE:
                    from.PushResponseFirstLine(RTSP_VERSION_1_0, 200, "OK");
                    return from.SendResponseMessage();
                default:
                    return false;
            }
        }

        private bool HandleRTSPRequestAnnounce(RtspProtocol pFrom, Variant requestHeaders, string requestContent)
        {
            //1. Make sure we ONLY handle application/sdp
            if ((string)requestHeaders[RTSP_HEADERS,RTSP_HEADERS_CONTENT_TYPE]!= RTSP_HEADERS_ACCEPT_APPLICATIONSDP)
            {
                FATAL("Invalid ANNOUNCE request:\n{0}", (requestHeaders.ToString()));
                return false;
            }

            //2. Get the SDP
            var sdp = pFrom.InboundSDP;

            //3. Parse the SDP
            if (!SDP.ParseSDP(sdp, requestContent))
            {
                FATAL("Unable to parse the SDP");
                return false;
            }

            //4. Get the first video track
            var videoTrack = sdp.GetVideoTrack(0,requestHeaders[RTSP_FIRST_LINE,RTSP_URL]);
                    
            var audioTrack = sdp.GetAudioTrack(0,requestHeaders[RTSP_FIRST_LINE,RTSP_URL]);
                    

            //5. Store the tracks inside the session for later use
            if (audioTrack != VariantType.Null)
            {
                pFrom.CustomParameters["pendingTracks",audioTrack["globalTrackIndex"]] = audioTrack;
            }
            if (videoTrack != VariantType.Null)
            {
                pFrom.CustomParameters["pendingTracks",videoTrack["globalTrackIndex"]] = videoTrack;
            }

            //6. Mark this connection as inbound connection
            pFrom.CustomParameters["isInbound"] = true;

            //7. Save the streamName
            string streamName = sdp.GetStreamName();
            if (streamName == "")
            {
                streamName = $"rtsp_stream_{pFrom.Id}";
            }
            pFrom.CustomParameters["sdpStreamName"] = streamName;
            streamName = new Uri(requestHeaders[RTSP_FIRST_LINE, RTSP_URL],UriKind.Absolute).Segments.Last();
            //8. Save the bandwidth hint
            pFrom.CustomParameters["sdpBandwidthHint"] = sdp.GetTotalBandwidth();

            //9. Get the inbound connectivity
            InboundConnectivity pInboundConnectivity = pFrom.GetInboundConnectivity(
                    streamName,
                    sdp.GetTotalBandwidth(),Application.Configuration[CONF_APPLICATION_RTCPDETECTIONINTERVAL]);
            if (pInboundConnectivity == null)
            {
                FATAL("Unable to create inbound connectivity");
                return false;
            }

            //8. Send back the response
            pFrom.PushResponseFirstLine(RTSP_VERSION_1_0, 200, "OK");
            return pFrom.SendResponseMessage();
        }

        private bool HandleRTSPRequestPlay(RtspProtocol pFrom, Variant requestHeaders, string requestContent)
        {
            //1. Get the outbound connectivity
            bool forceTcp = pFrom.CustomParameters["forceTcp"];
            var pOutboundConnectivity = GetOutboundConnectivity(pFrom, true);
            if (pOutboundConnectivity == null)
            {
                FATAL("Unable to get the outbound connectivity");
                return false;
            }

            if (forceTcp)
            {
                //3. Get the audio/video client ports
                byte videoDataChannelNumber = 0xff;
                byte videoRtcpChannelNumber = 0xff;
                byte audioDataChannelNumber = 0xff;
                byte audioRtcpChannelNumber = 0xff;
                if (pFrom.CustomParameters["audioDataChannelNumber"]!=null)
                {
                    audioDataChannelNumber = pFrom.CustomParameters["audioDataChannelNumber"];
                    audioRtcpChannelNumber = pFrom.CustomParameters["audioRtcpChannelNumber"];
                }
                if (pFrom.CustomParameters["videoDataChannelNumber"] != null)
                {
                    videoDataChannelNumber = pFrom.CustomParameters["videoDataChannelNumber"];
                    videoRtcpChannelNumber = pFrom.CustomParameters["videoRtcpChannelNumber"];
                }

                //4.register the video
                if (videoDataChannelNumber != 0xff)
                {
                    if (!pOutboundConnectivity.RegisterTCPVideoClient(pFrom.Id,
                            videoDataChannelNumber, videoRtcpChannelNumber))
                    {
                        FATAL("Unable to register video stream");
                        return false;
                    }
                }

                //5. Register the audio
                if (audioDataChannelNumber != 0xff)
                {
                    if (!pOutboundConnectivity.RegisterTCPAudioClient(pFrom.Id,
                            audioDataChannelNumber, audioRtcpChannelNumber))
                    {
                        FATAL("Unable to register audio stream");
                        return false;
                    }
                }
            }
            else
            {
                //3. Get the audio/video client ports
                ushort videoDataPortNumber = 0;
                ushort videoRtcpPortNumber = 0;
                ushort audioDataPortNumber = 0;
                ushort audioRtcpPortNumber = 0;
                if (pFrom.CustomParameters["audioDataPortNumber"] != null)
                {
                    audioDataPortNumber = pFrom.CustomParameters["audioDataPortNumber"];
                    audioRtcpPortNumber = pFrom.CustomParameters["audioRtcpPortNumber"];
                }
                if (pFrom.CustomParameters["videoDataPortNumber"] != null)
                {
                    videoDataPortNumber = pFrom.CustomParameters["videoDataPortNumber"];
                    videoRtcpPortNumber = pFrom.CustomParameters["videoRtcpPortNumber"];
                }

                //4.register the video
                if (videoDataPortNumber != 0)
                {
                    var videoDataAddress = ((TCPCarrier)pFrom.IOHandler).FarInfo;
                    videoDataAddress.Port = videoDataPortNumber;
                    var videoRtcpAddress = ((TCPCarrier)pFrom.IOHandler).FarInfo;
                    videoRtcpAddress.Port = videoRtcpPortNumber;
                    if (!pOutboundConnectivity.RegisterUDPVideoClient(pFrom.Id,
                            videoDataAddress, videoRtcpAddress))
                    {
                        FATAL("Unable to register video stream");
                        return false;
                    }
                }

                //5. Register the audio
                if (audioDataPortNumber != 0)
                {
                    var audioDataAddress = ((TCPCarrier)pFrom.IOHandler).FarInfo;
                    audioDataAddress.Port = audioDataPortNumber;
                    var audioRtcpAddress = ((TCPCarrier)pFrom.IOHandler).FarInfo;
                    audioRtcpAddress.Port = audioRtcpPortNumber;
                    if (!pOutboundConnectivity.RegisterUDPAudioClient(pFrom.Id,audioDataAddress, audioRtcpAddress))
                    {
                        FATAL("Unable to register audio stream");
                        return false;
                    }
                }
            }

            //6. prepare the response
            pFrom.PushResponseFirstLine(RTSP_VERSION_1_0, 200, "OK");
            //7. Done
            return pFrom.SendResponseMessage();
        }

        private string ComputeSDP(RtspProtocol pFrom,string localStreamName, string targetStreamName, string host)
        {
            StreamCapabilities pCapabilities = null;
            var pInboundStream = GetInboundStream(localStreamName);
           
            if (pInboundStream == null)
                FATAL("Stream {0} not found", localStreamName);
            else
                pCapabilities = pInboundStream.Capabilities;
            if (pCapabilities == null)
            {
                FATAL("Inbound stream {0} not found", (localStreamName));
                return "";
            }
            Debug(pCapabilities.AudioCodecId.ToString());
            string audioTrack = GetAudioTrack(pFrom, pCapabilities);
            string videoTrack = GetVideoTrack(pFrom, pCapabilities);
            if (audioTrack == "" && videoTrack == "")
                return "";

            string nearAddress = "0.0.0.0";
            string farAddress = "0.0.0.0";
            if ((pFrom.IOHandler != null)
                    && (pFrom.IOHandler.Type == IOHandlerType.IOHT_TCP_CARRIER))
            {
                nearAddress = ((TCPCarrier)pFrom.IOHandler).NearIP;
                farAddress = ((TCPCarrier)pFrom.IOHandler).FarIP;
            }

            if (targetStreamName == "")
                targetStreamName = localStreamName;

            //3. Prepare the body of the response

            var sw = new StringBuilder();
            sw.AppendLine("v=0");
            sw.AppendLine($"o=- {pFrom.Id} 0 IN IP4 {nearAddress}");
            sw.AppendLine("s=" + targetStreamName);
            sw.AppendLine("u=http://www.linkage.com");
            //result += "e=contact@evostream.com\r\n";
            sw.AppendLine("c=IN IP4 " + nearAddress);
            sw.AppendLine("t=0 0");
            //result += "a=recvonly\r\n";
            sw.Append(audioTrack);
            sw.Append(videoTrack);
            
            //FINEST("result:\n%s", STR(result));
            return sw.ToString();
        }

        private string GetVideoTrack(RtspProtocol pFrom, StreamCapabilities pCapabilities)
        {
            pFrom.CustomParameters["videoTrackId"] = "2"; //md5(format("V%u%s",pFrom->GetId(), STR(generateRandomString(4))), true);
            var sw = new StringBuilder();
            
            if (pCapabilities.VideoCodecId == VideoCodec.H264)
            {
                sw.AppendLine("m=video 0 RTP/AVP 97");
                sw.AppendLine("a=recvonly");
                sw.Append("a=control:trackID=");
                sw.AppendLine(pFrom.CustomParameters["videoTrackId"]);
                sw.AppendLine("a=rtpmap:97 H264/90000");
                sw.Append("a=fmtp:97 profile-level-id=");
                sw.Append($"{pCapabilities.Avc.SPS[1]:X2}{pCapabilities.Avc.SPS[2]:X2}{pCapabilities.Avc.SPS[3]:X2}");
                sw.Append("; packetization-mode=1; sprop-parameter-sets=");
                sw.Append(Convert.ToBase64String(pCapabilities.Avc.SPS) + ",");
                sw.AppendLine(Convert.ToBase64String(pCapabilities.Avc.PPS));
            }
            else
            {

                WARN("Unsupported video codec: %s", pCapabilities.VideoCodecId);
            }
            return sw.ToString();
        }

        private string GetAudioTrack(RtspProtocol pFrom, StreamCapabilities pCapabilities)
        {
            pFrom.CustomParameters["audioTrackId"] = "1"; //md5(format("A%u%s",pFrom->GetId(), STR(generateRandomString(4))), true);
            string result = "";
            
            switch (pCapabilities.AudioCodecId)
            {
                case AudioCodec.Aac:
                    result += "m=audio 0 RTP/AVP 96\r\n";
                    result += "a=recvonly\r\n";
                    result += $"a=rtpmap:96 mpeg4-generic/{pCapabilities.Aac._sampleRate}/2\r\n";
                    //FINEST("result: %s", STR(result));
                    result += "a=control:trackID="
                              + pFrom.CustomParameters["audioTrackId"] + "\r\n";
                    //rfc3640-fmtp-explained.txt Chapter 4.1
                    result +=
                        $"a=fmtp:96 streamtype=5; profile-level-id=15; mode=AAC-hbr; {pCapabilities.Aac.GetRTSPFmtpConfig()}; SizeLength=13; IndexLength=3; IndexDeltaLength=3;\r\n";
                    break;
                case AudioCodec.Speex:
                    result += "m=audio 0 RTP/AVP 98\r\n";
                    result += "a=rtpmap:98 speex/16000\r\n";
                    //FINEST("result: %s", STR(result));
                    result += "a=control:trackID="+ pFrom.CustomParameters["audioTrackId"] + "\r\n";

                    //http://www.rfc-editor.org/rfc/rfc5574.txt
                    result +="a=fmtp:98 mode=\"7,any\"\r\n";
                    break;
                default:
                    WARN("Unsupported audio codec: {0}", pCapabilities.AudioCodecId);
                    break;
            }
            return result;
        }

        private IInNetStream GetInboundStream(string streamName)
        {
            //1. get all the inbound network streams which begins with streamName
            var streams = Application.StreamsManager.FindByTypeByName(StreamTypes.ST_IN_NET, streamName, true,
                    Application.AllowDuplicateInboundNetworkStreams);
            if (streams.Count == 0)
                return null;

            //2. Get the fisrt value and see if it is compatible
            var pResult = streams.First().Value;
            if (!pResult.IsCompatibleWithType(StreamTypes.ST_OUT_NET_RTP))
            {
                FATAL("The stream {0} is not compatible with stream type {1}",
                        (streamName), StreamTypes.ST_OUT_NET_RTP.TagToString());
                return null;
            }

            //2. Done
            return (IInNetStream) pResult;
        }

        private bool SendAuthenticationChallenge(RtspProtocol from, Variant realm)
        {
            //10. Ok, the user doesn't know that this needs authentication. We
            //will respond back with a nice 401. Generate the line first
            string wwwAuthenticate = HTTPAuthHelper.GetWWWAuthenticateHeader(
                    realm["method"],
                    realm["name"]);

            //12. Save the nonce for later validation when new requests are coming in again
            from.CustomParameters["wwwAuthenticate"] = wwwAuthenticate;

            //13. send the response
            from.PushResponseFirstLine(RTSP_VERSION_1_0, 401, "Unauthorized");
            from.PushResponseHeader(HTTP_HEADERS_WWWAUTHENTICATE, wwwAuthenticate);
            return from.SendResponseMessage();
        }

        protected virtual string GetAuthenticationRealm(RtspProtocol @from, Variant requestHeaders, string requestContent) => _realms.Count > 0 ? _realms.Children.First().Key : "";

        protected virtual bool NeedAuthentication(RtspProtocol @from, Variant requestHeaders, string requestContent) => requestHeaders[RTSP_FIRST_LINE, RTSP_METHOD] != RTSP_METHOD_OPTIONS;

        private bool HandleRTSPRequestSetupOutbound(RtspProtocol @from, Variant requestHeaders, string requestContent)
        {
            //1. Minimal sanitize
            if (requestHeaders[RTSP_HEADERS, RTSP_HEADERS_TRANSPORT] == null)
            {
                FATAL("RTSP %s request doesn't have %s header line",
                        RTSP_METHOD_SETUP,
                        RTSP_HEADERS_TRANSPORT);
                return false;
            }

            //2. get the transport header line
            string raw = requestHeaders[RTSP_HEADERS, RTSP_HEADERS_TRANSPORT];
            Variant transport = Variant.Get();
            if (!Rtsp.SDP.ParseTransportLine(raw, transport))
            {
                FATAL("Unable to parse transport line {0}", (raw));
                return false;
            }
            bool forceTcp = false;
            if (transport["client_port"]!=null && (transport["rtp/avp/udp"]!=null || transport["rtp/avp"]!=null))
            {
                forceTcp = false;
            }
            else if (transport["interleaved"]!=null && transport["rtp/avp/tcp"]!=null)
            {
                forceTcp = true;
            }
            else
            {
                FATAL("Invalid transport line: {0}", (transport.ToString()));
                return false;
            }
            from.CustomParameters["forceTcp"] = forceTcp;
            
            //3. Get the outbound connectivity
            OutboundConnectivity pOutboundConnectivity = GetOutboundConnectivity(from, forceTcp);
            if (pOutboundConnectivity == null)
            {
                FATAL("Unable to get the outbound connectivity");
                return false;
            }

            //5. Find out if this is audio or video
            bool isAudioTrack;
            string rawUrl = requestHeaders[RTSP_FIRST_LINE,RTSP_URL];
            string audioTrackId = "trackID=" + from.CustomParameters["audioTrackId"];
            string videoTrackId = "trackID=" + from.CustomParameters["videoTrackId"];
            /*FINEST("rawUrl: %s; audioTrackId: %s; videoTrackId: %s; fa: %d; fv: %d",
                (rawUrl),
                (audioTrackId),
                (videoTrackId),
                rawUrl.find(audioTrackId) != string::npos,
                rawUrl.find(videoTrackId) != string::npos
            );*/
            if (rawUrl.Contains(audioTrackId))
            {
                isAudioTrack = true;
            }
            else if (rawUrl.Contains(videoTrackId))
            {
                isAudioTrack = false;
            }
            else
            {
                FATAL("Invalid track. Wanted: {0} or {1}; Got: {2}",
                        (from.CustomParameters["audioTrackId"]),
                        (from.CustomParameters["videoTrackId"]),
                        (requestHeaders[RTSP_FIRST_LINE,RTSP_URL]));
                return false;
            }
            from.CustomParameters["isAudioTrack"] = isAudioTrack;
            
            if (isAudioTrack)
            {
                if (forceTcp)
                {
                    from.CustomParameters["audioDataChannelNumber"] = transport["interleaved","data"];
                    from.CustomParameters["audioRtcpChannelNumber"] = transport["interleaved","rtcp"];
                    from.CustomParameters["audioTrackUri"] = requestHeaders[RTSP_FIRST_LINE,RTSP_URL];
                    pOutboundConnectivity.HasAudio = true;
                }
                else
                {
                    from.CustomParameters["audioDataPortNumber"] = transport["client_port","data"];
                    from.CustomParameters["audioRtcpPortNumber"] = transport["client_port","rtcp"];
                    from.CustomParameters["audioTrackUri"] = requestHeaders[RTSP_FIRST_LINE,RTSP_URL];
                    pOutboundConnectivity.HasAudio = true;
                }
            }
            else
            {
                if (forceTcp)
                {
                    from.CustomParameters["videoDataChannelNumber"] = transport["interleaved","data"];
                    from.CustomParameters["videoRtcpChannelNumber"] = transport["interleaved","rtcp"];
                    from.CustomParameters["videoTrackUri"] = requestHeaders[RTSP_FIRST_LINE,RTSP_URL];
                    pOutboundConnectivity.HasVideo = true;
                }
                else
                {
                    from.CustomParameters["videoDataPortNumber"] = transport["client_port","data"];
                    from.CustomParameters["videoRtcpPortNumber"] = transport["client_port","rtcp"];
                    from.CustomParameters["videoTrackUri"] = requestHeaders[RTSP_FIRST_LINE,RTSP_URL];
                    pOutboundConnectivity.HasVideo = true;
                }
            }
            
            //10. Create a session
            from.GenerateSessionId();

            //11 Compose the response
            from.PushResponseFirstLine(RTSP_VERSION_1_0, 200, "OK");
            @from.PushResponseHeader(RTSP_HEADERS_TRANSPORT,
                forceTcp
                    ? "RTP/AVP/TCP;unicast;interleaved="+ transport["interleaved","all"]
                    : $"RTP/AVP/UDP;unicast;source={((TCPCarrier) @from.IOHandler).NearIP};client_port={(string)(transport["client_port","all"])};server_port={(isAudioTrack ? (pOutboundConnectivity.AudioPorts) : (pOutboundConnectivity.VideoPorts))};ssrc={(isAudioTrack ? pOutboundConnectivity.AudioSSRC : pOutboundConnectivity.VideoSSRC)}");
            //12. Done
            return from.SendResponseMessage();
        }

        private OutboundConnectivity GetOutboundConnectivity(RtspProtocol pFrom, bool forceTcp)
        {
            //1. Get the inbound stream
            var pInNetStream =
                    (IInNetStream)Application.StreamsManager.FindByUniqueId(
                    pFrom.CustomParameters["streamId"]);
            if (pInNetStream == null)
            {
                FATAL("Inbound stream {0} not found",pFrom.CustomParameters["streamId"]);
                return null;
            }

            //2. Get the outbound connectivity
            OutboundConnectivity pOutboundConnectivity = pFrom.GetOutboundConnectivity(pInNetStream, forceTcp);
                    
            if (pOutboundConnectivity == null)
            {
                FATAL("Unable to get the outbound connectivity");
                return null;
            }
            return pOutboundConnectivity;
        }

        private bool HandleRTSPRequestSetupInbound(RtspProtocol @from, Variant requestHeaders, string requestContent)
        {
            //1. get the transport line and split it into parts
            if (requestHeaders[RTSP_HEADERS, RTSP_HEADERS_TRANSPORT] == null)
            {
                FATAL("No transport line");
                return false;
            }
            string transportLine = requestHeaders[RTSP_HEADERS, RTSP_HEADERS_TRANSPORT];
            Variant transport = Variant.Get();
            if (!SDP.ParseTransportLine(transportLine, transport))
            {
                FATAL("Unable to parse transport line");
                return false;
            }

            //2. Check and see if it has RTP/AVP/TCP,RTP/AVP/UDP or RTP/AVP
            if ((transport["rtp/avp/tcp"] == null)
                    && (transport["rtp/avp/udp"] == null)
                    && (transport["rtp/avp"] == null))
            {
                FATAL("Invalid transport line: {0}", (transportLine));
                return false;
            }

            //3. Check to see if it has either client_port OR interleaved
            if ((transport["client_port"] == null)
                    && (transport["interleaved"] == null))
            {
                FATAL("Invalid transport line: {0}", (transportLine));
                return false;
            }
            if ((transport["client_port"] != null)
                    && (transport["interleaved"]!=null))
            {
                FATAL("Invalid transport line: {0}", (transportLine));
                return false;
            }

            //4. Get the InboundConnectivity
            InboundConnectivity pConnectivity = from.InboundConnectivity;

            //4. Find the track inside the pendingTracks collection and setup the ports or channels
            if (from.CustomParameters["pendingTracks"] != VariantType.Map)
            {
                FATAL("Invalid state. No pending tracks");
                return false;
            }
            string controlUri = requestHeaders[RTSP_FIRST_LINE,RTSP_URL];


            var track =
                @from.CustomParameters["pendingTracks"].Children.Values.SingleOrDefault(x => x["controlUri"] == controlUri);
                    
            if (track == null)
            {
                FATAL("track {0} not found", (controlUri));
                return false;
            }
                if (transport["client_port"]!=null)
                {
                    track["portsOrChannels"] = transport["client_port"];
                    track["isTcp"] =false;
                }
                else
                {
                    track["portsOrChannels"] = transport["interleaved"];
                    track["isTcp"] = true;
                }
                if (!pConnectivity.AddTrack(track, (bool)track["isAudio"]))
                {
                    FATAL("Unable to add audio track");
                    return false;
                }
                transportLine = pConnectivity.GetTransportHeaderLine(track["isAudio"], false);
  
            //5. Create a session
            from.GenerateSessionId();

            //6. prepare the response
            from.PushResponseFirstLine(RTSP_VERSION_1_0, 200, "OK");
            from.PushResponseHeader(RTSP_HEADERS_TRANSPORT, transportLine);

            //7. Send it
            return from.SendResponseMessage();
        }

        public override void Broadcast(BaseProtocol @from, Variant invokeInfo)
        {
            //throw new System.NotImplementedException();
        }

        public override void CallClient(BaseProtocol to, string functionName, Variant param)
        {
            //throw new System.NotImplementedException();
        }

        public override void SharedObjectTrack(BaseProtocol to, string name, uint version, bool isPersistent, Variant primitives)
        {
            //throw new System.NotImplementedException();
        }

        public bool HandleRTSPResponse(RtspProtocol rtspProtocol, Variant responseHeaders,ref string responseContent)
        {
           
            if (responseHeaders[RTSP_HEADERS, RTSP_HEADERS_SESSION] != null)
            {
                rtspProtocol.SessionId = responseHeaders[RTSP_HEADERS, RTSP_HEADERS_SESSION];
            }
            if (responseHeaders[RTSP_HEADERS, RTSP_HEADERS_CSEQ] == null)
            {
                FATAL("Invalid response:\n{0}", (responseHeaders.ToString()));
                return false;
            }
            Variant requestHeaders = Variant.Get();
            string requestContent = "";
            rtspProtocol.GetRequest(responseHeaders[RTSP_HEADERS, RTSP_HEADERS_CSEQ], requestHeaders, ref requestContent);
            
            //2. Get the request, get the response and call the stack further
            return HandleRTSPResponse(rtspProtocol,
                    requestHeaders,
                   ref requestContent,
                    responseHeaders,
                   ref responseContent);

        }

        private bool HandleRTSPResponse(RtspProtocol rtspProtocol, Variant requestHeaders,ref string requestContent, Variant responseHeaders,ref string responseContent)
        {
            switch ((uint)responseHeaders[RTSP_FIRST_LINE, RTSP_STATUS_CODE])
            {
                case 200:
                    switch ((string)requestHeaders[RTSP_FIRST_LINE,RTSP_METHOD])
                    {
                        case RTSP_METHOD_OPTIONS:
                            return HandleRTSPResponse200Options(rtspProtocol,requestHeaders,ref requestContent, responseHeaders,ref responseContent);
                        case RTSP_METHOD_DESCRIBE:
                            return HandleRTSPResponse200Describe(rtspProtocol, requestHeaders, ref requestContent, responseHeaders, ref responseContent);
                        case RTSP_METHOD_SETUP:
                            return HandleRTSPResponse200Setup(rtspProtocol, requestHeaders, ref requestContent, responseHeaders, ref responseContent);
                        case RTSP_METHOD_PLAY:
                            return HandleRTSPResponse200Play(rtspProtocol, requestHeaders, ref requestContent, responseHeaders, ref responseContent);
                        case RTSP_METHOD_ANNOUNCE:
                            return HandleRTSPResponse200Announce(rtspProtocol, requestHeaders, ref requestContent, responseHeaders, ref responseContent);
                        case RTSP_METHOD_RECORD:
                            return HandleRTSPResponse200Record(rtspProtocol, requestHeaders, ref requestContent, responseHeaders, ref responseContent);
                        case RTSP_METHOD_TEARDOWN:
                            return true;
                        default:
                            return false;
                    }
                case 401:
                    var username = rtspProtocol.CustomParameters["uri", "userName"];
                    var password = rtspProtocol.CustomParameters["uri", "password"];
                    if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                    {
                        FATAL("No username/password provided");
                        return false;
                    }
                    string auth = responseHeaders[RTSP_HEADERS, HTTP_HEADERS_WWWAUTHENTICATE];
                    if (string.IsNullOrEmpty(auth))
                    {
                        FATAL("Invalid 401 response: {0}", (responseHeaders.ToString()));
                        return false;
                    }
                    if (!rtspProtocol.SetAuthentication(auth, username, password))
                    {
                        FATAL("Unable to authenticate: request headers:\n{0}\nresponseHeaders:\n{1}",
                (requestHeaders.ToString()),
                (responseHeaders.ToString()));
                        return false;
                    }
                    return true;
                case 404:
                    switch ((string)requestHeaders[RTSP_FIRST_LINE, RTSP_METHOD])
                    {
                        case RTSP_METHOD_PLAY:
                            FATAL("PLAY: Resource not found: "+(requestHeaders[RTSP_FIRST_LINE][RTSP_URL]));
                            return false;
                        case RTSP_METHOD_DESCRIBE:
                            FATAL("DESCRIBE: Resource not found: "+(requestHeaders[RTSP_FIRST_LINE][RTSP_URL]));
                            return false;
                        default:
                            FATAL("Response for method {0} not implemented yet\n{1}", ((string)requestHeaders[RTSP_FIRST_LINE, RTSP_METHOD]),
                (responseHeaders.ToString()));
                            return false;
                    }
                default:
                    return false;
            }
        }



        private bool HandleRTSPResponse200Record(RtspProtocol rtspProtocol, Variant requestHeaders, ref string requestContent, Variant responseHeaders, ref string responseContent)
        {
            bool forceTcp = rtspProtocol.CustomParameters["forceTcp"];
            var pConnectivity = GetOutboundConnectivity(rtspProtocol, forceTcp);
            if (pConnectivity == null)
            {
                FATAL("Unable to get outbound connectivity");
                return false;
            }
            bool result = false;
            var param = rtspProtocol.CustomParameters;
            if (param["audioTransport"] != null)
            {
                if (forceTcp)
                {
                    if (
                        !pConnectivity.RegisterTCPAudioClient(rtspProtocol.Id,
                            param["audioTransport", "interleaved", "data"],
                            param["audioTransport", "interleaved", "rtcp"]))
                    {
                        FATAL("Unable to register audio stream");
                        return false;
                    }
                }
                else
                {
                    
                    var dataAddress = new IPEndPoint((rtspProtocol.IOHandler as TCPCarrier).FarInfo.Address, (param["audioTransport", "interleaved", "data"]));
                    var rtcpAddress = new IPEndPoint((rtspProtocol.IOHandler as TCPCarrier).FarInfo.Address, (param["audioTransport", "interleaved", "rtcp"]));
                    if (
                        !pConnectivity.RegisterUDPAudioClient(rtspProtocol.Id,dataAddress, rtcpAddress))   
                    {
                        FATAL("Unable to register audio stream");
                        return false;
                    }
                }
                result = true;
            }
            if (param["videoTransport"] != null)
            {
                if (forceTcp)
                {
                    if (
                        !pConnectivity.RegisterTCPVideoClient(rtspProtocol.Id,
                            param["videoTransport", "interleaved", "data"],
                            param["videoTransport", "interleaved", "rtcp"]))
                    {
                        FATAL("Unable to register video stream");
                        return false;
                    }
                }
                else
                {
                    var dataAddress = new IPEndPoint((rtspProtocol.IOHandler as TCPCarrier).FarInfo.Address,(param["videoTransport", "interleaved", "data"]));
                    var rtcpAddress = new IPEndPoint((rtspProtocol.IOHandler as TCPCarrier).FarInfo.Address,(param["videoTransport", "interleaved", "rtcp"]));
                    if (
                        !pConnectivity.RegisterUDPVideoClient(rtspProtocol.Id, dataAddress, rtcpAddress))
                    {
                        FATAL("Unable to register video stream");
                        return false;
                    }
                }
                result = true;
            }
            return result;
        }

        private bool HandleRTSPResponse200Announce(RtspProtocol rtspProtocol, Variant requestHeaders, ref string requestContent, Variant responseHeaders, ref string responseContent)
        {
            bool forceTcp = rtspProtocol.CustomParameters["forceTcp"];

            //3. Get the outbound connectivity
            var pConnectivity = GetOutboundConnectivity(rtspProtocol,forceTcp);
            if (pConnectivity == null)
            {
                FATAL("Unable to get the outbound connectivity");
                return false;
            }
            var param = rtspProtocol.CustomParameters;
            string trackId = "";
            bool isAudio = false;
            if (param["audioTrackId"] != null)
            {
                trackId = param["audioTrackId"];
                param["audioTrackId"] = null;
                param["lastSetup"] = "audio";
                isAudio = true;
                pConnectivity.HasAudio = true;
            }
            else
            {
                if (param["videoTrackId"] != null)
                {
                    trackId = param["videoTrackId"];
                    param["videoTrackId"] = null;
                    param["lastSetup"] = "video";
                    pConnectivity.HasVideo = true;
                }
            }
            if (trackId != "")
            {
                var variantUri = param["uri"];
                var uri = variantUri["fullUri"] + "/trackID=" + trackId;
                rtspProtocol.PushRequestFirstLine(RTSP_METHOD_SETUP, uri, RTSP_VERSION_1_0);

                var transport = forceTcp ? $"RTP/AVP/TCP;unicast;interleaved={(isAudio ? pConnectivity.AudioChannels : pConnectivity.VideoChannels)};mode=record" : $"RTP/AVP;unicast;client_port={(isAudio ? pConnectivity.AudioChannels : pConnectivity.VideoChannels)};mode=record";
                rtspProtocol.PushRequestHeader(RTSP_HEADERS_TRANSPORT, transport);
                return rtspProtocol.SendRequestMessage();
            }
            else
            {
                FATAL("Bogus RTSP connection");
               rtspProtocol.EnqueueForDelete();
                return false;
            }
        }

        private bool HandleRTSPResponse200Play(RtspProtocol rtspProtocol, Variant requestHeaders, ref string requestContent, Variant responseHeaders, ref string responseContent)
        {
            //1. Get the inbound connectivity
            if (rtspProtocol.InboundConnectivity == null)
            {
                FATAL("Unable to get inbound connectivity");
                return false;
            }
            //2. Create the stream
            if (!rtspProtocol.InboundConnectivity.Initialize())
            {
                FATAL("Unable to initialize inbound connectivity");
                return false;
            }
            //3. Enable keep alive
            return rtspProtocol.EnableKeepAlive(10, rtspProtocol.CustomParameters["uri","fullUri"]);
        }

        private bool HandleRTSPResponse200Setup(RtspProtocol rtspProtocol, Variant requestHeaders, ref string requestContent, Variant responseHeaders, ref string responseContent)
        {
            if (rtspProtocol.CustomParameters["connectionType"] == "pull")
            {
                if (responseHeaders[RTSP_FIRST_LINE, RTSP_STATUS_CODE] != 200)
                {
                    FATAL("request {0} failed with response {1}",
                        (requestHeaders.ToString()),
                        (responseHeaders.ToString()));
                    return false;
                }
                if (rtspProtocol.CustomParameters["pendingTracks"].ArrayLength != 0)
                    return SendSetupTrackMessages(rtspProtocol);
                //2. Do the play command
                var uri = rtspProtocol.CustomParameters["uri", "fullUri"];
                //3. prepare the play command
                rtspProtocol.PushRequestFirstLine(RTSP_METHOD_PLAY, uri, RTSP_VERSION_1_0);
                return rtspProtocol.SendRequestMessage();
            }
            else
            {
                if (responseHeaders[RTSP_HEADERS, RTSP_HEADERS_TRANSPORT] == null)
                {
                    FATAL("RTSP {0} request doesn't have {1} header line",
                    RTSP_METHOD_SETUP,
                    RTSP_HEADERS_TRANSPORT);
                    return false;
                }
                //3. get the transport header line
                var raw = responseHeaders[RTSP_HEADERS, RTSP_HEADERS_TRANSPORT];
                var transport = Variant.Get();
                if (!SDP.ParseTransportLine(raw, transport))
                {
                    FATAL("Unable to parse transport line {0}", (raw));
                    return false;
                }
                bool forceTcp;
                if (transport["server_port"] != null &&
                    (transport["rtp/avp/udp"] != null || transport["rtp/avp"] != null))
                {
                    forceTcp = false;
                }else if (transport["interleaved"] != null && transport["rtp/avp/tcp"] != null)
                {
                    forceTcp = true;
                }
                else
                {
                    FATAL("Invalid transport line: {0}", (transport.ToString()));
                    return false;
                }
                if (forceTcp != (bool)rtspProtocol.CustomParameters["forceTcp"])
                {
                    FATAL("Invalid transport line: {0}", (transport.ToString()));
                    return false;
                }
                var pConnectivity = GetOutboundConnectivity(rtspProtocol, forceTcp);
                if (pConnectivity == null)
                {
                    FATAL("Unable to get outbound connectivity");
                    return false;
                }
                var param = rtspProtocol.CustomParameters;
                param[param["lastSetup"] == "audio" ? "audioTransport" : "videoTransport"] = transport;
                var variantUri = param["uri"];
                string trackId = "";
                bool isAudio = false;
                if (param["audioTrackId"] != null)
                {
                    trackId = param["audioTrackId"];
                    param["audioTrackId"] = null;
                    param["lastSetup"] = "audio";
                    isAudio = true;
                    pConnectivity.HasAudio = true;
                }
                else
                {
                    if (param["videoTrackId"] != null)
                    {
                        trackId = param["videoTrackId"];
                        param["videoTrackId"] = null;
                        param["lastSetup"] = "video";
                        pConnectivity.HasVideo = true;
                    }
                }
                if (trackId != "")
                {
                    var uri = variantUri["fullUri"] + "/trackID=" + trackId;
                    rtspProtocol.PushRequestFirstLine(RTSP_METHOD_SETUP, uri, RTSP_VERSION_1_0);
             
                    transport = forceTcp ? $"RTP/AVP/TCP;unicast;interleaved={(isAudio ? pConnectivity.AudioChannels : pConnectivity.VideoChannels)};mode=record" : $"RTP/AVP;unicast;client_port={(isAudio ? pConnectivity.AudioChannels : pConnectivity.VideoChannels)};mode=record";
                    rtspProtocol.PushRequestHeader(RTSP_HEADERS_TRANSPORT, transport);
                    return rtspProtocol.SendRequestMessage();
                }
                else
                {
                    rtspProtocol.PushRequestFirstLine(RTSP_METHOD_RECORD,variantUri["fullUri"],RTSP_VERSION_1_0);
                    return rtspProtocol.SendRequestMessage();
                }

            }
        }

        private bool HandleRTSPResponse200Describe(RtspProtocol rtspProtocol, Variant requestHeaders, ref string requestContent, Variant responseHeaders, ref string responseContent)
        {
            //1. Make sure we ONLY handle application/sdp
            if (responseHeaders[RTSP_HEADERS, RTSP_HEADERS_CONTENT_TYPE] == null)
            {
                FATAL("Invalid DESCRIBE response:\n{0}", (requestHeaders.ToString()));
                return false;
            }
            if (responseHeaders[RTSP_HEADERS, RTSP_HEADERS_CONTENT_TYPE] == RTSP_HEADERS_ACCEPT_APPLICATIONSDP)
            {
                FATAL("Invalid DESCRIBE response:\n{0}", (requestHeaders.ToString()));
                return false;
            }
            //2. Get the SDP
            var sdp = rtspProtocol.InboundSDP;
            //3. Parse the SDP
            if (!SDP.ParseSDP(sdp, responseContent))
            {
                FATAL("Unable to parse the SDP");
                return false;
            }
            //4. Get the first video track
            var videoTrack = sdp.GetVideoTrack(0,requestHeaders[RTSP_FIRST_LINE,RTSP_URL]);
            var audioTrack = sdp.GetAudioTrack(0,requestHeaders[RTSP_FIRST_LINE,RTSP_URL]);
            if ((videoTrack == VariantType.Null) && (audioTrack == VariantType.Null))
            {
                FATAL("No compatible tracks found");
                return false;
            }
            var forceTcp = rtspProtocol.CustomParameters["forceTcp"];
            var rtcpDetectionInterval = Application.Configuration[CONF_APPLICATION_RTCPDETECTIONINTERVAL];
            if (rtspProtocol.CustomParameters[CONF_APPLICATION_RTCPDETECTIONINTERVAL]!=null)
                rtcpDetectionInterval = (byte)rtspProtocol.CustomParameters[CONF_APPLICATION_RTCPDETECTIONINTERVAL];
            //5. Store the tracks inside the session for later use
            if (audioTrack != VariantType.Null)
            {
                audioTrack["isTcp"] = (bool)forceTcp;
                rtspProtocol.CustomParameters["pendingTracks"][(int)audioTrack["globalTrackIndex"]] = audioTrack;
            }
            if (videoTrack != VariantType.Null)
            {
                videoTrack["isTcp"] = (bool)forceTcp;
                rtspProtocol.CustomParameters["pendingTracks"][(int)videoTrack["globalTrackIndex"]] = videoTrack;
            }
            //6. Save the streamName
            string streamName = sdp.GetStreamName();
            if (streamName == "")
            {
                streamName = "rtsp_stream_"+rtspProtocol.Id;
            }
            rtspProtocol.CustomParameters["sdpStreamName"] = streamName;
            //7. Save the bandwidth hint
            rtspProtocol.CustomParameters["sdpBandwidthHint"] = sdp.GetTotalBandwidth();
            //8. Get the inbound connectivity
            var pInboundConnectivity = rtspProtocol.GetInboundConnectivity(streamName, sdp.GetTotalBandwidth(),
                rtcpDetectionInterval);
            if (pInboundConnectivity == null)
            {
                FATAL("Unable to create inbound connectivity");
                return false;
            }
            //9. Start sending the setup commands on the pending tracks;
            return SendSetupTrackMessages(rtspProtocol);
        }

        private bool SendSetupTrackMessages(RtspProtocol rtspProtocol)
        {
            //1. Get the pending tracks
            if (rtspProtocol.CustomParameters["pendingTracks"].ArrayLength == 0)
            {
                WARN("No more tracks");
                return true;
            }
            //2. Get the inbound connectivity
            if (rtspProtocol.InboundConnectivity == null)
            {
                FATAL("Unable to get inbound connectivity");
                return false;
            }
            //3. Get the first pending track
            var track = rtspProtocol.CustomParameters["pendingTracks"][0];
            if (!track.IsMap)
            {
                FATAL("Invalid track");
                return false;
            }
            //4. Add the track to the inbound connectivity
            if (!rtspProtocol.InboundConnectivity.AddTrack(track,track["isAudio"]))
            {
                FATAL("Unable to add the track to inbound connectivity");
                return false;
            }

            //6. Prepare the SETUP request
            rtspProtocol.PushRequestFirstLine(RTSP_METHOD_SETUP,
           track["controlUri"], RTSP_VERSION_1_0);
            rtspProtocol.PushRequestHeader(RTSP_HEADERS_TRANSPORT, rtspProtocol.InboundConnectivity.GetTransportHeaderLine(track["isAudio"], true));
            //7. Remove the track from pending
            rtspProtocol.CustomParameters["pendingTracks"].RemoveAt(0);
            //8. Send the request message
            return rtspProtocol.SendRequestMessage();
        }

        private bool HandleRTSPResponse200Options(RtspProtocol rtspProtocol, Variant requestHeaders, ref string requestContent, Variant responseHeaders, ref string responseContent)
        {
            if (rtspProtocol.HasConnectivity) return true;
            if (rtspProtocol.CustomParameters["connectionType"] == null)
            {
                FATAL("Bogus connection");
                rtspProtocol.EnqueueForDelete();
                return false;
            }
            //1. Sanitize
            if (responseHeaders[RTSP_HEADERS, RTSP_HEADERS_PUBLIC] == null)
            {
                FATAL("Invalid response:\n{0}", (responseHeaders.ToString()));
                return false;
            }
            //2. get the raw options
            string raw = responseHeaders[RTSP_HEADERS, RTSP_HEADERS_PUBLIC];
            //3. split and normalize the options
            var parts = raw.Split(',').Select(x => x.Split(':')).ToDictionary(x => x[0], x => x[1]);
            string url = requestHeaders[RTSP_FIRST_LINE, RTSP_URL];
            switch ((string)rtspProtocol.CustomParameters["connectionType"])
            {
                case "pull":
                    //4. Test the presence of the wanted methods
                    if (!parts.ContainsKey(RTSP_METHOD_DESCRIBE) || !parts.ContainsKey(RTSP_METHOD_SETUP) ||
                        !parts.ContainsKey(RTSP_METHOD_PLAY))
                    {
                        FATAL("Some of the supported methods are missing: {0}", (raw));
                        return false;
                    }
                    rtspProtocol.PushRequestFirstLine(RTSP_METHOD_DESCRIBE, url, RTSP_VERSION_1_0);
                    rtspProtocol.PushRequestHeader(RTSP_HEADERS_ACCEPT, RTSP_HEADERS_ACCEPT_APPLICATIONSDP);
                    return rtspProtocol.SendRequestMessage();
                case "push":
                    //4. Test the presence of the wanted methods
                    if (!parts.ContainsKey(RTSP_METHOD_ANNOUNCE) || !parts.ContainsKey(RTSP_METHOD_SETUP) ||
                        !parts.ContainsKey(RTSP_METHOD_RECORD))
                    {
                        FATAL("Some of the supported methods are missing: {0}", (raw));
                        return false;
                    }
                    var parameters = rtspProtocol.CustomParameters;
                    rtspProtocol.PushRequestFirstLine(RTSP_METHOD_ANNOUNCE,url,RTSP_VERSION_1_0);
                    var sdp = ComputeSDP(rtspProtocol,
                        parameters["customParameters","localStreamConfig","localStreamName"],
                        parameters["customParameters","localStreamConfig","targetStreamName"],
                        parameters["customParameters","localStreamConfig","targetUri","host"]);
                    if (sdp == "")
                    {
                        FATAL("Unable to compute sdp");
                        return false;
                    }
                    rtspProtocol.PushRequestHeader(RTSP_HEADERS_CONTENT_TYPE, RTSP_HEADERS_ACCEPT_APPLICATIONSDP);
                    rtspProtocol.PushRequestContent(sdp,false);
                    return rtspProtocol.SendRequestMessage();
                default:
                    FATAL("Bogus connection");
                    rtspProtocol.EnqueueForDelete();
                    return false;
            }
        }
    }
}