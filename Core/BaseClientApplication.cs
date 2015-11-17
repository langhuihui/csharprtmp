using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security;
using System.Security.Policy;
using Core.Protocols.Rtmp;
using CSharpRTMP.Common;
using System.Linq;
using CSharpRTMP.Core.NetIO;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Cluster;
using CSharpRTMP.Core.Protocols.Rtmp;
using CSharpRTMP.Core.Streaming;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Core
{
	public class BaseClientApplication:IDisposable
    {
        public delegate Variant CustomFunction(BaseProtocol protocol, Variant invokeInfo);
        protected readonly Dictionary<string, CustomFunction> CustomFunctions;
		public static uint _idGenerator;
        protected Variant _configuration;
	    public uint Id;
		public string Name {
			get;
            protected set;
		}
		public string[] Aliases {
			get;
            protected set;
		}
		public bool IsDefault {
			get;
            protected set;
		}

	    public readonly StreamsManager StreamsManager;
        public readonly SOManager SOManager;
		private readonly Dictionary<ulong,BaseAppProtocolHandler> _protocolsHandlers = new Dictionary<ulong, BaseAppProtocolHandler>();
	    public readonly HashSet<BaseAppProtocolHandler> AppProtocolHandlers = new HashSet<BaseAppProtocolHandler>(); 
		public bool AllowDuplicateInboundNetworkStreams{ get; set; }
	    public readonly string InstanceName;
	    
	    protected Variant _authSettings = Variant.Get();
        
	    public string DirPath;
        public BaseClientApplication(Variant configuration)
		{
			_configuration = configuration;
			
            Name = _configuration[Defines.CONF_APPLICATION_NAME];
            var index = Name.IndexOf('/');
            InstanceName = index == -1 ? "_default_" : Name.Substring(index+1);
            DirPath = _configuration[Defines.CONF_APPLICATION_DIRECTORY] + InstanceName + "/";
            Aliases = _configuration[Defines.CONF_APPLICATION_ALIASES] != null ? (_configuration[Defines.CONF_APPLICATION_ALIASES].Children).Select(x => (string)x.Value).ToArray() : new string[0];
            IsDefault = _configuration[Defines.CONF_APPLICATION_DEFAULT];
            AllowDuplicateInboundNetworkStreams = _configuration[Defines.CONF_APPLICATION_ALLOW_DUPLICATE_INBOUND_NETWORK_STREAMS];
           
            StreamsManager = new StreamsManager(this);
            SOManager = new SOManager(this);

            CustomFunctions = GetType()
               .GetMethods().Where(x => Attribute.IsDefined(x, typeof(CustomFunctionAttribute)))
               .ToDictionary(
                   x =>
                       x.GetCustomAttributes(typeof(CustomFunctionAttribute), true)
                           .OfType<CustomFunctionAttribute>()
                           .Single()
                          .Name, y => Delegate.CreateDelegate(typeof(CustomFunction), this,y) as CustomFunction);
        }

	    public string MediaPath
	    {
	        get
	        {
                var directory = Path.Combine(DirPath, "media") + Path.DirectorySeparatorChar;
	            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
	            return directory;
	        }
	    }

	    public string SOPath => Path.Combine(DirPath, "sharedobject") + Path.DirectorySeparatorChar;
	    public Variant Configuration => _configuration;

	    public virtual Variant CallCustomFunction(BaseProtocol pFrom, string functionName,Variant param)
        {
            this.Log().Info("call：{0}", functionName);
            if (!CustomFunctions.ContainsKey(functionName))
            {
                //throw new Exception("no such function");
                Logger.WARN("no such function");
                return null;
            }
            //if (ClientApplicationManager.IsSlave)
            //{
            //    (ClientApplicationManager.ClusterApplication.ClusterAppProtocolHandler as SlaveClusterAppProtocolHandler).CallCustomFunction(Id, pFrom, invoke);
            //    return null;
            //}
            //else
            //{
              //return CustomFunctions[functionName].Invoke(this,new object[]{pFrom, invoke.InvokeParam}) as Variant;
            return CustomFunctions[functionName](pFrom, param);
            //}
        }

        public void CallFunction(string functionName,BaseProtocol pFrom, Variant invoke)
        {
            GetType().GetMethod(functionName).Invoke(this,new object[] { pFrom, invoke});
        }

        public virtual void Broadcast(BaseProtocol from, Variant invokeInfo)
	    {
	        foreach (var baseAppProtocolHandler in AppProtocolHandlers)
	        {
                baseAppProtocolHandler.Broadcast(from,invokeInfo);
	        }
            if (ClientApplicationManager.ClusterApplication!=null)
                ClientApplicationManager.ClusterApplication.GetProtocolHandler<BaseClusterAppProtocolHandler>().Broadcast(Id, from, invokeInfo);
            invokeInfo.Recycle();
	    }
        public virtual void CallClient(BaseProtocol to, string functionName, Variant invoke)
        {
            GetProtocolHandler(to).CallClient(to,functionName, invoke);
        }

        public virtual void SharedObjectTrack(BaseProtocol to, string name, uint version, bool isPersistent, Variant primitives)
        {
             GetProtocolHandler(to).SharedObjectTrack(to, name, version, isPersistent, primitives);
        }
		public BaseAppProtocolHandler GetProtocolHandler(ulong protocolType){
			return _protocolsHandlers.ContainsKey(protocolType)?_protocolsHandlers[protocolType]:null;
		}
		public BaseAppProtocolHandler GetProtocolHandler(BaseProtocol pProtocol){
			return GetProtocolHandler(pProtocol.Type);
		}
        public T GetProtocolHandler<T>(BaseProtocol pProtocol) where T : BaseAppProtocolHandler
        {
			return (T)GetProtocolHandler(pProtocol);
		}
        public T GetProtocolHandler<T>() where T : BaseAppProtocolHandler
        {
            return _protocolsHandlers.Values.OfType<T>().Single();
        }
		virtual public BaseAppProtocolHandler GetProtocolHandler(string scheme){
			BaseAppProtocolHandler pResult = null;
			if (false) {

			}
			#if HAS_PROTOCOL_RTMP
			else if (scheme.StartsWith("rtmp")) {
			pResult = GetProtocolHandler(ProtocolTypes.PT_INBOUND_RTMP) ?? GetProtocolHandler(ProtocolTypes.PT_OUTBOUND_RTMP);
			}/* HAS_PROTOCOL_RTMP */
			#endif 
			#if HAS_PROTOCOL_RTP
			else if (scheme == "rtsp") {
                pResult = GetProtocolHandler(ProtocolTypes.PT_RTSP);
			}/* HAS_PROTOCOL_RTP */
			#endif 
			#if HAS_PROTOCOL_MMS
			else if (scheme == "mms") {
			pResult = GetProtocolHandler(PT_OUTBOUND_MMS);
			}/* HAS_PROTOCOL_MMS */
			#endif 
			else {
				Logger.WARN("scheme {0} not recognized", scheme);
			}
			return pResult;
		}
        public T GetProtocolHandler<T>(string scheme) where T : BaseAppProtocolHandler
        {
			return (T)GetProtocolHandler(scheme);
		}
		virtual public bool Initialize(){
		    foreach (
		        var attribute in
                    this.GetAttribute<AppProtocolHandlerAttribute>())
		    {
		        var appProtocolHandler =
		            Activator.CreateInstance(attribute.HandlerClass, (object) _configuration) as BaseAppProtocolHandler;
                AppProtocolHandlers.Add(appProtocolHandler);
		        foreach (var type in attribute.Type)
		            RegisterAppProtocolHandler(type, appProtocolHandler);

		    }
		   
		    return PullExternalStreams();
		}
		virtual public bool ActivateAcceptors(IEnumerable<IOHandler> acceptors)
		{
		    if (acceptors.All(ActivateAcceptor)) return true;
		    Logger.FATAL("Unable to activate acceptor");
		    return false;
		}

	    virtual public bool ActivateAcceptor(IOHandler pIOHandler) {
			switch (pIOHandler.Type) {
			case IOHandlerType.IOHT_ACCEPTOR:
				{
					var pAcceptor = (TCPAcceptor) pIOHandler;
                    pAcceptor.Application = this;
					return pAcceptor.StartAccept();
				}
			case IOHandlerType.IOHT_UDP_CARRIER:
				{
					var pUDPCarrier = (UDPCarrier) pIOHandler;
				    pUDPCarrier.Protocol.NearEndpoint.Application = this;
					return pUDPCarrier.StartAccept();
				}
			default:
				{
					Logger.FATAL("Invalid acceptor type");
					return false;
				}
			}
		}
		public string GetServicesInfo()
		{
		    return string.Join("", IOHandlerManager.ActiveIoHandler.Values.Select(GetServiceInfo));
			//return IOHandlerManager.ActiveIoHandler.Values.Aggregate("", (current, ioHandler) => current + GetServiceInfo(ioHandler));
		}
        virtual public bool AcceptTCPConnection(TCPAcceptor pTCPAcceptor, SocketAsyncEventArgs e)
        {
			return pTCPAcceptor.Accept(e);
		}
		public void RegisterAppProtocolHandler(ulong protocolType,BaseAppProtocolHandler pAppProtocolHandler) 
        {
			if (_protocolsHandlers.ContainsKey (protocolType)) {
				Logger.ASSERT("Invalid protocol handler type. Already registered");
			}
			_protocolsHandlers[protocolType] = pAppProtocolHandler;
		    pAppProtocolHandler.Application = this;
        }

		public void UnRegisterAppProtocolHandler(ulong protocolType)
		{
		    if (_protocolsHandlers.ContainsKey(protocolType))
		        _protocolsHandlers[protocolType].Application = null;
			_protocolsHandlers.Remove(protocolType);
		}
		virtual public bool StreamNameAvailable(string streamName,BaseProtocol pProtocol) {
			if (AllowDuplicateInboundNetworkStreams)
				return true;
			return StreamsManager.StreamNameAvailable(streamName);
		}
		virtual public bool OutboundConnectionFailed(dynamic customParameters) {
			Logger.WARN("You should override BaseRTMPAppProtocolHandler::OutboundConnectionFailed");
			return false;
		}
        public virtual bool OnConnect(BaseProtocol pFrom, Variant param)
        {
            return true;
        }

	    protected void Disconnect(BaseProtocol protocol)
	    {
	        protocol.EnqueueForDelete();
	        ProtocolManager.CleanupDeadProtocols();
	    }
		virtual public void RegisterProtocol(BaseProtocol pProtocol) {
			if (!_protocolsHandlers.ContainsKey(pProtocol.Type))
                Logger.ASSERT("Protocol handler not activated for protocol type {0} in application {1}",
					pProtocol.Type, Name);
            else
			_protocolsHandlers[pProtocol.Type].RegisterProtocol(pProtocol);
		}
		virtual public void UnRegisterProtocol(BaseProtocol pProtocol) {
            SOManager.UnRegisterProtocol(pProtocol);
            StreamsManager.UnRegisterStreams(pProtocol.Id);
            if (!_protocolsHandlers.ContainsKey(pProtocol.Type))
                Logger.ASSERT("Protocol handler not activated for protocol type {0} in application {1}",
                    pProtocol.Type, Name);
            else
            {
                _protocolsHandlers[pProtocol.Type].UnRegisterProtocol(pProtocol);
            }
			//INFO(typeid(_protocolsHandlers[pProtocol.Type]).name());
			this.Log().Info("Protocol {0} unregistered from application: {1}", pProtocol.ToString(), Name);
		}
		virtual public void SignalStreamRegistered(IStream pStream)
		{
		    var protocol = pStream.GetProtocol();
            Logger.INFO("Stream {0}({1}) with name `{2}` registered to application {3} from protocol {4}({5})",
                pStream.Type.TagToString(),pStream.UniqueId,pStream.Name,Name,
               protocol != null ? protocol.Type.TagToString() : "",
           protocol?.Id ?? 0);
		}

        virtual public void SignalStreamUnRegistered(IStream pStream)
        {
            var protocol = pStream.GetProtocol();
			 Logger.INFO("Stream {0}({1}) with name `{2}` unregistered to application {3} from protocol {4}({5})",
                pStream.Type.TagToString(),pStream.UniqueId,pStream.Name,Name,
                protocol != null ? protocol.Type.TagToString() : "",
           protocol?.Id ?? 0);
		}
		virtual public bool PullExternalStreams() {
		
			//1. Minimal verifications
			if(_configuration["externalStreams"]==null) {
				return true;
			}

//			if (!_c["externalStreams"] is IDictionary<string,object>) {
//				Logger.FATAL("Invalid rtspStreams node");
//				return false;
//			}

			//2. Loop over the stream definitions and validate duplicated stream names
		  
            foreach (var temp in _configuration["externalStreams"].Children.Values)
            {
				if(temp["localStreamName"] == null){
					Logger.WARN ("External stream configuration is doesn't have localStreamName property invalid");
					continue;
				}
				if(!AllowDuplicateInboundNetworkStreams){
					Logger.WARN ("External stream configuration produces duplicated stream names");
					continue;
				}
                
				if (!PullExternalStream(temp)) {
					Logger.WARN("External stream configuration is invalid");
				}
			}
			return true;
		}

        virtual public bool PullExternalStream(Variant streamConfig)
        {
			//1. Minimal verification
			if (streamConfig["uri"] == null) {
				Logger.FATAL("Invalid uri");
				return false;
			}
			//2. Split the URI
			var uri = new Uri(streamConfig["uri"]);
			
			//3. Depending on the scheme name, get the curresponding protocol handler
			///TODO: integrate this into protocol factory manager via protocol factories
			string scheme = uri.Scheme;
			var pProtocolHandler = GetProtocolHandler(scheme);
			if (pProtocolHandler == null) {
                Logger.WARN("Unable to find protocol handler for scheme {0} in application {1}", scheme, Name);
				return false;
			}
			//4. Initiate the stream pulling sequence
			return pProtocolHandler.PullExternalStream(uri, streamConfig);
		}
        virtual public bool PushLocalStream(Variant streamConfig)
        {
			//1. Minimal verification
            if (streamConfig["targetUri"] == null)
            {
				Logger.FATAL("Invalid uri");
				return false;
			}
            if (streamConfig["localStreamName"] == null)
            {
				Logger.FATAL("Invalid local stream name");
				return false;
			}
			var streamName = (string) streamConfig["localStreamName"];
			streamName = streamName.Trim ();
			if (streamName == "") {
				Logger.FATAL("Invalid local stream name");
				return false;
			}
            streamConfig["localStreamName"] = streamName;

			//2. Split the URI
            Uri uri = new Uri((string) streamConfig["targetUri"]);
			//3. Depending on the scheme name, get the curresponding protocol handler
			///TODO: integrate this into protocol factory manager via protocol factories
			string scheme = uri.Scheme;
			BaseAppProtocolHandler pProtocolHandler = GetProtocolHandler(scheme);
			if (pProtocolHandler == null) {
                Logger.WARN("Unable to find protocol handler for scheme {0} in application {1}", scheme, Name);
				return false;
			}

			//4. Initiate the stream pulling sequence
			return pProtocolHandler.PushLocalStream(streamConfig);
		}
		public bool ParseAuthentication() {
			//1. Get the authentication configuration node

            if (_configuration[Defines.CONF_APPLICATION_AUTH] !=null&& !_configuration[Defines.CONF_APPLICATION_AUTH].Children.Any())
            {
                Logger.WARN("Authentication node is present for application {0} but is empty or invalid", Name);
			}

            var auth = _configuration[Defines.CONF_APPLICATION_AUTH];
            if(auth!=null)
			//2. Cycle over all access schemas
            foreach (var scheme in auth.Children)
            {
                var pHandler = GetProtocolHandler(scheme.Key);
				if (pHandler == null) {
                    Logger.WARN("Authentication parsing for app name {0} failed. No handler registered for schema {1}", Name, scheme.Key);
					return true;
				}
                if (!pHandler.ParseAuthenticationNode(scheme.Value, _authSettings[scheme.Key]))
                {
                    Logger.FATAL("Authentication parsing for app name {0} failed. scheme was {1}", Name, scheme.Key);
					return false;
				}
			}
			return true;
		}
		public static void Shutdown(BaseClientApplication pApplication) {
			//1. Get the list of all active protocols
			var protocols = ProtocolManager.ActiveProtocols;

			//2. enqueue for delete for all protocols bound to pApplication
			foreach (var p in protocols.Where(x=>x.Value.Application!=null&&x.Value.Application.Id==pApplication.Id).Select(x=>x.Value))
			{
			    p.Application = null;
				p.EnqueueForDelete ();
			}

			//1. Get the list of all active IOHandlers and enqueue for delete for all services bound to pApplication
			var handlers = IOHandlerManager.ActiveIoHandler;
			foreach (var h in handlers) {
				BaseProtocol pTemp = h.Value.Protocol;
				while (pTemp != null) {
					if ((pTemp.Application != null)
						&& (pTemp.Application.Id == pApplication.Id)) {
						IOHandlerManager.EnqueueForDelete(h.Value);
						break;
					}
				    pTemp = pTemp.NearProtocol;
				}
			}

		    handlers = IOHandlerManager.ActiveIoHandler;
			foreach (var h in handlers) {
				if (h.Value.Type == IOHandlerType.IOHT_ACCEPTOR && ((TCPAcceptor)h.Value).Application!=null&&	((TCPAcceptor)h.Value).Application.Id == pApplication.Id) {
					IOHandlerManager.EnqueueForDelete(h.Value);
				}
			}
			//4. Unregister it
			ClientApplicationManager.UnRegisterApplication(pApplication);

		}
		public string GetServiceInfo(IOHandler pIOHandler) {
			if ((pIOHandler.Type != IOHandlerType.IOHT_ACCEPTOR)
				&& (pIOHandler.Type != IOHandlerType.IOHT_UDP_CARRIER))
				return "";
			if (pIOHandler.Type == IOHandlerType.IOHT_ACCEPTOR) {
				if ((((TCPAcceptor ) pIOHandler).Application == null)
					|| (((TCPAcceptor ) pIOHandler).Application.Id != Id)) {
					return "";
				}
			} else {
				if ((pIOHandler.Protocol == null)
                    || (pIOHandler.Protocol.NearEndpoint.Application == null)
                    || (pIOHandler.Protocol.NearEndpoint.Application.Id != Id))
                {
					return "";
				}
			}

		    var _params = pIOHandler.Type == IOHandlerType.IOHT_ACCEPTOR
		        ? ((TCPAcceptor) pIOHandler).Parameters
		        : ((UDPCarrier) pIOHandler).Parameters;
			if (_params.Children.Count == 0)return "";
			string ss = "";
			ss +="+---+---------------+-----+-------------------------+-------------------------+"+Environment.NewLine;
			ss += "|";
			ss +=(pIOHandler.Type == IOHandlerType.IOHT_ACCEPTOR ? "tcp" : "udp");
			ss += "|";
            string s =  _params[Defines.CONF_IP];
            ss += s.PadRight(3 * 4 + 3, ' ');
			ss += "|";
            int p =  _params[Defines.CONF_PORT];
		    s = p+"";
		    ss += s.PadRight(5, ' ');
			ss += "|";
            s=_params[Defines.CONF_PROTOCOL];
            ss += s.PadRight(25, ' ');
			ss += "|";
			ss += Name.PadRight(25,' ');
			ss += "|";
			ss += Environment.NewLine;
			return ss;
		}

	    public virtual void Dispose()
	    {
            foreach (var type in this.GetAttribute<AppProtocolHandlerAttribute>().SelectMany(attribute => attribute.Type))
	            UnRegisterAppProtocolHandler(type);
	    }

	    public virtual void OnPublish(BaseProtocol protocol,IInStream inStream,string type)
	    {
#if PARALLEL
            StreamsManager.GetWaitingSubscribers(inStream.Name, inStream.Type).AsParallel().ForAll(x => x.Link(inStream));
#else
            foreach (var pBaseOutStream in StreamsManager.GetWaitingSubscribers(inStream.Name, inStream.Type))
            {
                pBaseOutStream.Link(inStream);
            }
#endif
	        if (type == "append" || type == "record")
	            StreamsManager.CreateOutFileStream(protocol, inStream, type == "append");
	        if (ClientApplicationManager.ClusterApplication!=null)
	        {
                ClientApplicationManager.ClusterApplication.GetProtocolHandler<BaseClusterAppProtocolHandler>().PublishStream(Id, inStream, type);
	        }
	    }
	}
}

