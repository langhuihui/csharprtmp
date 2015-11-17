using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Common;
namespace CSharpRTMP.Core.Streaming
{
    public interface IStream : IDisposable
    {
        BaseProtocol GetProtocol();
        StreamCapabilities Capabilities { get; }
        void ReadyForSend();
        bool IsCompatibleWithType(ulong type);
        ulong Type { get; }
        string Name { get; }
        uint UniqueId { get; }
        bool SignalStop();
        bool SignalSeek(ref double absoluteTimestamp);
        bool SignalResume();
        bool SignalPause();
        bool SignalPlay(ref double absoluteTimestamp, ref double length);
        bool Play(double absoluteTimestamp, double length);
        bool Pause();
        bool Resume();
        bool Seek(double absoluteTimestamp);
        bool Stop();
        bool FeedData(Stream pData, uint dataLength, uint processedLength, uint totalLength,
            uint absoluteTimestamp, bool isAudio);

        void SendStreamMessage( BufferWithOffset buffer);
        bool IsEnqueueForDelete();
        void EnqueueForDelete();
        void GetStats(Variant info, uint namespaceId);
    }

    public abstract class BaseStream<T> :  IStream where T:BaseProtocol
    {
        private readonly StreamsManager _pStreamsManager;
        public T Protocol { get; }
        public string Name { get; }
        public ulong Type { get; }

        public readonly long CreationTimestamp;
        public uint UniqueId { get; }
        protected BaseStream() { Type = this.GetAttribute<StreamTypeAttribute>(false).First().Type; }
        protected BaseStream(T pProtocol, StreamsManager pStreamsManager, string name)
        {
            _pStreamsManager = pStreamsManager;
            UniqueId = _pStreamsManager.GenerateUniqueId();
            Protocol = pProtocol;
            Name = name;
            Type = GetType().GetCustomAttribute<StreamTypeAttribute>(true).Type;
            pStreamsManager.RegisterStream(this);
            CreationTimestamp = DateTime.Now.Ticks;
        }

        public BaseProtocol GetProtocol()
        {
            return Protocol;
        }
        public virtual void GetStats(Variant info, uint namespaceId)
        {
            info.Add("uniqueId", (((ulong)namespaceId) << 32) | UniqueId);
            info.Add("type", Type);
            info.Add("name", Name);
            info.Add("creationTimestamp", CreationTimestamp);
            long queryTimestamp = DateTime.Now.Ticks;

            info.Add("queryTimestamp", queryTimestamp);
            info.Add("upTime", queryTimestamp - CreationTimestamp);
        }
        public virtual void Dispose()
        {
            _pStreamsManager.UnRegisterStream(this);
        }
        public bool IsEnqueueForDelete()
        {
            return Protocol != null && Protocol.IsEnqueueForDelete;
        }

        public void EnqueueForDelete()
        {
            if (Protocol != null)
            {
                Protocol.EnqueueForDelete();
            }
            else
            {
                Dispose();
            }
        }

        public virtual StreamCapabilities Capabilities
        {
            get
            {
                Logger.ASSERT("Operation not supported");
                return null;
            }
        }

        public virtual bool SignalStop()
        {
            Logger.ASSERT("Operation not supported");
            return true;
        }

        public virtual void ReadyForSend()
        {
            Logger.ASSERT("Operation not supported");
        }

        public virtual bool SignalSeek(ref double absoluteTimestamp)
        {
            Logger.ASSERT("Operation not supported");
            return true;
        }

        public virtual bool SignalResume()
        {
            Logger.ASSERT("Operation not supported");
            return true;
        }

        public virtual bool SignalPause()
        {
            Logger.ASSERT("Operation not supported");
            return true;
        }

        public virtual bool SignalPlay(ref double absoluteTimestamp,ref double length)
        {
            Logger.ASSERT("Operation not supported");
            return true;
        }

        public virtual bool Play(double absoluteTimestamp, double length)
        {
            Logger.ASSERT("Operation not supported");
            return true;
        }

        public virtual bool Pause()
        {
            Logger.ASSERT("Operation not supported");
            return true;
        }

        public virtual bool Resume()
        {
            Logger.ASSERT("Operation not supported");
            return true;
        }

        public virtual bool Seek(double absoluteTimestamp)
        {
            Logger.ASSERT("Operation not supported");
            return true;
        }

        public virtual bool Stop()
        {
            Logger.ASSERT("Operation not supported");
            return true;
        }

        public virtual bool FeedData(Stream pData, uint dataLength, uint processedLength, uint totalLength,
            uint absoluteTimestamp, bool isAudio)
        {
            Logger.ASSERT("Operation not supported");
            return true;
        }
        public virtual void SendStreamMessage(BufferWithOffset buffer)
        {

        }
        public virtual bool IsCompatibleWithType(ulong type)
        {
            return
               GetType().GetCustomAttribute<StreamTypeAttribute>(true).Compat.Any(x => type.TagKindOf(x));
        }
    }
}
