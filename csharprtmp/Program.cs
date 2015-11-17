using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Core.Protocols.Rtmp;
using CSharpRTMP.Common;
using CSharpRTMP.Core;
using CSharpRTMP.Core.NetIO;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Rtmfp;

namespace CSharpRTMP
{
    class MainClass
    {
        private static ConfigFile _configFile = new ConfigFile(null, null);
        private static DefaultProtocolFactory _protocolFactory;

        public static void Main(string[] args)
        {
            if (Initialize()) Run();
            Cleanup();
        }

        private static bool Initialize()
        {
            LoggingExtensions.Logging.Log.InitializeWith<LoggingExtensions.log4net.Log4NetLog>();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            
            if (!_configFile.LoadConfigFile())
            {
                Logger.FATAL("Unable to load ConfigFile!");
                return false;
            }

            Logger.INFO("Initialize I/O handlers manager");
            IOHandlerManager.Initialize();
            if (!_configFile.ConfigModules())
            {
                Logger.FATAL("Unable to configure modules");
                return false;
            }
            Logger.INFO("Plug in the default protocol factory");
            _protocolFactory = new DefaultProtocolFactory();
            if (!_protocolFactory.RegisterProtocolFactory())
            {
                Logger.FATAL("Unable to register default protocols factory");
                return false;
            }
            Logger.INFO("Configure factories");
            if (!_configFile.ConfigFactories())
            {
                Logger.FATAL("Unable to configure factories");
                return false;
            }
            Logger.INFO("Configure acceptors");
            if (!_configFile.ConfigAcceptors())
            {
                Logger.FATAL("Unable to configure acceptors");
                return false;
            }

            Logger.INFO("Configure instances");
            if (!_configFile.ConfigInstances())
            {
                Logger.FATAL("Unable to configure instances");
                return false;
            }

            Logger.INFO("Start I/O handlers manager");
            IOHandlerManager.Start();

            Logger.INFO("Configure applications");
            if (!_configFile.ConfigApplications())
            {
                Logger.FATAL("Unable to configure applications");
                return false;
            }

            //Logger.INFO("Install the quit signal");
            //installQuitSignal(QuitSignalHandler);

            return true;
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            if (exception != null)
            {
                while (exception.InnerException != null) exception = exception.InnerException;
                Logger.FATAL(exception.ToString());
            }
            else
                Logger.FATAL(e.ToString());
            
        }

        private static void Cleanup()
        {
            Logger.WARN("Shutting down protocols manager");
            ProtocolManager.Shutdown();
            ProtocolManager.CleanupDeadProtocols();

            Logger.WARN("Shutting down I/O handlers manager");
            IOHandlerManager.ShutdownIOHandlers();
            IOHandlerManager.DeleteDeadHandlers();
            IOHandlerManager.Shutdown();

            Logger.WARN("Unregister and delete default protocol handler");
            ProtocolFactoryManager.UnRegisterProtocolFactory(_protocolFactory);

            Logger.WARN("Shutting down applications");
            ClientApplicationManager.Shutdown();

            Logger.WARN("Delete the configuration");

            _configFile = null;

            //Logger.WARN("Doing final OpenSSL cleanup");
            //CleanupSSL();

            Logger.WARN("Shutting down the logger leaving you in the dark. Bye bye... :(");
            Console.ReadKey();
        }
        public static void Run()
        {
            Logger.INFO("\n{0}", _configFile.GetServicesInfo());
            Logger.INFO("GO! GO! GO! ({0})", Process.GetCurrentProcess().Id);
            IOHandlerManager.Stopwatch.Start();
            //new Action(ProtocolManager.Manage).BeginInvoke(null,null);
            while (IOHandlerManager.Pulse())
            {
               // IOHandlerManager.DeleteDeadHandlers();
                ProtocolManager.CleanupDeadProtocols();
                Thread.Sleep(100);
                //ProtocolManager.Manage();
            }
            
            IOHandlerManager.Stopwatch.Stop();
        }
    }
}
