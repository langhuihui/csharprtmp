using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CSharpRTMP.Common
{
    public interface IRecyclable
    {
        void Recycle();
    }
    public static class ObjectPoolUtil
    {
        public static void ReturnPool<T>(this T e) where T : new() => GlobalPool<T>.Pool.Add(e);

        public static bool IsInPool<T>(this T e) where T : new() => GlobalPool<T>.Pool.Contains(e);
    }

    public static class GlobalPool<T> where T:new()
    {
        public static readonly ConcurrentBag<T> Pool = new ConcurrentBag<T>();
        public static T GetObject(params object[] args)
        {
            T result;
            return Pool.TryTake(out result) ? result : (T) Activator.CreateInstance(typeof (T), args);
        }

        public static T GetObject()
        {
            T result;
            return Pool.TryTake(out result) ? result : new T();
        }
        public static bool GetObject(out T result, params object[] args)
        {
            if (Pool.TryTake(out result)) return true;
            result = (T)Activator.CreateInstance(typeof(T), args);
            return false;
        }
        public static void RecycleObject(T o) => Pool.Add(o);
    }

    public class ObjectPool<T>
    {
        public readonly ConcurrentBag<T> Pool = new ConcurrentBag<T>();
        public T GetObject(params object[] args)
        {
            T result;
            if (Pool.TryTake(out result)) return result;
            return (T)Activator.CreateInstance(typeof(T), args);
        }

        public bool GetObject(out T result, params object[] args)
        {
            if (Pool.TryTake(out result)) return true;
            result = (T)Activator.CreateInstance(typeof(T), args);
            return false;
        }
        public void RecycleObject(T o) => Pool.Add(o);

        public int Size => Pool.Count;
    }
}
