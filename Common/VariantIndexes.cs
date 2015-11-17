using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CSharpRTMP.Common
{
    public partial class Variant
    {
        public Variant this[int index]
        {
            get
            {
                Variant result = null;
                (Value as VariantMap)?.TryGetValue(VariantMap.GetIndexString(index),out result);
                return result;
            }
            set
            {
                var v = Value as VariantMap;
                if (v == null) SetValue(v = GlobalPool<VariantMap>.GetObject());
                if (index > v.ArrayLength - 1) v.ArrayLength = index + 1;
                v[VariantMap.GetIndexString(index)] = value;
                IsArray = true;
            }
        }

        public Variant this[string key]
        {
            get
            {
                var value = Value as VariantMap;
                return value?.ContainsKey(key)==true ? value[key] : null;
                //return value == null || !value.ContainsKey(key) ? null : value[key];
            }
            set
            {
                var v = Value as VariantMap;
                if (v == null) SetValue(v=GlobalPool<VariantMap>.GetObject());
                if (value != null)
                {
                    if (v.ContainsKey(key) && !(v[key].Value is VariantMap))
                    {
                        v[key].Recycle();
                    }
                    v[key] = value;
                }
                else
                    v.Remove(key);
            }
        }

        public Variant this[params string[] keys]
        {
            get
            {
                var i = 0;
                var result = this[keys[i++]];
                while (result != null && i< keys.Length)
                {
                    result = result[keys[i++]];
                }
                return result;
            }
            set
            {
                var i = 0;
                var result = this;
                while (i < keys.Length - 1)
                {
                    var key = keys[i++];
                    if (result[key] == null)
                        result[key] = Get();
                    result = result[key];
                }
                result[keys[i]] = value;
            }
        }
    }
}
