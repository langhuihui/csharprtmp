using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using CSharpRTMP.Common;
using LoggingExtensions.log4net;
using LoggingExtensions.Logging;
using RtmfpDownloader.Properties;

namespace RtmfpDownloader
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            //base.OnStartup(e);
            if(Settings.Default.DowloadHistory==null)
                Settings.Default.DowloadHistory = new StringCollection();
            Log.InitializeWith<Log4NetLog>();
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
           
                e.Handled = true;
                this.Log().Error(e.ToString, e.Exception);
            
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
             e.ExceptionObject.Log().Error(e.ExceptionObject.ToString);
        }
    }
}
