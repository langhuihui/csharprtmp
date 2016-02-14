using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols;
using CSharpRTMP.Core.Protocols.Rtmfp;

namespace CSharpRTMP.Core.Streaming
{
    public interface IInStream : IStream
    {
        event Func<Stream, uint, uint, uint, uint, bool,bool> OnFeedData;
        HashSet<IOutStream> OutStreams { get; }
        bool Link(IOutStream pOutStream, bool reverseLink = true);
        bool UnLink(IOutStream pOutStream);
        void SignalOutStreamAttached(IOutStream pOutStream);
        void SignalOutStreamDetached(IOutStream pOutStream);
        uint ChunkSize { get; set; }
    }

    public interface IInNetStream : IInStream
    {
        
    }
    public abstract class BaseInNetStream<T> : BaseInStream<T>, IInNetStream where T : BaseProtocol
    {
        public List<byte[]> StreamMessageBuffer = new List<byte[]>();
        protected BaseInNetStream(T pProtocol, StreamsManager pStreamsManager,string name) 
            : base(pProtocol, pStreamsManager, name)
        {
            if (!Type.TagKindOf(StreamTypes.ST_IN_NET))
            {
                Logger.ASSERT("Incorrect stream type. Wanted a stream type in class {0} and got {1}", StreamTypes.ST_IN_NET.TagToString(), Type.TagToString());
            }
        }

        public override void SendStreamMessage(BufferWithOffset buffer)
        {
            base.SendStreamMessage(buffer);
            StreamMessageBuffer.Add(buffer);
        }

        public override void Dispose()
        {
            base.Dispose();
            StreamMessageBuffer.Clear();
        }

        public override void SignalOutStreamAttached(IOutStream pOutStream)
        {
            base.SignalOutStreamAttached(pOutStream);
            foreach (var buffer in StreamMessageBuffer)
            {
                pOutStream.SendStreamMessage(buffer);
            }
        }
    }
    
    public abstract class BaseInStream<T>:BaseStream<T>,IInStream where T :BaseProtocol
    {
        
        public event Func<Stream, uint, uint, uint, uint, bool, bool> OnFeedData;
        public event Action<BufferWithOffset> OnSendStreamMessage;
        public event Action OnFlush;
        public HashSet<IOutStream> OutStreams { get; protected set; }
        public virtual uint ChunkSize { get; set; }
        protected BaseInStream(T pProtocol, StreamsManager pStreamsManager, string name)
            : base(pProtocol, pStreamsManager, name)
        {
            OutStreams = new HashSet<IOutStream>();
        }
        public void Flush() => OnFlush?.Invoke();

        public override bool FeedData(Stream pData, uint dataLength, uint processedLength, uint totalLength, uint absoluteTimestamp,
            bool isAudio)
        {
            OnFeedData?.Invoke(pData, dataLength, processedLength, totalLength, absoluteTimestamp, isAudio);
            return true;
        }

        public override void SendStreamMessage(BufferWithOffset buffer) => OnSendStreamMessage?.Invoke(buffer);

        public override void Dispose()
        {
            base.Dispose();
            
            foreach (var outStream in OutStreams)
            {
                OnFeedData -= outStream.FeedData;
                OnSendStreamMessage -= outStream.SendStreamMessage;
                outStream.UnLink();
            }
            OutStreams.Clear();
        }

        public bool Link(IOutStream pOutStream, bool reverseLink = true)
        {
            if (!pOutStream.IsCompatibleWithType(Type) || !IsCompatibleWithType(pOutStream.Type))
            {
                Logger.FATAL("stream type {0} not compatible with stream type {1}",Type.TagToString(),pOutStream.Type.TagToString());
                return false;
            }
            if (OutStreams.Contains(pOutStream))
            {
                Logger.WARN("BaseInStream::Link: This stream is already linked");
                return true;
            }
            OutStreams.Add(pOutStream);
            if (reverseLink)
            {
                if (!pOutStream.Link(this, false))
                {
                    Logger.FATAL("BaseInStream::Link: Unable to reverse link");
                    //NYIA;
                    return false;
                }
            }
            SignalOutStreamAttached(pOutStream);
            
            return true;
        }

