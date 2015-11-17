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
    /// HistoryWindow.xaml 的交互逻辑
    /// </summary>
    public partial class HistoryWindow : Window
    {
        public MainWindow MainWindow;
        public HistoryWindow()
        {
            InitializeComponent();
        }

        private void ClearAll(object sender, RoutedEventArgs e)
        {
            Settings.Default.DowloadHistory.Clear();
            Settings.Default.Save();
            listBox.ItemsSource = null;
            listBox.ItemsSource = Settings.Default.DowloadHistory;
        }

        private void ReDownload(object sender, RoutedEventArgs e)
        {
            foreach (var url in listBox.SelectedItems.OfType<string>().Distinct())
            {
                var download = new DownloadProtocol();
                download.Start(url,null);
                MainWindow.DownloadList.Add(download);
            }
            Settings.Default.Save();
            Close();
        }

        private void Delete(object sender, RoutedEventArgs e)
        {
            foreach (var source in listBox.SelectedItems.OfType<string>().ToList())
            {
                Settings.Default.DowloadHistory.Remove(source);
            }
            listBox.ItemsSource = Settings.Default.DowloadHistory;
            Settings.Default.Save();
        }
    }
}
