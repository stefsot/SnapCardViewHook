using System;
using System.Runtime.InteropServices;
using System.Text;

namespace IL2CppApi.Runtime
{
    internal sealed class NativeApi
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)] private static extern IntPtr GetProcAddress(IntPtr module, string name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr InvokeDelegate(IntPtr method, IntPtr obj, IntPtr args, out IntPtr exception);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr PointerDelegate(IntPtr value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void VoidDelegate(IntPtr value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int IntDelegate(IntPtr value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal delegate bool BoolDelegate(IntPtr value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal delegate bool AssignableDelegate(IntPtr parent, IntPtr child);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ValueSizeDelegate(IntPtr type, out uint alignment);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void FieldGetDelegate(IntPtr obj, IntPtr field, IntPtr output);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void FieldSetObjectDelegate(IntPtr obj, IntPtr field, IntPtr value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr HandleDelegate(IntPtr obj, [MarshalAs(UnmanagedType.I1)] bool pinned);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void FreeHandleDelegate(IntPtr handle);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)] internal delegate IntPtr StringDelegate([MarshalAs(UnmanagedType.LPWStr)] string text, int length);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)] internal delegate void ExceptionDelegate(IntPtr exception, StringBuilder text, int length);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate uint LengthDelegate(IntPtr array);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate uint HeaderSizeDelegate();

        private readonly IntPtr _module = GetModuleHandle("GameAssembly.dll");
        internal readonly InvokeDelegate Invoke;
        internal readonly PointerDelegate ObjectClass, ClassName, ClassNamespace, Unbox, NewObject, TypeObject, ElementClass, StringChars;
        internal readonly VoidDelegate InitializeClass;
        internal readonly IntDelegate ElementSize, StringLength;
        internal readonly BoolDelegate IsValueType;
        internal readonly AssignableDelegate Assignable;
        internal readonly ValueSizeDelegate ValueSize;
        internal readonly FieldGetDelegate FieldGet;
        internal readonly FieldSetObjectDelegate FieldSetObject;
        internal readonly HandleDelegate NewHandle;
        internal readonly FreeHandleDelegate FreeHandle;
        internal readonly StringDelegate NewString;
        internal readonly ExceptionDelegate FormatException;
        internal readonly LengthDelegate ArrayLength;
        internal readonly HeaderSizeDelegate ArrayHeaderSize;

        internal NativeApi()
        {
            Invoke = Bind<InvokeDelegate>("il2cpp_runtime_invoke");
            ObjectClass = Bind<PointerDelegate>("il2cpp_object_get_class");
            ClassName = Bind<PointerDelegate>("il2cpp_class_get_name");
            ClassNamespace = Bind<PointerDelegate>("il2cpp_class_get_namespace");
            Unbox = Bind<PointerDelegate>("il2cpp_object_unbox");
            NewObject = Bind<PointerDelegate>("il2cpp_object_new");
            InitializeClass = Bind<VoidDelegate>("il2cpp_runtime_class_init");
            TypeObject = Bind<PointerDelegate>("il2cpp_type_get_object");
            ElementClass = Bind<PointerDelegate>("il2cpp_class_get_element_class");
            ElementSize = Bind<IntDelegate>("il2cpp_array_element_size");
            IsValueType = Bind<BoolDelegate>("il2cpp_class_is_valuetype");
            Assignable = Bind<AssignableDelegate>("il2cpp_class_is_assignable_from");
            ValueSize = Bind<ValueSizeDelegate>("il2cpp_class_value_size");
            FieldGet = Bind<FieldGetDelegate>("il2cpp_field_get_value");
            FieldSetObject = Bind<FieldSetObjectDelegate>("il2cpp_field_set_value_object");
            NewHandle = Bind<HandleDelegate>("il2cpp_gchandle_new");
            FreeHandle = Bind<FreeHandleDelegate>("il2cpp_gchandle_free");
            NewString = Bind<StringDelegate>("il2cpp_string_new_utf16");
            StringChars = Bind<PointerDelegate>("il2cpp_string_chars");
            StringLength = Bind<IntDelegate>("il2cpp_string_length");
            FormatException = Bind<ExceptionDelegate>("il2cpp_format_exception");
            ArrayLength = Bind<LengthDelegate>("il2cpp_array_length");
            ArrayHeaderSize = Bind<HeaderSizeDelegate>("il2cpp_array_object_header_size");
        }

        private T Bind<T>(string name) where T : Delegate
        {
            var address = _module == IntPtr.Zero ? IntPtr.Zero : GetProcAddress(_module, name);
            if (address == IntPtr.Zero) throw new NotSupportedException("Required IL2CPP export is unavailable: " + name);
            return Marshal.GetDelegateForFunctionPointer<T>(address);
        }
    }
}
