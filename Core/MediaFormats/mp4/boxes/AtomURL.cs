using System.Text;

namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomURL:VersionedAtom
    {
        private string _location;
        public AtomURL(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public AtomURL() : base(URL)
        {
            
        }
        public override void ReadData()
        {
            _location = Encoding.ASCII.GetString(Br.ReadBytes((int) (Size - 12)));
        }
    }
}