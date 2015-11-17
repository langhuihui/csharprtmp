using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.NetIO;
using CSharpRTMP.Core.Protocols.Rtmp;
using CSharpRTMP.Core.Streaming;
using Newtonsoft.Json.Bson;

namespace CSharpRTMP.Core.Protocols.Cluster
{
    public class SlaveClusterAppProtocolHandler : BaseClusterAppProtocolHandler
    {
        public delegate void GotAppIdDelegate(uint appId);

        public delegate void BackOnlineDelegate(OutboundClusterProtocol protocol);
        public OutboundClusterProtocol OutboundCluster;
        public System.Timers.Timer ReconnectTimer = new System.Timers.Timer(5000);
        private BackOnlineDelegate _offlineTasks;
        public readonly Dictionary<string ,GotAppIdDelegate> GotAppIdTasks = new Dictionary<string, GotAppIdDelegate>();
        public SlaveClusterAppProtocolHandler(Variant configuration)
            : base(configuration)
        {
            ReconnectTimer.Elapsed += ReconnectTimer_Elapsed;
            ReconnectTimer.Start();
        }
        public void OnAppListSynchronization(DirtyInfo dirty)
        {
            if (dirty.Type == Defines.SOT_SC_UPDATE_DATA &&  Application.SOManager["appList"][dirty.PropertyName] != null)
            {
                var appId = Application.SOManager["appList"][dirty.PropertyName];
                Logger.INFO("sync applist:{0},{1}", dirty.PropertyName, appId);
                ClientApplicationManager.GetOrCreateRoom(dirty.PropertyName, appId);
                if (GotAppIdTasks.ContainsKey(dirty.PropertyName))
                {
                    GotAppIdTasks[dirty.PropertyName](appId);
                }
            }
        }
        public bool ConnectOutboundCluster()
        {
            var uri = new Uri(Configuration["master"]);
            //string localStreamName = streamConfig["localStreamName"] ?? "stream_" + Utils.GenerateRandomString(8);
            var parameters = Variant.Get();
            //parameters["customParameters", "externalStreamConfig"] = streamConfig;
            parameters[Defines.CONF_APPLICATION_NAME] = Application.Name;
            var scheme = uri.Scheme;
            var endpoint =
                new IPEndPoint(
                    Dns.GetHostAddresses(uri.Host).First(x => x.AddressFamily == AddressFamily.InterNetwork),
                    uri.Port > 0 ? uri.Port : 1935);

            var chain = ProtocolFactoryManager.ResolveProtocolChain(Defines.CONF_PROTOCOL_OUTBOUND_CLUSTER);
            return TCPConnector<OutboundClusterProtocol>.Connect(endpoint, chain, parameters); 
        }
        
        private void ReconnectTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (ConnectOutboundCluster())
            {
                Logger.INFO("ConnectOutboundCluster");
                ReconnectTimer.Stop();
            }
        }

        public override void RegisterProtocol(BaseProtocol protocol)
        {
            OutboundCluster = protocol as OutboundClusterProtocol;
            foreach (var room in ClientApplicationManager.ApplicationByName.Values)
            {
                room.SOManager.RegisterProtocol(OutboundCluster);
            }
            while (_offlineTasks!=null)
            {
                _offlineTasks(OutboundCluster);
                _offlineTasks = null;
            }
        }

        public override void UnRegisterProtocol(BaseProtocol protocol)
        {
            Logger.INFO("DisconnectOutboundCluster");
            ReconnectTimer.Start();
            foreach (var so in OutboundCluster.SOs)
            {
                so.UnRegisterProtocol(OutboundCluster);
            }
            OutboundCluster = null;
        }

        public override void OnSOCreated(SO so)
        {
            if(OutboundCluster!=null)so.RegisterProtocol(OutboundCluster);
        }

        public override void Broadcast(uint appId, BaseProtocol pFrom, Variant invokeInfo)
        {
            if(pFrom != OutboundCluster)OutboundCluster.Broadcast(appId, pFrom, invokeInfo);
        }

        public override void CallAppFunction(uint appId, string functionName, Variant invoke)
        {
            if (OutboundCluster != null) OutboundCluster.Send(ClusterMessageType.Call, o =>
            {
                o.Write7BitValue(appId);
                o.Write(functionName);
                o.Write(invoke.ToBytes());
            });
        }

        public uint GetAppId(string appName)
        {
            if (Application.SOManager["appList"][appName] == null)
            {
                if (OutboundCluster != null)
                {
                    Logger.INFO("GetAppId:" + appName);
                    OutboundCluster.Send(ClusterMessageType.GetAppId, o => o.Write(appName));
                }
                else
                {
                    if (_offlineTasks == null)
                        _offlineTasks = x => x.Send(ClusterMessageType.GetAppId, o => o.Write(appName));
                    else
                        _offlineTasks += x => x.Send(ClusterMessageType.GetAppId, o => o.Write(appName));
                }
                return 0;
            }
            return Application.SOManager["appList"][appName];
        }
        public override void PublishStream(uint appId, IInStream inStream,string type="live")
        {
            if (OutboundCluster != null) OutboundCluster.PublishStream(appId, inStream,type);
        }

        public override void PlayStream(uint appId, string streamName)
        {
            if (OutboundCluster != null) OutboundCluster.PlayStream(appId, streamName);
        }
    }
}
