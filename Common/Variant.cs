using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using static CSharpRTMP.Common.Logger;

namespace CSharpRTMP.Common
{
    public enum VariantType
    {
        Null = 1,
        Undefined = 2,
        Boolean = 3,
        SByte = 4,
        Int16 = 5,
        Int32 = 6,
        Int64 = 7,
        Byte = 8,
        UInt16 = 9,
        UInt32 = 10,
        UInt64 = 11,
        Double = 12,
        Numberic = 13,
        Timestamp = 14,
        Date = 15,
        Time = 16,
        String = 17,
        TypedMap = 18,
        Map = 19,
        ByteArray = 20
    }
    public sealed partial class Variant : IEnumerable,IRecyclable
    {
        public object Value;
        public VariantType ValueType;
        public T[] ToArray<T>()
        {
            var v = Value as VariantMap;
            return v?.Where(x => x.Key.StartsWith(Defines.VAR_INDEX_VALUE)).Select(x => (T)x.Value.Value).ToArray();
        }
        public Dictionary<string, T> ToDictionary<T>()
        {
            var v = Value as VariantMap;
            return v.AsParallel().ToDictionary(x => x.Key, y => (T)y.Value.Value);
        }
        public void Add<T>(string key, T value)
        {
            this[key] = value is Variant ?value as Variant:Get(value);
        }
        public void Add<T>( T value)
        {
            this[ArrayLength == -1 ? 0 : ArrayLength] = value is Variant ? value as Variant : Get(value);
        }

        public void Insert<T>(int index,T value )
        {
            var oldlength = ArrayLength;
            for (var i = oldlength; i >index; i--)
            {
                Children[VariantMap.GetIndexString(i)] = Children[VariantMap.GetIndexString(i-1)];
            }
            Children[VariantMap.GetIndexString(index)] = value is Variant ? value as Variant : Get(value);
            ArrayLength++;
        }
        public void RemoveAt(int index)
        {
            var newLength = ArrayLength-1;
            for (var i = index + 1; i <= newLength; i++)
            {
                Children[VariantMap.GetIndexString(i - 1)] = Children[VariantMap.GetIndexString(i)];
            }
            Children.Remove(VariantMap.GetIndexString(newLength));
            ArrayLength = newLength;
        }
        
        //public void AddRange<T>(params T[] values)
        //{
            
        //}
        //public static Variant CreateMap()
        //{
        //    return new Variant(new VariantMap());
        //}

        public bool IsMap => Value is VariantMap;

        public bool IsArray
        {
            get
            {
                var variantMap = Value as VariantMap;
                return variantMap?.IsArray ?? false;
            }
            set
            {
                var variantMap = Value as VariantMap;
                if (variantMap != null)
                {
                    variantMap.IsArray = value;
                }
                else
                {
                    variantMap = GlobalPool<VariantMap>.GetObject();
                    variantMap.IsArray = value;
                    SetValue(variantMap);
                }
            }
        }

        public Dictionary<string, Variant> Children => Value as VariantMap;

        public IEnumerator GetEnumerator()
        {
            if (IsMap)
                return (Value as VariantMap).GetEnumerator();
            return new HashSet<object>().GetEnumerator();
        }
        public int ArrayLength {
            set
            {
                var variantMap = Value as VariantMap;
                if (variantMap != null) variantMap.ArrayLength = value;
            }
            get
            {
                var variantMap = Value as VariantMap;
                if (variantMap != null) return variantMap.ArrayLength;
                return -1;
            }
        }
        public int Count => (Value as VariantMap)?.Count ?? -1;

