using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.MediaFormats
{
    public enum MediaFrameType:byte
    {
        Audio, Video, Data, Message
    }
   [StructLayout(LayoutKind.Sequential)]
    
    public struct MediaFrame
    {
      
        public uint AbsoluteTime;
       
        public double DeltaTime;
       
        public uint Start;
      
        public uint Length;
      
        public int CompositionOffset;

        public MediaFrameType Type;
        [MarshalAs(UnmanagedType.I1)]
        public bool IsKeyFrame;
        [MarshalAs(UnmanagedType.I1)]
        public bool IsBinaryHeader;

       public override string ToString()
        {
            return $"{Start}:{Length}:{Type}:{DeltaTime}:{IsKeyFrame}:{AbsoluteTime}:{IsBinaryHeader}";
        }
        public static readonly int MediaFrameSize = Marshal.SizeOf(typeof(MediaFrame));
        public static bool ReadFromMediaFile(MediaFile file,out MediaFrame frame)
        {
            try
            {
                var bytes = new byte[MediaFrameSize];
                file.ReadBuffer(bytes);
                bytes.GetStruct(out frame);
            }
            catch (Exception ex)
            {
                frame = new MediaFrame();
                Logger.FATAL("cant't ReadFromMediaFile:{0}",ex);
                return false;
            }
            
            return true;
        }
    }
}
