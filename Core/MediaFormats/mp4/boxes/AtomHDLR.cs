using System;
using System.Text;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomHdlr:VersionedAtom
    {
        public string ComonentType;
        public uint ComponentSubType;
        public uint ComponentManufacturer;
        public uint ComponentFlags;
        public uint ComponentFlagsMask;
        public string ComponentName;

        public AtomHdlr(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomHdlr():base(HDLR)
        {
            
        }
        public override void ReadData()
        {
            ComonentType = new string(new [] {Br.ReadChar(),Br.ReadChar(),Br.ReadChar(),Br.ReadChar()});
            ComponentSubType = Br.ReadUInt32();
            ComponentManufacturer = Br.ReadUInt32();
            ComponentFlags = Br.ReadUInt32();
            ComponentFlagsMask = Br.ReadUInt32();
            ComponentName = Encoding.ASCII.GetString(Br.ReadBytes((int) (Size - 32)));
        }

        public override string Hierarchy(int indent) => base.Hierarchy(indent)+$"({ComponentSubType})";
    }
}