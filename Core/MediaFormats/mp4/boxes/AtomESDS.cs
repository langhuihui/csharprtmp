using System;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomESDS:VersionedAtom
    {
        private ushort _MP4ESDescrTag_ID;
        private byte _MP4ESDescrTag_Priority;
        public long ExtraDataStart;
        public uint ExtraDataLength;
        private byte _MP4DecConfigDescrTag_ObjectTypeID;
        private byte _MP4DecConfigDescrTag_StreamType;
        private uint _MP4DecConfigDescrTag_BufferSizeDB;
        private uint _MP4DecConfigDescrTag_MaxBitRate;
        private uint _MP4DecConfigDescrTag_AvgBitRate;
        public const byte MP4ESDescrTag = 0x03;
        public const byte MP4DecConfigDescrTag = 0x04;
        public const byte MP4DecSpecificDescrTag = 0x05;
        public const byte MP4UnknownTag = 0x06;
        public AtomESDS(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public override void ReadData()
        {
            byte tagType =0;
            uint length=0;
            Action ReadTagAndLength = () =>
            {
                tagType = Br.ReadByte();
                length = Br.ReadUInt32();
            };
            ReadTagAndLength();
            _MP4ESDescrTag_ID = Br.ReadUInt16();
            if (tagType == MP4ESDescrTag)
            {
                _MP4ESDescrTag_Priority = Br.ReadByte();
            }
            ReadTagAndLength();
            if (tagType == MP4DecConfigDescrTag)
            {
                _MP4DecConfigDescrTag_ObjectTypeID = Br.ReadByte();
                _MP4DecConfigDescrTag_StreamType= Br.ReadByte();
                _MP4DecConfigDescrTag_BufferSizeDB = Br.ReadU24();
                _MP4DecConfigDescrTag_MaxBitRate = Br.ReadUInt32();
                _MP4DecConfigDescrTag_AvgBitRate = Br.ReadUInt32();
                ReadTagAndLength();
                if (tagType == MP4UnknownTag)
                {
                    if (tagType == MP4DecSpecificDescrTag)
                    {
                        //iso14496-3
                        //http://wiki.multimedia.cx/index.php?title=MPEG-4_Audio
                        ExtraDataStart = Br.BaseStream.Position;
                        ExtraDataLength = length;
                        SkipRead(false);
                    }
                }
            }
        }
    }
}