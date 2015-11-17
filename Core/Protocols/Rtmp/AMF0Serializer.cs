using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Serialization;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Streaming;
using static CSharpRTMP.Core.Protocols.Rtmp.AMF0Serializer;

namespace CSharpRTMP.Core.Protocols.Rtmp
{
    public class AmfReadTypeAttribute : Attribute
    {
        public byte Type { get; }

        public AmfReadTypeAttribute(byte type)
        {
            Type = type;
        }
    }
    public class AMF0Reader : N2HBinaryReader
    {
        public bool Referencing;
        public delegate T AmfReadType<out T>(AMF0Reader instance,bool withType = false);

        private static readonly Dictionary<byte, Delegate> ReadMap = new Dictionary<byte, Delegate>();

        static AMF0Reader()
        {
            var type = typeof(AMF0Reader);
            var amfReadTypeOf = typeof(AmfReadType<>);
            foreach (var method in type.GetMethods())
            {
                var attribute = method.GetCustomAttribute<AmfReadTypeAttribute>();
                if (attribute == null) continue;
                ReadMap[attribute.Type] = Delegate.CreateDelegate(amfReadTypeOf.MakeGenericType(method.ReturnType), method);
            }
        }
        public AMF0Reader(Stream input) : base(input)
        {

        }

        public bool Available => BaseStream.GetAvaliableByteCounts() > 0;

        [AmfReadType(AMF0_SHORT_STRING)]
        public string ReadShortString(bool withType = false)
        {
            if (withType)
            {
                ReadByte();
            }

            var length = ReadInt16();
            //var result = Encoding.ASCII.GetString(ReadBytes(length));
            return Encoding.UTF8.GetString(ReadBytes(length));
        }
        [AmfReadType(AMF0_LONG_STRING)]
        public string ReadLongString(bool withType = false)
        {
            if (withType)
            {
                ReadByte();
            }
            var length = ReadInt32();
            //var result = Encoding.ASCII.GetString(ReadBytes(length));
            return Encoding.ASCII.GetString(ReadBytes(length)); ;
        }
        [AmfReadType(AMF0_NUMBER)]
        public double ReadAMFDouble(bool withType = false)
        {
            if (withType)
            {
                ReadByte();
            }
            var data = ReadInt64();
            return BitConverter.Int64BitsToDouble(data);
        }
        [AmfReadType(AMF0_OBJECT)]
        public Variant ReadObject(bool withType = false)
        {
            if (withType)
            {
                ReadByte();
            }
            var result = Variant.Get();
            var buffer = ReadBytes(3);
            while (!(buffer[0] == 0 && buffer[1] == 0 && buffer[2] == 9))
            {
                BaseStream.Position -= 3;
                var key = ReadShortString();
                result[key] = ReadVariant();
                buffer = ReadBytes(3);
            }
            return result;
        }
        [AmfReadType(AMF0_ARRAY)]
        public Variant ReadArray(bool withType = false)
        {
            if (withType)
            {
                ReadByte();
            }
            var result = Variant.Get();
            result.IsArray = true;
            var length = ReadInt32();
            for (var i = 0; i < length; i++)
            {
                result.Add(ReadVariant());
            }
            return result;
        }
        [AmfReadType(AMF0_MIXED_ARRAY)]
        public Variant ReadMixedArray(bool withType = false)
        {
            if (withType)
            {
                ReadByte();
            }
            var result = Variant.Get();
            result.IsArray = true;
            var length = ReadInt32();
            for (var i = 0; i < length; i++)
            {
                var key = ReadShortString();
                var value = ReadVariant();
                try
                {
                    var index = Convert.ToInt32(key);
                    result[index] = value;
                }
                catch (Exception)
                {
                    result[key] = value;
                }
            }
            var buffer = ReadBytes(3);
            while (!(buffer[0] == 0 && buffer[1] == 0 && buffer[2] == 9))
            {
                BaseStream.Position -= 3;
                var key = ReadShortString();
                result[key] = ReadVariant();
                buffer = ReadBytes(3);
            }
            return true;
        }

