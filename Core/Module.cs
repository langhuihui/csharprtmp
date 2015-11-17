using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CSharpRTMP.Core;
using CSharpRTMP.Core.NetIO;
using CSharpRTMP.Core.Protocols;
using Microsoft.CSharp;
using Newtonsoft.Json.Linq;
using static CSharpRTMP.Common.Defines;
using static CSharpRTMP.Common.Logger;

namespace CSharpRTMP.Common
{
    public class Module
    {
        public Variant config;
        public Func<Variant, BaseClientApplication> getApplication;
        public Func<Variant, BaseProtocolFactory> getFactory;
        BaseClientApplication pApplication;
        BaseProtocolFactory pFactory;
        //private AppDomain appDomain;
        Assembly libHandler;
        readonly List<IOHandler> acceptors = new List<IOHandler>();
        readonly CSharpCodeProvider objCSharpCodePrivoder = new CSharpCodeProvider();
        readonly CompilerParameters objCompilerParameters = new CompilerParameters { GenerateInMemory = true };
        public Module ()
        {
            objCompilerParameters.ReferencedAssemblies.Add("System.dll");
            objCompilerParameters.ReferencedAssemblies.Add("System.Core.dll");
            objCompilerParameters.ReferencedAssemblies.Add("Core.dll");
            objCompilerParameters.ReferencedAssemblies.Add("Common.dll");
            objCompilerParameters.ReferencedAssemblies.Add("Newtonsoft.Json.dll");
        }
        public void Release()
        {
            if (pFactory != null)
            {
                ProtocolFactoryManager.UnRegisterProtocolFactory(pFactory);
                pFactory = null;
            }
            //if (libHandler != null)
            //{
            //    AppDomain.Unload(appDomain);
            //}
        }

        public bool Load()
        {
            if (getApplication != null || LoadLibrary()) return true;
            FATAL("Unable to load module library");
            return false;
        }

        bool LoadLibrary()
        {
            string path = config[CONF_APPLICATION_LIBRARY];
            try
            {
                //appDomain = AppDomain.CreateDomain(path);
                var scripts = Directory.GetFiles(config[CONF_APPLICATION_DIRECTORY], "*.cs");
                if (scripts.Count() == 0)
                {
                    libHandler = Assembly.LoadFrom(path);
                }
                else
                {

                    var cr = objCSharpCodePrivoder.CompileAssemblyFromFile(objCompilerParameters, scripts);

                    if (!cr.Errors.HasErrors)
                    {
                        libHandler = cr.CompiledAssembly;
                    }
                    else
                    {
                        string strErrorMsg = cr.Errors.Count + " Errors:";

                        for (int x = 0; x < cr.Errors.Count; x++)
                        {

                            strErrorMsg = strErrorMsg + "/r/nLine: " +

                                          cr.Errors[x].Line + " - " +

                                          cr.Errors[x].ErrorText;

                        }
                        WARN(strErrorMsg);
                        libHandler = Assembly.LoadFrom(path);
                    }
                }
                //appDomain.Load(libHandler.GetName());
            }
            catch (Exception ex)
            {
                FATAL("Unable to open library {0} Error was: {1}",path,ex.Message);
                return false;
            }
            string functionName = config[CONF_APPLICATION_INIT_FACTORY_FUNCTION];
           
            var appType = libHandler.GetTypes().SingleOrDefault(y => y.BaseType == typeof (BaseClientApplication));
            if (appType == null)
            {
                FATAL("Unable to find class {0}", functionName);
                return false;
            }
            getApplication = x => (BaseClientApplication)Activator.CreateInstance(appType, new object[] { x });
            //functionName = config[Defines.CONF_APPLICATION_INIT_FACTORY_FUNCTION];
            var factory = libHandler.GetTypes().SingleOrDefault(x => x.BaseType == typeof(BaseProtocolFactory));
            if(factory!=null)
                getFactory = x => (BaseProtocolFactory)Activator.CreateInstance(factory, x);

            INFO("Module {0} loaded",path);
            return true;
        }

