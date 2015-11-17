using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Random = System.Random;

namespace CSharpRTMP.Common
{
    public class RC4_KEY
    {
        public byte x, y;
        public byte[] data = new byte[256];
    }
    
    public static class Utils
    {
        public static Random Random = new Random((int)DateTime.Now.Ticks);
        public static readonly ulong __STREAM_CAPABILITIES_VERSION = MakeTag("VER3");
        public static RecyclableMemoryStreamManager Rms = new RecyclableMemoryStreamManager();
        public static MD5 Md5 = MD5.Create();
        public static readonly string[] NaluTypes =
        {
            null,"SLICE","DPA","DPB","DPC",
            "IDR","SEI","SPS","PPS","PD",
            "EOSEQ","EOSTREAM","FILL",
            "RESERVED13","RESERVED14","RESERVED15","RESERVED16",
            "RESERVED17","RESERVED18","RESERVED19","RESERVED20",
            "RESERVED21","RESERVED22","RESERVED23","STAPA","STAPB",
            "MTAP16","MTAP24","FUA","FUB","RESERVED30","RESERVED31"
        };
        public static readonly BinaryFormatter BinaryFormatter = new BinaryFormatter();
        public static DateTime DateTime1970 = new DateTime(1970, 1, 1,0,0,0,DateTimeKind.Utc);
        public static DateTime DateTime1904 = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        //public static readonly long Seconds1970 = (long)(DateTime1970 - new DateTime()).TotalSeconds;
        public static readonly bool IsBigEndian = BitConverter.GetBytes(0x01)[0] == 0x01;
        public static Dictionary<Type, object[]> CustomAttributesCache = new Dictionary<Type, object[]>();

        public static byte[] GenerateRandomBytes(int count)
        {
            var bytes = new byte[count];
            Random.NextBytes(bytes);
            return bytes;
        }
        public static double SecondsFrom1970(this DateTime dateTime) => (dateTime - DateTime1970).TotalSeconds;
        public static ulong SecondsFrom1904(this DateTime dateTime) => (ulong)(dateTime - DateTime1904).TotalSeconds;
        public static double MilliSecondsFrom1970(this DateTime dateTime) => (dateTime - DateTime1970).TotalMilliseconds;
        public static bool IsElapsed(this DateTime dateTime,int minisecond) => (DateTime.Now - dateTime).TotalMilliseconds > minisecond;

        public static int Elapsed(this DateTime dateTime) => (int) (DateTime.Now - dateTime).TotalMilliseconds;

        public static DateTime CTimeToDateTime(double secondsFrom970) => DateTime1970.AddSeconds(secondsFrom970);

        public static string NaluToString(byte naluType)
        {
            var index = naluType & 0x1F;
            return index < 32||index==0 ? NaluTypes[index] : "UNDEFINED";
        }
        public static void Map_Erase<TKey,TValue>(this IDictionary<TKey, TValue> map, TKey key)
        {
            if (map.ContainsKey(key)) map.Remove(key);
        }

        public static void Map_Erase2<TKey1,TKey2, TValue>(this IDictionary<TKey1, Dictionary<TKey2, TValue>> map, TKey1 key1, TKey2 key2)
        {
            if (map.ContainsKey(key1))
            {
                map[key1].Map_Erase(key2);
                if(map[key1].Count==0)map.Map_Erase(key1);
            }
        }

        public static ulong MakeTag(string s) => MakeTag(s.ToCharArray());

        public static ulong MakeTag(params char[] args)
        {
            //var array = new ulong[] {56,48,40,32,24,16,8,0};
            var i = 56 + 8;
            return args.Select(x => (Convert.ToUInt64(x) << (i -= 8))).Aggregate((a, b) => a | b);
            //for (int i = 0; i < args.Length; i++)
            //{
            //    array[i] = (ulong) args[i];
            //}
            //return array[0]<<56|(array[1]<<48)|(array[2]<<40)|(array[3]<<32)|(array[4]<<24)|(array[5]<<16)|(array[6]<<8)|array[7];
        }
        private static IEnumerable<byte> _TagToString(ulong tag)
        {
            for (uint i = 0; i < 8; i++)
            {
                byte v = (byte)(tag >> ((int)(7 - i) * 8) & 0xff);
                if (v == 0) yield break;
                yield return v;
            }
        }
        public static string TagToString(this ulong tag) => new string(Encoding.ASCII.GetChars(_TagToString(tag).ToArray()));

