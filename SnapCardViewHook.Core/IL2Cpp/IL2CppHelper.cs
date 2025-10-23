using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
// ReSharper disable InconsistentNaming

namespace SnapCardViewHook.Core.IL2Cpp
{
    internal unsafe class IL2CppHelper
    {
        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate void delegate_il2cpp_field_static_get_value(void* field, void* value);
        private delegate IntPtr delegate_il2cpp_string_new_utf16(char* c, int len);

        private static readonly IntPtr GameAssemblyHandle;
        private static readonly delegate_il2cpp_field_static_get_value il2cpp_field_static_get_value;
        private static readonly delegate_il2cpp_string_new_utf16 il2cpp_string_new_utf16;

        private static T MakeApi<T>(string api)
        {
            return Marshal.GetDelegateForFunctionPointer<T>(GetProcAddress(GameAssemblyHandle, api));
        }

        static IL2CppHelper()
        {
            GameAssemblyHandle = GetModuleHandle("GameAssembly.dll");
            il2cpp_field_static_get_value = MakeApi<delegate_il2cpp_field_static_get_value>("il2cpp_field_static_get_value");
            il2cpp_string_new_utf16 = MakeApi<delegate_il2cpp_string_new_utf16>("il2cpp_string_new_utf16");
        }

        internal static void GetStaticFieldValue(void* fieldInfo, void* value)
        {
            il2cpp_field_static_get_value(fieldInfo, value);
        }

        internal static IntPtr GetStaticFieldValue(IntPtr fieldInfo)
        {
            IntPtr value;
            GetStaticFieldValue((void*)fieldInfo, &value);
            return value;
        }

        internal static unsafe IntPtr NewString(string s)
        {
            fixed (char* c = s)
                return il2cpp_string_new_utf16(c, s.Length);
        }


        internal static IntPtr GetModuleHandle()
        {
            return GameAssemblyHandle;
        }

        internal static unsafe IntPtr[] EnumerateList(IL2CppList* l)
        {
            if (l == null)
                return Array.Empty<IntPtr>();

            if (l->Size == 0)
                return Array.Empty<IntPtr>();

            var v = &l->Array->vector;
            var items = new IntPtr[l->Size];

            for (var i = 0; i < l->Size; i++)
            {
                var item = v[i];

                if (item == null)
                    break;

               
                items[i] = new IntPtr(item);
            }

            return items;
        }
    }
}
