using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Timer;
using CSharpRTMP.Core.Streaming;
using static CSharpRTMP.Common.Defines;
using static CSharpRTMP.Common.Logger;
using Debug = System.Diagnostics.Debug;

namespace CSharpRTMP.Core.Protocols.Rtsp
{
    public class RtspKeepAliveTimer : BaseTimerProtocol
    {
        private readonly uint _protocolId;
        public override MemoryStream OutputBuffer { get; } = Utils.Rms.GetStream();

        public RtspKeepAliveTimer(uint protocolId)
        {
            _protocolId = protocolId;
        }

        public override bool TimePeriodElapsed()
        {
            var protocol = ProtocolManager.GetProtocol(_protocolId) as RtspProtocol;
            if (protocol == null)
            {
                FATAL("Unable to get parent protocol");
                return false;
            }
            if (!protocol.SendKeepAliveOptions())
            {
                FATAL("Unable to send keep alive options");
                return false;
            }
            return true;
        }
    }

    [ProtocolType(ProtocolTypes.PT_RTSP)]
    [AllowFarTypes(ProtocolTypes.PT_TCP)]
    [AllowNearTypes(ProtocolTypes.PT_INBOUND_RTP)]
    public class RtspProtocol : BaseProtocol
    {

        enum RtspState
        {
            Header,NormalHeader, InterleavedHeader,Playload
        }
        private bool _rtpData;
        private RtspState _state;
        private uint _rtpDataLength;
        private uint _rtpDataChanel;
        private uint _contentLength;
        private uint _requestSequence;
        private uint _keepAliveTimerId;
        public Variant InboundSDP = Variant.Get();
        private Variant _authentication = Variant.Get();
        private BaseRtspAppProtocolHandler _protocolHandler;
        private string _keepAliveURI;
        private Variant _requestHeaders;
        private string _requestContent;
        private Dictionary<uint, Variant> _pendingRequestHeaders = new Dictionary<uint, Variant>();
        private Dictionary<uint, Variant> _pendingRequestContent = new Dictionary<uint, Variant>();
        public uint LastRequestSequence;
        private Variant _responseHeaders = Variant.Get();
        private string _responseContent;
        private Variant _inboundHeaders = Variant.Get();
        private string _inboundContent;
        public OutboundConnectivity OutboundConnectivity;
        public InboundConnectivity InboundConnectivity;
     
        private string _sessionId;
       
        public const uint RTSP_MAX_LINE_LENGTH = 256;
        public const uint RTSP_MAX_HEADERS_COUNT = 64;
        public const uint RTSP_MAX_HEADERS_SIZE = 2028;
        public const uint RTSP_MAX_CHUNK_SIZE = 1024*128;
        public RtspProtocol()
        {
            _state = RtspState.Header;
            _rtpData = false;

        }

        public override MemoryStream OutputBuffer { get; } = Utils.Rms.GetStream();

        public override bool Initialize(Variant parameters)
        {
            CustomParameters = parameters;
            return true;
        }

        public override BaseClientApplication Application
        {
            get { return _application; }
            set
            {
                base.Application = value;
                if (_application != null)
                {
                    _protocolHandler = _application.GetProtocolHandler(Type) as BaseRtspAppProtocolHandler;
                    if (_protocolHandler == null)
                    {
                        FATAL("Protocol handler not found");
                        EnqueueForDelete();
                    }
                }
                else
                {
                    _protocolHandler = null;
                }
            }
        }


        public override void GetStats(Variant info, uint namespaceId)
        {
            base.GetStats(info, namespaceId);
            info["streams"].IsArray = true;

            if (Application != null)
            {
                var streamsManager = Application.StreamsManager;
                var streams = streamsManager.FindByProtocolId(Id);
                foreach (var stream in streams.Values)
                {
                    var si = new Variant();
                    stream.GetStats(si, namespaceId);
                    info["streams"].Add(si);
                }
            }
        }

        public string SessionId
        {
            get { return _sessionId; }
            set
            {
                var parts = value.Split(';');
                if (parts.Length >= 1) _sessionId = parts[0];
                if (string.IsNullOrEmpty(_sessionId))
                {
                    _sessionId = value;
                }

            }
        }

        public bool SetAuthentication(string wwwAuthenticateHeader, string userName, string password)
        {
            if (_authentication != VariantType.Null)
            {
                FATAL("Authentication was setup but it failed");
                return false;
            }
            _authentication["userName"] = userName;
            _authentication["password"] = password;
            _authentication["lastWwwAuthenticateHeader"] = wwwAuthenticateHeader;
            //2. re-do the request
            return SendRequestMessage();
        }

