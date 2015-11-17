using System.Collections.Generic;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomDATA : BaseAtom
    {
        private uint _type;
        private uint _unknown;
        private string _dataString;
        private List<ushort> _dataUI16=new List<ushort>(); 
        public AtomDATA(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public override string Hierarchy(int indent)
        {//todo Hierarchy
            throw new System.NotImplementedException();
        }

        public override void Read()
        {
            _type = Br.ReadUInt32();
            _unknown = Br.ReadUInt32();
            switch (_type)
            {
                case 1:
                    _dataString = ReadString((int) (Size-8-8));
                    return;
                case 0:
                    var count = (Size - 8 - 8)/2;
                    for (int i = 0; i < count; i++)
                    {
                        _dataUI16.Add(Br.ReadUInt16());
                    }
                    return;
                case 21:
                    return;
            }
        }
        //todo Variant
        public Variant Variant=>new Variant();

    }
}