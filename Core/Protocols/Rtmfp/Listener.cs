using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class StreamWriter : FlowWriter
    {
        private readonly byte _type;
        public QualityOfService QOS = new QualityOfService();
        public StreamWriter(byte type, string signature, IBandWriter band):base(signature,band)
        {
            _type = type;
        }

        public void Write(uint time, Stream data, bool unbuffered)
        {
            //Logger.INFO(_type == 0x09 ? "Video timestamp : {0}" : "Audio timestamp : {0}", time);
            if (unbuffered)
            {
                if (data.Position >= 5)
                {
                    data.Position -= 5;
                    var writer = new H2NBinaryWriter(data);
                    writer.Write(_type);
                    writer.Write(time);
                    WriteUnbufferedMessage(data as MemoryStream, data as MemoryStream);
                }
            }
            var _out = WriterRawMessage(true);
            _out.Write(_type);
            _out.Write(time);
            data.CopyDataTo(_out.BaseStream);
        }

        public bool Reseted;
        public Peer Client;
    }

    //public class AudioWriter : StreamWriter
    //{
    //    public AudioWriter(string signature, IBandWriter band)
    //        : base(0x08, signature, band)
    //    {
    //    }
    //}

    //public class VideoWriter : StreamWriter
    //{
    //    public VideoWriter(string signature, IBandWriter band)
    //        : base(0x09, signature, band)
    //    {
    //    }
    //}
    public class Listener:IDisposable
    {
        private readonly FlowWriter _writer;
        private StreamWriter _audioWriter;
        private StreamWriter _videoWriter;
        private uint _time;
        private uint _addingTime;
        private long _deltaTime= -1;
        private bool _firstVideo = true;
        private bool _firstAudio = true;
        private bool _firstKeyFrame;
        private readonly bool _unbuffered;
        private uint _boundId;
        public Publication Publication;
        public uint Id;
        public bool AudioSampleAccess;
        public bool VideoSampleAccess;
        public bool ReceiveAudio = true;
        public bool ReceiveVideo = true;

        public Listener(uint id, Publication publication, FlowWriter writer, bool unbuffered)
        {
            Id = id;
            Publication = publication;
            _writer = writer;
            _unbuffered = unbuffered;
        }

        public void Init(Peer client)
        {
            if (_audioWriter == null)
            {
                _audioWriter = _writer.NewStreamWriter(0x08);
                _audioWriter.Client = client;
            }else
                Logger.WARN("Listener {0} audio track has already been initialized",Id);
            if (_videoWriter == null)
            {
                _videoWriter = _writer.NewStreamWriter(0x09);
                _videoWriter.Client = client;
            }
            else
            {
                Logger.WARN("Listener {0} video track has already been initialized", Id);
            }
            WriteBounds();
        }
        public void Flush()
        {
            if (_audioWriter != null) _audioWriter.Flush();
            if (_videoWriter != null) _videoWriter.Flush();
            _writer.Flush(true);
        }

        private void WriteBounds()
        {
            if(_videoWriter!=null)WriteBound(_videoWriter);
            if(_audioWriter!=null)WriteBound(_audioWriter);
            WriteBound(_writer);
            _boundId++;
        }

        private void WriteBound(FlowWriter writer)
        {
            var data = writer.WriterRawMessage();
            data.Write((ushort)0x22);
            data.Write(_boundId);
            data.Write(3);
        }

        public void PushAudioPacket(uint time, N2HBinaryReader packet)
        {
            if (!ReceiveAudio)
            {
                _firstAudio = true;
                return;
            }
            if (_audioWriter == null)
            {
                Logger.FATAL("Listener {0} must be initialized before to be used", Id);
                return;
            }
            if (_audioWriter.Reseted)
            {
                _audioWriter.Reseted = false;
                WriteBounds();
            }
            time = ComputeTime(time);
            if (_firstAudio)
            {
                _firstAudio = false;
                var size = Publication.AudioCodecBuffer.GetAvaliableByteCounts();
                if (size > 0)
                    _audioWriter.Write(time, Publication.AudioCodecBuffer, false);
            }
            _audioWriter.Write(time, packet.BaseStream, _unbuffered);
        }
        public void PushVideoPacket(uint time, N2HBinaryReader packet)
        {
            if (!ReceiveVideo)
            {
                //_firstKeyFrame = false;
                _firstVideo = true;
                return;
            }
            if (_videoWriter == null)
            {
                Logger.FATAL("Listener {0} must be initialized before to be used", Id);
                return;
            }
            //var temp = packet.ReadByte();
            //packet.BaseStream.Position--;
            //if ((temp & 0xF0) == 0x10) _firstKeyFrame = true;
            //if (!_firstKeyFrame)
            //{
            //    _videoWriter.QOS.DroppedFrames++;
            //    return;
            //}
            if (_videoWriter.Reseted)
            {
                _videoWriter.Reseted = false;
                WriteBounds();
            }
            time = ComputeTime(time);
            if (_firstVideo)
            {
                _firstVideo = false;
                var size = Publication.VideoCodecBuffer.GetAvaliableByteCounts();
                if (size > 0)
                {
                    _videoWriter.Write(time, Publication.VideoCodecBuffer, false);
                }
            }
            _videoWriter.Write(time, packet.BaseStream, _unbuffered);
        }

        private uint ComputeTime(uint time)
        {
            if (_deltaTime < 0 || _deltaTime > time) _deltaTime = time;
            return _time = (uint) (time - _deltaTime + _addingTime);
        }

        public void PushDataPacket(string name, N2HBinaryReader packet)
        {
            if (_unbuffered)
            {
                var offset = name.Length + 9;
                if (packet.BaseStream.Position >= offset)
                {
                    packet.BaseStream.Position -= offset;
                    _writer.WriteUnbufferedMessage(packet.BaseStream as MemoryStream);
                    return;
                }
            }
            packet.BaseStream.CopyDataTo(_writer.WriteAMFPacket(name).BaseStream);
            //packet.BaseStream.CopyTo(_writer.WriteAMFPacket(name).BaseStream);
        }

        public void StartPublishing(string name)
        {
            _writer.WriteStatusResponse("Play.PublishNotify",name+" is now published");
            _firstKeyFrame = false;
        }

        public void StopPublishing(string name)
        {
            _writer.WriteStatusResponse("Play.UnpublishNotify", name + " is now published");
            _deltaTime = -1;
            _addingTime = _time;
            _audioWriter.QOS.Reset();
            _videoWriter.QOS.Reset();
        }


        public void Dispose()
        {
            if (_videoWriter != null) _videoWriter.Close();
            if(_audioWriter!=null)_audioWriter.Close();
        }
    }
  
}