        public Variant ReadTypedObject(bool withType = false)
        {
            throw new NotImplementedException("未实现");
        }

        public bool ReadBooleanWithType(bool withType)
        {
            ReadByte();
            return ReadBoolean();
        }
        [AmfReadType(AMF0_AMF3_OBJECT)]
        public Variant ReadAMF3Object(bool withType = false)
        {
            if (withType)
            {
                ReadByte();
            }
            return new AMF3Reader(BaseStream).ReadVariant();
        }
        [AmfReadType(AMF0_TIMESTAMP)]
        public DateTime ReadTimestamp(bool withType = false)
        {
            if (withType) ReadByte();
            var time = ReadDouble();
            ReadBytes(2);
            return Utils.CTimeToDateTime(time);
        }
        [AmfReadType(AMF0_NULL)]
        public Variant ReadNull(bool withType = false) => Variant.Get();
        [AmfReadType(AMF0_UNDEFINED)]
        public Variant ReadUndefined(bool withType = false) => Variant.Get();

        public T Read<T>() => ((AmfReadType<T>)ReadMap[ReadByte()])(this);

        public Variant ReadVariant()
        {
            switch (ReadByte())
            {
                case AMF0_SHORT_STRING:
                    return Variant.Get(ReadShortString());
                case AMF0_LONG_STRING:
                    return Variant.Get(ReadLongString());
                case AMF0_NUMBER:
                    return Variant.Get(ReadAMFDouble());
                case AMF0_OBJECT:
                    return ReadObject();
                case AMF0_BOOLEAN:
                    return Variant.Get(ReadBoolean());
                case AMF0_NULL:
                    return ReadNull();
                case AMF0_UNDEFINED:
                    return ReadUndefined();
                case AMF0_MIXED_ARRAY:
                    return ReadMixedArray();
                case AMF0_ARRAY:
                    return ReadArray();
               case AMF0_AMF3_OBJECT:
                    return ReadAMF3Object();
                case AMF0_TIMESTAMP:
                    return ReadTimestamp();
                default:
                    return Variant.Get();
            }
        }
    }

    public class AMF0Writer : H2NBinaryWriter
    {
        private static readonly string[] _keysOrder =
        {
            "app","flashVer","fmsVer","swfUrl","tcUrl","fpad","capabilities",
		"audioCodecs","videoCodecs","videoFunction","pageUrl","level","code",
		"description","details","clientid","duration","width","height","videorate",
		"framerate","videocodecid","audiorate","audiorate","audiodelay",
		"audiocodecid","canSeekToEnd","creationdate"};
        private static readonly byte[] _endofObject = { 0, 0, 9 };
        public bool AMF0Preference;

        public AMF0Writer(Stream source) : base(source)
        {
        }
        public bool WriteShortString(string value, bool withType = false)
        {
            if (withType)
            {
                Write(AMF0_SHORT_STRING);
            }
            var buffer = Encoding.UTF8.GetBytes(value);
            Write((short)buffer.Length);
            Write(buffer);
            return true;
        }


        public bool WriteLongString(string value, bool withType = false)
        {
            if (withType)
            {
                Write(AMF0_LONG_STRING);
            }
            var buffer = Encoding.UTF8.GetBytes(value);
            Write(buffer.Length);
            Write(buffer);
            return true;
        }



        public bool WriteDouble(double value, bool withType = false)
        {
            if (withType)
            {
                Write(AMF0_NUMBER);
            }
            Write(BitConverter.DoubleToInt64Bits(value));
            return true;
        }

        public void BeginObject(string type, bool externalizable)
        {
            if (string.IsNullOrEmpty(type))
            {
                Write(AMF0_OBJECT);
            }
            else
            {
                Write(AMF0_TYPED_OBJECT);
                WriteShortString(type);
            }
        }

        public void EndObject() => Write(_endofObject);

