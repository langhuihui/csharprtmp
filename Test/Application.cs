
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security;
using CSharpRTMP.Common;
using CSharpRTMP.Core;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Cluster;
using CSharpRTMP.Core.Protocols.Rtmfp;
using CSharpRTMP.Core.Protocols.Rtmp;

namespace Test
{
    [AppProtocolHandler(typeof(BaseRTMPAppProtocolHandler),ProtocolTypes.PT_INBOUND_RTMP)]
    [AppProtocolHandler(typeof(BaseRtmfpAppProtocolHandler),ProtocolTypes.PT_INBOUND_RTMFP )]
    public class Application:BaseClientApplication
    {
        private readonly Dictionary<string, BaseProtocol> OnlineClient = new Dictionary<string, BaseProtocol>(); 
        public Application(Variant configuration)
            : base(configuration)
        {
            
        }
        [CustomFunction("callClient")]

        public Variant _CallClient(BaseProtocol pFrom, Variant invoke)
        {
            string clientId = invoke[1];
            string functionName = invoke[2];
            if (OnlineClient.ContainsKey(clientId))
            {
                invoke.RemoveAt(1);
                invoke.RemoveAt(1);
                CallClient(OnlineClient[clientId], functionName, invoke);
            }
            else if(!(pFrom is BaseClusterProtocol))
            {
                if (ClientApplicationManager.ClusterApplication != null)
                    ClientApplicationManager.ClusterApplication.GetProtocolHandler<BaseClusterAppProtocolHandler>().CallAppFunction(Id, "_CallClient", invoke);
            }
            return null;
        }

        [CustomFunction("broadcast")]

        public Variant _Broadcast(BaseProtocol pFrom, Variant invoke)
        {
            var message = Variant.Get();
            message[Defines.RM_INVOKE_FUNCTION] = (string)invoke[1];
            var param = invoke.Clone();
            param.RemoveAt(0);
            param.RemoveAt(0);
            message[Defines.RM_INVOKE_PARAMS] = param;
            Broadcast(pFrom,message);
            return null;
        }

        public override void UnRegisterProtocol(BaseProtocol pProtocol)
        {
            var userList = SOManager["userList"];
            var room = SOManager["room"];
            lock (room)
            {
                base.UnRegisterProtocol(pProtocol);
                if (OnlineClient.ContainsValue(pProtocol))
                {
                    var clientId = OnlineClient.Single(x => x.Value == pProtocol).Key;
                    if (room["publisher1"] != null && room["publisher1"]["id"] == clientId) room.UnSet("publisher1");
                    if (room["publisher2"] != null && room["publisher2"]["id"] == clientId) room.UnSet("publisher2");
                    if (room["adminId"] != null && room["adminId"]["id"] == clientId)
                    {
                        room.UnSet("adminId");
                        room.UnSet("ppt");
                        room.UnSet("pptPageNum");
                    }
                    room.Track();
                    OnlineClient.Remove(clientId);
                    userList.UnSet(clientId);
                    userList.Track();
                }
            }
        }

        public override bool OnConnect(BaseProtocol pFrom, Variant param)
        {
            string clientId = param[1];
            var so = SOManager["userList"];
            lock (so)
            {
                if (so.HasProperty(clientId) && OnlineClient.ContainsKey(clientId)) Disconnect(OnlineClient[clientId]);
                so.Set(clientId, Variant.GetMap(new VariantMapHelper
            {
                {"id",clientId},
                {"name",(string)param[2]},
                {"hasCamera",(bool)param[3]},
                {"hasMic",(bool)param[4]},
                {"role",(int)param[5]},
                {"handUp",false}
            }));
                OnlineClient[clientId] = pFrom;
            }
            
            if ((int)param[5] == 1)
            {
                
                so = SOManager["room"];
                lock (so)
                {
                    so.Set("adminId", clientId);
                //    so.Set("publisher1", Variant.GetMap(new VariantMapHelper
                //{
                //    {"id",clientId},
                //    {"name",(string)param[2]},
                //}));
                }
               
            }
            return true;
        }
    }
}
