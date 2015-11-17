using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using CSharpRTMP.Common;
using static CSharpRTMP.Core.Protocols.Rtmp.AMF3Serializer;

namespace CSharpRTMP.Core.Protocols.Rtmp
{
    public class AMF3Reader : N2HBinaryReader
    {
        
        private readonly List<byte[]> _byteArrays = new List<byte[]>();
        private readonly List<string> _strings = new List<string>();
        private readonly List<object> _objects = new List<object>();
        private readonly List<Variant> _traits = new List<Variant>(); 
        //private readonly Dictionary<byte, string> _readMap = new Dictionary<byte, string>
        //{
        //        {AMF3_INTEGER,"ReadInterger"},
        //        {AMF3_STRING,"ReadAMFString"},
        //        {AMF3_TRUE,"ReadTrue"},
        //        {AMF3_DOUBLE,"ReadAMFDouble"},
        //        {AMF3_OBJECT,"ReadObject"},
        //        {AMF3_FALSE,"ReadFalse"},
        //        {AMF3_NULL,"ReadNull"},
        //        {AMF3_UNDEFINED,"ReadUndefined"},
        //        {AMF3_BYTEARRAY,"ReadByteArray"},
        //        {AMF3_ARRAY,"ReadArray"},
        //        {AMF3_DATE,"ReadDate"},
        //        {AMF3_XML,"ReadXML"}

