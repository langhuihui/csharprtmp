using CSharpRTMP.Common;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomMetaField:BoxAtom
    {
        private string _stringData;
        private AtomDATA _atomDATA;

        public AtomMetaField(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public override void Read()
        {
            if (Size >= 8)
            {
                Document.MediaFile.SeekAhead(4);
                var type = Br.ReadUInt32();
                if (type == DATA)
                {
                    Document.MediaFile.SeekBehind(4);
                    _stringData = Br.ReadBytes((int)(Size - 8 - 4)).BytesToString();
                }
                else
                {
                    Document.MediaFile.SeekBehind(8);
                    base.Read();
                }
            }
            base.Read();
        }

        public override void AtomCreated(BaseAtom atom)
        {
            switch (atom.Type)
            {
                case DATA:
                    _atomDATA = (AtomDATA) atom;
                    return;
            }
        }

        public Variant Variant => _atomDATA.Variant;
    }
}