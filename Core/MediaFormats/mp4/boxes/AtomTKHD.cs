namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomTKHD: HeaderAtom
    {
        private byte[] _reserved1;
        private byte[] _reserved2;
        public ushort Layer;
        public ushort AlternateGroup;
        public float Volume;
        private byte[] _reserved3;
        public Matrix Matrix;

        public AtomTKHD(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomTKHD() : base(TKHD)
        {
            
        }

        public bool IsEnabled
        {
            set
            {
                if (value) Flags|= 1;
                else Flags &= ~1;
            }
            get { return (Flags & 1) > 0; }
        }
        public bool IsInMovie
        {
            set
            {
                if (value) Flags |= 2;
                else Flags &= ~2;
            }
            get { return (Flags & 2) > 0; }
        }

        public bool IsInPreview
        {
            set
            {
                if (value) Flags |= 4;
                else Flags &= ~4;
            }
            get { return (Flags & 4) > 0; }
        }
        public bool IsInPoster
        {
            set
            {
                if (value) Flags |= 8;
                else Flags &= ~8;
            }
            get { return (Flags & 8) > 0; }
        }

        public uint TrackId;
        public uint Width;
        public uint Height;
        public override void ReadData()
        {
            CreationTime = Br.ReadUInt32();
            ModificationTime = Br.ReadUInt32();
            TrackId = Br.ReadUInt32();
            _reserved1 = Br.ReadBytes(4);
            Duration = Br.ReadUInt32();
            _reserved2 = Br.ReadBytes(8);
            Layer = Br.ReadUInt16();
            AlternateGroup = Br.ReadUInt16();
            Volume = Br.ReadInt16();
            _reserved3 = Br.ReadBytes(2);
            Matrix=  Matrix.FromByteBuffer(Br.BaseStream);
            Width = Br.ReadUInt32();
            Height = Br.ReadUInt32();

        }
    }
}