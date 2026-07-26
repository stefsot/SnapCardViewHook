using IL2CppApi.Wrappers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
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

        internal static IntPtr GetStaticFieldValue(IL2CppFieldInfoWrapper fieldInfo)
        {
            Debug.Assert(fieldInfo.Attributes.HasFlag(FieldAttributes.Static));
            return GetStaticFieldValue(fieldInfo.Ptr);
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

        internal static unsafe void EnumerateList(IL2CppList* l, Action<IntPtr, int> callback)
        {
            if (l == null)
                return;

            if (l->Size == 0)
                return;

            var v = &l->Array->vector;

            for (var i = 0; i < l->Size; i++)
            {
                var item = v[i];
                callback(new IntPtr(item), i);
            }
        }

        internal static unsafe T[] ListToArray<T>(IL2CppList* l)
            where T : unmanaged
        {
            if (l == null || l->Array == null || l->Size <= 0)
                return Array.Empty<T>();

            var items = new T[l->Size];
            var vector = (T*)&l->Array->vector;

            for (var i = 0; i < l->Size; i++)
                items[i] = vector[i];

            return items;
        }

        internal static unsafe IntPtr[] ListToArray(IL2CppList* l)
        {
            return ListToArray<IntPtr>(l);
        }

        internal static unsafe T[] ArrayToArray<T>(IL2CppArray* array)
            where T : unmanaged
        {
            if (array == null || array->Count <= 0)
                return Array.Empty<T>();

            var items = new T[array->Count];
            var vector = (T*)&array->vector;

            for (var i = 0; i < array->Count; i++)
                items[i] = vector[i];

            return items;
        }
    }
}
