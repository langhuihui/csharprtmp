using System.Collections.Generic;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomILST:BoxAtom
    {
        private readonly List<AtomMetaField> _metaFields = new List<AtomMetaField>(); 
        public AtomILST(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public Variant Variant
        {
            get
            {
                var result = new Variant();
                foreach (var atomMetaField in _metaFields)
                {
                    result[atomMetaField.TypeString] = atomMetaField.Variant;
                }
                return result;
            }
        }
        public override void AtomCreated(BaseAtom atom)
        {
            switch (atom.Type)
            {
                case _NAM:
                case CPIL:
                case PGAP:
                case TMPO:
                case _TOO:
                case _ART1:
                case _ART2:
                case _PRT:
                case _ALB:
                case GNRE:
                case TRKN:
                case _DAY:
                case DISK:
                case _CMT:
                case COVR:
                case AART:
                case _WRT:
                case _GRP:
                case _LYR:
                case DESC:
                case TVSH:
                case TVEN:
                case TVSN:
                case TVES:
                case _DES:
                    _metaFields.Add((AtomMetaField ) atom);
                    break;
            }
        }
    }
}