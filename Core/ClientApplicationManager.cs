using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Cluster;
using CSharpRTMP.Core.Protocols.Rtmp;

namespace CSharpRTMP.Core
{

    public static class ClientApplicationManager
    {
        public static BaseClientApplication DefaultApplication { get; private set; }
        public static bool IsSlave;
        public static BaseClientApplication ClusterApplication;
        public static Dictionary<uint, BaseClientApplication> ApplicationById = new Dictionary<uint, BaseClientApplication>();
        public static Dictionary<string, BaseClientApplication> ApplicationByName= new Dictionary<string, BaseClientApplication>();
        public static void Shutdown()
        {
            ApplicationById.Clear();
            ApplicationByName.Clear();
            DefaultApplication = null;
        }
        public static bool RegisterApplication(this BaseClientApplication pClientApplication)
        {
            if (pClientApplication.Id == 0)
            {
                if (IsSlave)
                {
                    pClientApplication.Id = ClusterApplication.GetProtocolHandler<SlaveClusterAppProtocolHandler>().GetAppId(pClientApplication.Name);
                }
                else
                {
                    pClientApplication.Id = ++BaseClientApplication._idGenerator;
                    ClusterApplication.SOManager["appList"][pClientApplication.Name] = pClientApplication.Id;
                    ClusterApplication.SOManager["appList"].Track();
                }
            }
            Logger.INFO("RegisterApplication：{0}({1})", pClientApplication.Name, pClientApplication.Id);
            if (ApplicationById.ContainsKey(pClientApplication.Id))
            {
                Logger.FATAL("Client application with id {0} already registered", pClientApplication.Id);
                return false;
            }
            if (ApplicationByName.ContainsKey(pClientApplication.Name))
            {
                Logger.FATAL("Client application with name `{0}` already registered", pClientApplication.Name);
                return false;
            }
            if (pClientApplication.Aliases.Any(x => ApplicationByName.ContainsKey(x)))
            {
                Logger.FATAL("Client application with alias `{0}` already registered", pClientApplication.Name);
                return false;
            }
            if(pClientApplication.Id != 0)
                ApplicationById[pClientApplication.Id] = pClientApplication;
            ApplicationByName[pClientApplication.Name] = pClientApplication;
            //foreach (var aliases in pClientApplication.Aliases)
            //    ApplicationByName[aliases] = pClientApplication;
            if (pClientApplication.IsDefault) DefaultApplication = pClientApplication;
            return true;
        }

        public static void UnRegisterApplication(this BaseClientApplication pClientApplication)
        {
            if (ApplicationById.ContainsKey(pClientApplication.Id)) ApplicationById.Remove(pClientApplication.Id);
            if (ApplicationByName.ContainsKey(pClientApplication.Name)) ApplicationByName.Remove(pClientApplication.Name);
            foreach (var item in ApplicationByName.Where(x => pClientApplication.Aliases.Contains(x.Key)).ToArray())
            {
                ApplicationByName.Remove(item.Key);
            }
            if (DefaultApplication?.Id == pClientApplication.Id)
            {
                DefaultApplication = null;
            }
            pClientApplication.Log().Info("Application `{0}` ({1}) unregistered", pClientApplication.Name,
                    pClientApplication.Id);
        }

        public static BaseClientApplication FindAppByName(string name) => ApplicationByName.ContainsKey(name) ? ApplicationByName[name] : null;

        public static BaseClientApplication GetOrCreateRoom(string roomFullName,uint id =  0)
        {
            BaseClientApplication app = FindAppByName(roomFullName);
            if (app!=null)
            {
                if (id == 0 || app.Id != 0) return app;
                app.Id = id;
                ApplicationById[id] = app;
                return app;
            }
           
            var appName = GetAppName(roomFullName);
            app = FindAppByName(appName);
            return app==null?null:CreateRoom(app, roomFullName, id);
        }
        public static BaseClientApplication CreateRoom(BaseClientApplication defaultRoom, string roomFullName, uint id = 0)
        {
            var config = defaultRoom.Configuration.Clone();
            config[Defines.CONF_APPLICATION_NAME] = roomFullName;
            var application = (BaseClientApplication)Activator.CreateInstance(defaultRoom.GetType(), (object)config);
            application.Id = id;
            RegisterApplication(application);
            application.Initialize();
            return application;
        }
        public static BaseClientApplication SwitchRoom(BaseProtocol from,string roomFullName, Variant configuration)
        {
            var appName = GetAppName(roomFullName);

            if (((string) configuration[Defines.CONF_APPLICATION_NAME]).Split('/')[0] != appName)
            {
                return from.Application = GetOrCreateRoom(roomFullName);
            }
            if (roomFullName == (string) configuration[Defines.CONF_APPLICATION_NAME] || string.IsNullOrEmpty(roomFullName))
            {//为自身
                return from.Application;
            }
           
            var app = FindAppByName(roomFullName) ?? CreateRoom(from.Application, roomFullName);
            from.Application = app;
            return app;
        }

        public static string GetAppName(string fullName) => fullName.Split('/')[0];
    }
}
