namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomNULL:BaseAtom
    {
        public AtomNULL(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public override bool IsIgnored => true;

        public override string Hierarchy(int indent)
        {
            return new string(' ', indent<<2)+"null";
        }

        public override void Read()
        {
            SkipRead(false);
        }
    }
}