using System.Collections.Generic;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public struct AVCCParameter
    {
        public ushort Size;
        public byte[] Data;
    }
    public class AtomAVCC:BaseAtom
    {
        private byte _configurationVersion;
        private byte _profile;
        private byte _profileCompatibility;
        private byte _level;
        private byte _naluLengthSize;
        private byte _seqCount;
        private List<AVCCParameter> _seqParameters = new List<AVCCParameter>();
        private byte _picCount;
        private List<AVCCParameter> _picParameters = new List<AVCCParameter>();

        public AtomAVCC(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public long ExtraDataStart => Start + 8;
        public long ExtraDataLength => Size - 8;

        public override string Hierarchy(int indent)
        {
            //todo Hierarchy
            throw new System.NotImplementedException();
        }

        public override void Read()
        {
            _configurationVersion = Br.ReadByte();
            _profile= Br.ReadByte();
            _profileCompatibility= Br.ReadByte();
            _level= Br.ReadByte();
            _naluLengthSize= Br.ReadByte();
            _naluLengthSize = (byte) (1 + (_naluLengthSize & 0x03));
            _seqCount= Br.ReadByte();
            _seqCount = (byte) (_seqCount & 0x1f);
            for (int i = 0; i < _seqCount; i++)
            {
                AVCCParameter parameter;
                parameter.Size = Br.ReadUInt16();
                parameter.Data = null;
                if (parameter.Size > 0)
                {
                    parameter.Data = Br.ReadBytes(parameter.Size);
                }
                _seqParameters.Add(parameter);
            }
            _picCount= Br.ReadByte();
            for (int i = 0; i < _seqCount; i++)
            {
                AVCCParameter parameter;
                parameter.Size = Br.ReadUInt16();
                parameter.Data = null;
                if (parameter.Size > 0)
                {
                    parameter.Data = Br.ReadBytes(parameter.Size);
                }
                _picParameters.Add(parameter);
            }
        }
    }
}