        public string GenerateSessionId()
        {
            if (!string.IsNullOrEmpty(_sessionId)) return _sessionId;
            _sessionId = Utils.GenerateRandomString(8);
            return _sessionId;
        }

        public bool EnableKeepAlive(uint period, string keepAliveURI)
        {
            var timer = new RtspKeepAliveTimer(Id);
            _keepAliveTimerId = timer.Id;
            _keepAliveURI = keepAliveURI.Trim();
            if (_keepAliveURI == string.Empty) _keepAliveURI = "*";
            return timer.EnqueueForTimeEvent(period);
        }

        public bool SendKeepAliveOptions()
        {
            PushRequestFirstLine(RTSP_METHOD_OPTIONS, _keepAliveURI, RTSP_VERSION_1_0);
            if (CustomParameters[RTSP_HEADERS_SESSION] != null)
            {
                PushRequestHeader(RTSP_HEADERS_SESSION,
                    CustomParameters[RTSP_HEADERS_SESSION]);
            }
            return SendRequestMessage();
        }


        public bool HasConnectivity => InboundConnectivity != null || OutboundConnectivity != null;

       

        public override bool SignalInputData(int recAmount)
        {
            while (InputBuffer.AvaliableByteCounts > 0)
            {
                switch (_state)
                {
                    case RtspState.InterleavedHeader:
                        //6. Do we have enough data?
                        if (InputBuffer.AvaliableByteCounts < _rtpDataLength)
                        {
                            return true;
                        }
                        _state = RtspState.Playload;
                        goto case RtspState.Playload;
                    case RtspState.NormalHeader:
                        ParseNormalHeaders();
                        if (_state != RtspState.Playload)
                            return true;
                        goto case RtspState.Playload;
                    case RtspState.Header:
                        
                        if (InputBuffer.Reader.PeekChar() == '$')
                        {
                            //1. Marl this as a interleaved content
                            _rtpData = true;
                            //2. Do we have at least 4 bytes ($ sign, channel byte an 2-bytes length)?
                            var bufferLength = InputBuffer.AvaliableByteCounts;
                            if (bufferLength < 4) return true;
                            //3. Get the buffer
                            InputBuffer.Reader.ReadByte();
                            //4. Get the channel id
                            _rtpDataChanel = InputBuffer.Reader.ReadByte();
                            //5. Get the packet length
                            _rtpDataLength = InputBuffer.Reader.ReadUInt16();

                            if (_rtpDataLength > 8192)
                            {
                                FATAL("RTP data length too big");
                                return false;
                            }
                            //6. Do we have enough data?
                            if (_rtpDataLength + 4 > bufferLength)
                            {
                                _state = RtspState.InterleavedHeader;
                                return true;
                            }
                            _state = RtspState.Playload;
                            goto case RtspState.Playload;
                        }
                        _state = RtspState.NormalHeader;
                        goto case RtspState.NormalHeader;
                    case RtspState.Playload:
                        if (_rtpData)
                        {
                            if (InboundConnectivity != null)
                            {
                                if (!InboundConnectivity.FeedData(
                                    _rtpDataChanel, InputBuffer, _rtpDataLength))
                                {
                                    FATAL("Unable to handle raw RTP packet");
                                    return false;
                                }
                            }
                            else
                            {
                                InputBuffer.Ignore(_rtpDataLength);
                            }
                            _state = RtspState.Header;
                        }
                        else
                        {
                            if (!HandleRTSPMessage())
                            {
                                FATAL("Unable to handle content");
                                return false;
                            }
                        }
                        break;
                  
                }
            }
            return false;
        }


        public void PushRequestFirstLine(string method, string url,
            string version)
        {
            _requestHeaders.SetValue();
            _requestContent = "";
            _requestHeaders[RTSP_FIRST_LINE, RTSP_METHOD] = method;
            _requestHeaders[RTSP_FIRST_LINE, RTSP_URL] = url;
            _requestHeaders[RTSP_FIRST_LINE, RTSP_VERSION] = version;
        }

        public void PushRequestHeader(string name, string value) => _requestHeaders[RTSP_HEADERS,name] = value;

        public void PushRequestContent(string outboundContent, bool append)
        {
            if (append)
                _requestContent += "\r\n" + outboundContent;
            else
                _requestContent = outboundContent;
        }

