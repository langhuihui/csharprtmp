using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using static System.Convert;

namespace CSharpRTMP.Common
{
    partial class Variant
    {
        public static implicit operator string(Variant v) => v == null ? null : Convert.ToString(v.Value);

        public static implicit operator Variant(string v) => Get(v);

        public static implicit operator DateTime(Variant v) => ToDateTime(v.Value);

        public static implicit operator Variant(DateTime v) => Get(v);

        public static implicit operator sbyte(Variant v) => ToSByte(v?.Value ?? 0);

        public static implicit operator Variant(sbyte v) => Get(v);

        public static implicit operator byte(Variant v) => ToByte(v?.Value ?? 0);

        public static implicit operator Variant(byte v) => Get(v);

        public static implicit operator short(Variant v) => ToInt16(v?.Value ?? 0);

        public static implicit operator Variant(short v) => Get(v);

        public static implicit operator ushort(Variant v) => ToUInt16(v?.Value ?? 0);

        public static implicit operator Variant(ushort v) => Get(v);

        public static implicit operator int(Variant v) => ToInt32(v?.Value ?? 0);

        public static implicit operator Variant(int v) => Get(v);

        public static implicit operator uint(Variant v) => ToUInt32(v?.Value ?? 0);

        public static implicit operator Variant(uint v) => Get(v);

        public static implicit operator long(Variant v) => ToInt64(v?.Value ?? 0);

        public static implicit operator Variant(long v) => Get(v);

        public static implicit operator ulong(Variant v) => ToUInt64(v?.Value ?? 0);

        public static implicit operator Variant(ulong v) => Get(v);

        public static implicit operator double(Variant v) => ToDouble(v?.Value ?? 0);

        public static implicit operator Variant(double v) => Get(v);

        public static implicit operator bool(Variant v) => ToBoolean(v?.Value ?? false);
        public static implicit operator Variant(bool v) => Get(v);
        public static implicit operator byte[](Variant v) => v.Value as byte[];
        public static implicit operator Variant(byte[] v) => Get(v);

        public static implicit operator Array(Variant v)
        {
            var variants = v.Value as List<Variant>;
            return variants?.Select(x=>x.Value).ToArray();
        }
        public static implicit operator VariantMap(Variant v) => v.Value as VariantMap;

        public static bool operator ==(Variant v1, VariantType v2)
        {
            if ((v1 as object) == null) return false;
            return v2 == VariantType.Numberic
                ? v1.ValueType == VariantType.SByte ||
                  v1.ValueType == VariantType.Int16 ||
                  v1.ValueType == VariantType.Int32 ||
                  v1.ValueType == VariantType.Int64 ||
                  v1.ValueType == VariantType.UInt16 ||
                  v1.ValueType == VariantType.UInt32 ||
                  v1.ValueType == VariantType.UInt64 ||
                  v1.ValueType == VariantType.Double ||
                  v1.ValueType == VariantType.Byte
                : v1.ValueType == v2;
        }

        public static bool operator !=(Variant v1, VariantType v2) => !(v1 == v2);

        //public static bool operator ==(Variant v1, object v2)
        //{
        //    var v1Null = ((v1 as Object) == null || v1.ValueType == VariantType.Null);
        //    return v1Null && v2 == null ||
        //           !v1Null && (v2 != null && v1.Value.Equals(v2 is Variant ? ((Variant) v2).Value : v2));
        //}
        public static bool operator ==(Variant v1, object v2) => (object) v1 == null && v2 == null
                                                                 || (object) v1 != null && v2 != null && v1.Value.Equals(v2 is Variant ? (v2 as Variant).Value : v2);
        public static bool operator !=(Variant v1, object v2) => !(v1 == v2);
    }
}
