using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.NetIO;
using CSharpRTMP.Core.Protocols;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static CSharpRTMP.Common.Defines;
using static CSharpRTMP.Common.Logger;

namespace CSharpRTMP.Core
{
    public sealed class Acceptor : ConfigurationSection
    {
        [ConfigurationProperty("ip",DefaultValue = "0.0.0.0")]
        public string Ip => (string)this["ip"];
        [ConfigurationProperty("port",IsRequired = true)]
        public short Port => (short)this["port"];
        [ConfigurationProperty("protocol",IsKey = true,IsRequired = true)]
        public string Protocol => (string)this["protocol"];
    }
    public sealed class AcceptorCollection: ConfigurationElementCollection
    {
        protected override string ElementName => "acceptor";
        protected override ConfigurationElement CreateNewElement()
        {
            return new Acceptor();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((Acceptor)element).Protocol;
        }
        public override ConfigurationElementCollectionType CollectionType => ConfigurationElementCollectionType.BasicMap;
    }

    public sealed class ApplicationSection : ConfigurationSection
    {
        [ConfigurationProperty("name")]
        public string AppName => (string)this["name"];

        [ConfigurationProperty("", IsDefaultCollection = true, IsRequired = false)]
        public AcceptorCollection Acceptors => (AcceptorCollection)this[""];

        [ConfigurationProperty("master", IsRequired = false)]
        public string Master => (string)this["master"];

        [ConfigurationProperty(CONF_APPLICATION_KEYFRAMESEEK, IsRequired = false,DefaultValue = false)]
        public bool KeyFrameSeek => (bool) this[CONF_APPLICATION_KEYFRAMESEEK];
        [ConfigurationProperty(CONF_APPLICATION_MEDIAFOLDER, IsRequired = false, DefaultValue = null)]
        public string MediaFolder => (string)this[CONF_APPLICATION_MEDIAFOLDER];

        [ConfigurationProperty(CONF_APPLICATION_LIBRARY, IsRequired = false, DefaultValue = null)]
        public string LibraryPath => (string) this[CONF_APPLICATION_LIBRARY]; 
         [ConfigurationProperty(CONF_APPLICATION_DIRECTORY, IsRequired = false, DefaultValue = null)]
        public string AppDir => (string)this[CONF_APPLICATION_DIRECTORY];

        [ConfigurationProperty(CONF_APPLICATION_SEEKGRANULARITY, IsRequired = false, DefaultValue = 1)]
        public int SeekGranularity => (int)this[CONF_APPLICATION_SEEKGRANULARITY];
        [ConfigurationProperty(CONF_APPLICATION_CLIENTSIDEBUFFER, IsRequired = false, DefaultValue = (long)5)]
        public long ClientSideBuffer => (long)this[CONF_APPLICATION_CLIENTSIDEBUFFER];
        [ConfigurationProperty(CONF_APPLICATION_RTCPDETECTIONINTERVAL, IsRequired = false, DefaultValue = (byte)60)]
        public byte RtcpDetectionInterval => (byte)this[CONF_APPLICATION_RTCPDETECTIONINTERVAL];
    }
    public sealed class ApplicationConfigCollection : ConfigurationElementCollection
    {
        protected override string ElementName => "application";

        protected override ConfigurationElement CreateNewElement()
        {
            return new ApplicationSection();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((ApplicationSection)element).AppName;
        }
        public override ConfigurationElementCollectionType CollectionType => ConfigurationElementCollectionType.BasicMap;
    }

    public sealed class ApplicationsSection : ConfigurationSection
    {
        [ConfigurationProperty(CONF_APPLICATIONS_ROOTDIRECTORY, DefaultValue = "applications")]
        public string RootDirectory => (string)this[CONF_APPLICATIONS_ROOTDIRECTORY];

        [ConfigurationProperty("", IsDefaultCollection = true)]
        public ApplicationConfigCollection Applications => (ApplicationConfigCollection)base[""];
       
    }

    public class ConfigFile:IDisposable
    {
        Variant _configuration;
        Variant _logAppenders;
        string _rootAppFolder;
        readonly Variant _applications = new Variant();
        private readonly HashSet<string> _uniqueNames = new HashSet<string>();
        private readonly Dictionary<string, Module> _modules = new Dictionary<string, Module>();
        private readonly Func<Variant, BaseClientApplication> _staticGetApplicationFunction;
        private readonly Func<Variant, BaseProtocolFactory> _staticGetFactoryFunction;

        public ConfigFile(Func<Variant, BaseClientApplication> staticGetApplicationFunction, Func<Variant, BaseProtocolFactory> staticGetFactoryFunction)
        {
            _staticGetApplicationFunction = staticGetApplicationFunction;
            _staticGetFactoryFunction = staticGetFactoryFunction;
        }