        public bool SendRequestMessage()
        {
            //1. Put the first line
            OutputBuffer.Write(_requestHeaders[RTSP_FIRST_LINE, RTSP_METHOD]+" "+_requestHeaders[RTSP_FIRST_LINE, RTSP_URL] + " " + _requestHeaders[RTSP_FIRST_LINE, RTSP_VERSION] + "\r\n");

            //2. Put our request sequence in place
            _requestHeaders[RTSP_HEADERS, RTSP_HEADERS_CSEQ] = (++_requestSequence).ToString();

            //3. Put authentication if necessary
            if (_authentication == VariantType.Null)
            {
                if (!HTTPAuthHelper.GetAuthorizationHeader(
                    _authentication["lastWwwAuthenticateHeader"],
                    _authentication["userName"],
                    _authentication["password"],
                    _requestHeaders[RTSP_FIRST_LINE, RTSP_URL],
                    _requestHeaders[RTSP_FIRST_LINE, RTSP_METHOD],
                    _authentication["temp"]))
                {
                    FATAL("Unable to create authentication header");
                    return false;
                }

                _requestHeaders[RTSP_HEADERS, HTTP_HEADERS_AUTORIZATION] =
                    _authentication["temp", "authorizationHeader", "raw"];
            }

            _pendingRequestHeaders[_requestSequence] = _requestHeaders;
            _pendingRequestContent[_requestSequence] = _requestContent;
            if (_pendingRequestHeaders.Count > 10 || _pendingRequestContent.Count > 10)
            {
                FATAL("Requests backlog count too high");
                return false;
            }

            //3. send the mesage
            return SendMessage(_requestHeaders, _requestContent);
        }

        public bool GetRequest(uint seqId, Variant result, ref string content)
        {
            if (_pendingRequestHeaders.ContainsKey(seqId) && _pendingRequestContent.ContainsKey(seqId))
            {
                result.SetValue(_pendingRequestHeaders[seqId]);
                content = _pendingRequestContent[seqId];
            }
            else
            {
                _pendingRequestHeaders.Remove(seqId);
                _pendingRequestContent.Remove(seqId);
                return false;
            }
            return true;
        }

        public void ClearResponseMessage()
        {
            _responseHeaders.SetValue();
            _responseContent = "";
        }

        public void PushResponseFirstLine(string version, uint code, string reason)
        {
            _responseHeaders[RTSP_FIRST_LINE, RTSP_VERSION] = version;
            _responseHeaders[RTSP_FIRST_LINE, RTSP_STATUS_CODE] = code;
            _responseHeaders[RTSP_FIRST_LINE, RTSP_STATUS_CODE_REASON] = reason;
        }

        public void PushResponseHeader(string name, string value) => _responseHeaders[RTSP_HEADERS,name] = value;

        public void PushResponseContent(string outboundContent, bool append)
        {
            if (append)
                _responseContent += "\r\n" + outboundContent;
            else
                _responseContent = outboundContent;
        }

        
        public bool SendResponseMessage()
        {
           
            //1. Put the first line
            OutputBuffer.Write(
                    _responseHeaders[RTSP_FIRST_LINE, RTSP_VERSION] +" "+ _responseHeaders[RTSP_FIRST_LINE, RTSP_STATUS_CODE] + " " + _responseHeaders[RTSP_FIRST_LINE, RTSP_STATUS_CODE_REASON] + "\r\n");

            //2. send the mesage
            return SendMessage(_responseHeaders, _responseContent);
        }

        public OutboundConnectivity GetOutboundConnectivity(IInNetStream pInNetStream, bool forceTcp)
        {
            if (OutboundConnectivity == null)
            {
                BaseOutNetRTPUDPStream pOutStream = new OutNetRTPUDPH264Stream(this,Application.StreamsManager,pInNetStream.Name, forceTcp);
                OutboundConnectivity = new OutboundConnectivity(forceTcp, this);
                if (!OutboundConnectivity.Initialize())
                {
                    FATAL("Unable to initialize outbound connectivity");
                    return null;
                }
                pOutStream.Connectivity = OutboundConnectivity;
                OutboundConnectivity.OutStream=pOutStream;

                if (!pInNetStream.Link(pOutStream))
                {
                    FATAL("Unable to link streams");
                    return null;
                }
            }

            return OutboundConnectivity;
        }
        public void CloseOutboundConnectivity() => OutboundConnectivity = null;

        public bool SendRaw(byte[] buffer)
        {
            OutputBuffer.WriteBytes(buffer);
            return EnqueueForOutbound(OutputBuffer);
        }

