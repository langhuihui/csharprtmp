using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{

    public interface IBandWriter
    {
        void InitFlowWriter(FlowWriter flowWriter);
        void ResetFlowWriter(FlowWriter flowWriter);
        bool Failed();
        bool CanWriterFollowing(FlowWriter flowWriter);
        RtmfpWriter Writer { get; set; }
        H2NBinaryWriter WriteMessage(byte type, ushort length, FlowWriter flowWriter = null);
        void Flush(bool echoTime = false);
    }

    //public class BandWriterNull : IBandWriter
    //{
    //    private static readonly RtmfpWriter WriterNull = new RtmfpWriter(null);
    //    public void InitFlowWriter(FlowWriter flowWriter)
    //    {
            
    //    }

    //    public void ResetFlowWriter(FlowWriter flowWriter)
    //    {
            
    //    }

    //    public void Close()
    //    {
            
    //    }

    //    public bool Failed()
    //    {
    //        return true;
    //    }

    //    public bool CanWriterFollowing(FlowWriter flowWriter)
    //    {
    //        return false;
    //    }

    //    public RtmfpWriter Writer
    //    {
    //        get { return WriterNull; }
    //        set { }
    //    }

    //    public H2NBinaryWriter WriteMessage(byte type, ushort length, FlowWriter flowWriter)
    //    {
    //        return WriterNull;
    //    }

    //    public void Flush(bool echoTime = false)
    //    {
            
    //    }

    //}
}
