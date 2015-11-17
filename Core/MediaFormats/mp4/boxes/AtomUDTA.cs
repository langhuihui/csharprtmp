using System.Collections.Generic;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomUDTA:BoxAtom
    {
        private AtomMETA _atomMeta;
        private List<AtomMetaField> _metaFields = new List<AtomMetaField>();
        public AtomUDTA(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public override void Read()
        {
            if (Parent != null && Parent.Type == MOOV)
                base.Read();
            else
                SkipRead(false);
        }

        public override void AtomCreated(BaseAtom atom)
        {
            switch (atom.Type)
            {
                case META:
                    _atomMeta = (AtomMETA)atom;
                    return;
                case NAME:
                case _ALB:
                case _ART1:
                case _ART2:
                case _PRT:
                case _CMT:
                case _CPY:
                case _DES:
                case _NAM:
                case _COM:
                case _GEN:
                    _metaFields.Add(atom as AtomMetaField);
                    return;
                default:
                    return;
            }
        }
    }
}