        public bool SendRaw(MsgHdr message,ref RTPClient client,bool isAudio,bool isData)
        {
            OutputBuffer.WriteByte((byte) '$');
            if (isAudio)
            {
                OutputBuffer.WriteByte(isData ? client.audioDataChannel : client.audioRtcpChannel);
            }
            else
            {
                OutputBuffer.WriteByte(isData ? client.videoDataChannel : client.videoRtcpChannel);
            }
            OutputBuffer.Write((ushort)message.Buffers.Sum(x=>x.Length));
            foreach (var buffer in message.Buffers)
            {
                OutputBuffer.WriteBytes(buffer);
            }
            return EnqueueForOutbound(OutputBuffer);
        }

        public bool SendMessage(Variant headers, string content)
        {
            //1. Add info about us
            headers[RTSP_HEADERS,RTSP_HEADERS_SERVER] = RTSP_HEADERS_SERVER_US;
            headers[RTSP_HEADERS,RTSP_HEADERS_X_POWERED_BY] = RTSP_HEADERS_X_POWERED_BY_US;
            //2. Add the content length if required
            if (content.Length > 0)
            {
                headers[RTSP_HEADERS,RTSP_HEADERS_CONTENT_LENGTH] = content.Length.ToString();
            }
            //3. Add the session id if necessary
            if (!string.IsNullOrEmpty(_sessionId))
            {
                headers[RTSP_HEADERS,RTSP_HEADERS_SESSION] = _sessionId;
            }
            var sb = new StringBuilder();
            foreach (var header in headers[RTSP_HEADERS].Children)
            {
                sb.AppendLine(header.Key + ": " + header.Value);
            }
            sb.AppendLine();
            sb.Append(content);
            OutputBuffer.Write(sb.ToString());
            return EnqueueForOutbound(OutputBuffer);
        }

        private bool ParseNormalHeaders()
        {
            _inboundHeaders.SetValue();
            _inboundContent = "";
            if (InputBuffer.AvaliableByteCounts < 4) return true;
            //2. Detect the headers boundaries
            var headersSize = 0;
            var markerFound = false;
            var pos = InputBuffer.Position;
            while (InputBuffer.AvaliableByteCounts >= 4)
            {
                if (InputBuffer.Position - pos >= RTSP_MAX_HEADERS_SIZE)
                {
                    FATAL("Headers section too long");
                    return false;
                }
                if (InputBuffer.ReadByte() != 0x0d || InputBuffer.ReadByte() != 0x0a|| InputBuffer.ReadByte() != 0x0d || InputBuffer.ReadByte() != 0x0a) continue;
              
                    markerFound = true;
                    headersSize = (int) (InputBuffer.Position-pos - 4);
                    break;
            }
           
            //3. Are the boundaries correct?
            //Do we have enough data to parse the headers?
            if (headersSize == 0)
            {
                return !markerFound;
            }
            InputBuffer.Position = pos;
            var rawHeaders = Encoding.ASCII.GetString(InputBuffer.Reader.ReadBytes(headersSize)); 
            System.Diagnostics.Debug.WriteLine(rawHeaders);
            var lines = rawHeaders.Split(new []{ "\r\n" },StringSplitOptions.None);
            if(lines.Length==0)
            {
                FATAL("Incorrect RTSP request");
                return false;
            }

            //4. Get the fisrt line and parse it. This is either a status code
            //for a previous request made by us, or the request that we just received
            if (!ParseFirstLine(lines[0]))
            {
                FATAL("Unable to parse the first line");
                return false;
            }
            _inboundHeaders[RTSP_HEADERS] = Variant.Get();
            foreach (var line in lines.Skip(1))
            {
                string splitter = ": ";
                if (line.StartsWith(splitter) || line.EndsWith(splitter))
                {
                    splitter = ":";
                    if (line.StartsWith(splitter) || line.EndsWith(splitter))
                    {
                        WARN("Invalid header line: {0}", (line));
                        continue;
                    }
                }

                _inboundHeaders[RTSP_HEADERS][line.Substring(0, line.IndexOf(splitter, StringComparison.Ordinal))] =
                    line.Substring(line.IndexOf(splitter, StringComparison.Ordinal) + splitter.Length);
            }
            //6. default a transfer type to Content-Length: 0 if necessary
            if (_inboundHeaders[RTSP_HEADERS, RTSP_HEADERS_CONTENT_LENGTH] == null)
            {
                _inboundHeaders[RTSP_HEADERS,RTSP_HEADERS_CONTENT_LENGTH] = "0";
            }

            //7. read the transfer type and set this request or response flags
            string contentLengthString = _inboundHeaders[RTSP_HEADERS, RTSP_HEADERS_CONTENT_LENGTH];
            contentLengthString= contentLengthString.Replace(" ", "");
            try
            {
                _contentLength = Convert.ToUInt32(contentLengthString);
            }
            catch
            {
                FATAL("Invalid RTSP headers:\n{0}", (_inboundHeaders.ToString()));
                return false;
            }
           
            //7. Advance the state and ignore the headers part from the buffer
            _state = RtspState.Playload;
            _rtpData = false;
            InputBuffer.Ignore(4);
            return true;
        }

