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
    public class Application : BaseClientApplication
    {
        public Application(Variant configuration) : base(configuration)
        {
            ClientApplicationManager.ClusterApplication = this;
            ClientApplicationManager.IsSlave = !string.IsNullOrEmpty(configuration["master"]);
        }

        public override bool Initialize()
        {
            if (ClientApplicationManager.IsSlave)
            {
                Logger.WARN("Server is in Slave mode");
                var sc = new SlaveClusterAppProtocolHandler(_configuration);
                RegisterAppProtocolHandler(ProtocolTypes.PT_OUTBOUND_CLUSTER, sc);
                SOManager["appList"].Synchronization += sc.OnAppListSynchronization;
            }
            else
            {
                Logger.WARN("Server is in Master mode");
                RegisterAppProtocolHandler(ProtocolTypes.PT_INBOUND_CLUSTER, new MasterClusterAppProtocolHandler(_configuration));
            }
            return true;
        }
    }
}
