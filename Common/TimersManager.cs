using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CSharpRTMP.Common
{
    public struct TimerEvent
    {
        public uint Period;
        public uint Id;
        public object UserData;
    }

    
    public class TimersManager
    {
        private uint _slotsCount;
        private readonly List<Dictionary<uint, TimerEvent>> _pSlots = new List<Dictionary<uint, TimerEvent>>();
        private readonly Dictionary<uint, uint> _periodsMap = new Dictionary<uint, uint>();
        private readonly List<uint> _periodsVector = new List<uint>();
        private int _lastTime;
        private int _currentSlotIndex;
        readonly Action<TimerEvent> _processTimerEvent;
        public TimersManager(Action<TimerEvent> timerEvent)
        {
            _processTimerEvent = timerEvent;
        }
        public void TimeElapsed(TimeSpan currentTime)
        {
            var delta = currentTime.Seconds - _lastTime;
            _lastTime = currentTime.Seconds;
            if (delta <= 0 || _slotsCount == 0)
                return;

            for (;delta>0;delta--, _currentSlotIndex++)
            {
                foreach (var item in _pSlots[(int) (_currentSlotIndex % _slotsCount)].Values)
                {
                    _processTimerEvent(item);
                }
            }
        }
        public void AddTimer(TimerEvent timerEvent)
        {
            UpdatePeriods(timerEvent.Period);
            int min = 999999999;
            uint startIndex = 0;

            for (var i = 0; i < _slotsCount; i++)
            {
                if (min > _pSlots[i].Count)
                {
                    startIndex = (uint)i;
                    min = _pSlots[i].Count;
                }
            }

            while (!_pSlots[(int) (startIndex % _slotsCount)].ContainsKey(timerEvent.Id))
            {
                _pSlots[(int) (startIndex % _slotsCount)][timerEvent.Id] = timerEvent;
                startIndex += timerEvent.Period;
            }
        }

        public void RemoveTimer(uint eventTimerId)
        {
            for (int i = 0; i < _slotsCount; i++)
            {
                if (_pSlots[i].ContainsKey(eventTimerId))
                {
                    _pSlots[i].Remove(eventTimerId);
                }
            }
        }
        void UpdatePeriods(uint period)
        {
            if (_periodsMap.ContainsKey(period)) return;
            _periodsMap[period] = period;
            _periodsVector.Add(period);

            uint newSlotsCount = LCM(_periodsVector, 0);
            if (newSlotsCount == 0)
                newSlotsCount = period;
            if (newSlotsCount == _slotsCount)return;
            if (_pSlots.Count < newSlotsCount)
            {
                var newcount = newSlotsCount - _pSlots.Count;
                _pSlots.AddRange(Enumerable.Range(0, (int) newcount).Select(x => new Dictionary<uint, TimerEvent>()));
            }
            else
            {
                _pSlots.RemoveRange((int) (_pSlots.Count-1-newSlotsCount),(int) newSlotsCount);
            }
            if (_slotsCount > 0)
            {
                for (var i = (int)_slotsCount; i < newSlotsCount; i++)
                {
                    _pSlots[i] = _pSlots[(int)(i % _slotsCount)];
                }
            }
            //_pSlots = pNewSlots;
            _slotsCount = newSlotsCount;
        }

        static uint GCD(uint a, uint b)
        {
            while (b != 0)
            {
                uint t = b;
                b = a % b;
                a = t;
            }
            return a;
        }

        static uint LCM(uint a, uint b)
        {
            if (a == 0 || b == 0) return 0;

            var result = a * b / GCD(a, b);
            LogExtensions.Log<TimersManager>().Info("a: {0}; b: {1}; r: {2}", a, b, result);
            return result;
        }

        static uint GCD(List<uint> numbers, uint startIndex)
        {
            return numbers.Count <= 1 || numbers.Count <= startIndex
                ? 0
                : GCD(numbers[(int) startIndex],
                    numbers.Count - startIndex > 2 ? GCD(numbers, startIndex + 1) : numbers[(int) (startIndex + 1)]);
        }

        static uint LCM(List<uint> numbers, uint startIndex)
        {
            return numbers.Count <= 1 || numbers.Count <= startIndex
                ? 0
                : LCM(numbers[(int) startIndex],
                    numbers.Count - startIndex > 2 ? LCM(numbers, startIndex + 1) : numbers[(int) (startIndex + 1)]);
        }
    }

}
