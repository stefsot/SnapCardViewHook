using IL2CppApi.Wrappers;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace IL2CppApi.Runtime
{
    public sealed unsafe class Il2CppRuntime : IDisposable
    {
        private readonly Il2CppMetadataCache _metadata;
        private readonly NativeApi _api;
        private readonly int _thread = Thread.CurrentThread.ManagedThreadId;
        private readonly List<IntPtr> _handles = new List<IntPtr>();
        private readonly HashSet<IntPtr> _rooted = new HashSet<IntPtr>();
        private readonly int _arrayDataOffset;
        private bool _disposed;

        public Il2CppRuntime()
        {
            if (IntPtr.Size != 8) throw new NotSupportedException("This IL2CPP runtime bridge requires an x64 process.");
            _metadata = Il2CppMetadataCache.Shared;
            _api = _metadata.Api;
            _arrayDataOffset = _metadata.ArrayDataOffset;
        }

        public IL2CppClassWrapper Class(string assembly, string ns, string name, bool optional = false)
        {
            CheckThread();
            return _metadata.Class(assembly, ns, name, optional);
        }

        public IL2CppClassWrapper NestedClass(IL2CppClassWrapper owner, string name, bool optional = false)
        {
            CheckThread();
            return _metadata.NestedClass(owner, name, optional);
        }

        public IL2CppClassWrapper ClassFromType(IL2CppTypeWrapper type)
        {
            CheckThread();
            return _metadata.Type(type).Class;
        }

        public IL2CppMethodInfoWrapper Method(IL2CppClassWrapper owner, string name, bool isStatic, params string[] parameters)
        {
            CheckThread();
            return _metadata.Method(owner, name, isStatic, parameters);
        }

        public IL2CppFieldInfoWrapper Field(IL2CppClassWrapper owner, string name, bool optional = false)
        {
            CheckThread();
            return _metadata.Field(owner, name, optional);
        }

        public IntPtr Invoke(IL2CppMethodInfoWrapper method, IntPtr instance, params Il2CppArgument[] args) =>
            InvokeCore(method, instance, args, temporaryResult: false, out _);
        
        public T InvokeValue<T>(IL2CppMethodInfoWrapper method, IntPtr instance, params Il2CppArgument[] args) where T : unmanaged
        {
            var handle = IntPtr.Zero;
            try
            {
                var boxed = InvokeCore(method, instance, args, temporaryResult: true, out handle);
                return Value<T>(boxed);
            }
            finally
            {
                if (handle != IntPtr.Zero) _api.FreeHandle(handle);
            }
        }

        public byte[] InvokeByteArray(IL2CppMethodInfoWrapper method, IntPtr instance, int expectedLength, params Il2CppArgument[] args)
        {
            var handle = IntPtr.Zero;
            try
            {
                var array = InvokeCore(method, instance, args, temporaryResult: true, out handle);
                return ByteArray(array, expectedLength);
            }
            finally
            {
                if (handle != IntPtr.Zero) _api.FreeHandle(handle);
            }
        }

        public void InvokeByteArrayInto(IL2CppMethodInfoWrapper method, IntPtr instance, byte[] destination, params Il2CppArgument[] args)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            var handle = IntPtr.Zero;
            try
            {
                var array = InvokeCore(method, instance, args, temporaryResult: true, out handle);
                CopyByteArray(array, destination);
            }
            finally
            {
                if (handle != IntPtr.Zero) _api.FreeHandle(handle);
            }
        }

        private IntPtr InvokeCore(IL2CppMethodInfoWrapper method, IntPtr instance, Il2CppArgument[] args,
            bool temporaryResult, out IntPtr temporaryHandle)
        {
            temporaryHandle = IntPtr.Zero;
            CheckThread();
            if (method == null || method.IsNull) throw new ArgumentNullException(nameof(method));
            if (args == null) throw new ArgumentNullException(nameof(args));
            var signature = _metadata.Invocation(method);
            if (signature.Parameters.Length != args.Length) throw new ArgumentException("Incorrect IL2CPP argument count.");
            if (!signature.IsStatic && !IsInstance(instance, signature.DeclaringType))
                throw new InvalidOperationException("Invalid IL2CPP object type for " + signature.Name + ".");
            var pins = new List<GCHandle>();
            try
            {
                IntPtr* argv = stackalloc IntPtr[args.Length];
                for (var i = 0; i < args.Length; i++)
                {
                    var parameter = signature.Parameters[i];
                    var isValue = parameter.Layout.IsValueType;
                    if (isValue != (args[i].ValueBytes != null))
                        throw new ArgumentException("Reference/value argument mismatch for " + signature.Name + ".");
                    if (isValue)
                    {
                        if (parameter.Layout.Size != args[i].ValueBytes.Length)
                            throw new NotSupportedException("Native argument size mismatch for " + signature.Name + ".");
                        var pin = GCHandle.Alloc(args[i].ValueBytes, GCHandleType.Pinned);
                        pins.Add(pin);
                        argv[i] = pin.AddrOfPinnedObject();
                    }
                    else
                    {
                        if (args[i].Reference != IntPtr.Zero && !IsInstance(args[i].Reference, parameter.Class))
                            throw new ArgumentException("Native reference type mismatch for " + signature.Name + ".");
                        
                        argv[i] = args[i].Reference;
                    }
                }
                var result = _api.Invoke(method.Ptr, instance, args.Length == 0 ? IntPtr.Zero : (IntPtr)argv, out var error);
                if (error != IntPtr.Zero)
                {
                    var message = new StringBuilder(2048);
                    _api.FormatException(error, message, message.Capacity);
                    throw new InvalidOperationException("IL2CPP " + signature.Name + " failed: " + message);
                }
                if (!temporaryResult) return Keep(result);
                if (result != IntPtr.Zero)
                {
                    temporaryHandle = _api.NewHandle(result, false);
                    if (temporaryHandle == IntPtr.Zero) throw new OutOfMemoryException("Could not root the IL2CPP return value.");
                }
                return result;
            }
            finally { foreach (var pin in pins) pin.Free(); }
        }

        public T Value<T>(IntPtr boxed) where T : unmanaged
        {
            CheckThread();
            if (boxed == IntPtr.Zero) throw new InvalidOperationException("IL2CPP returned a null value.");
            var layout = _metadata.Layout(_api.ObjectClass(boxed));
            if (!layout.IsValueType || layout.Size != sizeof(T))
                throw new NotSupportedException("Unexpected native value layout for " + typeof(T).Name + ".");
            return *(T*)_api.Unbox(boxed);
        }

        public T ReadField<T>(IntPtr instance, IL2CppFieldInfoWrapper field) where T : unmanaged
        {
            CheckThread();
            if (instance == IntPtr.Zero) throw new InvalidOperationException("Cannot read a null IL2CPP object.");
            if (field == null || field.IsNull) throw new ArgumentNullException(nameof(field));
            var metadata = _metadata.FieldInfo(field);
            if (!metadata.Type.Layout.IsValueType || metadata.Type.Layout.Size != sizeof(T))
                throw new NotSupportedException("Unexpected field layout: " + metadata.Name);
            T value = default;
            _api.FieldGet(instance, field.Ptr, (IntPtr)(&value));
            return value;
        }

        public IntPtr ReadReference(IntPtr instance, IL2CppFieldInfoWrapper field)
        {
            CheckThread();
            if (field == null) return IntPtr.Zero;
            var metadata = _metadata.FieldInfo(field);
            if (instance == IntPtr.Zero || metadata.Type.Layout.IsValueType)
                throw new InvalidOperationException("Invalid reference field read: " + metadata.Name);
            IntPtr value = IntPtr.Zero;
            _api.FieldGet(instance, field.Ptr, (IntPtr)(&value));
            return Keep(value);
        }

        public void WriteReference(IntPtr instance, IL2CppFieldInfoWrapper field, IntPtr value)
        {
            CheckThread();
            if (field == null || field.IsNull) throw new ArgumentNullException(nameof(field));
            var metadata = _metadata.FieldInfo(field);
            if (metadata.IsStatic || metadata.Type.Layout.IsValueType || !IsInstance(instance, metadata.DeclaringType))
                throw new InvalidOperationException("Invalid reference field write: " + metadata.Name);
            if (value != IntPtr.Zero && !IsInstance(value, metadata.Type.Class))
                throw new ArgumentException("Native reference type mismatch for field " + metadata.Name + ".", nameof(value));
            
            _api.FieldSetObject(instance, field.Ptr, value);
        }

        public IntPtr NewObject(IL2CppClassWrapper type)
        {
            CheckThread();
            _api.InitializeClass(type.Ptr);
            var result = Keep(_api.NewObject(type.Ptr));
            if (result == IntPtr.Zero) throw new OutOfMemoryException("Could not allocate " + type.Name);
            return result; 
        }

        public IntPtr TypeObject(IL2CppClassWrapper type) { CheckThread(); return Keep(_api.TypeObject(type.Type.Ptr)); }
        public IntPtr String(string text) { CheckThread(); return Keep(_api.NewString(text, text.Length)); }
        public string StringValue(IntPtr value)
        {
            CheckThread();
            return value == IntPtr.Zero ? null : Marshal.PtrToStringUni(_api.StringChars(value), _api.StringLength(value));
        }

        public bool IsInstance(IntPtr value, IL2CppClassWrapper type)
        {
            CheckThread();
            return value != IntPtr.Zero && _api.Assignable(type.Ptr, _api.ObjectClass(value));
        }

        public string ObjectTypeName(IntPtr value)
        {
            CheckThread();
            if (value == IntPtr.Zero) return "<null>";
            var type = _api.ObjectClass(value);
            var ns = Marshal.PtrToStringAnsi(_api.ClassNamespace(type));
            var name = Marshal.PtrToStringAnsi(_api.ClassName(type));
            return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
        }

        public IntPtr[] ReferenceArray(IntPtr array, int? usedCount = null)
        {
            CheckThread();
            if (array == IntPtr.Zero) return Array.Empty<IntPtr>();
            var arrayClass = _api.ObjectClass(array);
            if (_api.IsValueType(_api.ElementClass(arrayClass)) || _api.ElementSize(arrayClass) != IntPtr.Size)
                throw new NotSupportedException("Expected a native reference array.");
            var length = checked((int)_api.ArrayLength(array));
            var count = usedCount ?? length;
            if (count < 0 || count > length || count > 200000)
                throw new InvalidOperationException("Invalid native array length.");
            var result = new IntPtr[count];
            PinArray(array);
            var data = IntPtr.Add(array, _arrayDataOffset);
            for (var i = 0; i < count; i++)
                result[i] = Keep(Marshal.ReadIntPtr(data, checked(i * IntPtr.Size)));
            return result;
        }

        public IntPtr[] ListReferences(IntPtr list, IL2CppClassWrapper type)
        {
            if (list == IntPtr.Zero) return Array.Empty<IntPtr>();
            var count = ReadField<int>(list, Field(type, "_size"));
            var array = ReadReference(list, Field(type, "_items"));
            if (array == IntPtr.Zero && count != 0) throw new InvalidOperationException("Invalid native list.");
            return ReferenceArray(array, count);
        }

        public byte[] ByteArray(IntPtr array, int expectedLength)
        {
            CheckThread();
            var result = new byte[expectedLength];
            CopyByteArray(array, result);
            return result;
        }

        public void CopyByteArray(IntPtr array, byte[] destination)
        {
            CheckThread();
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (array == IntPtr.Zero || _api.ElementSize(_api.ObjectClass(array)) != 1 ||
                _api.ArrayLength(array) != (uint)destination.Length)
                throw new InvalidOperationException("IL2CPP returned an unexpected byte array size.");
            var handle = _api.NewHandle(array, true);
            if (handle == IntPtr.Zero) throw new OutOfMemoryException("Could not pin an IL2CPP array.");
            try { Marshal.Copy(IntPtr.Add(array, _arrayDataOffset), destination, 0, destination.Length); }
            finally { _api.FreeHandle(handle); }
        }

        private void PinArray(IntPtr array)
        {
            var handle = _api.NewHandle(array, true);
            if (handle == IntPtr.Zero) throw new OutOfMemoryException("Could not pin an IL2CPP array.");
            _handles.Add(handle);
        }

        private IntPtr Keep(IntPtr value)
        {
            if (value != IntPtr.Zero && _rooted.Add(value))
            {
                var handle = _api.NewHandle(value, false);
                if (handle == IntPtr.Zero) throw new OutOfMemoryException("Could not root an IL2CPP object.");
                _handles.Add(handle);
            }
            return value;
        }

        private void CheckThread()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Il2CppRuntime));
            if (Thread.CurrentThread.ManagedThreadId != _thread)
                throw new InvalidOperationException("IL2CPP calls must stay on the thread that created this runtime.");
        }

        public void Dispose()
        {
            CheckThread();
            for (var i = _handles.Count - 1; i >= 0; i--) _api.FreeHandle(_handles[i]);
            _handles.Clear(); _rooted.Clear(); _disposed = true;
        }
    }
}