        public static bool TagKindOf(this ulong tag,ulong kind) => (tag & kind.GetTagMask()) == kind;

        public static ulong GetTagMask(this ulong tag)
        {
            ulong result = 0xffffffffffffffffL;
            for (sbyte i = 56; i >= 0; i -= 8)
            {
                if (((tag >> i) & 0xff) == 0)
                    break;
                result = result >> 8;
            }
            return ~result;
        }

        public static void WriteBytes(this MemoryStream ms,byte[] bytes) => ms.Write(bytes, 0, bytes.Length);

        public static byte[] ToBytes(this object o)
        {
            using (var ms = Rms.GetStream())
            {
                BinaryFormatter.Serialize(ms,o);
                return ms.ToArray();
            }
        }
        public static T ToObject<T>(this byte[] o)
        {
            using (var ms = new MemoryStream(o))
            {
                return (T)BinaryFormatter.Deserialize(ms);
            }
        }
        private const string AlowedCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        private static readonly int AlowedCharactersLength = AlowedCharacters.Length;
        public static string GenerateRandomString(int onbwcheckSize)
        {
            var bytes = new char[onbwcheckSize];
            
            var l = AlowedCharacters.Length;
            for (var i = 0; i < onbwcheckSize; i++)
                bytes[i] = AlowedCharacters[Random.Next(0, AlowedCharactersLength - 1)];
            return new string(bytes);
        }
        public static string NormalizePath(this string basepath, string filepath = "")
        {
            basepath = Path.GetFullPath(basepath);
            filepath = Path.GetFullPath(basepath + filepath);
            return string.IsNullOrEmpty(basepath) ||
                string.IsNullOrEmpty(filepath) ||
                !filepath.StartsWith(basepath) ||
                (!File.Exists(filepath)&&!Directory.Exists(filepath))
                ? string.Empty : filepath;
        }
        