        public bool LoadConfigFile()
        {
            ApplicationsSection config = (ApplicationsSection)ConfigurationManager.GetSection(CONF_APPLICATIONS);
            _rootAppFolder = config.RootDirectory;
            if (string.IsNullOrEmpty(_rootAppFolder)) _rootAppFolder = ".";
            if (_rootAppFolder.Last() != Path.DirectorySeparatorChar) _rootAppFolder += Path.DirectorySeparatorChar;
            return config.Applications.Cast<ApplicationSection>().All(NormalizeApplication);
        }
        private bool NormalizeApplication(ApplicationSection appConfig)
        {
            var name = appConfig.AppName;
            var node = new Variant { { "name", name } };
            if (!string.IsNullOrEmpty(appConfig.Master))
                node["master"] = appConfig.Master;

            var temp = "";
            
            if (string.IsNullOrEmpty(name))
            {
                FATAL("Invalid application name");
                return false;
            }
            if (_uniqueNames.Contains(name))
            {
                FATAL("Application name {0} already taken", name);
                return false;
            }
            _uniqueNames.Add(name);

            string appDir = appConfig.AppDir;
            if (string.IsNullOrEmpty(appDir)) appDir = _rootAppFolder + name;
            temp = appDir.NormalizePath();
            if (temp.Equals(string.Empty))
                WARN("path not found:{0}", appDir);
            else appDir = temp;
            if (appDir.Last() != Path.DirectorySeparatorChar) appDir += Path.DirectorySeparatorChar;
            node[CONF_APPLICATION_DIRECTORY] = appDir;

            string mediaFolder = appConfig.MediaFolder;
            if (!string.IsNullOrEmpty(mediaFolder)) { 
                temp = mediaFolder.NormalizePath();
                if (temp.Equals(string.Empty))
                {
                    WARN("path not found:{0}", mediaFolder);
                    try { Directory.CreateDirectory(mediaFolder); }
                    catch (Exception) { FATAL("can't create mediafolder"); }
                }
                else mediaFolder = temp;
                if (mediaFolder.Last() != Path.DirectorySeparatorChar) mediaFolder += Path.DirectorySeparatorChar;
                node[CONF_APPLICATION_MEDIAFOLDER] = mediaFolder;
            }
            string libraryPath = appConfig.LibraryPath;
            if (string.IsNullOrEmpty(libraryPath)) libraryPath = appDir + name + ".dll";
            temp = libraryPath.NormalizePath();
            if (temp.Equals(string.Empty))
            {
                if (_staticGetApplicationFunction == null || _staticGetFactoryFunction == null)
                {
                    WARN("Library not found:{0}", libraryPath);
                    //return false;
                }
                temp = libraryPath;
            }
            libraryPath = temp;

            node[CONF_APPLICATION_LIBRARY] = libraryPath;

            //if (string.IsNullOrEmpty(node[CONF_APPLICATION_INIT_APPLICATION_FUNCTION]))  node[CONF_APPLICATION_INIT_APPLICATION_FUNCTION]  = "GetApplication_" + name;
            //if (string.IsNullOrEmpty(node[CONF_APPLICATION_INIT_FACTORY_FUNCTION])) node[CONF_APPLICATION_INIT_FACTORY_FUNCTION] = "GetFactory_" + name;

            //if (node[CONF_APPLICATION_VALIDATEHANDSHAKE] == null)
            //    node[CONF_APPLICATION_VALIDATEHANDSHAKE] = true;
            //if (node[CONF_APPLICATION_DEFAULT] == null)
            //    node[CONF_APPLICATION_DEFAULT] = false;

            //if (node[CONF_APPLICATION_GENERATE_METFILES] == null)
            //    node[CONF_APPLICATION_GENERATE_METFILES] = false;

            node[CONF_APPLICATION_KEYFRAMESEEK] = appConfig.KeyFrameSeek;

            //if (node[CONF_APPLICATION_RENAMEBADFILES] == null)
            //    node[CONF_APPLICATION_RENAMEBADFILES] = false;
            //if (node[CONF_APPLICATION_EXTERNSEEKGENERATOR] == null)
            //    node[CONF_APPLICATION_EXTERNSEEKGENERATOR] = false;

            var seekGranularity = appConfig.SeekGranularity;
            if (seekGranularity < 0 || seekGranularity > 300)seekGranularity = 1;
            node[CONF_APPLICATION_SEEKGRANULARITY] = seekGranularity;

            var clientSideBuffer = appConfig.ClientSideBuffer;
            if (clientSideBuffer < 0 || clientSideBuffer > 300)clientSideBuffer = 5;
                
            node[CONF_APPLICATION_CLIENTSIDEBUFFER] = clientSideBuffer;
            node[CONF_APPLICATION_RTCPDETECTIONINTERVAL] = appConfig.RtcpDetectionInterval>=60?60:appConfig.RtcpDetectionInterval;

            if (appConfig.Acceptors.Count > 0)
            {
                var acceptors = new Variant();
                node["acceptors"] = acceptors;
                foreach (Acceptor acceptor in appConfig.Acceptors)
                {
                    var acceptorVariant = new Variant
                    {
                        {"ip", acceptor.Ip},
                        {"port", acceptor.Port},
                        {"protocol", acceptor.Protocol}
                    };
                    node["acceptors"].Add(acceptorVariant);
                    if (NormalizeApplicationAcceptor(acceptorVariant, appDir)) continue;
                    FATAL("Invalid acceptor in {0}", appDir);
                    return false;
                }
            }

            //var aliases = node[CONF_APPLICATION_ALIASES];
            //if (aliases == null||aliases.Children.Values.All(x => _uniqueNames.Add( x))) return true;
            //FATAL("Alias name is already taken");
            _applications.Add(node);
            return true;
        }

        
        private bool NormalizeApplicationAcceptor(Variant node, string baseFolder)
        {
            var ip = (string) node[CONF_IP];
            if (string.IsNullOrEmpty(ip))
            {
                FATAL("Invalid ip");
                return false;
            }
            //Dns.GetHostAddresses(ip)
            int port = node[CONF_PORT];
            if (port <= 0 || port >= 65535)
            {
                FATAL("Invalid port");
                return false;
            }
            var protocol = (string)node[CONF_PROTOCOL];
            if (string.IsNullOrEmpty(protocol))
            {
                FATAL("Invalid protocol");
                return false;
            }
            var sslKey = (string) node[CONF_SSL_KEY];
            if (!string.IsNullOrEmpty(sslKey))
            {
                if (sslKey[0] != Path.DirectorySeparatorChar && sslKey[0] != '.')
                {
                    sslKey = baseFolder + sslKey;
                }
                var temp = sslKey.NormalizePath();
                if (string.IsNullOrEmpty(temp))
                {
                    FATAL("SSL key not found");
                    return false;
                }
                sslKey = temp;
            }
            node[CONF_SSL_KEY] = sslKey;
            var sslCert = (string)node[CONF_SSL_CERT];
            if (!string.IsNullOrEmpty(sslCert))
            {
                if (sslCert[0] != Path.DirectorySeparatorChar && sslCert[0] != '.')
                {
                    sslCert = baseFolder + sslCert;
                }
                var temp = sslCert.NormalizePath();
                if (string.IsNullOrEmpty(temp))
                {
                    FATAL("SSL cert not found");
                    return false;
                }
                sslCert = temp;
            }
            node[CONF_SSL_CERT] = sslCert;
            if (((!string.IsNullOrEmpty(sslKey)) || (string.IsNullOrEmpty(sslCert))) && ((string.IsNullOrEmpty(sslKey)) || (!string.IsNullOrEmpty(sslCert)))) return true;
            FATAL("Invalid ssl key/cert");
            return false;
        }
        public void Dispose()
        {
            _modules.Select(x=>x.Value).AsParallel().ForAll(x=>x.Release());
            _modules.Clear();
        }

        public string GetServicesInfo()
        {
            return @"
+-----------------------------------------------------------------------------+
|                                                                     Services|
+---+---------------+-----+-------------------------+-------------------------+
| c |      ip       | port|   protocol stack name   |     application name    |
"+string.Join("", ClientApplicationManager.ApplicationById.Select(x => x.Value.GetServicesInfo()))+
@"+---+---------------+-----+-------------------------+-------------------------+
";
        }

        public bool ConfigModules() => _applications.Children.Values.All(ConfigModule);

        private bool ConfigModule(Variant node)
        {
            var module = new Module {config = node};
            if (_staticGetApplicationFunction != null)
            {
                module.getApplication = _staticGetApplicationFunction;
                module.getFactory = _staticGetFactoryFunction;
            }
            if (!module.Load())
            {
                FATAL("Unable to load module");
                return false;
            }
            _modules.Add(node[CONF_APPLICATION_NAME], module);

            return true;
        }

        public bool ConfigFactories() => _modules.All(x => x.Value.ConfigFactory());


        public bool ConfigAcceptors() => _modules.All(x => x.Value.BindAcceptors());

        public bool ConfigInstances() => true;

        public bool ConfigApplications() => _modules.All(x => x.Value.ConfigApplication());
    }
}