        public string TypeName
        {
            get
            {
                var variantMap = Value as VariantMap;
                return variantMap?.TypeName;
            }
            set{
            if (ValueType != VariantType.Map && ValueType != VariantType.TypedMap && ValueType != VariantType.Undefined &&
                ValueType != VariantType.Null)
            {
                ASSERT($"SetMapName failed:{ToString()}");
                return;
            }
            ValueType = VariantType.TypedMap;
            var variantMap = Value as VariantMap;
            variantMap.TypeName = value;
            }
        }
        public void Compact()
        {
            switch (ValueType)
            {
                    case VariantType.Double:
                    var doubleVal = (double) Value;
                    if (doubleVal < int.MinValue || doubleVal > uint.MaxValue) break;
                    if ((long) doubleVal != doubleVal) break;
                    SetValue((long)doubleVal);
                    goto case VariantType.Int64;
                      case VariantType.Int64:
                    var longVal = (long) Value;
                    if (longVal < int.MinValue || longVal > uint.MaxValue) break;
                    if (longVal < 0)
                    {
                        SetValue((int) longVal);
                        goto case VariantType.Int32;
                    }
                    else
                    {
                        SetValue((uint)longVal);
                        goto case VariantType.UInt32;
                    }
                      case VariantType.Int32:
                    var int32Val = (int) Value;
                    if (int32Val < short.MinValue || int32Val > ushort.MaxValue) break;
                    if (int32Val < 0)
                    {
                        SetValue((short)int32Val);
                        goto case VariantType.Int16;
                    }
                    else
                    {
                        SetValue((ushort)int32Val);
                        goto case VariantType.UInt16;
                    }

                    case VariantType.Int16:
                    var int16Val = (short) Value;
                    if (int16Val < sbyte.MinValue || int16Val > byte.MaxValue) break;
                    if (int16Val < 0)
                    {
                        SetValue((sbyte)int16Val);
                        goto case VariantType.SByte;
                    }
                    else
                    {
                        SetValue((byte)int16Val);
                        goto case VariantType.Byte;
                    }
                      
                      case VariantType.UInt64:
                    if ((ulong) Value <= long.MaxValue)
                    {
                        SetValue((long)(ulong)Value);
                        goto case VariantType.Int64;
                    }
                    break;
                      case VariantType.UInt32:
                    if ((uint) Value <= int.MaxValue)
                    {
                        SetValue((int)(uint)Value);
                        goto case VariantType.Int32;
                    }
                    break;
                case VariantType.UInt16:
                    if ((ushort)Value <= short.MaxValue)
                    {
                        SetValue((short)(ushort)Value);
                        goto case VariantType.Int16;
                    }
                    break;
                case VariantType.Byte:
                    if ((byte)Value <= sbyte.MaxValue)
                    {
                        SetValue((sbyte)(byte)Value);
                        goto case VariantType.SByte;
                    }
                    break;
                case VariantType.SByte:
                    
                    break;
                case VariantType.Map:
                case VariantType.TypedMap:
                    foreach (var variant in Children)
                    {
                        variant.Value.Compact();
                    }
                    break;
                default:
                    break;

            }
        }

        public Variant Clone()
        {
            var clone = Get();
            clone.ValueType = ValueType;
            clone.Value = Value is VariantMap ? (Value as VariantMap).Clone() : Value;
            return clone;
        }
        public override string ToString()
        {
            if (Value == null) return "";
            using (var sw = new StringWriter())
            {
                var writer = new JsonTextWriter(sw) {Indentation = 2, Formatting = Formatting.Indented};
                SerializeToJson().WriteTo(writer);
                return sw.ToString();
            }
            //return JsonConvert.SerializeObject(SerializeToJson());
        }

        public void Recycle()
        {
            (Value as VariantMap)?.Recycle();
            Value = null;
            ValueType = VariantType.Null;
            this.ReturnPool();
        }

        public static Variant Get()
        {
            Variant result;
            return GlobalPool<Variant>.Pool.TryTake(out result) ? result : new Variant();
        }

        public static Variant Get(object value)
        {
            Variant result;
            if (!GlobalPool<Variant>.Pool.TryTake(out result))
            {
                if (value == null) return new Variant();
                result = new Variant();
                //return Activator.CreateInstance(typeof(Variant),value) as Variant;
            }
            result.SetValue(value);
            return result;
        }

        public static Variant GetList(params Variant[] items)
        {
            var map = GlobalPool<VariantMap>.GetObject();
            map.IsArray = true;
            for (var i = 0; i < items.Length; i++)
            {
                map[VariantMap.GetIndexString(i)] = items[i];
            }
            map.ArrayLength = items.Length;
            return Get(map);
        }

        public static Variant GetMap(VariantMapHelper dic) => Get(dic.VariantMap);
    }

    public struct VariantMapHelper : IEnumerable
    {
        public VariantMap VariantMap;
        public void Add<T>(string key, T value)
        {
            if (VariantMap == null) VariantMap = GlobalPool<VariantMap>.GetObject();
            VariantMap[key] = Variant.Get(value);
        }

        public IEnumerator GetEnumerator() => VariantMap.GetEnumerator();
    }
}
