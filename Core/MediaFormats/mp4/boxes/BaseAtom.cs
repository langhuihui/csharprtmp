using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public interface IBoxContainer
    {
        List<BaseAtom> SubAtoms { get; }
        IBoxContainer Parent { get; set; }
        uint Type { get; }
        void AddAtom(BaseAtom atom);
    }

    public abstract class BaseAtom
    {
        public const uint NULL = 0x00000000;
        public const uint FTYP = 0x66747970;
        public const uint MOOV = 0x6d6f6f76;
        public const uint MOOF = 0x6d6f6f66;
        public const uint MVHD = 0x6d766864;
        public const uint MFHD = 0x6d666864;
        public const uint MVEX = 0x6d766578;
        public const uint TRAK = 0x7472616b;
        public const uint TRAF = 0x74726166;
        public const uint TREX = 0x74726578;
        public const uint TRUN = 0x7472756e;
        public const uint TKHD = 0x746b6864;
        public const uint TFHD = 0x74666864;
        public const uint MDIA = 0x6d646961;
        public const uint MDHD = 0x6d646864;
        public const uint MDAT = 0x6d646174;
        public const uint HDLR = 0x68646c72;
        public const uint MINF = 0x6d696e66;
        public const uint SMHD = 0x736d6864;
        public const uint DINF = 0x64696e66;
        public const uint STBL = 0x7374626c;
        public const uint VMHD = 0x766d6864;
        public const uint DREF = 0x64726566;
        public const uint SAIO = 0x7361696f;
        public const uint STSD = 0x73747364;
        public const uint STHD = 0x73746864;
        public const uint STTS = 0x73747473;
        public const uint SDTP = 0x73647470;
        public const uint STSC = 0x73747363;
        public const uint STSZ = 0x7374737a;
        public const uint STCO = 0x7374636f;
        public const uint SUBS = 0x73756273;
        public const uint SGPD = 0x73657064;
        public const uint SBGP = 0x73626570;
        public const uint CTTS = 0x63747473;
        public const uint STSS = 0x73747373;
        public const uint URL = 0x75726c20;
        public const uint MP4A = 0x6d703461;
        public const uint MP42 = 0x6d703432;
        public const uint MP3 = 0x2e6d7033; //.mp3
        public const uint AVC1 = 0x61766331;
        public const uint ESDS = 0x65736473;
        public const uint ELTS = 0x656c7473;
        public const uint EDTS = 0x65647473;
        public const uint VIDE = 0x76696465;
        public const uint SOUN = 0x736f756e;
        public const uint AVCC = 0x61766343;
        public const uint UDTA = 0x75647461;
        public const uint WAVE = 0x77617665;
        public const uint CHAN = 0x6368616e;
        public const uint META = 0x6d657461;
        public const uint ILST = 0x696c7374;
        public const uint ISOM = 0x69736f6d;
        public const uint _NAM = 0xa96e616d;
        public const uint CPIL = 0x6370696c;
        public const uint PGAP = 0x70676170;
        public const uint TMPO = 0x746d706f;
        public const uint _TOO = 0xa9746f6f;
        public const uint _ART1 = 0xa9415254;
        public const uint _ART2 = 0xa9617274;
        public const uint _PRT = 0xa9707274;
        public const uint _ALB = 0xa9616c62;
        public const uint GNRE = 0x676e7265;
        public const uint TRKN = 0x74726b6e;
        public const uint _DAY = 0xa9646179;
        public const uint DISK = 0x6469736b;
        public const uint _CMT = 0xa9636d74;
        public const uint _CPY = 0xa9637079;
        public const uint _DES = 0xa9646573;
        public const uint DATA = 0x64617461;
        public const uint COVR = 0x636f7672;
        public const uint AART = 0x61415254;
        public const uint _WRT = 0xa9777274;
        public const uint _GRP = 0xa9677270;
        public const uint _LYR = 0xa96c7972;
        public const uint NAME = 0x6e616d65;
        public const uint NMHD = 0x6e6d6864;
        public const uint HMHD = 0x686d6864;
        public const uint _COM = 0xa9636f6d;
        public const uint _GEN = 0xa967656e;
        public const uint DESC = 0x64657363;
        public const uint TVSH = 0x74767368;
        public const uint TVEN = 0x7476656e;
        public const uint TVSN = 0x7476736e;
        public const uint TVES = 0x74766573;
        public const uint CO64 = 0x636f3634;
        public long Start { get; }
        public virtual long Size { get; set; }
        public uint Type { get; }
        public IBoxContainer Parent { get; set; }
        public MP4Document Document;

        public IsoTypeReader Br => Document.Reader;
        public IsoTypeWriter Wr => Document.Writer;
        public uint CurrentPosition => (uint) Document.MediaFile.Position;
        public virtual bool IsIgnored => false;

        public string TypeString
        {
            get
            {
                var c1 = (char) (Type >> 24);
                var c2 = (char) (Type >> 16);
                var c3 = (char) (Type >> 8);
                var c4 = (char) (Type);
                return new string(new[] {c1, c2, c3, c4});
            }
        }

        protected BaseAtom(MP4Document document, uint type, long size, long start)
        {
            Start = start;
            Size = size;
            Type = type;
            document.AddAtom(this);
            Document = document;
        }

        protected BaseAtom(uint type)
        {
            Type = type;
            Size = -1;
        }

        public abstract string Hierarchy(int indent);
        public abstract void Read();
        private bool IsSmallBox => Size + 8 < (1L << 32);

        public virtual void Write()
        {
            if (IsSmallBox)
            {
                Wr.Write((uint)GetSize());
                Wr.Write(Type);
            }
            else
            {
                Wr.Write(1);
                Wr.Write(Type);
                Wr.Write(GetSize());
            }
            //if (UserBox.TYPE.equals(getType()))
            //{
            //    byteBuffer.put(getUserType());
            //}
        }
        public long GetSize()
        {
            long size = Size;
            size += (8 + // size|type
                     (size >= ((1L << 32) - 8) ? 8 : 0));
            return size;
        }
        public virtual IEnumerable<BaseAtom> GetPath(List<uint> path) => null;
        public IEnumerable<BaseAtom> GetPath(params uint[] path) => GetPath(path.ToList());

        protected bool SkipRead(bool issueWarn)
        {
            if (issueWarn)
            {

            }
            return Document.MediaFile.SeekTo(Start + Size);
        }

        public string ReadString(int l) => Encoding.ASCII.GetString(Br.ReadBytes(l));
    }

    public abstract class VersionedAtom : BaseAtom
    {
        public byte Version;
        public int Flags;

        protected VersionedAtom(MP4Document document, uint type, long size, long start)
            : base(document, type, size, start)
        {

        }

        protected VersionedAtom(uint type) : base(type)
        {

        }

        public abstract void ReadData();
      
        public override void Read()
        {
            Version = Br.ReadByte();
            Flags = Br.Read24();
            ReadData();
        }

        public override void Write()
        {
            Wr.Write(Version);
            Wr.Write24(Flags);
        }

        public override string Hierarchy(int indent) => new string(' ', indent << 2) + TypeString;
    }

    public abstract class BoxAtom : BaseAtom, IBoxContainer
    {
        public List<BaseAtom> SubAtoms { get; } = new List<BaseAtom>();

        protected BoxAtom(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        protected BoxAtom(uint type) : base(type)
        {

        }


        public void AddAtom(BaseAtom atom)
        {
            SubAtoms.Add(atom);
            atom.Parent = this;
            atom.Document = Document;
        }

        public virtual void AtomCreated(BaseAtom atom)
        {

        }

        public override void Read()
        {
            while (CurrentPosition != Start + Size)
            {
                var pAtom = Document.ReadAtom(this);
                if (!pAtom.IsIgnored) AtomCreated(pAtom);
                SubAtoms.Add(pAtom);
            }
        }

        public override void Write()
        {

        }

        public override string Hierarchy(int indent)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(' ', (int) (indent << 2));
            stringBuilder.AppendLine(TypeString);
            if (SubAtoms.Count == 0)
            {
                stringBuilder.Append(' ', (int) ((indent + 1) << 2));
                stringBuilder.Append("[empty]");
                return stringBuilder.ToString();
            }
            foreach (var baseAtom in SubAtoms)
            {
                stringBuilder.AppendLine(baseAtom.Hierarchy(indent + 1));
            }
            stringBuilder.Remove(stringBuilder.Length - 1, 1);
            return stringBuilder.ToString();
        }

        public override IEnumerable<BaseAtom> GetPath(List<uint> path)
        {
            if (path.Count == 0) return null;
            var search = path[0];
            path.RemoveAt(0);
            return
                SubAtoms.Where(baseAtom => baseAtom.Type == search)
                    .Select(baseAtom => path.Count == 0 ? baseAtom : baseAtom.GetPath(path).FirstOrDefault());
        }
    }

    public abstract class VersionedBoxAtom : BoxAtom
    {
        public byte Version;
        public byte[] Flags = new byte[3];

        protected VersionedBoxAtom(MP4Document document, uint type, long size, long start)
            : base(document, type, size, start)
        {
        }

        protected VersionedBoxAtom(uint type) : base(type)
        {

        }

        public abstract void ReadData();

        public override void Read()
        {
            Version = Br.ReadByte();
            Br.Read(Flags, 0, 3);
            ReadData();
            base.Read();
        }

    }

    public abstract class HeaderAtom : VersionedAtom
    {
        public ulong CreationTime;
        public ulong ModificationTime;
        public ulong Duration;
        protected HeaderAtom(uint type) : base(type)
        {
            
        }
        protected HeaderAtom(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
            
        }
    }
}
