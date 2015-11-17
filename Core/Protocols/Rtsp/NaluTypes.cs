using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRTMP.Core.Protocols.Rtsp
{
    public enum NaluType
    {
        //iso14496-10.pdf Page 60/280
        //Table 7-1 – NAL unit type codes
 NALU_TYPE_UNDEFINED     ,
 NALU_TYPE_SLICE         ,
 NALU_TYPE_DPA           ,
 NALU_TYPE_DPB           ,
 NALU_TYPE_DPC           ,
 NALU_TYPE_IDR           ,
 NALU_TYPE_SEI           ,
 NALU_TYPE_SPS           ,
 NALU_TYPE_PPS           ,
 NALU_TYPE_PD            ,
 NALU_TYPE_EOSEQ         ,
 NALU_TYPE_EOSTREAM      ,
 NALU_TYPE_FILL         ,
 NALU_TYPE_RESERVED13   ,
 NALU_TYPE_RESERVED14   ,
 NALU_TYPE_RESERVED15   ,
 NALU_TYPE_RESERVED16   ,
 NALU_TYPE_RESERVED17   ,
 NALU_TYPE_RESERVED18   ,
 NALU_TYPE_RESERVED19   ,
 NALU_TYPE_RESERVED20   ,
 NALU_TYPE_RESERVED21   ,
 NALU_TYPE_RESERVED22   ,
 NALU_TYPE_RESERVED23   ,

        //RFC3984 Page 11/82
        //Table 1.  Summary of NAL unit types and their payload structures
 NALU_TYPE_STAPA        ,
 NALU_TYPE_STAPB        ,
 NALU_TYPE_MTAP16       ,
 NALU_TYPE_MTAP24       ,
 NALU_TYPE_FUA          ,
 NALU_TYPE_FUB          ,
 NALU_TYPE_RESERVED30   ,
 NALU_TYPE_RESERVED31 
    }
}
