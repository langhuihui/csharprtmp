using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Rtmp;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class MessageNull : MessageBuffered
    {
        public MessageNull ():base(false)
        {
            
        }
    }
    public class MessageBuffered : Message
    {
        public AMF0Writer Writer;

        public MessageBuffered()
        {
           
        }
        public MessageBuffered(bool repeatable = true)
            : base(Utils.Rms.GetStream(), repeatable)
        {
            Writer = new AMF0Writer(_stream);
        }
        protected override uint Init(uint position)
        {
            _stream.Position = position;
            return (uint)_stream.GetAvaliableByteCounts();
        }
        public H2NBinaryWriter RawWriter { get { return Writer; } }
        public override void Recycle()
        {
            base.Recycle();
            GlobalPool<MessageBuffered>.RecycleObject(this);
        }
    }

    public class MessageUnbuffered : Message
    {
        public MessageUnbuffered()
        {
            throw new Exception();
        }
        private BinaryReader _readerAck;
        public MessageUnbuffered(MemoryStream stream,MemoryStream memAck = null) : base(stream, false)
        {
            _readerAck = new BinaryReader(memAck);
        }

        protected override uint Init(uint postion)
        {
            _stream.Position = postion;
            return (uint) _stream.GetAvaliableByteCounts();
        }

        public override void Recycle()
        {
            base.Recycle();
            GlobalPool<MessageUnbuffered>.RecycleObject(this);
        }
    }
    public abstract class Message:IDisposable,IRecyclable
    {
        public struct FragmentInfo
        {
            public uint Offset;
            public ulong Stage;

            public FragmentInfo(uint offset, ulong stage)
            {
                Offset = offset;
                Stage = stage;
                //Debug.WriteLine("new FragmentInfo:{0}",stage);
            }
        }

        public N2HBinaryReader Reader;
        protected readonly MemoryStream _stream;
        public bool Repeatable;
        public List<FragmentInfo> Fragments = new List<FragmentInfo>();

        protected Message()
        {
            
        }
        protected Message(MemoryStream stream, bool repeatable)
        {
            _stream = stream;
            Reader = new N2HBinaryReader(stream);
            Repeatable = repeatable;
        }
        
        protected abstract uint Init(uint position);
        public N2HBinaryReader GetReader(out uint size)
        {

            size = Init(Fragments.Count > 0 ? Fragments[0].Offset : 0);
            return Reader;
        }
        public N2HBinaryReader GetReader(uint fragment,out uint size)
        {
            size = Init(fragment);
            return Reader;
        }

        public N2HBinaryReader MemAck(out uint avaliable, out uint size)
        {
            var result = GetReader(out avaliable);
            size = avaliable;
            return result;
        }
        
        public void Dispose()
        {
            _stream.Dispose();
        }

        public virtual void Recycle()
        {
            _stream.SetLength(0);
        }
    }
}
