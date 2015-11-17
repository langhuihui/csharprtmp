using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Core.Protocols.Rtmp;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Streaming;
using RtmfpDownloader.Annotations;

namespace RtmfpDownloader
{
    public class RtmpDownload:InboundRTMPProtocol,IDownload
    {
        public string StreamName;
        public string AppName;
        public string TcUrl;
        public event PropertyChangedEventHandler PropertyChanged;
        public long TotalDownload => OutFileStream?.TotalBytes ?? 0;
        public string Url { get; set; }
        public long GetSpeed()
        {
            Speed = TotalDownload - _lastTotalBytes;
            _lastTotalBytes = TotalDownload;
            OnPropertyChanged(nameof(SpeedStr));
            OnPropertyChanged(nameof(TotalDownloadStr));
            if (Status == "正在下载") OnPropertyChanged(nameof(TimeSpent));
            return Speed;
        }

        public IOutFileStream OutFileStream;
        private long _lastTotalBytes;
        public long Speed;
        public string SpeedStr => Speed > 1024 * 1024 ? (Speed / 1024.0 / 1024.0).ToString("F2") + "MB/s" : (Speed / 1024.0).ToString("F2") + "KB/s";
        public string TotalDownloadStr => TotalDownload > 1024 * 1024 ? (TotalDownload / 1024.0 / 1024.0).ToString("F2") + "MB" : (TotalDownload / 1024.0).ToString("F2") + "KB";
        public string TimeSpent => (DateTime.Now - StartTime).ToString("hh\\:mm\\:ss");
        public DateTime StartTime { get; set; }

        public string Status
        {
            get { return _status; }
            set { _status = value; OnPropertyChanged(); }
        }

        public string FilePath { get; set; }

        public string Log
        {
            get { return _log; }
            set { _log = value; OnPropertyChanged(); }
        }

        private string _status;
        private string _log;

        public void Start(string url,string filePath)
        {
            Url = url;
            FilePath = filePath;
            var uri = new Uri(url);
            var segs = uri.Segments.ToList();
            segs.RemoveAt(0);
            StreamName = segs.Last();
            segs.Remove(StreamName);
            AppName = string.Join("", segs);
            TcUrl = url.Remove(url.IndexOf(StreamName));
            StreamName = StreamName.TrimStart('/');
            MainWindow.ClientApp.PullExternalStream(Variant.GetMap(new VariantMapHelper()
                {
                    {"uri", url}, {"tcUrl",TcUrl}, {"localStreamName",StreamName}
                }));
            Application = MainWindow.ClientApp;
            OutFileStream = Application.StreamsManager.CreateOutFileStream(this, StreamName, filePath,false);
            StartTime = DateTime.Now;
            Status = "正在下载";

        }
        public void Restart()
        {
            MainWindow.ClientApp.PullExternalStream(Variant.GetMap(new VariantMapHelper()
                {
                    {"uri", Url}, {"tcUrl",TcUrl}, {"localStreamName",StreamName}
                }));
            OutFileStream = Application.StreamsManager.CreateOutFileStream(this, StreamName, FilePath, false);
            StartTime = DateTime.Now;
            Status = "正在下载";
        }
       

        public void Stop()
        {
            //Dispose();
            OutFileStream.InStream.EnqueueForDelete();
            OutFileStream.Dispose();
            Status = "已停止";
        }
        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
