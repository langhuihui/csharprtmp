using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Cluster;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Core
{
	public abstract class BaseAppProtocolHandler
	{
		protected readonly Variant Configuration;
		public virtual BaseClientApplication Application{ set; get;}

        protected BaseAppProtocolHandler(Variant configuration)
        {
            Configuration = configuration;

        }
        public virtual bool ParseAuthenticationNode(Variant node, Variant result)
        {
			return false;
		}
        public virtual bool PullExternalStream(Uri url, Variant streamConfig)
        {
			return false;
		}
        public virtual bool PushLocalStream(Variant streamConfig)
        {
			return false;
		}

        public abstract void RegisterProtocol(BaseProtocol protocol);
        public abstract void UnRegisterProtocol(BaseProtocol protocol);

	    public abstract void Broadcast(BaseProtocol from,Variant invokeInfo);
        public abstract void CallClient(BaseProtocol to, string functionName, Variant param);
        public abstract void SharedObjectTrack(BaseProtocol to, string name, uint version, bool isPersistent, Variant primitives);
	}
}

