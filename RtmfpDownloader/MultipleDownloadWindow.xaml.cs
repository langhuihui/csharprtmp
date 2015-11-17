using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using RtmfpDownloader.Properties;

namespace RtmfpDownloader
{
    /// <summary>
    /// MultipleDownloadWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MultipleDownloadWindow : Window
    {
        public MainWindow MainWindow;
        public MultipleDownloadWindow()
        {
            InitializeComponent();
        }

        private void StartDownload(object sender, RoutedEventArgs e)
        {
            var lines = textBox.Text.Split(new [] { "\r\n" } ,StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var url = line;
                IDownload download = null;
                if (url.StartsWith("rtmp://"))
                {
                    download = new RtmpDownload();
                    download.Start(url,null);
                }
                else if (url.StartsWith("rtmfp://"))
                {
                    download = new DownloadProtocol();
                    download.Start(url,null);
                }

                Settings.Default.DowloadHistory.Add(url);
                MainWindow.DownloadList.Add(download);
            }
            Settings.Default.Save();
            Close();
        }
    }
}