        public virtual void SignalOutStreamAttached(IOutStream pOutStream)
        {
            OnFeedData += pOutStream.FeedData;
            OnSendStreamMessage += pOutStream.SendStreamMessage;
            
            var stream = pOutStream as OutNetRtmfpStream;
            if (stream != null) OnFlush += stream.Flush;
        }

        public bool UnLink(IOutStream pOutStream)
        {
            OutStreams.Remove(pOutStream);
            OnFeedData -= pOutStream.FeedData;
            OnSendStreamMessage -= pOutStream.SendStreamMessage;
            SignalOutStreamDetached(pOutStream);
            return true;
        }

        public virtual void SignalOutStreamDetached(IOutStream pOutStream)
        {
            var stream = pOutStream as OutNetRtmfpStream;
            if (stream != null) OnFlush -= stream.Flush;
        }

        public override bool Play(double absoluteTimestamp, double length)
        {
            
            if (!SignalPlay(ref absoluteTimestamp, ref length))
            {
                Logger.FATAL("Unable to signal play");
                return false;
            }

#if PARALLEL
            OutStreams.AsParallel().ForAll(x =>
            {
                if (!x.InStream.SignalPlay(ref absoluteTimestamp, ref length))
                    Logger.WARN("Unable to signal play on an outbound stream");
            });
            
#else
            foreach (
                var baseOutStream in
                    OutStreams.Where(
                        baseOutStream => !baseOutStream.SignalPlay(ref absoluteTimestamp, ref length)))
                Logger.WARN("Unable to signal play on an outbound stream");
#endif
            return true;
        }

        public override bool Pause()
        {
            if (!SignalPause())
            {
                Logger.FATAL("Unable to signal pause");
                return false;
            }
#if PARALLEL
            OutStreams.AsParallel().ForAll(x =>
            {
                if (!x.InStream.SignalPause())
                    Logger.WARN("Unable to signal pause on an outbound stream");
            });

#else
            foreach (
                var baseOutStream in
                    OutStreams.Where(
                        baseOutStream => !baseOutStream.SignalPause()))
                Logger.WARN("Unable to signal pause on an outbound stream");
#endif
            return true;
        }

        public override bool Resume()
        {
            if (!SignalResume())
            {
                Logger.FATAL("Unable to signal resume");
                return false;
            }
#if PARALLEL
            OutStreams.AsParallel().ForAll(x =>
            {
                if (!x.InStream.SignalResume())
                    Logger.WARN("Unable to signal resume on an outbound stream");
            });

#else
            foreach (
                var baseOutStream in
                    OutStreams.Where(
                        baseOutStream => !baseOutStream.SignalResume()))
                Logger.WARN("Unable to signal resume on an outbound stream");
#endif
            return true;
        }

        public override bool Seek(double absoluteTimestamp)
        {
#if PARALLEL
            OutStreams.AsParallel().ForAll(x =>
            {
                if (!x.InStream.SignalSeek(ref absoluteTimestamp))
                    Logger.WARN("Unable to signal seek on an outbound stream");
            });
#else
            foreach (
                var baseOutStream in
                    OutStreams.Where(
                        baseOutStream => !baseOutStream.SignalSeek(ref absoluteTimestamp)))
                Logger.WARN("Unable to signal seek on an outbound stream");
#endif
            if (!SignalSeek(ref absoluteTimestamp))
            {
                Logger.FATAL("Unable to signal seek");
                return false;
            }
            return true;
        }

        public override bool Stop()
        {
            if (!SignalStop())
            {
                Logger.FATAL("Unable to signal stop");
                return false;
            }
#if PARALLEL
            OutStreams.AsParallel().ForAll(x =>
            {
                if (!x.InStream.SignalStop())
                    Logger.WARN("Unable to signal stop on an outbound stream");
            });

#else
            foreach (
                var baseOutStream in
                    OutStreams.Where(
                        baseOutStream => !baseOutStream.SignalStop()))
                Logger.WARN("Unable to signal stop on an outbound stream");
#endif
            return true;
        }

        public override void GetStats(Variant info, uint namespaceId)
        {
            base.GetStats(info, namespaceId);
            info["outStreamsUniqueIds"] = Variant.Get(OutStreams.Select(x => Variant.Get((((ulong)namespaceId) << 32) | x.UniqueId)).ToList());
            info.Add("bandwidth",Capabilities?.BandwidthHint ?? 0);
        }
    }
}