        //};
        //static AMF3Reader()
        //{
        //    var type = typeof(AMF0Reader);
        //    var amfReadTypeOf = typeof(AMF0Reader.AmfReadType<>);
        //    foreach (var method in type.GetMethods())
        //    {
        //        var attribute = method.GetCustomAttribute<AmfReadTypeAttribute>();
        //        if (attribute == null) continue;
        //        _readMap[attribute.Type] = Delegate.CreateDelegate(amfReadTypeOf.MakeGenericType(method.ReturnType), method);
        //    }
        //}
        public AMF3Reader(Stream input) : base(input)
        {
        }
        public uint ReadInterger(bool withType = false)
        {
            if (withType) ReadByte();
            return ReadU29();
        }
        public double ReadAMFDouble(bool withType = false)
        {
            if (withType)
            {
                ReadByte();
            }
            var data = ReadInt64();
            return BitConverter.Int64BitsToDouble(data);
        }
        //public T Read<T>()
        //{
        //    return (T)GetType().GetMethod(_readMap[ReadByte()]).Invoke(this, new object[] { false });
        //}
        public DateTime ReadDate(bool withType = false)
        {
            if (withType) ReadByte();
            var temp = ReadU29();
            if ((temp & 0x01) == 1)
            {
                var time = BitConverter.Int64BitsToDouble(ReadInt64());
                var result = Utils.CTimeToDateTime(time);
                _objects.Add(result);
                return result;
            }
            return (DateTime)_objects[(int)temp >> 1];
        }
        public bool ReadTrue()
        {
            return true;
        }
        public bool ReadFalse()
        {
            return false;
        }
        public Variant ReadNull()
        {
            return Variant.Get();
        }
        public string ReadAMFString(bool readType = false)
        {
            var temp = ReadU29();
            var length = (int)(temp >> 1);
            if ((temp & 0x01) != 1) return _strings[length];
            if (length == 0) return "";
            var result = Encoding.UTF8.GetString(ReadBytes(length));
            _strings.Add(result);
            return result;
        }
        public uint ReadU29()
        {
            uint result = 0;
            for (var i = 0; i < 4; i++)
            {
                var b = ReadByte();
                if (i != 3)
                {
                    result = (result << 7) | (byte)(b & 0x7f);
                }
                else
                {
                    result = (result << 7) | b;
                }
                if ((b & 0x80) == 0) break;
            }
            return result;
        }
        public Variant ReadUndefined()
        {
            return Variant.Get();
        }
        public Variant ReadVariant()
        {
            switch (ReadByte())
            {
                case AMF3_INTEGER:
                    return Variant.Get(ReadInterger());
                case AMF3_STRING:
                    return Variant.Get(ReadAMFString());
                case AMF3_TRUE:
                    return Variant.Get(ReadTrue());
                case AMF3_FALSE:
                    return Variant.Get(ReadFalse());
                case AMF3_OBJECT:
                    return ReadObject();
                case AMF3_DOUBLE:
                    return Variant.Get(ReadAMFDouble());
                case AMF3_NULL:
                    return ReadNull();
                case AMF3_UNDEFINED:
                    return ReadUndefined();
                case AMF3_BYTEARRAY:
                    return ReadByteArray();
                case AMF3_ARRAY:
                    return ReadArray();
                case AMF3_DATE:
                    return ReadDate();
                //case AMF0Serializer.AMF3_XML:
                //    return ReadXML();
                default:
                    return Variant.Get();
            }
        }
        public Variant ReadArray(bool withType = false)
        {
            if (withType) ReadByte();
            var temp = ReadU29();
            if ((temp & 0x01) == 0)
            {
                return _objects[(int)temp >> 1] as Variant;
            }
            var result = Variant.Get();
            result.IsArray = true;
            var key = ReadAMFString();
            while (!string.IsNullOrEmpty(key))
            {
                result[key] = ReadVariant();
                key = ReadAMFString();
            }
            var denseSize = temp >> 1;
            for (var i = 0; i < denseSize; i++)
            {
                result[i] = ReadVariant();
            }
            _objects.Add(result);
            return result;
        }
        public Variant ReadByteArray(bool readType = false)
        {
            if (readType) ReadByte();
            uint temp = ReadU29();
            var result = Variant.Get();
            if ((temp & 0x01) == 1)
            {
                var length = temp >> 1;
                if (length != 0)
                {
                    var buffer = ReadBytes((int)length);
                    result.SetValue(buffer);
                    _byteArrays.Add(buffer);
                }
                else
                {
                    result.SetValue(new byte[0]);
                }

            }
            else
            {
                result.SetValue(_byteArrays[(int)(temp >> 1)]);
            }
            return result;
        }
        public Variant ReadObject(bool readType = false)
        {
            if (readType) ReadByte();
            var temp = ReadU29();
            var result = Variant.Get();
            var objectReference = (temp & 0x01) == 0;
            var objectReferenceIndex = temp >> 1;
            var traitsReference = ((temp & 0x02) == 0);
            var traitsReferenceIndex = temp >> 2;
            var traitsExtended = ((temp & 0x07) == 0x07);
            var isDynamic = ((temp & 0x08) != 0);
            var traitsCount = temp >> 4;
            if (objectReference)
            {
                return _objects[(int)objectReferenceIndex] as Variant;
            }
            if (traitsExtended)
            {
                var className = ReadAMFString();
                if (className == "flex.messaging.io.ArrayCollection")
                {
                    result = ReadVariant();
                    result.TypeName = className;
                    _objects.Add(result);
                    return result;
                }
                else
                {
                    return null;
                }
            }
            var objectIndex = _objects.Count;
            _objects.Add(Variant.Get());
            Variant traits;
            if (traitsReference)
            {
                traits = _traits[(int)traitsReferenceIndex];
            }
            else
            {
                traits = Variant.Get();
                var traitsIndex = _traits.Count;
                _traits.Add(Variant.Get());
                traits[Defines.AMF3_TRAITS_DYNAMIC] = isDynamic;
                var className = ReadAMFString();
                traits[Defines.AMF3_TRAITS_CLASSNAME] = className;
                traits[Defines.AMF3_TRAITS] = Variant.Get();
                for (var i = 0; i < traitsCount; i++)
                {
                    traits[Defines.AMF3_TRAITS].Add(ReadAMFString());
                }
                _traits[traitsIndex] = traits;
            }
            if (!string.IsNullOrEmpty(traits[Defines.AMF3_TRAITS_CLASSNAME]))
            {
                result.TypeName = traits[Defines.AMF3_TRAITS_CLASSNAME];
            }
            for (int i = 0; i < traits[Defines.AMF3_TRAITS].Count; i++)
            {
                result[(string)traits[Defines.AMF3_TRAITS][i]] = ReadVariant();
            }
            var readDynamicPoperties = traitsReference ? (bool)traits[Defines.AMF3_TRAITS_DYNAMIC] : isDynamic;
            if (readDynamicPoperties)
            {
                var key = ReadAMFString();
                while (!string.IsNullOrEmpty(key))
                {
                    result[key] = ReadVariant();
                    key = ReadAMFString();
                }
            }
            _objects[objectIndex] = result;
            return result;
        }
    }

    public class AMF3Writer : H2NBinaryWriter
    {
        public AMF3Writer(Stream source) : base(source)
        {
        }
        public bool WriteInterger(uint value, bool withType = false)
        {
            if (withType) Write(AMF3_INTEGER);
            return WriteU29(value);
        }

