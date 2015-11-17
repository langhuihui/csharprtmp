namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomAVC1:VersionedBoxAtom
    {
        private ushort _reserved;
        private ushort _referenceIndex;
        private ushort _qtVideoEncodingVersion;
        private ushort _qtVideoEncodingRevisionLevel;
        private uint _qtVideoEncodingVendor;
        private uint _qtVideoTemporalQuality;
        private uint _qtVideoSpatialQuality;
        private uint _videoFramePixelSize;
        private uint _horizontalDpi;
        private uint _verticalDpi;
        private uint _qtVideoDataSize;
        private ushort _videoFrameCount;
        private byte _videoEncoderNameLength;
        private string _videoEncoderName;
        private ushort _videoPixelDepth;
        private ushort _qtVideoColorTableId;
        private AtomAVCC _atomAVCC;

        public AtomAVC1(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public override void ReadData()
        {
            _reserved = Br.ReadUInt16();
            _referenceIndex = Br.ReadUInt16();
            _qtVideoEncodingVersion = Br.ReadUInt16();
            _qtVideoEncodingRevisionLevel= Br.ReadUInt16();
            _qtVideoEncodingVendor = Br.ReadUInt32();
            _qtVideoTemporalQuality = Br.ReadUInt32();
            _qtVideoSpatialQuality = Br.ReadUInt32();
            _videoFramePixelSize= Br.ReadUInt32();
            _horizontalDpi= Br.ReadUInt32();
            _verticalDpi= Br.ReadUInt32();
            _qtVideoDataSize= Br.ReadUInt32();
            _videoFrameCount= Br.ReadUInt16();
            _videoEncoderNameLength = Br.ReadByte();
            if (_videoEncoderNameLength < 31)
                _videoEncoderNameLength = 31;
            _videoEncoderName = ReadString(_videoEncoderNameLength);
            _videoPixelDepth = Br.ReadUInt16();
            _qtVideoColorTableId= Br.ReadUInt16();
        }

        public override void AtomCreated(BaseAtom atom)
        {
            switch (atom.Type)
            {
                case AVCC:
                    _atomAVCC = (AtomAVCC) atom;
                    return;
            }
        }
    }
}