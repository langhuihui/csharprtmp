using CSharpRTMP.Common;

namespace CSharpRTMP.Core.Protocols.Rtsp
{
    internal class HTTPAuthHelper
    {
        public static string GetWWWAuthenticateHeader(string type, string realmName)
        {
            string result = "";
            realmName= realmName.Replace("\\", "\\\\");
            realmName = realmName.Replace("\"", "\\\"");
            result = type + " realm=\"" + realmName + "\"";
            if (type == "Digest")
            {
                result += ", nonce=\"" + Utils.Md5.ComputeHash(Utils.GenerateRandomBytes(8)).BytesToString() + "\", algorithm=\"MD5\"";
            }
            return result;
        }

        public static bool ValidateAuthRequest(string rawChallange, string rawResponse,
        string method, string requestUri, Variant realm)
        {
            throw new System.NotImplementedException();
        }

        public static bool GetAuthorizationHeader(string s, string s1, string s2, string requestHeader, string requestHeader1, Variant variant)
        {
            throw new System.NotImplementedException();
        }
    }
}