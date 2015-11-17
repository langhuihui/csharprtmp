using System.Collections.Generic;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    /// <summary>
    /// FileTypeBox
    /// </summary>
    public class AtomFTYP : BaseAtom
    {
        public List<uint> CompatibleBrands = new List<uint>();
        private uint _majorBrand;
        private uint _minorVersion;

        public AtomFTYP(MP4Document document, long size, long start) : base(document, FTYP, size, start)
        {
        }

        public override long Size =>base.Size==-1? 8 + CompatibleBrands.Count * 4:base.Size;

        public AtomFTYP(uint majorBrand, uint minorVersion, params uint[] brands) : base(FTYP)
        {
            _majorBrand = majorBrand;
            _minorVersion = minorVersion;
            CompatibleBrands.AddRange(brands);
        }

        public override void Write()
        {
            base.Write();
            Wr.Write(_majorBrand);
            Wr.Write(_minorVersion);
            foreach (var compatibleBrand in CompatibleBrands)
            {
                Wr.Write(compatibleBrand);
            }
        }

        public override string Hierarchy(int indent)
        {
            throw new System.NotImplementedException();
        }

        public override void Read()
        {
            _majorBrand = Br._ReadUInt32();
            _minorVersion = Br._ReadUInt32();
            for (int i = 16; i < Size; i += 4)
            {
                CompatibleBrands.Add(Br._ReadUInt32());
            }
        }
    }
}