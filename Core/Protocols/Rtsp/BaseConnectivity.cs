namespace CSharpRTMP.Core.Protocols.Rtsp
{
    public class BaseConnectivity
    {
        public static uint ToRTPTS(double milliseconds, uint rate)
        {
            return (uint)((milliseconds / 1000.00) * rate);
        }
    }
}