        public bool ConfigFactory()
        {
            if (getFactory == null) return true;
            pFactory = getFactory(config);
            if (pFactory == null) return true;
            if (pFactory.RegisterProtocolFactory())
            {
                INFO("Loaded factory from application {0}", config[CONF_APPLICATION_NAME]);
                return true;
            }
            FATAL("Unable to register factory exported by application {0}", config[CONF_APPLICATION_NAME]);
            return false;
        }

        public bool BindAcceptors()
        {
            if (config[CONF_ACCEPTORS] == null) return true;
            foreach (var item in config[CONF_ACCEPTORS].Children.Where(item => !BindAcceptor(item.Value)))
            {
                FATAL("Unable to configure acceptor:\n{0}", item.ToString());
                return false;
            }
            return true;
        }

        bool BindAcceptor(Variant node)
        {
            //1. Get the chain
           var chain = ProtocolFactoryManager.ResolveProtocolChain(node[CONF_PROTOCOL]);
            if (chain.Count == 0)
            {
                WARN("Invalid protocol chain: {0}",node[CONF_PROTOCOL]);
            }
            //2. Is it TCP or UDP based?
            if (chain[0] == ProtocolTypes.PT_TCP)
            {
                //3. This is a tcp acceptor. Instantiate it and start accepting connections
                var pAcceptor = new TCPAcceptor( node[CONF_IP], node[CONF_PORT], node, chain);
                if (!pAcceptor.Bind())
                {
                    FATAL("Unable to fire up acceptor from this config node:{0}",node.ToString());
                    return false;
                }
                acceptors.Add(pAcceptor);
                return true;
            }
            else if (chain[0] == ProtocolTypes.PT_UDP)
            {
                //4. Ok, this is an UDP acceptor. Because of that, we can instantiate
                //the full stack. Get the stack first
                var pProtocol = ProtocolFactoryManager.CreateProtocolChain(chain, node);
                if (pProtocol == null)
                {
                    FATAL("Unable to instantiate protocol stack {0}", node[CONF_PROTOCOL]);
                    return false;
                }
                //5. Create the carrier and bind it
                var pUDPCarrier = UDPCarrier.Create(node[CONF_IP], node[CONF_PORT], pProtocol);
                if (pUDPCarrier == null)
                {
                    FATAL("Unable to instantiate UDP carrier on {0}:{1}", node[CONF_IP],
                        node[CONF_PORT]);
                    pProtocol.EnqueueForDelete();
                    return false;
                }
                pUDPCarrier.Parameters = node;
                acceptors.Add(pUDPCarrier);
                return true;
            }
            else
            {
                FATAL("Invalid carrier type");
                return false;
            }
        }

        public bool ConfigApplication()
        {
            var path = config[CONF_APPLICATION_LIBRARY];
            if (getApplication == null)
            {
                WARN("Module {0} doesn't export any applications", path);
                return true;
            }
            pApplication = getApplication(config);
            pApplication.Id = ++BaseClientApplication._idGenerator;
            if (pApplication == null)
            {
                FATAL("Unable to load application {0}.",config[CONF_APPLICATION_NAME]);
                return false;
            }
            INFO("Application {0} instantiated",pApplication.Name);
            if (!pApplication.RegisterApplication())
            {
                FATAL("Unable to register application {0}",pApplication.Name);
                pApplication = null;
                return false;
            }
            if (!pApplication.Initialize())
            {
                FATAL("Unable to initialize the application:{0}",pApplication.Name);
                return false;
            }
            if (!pApplication.ParseAuthentication())
            {
                FATAL("Unable to parse authetication for application {0}",pApplication.Name);
                return false;
            }
            if (!pApplication.ActivateAcceptors(acceptors))
            {
                FATAL("Unable to activate acceptors for application {0}", pApplication.Name);
                return false;
            }
            return true;
        }
    };
}
