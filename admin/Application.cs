using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Rtmfp;
using CSharpRTMP.Core.Protocols.Rtmp;

namespace admin
{
    [AppProtocolHandler(typeof(BaseRTMPAppProtocolHandler), ProtocolTypes.PT_INBOUND_RTMP)]
    [AppProtocolHandler(typeof(BaseRtmfpAppProtocolHandler), ProtocolTypes.PT_INBOUND_RTMFP)]
    public class Application:BaseClientApplication
    {
        private PerformanceCounter _oPerformanceCounter ;
        private readonly PerformanceCounterCategory _networkPerformanceCatagory = new PerformanceCounterCategory("Network Interface");
        private readonly PerformanceCounterCategory _dotnetMemoryPerformanceCatagory = new PerformanceCounterCategory(".NET CLR Memory");
        private  IEnumerable<PerformanceCounter> _bytesReceived;
        private  IEnumerable<PerformanceCounter> _bytesSent;
        private  IEnumerable<PerformanceCounter> _bytesTotal;
        private  PerformanceCounter _memoryPerformanceCounter;
        public Application(Variant configuration) : base(configuration)
        {
           
        }
        public override bool OnConnect(BaseProtocol pFrom, Variant param)
        {
            _oPerformanceCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            var p = Process.GetCurrentProcess();
            var allCounters = _networkPerformanceCatagory.GetInstanceNames()
                .SelectMany(name => _networkPerformanceCatagory.GetCounters(name)).ToList();
            _bytesReceived = allCounters.Where(counters => counters.CounterName == "Bytes Received/sec");
            _bytesTotal = allCounters.Where(counters => counters.CounterName == "Bytes Total/sec");
             _bytesSent= allCounters.Where(counters => counters.CounterName == "Bytes Sent/sec");
            _memoryPerformanceCounter =
                _dotnetMemoryPerformanceCatagory.GetCounters(
                    _dotnetMemoryPerformanceCatagory.GetInstanceNames()
                        .Single(x => x == p.ProcessName || x.StartsWith(p.Id.ToString())))
                    .Single(x => x.CounterName == "# Total committed Bytes");
            return true;
        }

        [CustomFunction("test")]
        public Variant Test(BaseProtocol pFrom, Variant invoke)
        {
            var result = new Variant();
            result["catagory"] = new Variant(PerformanceCounterCategory.GetCategories().Select(x=>new Variant(x.CategoryName)).ToList());
            return result;
        }
        [CustomFunction("getOverall")]
        public Variant GetOverall(BaseProtocol pFrom, Variant invoke)
        {
            var result = new Variant();
            result["serverMode"] = ClientApplicationManager.IsSlave?"slave":"master";
            result["appList"] = new Variant(ClientApplicationManager.ApplicationByName.Keys.Select(x=>new Variant(x)).ToList());
            result["memory"] = _memoryPerformanceCounter.NextValue();
            result["cpu"] = _oPerformanceCounter.NextValue();
            result["bytesSent"] = _bytesSent.Sum(x => x.NextValue());
            result["bytesReceived"] = _bytesReceived.Sum(x => x.NextValue());
            result["bytesTotal"] = _bytesTotal.Sum(x => x.NextValue());
            return result;
        }

        [CustomFunction("getAppInfo")]
        public Variant GetAppInfo(BaseProtocol pFrom, Variant invoke)
        {
            var appName = invoke[1];
            var app = ClientApplicationManager.ApplicationByName[appName];
            var result = new Variant();
            result["config"] = app.Configuration;
            result["servicesInfo"] = app.GetServicesInfo();
            result["connections"] = new Variant(ProtocolManager.ActiveProtocols.Values.Where(x => x.Application == app).Select(x => x.GetStackStats(new Variant(), Id)).ToList());
            result["streams"] = new Variant(StreamsManager.StreamsByUniqueId.Select(x =>
            {
                var info = new Variant();
                info["name"] = x.Value.Name + "#" + x.Value.UniqueId;
                x.Value.GetStats(info, Id);
                return info;
            }).ToList());
            //foreach (var baseAppProtocolHandler in app.AppProtocolHandlers)
            //{

            //}
            return result;
        }
    }
}
