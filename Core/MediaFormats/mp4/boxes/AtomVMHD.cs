namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomVMHD:VersionedBoxAtom
    {
        private ushort _graphicsMode;
        private byte[] _opcolor;
        public AtomVMHD(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomVMHD() : base(VMHD)
        {
            
        }
        public override void ReadData()
        {
            _graphicsMode = Br.ReadUInt16();
            _opcolor = Br.ReadBytes(6);
        }
    }
}