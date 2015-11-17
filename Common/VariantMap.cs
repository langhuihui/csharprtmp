using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace CSharpRTMP.Common
{
    [Serializable]
    public sealed class VariantMap :Dictionary<string, Variant>,IRecyclable
    {
        public string TypeName;
        public bool IsArray;
        public int ArrayLength;
        public static List<string> IndexStrings = Enumerable.Range(0, 10).Select(x => Defines.VAR_INDEX_VALUE+x).ToList();
        public static Dictionary<string,string> IndexToString = new Dictionary<string, string>(); 
        public VariantMap()
        {
        }

        public VariantMap(SerializationInfo info, StreamingContext context):base(info,context)
        {
            TypeName = info.GetString("TypeName");
            IsArray = info.GetBoolean("IsArray");
            ArrayLength = info.GetInt32("ArrayLength");
        }
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info,context);
            info.AddValue("TypeName", TypeName);
            info.AddValue("IsArray", IsArray);
            info.AddValue("ArrayLength", ArrayLength);
        }
        public static string GetIndexString(int index)
        {
            for (var i = IndexStrings.Count; i <= index; i++)
            {
                IndexStrings.Add(Defines.VAR_INDEX_VALUE+i);
            }
            return IndexStrings[index];
        }
        public static string IndexStringToIntString(string indexString)
        {
            if (!IndexToString.ContainsKey(indexString))
            {
                IndexToString[indexString] = indexString.Substring(Defines.VAR_INDEX_VALUE_LEN);
            }
            return IndexToString[indexString];
        }
        public static int IndexStringToInt(string indexString)
        {
            if(IndexStrings.Contains(indexString))
            return IndexStrings.IndexOf(indexString);
            var j = Convert.ToInt32(indexString.Substring(Defines.VAR_INDEX_VALUE_LEN));
            IndexStrings.AddRange(Enumerable.Range(IndexStrings.Count, j).Select(x => Defines.VAR_INDEX_VALUE + x));
            return j;
        }
        public void CaculateArrayLength()
        {
            ArrayLength =
                this.Where(x => x.Key.StartsWith(Defines.VAR_INDEX_VALUE))
                    .Select(x => IndexStringToInt(x.Key))
                    .Max();
        }

        public VariantMap Clone()
        {
            var clone = GlobalPool<VariantMap>.GetObject();
            clone.TypeName = TypeName;
            clone.IsArray = IsArray;
            clone.ArrayLength = ArrayLength;
            foreach (var key in Keys)
            {
                clone[key] = this[key].Clone();
            }
            return clone;
        }
        public void Recycle()
        {
            foreach (var variant in Values)
            {
                variant.Recycle();
            }
            IsArray = false;
            ArrayLength = 0;
            Clear();
            TypeName = null;
            this.ReturnPool();
        }
    }
}
