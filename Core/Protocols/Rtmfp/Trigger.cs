using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Rtmp;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class Trigger
    {
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private sbyte _cycle = -1;
        private byte _time;
        public void Stop()
        {
            _stopwatch.Stop();
        }

        public void Reset()
        {
            _time = 0;
            _cycle = -1;
            _stopwatch.Restart();
        }

        public void Start()
        {
            if (_stopwatch.IsRunning) return;
            _time = 0;
            _cycle = -1;
            _stopwatch.Start();
        }

        public bool Raise()
        {
            if (!_stopwatch.IsRunning) return false;
            if (_time == 0 && _stopwatch.ElapsedMilliseconds < 2000) return false;
            _time++;
            if (_time >= _cycle)
            {
                _time = 0;
                _cycle++;
                if (_cycle == 7)
                {
                    throw new Exception("Repeat trigger failed");
                }
                Logger.Debug("Repeat trigger cycle {0}", _cycle + 1);
                return true;
            }
            return false;
        }
    }
}
