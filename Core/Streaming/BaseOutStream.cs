using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Rtmp;

namespace CSharpRTMP.Core.Streaming
{
    public interface IOutStream : IStream
    {
        IInStream InStream { get; }
        bool Link(IInStream pInStream, bool reverseLink = true);
        bool UnLink();
        void SignalAttachedToInStream();
        void SignalDetachedFromInStream();
        void SignalStreamCompleted();
        bool IsLinked { get; }
    }
    public interface IOutNetStream : IOutStream
    {
        void SendPublishNotify();
        void SendUnpublishNotify();
    }

    public interface IOutFileStream : IOutStream
    {
        long TotalBytes { get; set; }
    }
    public abstract class BaseOutNetStream<T> : BaseOutStream<T> ,IOutNetStream where T:BaseProtocol
    {
        protected BaseOutNetStream(T pProtocol, StreamsManager pStreamsManager,string name) : base(pProtocol, pStreamsManager, name)
        {
            if (!Type.TagKindOf(StreamTypes.ST_OUT_NET))
            {
                Logger.ASSERT("Incorrect stream type. Wanted a stream type in class {0} and got {1}", StreamTypes.ST_OUT_NET.TagToString(), Type.TagToString());
            }
        }
        public virtual void SendPublishNotify()
        {
            
        }

        public virtual void SendUnpublishNotify()
        {
            
        }
    }
    public abstract class BaseOutFileStream<T> : BaseOutStream<T>, IOutFileStream where T : BaseProtocol
    {
        public string FilePath;
        public long TotalBytes { get; set; }
        protected BaseOutFileStream(T pProtocol, StreamsManager pStreamsManager,string filePath, string name)
            : base(pProtocol, pStreamsManager, name)
        {
            FilePath = filePath;
            if (!Type.TagKindOf(StreamTypes.ST_OUT_FILE))
            {
                Logger.ASSERT("Incorrect stream type. Wanted a stream type in class {0} and got {1}", StreamTypes.ST_OUT_FILE.TagToString(), Type.TagToString());
            }
        }
    }
    public abstract class BaseOutStream<T>:BaseStream<T>,IOutStream where T:BaseProtocol
    {
        public IInStream InStream { get; private set; }

        protected BaseOutStream(T pProtocol, StreamsManager pStreamsManager,  string name) : base(pProtocol, pStreamsManager, name)
        {
            if (!Type.TagKindOf(StreamTypes.ST_OUT))
            {
                Logger.ASSERT("Incorrect stream type. Wanted a stream type in class {0} and got {1}", StreamTypes.ST_OUT.TagToString(), Type.TagToString());
            }
            
        }

        public override StreamCapabilities Capabilities => InStream.Capabilities;

        public override void Dispose()
        {
            base.Dispose();
            InStream?.UnLink(this);
            InStream = null;
        }

        public override void ReadyForSend() => InStream?.ReadyForSend();

        public virtual bool Link(IInStream pInStream, bool reverseLink=true)
        {
            if (!pInStream.IsCompatibleWithType(Type) || !IsCompatibleWithType(pInStream.Type))
            {
                Logger.FATAL("stream type {0} not compatible with stream type {1}",Type.TagToString(),pInStream.Type.TagToString());
                return false;
            }
            if (InStream != null)
            {
                if (InStream.UniqueId == pInStream.UniqueId)
                {
                    Logger.WARN("BaseOutStream::Link: This stream is already linked");
                    return true;
                }
                Logger.FATAL("BaseOutStream::Link: This stream is already linked to stream with unique id {0}",InStream.UniqueId);
                return false;
            }
            InStream = pInStream;
            if (reverseLink)
            {
                if (!InStream.Link(this, false))
                {
                    Logger.FATAL("BaseOutStream::Link: Unable to reverse link");
                    InStream = null;
                    return false;
                }
            }
            SignalAttachedToInStream();
            return true;
        }

        public bool UnLink()
        {
            if (InStream == null)
            {
                Logger.WARN("BaseOutStream::UnLink: This stream is not linked");
                return true;
            }
            InStream = null;
            SignalDetachedFromInStream();
            return true;
        }

        public bool IsLinked => InStream != null;

        public override void GetStats(Variant info, uint namespaceId)
        {
            base.GetStats(info,namespaceId);
            info["inStreamUniqueId"] = InStream != null ? Variant.Get((((ulong)namespaceId) << 32) | InStream.UniqueId) : Variant.Get();
            info.Add("bandwidth", Capabilities?.BandwidthHint ?? 0);
        }
        public override bool Play(double absoluteTimestamp, double length)
        {
            if (InStream == null || InStream.SignalPlay(ref absoluteTimestamp, ref length))
                return SignalPlay(ref absoluteTimestamp, ref length);
            Logger.FATAL("Unable to signal play");
            return false;
        }

        public override bool Pause()
        {
            if (InStream==null||InStream.SignalPause()) return SignalPause();
            Logger.FATAL("Unable to signal pause");
            return false;
        }

        public override bool Resume()
        {
            if (InStream == null || InStream.SignalResume()) return SignalResume();
            Logger.FATAL("Unable to signal resume");
            return false;
        }

        public override bool Seek(double absoluteTimestamp)
        {
            
            if (InStream != null)
            {
                lock (InStream)
                {
                    if (SignalSeek(ref absoluteTimestamp) && InStream.SignalSeek(ref absoluteTimestamp))
                        return true;
                }
            }
            else
            {
                if (SignalSeek(ref absoluteTimestamp)) return true;
            }

            Logger.FATAL("Unable to signal seek");
            return false;
        }
        public override bool Stop()
        {
            if (InStream == null || InStream.SignalStop()) return SignalStop();
            Logger.FATAL("Unable to signal stop");
            return false;
        }

        public virtual void SignalAttachedToInStream()
        {
        }

        public virtual void SignalDetachedFromInStream()
        {
        }

        public virtual void SignalStreamCompleted()
        {
            
        }
    }
}
