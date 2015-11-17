using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class Sample
    {
        public DateTime Time;
        public uint Received;
        public uint Lost;
        public uint Size;
        public long LatencyGradient;
        public Sample(DateTime time, uint received, uint lost, uint size, long latencyGradient)
        {
            Time = time;
            Received = received;
            Lost = lost;
            Size = size;
            LatencyGradient = latencyGradient;
        }
    }
    public class QualityOfService
    {
        public static QualityOfService QualityOfServiceNull = new QualityOfService();
        private bool _fullSample;
        private readonly List<Sample> _samples = new List<Sample>();
        private uint _preTime;
        private uint _size;
        private DateTime _reception = DateTime.Now;
        private long _latency;
        private long _latencyGradient;
        private uint _num;
        private uint _den;
        public uint Latency;
        public double LostRate;
        public double ByteRate;
        public double CongestionRate;
        public uint DroppedFrames;
        public void Add(uint time,uint received,uint lost,uint size,uint ping)
        {
            long latencyGradient = 0;
            if (_samples.Count > 0)
            {
                if (time >= _preTime)
                {
                    var delta = time - _preTime;
                    var deltaReal = (uint) (_reception.Elapsed());
                    latencyGradient = (long) deltaReal - delta;
                    _latency += latencyGradient;
                }
                else
                    Logger.WARN("QoS computing with a error time value ({0}) inferiors than precedent time ({1})", time,
                        _preTime);
            }
            else
                _latency = ping/2;
            Latency = (uint) (_latency < 0 ? 0 : _latency);
            _preTime = time;
            _num += lost;
            _den += (lost + received);
            _size += size;
            _latencyGradient += latencyGradient;
            foreach (var sample in _samples.ToArray())
            {
                if (sample.Time.IsElapsed(5000))
                {
                    _fullSample = true;
                    break;
                }
                _den -= (sample.Received + sample.Lost);
                _num -= sample.Lost;
                _size -= sample.Size;
                _latencyGradient -= sample.LatencyGradient;
                _samples.Remove(sample);
            }
            _reception = DateTime.Now;
            _samples.Add(new Sample(_reception,received,lost,size,latencyGradient));
            var elapsed = _fullSample?5000:(uint)(_samples[0].Time.Elapsed());
            ByteRate = 0;
            double congestion = 0;
            if (elapsed > 0)
            {
                ByteRate = _size/elapsed*1000;
                congestion = _latencyGradient/elapsed;
            }
            if (_den == 0) Logger.FATAL("Lost rate computing with a impossible null number of fragments received");
            else
            {
                LostRate = _num/_den;
                congestion += LostRate;
            }
            CongestionRate = congestion > 1 ? 1 : (congestion < -1 ? -1 : congestion);
        }

        public void Reset()
        {
            LostRate = 0;
            ByteRate = 0;
            CongestionRate = 0;
            Latency = 0;
            DroppedFrames = 0;
            _fullSample = false;
            _latencyGradient = _latency = 0;
            _size = _num = _den = _preTime = 0;
            _samples.Clear();
        }
    }
}
