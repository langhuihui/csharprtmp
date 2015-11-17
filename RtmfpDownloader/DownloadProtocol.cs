using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Threading;
using Core.Protocols.Rtmp;
using CSharpRTMP.Common;
using CSharpRTMP.Core;
using CSharpRTMP.Core.NetIO;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Rtmfp;
using CSharpRTMP.Core.Streaming;
using Microsoft.Win32;
using RtmfpDownloader.Annotations;
using Cookie = CSharpRTMP.Core.Protocols.Rtmfp.Cookie;

namespace RtmfpDownloader
{
    [ProtocolType(ProtocolTypes.PT_INBOUND_RTMFP)]
    [AllowFarTypes(ProtocolTypes.PT_UDP)]
    public class DownloadProtocol : OutboundRTMFPProtocol, IDownload
    {
        public string AppName;
        public string TcUrl;
        public string StreamName;
        public DownloadSession DownloadSession;
        public string Url { get; set; }
        public DateTime StartTime { get; set; }
        private long _lastTotalBytes;
        public long Speed;
        public long TotalDownload => DownloadSession?.TotalBytes ?? 0;
        public string SpeedStr => Speed>1024*1024?(Speed/1024.0/1024.0).ToString("F2") + "MB/s" :(Speed/1024.0).ToString("F2") + "KB/s";
        public string TotalDownloadStr => TotalDownload> 1024 * 1024 ? (TotalDownload/1024.0/1024.0).ToString("F2") + "MB": (TotalDownload / 1024.0).ToString("F2") + "KB";
        public string TimeSpent => (DateTime.Now - StartTime).ToString("hh\\:mm\\:ss");

        public string Status
        {
            get { return _status; }
            set { _status = value;OnPropertyChanged(); }
        }

        public string FilePath { get; set; }

        public string Log
        {
            get { return _log; }
            set { _log = value+"\r\n"; OnPropertyChanged(); }
        }

        private string _status;
        private string _log;

        public void Start(string url,string filePath)
        {
            Url = url;
            var uri = new Uri(url);
            var segs = uri.Segments.ToList();
            segs.RemoveAt(0);
            StreamName = segs.Last();
            segs.Remove(StreamName);
            AppName = string.Join("", segs);
            TcUrl = url.Remove(url.LastIndexOf(StreamName));
            StreamName = StreamName.TrimStart('/');
            
            FilePath = filePath;
            Session = DownloadSession = new DownloadSession(this);
            var app = new BaseClientApplication(Variant.GetMap(new VariantMapHelper
            {
                {Defines.CONF_APPLICATION_NAME, AppName}
            })) {Id = 1};
            Application = app;
            DownloadSession.Connect(url);
            Status = "正在连接";
            StartTime = DateTime.Now;
            OnPropertyChanged(nameof(TimeSpent));
        }

        public void Restart()
        {
            this.RegisterProtocol();
            var udpProtocol = new UDPProtocol { NearProtocol = this };
            UDPCarrier.Create("", 0, this);
            Session = DownloadSession = new DownloadSession(this);
            DownloadSession.Connect(Url);
            Status = "正在连接";
            StartTime = DateTime.Now;
            OnPropertyChanged(nameof(TimeSpent));
        }
        public void Stop()
        {
            DownloadSession.EnqueueForDelete();
            Dispose();
            Status = "已停止";
        }

        public long GetSpeed()
        {
            Speed = TotalDownload - _lastTotalBytes;
            _lastTotalBytes = TotalDownload;
            OnPropertyChanged(nameof(SpeedStr));
            OnPropertyChanged(nameof(TotalDownloadStr));
            if (Status == "正在下载") OnPropertyChanged(nameof(TimeSpent));
            DownloadSession.Manage();
            return Speed;
        }

        public override Session CreateSession(Peer peer, Cookie cookie)
        {
            var connection = new FlowWriter(FlowConnection.Signature,DownloadSession,0);
            var connectArgs = Variant.Get();
            connectArgs["app"] = AppName;
            connectArgs["tcUrl"] = TcUrl;
            connectArgs["objectEncoding"] = 3.0;
            connectArgs["flashVer"] = "WIN 17,0,0,134";
            connectArgs["fpad"] = false;
            connectArgs["capabilities"] = 235.0;
            connectArgs["audioCodecs"] = 3575.0;
            connectArgs["videoCodecs"] = 252.0;
            connectArgs["videoFunction"] = 1.0;
            connectArgs["swfUrl"] = Variant.Get();
            connectArgs["pageUrl"] = Variant.Get();
            connection.Connect(connectArgs, (f1, message) =>
            {
                Log += message[1]["code"];
                if (message[1]["code"] == "NetConnection.Connect.Success")
                {
                    //connection.SetPeerInfo(FarProtocol.IOHandler.Socket.LocalEndPoint as IPEndPoint);
                    connection.CreateStream((f2, o) =>
                    {
                        DownloadSession.PlayStream(StreamName, f2.Id, o[1],
                            new OutFileRTMPFLVStream(this, StreamsManager,
                                FilePath ?? Url.Substring(8).Replace('/', '_').Replace(':', '_') + ".flv", StreamName));
                        Status = "正在下载";
                        StartTime = DateTime.Now;
                    });
                }
                else
                {
                    Status = "连接失败";
                }
            });
            return DownloadSession;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
