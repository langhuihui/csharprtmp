using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    /// <summary>
    /// SampleAuxiliaryInformationOffsetsBox
    /// </summary>
    public class AtomSAIO : VersionedAtom
    {
        public long[] Offsets = new long[0];
        public string AuxInfoType;
        public string AuxInfoTypeParameter;
        public AtomSAIO(MP4Document document, uint type, long size, long start) : base(document, SAIO, size, start)
        {
        }
        
        public override void ReadData()
        {
            throw new NotImplementedException();
        }
    }
}
