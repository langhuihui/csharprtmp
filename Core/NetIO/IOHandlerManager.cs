using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.NetIO
{
    
    public static class IOHandlerManager
    {
        public static readonly Dictionary<uint, IOHandler> ActiveIoHandler = new Dictionary<uint, IOHandler>();
        public static readonly ConcurrentBag<IOHandler> DeadIoHandler = new ConcurrentBag<IOHandler>();
        public static TimersManager TimersManager;
        private static bool _isShuttingDown;
        public static Stopwatch Stopwatch = new Stopwatch();
        public static void Initialize()
        {
            TimersManager = new TimersManager(ProcessTimer);
        }

        public static void Start()
        {
            _isShuttingDown = false;
        }
        public static SocketAsyncEventArgs CreateOrGetSocketAsyncEventArgs(this IOHandler ioHandler)
        {
            SocketAsyncEventArgs saea;
            if (!GlobalPool<SocketAsyncEventArgs>.GetObject(out saea))
                saea.Completed += saeCompleted;
            saea.UserToken = ioHandler;
            return saea;
        }
        
        static void saeCompleted(object sender, SocketAsyncEventArgs e)
        {
            var ioHandler = e.UserToken as IOHandler;
            if (ioHandler != null && !ioHandler.OnEvent(e))
            {
                EnqueueForDelete(ioHandler);
            }
        }

        public static bool EnableTimer(this IOHandler ioHandler, uint seconds)
        {
            var timerEvent = new TimerEvent {Id = ioHandler.Id,Period = seconds};
            TimersManager.AddTimer(timerEvent);
            return true;
        }

        public static bool DisableTimer(this IOHandler ioHandler)
        {
            TimersManager.RemoveTimer(ioHandler.Id);
            return true;
        }

        
        public static void SignalShutdown()
        {
            _isShuttingDown = true;
        }

        public static void ShutdownIOHandlers()
        {
            foreach (var ioHandler in ActiveIoHandler)
            {
                EnqueueForDelete(ioHandler.Value);
            }
          //  Parallel.ForEach(ActiveIoHandler, x => EnqueueForDelete(x.Value));
        }
        public static void Shutdown()
        {
            _isShuttingDown = false;
            if (ActiveIoHandler.Count != 0 || DeadIoHandler.Count != 0)
                Logger.FATAL("Incomplete shutdown!!!");
        }

        public static void RegisterIOHandler(this IOHandler pIoHandler)
        {
            if (ActiveIoHandler.ContainsKey(pIoHandler.Id))
                Logger.ASSERT("IOHandler already registered");
            ActiveIoHandler[pIoHandler.Id] = pIoHandler;
            Logger.Debug("Handlers count changed: {0}->{1}", ActiveIoHandler.Keys.Count - 1, ActiveIoHandler.Keys.Count);
        }

        public static void UnRegisterIOHandler(this IOHandler pIoHandler)
        {
            pIoHandler.WriteEnabled = false;
            pIoHandler.ReadEnabled = false;
            pIoHandler.AcceptEnabled = false;
            pIoHandler.DisableTimer();
            ActiveIoHandler.Remove(pIoHandler.Id);
            Logger.Debug("Handlers count changed: {0}->{1}", ActiveIoHandler.Keys.Count + 1, ActiveIoHandler.Keys.Count);
        }
        public static void EnqueueForDelete(IOHandler ioHandler)
        {
            ioHandler.WriteEnabled = false;
            ioHandler.ReadEnabled = false;
            ioHandler.AcceptEnabled = false;
            ioHandler.DisableTimer();
            ioHandler.Dispose();
            //DeadIoHandler.AddLast(ioHandler);
        }

        public static int DeleteDeadHandlers()
        {
            var count = 0;
            IOHandler deadHandler;
            while (DeadIoHandler.TryTake(out deadHandler))
            {
                deadHandler.Dispose();
                count++;
            }
            return count;
        }

        public static bool Pulse()
        {
            if (_isShuttingDown)return false;
            if (ActiveIoHandler.Count == 0) return true;

            TimersManager.TimeElapsed(Stopwatch.Elapsed);
            return true;
        }
        public static void ProcessTimer(TimerEvent timerEvent)
        {
            if (!ActiveIoHandler.ContainsKey(timerEvent.Id)) return;
            if (!ActiveIoHandler[timerEvent.Id].OnEvent(null))
            {
                EnqueueForDelete(ActiveIoHandler[timerEvent.Id]);
            }
        }
    }
}
