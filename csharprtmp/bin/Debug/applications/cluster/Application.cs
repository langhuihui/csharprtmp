using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Cluster;
using CSharpRTMP.Core.Protocols.Rtmp;

namespace cluster
{
    public class Application : BaseClientApplication, IClusterApplication
    {
        public BaseClusterAppProtocolHandler ClusterAppProtocolHandler { get; private set; }
        public Application(Variant configuration) : base(configuration)
        {
            ClientApplicationManager.ClusterApplication = this;
            ClientApplicationManager.IsSlave = !string.IsNullOrEmpty(configuration["master"]);
        }

        public override bool Initialize()
        {
            if (ClientApplicationManager.IsSlave)
            {
                Logger.INFO("Server is in Slave mode");
                ClusterAppProtocolHandler = new SlaveClusterAppProtocolHandler(_configuration);
                RegisterAppProtocolHandler(ProtocolTypes.PT_OUTBOUND_CLUSTER, ClusterAppProtocolHandler);
                SOManager["appList"].Synchronization += dirty =>
                {
                    if (dirty.Type == Defines.SOT_SC_UPDATE_DATA && ClientApplicationManager.ApplicationByName.ContainsKey(dirty.PropertyName) && SOManager["appList"][dirty.PropertyName] != null)
                    {
                        ClientApplicationManager.ApplicationByName[dirty.PropertyName].Id =
                            SOManager["appList"][dirty.PropertyName];
                        ClientApplicationManager.ApplicationById[SOManager["appList"][dirty.PropertyName]] =
                            ClientApplicationManager.ApplicationByName[dirty.PropertyName];
                    }
                };
            }
            else
            {
                ClusterAppProtocolHandler = new MasterClusterAppProtocolHandler(_configuration);
                Logger.INFO("Server is in Master mode");
                RegisterAppProtocolHandler(ProtocolTypes.PT_INBOUND_CLUSTER, ClusterAppProtocolHandler);
            }
            return true;
        }
        public void Broadcast(BaseClientApplication app, BaseProtocol @from, Variant invokeInfo)
        {
            ClusterAppProtocolHandler.Broadcast(app.Id, from, invokeInfo);
        }

        public void OnSOCreated(SO so)
        {
            ClusterAppProtocolHandler.OnSOCreated(so);
        }

        private bool _connected;

        public bool Connected
        {
            get
            {
                return _connected;
            }
            set
            {
                _connected = value;
                if (!_connected)
                {
                    (ClusterAppProtocolHandler as SlaveClusterAppProtocolHandler).ReconnectTimer.Start();
                }
            }
        }

        public void SetAppId(BaseClientApplication pClientApplication)
        {
            SOManager["appList"][pClientApplication.Name] = pClientApplication.Id;
        }

        public bool GetAppId(BaseClientApplication pClientApplication)
        {
            if (SOManager["appList"][pClientApplication.Name] == null)
            {
                  (ClusterAppProtocolHandler as SlaveClusterAppProtocolHandler).GetAppId(pClientApplication.Name);
                return false;
            }
            else
            {
                pClientApplication.Id = SOManager["appList"][pClientApplication.Name];
                return true;
            }
        }
    }
}