        public bool WriteAMFDouble(double value, bool withType = false)
        {
            if (withType)
            {
                Write(AMF3_DOUBLE);
            }
            Write(BitConverter.DoubleToInt64Bits(value));
            return true;
        }


        public bool WriteAMFBoolean(bool value)
        {
            Write(value ? AMF3_TRUE : AMF3_FALSE);
            return true;
        }

        public bool WriteNull()
        {
            Write(AMF3_NULL);
            return true;
        }

        public bool WriteUndefined()
        {
            Write(AMF3_UNDEFINED);
            return true;
        }



        public bool WriteAMFString(string value, bool withType = false)
        {
            WriteU29((uint)(value.Length << 1) | 0x01);
            Write(Encoding.UTF8.GetBytes(value));
            return true;
        }



        public bool WriteDate(DateTime value, bool withType = false)
        {
            if (withType) Write(AMF3_DATE);
            WriteU29(1);
            return WriteAMFDouble(value.SecondsFrom1970());
        }



        public bool WriteArray(Variant value, bool withType = false)
        {
            if (withType) Write(AMF3_ARRAY);
            var denseSize = value.ArrayLength;
            WriteU29((uint)(denseSize << 1) | 0x01);
            foreach (var item in value.Children)
            {
                WriteAMFString(item.Key);
                WriteVariant(item.Value);
            }
            WriteAMFString("");
            for (var i = 0; i < denseSize; i++)
            {
                WriteVariant(value[i]);
            }
            return true;
        }



        public bool WriteObject(Variant value, bool withType = false)
        {
            if (withType) Write(AMF3_OBJECT);
            WriteU29(0x0b);
            const string className = "";
            WriteAMFString(className);
            foreach (var item in value.Children)
            {
                WriteAMFString(item.Key);
                WriteVariant(item.Value);
            }
            WriteAMFString("");
            return true;
        }


        public bool WriteByteArray(byte[] value, bool withType = false)
        {
            WriteU29((uint)(value.Length << 1) | 0x01);
            Write(value);
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
                case VariantType.ByteArray:
                    return WriteByteArray(value, true);
                case VariantType.Map:
                    return value.IsArray ? WriteArray(value, true) : WriteObject(value, true);
                case VariantType.Date:
                case VariantType.Time:
                case VariantType.Timestamp:
                    return WriteDate(value, true);
                case VariantType.String:
                    return WriteAMFString(value, true);
                case VariantType.Boolean:
                    return WriteAMFBoolean(value);
                default:
                    return value == VariantType.Numberic && WriteAMFDouble(value, true);
            }
        }


        public bool WriteU29(uint value)
        {
            var temp = (uint)IPAddress.HostToNetworkOrder((int)value);
            var buffer = BitConverter.GetBytes(temp);
            if ((0x00000000 <= value) && (value <= 0x0000007f))
            {
                Write(buffer[3]);
                return true;
            }
            if ((0x00000080 <= value) && (value <= 0x00003fff))
            {
                Write(((buffer[2] << 1) | (buffer[3] >> 7)) | 0x80);
                Write(buffer[3] & 0x7f);
                return true;
            }
            if ((0x00004000 <= value) && (value <= 0x001fffff))
            {
                Write(((buffer[1] << 2) | (buffer[2] >> 6)) | 0x80);
                Write(((buffer[2] << 1) | (buffer[3] >> 7)));
                Write(buffer[3] & 0x7f);
                return true;
            }
            if ((0x0020000 <= value) && (value <= 0x01fffffff))
            {
                Write(((buffer[0] << 2) | (buffer[1] >> 6)) | 0x80);
                Write(((buffer[1] << 1) | (buffer[2] >> 7)));
                Write(buffer[2] | 0x80);
                Write(buffer[3] & 0x7f);
                return true;
            }
            return false;
        }
    }
    public class AMF3Serializer
    {
        public const byte AMF3_UNDEFINED = 0x00;
        public const byte AMF3_NULL = 0x01;
        public const byte AMF3_FALSE = 0x02;
        public const byte AMF3_TRUE = 0x03;
        public const byte AMF3_INTEGER = 0x04;
        public const byte AMF3_DOUBLE = 0x05;
        public const byte AMF3_STRING = 0x06;
        public const byte AMF3_XMLDOC = 0x07;
        public const byte AMF3_DATE = 0x08;
        public const byte AMF3_ARRAY = 0x09;
        public const byte AMF3_OBJECT = 0x0a;
        public const byte AMF3_XML = 0x0b;
        public const byte AMF3_BYTEARRAY = 0x0c;
    }
}