        private bool ParseFirstLine(string line)
        {
            var parts = line.Split(' ');
            if (parts.Length < 3)
            {
                FATAL("Incorrect first line: {0}",line);
                return false;
            }
            
            switch (parts[0])
            {
                case RTSP_VERSION_1_0:
                    uint result;
                    if (!uint.TryParse(parts[1],out result))
                    {
                        FATAL("Invalid RTSP code: {0}", (parts[1]));
                        return false;
                    }
                    var reason = string.Join(" ", parts.Skip(2));
                    _inboundHeaders[RTSP_FIRST_LINE,RTSP_VERSION] = parts[0];
                    _inboundHeaders[RTSP_FIRST_LINE,RTSP_STATUS_CODE] = result;
                    _inboundHeaders[RTSP_FIRST_LINE,RTSP_STATUS_CODE_REASON] = reason;
                    _inboundHeaders["isRequest"] = false;
                    return true;
                case RTSP_METHOD_DESCRIBE:
                case RTSP_METHOD_OPTIONS:
                case RTSP_METHOD_PAUSE:
                case RTSP_METHOD_PLAY:
                case RTSP_METHOD_SETUP:
                case RTSP_METHOD_TEARDOWN:
                case RTSP_METHOD_RECORD:
                case RTSP_METHOD_ANNOUNCE:
                    if (parts[2] != RTSP_VERSION_1_0)
                    {
                        FATAL("RTSP version not supported:{0}", (parts[2]));
                        return false;
                    }

                    _inboundHeaders[RTSP_FIRST_LINE,RTSP_METHOD] = parts[0];
                    _inboundHeaders[RTSP_FIRST_LINE,RTSP_URL] = parts[1];
                    _inboundHeaders[RTSP_FIRST_LINE,RTSP_VERSION] = parts[2];
                    _inboundHeaders["isRequest"] = true;

                    return true;
                default:
                    FATAL("Incorrect first line: {0}", (line));

                    return false;
            }

        }
        private bool HandleRTSPMessage()
        {
            //1. Get the content
            if (_contentLength > 0)
            {
                if (_contentLength > 1024 * 1024)
                {
                    FATAL("Bogus content length: {0}", _contentLength);
                    return false;
                }
                var chunkLength = _contentLength - _inboundContent.Length;
                chunkLength = InputBuffer.AvaliableByteCounts < chunkLength ? InputBuffer.AvaliableByteCounts : chunkLength;
                _inboundContent += InputBuffer.Reader.ReadBytes((int) chunkLength).BytesToString();
                if (_inboundContent.Length < _contentLength)
                {
                    FINEST("Not enough data. Wanted: {0}; got: {1}", _contentLength, _inboundContent.Length);
                    return true;
                }
            }

            //2. Call the protocol handler
            var result = _inboundHeaders["isRequest"] ? _protocolHandler.HandleRTSPRequest(this, _inboundHeaders, _inboundContent) : _protocolHandler.HandleRTSPResponse(this, _inboundHeaders,ref _inboundContent);

            _state = RtspState.Header;
            return result;
        }

        public InboundConnectivity GetInboundConnectivity(string sdpStreamName, uint bandwidthHint, byte rtcpDetectionInterval)
        {
            CloseInboundConnectivity();
            var streamName = ((string)CustomParameters["localStreamName"]) ?? sdpStreamName;
            InboundConnectivity = new InboundConnectivity(this, streamName,
                    bandwidthHint,TimeSpan.FromSeconds(rtcpDetectionInterval) );
            return InboundConnectivity;
        }

        private void CloseInboundConnectivity()
        {
            InboundConnectivity?.Dispose();
            InboundConnectivity = null;
        }
    }
}