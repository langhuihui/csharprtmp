namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class IgnoredAtom:BaseAtom
    {
        public IgnoredAtom(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public override string Hierarchy(int indent) => new string(' ',indent << 2) + TypeString;
        public override void Read()
        {
            SkipRead(Type != 0x736b6970 && Type != 0x66726565 && Type != 0x6d646174);
        }

        public override bool IsIgnored => true;
    }
}