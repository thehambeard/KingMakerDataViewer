//
// TO DO:
// 1. ref ReturnTyoe
// 2. Nullable<T> support
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace ModMaker.Utils
{
    public static partial class ReflectionCache
    {
        private const BindingFlags ALL_FLAGS = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Queue _cahce = new Queue();

        public static int Count => _cahce.Count;

        public static int SizeLimit { get; set;} = 1000;

        public static void Clear()
        {
            _fieldCache.Clear();
            _propertieCache.Clear();
            _methodCache.Clear();
            _cahce.Clear();
        }

        private static void EnqueueCache(object obj)
        {
            while (_cahce.Count >= SizeLimit && _cahce.Count > 0)
                _cahce.Dequeue();
            _cahce.Enqueue(obj);
        }

        private static bool IsStatic(Type type)
        {
            return type.IsAbstract && type.IsSealed;
        }

        private static TypeBuilder RequestTypeBuilder()
        {
            AssemblyName asmName = new AssemblyName(nameof(ReflectionCache) + "." + Guid.NewGuid().ToString());
            AssemblyBuilder asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndCollect);
            ModuleBuilder moduleBuilder = asmBuilder.DefineDynamicModule("<Module>");
            return moduleBuilder.DefineType("<Type>");
        }
    }
}
