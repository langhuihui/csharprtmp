using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRTMP.Core.MediaFormats.mp4
{
    public interface IGroupEntry
    {
        string Type { get; }
        void Parse(byte[] bytes);
        byte[] ToBytes();
    }
}
