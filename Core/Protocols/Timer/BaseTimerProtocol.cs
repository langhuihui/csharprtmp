using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.NetIO;

namespace CSharpRTMP.Core.Protocols.Timer
{
    [ProtocolType(ProtocolTypes.PT_TIMER)]
    public class BaseTimerProtocol:BaseProtocol
    {
        private IOTimer _pTimer = new IOTimer();
        public BaseTimerProtocol()
        {
            _pTimer.Protocol = this;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (_pTimer != null)
            {
                _pTimer.Protocol = null;
                _pTimer.Dispose();
                _pTimer = null;
            }
        }
        public override IOHandler IOHandler
        {
            get { return _pTimer; }
            set
            {
                if (value != null && value.Type != IOHandlerType.IOHT_TIMER)
                    Logger.ASSERT("This protocol accepts only Timer carriers");
                _pTimer = (IOTimer)value;
            }
        }
        public bool EnqueueForTimeEvent(uint seconds)
        {
            if (_pTimer != null) return _pTimer.EnqueueForTimeEvent(seconds);
            Logger.ASSERT("BaseTimerProtocol has no timer");
            return false;
        }
    }
}
