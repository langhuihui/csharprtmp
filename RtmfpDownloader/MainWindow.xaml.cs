using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using CSharpRTMP.Common;
using CSharpRTMP.Core;
using CSharpRTMP.Core.NetIO;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Rtmp;
using Microsoft.Win32;
using RtmfpDownloader.Properties;

namespace RtmfpDownloader
{
    public interface IDownload : INotifyPropertyChanged
    {
        DateTime StartTime { get; set; }
        string Url { get; set; }
        long GetSpeed();
        string SpeedStr { get; }
        string TotalDownloadStr { get; }
        string TimeSpent { get; }
        void Start(string url,string filePath);
        void Stop();
        void Restart();
        string Status { get; set; }
        string FilePath { get; set; }
        string Log { get; set; }
    }
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<IDownload> DownloadList = new ObservableCollection<IDownload>();
        public readonly static BaseClientApplication ClientApp = new BaseClientApplication(Variant.GetMap(new VariantMapHelper()
            {
                {Defines.CONF_APPLICATION_NAME, "download"}
            }))
        { Id = 2 };
        public MainWindow()
        {
            InitializeComponent();
            IOHandlerManager.Initialize();
            var protocolFactory = new DefaultProtocolFactory();
            protocolFactory.RegisterProtocolFactory();
            ClientApp.RegisterApplication();
            listBox.ItemsSource = DownloadList;
            var _speedTimer = new DispatcherTimer(TimeSpan.FromSeconds(1),DispatcherPriority.Normal, _speedTimer_Elapsed, Dispatcher);
            _speedTimer.Start();
            ClientApp.RegisterAppProtocolHandler(ProtocolTypes.PT_OUTBOUND_RTMP,new BaseRTMPAppProtocolHandler(Variant.Get()));
        }

        private void _speedTimer_Elapsed(object sender, EventArgs e)
        {
            var totalSpeed = DownloadList.Sum(x => x.GetSpeed());
            TotalSpeed.Text = totalSpeed > 1024*1024 ? (totalSpeed/1024.0/1024.0).ToString("F2") + "MB/s" : (totalSpeed/1024.0).ToString("F2") + "KB/s";
        }

        private void DownloadVideo(object sender, RoutedEventArgs e)
        {
            var url = textBox.Text;
            Settings.Default.LastURL = url;
            Settings.Default.Save();
            IDownload download = null;
            var dialog = new SaveFileDialog()
            {
                CheckFileExists = false,AddExtension = true,OverwritePrompt = true,CreatePrompt = false,CheckPathExists = false,DefaultExt = ".flv",
                Filter = "Flash 视频|*.flv" // Filter files by extension
            };
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }
            
            if (url.StartsWith("rtmp://"))
            {
                download = new RtmpDownload();
                download.Start(url, dialog.FileName);
            }
            else if (url.StartsWith("rtmfp://"))
            {
                download = new DownloadProtocol();
                download.Start(url,dialog.FileName);
            }
            
            Settings.Default.DowloadHistory.Add(url);
            Settings.Default.Save();
            DownloadList.Add(download);
            listBox.SelectedItem = download;
        }

        private void StopDownload(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button.DataContext as IDownload;
            if (item != null)
            {
                if ((string)button.Content == "停止")
                {
                    item.Stop();
                    button.Content = "重下";
                }
                else
                {
                    item.Restart();
                    button.Content = "停止";
                }
            }
            else
            {
                var needStop = listBox.SelectedItems.OfType<IDownload>().ToList();
                foreach (var downloadProtocol in needStop)
                {
                    downloadProtocol.Stop();
                    //DownloadList.Remove(downloadProtocol);
                }
            }
           
        }

        private void History(object sender, RoutedEventArgs e)
        {
            new HistoryWindow { MainWindow = this }.Show();
        }

        private void DownloadMulti(object sender, RoutedEventArgs e)
        {
            new MultipleDownloadWindow { MainWindow = this}.Show();
        }

        private void DeleteDownload(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button).DataContext as IDownload;
            item.Stop();
            DownloadList.Remove(item);
        }

        private void OpenPath(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button).DataContext as IDownload;
            Process.Start("explorer.exe", new FileInfo(item.FilePath).Directory.FullName);
        }

        private void SelectAll(object sender, RoutedEventArgs e)
        {
            listBox.SelectAll();
        }
    }
}
