using System;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomMVHD:VersionedAtom
    {
        private uint _creationTime;
        private uint _modificationTime;
        public uint TimeScale;
        public uint Duration;
        private uint _preferredRate;
        private uint _preferredVolume;
        private byte[] _reserved;
        //private byte[] _matrixStructure;
        private uint _previewTime;
        private uint _previewDuration;
        private uint _posterTime;
        private uint _selectionTime;
        private uint _selectionDuration;
        private uint _currentTime;
        public uint NextTrackId;
        public Matrix Matrix;
        public AtomMVHD(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomMVHD() : base(MVHD)
        {
            _creationTime = (uint)DateTime.Now.SecondsFrom1904();
            _modificationTime = (uint)DateTime.Now.SecondsFrom1904();
        }
        public override void ReadData()
        {
            _creationTime = Br.ReadUInt32();
            _modificationTime = Br.ReadUInt32();
            TimeScale = Br.ReadUInt32();
            Duration = Br.ReadUInt32();
            _preferredRate = Br.ReadUInt32();
            _preferredVolume = Br.ReadUInt32();
            _reserved = Br.ReadBytes(10);
            Matrix.FromByteBuffer(Br.BaseStream);
            //_matrixStructure = Br.ReadBytes(36);
            _previewTime = Br.ReadUInt32();
            _previewDuration = Br.ReadUInt32();
            _posterTime= Br.ReadUInt32();
            _selectionTime= Br.ReadUInt32();
            _selectionDuration= Br.ReadUInt32();
            _currentTime= Br.ReadUInt32();
            NextTrackId= Br.ReadUInt32();
        }
    }
}