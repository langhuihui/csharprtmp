using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Common
{
    [Serializable]
    partial class Variant:ISerializable
    {
        public Variant(SerializationInfo info, StreamingContext context)
        {
            ValueType = (VariantType)info.GetByte("ValueType");
            switch (ValueType)
            {
                case VariantType.Map:
                case VariantType.TypedMap:
                    Value = info.GetValue("Value", typeof (VariantMap));
                    break;
                    case VariantType.ByteArray:
                    Value = info.GetValue("Value", typeof (byte[]));
                    break;
                case VariantType.Date:
                case VariantType.Time:
                case VariantType.Timestamp:
                    Value = info.GetValue("Value", typeof (DateTime));
                    break;
                case VariantType.Null:
                case VariantType.Undefined:
                    Value = null;
                    break;
                default:
                    Value = info.GetValue("Value",typeof(object));
                    break;
            }
        }
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("ValueType",(byte)ValueType);
            switch (ValueType)
            {
                case VariantType.Null:
                case VariantType.Undefined:
                    break;
                default:
                    info.AddValue("Value",Value);
                    break;
            }
        }

        public void SerializeToFile(string path) => File.WriteAllText(path, JsonConvert.SerializeObject(SerializeToJson()));

        public static bool DeserializeFromFile(string path,out Variant result)
        {
            //result = File.ReadAllBytes(path).ToObject<Variant>();
            result = DeserializeFromJsonFile(path);
            return true;
        }

        public static Variant DeserializeFromJsonFile(string path)
        {
            var json = JsonConvert.DeserializeObject(File.ReadAllText(path)) as JToken;
            return Get(json);
        }

   
        public JToken SerializeToJson()
        {
            switch (ValueType)
            {
                case VariantType.Map:
                case VariantType.TypedMap:
                    return new JObject(Children.Select(x => new JProperty(x.Key, x.Value.SerializeToJson())));
                default:
                    return Value == null?null:JToken.FromObject(Value);
            }
        }
    }
}
