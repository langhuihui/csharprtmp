using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Core.Protocols.Rtmp;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Cluster;
using CSharpRTMP.Core.Protocols.Timer;
using CSharpRTMP.Core.Streaming;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Core.Protocols.Rtmp
{
    public class BaseRTMPAppProtocolHandler : BaseAppProtocolHandler
    {
        protected readonly Dictionary<uint, BaseRTMPProtocol> _connections = new Dictionary<uint, BaseRTMPProtocol>();
        protected readonly Dictionary<uint, uint> _nextInvokeId = new Dictionary<uint, uint>();
        public readonly bool ValidateHandshake;
        //protected readonly bool _keyframeSeek;
        //protected readonly int _clientSideBuffer;
        //protected readonly uint _seekGranularity;
        //protected readonly bool _renameBadFiles;
        //protected readonly bool _externSeekGenerator;
        protected readonly bool _enableCheckBandwidth;
        protected DateTime _lastUsersFileUpdate;
        protected readonly AmfMessage _onBWCheckMessage;
        protected AmfMessage _onBWCheckStrippedMessage;

        protected readonly Dictionary<uint, Dictionary<uint, AmfMessage>> _resultMessageTracking = new Dictionary<uint, Dictionary<uint, AmfMessage>>();
        protected const int ONBWCHECK_SIZE = 32767;
        protected string _authMethod;
        protected Variant _adobeAuthSettings;
        protected string _adobeAuthSalt;
        protected Variant _users;
        private BaseRTMPProtocol _externalStreamProtocol;

        public BaseRTMPAppProtocolHandler(Variant configuration)
            : base(configuration)
        {
            ValidateHandshake = configuration[Defines.CONF_APPLICATION_VALIDATEHANDSHAKE];
            //_keyframeSeek = configuration[Defines.CONF_APPLICATION_KEYFRAMESEEK];
            //_clientSideBuffer = configuration[Defines.CONF_APPLICATION_CLIENTSIDEBUFFER];
            //_seekGranularity = (uint)((double)configuration[Defines.CONF_APPLICATION_SEEKGRANULARITY] * 1000);
            //_renameBadFiles = configuration[Defines.CONF_APPLICATION_RENAMEBADFILES];
            //_externSeekGenerator = configuration[Defines.CONF_APPLICATION_EXTERNSEEKGENERATOR];
            // _enableCheckBandwidth = configuration["enableCheckBandwidth"] != null && configuration["enableCheckBandwidth"];
            _enableCheckBandwidth = true;
            if (_enableCheckBandwidth)
            {
                _onBWCheckMessage = GenericMessageFactory.GetInvoke(3, 0, 0, false, 0,
                    Defines.RM_INVOKE_FUNCTION_ONBWCHECK,
                    Variant.GetList(Variant.Get(), Utils.GenerateRandomString(ONBWCHECK_SIZE)));
                _onBWCheckStrippedMessage = new AmfMessage
                {
                    Header =
                        GenericMessageFactory.VH(HeaderType.HT_FULL, 3, 0, 0, Defines.RM_HEADER_MESSAGETYPE_INVOKE, 0,
                            false),

                    Body = Variant.GetMap(new VariantMapHelper
                    {
                        {
                            Defines.RM_INVOKE,
                            Variant.GetMap(new VariantMapHelper
                            {
                                {Defines.RM_INVOKE_FUNCTION, Defines.RM_INVOKE_FUNCTION_ONBWCHECK}
                            })
                        }
                    })
                };
            }
            if (configuration[Defines.CONF_APPLICATION_GENERATE_META_FILES])
            {
                GenerateMetaFiles();
            }
        }

        public override bool ParseAuthenticationNode(Variant node, Variant result)
        {
            //1. Validation
            if (node[Defines.CONF_APPLICATION_AUTH_TYPE] != Defines.CONF_APPLICATION_AUTH_TYPE_ADOBE)
            {
                Logger.FATAL("Invalid authentication type");
                return false;
            }
            if (node[Defines.CONF_APPLICATION_AUTH_ENCODER_AGENTS] == null || node[Defines.CONF_APPLICATION_AUTH_ENCODER_AGENTS].Count == 0)
            {
                Logger.FATAL("Invalid users file path");
                return false;
            }
            //2. Users file validation
            string usersFile = node[Defines.CONF_APPLICATION_AUTH_USERS_FILE];
            if (!usersFile.StartsWith("/") && !usersFile.StartsWith("."))
            {
                usersFile = Configuration[Defines.CONF_APPLICATION_DIRECTORY] + usersFile;
            }
     
            if (File.Exists(usersFile))
            {
                Logger.FATAL("Invalid authentication configuration. Missing users file:{0}", usersFile);
                return false;
            }
            //3. Build the result
            result[Defines.CONF_APPLICATION_AUTH_TYPE] = Defines.CONF_APPLICATION_AUTH_TYPE_ADOBE;
            result[Defines.CONF_APPLICATION_AUTH_USERS_FILE] = usersFile;
            foreach (var item in node[Defines.CONF_APPLICATION_AUTH_ENCODER_AGENTS].Children)
            {
                if (string.IsNullOrEmpty(item.Value))
                {
                    Logger.FATAL("Invalid encoder agent encountered");
                    return false;
                }
                result[Defines.CONF_APPLICATION_AUTH_ENCODER_AGENTS][item.Key] = item.Value;
            }
            result["adobeAuthSalt"] = _adobeAuthSalt = Utils.GenerateRandomString(32);
            _adobeAuthSettings = result;
            _authMethod = Defines.CONF_APPLICATION_AUTH_TYPE_ADOBE;
            var modificationDate = new FileInfo(usersFile).LastWriteTime;
            if (modificationDate != _lastUsersFileUpdate)
            {
                _users.SetValue();
                _users = Variant.DeserializeFromJsonFile(usersFile)["users"];
                _lastUsersFileUpdate = modificationDate;
            }
            return true;
        }
        private void GenerateMetaFiles()
        {
            var mediaFolder = Application.MediaPath;
            var dinfo = new DirectoryInfo(mediaFolder);
            if (dinfo.Exists)
            {
                foreach (var fileInfo in dinfo.GetFiles())
                {
                    var extension = Path.GetExtension(fileInfo.Name).ToLower().TrimStart('.');

                    if (!new[] { "flv", "mp3", "mp4", "m4a", "m4v", "mov", "f4v" }.Contains(extension)) continue;
                    var flashName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                    if (extension != "flv") flashName = extension == "mp3" ? extension + ":" + flashName : "mp4:" + flashName + "." + extension;
                   
                    GetMetaData(flashName, true);
                }
            }
            Logger.FATAL("Unable to list folder");
        }

        private Variant GetMetaData(string streamName, bool extractInnerMetadata)
        {
            return Application.StreamsManager.GetMetaData(streamName, extractInnerMetadata, Configuration);
        }
       
        public override void RegisterProtocol(BaseProtocol protocol)
        {
            if (_connections.ContainsKey(protocol.Id)) return;
            _connections[protocol.Id] = (BaseRTMPProtocol)protocol;
            _nextInvokeId[protocol.Id] = 1;
        }

        public override void UnRegisterProtocol(BaseProtocol protocol)
        {
            
            if (!_connections.ContainsKey(protocol.Id)) return;
            _connections.Remove(protocol.Id);
            _nextInvokeId.Remove(protocol.Id);
            _resultMessageTracking.Remove(protocol.Id);
        }

        private string NormalizeStreamName(string streamName)
        {
            return streamName.Replace('-', '_').Replace('?', '-').Replace('&', '-').Replace('=', '-');
        }

        public bool InboundMessageAvailable(BaseRTMPProtocol pFrom,  Variant messageBody, Channel channel,out bool recycleBody)
        {
            recycleBody = true;
            AmfMessage message;
            message.Header = channel.lastInHeader;
            message.Body = messageBody;
            var parameters = pFrom.CustomParameters;
            if (parameters["authState"] == null)
            {
                parameters["authState"] = Variant.GetMap(new VariantMapHelper
                {
                    {"stage","authenticated"},
                    {"canPublish",true},
                    {"canOverrideStreamName",false}
                });
               
            }
            var authState = parameters["authState"];
            if (pFrom.Type == ProtocolTypes.PT_INBOUND_RTMP && !string.IsNullOrEmpty(_authMethod))
            {
                if (!AuthenticateInbound(pFrom, message, authState))
                {
                    Logger.FATAL("Unable to authenticate");
                    return false;
                }
            }
            uint size;
            Dictionary<uint, IStream> possibleStreams;
            InNetRTMPStream pInNetRTMPStream;
            switch (message.MessageType)
            {
                case Defines.RM_HEADER_MESSAGETYPE_WINACKSIZE:
                    if (messageBody[Defines.RM_WINACKSIZE] != VariantType.Numberic)
                    {
                        Logger.FATAL("Invalid message:{0}", messageBody.ToString());
                        return false;
                    }
                    size = messageBody[Defines.RM_WINACKSIZE];
                    if ((size > 4 * 1024 * 1024) || size == 0)
                    {
                        Logger.FATAL("Invalid message:{0}", messageBody.ToString());
                        return false;
                    }
                    pFrom.SetWinAckSize(size);
                    return true;
                case Defines.RM_HEADER_MESSAGETYPE_PEERBW:
                    Logger.WARN("ProcessPeerBW");
                    return true;
                case Defines.RM_HEADER_MESSAGETYPE_ACK:
                    return true;
                case Defines.RM_HEADER_MESSAGETYPE_CHUNKSIZE:
                    if (messageBody[Defines.RM_CHUNKSIZE] != VariantType.Numberic)
                    {
                        Logger.FATAL("Invalid message:{0}", messageBody.ToString());
                        return false;
                    }
                    size = messageBody[Defines.RM_CHUNKSIZE];
                    if ((size > 4 * 1024 * 1024) || size == 0)
                    {
                        Logger.FATAL("Invalid message:{0}", messageBody.ToString());
                        return false;
                    }
                    if (!pFrom.SetInboundChunkSize(size))
                    {
                        Logger.FATAL("Unable to set chunk size:{0}", messageBody.ToString());
                        return false;
                    }
                    return true;
                case Defines.RM_HEADER_MESSAGETYPE_USRCTRL:
                    switch ((ushort)messageBody[Defines.RM_USRCTRL, Defines.RM_USRCTRL_TYPE])
                    {
                        case Defines.RM_USRCTRL_TYPE_PING_REQUEST:
                            return SendRTMPMessage(pFrom,  ConnectionMessageFactory.GetPong());
                        case Defines.RM_USRCTRL_TYPE_STREAM_BEGIN:
                        case Defines.RM_USRCTRL_TYPE_STREAM_SET_BUFFER_LENGTH:
                        case Defines.RM_USRCTRL_TYPE_STREAM_IS_RECORDED:
                        case Defines.RM_USRCTRL_TYPE_PING_RESPONSE:
                            Logger.WARN("User control message type: {0}",
                                messageBody[Defines.RM_USRCTRL, Defines.RM_USRCTRL_TYPE_STRING]);
                            return true;
                        case Defines.RM_USRCTRL_TYPE_UNKNOWN1:
                        case Defines.RM_USRCTRL_TYPE_UNKNOWN2:
                            return true;
                        default:
                            Logger.FATAL("Invalid user ctrl:\n{0}", messageBody.ToString());
                            return false;
                    }
                case Defines.RM_HEADER_MESSAGETYPE_NOTIFY:
                    //1. Find the corresponding inbound stream
                    possibleStreams = Application.StreamsManager.FindByProtocolIdByType(pFrom.Id,
                        StreamTypes.ST_IN_NET_RTMP, false);
                    pInNetRTMPStream = possibleStreams.Select(x => x.Value as InNetRTMPStream)
                        .SingleOrDefault(
                            x => x.RtmpStreamId == message.StreamId);
                    if (pInNetRTMPStream == null)
                    {
                        Logger.WARN("No stream found. Serached for {0}:{1}. Message was:{2}", pFrom.Id, message.StreamId, messageBody.ToString());
                        return true;
                    }
                    //2. Remove all string values starting with @
                    foreach (var item in messageBody[Defines.RM_NOTIFY, Defines.RM_NOTIFY_PARAMS].Children.Where(
                        x => x.Value == VariantType.String && ((string)x.Value).StartsWith("@")).Select(x => x.Key).ToArray())
                    {
                        messageBody[Defines.RM_NOTIFY, Defines.RM_NOTIFY_PARAMS].Children.Remove(item);
                    }
                    recycleBody = false;
                    //3. Brodcast the message on the inbound stream
                    pInNetRTMPStream.SendStreamMessage(new BufferWithOffset(((MemoryStream)channel.inputData.BaseStream).GetBuffer(), length: (int)message.MessageLength));
                    return true;
                case Defines.RM_HEADER_MESSAGETYPE_FLEXSTREAMSEND:
                    recycleBody = false;
                    //1. Find the corresponding inbound stream
                    possibleStreams = Application.StreamsManager.FindByProtocolIdByType(pFrom.Id, StreamTypes.ST_IN_NET_RTMP, false);
                    pInNetRTMPStream = possibleStreams.Select(x => x.Value as InNetRTMPStream)
                        .SingleOrDefault(x => x.RtmpStreamId == message.StreamId);
                    if (pInNetRTMPStream == null)
                    {
                        Logger.WARN("No stream found. Serached for {0}:{1}. Message was:{2}", pFrom.Id, message.StreamId, messageBody.ToString());
                        return true;
                    }
                    //2. Remove all string values starting with @
                    foreach (var item in messageBody[Defines.RM_FLEXSTREAMSEND, Defines.RM_FLEXSTREAMSEND_PARAMS].Children.Where(
                        x => x.Value == VariantType.String && ((string)x.Value).StartsWith("@")).Select(x => x.Key).ToArray())
                    {
                        messageBody[Defines.RM_FLEXSTREAMSEND, Defines.RM_FLEXSTREAMSEND_PARAMS].Children.Remove(item);
                    }
                    //写入文件
                    //pInNetRTMPStream.SendStreamMessage(channel);
                    //3. Brodcast the message on the inbound stream
                    pInNetRTMPStream.SendStreamMessage(new BufferWithOffset(((MemoryStream)channel.inputData.BaseStream).GetBuffer(),length: (int)message.MessageLength));
                    return true;
                case Defines.RM_HEADER_MESSAGETYPE_SHAREDOBJECT:
                case Defines.RM_HEADER_MESSAGETYPE_FLEXSHAREDOBJECT:
                    return Application.SOManager.Process(pFrom, messageBody);
                case Defines.RM_HEADER_MESSAGETYPE_INVOKE:
                case Defines.RM_HEADER_MESSAGETYPE_FLEX:
                    string functionName = messageBody[Defines.RM_INVOKE][Defines.RM_INVOKE_FUNCTION];
                    uint currentInvokeId = messageBody[Defines.RM_INVOKE, Defines.RM_INVOKE_ID];
                    if (currentInvokeId != 0 && _nextInvokeId[pFrom.Id] <= currentInvokeId)
                    {
                        _nextInvokeId[pFrom.Id] = currentInvokeId + 1;
                    }
                    string streamName;
                    BaseOutNetRTMPStream pBaseOutNetRTMPStream = null;
                    double timeOffset;
                    Variant metadata;
                    switch (functionName)
                    {
                        case Defines.RM_INVOKE_FUNCTION_CONNECT:
                            return ProcessInvokeConnect(pFrom,  message);
                            
                        case Defines.RM_INVOKE_FUNCTION_CREATESTREAM:
                            //1. Create the neutral stream
                            uint id = 0;
                            if (pFrom.CreateNeutralStream(ref id) == null)
                            {
                                Logger.FATAL("Unable to create stream");
                                return false;
                            }

                            //2. Send the response
                            return SendRTMPMessage(pFrom,  StreamMessageFactory.GetInvokeCreateStreamResult(message, id));
                        case Defines.RM_INVOKE_FUNCTION_PUBLISH:
                            //1. gather the required data from the request
                            var param1 = messageBody[Defines.RM_INVOKE, Defines.RM_INVOKE_PARAMS][1];
                            if (param1 != VariantType.String && param1 != VariantType.Boolean)
                            {
                                Logger.FATAL("Invalid request:{0}", messageBody.ToString());
                                return false;
                            }
                            if (param1 == VariantType.Boolean)
                            {
                                if (param1 != false)
                                {
                                    Logger.FATAL("Invalid request {0}", messageBody.ToString());
                                    return false;
                                }
                                this.Log().Info("Closing stream via publish(false)");
                                return pFrom.CloseStream(message.StreamId, true);
                            }
                            streamName = param1;
                            //2. Check to see if we are allowed to create inbound streams
                            if (!pFrom.CustomParameters["authState"]["canPublish"])
                            {
                                return
                                    pFrom.SendMessage(StreamMessageFactory.GetInvokeOnStatusStreamPublishBadName(
                                        message, streamName),true);
                            }
                            var recording = string.Equals(message.InvokeParam[2], Defines.RM_INVOKE_PARAMS_PUBLISH_TYPERECORD);
                            var appending = string.Equals(message.InvokeParam[2], Defines.RM_INVOKE_PARAMS_PUBLISH_TYPEAPPEND);
                            //3. Test to see if this stream name is already published somewhere
                            if (Application.AllowDuplicateInboundNetworkStreams)
                            {
                                var existingStreams =
                                    Application.StreamsManager.FindByTypeByName(StreamTypes.ST_IN_NET_RTMP, streamName,false, false);
                                        
                                if (existingStreams.Count > 0)
                                {
                                    if (pFrom.CustomParameters["authState"]["canOverrideStreamName"])
                                    {
                                        foreach (var existingStream in existingStreams.Values.OfType<InNetRTMPStream>().Where(existingStream => existingStream.Protocol != null))
                                        {
                                            Logger.WARN(
                                                "Overriding stream {0}:{1} with name {2} from conneciton {3}",
                                                existingStream.RtmpStreamId, existingStream.UniqueId,
                                                existingStream.Name, existingStream.Protocol.Id);
                                            (existingStream.Protocol as BaseRTMPProtocol).CloseStream(
                                                existingStream.RtmpStreamId, true);
                                        }
                                    }
                                    else
                                    {
                                        Logger.WARN(
                                            "Unable to override stream {0} because this connection dosen't have the right",
                                            streamName);
                                        return
                                            pFrom.SendMessage(
                                                StreamMessageFactory.GetInvokeOnStatusStreamPublishBadName(message,
                                                    streamName),true);
                                    }
                                }
                            }
                            else if (!Application.StreamNameAvailable(streamName, pFrom))
                            {
                                Logger.WARN("Stream name {0} already occupied and application doesn't allow duplicated inbound network streams", streamName);
                                return
                                    pFrom.SendMessage( StreamMessageFactory.GetInvokeOnStatusStreamPublishBadName(
                                        message, streamName), true);
                            }
                            //4. Create the network inbound stream
                            pInNetRTMPStream = pFrom.CreateINS(message.ChannelId, message.StreamId, streamName);
                            if (pInNetRTMPStream == null)
                            {
                                Logger.FATAL("Unable to create inbound stream");
                                return false;
                            }
                            //7. Bind the waiting subscribers
                            Application.OnPublish(pFrom, pInNetRTMPStream, message.InvokeParam[2]);
                            //8. Send the streamPublished status message
                            if (!pInNetRTMPStream.SendOnStatusStreamPublished())
                            {
                                Logger.FATAL("Unable to send OnStatusStreamPublished");
                                return false;
                            }
                            //9. Start recording if necessary
                            //if (recording || appending)
                            //{
                                
                            //    var meta = GetMetaData(streamName, false);
                            //    var pOutFileStream = CreateOutFileStream(pFrom, meta, appending);
                            //    if (pOutFileStream != null && pOutFileStream.Link(pInNetRTMPStream)) return true;
                            //    Logger.FATAL("Unable to bind the recording stream");
                            //    return false;
                            //}
                            return true;
                        case Defines.RM_INVOKE_FUNCTION_PLAY:
                            //1. Minimal validation
                            if (message.InvokeParam[1] != VariantType.String)
                            {
                                Logger.FATAL("Invalid request:{0}", message.Body.ToString());
                                return false;
                            }
                            //2. Close any streams left open
                            if (!pFrom.CloseStream(message.StreamId, true))
                            {
                                Logger.FATAL("Unable to close stream {0} {1}", pFrom.Id, message.StreamId);
                                return false;
                            }
                            //3. Gather required data from the request
                            streamName = message.InvokeParam[1];
                            double startTime = message.InvokeParam[2] == VariantType.Double ? (double)message.InvokeParam[2] : -2000;
                            double length = message.InvokeParam[3] == VariantType.Double ? (double)message.InvokeParam[3] : -1000;
                            if (startTime < 0 && startTime != -2000 && startTime != -1000) startTime = -2000;
                            if (length < 0 && length != -1) length = -1;
                            
                            Logger.INFO("Play request for stream name `{0}`. Start:{1}; length: {2}",    streamName, startTime, length);
                            //6. bind the network outbound stream to the inbound stream
                            //depending on the type of the outbound stream
                            switch ((int)startTime)
                            {
                                case -2000:
                                    bool linked;
                                    //7. try to link to live stream
                                    if (!TryLinkToLiveStream(pFrom, message.StreamId, streamName, out linked))
                                    {
                                        Logger.FATAL("Unable to link streams");
                                        return false;
                                    }
                                    if (linked) return true;

                                    metadata = GetMetaData(streamName, true);
                                    //8. try to link to file stream
                                    if (!TryLinkToFileStream(pFrom, message.StreamId, metadata, streamName, startTime, length, out linked))
                                    {
                                        Logger.FATAL("Unable to link streams");
                                        return false;
                                    }
                                    if (linked) return true;
                                    //9. Ok, no live/file stream. Just wait for the live stream now...
                                    Logger.WARN("We are going to wait for the live stream `{0}`", streamName);
                                    pBaseOutNetRTMPStream = pFrom.CreateONS(message.StreamId, streamName, StreamTypes.ST_IN_NET_RTMP);
                                    goto case -999;
                                case -1000://only live
                                    if (!TryLinkToLiveStream(pFrom, message.StreamId, streamName, out linked))
                                    {
                                        Logger.FATAL("Unable to link streams");
                                        return false;
                                    }
                                    if (linked) return true;
                                    Logger.WARN("We are going to wait for the live stream `%s`", streamName);
                                    pBaseOutNetRTMPStream = pFrom.CreateONS(
                                        message.StreamId, streamName, StreamTypes.ST_IN_NET_RTMP);
                                    goto case -999;
                                case -999:
                                    //Application.ClusterAppProtocolHandler.PlayStream(Application.InstanceName,streamName);
                                    if (ClientApplicationManager.ClusterApplication != null)
                                    {
                                        ClientApplicationManager.ClusterApplication.GetProtocolHandler<BaseClusterAppProtocolHandler>().PlayStream(Application.Id, streamName);
                                    }
                                    //request["waitForLiveStream"] = true;
                                    //request["streamName"] = streamName;
                                    //if (pFrom.CustomParameters["origin"] != null)
                                    //{
                                        //if (_externalStreamProtocol == null)
                                        //    Application.PullExternalStream(new Variant
                                        //    {
                                        //        {"uri", "rtmp://192.168.20.56/live"},
                                        //        {"localStreamName", streamName},
                                        //        {"emulateUserAgent", "MAC 10,1,82,76"},
                                        //        {"swfUrl", "my crtmpserver"},
                                        //        {"pageUrl", "linkage"}
                                        //    });
                                        //else
                                        //{
                                        //    PlayAnotherStream(_externalStreamProtocol, streamName);
                                        //}
                                    //}
                                    return pBaseOutNetRTMPStream != null;
                                default://only recorded
                                    metadata = GetMetaData(streamName, true);
                                    //12. Perform little adjustment on metadata
                                    if ((string)metadata[Defines.META_MEDIA_TYPE] == Defines.MEDIA_TYPE_LIVE_OR_FLV)
                                    {
                                        metadata[Defines.META_MEDIA_TYPE] = Defines.MEDIA_TYPE_FLV;
                                    }

                                    //13. try to link to file stream

                                    if (!TryLinkToFileStream(pFrom, message.StreamId, metadata, streamName,
                                            startTime, length, out linked))
                                    {
                                        Logger.FATAL("Unable to link streams");
                                        return false;
                                    }
                                    return linked;
                            }
                        case Defines.RM_INVOKE_FUNCTION_PAUSERAW:
                        case Defines.RM_INVOKE_FUNCTION_PAUSE:
                            pBaseOutNetRTMPStream = Application.StreamsManager.FindByProtocolIdByType(pFrom.Id,
                                StreamTypes.ST_OUT_NET_RTMP, true).Values.OfType<BaseOutNetRTMPStream>().SingleOrDefault(x => x.RTMPStreamId == message.StreamId);
                            if (pBaseOutNetRTMPStream == null)
                            {
                                Logger.FATAL("No out stream");
                                return false;
                            }
                            //3. get the operation
                            bool pause = message.InvokeParam[1];
                            if (pause)
                            {
                                //4. Pause it
                                return pBaseOutNetRTMPStream.Pause();
                            }
                            else
                            {
                                timeOffset = 0.0;
                                if (message.InvokeParam[2] == VariantType.Numberic)
                                    timeOffset = message.InvokeParam[2];

                                //8. Perform seek
                                if (!pBaseOutNetRTMPStream.Seek(timeOffset))
                                {
                                    Logger.FATAL("Unable to seek");
                                    return false;
                                }

                                //9. Resume
                                return pBaseOutNetRTMPStream.Resume();
                            }
                        case Defines.RM_INVOKE_FUNCTION_CLOSESTREAM:
                            return pFrom.CloseStream(message.StreamId, true);
                        case Defines.RM_INVOKE_FUNCTION_SEEK:
                            //1. Read stream index and offset in millisecond

                            timeOffset = 0.0;
                            if (message.InvokeParam[1] == VariantType.Numberic)
                                timeOffset = message.InvokeParam[1];

                            //2. Find the corresponding outbound stream
                            pBaseOutNetRTMPStream = Application.StreamsManager.FindByProtocolIdByType(pFrom.Id,
                                StreamTypes.ST_OUT_NET_RTMP, true).Values.OfType<BaseOutNetRTMPStream>().SingleOrDefault(x => x.RTMPStreamId == message.StreamId);
                            if (pBaseOutNetRTMPStream == null)
                            {
                                Logger.FATAL("No out stream");
                                return false;
                            }

                            return pBaseOutNetRTMPStream.Seek(timeOffset);
                        case Defines.RM_INVOKE_FUNCTION_RELEASESTREAM:
                            //1. Attempt to find the stream
                            var streams = Application.StreamsManager.FindByProtocolIdByName(pFrom.Id,
                                message.InvokeParam[1], false);
                            uint streamId = 0;
                            if (streams.Count > 0)
                            {
                                //2. Is this the correct kind?
                                if (streams.Values.First().Type.TagKindOf(StreamTypes.ST_IN_NET_RTMP))
                                {
                                    //3. get the rtmp stream id
                                    pInNetRTMPStream = (InNetRTMPStream)streams.Values.First();
                                    streamId = pInNetRTMPStream.RtmpStreamId;

                                    //4. close the stream
                                    if (!pFrom.CloseStream(streamId, true))
                                    {
                                        Logger.FATAL("Unable to close stream");
                                        return true;
                                    }
                                }
                            }
                            if (streamId > 0)
                            {
                                //5. Send the response
                                if (!pFrom.SendMessage( StreamMessageFactory.GetInvokeReleaseStreamResult(3,
                                        streamId, message.InvokeId, streamId), true))
                                {
                                    Logger.FATAL("Unable to send message to client");
                                    return false;
                                }
                            }
                            else
                            {
                                if (!pFrom.SendMessage( StreamMessageFactory.GetInvokeReleaseStreamErrorNotFound(message), true))
                                {
                                    Logger.FATAL("Unable to send message to client");
                                    return false;
                                }
                            }
                            //3. Done
                            return true;
                        case Defines.RM_INVOKE_FUNCTION_DELETESTREAM:
                            return pFrom.CloseStream(message.InvokeParam[1], false);
                        case Defines.RM_INVOKE_FUNCTION_RESULT:
                        case Defines.RM_INVOKE_FUNCTION_ERROR:
                            if (!_resultMessageTracking.ContainsKey(pFrom.Id) ||
                                !_resultMessageTracking[pFrom.Id].ContainsKey(message.InvokeId))
                                return true;
                            var request0 = _resultMessageTracking[pFrom.Id][message.InvokeId];
                            switch (request0.InvokeFunction)
                            {
                                case Defines.RM_INVOKE_FUNCTION_CONNECT:
                                    return ProcessInvokeConnectResult(pFrom, request0, message);
                                case Defines.RM_INVOKE_FUNCTION_CREATESTREAM:
                                    return ProcessInvokeCreateStreamResult(pFrom, request0, message);
                                case Defines.RM_INVOKE_FUNCTION_FCSUBSCRIBE:
                                    return true;
                                case Defines.RM_INVOKE_FUNCTION_ONBWCHECK:
                                    startTime = pFrom.CustomParameters["lastOnnBWCheckMessage"];
                                    double totalTime = (DateTime.Now.Ticks - startTime) / (double)1000000;
                                    var speed = (int)(ONBWCHECK_SIZE / totalTime / 1024.0 * 8.0);
                                    return SendRTMPMessage(pFrom, GenericMessageFactory.GetInvokeOnBWDone(speed));
                                default:
                                    Logger.WARN("Invoke result not yet implemented: Request:{0} Response:{1}", request0.ToString(), message.ToString());
                                    return true;
                            }
                        case Defines.RM_INVOKE_FUNCTION_ONSTATUS:
                            return ProcessInvokeOnStatus(pFrom, message);
                        case Defines.RM_INVOKE_FUNCTION_FCPUBLISH:
                            //1. Get the stream name
                            streamName = message.InvokeParam[1];

                            //2. Send the release stream response. Is identical to the one
                            //needed by this fucker
                            //TODO: this is a nasty hack
                            if (!pFrom.SendMessage( StreamMessageFactory.GetInvokeReleaseStreamResult(3, 0, message.InvokeId, 0), true))
                            {
                                Logger.FATAL("Unable to send message to client");
                                return false;
                            }

                            //3. send the onFCPublish message
                            if (!SendRTMPMessage(pFrom,  StreamMessageFactory.GetInvokeOnFCPublish(3, 0, 0, false, 0,
                                    Defines.RM_INVOKE_PARAMS_ONSTATUS_CODE_NETSTREAMPUBLISHSTART, streamName)))
                            {
                                Logger.FATAL("Unable to send message to client");
                                return false;
                            }

                            //4. Done
                            return true;
                        case Defines.RM_INVOKE_FUNCTION_GETSTREAMLENGTH:
                            metadata = GetMetaData(message.InvokeParam[1], true);
                            var _params = Variant.GetList(Variant.Get(),metadata == VariantType.Map
                                ?  metadata[Defines.META_FILE_DURATION]/1000.00 : 0.0);
                            if (!SendRTMPMessage(pFrom,  GenericMessageFactory.GetInvokeResult(message, _params)))
                            {
                                Logger.FATAL("Unable to send message to client");
                                return false;
                            }
                            return true;
                        case Defines.RM_INVOKE_FUNCTION_ONBWDONE:
                            return true;
                        case Defines.RM_INVOKE_FUNCTION_CHECKBANDWIDTH:
                        case "_checkbw":
                            if (!_enableCheckBandwidth)
                            {
                                Logger.WARN("checkBandwidth is disabled.");
                                return true;
                            }
                            if (!SendRTMPMessage(pFrom,  _onBWCheckMessage,true, false))
                            {
                                Logger.FATAL("Unable to send message to flash player");
                                return false;
                            }
                            pFrom.CustomParameters["lastOnnBWCheckMessage"] = DateTime.Now.Ticks;
                            return true;
                        case "receiveAudio":
                            pBaseOutNetRTMPStream = Application.StreamsManager.FindByProtocolIdByType(pFrom.Id,
                               StreamTypes.ST_OUT_NET_RTMP, true).Values.OfType<BaseOutNetRTMPStream>().SingleOrDefault(x => x.RTMPStreamId ==message.StreamId);
                            if (pBaseOutNetRTMPStream != null)
                                pBaseOutNetRTMPStream.ReceiveAudio = message.InvokeParam[1];
                            return true;
                        case "receiveVideo":
                             pBaseOutNetRTMPStream = Application.StreamsManager.FindByProtocolIdByType(pFrom.Id,
                               StreamTypes.ST_OUT_NET_RTMP, true).Values.OfType<BaseOutNetRTMPStream>().SingleOrDefault(x => x.RTMPStreamId == message.StreamId);
                            if (pBaseOutNetRTMPStream != null)
                                pBaseOutNetRTMPStream.ReceiveVideo = message.InvokeParam[1];
                            return true;
                        default:
                            return ProcessInvokeGeneric(pFrom,  message);
                            
                    }
                case Defines.RM_HEADER_MESSAGETYPE_ABORTMESSAGE:
                    if (messageBody[Defines.RM_ABORTMESSAGE] != VariantType.Numberic)
                    {
                        Logger.FATAL("Invalid message {0}", messageBody.ToString());
                        return false;
                    }
                    return pFrom.ResetChannel(messageBody[Defines.RM_ABORTMESSAGE]);
                default:
                    Logger.FATAL("Request type not yet implemented:{0}", messageBody.ToString());
                    return false;
            }
        }

        protected virtual bool ProcessInvokeConnect(BaseRTMPProtocol pFrom,AmfMessage message)
        {
            string appName = message.InvokeParam[0][Defines.RM_INVOKE_PARAMS_CONNECT_APP];
            //var parameters = pFrom.CustomParameters;
            //var instanceName = index == -1?"_default_": appName.Substring(index + 1);
            var oldApplication = pFrom.Application;
            var newApp = ClientApplicationManager.SwitchRoom(pFrom, appName, Configuration);

            if (newApp != null && newApp != oldApplication)
            {
                var handler = newApp.GetProtocolHandler<BaseRTMPAppProtocolHandler>(pFrom);
                return handler.ProcessInvokeConnect(pFrom, message);
            }

            if (newApp == null || (newApp == oldApplication && !Application.OnConnect(pFrom, message.InvokeParam)))
            {
                if (!pFrom.SendMessage(ConnectionMessageFactory.GetInvokeConnectError(message, "")))
                {
                    return false;
                }

                if (!pFrom.SendMessage(ConnectionMessageFactory.GetInvokeClose()))
                {
                    return false;
                }
                // pFrom.EnqueueForOutbound();
                return true;
            }
            //1. Send the channel specific messages
            if (!SendRTMPMessage(pFrom,  GenericMessageFactory.GetWinAckSize(2500000)))
            {
               
                return false;
            }
            if (!SendRTMPMessage(pFrom,  GenericMessageFactory.GetPeerBW(2500000, Defines.RM_PEERBW_TYPE_DYNAMIC)))
            {
               
                return false;
            }
            //2. Initialize stream 0
            if (!SendRTMPMessage(pFrom,  StreamMessageFactory.GetUserControlStreamBegin(0)))
            {
                
                return false;
            }
            //3. Send the connect result
            if (!SendRTMPMessage(pFrom,  ConnectionMessageFactory.GetInvokeConnectResult(message)))
            {
               
                return false;
            }
            //4. Send onBWDone
            if (SendRTMPMessage(pFrom,  GenericMessageFactory.GetInvokeOnBWDone(1024 * 8))) return true;
           
            return false;
        }

        virtual protected bool ProcessInvokeGeneric(BaseRTMPProtocol pFrom, AmfMessage request)
        {
            //Logger.WARN("Default implementation of ProcessInvokeGeneric: Request: {0}", request.InvokeFunction);
            try
            {
                var result = Application.CallCustomFunction(pFrom, request.InvokeFunction,request.InvokeParam);
                return SendRTMPMessage(pFrom,
                    GenericMessageFactory.GetInvokeResult(request.ChannelId, request.StreamId,
                        request.InvokeId, Variant.Get(), result));
            }
            catch (CallErrorException ex)
            {
                return SendRTMPMessage(pFrom, 
                    GenericMessageFactory.GetInvokeError(request.ChannelId, request.StreamId,
                        request.InvokeId, Variant.Get(), Variant.Get(ex.Message)));
            }
            catch (Exception ex)
            {
                return SendRTMPMessage(pFrom,  GenericMessageFactory.GetInvokeCallFailedError(request));
            }
        }

        
        
       
        //private bool PlayAnotherStream(BaseRTMPProtocol pFrom,string streamName)
        //{
        //    var FCSubscribeRequest = StreamMessageFactory.GetInvokeFCSubscribe(streamName);
        //    if (!SendRTMPMessage(pFrom, FCSubscribeRequest, true))
        //    {
        //        Logger.FATAL("Unable to send request:\n{0}", FCSubscribeRequest.ToString());
        //        return false;
        //    }
        //    var createStreamRequest = StreamMessageFactory.GetInvokeCreateStream();
        //    if (!SendRTMPMessage(pFrom, createStreamRequest, true))
        //    {
        //        Logger.FATAL("Unable to send request:\n{0}", createStreamRequest.ToString());
        //        return false;
        //    }
        //    return true;
        //}
        private bool AuthenticateInbound(BaseRTMPProtocol pFrom,  AmfMessage request, Variant authState)
        {
            if (_authMethod == Defines.CONF_APPLICATION_AUTH_TYPE_ADOBE)
                return AuthenticateInboundAdobe(pFrom, request, authState);
            Logger.FATAL("Auth scheme not supported: {0}", _authMethod);
            return false;
        }

        private bool AuthenticateInboundAdobe(BaseRTMPProtocol pFrom,  AmfMessage request, Variant authState)
        {
            if (authState["stage"] == null) authState["stage"] = "inProgress";
            else if (authState["stage"] == "authenticated") return true;
            if (authState["stage"] != "inProgress")
            {
                Logger.FATAL("This protocol in not in the authenticating mode");
                return false;
            }
            //1. Validate the type of request
            if (request.MessageType != Defines.RM_HEADER_MESSAGETYPE_INVOKE)
            {
                this.Log().Info("This is not an invoke. Wait for it...");
                return true;
            }

            //2. Validate the invoke function name
            if (request.InvokeFunction != Defines.RM_INVOKE_FUNCTION_CONNECT)
            {
                Logger.FATAL("This is not a connect invoke");
                return false;
            }

            //3. Pick up the first param in the invoke
            Variant connectParams = request.InvokeParam[0];
            if (connectParams != VariantType.Map)
            {
                Logger.FATAL("first invoke param must be a map");
                return false;
            }
            //4. pick up the agent name
            if ((connectParams[Defines.RM_INVOKE_PARAMS_CONNECT_FLASHVER] == null)
                    || (connectParams[Defines.RM_INVOKE_PARAMS_CONNECT_FLASHVER] != VariantType.String))
            {
                Logger.WARN("Incorrect user agent");
                authState["stage"] = "authenticated";
                authState["canPublish"] = false;
                authState["canOverrideStreamName"] = false;
                return true;
            }
            string flashVer = connectParams[Defines.RM_INVOKE_PARAMS_CONNECT_FLASHVER];

            //6. test the flash ver against the allowed encoder agents
            if (_adobeAuthSettings[Defines.CONF_APPLICATION_AUTH_ENCODER_AGENTS, flashVer] == null)
            {
                Logger.WARN("This agent is not on the list of allowed encoders: `{0}`", flashVer);
                authState["stage"] = "authenticated";
                authState["canPublish"] = false;
                authState["canOverrideStreamName"] = false;
                return true;
            }

            //7. pick up the tcUrl from the first param
            if ((connectParams[Defines.RM_INVOKE_PARAMS_CONNECT_APP] == null)
                    || (connectParams[Defines.RM_INVOKE_PARAMS_CONNECT_APP] != VariantType.String))
            {
                Logger.WARN("Incorrect app url");
                authState["stage"] = "authenticated";
                authState["canPublish"] = (bool)false;
                authState["canOverrideStreamName"] = (bool)false;
                return true;
            }
            string appUrl = connectParams[Defines.RM_INVOKE_PARAMS_CONNECT_APP];

            //8. Split the URI into parts
            var appUrlParts = appUrl.Split('?');
            if (appUrlParts.Length == 1)
            {
                //bare request. We need to tell him that he needs auth
                if (!pFrom.SendMessage( ConnectionMessageFactory.GetInvokeConnectError(request,
                        "[ AccessManager.Reject ] : [ code=403 need auth; authmod=adobe ] : ")))
                {
                    Logger.FATAL("Unable to send message");
                    return false;
                }

                if (!pFrom.SendMessage( ConnectionMessageFactory.GetInvokeClose()))
                {
                    Logger.FATAL("Unable to send message");
                    return false;
                }
                //pFrom.SendMessagesBlock.TriggerBatch();
                pFrom.EnqueueForOutbound(pFrom.OutputBuffer);
                pFrom.GracefullyEnqueueForDelete();
                return true;
            }
            else if (appUrlParts.Length == 2)
            {
                var _params = appUrlParts[1].GetURLParam();
                if ((!_params.ContainsKey("authmod")) || (!_params.ContainsKey("user")))
                {
                    Logger.WARN("Invalid appUrl: {0}", appUrl);
                    authState["stage"] = "authenticated";
                    authState["canPublish"] = false;
                    authState["canOverrideStreamName"] = false;
                    return true;
                }

                string user = _params["user"];

                if (_params.ContainsKey("challenge")
                    && _params.ContainsKey("response")
                    && _params.ContainsKey("opaque"))
                {
                    string challenge = _params["challenge"];
                    string response = _params["response"];
                    string opaque = _params["opaque"];
                    string password = GetAuthPassword(user);
                    if (password == "")
                    {
                        Logger.WARN("No such user: `{0}`", user);

                        if (!pFrom.SendMessage( ConnectionMessageFactory.GetInvokeConnectError(request,
                            "[ AccessManager.Reject ] : [ authmod=adobe ] : ?reason=authfailed&opaque=vgoAAA==")))
                        {
                            Logger.FATAL("Unable to send message");
                            return false;
                        }

                        if (!pFrom.SendMessage( ConnectionMessageFactory.GetInvokeClose()))
                        {
                            Logger.FATAL("Unable to send message");
                            return false;
                        }
                        //pFrom.SendMessagesBlock.TriggerBatch();
                        pFrom.EnqueueForOutbound(pFrom.OutputBuffer);
                        pFrom.GracefullyEnqueueForDelete();
                        return true;
                    }
                    var md5 = MD5.Create();
                    string str1 = user + _adobeAuthSalt + password;
                    string hash1 = Convert.ToBase64String(md5.ComputeHash(Encoding.ASCII.GetBytes(str1)));
                    string str2 = hash1 + opaque + challenge;
                    string wanted = Convert.ToBase64String(md5.ComputeHash(Encoding.ASCII.GetBytes(str2)));

                    if (response == wanted)
                    {
                        authState["stage"] = "authenticated";
                        authState["canPublish"] = true;
                        authState["canOverrideStreamName"] = true;
                        Logger.WARN("User `{0}` authenticated", user);
                        return true;
                    }
                    else
                    {
                        Logger.WARN("Invalid password for user `{0}`", user);
                        if (!pFrom.SendMessage( ConnectionMessageFactory.GetInvokeConnectError(request,
                            "[ AccessManager.Reject ] : [ authmod=adobe ] : ?reason=authfailed&opaque=vgoAAA==")))
                        {
                            Logger.FATAL("Unable to send message");
                            return false;
                        }
                        if (!pFrom.SendMessage( ConnectionMessageFactory.GetInvokeClose()))
                        {
                            Logger.FATAL("Unable to send message");
                            return false;
                        }
                        //pFrom.SendMessagesBlock.TriggerBatch();
                        pFrom.EnqueueForOutbound(pFrom.OutputBuffer);
                        pFrom.GracefullyEnqueueForDelete();
                        return true;
                    }
                }
                else
                {
                    string challenge = Utils.GenerateRandomString(6) + "==";
                    string opaque = challenge;
                    string description =
                        "[ AccessManager.Reject ] : [ authmod=adobe ] : ?reason=needauth&user={0}&salt={1}&challenge={2}&opaque={3}";

                    description = string.Format(description, user, _adobeAuthSalt, challenge, opaque);

                    if (!pFrom.SendMessage(ConnectionMessageFactory.GetInvokeConnectError(request, description)))
                    {
                        Logger.FATAL("Unable to send message");
                        return false;
                    }


                    if (!pFrom.SendMessage(ConnectionMessageFactory.GetInvokeClose()))
                    {
                        Logger.FATAL("Unable to send message");
                        return false;
                    }
                    //pFrom.SendMessagesBlock.TriggerBatch();
                    pFrom.EnqueueForOutbound(pFrom.OutputBuffer);
                    pFrom.GracefullyEnqueueForDelete();
                    return true;
                }
            }
            else
            {
                Logger.FATAL("Invalid appUrl: {0}", appUrl);
                return false;
            }
        }

        private string GetAuthPassword(string user)
        {
            string usersFile = _adobeAuthSettings[Defines.CONF_APPLICATION_AUTH_USERS_FILE];
            string fileName = Path.GetFileName(usersFile);
            string extension = Path.GetExtension(usersFile);

            var modificationDate = new FileInfo(usersFile).LastWriteTime;

            if (modificationDate != _lastUsersFileUpdate)
            {
                try
                {
                    _users = Variant.DeserializeFromJsonFile(usersFile)["users"];
                }
                catch (Exception ex)
                {
                    Logger.FATAL("Unable to read users file: `{0}`", usersFile);
                    return "";
                }

                _lastUsersFileUpdate = modificationDate;
            }

            if (_users != VariantType.Map)
            {
                Logger.FATAL("Invalid users file: `{0}`", usersFile);
                return "";
            }

            if (_users[user] != null)
            {
                if (_users[user] == VariantType.String)
                {
                    return _users[user];
                }
                else
                {
                    Logger.FATAL("Invalid users file: `{0}`", usersFile);
                    return "";
                }
            }
            else
            {
                Logger.FATAL("User `{0}` not present in users file: `{1}`",
                        user,
                        usersFile);
                return "";
            }
        }

        protected bool SendRTMPMessage(BaseRTMPProtocol pTo,  AmfMessage message, bool trackResponse = false, bool recycleMessageBody = true)
        {
            switch (message.MessageType)
            {
                case Defines.RM_HEADER_MESSAGETYPE_INVOKE:
                    if (message.InvokeFunction != Defines.RM_INVOKE_FUNCTION_RESULT)
                    {
                        if (!_nextInvokeId.ContainsKey(pTo.Id))
                        {
                            Logger.FATAL("Unable to get next invoke ID");
                            return false;
                        }
                        if (trackResponse)
                        {
                            uint invokeId = _nextInvokeId[pTo.Id];
                            _nextInvokeId[pTo.Id] = invokeId + 1;
                            message.InvokeId = invokeId;
                            if (!_resultMessageTracking.ContainsKey(pTo.Id))
                                _resultMessageTracking[pTo.Id] = new Dictionary<uint, AmfMessage>();
                            //do not store stupid useless amount of data needed by onbwcheck
                            if (message.InvokeFunction == Defines.RM_INVOKE_FUNCTION_ONBWCHECK)
                                _resultMessageTracking[pTo.Id][invokeId] = _onBWCheckStrippedMessage;
                            else
                                _resultMessageTracking[pTo.Id][invokeId] = message;
                            recycleMessageBody = false;
                        }
                        else
                        {
                             message.InvokeId = 0;
                        }
                        //return pTo.SendMessage(message,true);
                    }
                    return pTo.SendMessage( message, true, recycleMessageBody);
                case Defines.RM_HEADER_MESSAGETYPE_FLEXSTREAMSEND:
                case Defines.RM_HEADER_MESSAGETYPE_WINACKSIZE:
                case Defines.RM_HEADER_MESSAGETYPE_PEERBW:
                case Defines.RM_HEADER_MESSAGETYPE_USRCTRL:
                case Defines.RM_HEADER_MESSAGETYPE_ABORTMESSAGE:
                    return pTo.SendMessage( message, true, recycleMessageBody);
                default:
                    Logger.FATAL("Unable to send message:\n{0}", message.ToString());
                    return false;
            }
        }

        private bool TryLinkToLiveStream(BaseRTMPProtocol pFrom, uint streamId, string streamName, out bool linked)
        {
            linked = false;
            //1. Get get the short version of the stream name
            var parts = streamName.Split('?');
            var shortName = parts[0];
            //2. Search for the long version first
            var inboundStreams = Application.StreamsManager.FindByTypeByName(StreamTypes.ST_IN_NET, streamName, true,
                false);
            //3. Search for the short version if necessary
            if (inboundStreams.Count == 0)
                inboundStreams = Application.StreamsManager.FindByTypeByName(StreamTypes.ST_IN_NET, shortName + "?",
                    true, true);
            //4. Do we have some streams?
            if (inboundStreams.Count == 0)
            {
                Logger.WARN("No live streams found: `{0}` or `{1}`", streamName, shortName);
                return true;
            }
            //5. Get the first stream in the inboundStreams
            var pBaseInNetStream = inboundStreams.Values.OfType<IInStream>()
                 .FirstOrDefault(
                     x =>
                         x.IsCompatibleWithType(StreamTypes.ST_OUT_NET_RTMP_4_TS) ||
                         x.IsCompatibleWithType(StreamTypes.ST_OUT_NET_RTMP_4_RTMP));
            if (pBaseInNetStream == null)
            {
                Logger.WARN("No live streams found: `{0}` or `{1}`", streamName, shortName);
                return true;
            }
            //6. Create the outbound stream
            var pBaseOutNetRTMPStream = pFrom.CreateONS(streamId, streamName, (pBaseInNetStream as InClusterStream)?.ContentStreamType ?? pBaseInNetStream.Type);

            if (pBaseOutNetRTMPStream == null)
            {
                Logger.FATAL("Unable to create network outbound stream");
                return false;
            } 
          
            //7. Link them
            if (!pBaseInNetStream.Link(pBaseOutNetRTMPStream))
            {
                Logger.FATAL("Link failed");
                return false;
            }

            //8. Done
            linked = true;
            return true;
        }

        private bool TryLinkToFileStream(BaseRTMPProtocol pFrom, uint streamId, Variant metadata, string streamName, double startTime, double length, out bool linked)
        {
            linked = false;
            //1. Try to create the in file streams
            InFileRTMPStream pRTMPInFileStream = pFrom.CreateIFS(metadata);
            if (pRTMPInFileStream == null)
            {
                Logger.WARN("No file streams found: {0}", streamName);
                return true;
            }
            //2. Try to create the out net stream
            BaseOutNetRTMPStream pBaseOutNetRTMPStream = pFrom.CreateONS(
                    streamId, streamName, pRTMPInFileStream.Type);
            if (pBaseOutNetRTMPStream == null)
            {
                Logger.FATAL("Unable to create network outbound stream");
                return false;
            }
            //3. Link them
            if (!pRTMPInFileStream.Link(pBaseOutNetRTMPStream))
            {
                Logger.FATAL("Link failed");
                return false;
            }
            //4. Register it to the signaled streams
   
            //pFrom.SignalONS(pBaseOutNetRTMPStream);
            //5. Fire up the play routine
            if (!pRTMPInFileStream.Play(startTime, length))
            {
                Logger.FATAL("Unable to start the playback");
                return false;
            }

            //6. Done
            linked = true;
            return true;
        }
        
        #region 连接外部server

        private bool ProcessInvokeOnStatus(BaseRTMPProtocol pFrom, AmfMessage request)
        {
            if ((!NeedsToPullExternalStream(pFrom))
            && (!NeedsToPushLocalStream(pFrom)))
            {
                Logger.WARN("Default implementation of ProcessInvokeOnStatus in application {0}: Request:\n{1}",
                        Application.Name,
                        request.ToString());
                return true;
            }

            //1. Test and see if this connection is an outbound RTMP connection
            //and get a pointer to it
            if (pFrom.Type != ProtocolTypes.PT_OUTBOUND_RTMP)
            {
                Logger.FATAL("This is not an outbound connection");
                return false;
            }
            var pProtocol = (OutboundRTMPProtocol)pFrom;

            //2. Validate the request
            if (request.InvokeParam[1]["code"] != VariantType.String)
            {
                Logger.FATAL("invalid onStatus:\n{0}", request.Body.ToString());
                return false;
            }


            //6. Get our hands on streaming parameters

            var path = NeedsToPullExternalStream(pFrom) ? "externalStreamConfig" : "localStreamConfig";
            Variant parameters = pFrom.CustomParameters["customParameters"][path];

            if (NeedsToPullExternalStream(pFrom))
            {
                if (request.InvokeParam[1]["code"] != "NetStream.Play.Start")
                {
                    Logger.WARN("onStatus message ignored:\n{0}", request.Body.ToString());
                    return true;
                }
                if (!Application.StreamNameAvailable(parameters["localStreamName"],pProtocol))    
                {
                    Logger.WARN("Stream name {0} already occupied and application doesn't allow duplicated inbound network streams",
                            parameters["localStreamName"]);
                    return false;
                }
                var pStream = pProtocol.CreateINS(request.ChannelId,request.StreamId, parameters["localStreamName"]);
                if (pStream == null)
                {
                    Logger.FATAL("Unable to create stream");
                    return false;
                }

                var waitingSubscribers =
                        Application.StreamsManager.GetWaitingSubscribers(pStream.Name, pStream.Type);
                foreach (var waitingSubscriber in waitingSubscribers)
                {
                    pStream.Link(waitingSubscriber);
                }

            }
            else
            {
                if (request.InvokeParam[1]["code"] != "NetStream.Publish.Start")
                {
                    Logger.WARN("onStatus message ignored:\n{0}", request.ToString());
                    return true;
                }

                var pBaseInStream =
                        (IInStream)Application.StreamsManager.FindByUniqueId(parameters["localUniqueStreamId"]);

                if (pBaseInStream == null)
                {
                    Logger.FATAL("Unable to find the inbound stream with id {0}",
                             parameters["localUniqueStreamId"]);
                    return false;
                }

                //5. Create the network outbound stream
                var pBaseOutNetRTMPStream = pProtocol.CreateONS(
                    request.StreamId,
                    pBaseInStream.Name,
                    pBaseInStream.Type);
                if (pBaseOutNetRTMPStream == null)
                {
                    Logger.FATAL("Unable to create outbound stream");
                    return false;
                }
                pBaseOutNetRTMPStream.SendOnStatusPlayMessages = false;

                //6. Link and return
                if (!pBaseInStream.Link(pBaseOutNetRTMPStream))
                {
                    Logger.FATAL("Unable to link streams");
                    return false;
                }
            }

            return true;
        }

        private bool ProcessInvokeCreateStreamResult(BaseRTMPProtocol pFrom,AmfMessage request, AmfMessage response)
        {
            //1. Do we need to push/pull a stream?
            if ((!NeedsToPullExternalStream(pFrom))
                    && (!NeedsToPushLocalStream(pFrom)))
            {
                Logger.WARN("Default implementation of ProcessInvokeCreateStreamResult: Request:\n{0}\nResponse:\n{1}",
                        request.ToString(),
                        response.ToString());

                return true;
            }

            //2. Test and see if this connection is an outbound RTMP connection
            //and get a pointer to it
            if (pFrom.Type != ProtocolTypes.PT_OUTBOUND_RTMP)
            {
                Logger.FATAL("This is not an outbound connection");
                return false;
            }
            var pProtocol = (OutboundRTMPProtocol)pFrom;

            //3. Test the response
            if (response.InvokeFunction != Defines.RM_INVOKE_FUNCTION_RESULT || response.InvokeParam[1] != VariantType.Numberic)
            {
                Logger.FATAL("createStream failed:\n{0}", response.ToString());
                return false;
            }

            //4. Get the assigned stream ID
            uint rtmpStreamId = response.InvokeParam[1];

            //5. Create the neutral stream
            var pStream = pProtocol.CreateNeutralStream(ref rtmpStreamId);
            if (pStream == null)
            {
                Logger.FATAL("Unable to create neutral stream");
                return false;
            }


            //6. Get our hands on streaming parameters
            var path = NeedsToPullExternalStream(pFrom) ? "externalStreamConfig" : "localStreamConfig";
            var parameters = pFrom.CustomParameters["customParameters"][path];

            //7. Create publish/play request
            AmfMessage publishPlayRequest;
            if (NeedsToPullExternalStream(pFrom))
            {
                publishPlayRequest = StreamMessageFactory.GetInvokePlay(3, rtmpStreamId,
                        parameters["localStreamName"], -2, -1);
            }
            else
            {
                string targetStreamType = parameters["targetStreamType"];

                if ((targetStreamType != "live")
                        && (targetStreamType != "record")
                        && (targetStreamType != "append"))
                {
                    targetStreamType = "live";
                }
                publishPlayRequest = StreamMessageFactory.GetInvokePublish(3, rtmpStreamId,
                        parameters["targetStreamName"], targetStreamType);
            }

            //8. Send it
            if (!SendRTMPMessage(pFrom, publishPlayRequest, true))
            {
                Logger.FATAL("Unable to send request:\n{0}", publishPlayRequest.ToString());
                return false;
            }

            //9. Done
            return true;
        }

        private bool ProcessInvokeConnectResult(BaseRTMPProtocol pFrom, AmfMessage request, AmfMessage response)
        {
            //1. Do we need to push/pull a stream?
            if ((!NeedsToPullExternalStream(pFrom))
                    && (!NeedsToPushLocalStream(pFrom)))
            {
                Logger.WARN("Default implementation of ProcessInvokeConnectResult: Request:\n{0}\nResponse:\n{1}",
                        request.ToString(),
                        response.ToString());
                return true;
            }
            //2. See if the result is OK or not
            if (response.InvokeFunction != Defines.RM_INVOKE_FUNCTION_RESULT)
            {
                if (response.InvokeFunction != Defines.RM_INVOKE_FUNCTION_ERROR
                    || response.InvokeParam != VariantType.Map
                    || response.InvokeParam.ArrayLength < 2
                    || response.InvokeParam[1] != VariantType.Map
                    || response.InvokeParam[1]["level"] != "error"
                    || response.InvokeParam[1]["code"] != "NetConnection.Connect.Rejected"
                    || response.InvokeParam[1]["description"] == ""
                )
                {
                    Logger.FATAL("Connect failed:\n{0}", response.ToString());
                    return false;
                }
                string description = response.InvokeParam[1]["description"];
                var parts = description.Split('?');
                if (parts.Length != 2)
                {
                    Logger.FATAL("Connect failed:\n{0}", response.ToString());
                    return false;
                }
                description = parts[1];
                var _params = description.GetURLParam();
                if (!_params.ContainsKey("reason")
                    || !_params.ContainsKey("user")
                    || !_params.ContainsKey("salt")
                    || !_params.ContainsKey("challenge")
                    || !_params.ContainsKey("opaque")
                    || _params["reason"] != "needauth")
                {
                    Logger.FATAL("Connect failed:\n{0}", response.ToString());
                    return false;
                }
                var customParameters = pFrom.CustomParameters;
                var streamConfig = NeedsToPullExternalStream(pFrom) ? customParameters["customParameters", "externalStreamConfig"] : customParameters["customParameters", "localStreamConfig"];
                foreach (var param in _params)
                {
                    streamConfig["auth"].Add(param.Key, param.Value);
                }
                return false;
            }
            if (response.InvokeParam[1] != VariantType.Map
                || response.InvokeParam[1]["code"] != "NetConnection.Connect.Success")
            {
                Logger.FATAL("Connect failed:\n{0}", response.ToString());
                return false;
            }
            if (NeedsToPullExternalStream(pFrom))
            {
                var parameters = pFrom.CustomParameters["customParameters","externalStreamConfig"];

                if (!SendRTMPMessage(pFrom, StreamMessageFactory.GetInvokeFCSubscribe(parameters["localStreamName"]), true))
                {
                    return false;
                }
            }
            //3. Create the createStream request
            //4. Send it
            if (!SendRTMPMessage(pFrom, StreamMessageFactory.GetInvokeCreateStream(), true))
            {
                return false;
            }

            //5. Done
            return true;
        }

        public bool OutboundConnectionEstablished(OutboundRTMPProtocol pFrom)
        {
            if (NeedsToPullExternalStream(pFrom))
            {
                return PullExternalStream(pFrom);
            }

            if (NeedsToPushLocalStream(pFrom))
            {
                return PushLocalStream(pFrom);
            }

            Logger.WARN("You should override BaseRTMPAppProtocolHandler.OutboundConnectionEstablished");
            return false;
        }

        public override bool PullExternalStream(Uri uri, Variant streamConfig)
        {
            string localStreamName = streamConfig["localStreamName"] == null ? "stream_"+Utils.GenerateRandomString(8) : (string) streamConfig["localStreamName"];
            streamConfig["localStreamName"] = localStreamName;
            var parameters = Variant.Get();
            parameters["customParameters","externalStreamConfig"] = streamConfig;
            parameters[Defines.CONF_APPLICATION_NAME] = Application.Name;
            var scheme = uri.Scheme;
            switch (scheme)
            {
                case "rtmp":
                    parameters[Defines.CONF_PROTOCOL] = Defines.CONF_PROTOCOL_OUTBOUND_RTMP;
                    break;
                case "rtmpt":
                    parameters[Defines.CONF_PROTOCOL] = Defines.CONF_PROTOCOL_OUTBOUND_RTMPT;
                    break;
                case "rtmpe":
                    parameters[Defines.CONF_PROTOCOL] = Defines.CONF_PROTOCOL_OUTBOUND_RTMPE;
                    break;
                default:
                    Logger.FATAL("scheme {0} not supported by RTMP handler", (scheme));
                    return false;
            }

            var endpoint = new IPEndPoint(Dns.GetHostAddresses(uri.Host).First(x=>x.AddressFamily==AddressFamily.InterNetwork), uri.Port > 0 ? uri.Port : 1935);
            
            return OutboundRTMPProtocol.Connect(endpoint, parameters);
        }

        public bool PullExternalStream(BaseRTMPProtocol pFrom)
        {
            //1. Get the stream configuration and the URI from it
            var streamConfig = pFrom.CustomParameters["customParameters"]["externalStreamConfig"];
            _externalStreamProtocol = pFrom;
            //2. Issue the connect invoke
            return ConnectForPullPush(pFrom, "uri", streamConfig, true);
        }
        public bool PushLocalStream(BaseRTMPProtocol pFrom)
        {
            //1. Get the stream configuration and the URI from it
            var streamConfig = pFrom.CustomParameters["customParameters"]["localStreamConfig"];

            //2. Issue the connect invoke
            return ConnectForPullPush(pFrom, "targetUri", streamConfig, false);
        }

        private bool ConnectForPullPush(BaseRTMPProtocol pFrom, string uriPath, Variant streamConfig, bool isPull)
        {
            var uri = new Uri(streamConfig[uriPath]);
            var appName = isPull ? uri.Segments[1] : uri.PathAndQuery;
            if (!string.IsNullOrEmpty(appName))
            {
                appName = appName.Trim('/');
            }
            if (string.IsNullOrEmpty(appName))
            {
                Logger.FATAL("invalid uri:{0}", uri.OriginalString);
                return false;
            }
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var userInfo = uri.UserInfo.Split(':');
                if (streamConfig["auth"] != null)
                {
                    var user = userInfo[0];
                    var password = userInfo[1];
                    var salt = (string)streamConfig["auth"]["salt"];
                    var opaque = (string)streamConfig["auth"]["opaque"];
                    var challenge = (string)streamConfig["auth"]["challenge"];
                    var md5 = MD5.Create();
                    var response =
                        Convert.ToBase64String(
                            md5.ComputeHash(
                                Encoding.ASCII.GetBytes(
                                    Convert.ToBase64String(
                                        md5.ComputeHash(Encoding.ASCII.GetBytes(user + salt + password))) + opaque +
                                    challenge)));
                    appName += "?authmod=abobe"
                               + "&user=" + user
                               + "&challenge=" + challenge
                               + "&opaque=" + opaque
                               + "&salt=" + salt
                               + "&response=" + response;
                    streamConfig["emulateUserAgent"] = "FMLE/3.0 (compatible; FMSc/1.0)";
                }
                else
                {
                    appName = appName + "?authmod=adobe&user=" + userInfo[0];
                    streamConfig["emulateUserAgent"] = "FMLE/3.0 (compatible; FMSc/1.0)";
                }
            }
            //	//3. Compute tcUrl: rtmp://host/appName
            //	string tcUrl = format("%s://%s%s/%s",
            //			STR(uri.scheme()),
            //			STR(uri.host()),
            //			STR(uri.portSpecified() ? format(":%"PRIu32) : ""),
            //			STR(appName));

            //4. Get the user agent
            var userAgent = (string)streamConfig["emulateUserAgent"];

            if (string.IsNullOrEmpty(userAgent))
            {
                userAgent = Defines.HTTP_HEADERS_SERVER_US;
            }
            var tcUrl = (string)streamConfig["tcUrl"];

            var swfUrl = (string)streamConfig["swfUrl"];

            var pageUrl = (string)streamConfig["pageUrl"];
            //6. Prepare the connect request
            var connectRequest = ConnectionMessageFactory.GetInvokeConnect(
                    appName, //string appName
                    tcUrl, //string tcUrl
                    3191, //double audioCodecs
                    239, //double capabilities
                    userAgent, //string flashVer
                    false, //bool fPad
                    pageUrl, //string pageUrl
                    swfUrl, //string swfUrl
                    252, //double videoCodecs
                    1, //double videoFunction
                    0 //double objectEncoding
                    );
            //7. Send it
            if (!SendRTMPMessage(pFrom, connectRequest, true))
            {
                Logger.FATAL("Unable to send request:\n{0}", connectRequest.ToString());
                return false;
            }

            return true;
        }

        private bool NeedsToPushLocalStream(BaseRTMPProtocol pFrom)
        {
            var parameters = pFrom.CustomParameters;
            if (parameters["customParameters","localStreamConfig","targetUri"] == null) return false;
            return true;
        }

        private bool NeedsToPullExternalStream(BaseRTMPProtocol pFrom)
        {
            var parameters = pFrom.CustomParameters;
            if (parameters["customParameters","externalStreamConfig","uri"] == null) return false;
            return true;
        }
        #endregion
      
        public override void CallClient(BaseProtocol pTo, string functionName, Variant param)
        {
            var message = GenericMessageFactory.GetInvoke(3, 0, 0, false, 0, functionName, param.Clone());
            SendRTMPMessage(pTo as BaseRTMPProtocol,  message);
        }
        public override void Broadcast(BaseProtocol from,Variant invokeInfo)
        {
            var param = invokeInfo[Defines.RM_INVOKE_PARAMS].Clone();
            param.Insert(0, Variant.Get());
            var message = GenericMessageFactory.GetInvoke(3, 0, 0, false, 0, invokeInfo[Defines.RM_INVOKE_FUNCTION], Variant.Get(param));
            Logger.INFO("send to {0} clients:{1}", _connections.Count,message.Body.ToString());
            foreach (var baseRtmpProtocol in _connections.Values)
            {
                SendRTMPMessage(baseRtmpProtocol, message, false, false);
            }
            //message.Body.Recycle();
        }

        public override void SharedObjectTrack(BaseProtocol to, string name, uint version, bool isPersistent, Variant primitives)
        {
            var message = SOMessageFactory.GetSharedObject(3, 0, 0, false, name, version, isPersistent);
            message.Body[Defines.RM_SHAREDOBJECT, Defines.RM_SHAREDOBJECT_PRIMITIVES] = primitives;
            if (!(to as BaseRTMPProtocol).SendMessage(message, true))
            {
                to.EnqueueForDelete();
            }
        }
    }
}
