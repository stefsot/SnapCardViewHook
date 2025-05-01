using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SnapCardViewHook.Core.Helpers
{
    internal static class JitHelper
    {
        public static void PrepareAllMethods(Type type)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                   BindingFlags.Instance | BindingFlags.Static |
                                                   BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                if (method.ContainsGenericParameters)
                    continue;

                var handle = method.MethodHandle;

                if (method.IsGenericMethodDefinition)
                    continue;

                RuntimeHelpers.PrepareMethod(handle);
            }
        }
    }
}
