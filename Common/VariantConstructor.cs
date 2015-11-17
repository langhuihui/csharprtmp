using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Common
{
    
    partial class Variant
    {
        public Variant(VariantMap value)
        {
            Value = value;
            ValueType = VariantType.Map;
        }
        public Variant()
        {
            ValueType = VariantType.Null;
        }

        //public Variant(object o)
        //{
        //    if (o == null)
        //    {
        //        Value = null;
        //        ValueType = VariantType.Null;
        //        return;
        //    } 
        //    Value = o;
        //    var type = o.GetType().Name;
        //    VariantType vType;
        //    if (Enum.TryParse(type, false, out vType))
        //    {
        //        ValueType = vType;
        //    }
        //    else
        //    {
        //        if (o is byte[])
        //        {
        //            ValueType = VariantType.ByteArray;
        //        }
        //        else if (o is VariantMap)
        //        {
        //            ValueType = VariantType.Map;
        //        }
        //        else
        //        {
        //            ValueType = VariantType.Undefined;
        //        }
        //    }
        //}
        public Variant(Variant v)
        {
            Value = v.Value;
            ValueType = v.ValueType;
        }
        public Variant(byte[] v)
        {
            Value = v;
            ValueType = VariantType.ByteArray;
        }
        public Variant(byte v)
        {
            Value = v;
            ValueType = VariantType.SByte;
        }
        public Variant(sbyte v)
        {
            Value = v;
            ValueType = VariantType.Byte;
        }
        public Variant(int v)
        {
            Value = v;
            ValueType = VariantType.Int32;
        }

        public Variant(uint v)
        {
            Value = v;
            ValueType = VariantType.UInt32;
        }
        public Variant(ushort v)
        {
            Value = v;
            ValueType = VariantType.UInt16;
        }
        public Variant(short v)
        {
            Value = v;
            ValueType = VariantType.Int16;
        }
        public Variant(ulong v)
        {
            Value = v;
            ValueType = VariantType.UInt64;
        }

        public Variant(long v)
        {
            Value = v;
            ValueType = VariantType.Int64;
        }

        public Variant(double v)
        {
            Value = v;
            ValueType = VariantType.Double;
        }

        public Variant(bool v)
        {
            Value = v;
            ValueType = VariantType.Boolean;
        }

        public Variant(string v)
        {
            Value = v;
            ValueType = VariantType.String;
        }

        public Variant(int year, int month, int day)
        {
            Value = new DateTime(year, month, day);
            ValueType = VariantType.Date;
        }

        public Variant(int hour, int min, int sec, int m)
        {
            Value = new DateTime(1970, 1, 1, hour, min, sec, m);
            ValueType = VariantType.Time;
        }
        public Variant(int year, int month, int day, int hour, int min, int sec, int m)
        {
            Value = new DateTime(year, month, day, hour, min, sec, m);
            ValueType = VariantType.Timestamp;
        }

        public Variant(DateTime dateTime)
        {
            Value = dateTime;
            ValueType = VariantType.Timestamp;
        }

        public Variant(IList<Variant> array)
        {
            var vm = new VariantMap();
            Value = vm;
            ValueType = VariantType.Map;
            vm.IsArray = true;
            vm.ArrayLength = array.Count;
            for (var i = 0; i < vm.ArrayLength; i++)
            {
                vm[VariantMap.GetIndexString(i)] = array[i];
            }
        }
        public Variant(JToken json)
        {
            switch (json.Type)
            {
                case JTokenType.Array:
                    ValueType = VariantType.Map;
                    foreach (var item in json)
                        Add(Get(item));
                    break;
                case JTokenType.Object:
                    ValueType = VariantType.Map;
                    foreach (var property in (json as JObject).Properties())
                    {
                        //Debug.WriteLine(property.Value.GetType().ToString());
                        Add(property.Name, Get(property.Value));
                        if (property.Name.StartsWith(Defines.VAR_INDEX_VALUE))
                        {
                            IsArray = true;
                            ArrayLength = Math.Max(ArrayLength, VariantMap.IndexStringToInt(property.Name)+1);
                        }
                    }
                    break;
                case JTokenType.Boolean:
                    ValueType = VariantType.Boolean;
                    Value = (bool) json;
                    break;
                case JTokenType.Null:
                    ValueType = VariantType.Null;
                    break;
                case JTokenType.String:
                    ValueType = VariantType.String;
                    Value = (string) json;
                    break;
                case JTokenType.Integer:
                    if ((ulong) json <= int.MaxValue)
                    {
                        ValueType = VariantType.Int32;
                        Value = (int) json;
                    }
                    else
                    {
                        ValueType=VariantType.Int64;
                        Value = (long) json;
                    }
                    break;
                case JTokenType.Float:
                    ValueType = VariantType.Double;
                    Value = (double) json;
                    break;
                case JTokenType.Undefined:
                    ValueType = VariantType.Undefined;
                    break;
                case JTokenType.Date:
                    ValueType = VariantType.Date;
                    Value = (DateTime) json;
                    break;
                case JTokenType.TimeSpan:
                    ValueType = VariantType.Time;
                    Value = (DateTime)json;
                    break;
            }
        }

        public void SetValue(VariantMap value)
        {
            Value = value;
            ValueType = VariantType.Map;
        }
       
        public void SetValue(object o = null)
        {
            if (o == null)
            {
                Value = null;
                ValueType = VariantType.Null;
                return;
            }
            if (o is JToken)
            {
                SetValue(o as JToken);
            }else if (o is byte[])
            {
                SetValue(o as byte[]);
            }
            else if (o is VariantMap)
            {
                SetValue(o as VariantMap);
            }
            else if (o is IList<Variant>)
            {
                SetValue(o as IList<Variant>);
            }
            else if (o is Variant)
            {
                SetValue(o as Variant);
            }
            else if (Enum.TryParse(o.GetType().Name, false, out ValueType))
            {
                Value = o;
            }
            else
            {
                Debug.WriteLine(o.GetType().Name);
                ValueType = VariantType.Undefined;
            }
        }
        public void SetValue(Variant v)
        {
            Value = v.Value;
            ValueType = v.ValueType;
        }
        public void SetValue(byte[] v)
        {
            Value = v;
            ValueType = VariantType.ByteArray;
        }
        public void SetValue(byte v)
        {
            Value = v;
            ValueType = VariantType.SByte;
        }
        public void SetValue(sbyte v)
        {
            Value = v;
            ValueType = VariantType.Byte;
        }
        public void SetValue(int v)
        {
            Value = v;
            ValueType = VariantType.Int32;
        }

        public void SetValue(uint v)
        {
            Value = v;
            ValueType = VariantType.UInt32;
        }
        public void SetValue(ushort v)
        {
            Value = v;
            ValueType = VariantType.UInt16;
        }
        public void SetValue(short v)
        {
            Value = v;
            ValueType = VariantType.Int16;
        }
        public void SetValue(ulong v)
        {
            Value = v;
            ValueType = VariantType.UInt64;
        }

        public void SetValue(long v)
        {
            Value = v;
            ValueType = VariantType.Int64;
        }

        public void SetValue(double v)
        {
            Value = v;
            ValueType = VariantType.Double;
        }

        public void SetValue(bool v)
        {
            Value = v;
            ValueType = VariantType.Boolean;
        }

        public void SetValue(string v)
        {
            Value = v;
            ValueType = VariantType.String;
        }
        public void SetValue(int year, int month, int day)
        {
            Value = new DateTime(year, month, day);
            ValueType = VariantType.Date;
        }

        public void SetValue(int hour, int min, int sec, int m)
        {
            Value = new DateTime(1970, 1, 1, hour, min, sec, m);
            ValueType = VariantType.Time;
        }
        public void SetValue(int year, int month, int day, int hour, int min, int sec, int m)
        {
            Value = new DateTime(year, month, day, hour, min, sec, m);
            ValueType = VariantType.Timestamp;
        }
        public void SetValue(DateTime dateTime)
        {
            Value = dateTime;
            ValueType = VariantType.Timestamp;
        }
        public void SetValue(IList<Variant> array)
        {
            var vm = new VariantMap();
            Value = vm;
            ValueType = VariantType.Map;
            vm.IsArray = true;
            vm.ArrayLength = array.Count;
            for (var i = 0; i < vm.ArrayLength; i++)
            {
                vm[VariantMap.GetIndexString(i)] = array[i];
            }
        }
        public void SetValue(JToken json)
        {
            switch (json.Type)
            {
                case JTokenType.Array:
                    ValueType = VariantType.Map;
                    foreach (var item in json)
                        Add(Get(item));
                    break;
                case JTokenType.Object:
                    ValueType = VariantType.Map;
                    foreach (var property in (json as JObject).Properties())
                    {
                        Add(property.Name, Get(property.Value));
                        if (property.Name.StartsWith(Defines.VAR_INDEX_VALUE))
                        {
                            IsArray = true;
                            ArrayLength = Math.Max(ArrayLength, VariantMap.IndexStringToInt(property.Name) + 1);
                        }
                    }
                    break;
                case JTokenType.Boolean:
                    ValueType = VariantType.Boolean;
                    Value = (bool)json;
                    break;
                case JTokenType.Null:
                    ValueType = VariantType.Null;
                    break;
                case JTokenType.String:
                    ValueType = VariantType.String;
                    Value = (string)json;
                    break;
                case JTokenType.Integer:
                    if ((ulong)json <= int.MaxValue)
                    {
                        ValueType = VariantType.Int32;
                        Value = (int)json;
                    }
                    else
                    {
                        ValueType = VariantType.Int64;
                        Value = (long)json;
                    }
                    break;
                case JTokenType.Float:
                    ValueType = VariantType.Double;
                    Value = (double)json;
                    break;
                case JTokenType.Undefined:
                    ValueType = VariantType.Undefined;
                    break;
                case JTokenType.Date:
                    ValueType = VariantType.Date;
                    Value = (DateTime)json;
                    break;
                case JTokenType.TimeSpan:
                    ValueType = VariantType.Time;
                    Value = (DateTime)json;
                    break;
            }
        }
    }
}