        public static IEnumerable<T> GetAttribute<T>(this object target,bool inherit = true)
        {
            var t = target.GetType();
            if (!CustomAttributesCache.ContainsKey(t))
            {
                CustomAttributesCache[t] = t.GetCustomAttributes(inherit);
            }
            return CustomAttributesCache[t].OfType<T>();
        }
        public static bool SerializeToFile(this JToken data,string path)
        {
            try
            {
                File.WriteAllText(path, data.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return false;
            }
            return true;
        }
      
        

        public static Dictionary<string, string> GetURLParam(this string s) => s.Split('&').Select(x => x.Split('=')).ToDictionary(x => x[0], y => y[1]);

        public static void RC4(BufferWithOffset buffer, RC4_KEY key, long length)
        {
            var state = new byte[256];
            short counter;
            byte x = key.x;
            byte y = key.y;
            Buffer.BlockCopy(key.data, 0, state, 0, 256);
            for (counter = 0; counter < length; counter++)
            {
                x = (byte)((x + 1) % 256);
                y = (byte)((state[x] + y) % 256);
                var temp = state[x];
                state[x] = state[y];
                state[y] = temp;
                var xorIndex = (byte)((state[x] + state[y]) % 256);
                buffer[counter] ^= state[xorIndex];
            }
            Buffer.BlockCopy(state, 0, buffer.Buffer, 0, 256);
            key.x = x;
            key.y = y;
        }

        private static void Prepare_key(RC4_KEY key, byte[] key_data_ptr, int key_data_len)
        {
            var state= key.data;;
            short counter;
            for (counter = 0; counter < 256; counter++)
                state[counter] = (byte) counter;
            key.x = 0;
            key.y = 0;
            byte index1 = 0;
            byte index2 = 0;
            for (counter = 0; counter < 256; counter++)
            {
                index2 = (byte) ((key_data_ptr[index1] + state[counter] + index2) % 256);
                var temp = state[counter];
                state[counter] = state[index2];
                state[index2] = temp;
                index1 = (byte) ((index1 + 1) % key_data_len);
            }       
        }
        public static void InitRC4Encryption(byte[] secretKey, byte[] pubKeyIn, byte[] pubKeyOut, RC4_KEY rc4keyIn,
            RC4_KEY rc4keyOut)
        {
            var sha256 = new HMACSHA256(secretKey);
            var digest = sha256.TransformFinalBlock(pubKeyIn, 0, pubKeyIn.Length);
            Prepare_key(rc4keyOut, digest, 16);
            digest = sha256.TransformFinalBlock(pubKeyOut, 0, pubKeyOut.Length);
            Prepare_key(rc4keyIn, digest, 16);
        }
        public static byte[] GetBytes<TStruct>(this TStruct data) where TStruct : struct
        {
            int structSize = Marshal.SizeOf(typeof(TStruct));
            var buffer = new byte[structSize];
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            Marshal.StructureToPtr(data, handle.AddrOfPinnedObject(), false);
            handle.Free();
            return buffer;
        }
        public static void GetStruct<TStruct>(this byte[] sourceData, out TStruct objectRef, int startIndex = 0) where TStruct : struct
        {
            IntPtr tmptr = IntPtr.Zero;
            var length = Marshal.SizeOf(typeof(TStruct));
            try
            {
                tmptr = Marshal.AllocHGlobal(length);
                Marshal.Copy(sourceData, startIndex, tmptr, length);
                objectRef = (TStruct)Marshal.PtrToStructure(tmptr, typeof(TStruct));
                Marshal.FreeHGlobal(tmptr);
            }
            catch (Exception ex)
            {
                if (tmptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(tmptr);
                throw new Exception("内存操作失败：" + ex.ToString());
            }
        }

        public static void ToBytes(this string source, BufferWithOffset buffer)
        {
            var charArray = source.ToCharArray().Select(x=>(byte)x).ToArray();
            Buffer.BlockCopy(charArray, 0, buffer.Buffer, buffer.Offset, charArray.Length);
        }

        public static byte[] ToBytes(this string source) => source.ToCharArray().Select(x => (byte)x).ToArray();

        public static string BytesToString(this byte[] buffer) => new string(buffer.Select(x=>(char)x).ToArray());

        public static byte[] DecodeFromHex(string inputString)
        {
            var bytes = new byte[inputString.Length >> 1];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(inputString.Substring(i << 1, 2), 16);
            }
            return bytes;
        }

        public static byte[] DecodeFromBase64(string inputString)
        {
            var ms = Rms.GetStream();
            using (FromBase64Transform myTransform = new FromBase64Transform(FromBase64TransformMode.IgnoreWhiteSpaces))
            {
                byte[] myOutputBytes = new byte[myTransform.OutputBlockSize];
                byte[] myInputBytes = inputString.ToCharArray().Select(x => (byte)x).ToArray();

                //Transform the data in chunks the size of InputBlockSize. 
                int i = 0;
                while (myInputBytes.Length - i > 4/*myTransform.InputBlockSize*/)
                {
                    int bytesWritten = myTransform.TransformBlock(myInputBytes, i, 4/*myTransform.InputBlockSize*/, myOutputBytes, 0);
                    i += 4/*myTransform.InputBlockSize*/;
                    ms.Write(myOutputBytes, 0, myOutputBytes.Length);
                }

                //Transform the final block of data.
                myOutputBytes = myTransform.TransformFinalBlock(myInputBytes, i, myInputBytes.Length - i);
                ms.Write(myOutputBytes, 0, myOutputBytes.Length);
                //Free up any used resources.
                myTransform.Clear();

            }
            return ms.ToArray();
        }

    }
}
