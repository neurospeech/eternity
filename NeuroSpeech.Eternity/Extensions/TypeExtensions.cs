using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace NeuroSpeech.Eternity.Extensions
{
    internal static class TypeExtensions
    {

        public static MethodInfo GetVirtualMethod(this Type type, string name)
        {
            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Default | BindingFlags.FlattenHierarchy))
            {
                if (!m.IsVirtual)
                    continue;
                if (m.Name == name)
                    return m;
            }
            throw new MethodAccessException($"Method {name} not found on type {type.FullName}");
        }

    }
}