        public bool WriteObject(Variant value, bool withType = false)
        {
            if (withType)
            {
                Write(AMF0_OBJECT);
            }
            foreach (var item in _keysOrder.Where(x => value.Children.ContainsKey(x)).ToArray())
            {
                WriteShortString(item);
                WriteVariant(value[item]);
                value.Children.Remove(item);
            }
            foreach (var item in value.Children)
            {
                WriteShortString(item.Key.StartsWith(Defines.VAR_INDEX_VALUE) ? VariantMap.IndexStringToIntString(item.Key) : item.Key);
                WriteVariant(item.Value);
            }
            Write(_endofObject);
            return true;
        }



        public bool WriteTypedObject(Variant value, bool withType = false)
        {
            if (withType)
            {
                Write(AMF0_TYPED_OBJECT);
            }
            WriteShortString(value.TypeName);
            return WriteObject(value);
        }



        public bool WriteMixedArray(Variant value, bool withType = false)
        {
            if (withType)
            {
                Write(AMF0_MIXED_ARRAY);
            }
            Write(value.Count);
            foreach (var s in _keysOrder.Where(x => value.Children.ContainsKey(x)).ToArray())
            {
                WriteShortString(s);
                WriteVariant(value[s]);
                value.Children.Remove(s);
            }
            foreach (var item in value.Children)
            {
                WriteShortString(item.Key.StartsWith(Defines.VAR_INDEX_VALUE) ? item.Key.Substring(Defines.VAR_INDEX_VALUE_LEN) : item.Key);
                WriteVariant(item.Value);
            }
            Write(_endofObject);
            return true;
        }



        public bool WriteArray(Variant value, bool withType = false)
        {
            throw new NotImplementedException("未实现");
        }



        public bool WriteAMF3Object(Variant result, bool withType = false)
        {
            if (withType)
            {
                Write(AMF0_AMF3_OBJECT);
            }
            new AMF3Writer(BaseStream).WriteVariant(result);
            return true;
        }



        public bool WriteBoolean(bool value, bool withType = false)
        {
            if (withType)
                Write(AMF0_BOOLEAN);
            Write(value);
            return true;
        }


        public bool WriteTimestamp(DateTime time, bool withType = false)
        {
            if (withType) Write(AMF0_TIMESTAMP);
            WriteDouble(time.SecondsFrom1970());
            Write((short)0);
            return true;
        }

        public bool WriteNull()
        {
            Write(AMF0_NULL);
            return true; 
        }

        public bool WriteUndefined()
        {
            Write(AMF0_UNDEFINED);
            return true;
        }

        public bool WriteVariant(Variant value)
        {
            switch (value.ValueType)
            {
                case VariantType.Null:
                    return WriteNull();
                case VariantType.Undefined:
                    return WriteUndefined();
                case VariantType.Map:
                    return value.IsArray ? WriteMixedArray(value, true) : WriteObject(value, true);
                case VariantType.TypedMap:
                    return WriteTypedObject(true);
                case VariantType.ByteArray:
                    return WriteAMF3Object(true);
                case VariantType.String:
                    string temp = value;
                    return temp.Length >= 65535 ? WriteLongString(temp, true) : WriteShortString(temp, true);
                case VariantType.Boolean:
                    return WriteBoolean(value, true);
                case VariantType.Date:
                case VariantType.Time:
                case VariantType.Timestamp:
                    return WriteTimestamp(value, true);
                default:
                    if (value == VariantType.Numberic)
                    {
                        return WriteDouble(value, true);
                    }
                    return false;
            }
        }

    }
    public class AMF0Serializer
    {
        public const byte AMF0_NUMBER = 0x00;
        public const byte AMF0_BOOLEAN = 0x01;
        public const byte AMF0_SHORT_STRING = 0x02;
        public const byte AMF0_OBJECT = 0x03;
        public const byte AMF0_NULL = 0x05;
        public const byte AMF0_UNDEFINED = 0x06;
        public const byte AMF0_MIXED_ARRAY = 0x08;
        public const byte AMF0_ARRAY = 0x0a;
        public const byte AMF0_TIMESTAMP = 0x0b;
        public const byte AMF0_LONG_STRING = 0x0c;
        public const byte AMF0_TYPED_OBJECT = 0x10;
        public const byte AMF0_AMF3_OBJECT = 0x11;
    }
}
