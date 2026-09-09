using IL2CppApi.Wrappers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace IL2CppApi.Runtime
{
    internal sealed class Il2CppMetadataCache
    {
        private static readonly object SharedLock = new object();
        private static Il2CppMetadataCache _shared;
        internal static Il2CppMetadataCache Shared
        {
            get
            {
                lock (SharedLock) return _shared ??= new Il2CppMetadataCache();
            }
        }

        internal readonly NativeApi Api;
        internal readonly int ArrayDataOffset;
        private readonly ConcurrentDictionary<string, IL2CppImageWrapper> _images =
            new ConcurrentDictionary<string, IL2CppImageWrapper>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<IntPtr, Dictionary<(string Namespace, string Name), IL2CppClassWrapper>> _classes =
            new ConcurrentDictionary<IntPtr, Dictionary<(string Namespace, string Name), IL2CppClassWrapper>>();
        private readonly ConcurrentDictionary<(IntPtr Owner, string Name), IL2CppClassWrapper> _nestedClasses =
            new ConcurrentDictionary<(IntPtr Owner, string Name), IL2CppClassWrapper>();
        private readonly ConcurrentDictionary<(IntPtr Owner, string Name, bool Static, int ParameterCount, string Parameters), IL2CppMethodInfoWrapper> _methods =
            new ConcurrentDictionary<(IntPtr Owner, string Name, bool Static, int ParameterCount, string Parameters), IL2CppMethodInfoWrapper>();
        private readonly ConcurrentDictionary<(IntPtr Owner, string Name), IL2CppFieldInfoWrapper> _fields =
            new ConcurrentDictionary<(IntPtr Owner, string Name), IL2CppFieldInfoWrapper>();
        private readonly ConcurrentDictionary<IntPtr, TypeMetadata> _types = new ConcurrentDictionary<IntPtr, TypeMetadata>();
        private readonly ConcurrentDictionary<IntPtr, ValueLayout> _layouts = new ConcurrentDictionary<IntPtr, ValueLayout>();
        private readonly ConcurrentDictionary<IntPtr, InvocationMetadata> _invocations = new ConcurrentDictionary<IntPtr, InvocationMetadata>();
        private readonly ConcurrentDictionary<IntPtr, FieldMetadata> _fieldInfo = new ConcurrentDictionary<IntPtr, FieldMetadata>();

        private Il2CppMetadataCache()
        {
            Api = new NativeApi();
            ArrayDataOffset = checked((int)Api.ArrayHeaderSize());
            if (ArrayDataOffset < 2 * IntPtr.Size || ArrayDataOffset > 256 || ArrayDataOffset % IntPtr.Size != 0)
                throw new NotSupportedException("The runtime reported an invalid IL2CPP array header size.");
        }

        internal IL2CppClassWrapper Class(string assembly, string ns, string name, bool optional)
        {
            var image = Image(assembly);
            IL2CppClassWrapper result = null;
            if (image != null)
            {
                if (!_classes.TryGetValue(image.Ptr, out var classes))
                    classes = _classes.GetOrAdd(image.Ptr, IndexClasses(image));
                classes.TryGetValue((ns, name), out result);
            }
            if (result == null && !optional)
                throw new NotSupportedException("IL2CPP type not found: " + assembly + ":" + ns + "." + name);
            return result;
        }

        private IL2CppImageWrapper Image(string name)
        {
            if (_images.TryGetValue(name, out var image)) return image;

            foreach (var loaded in IL2CppDumper.GetLoadedAssemblies())
                if (loaded != null && !loaded.IsNull) _images.TryAdd(loaded.Name, loaded);
            _images.TryGetValue(name, out image);
            return image;
        }

        private static Dictionary<(string Namespace, string Name), IL2CppClassWrapper> IndexClasses(IL2CppImageWrapper image)
        {
            var classes = new Dictionary<(string Namespace, string Name), IL2CppClassWrapper>();
            foreach (var type in image.GetClasses())
            {
                if (type == null || type.IsNull) continue;
                var key = (type.Namespace, type.Name);
                if (!classes.ContainsKey(key)) classes.Add(key, type); 
            }
            return classes;
        }

        internal IL2CppClassWrapper NestedClass(IL2CppClassWrapper owner, string name, bool optional)
        {
            RequireClass(owner);
            var key = (owner.Ptr, name);
            if (_nestedClasses.TryGetValue(key, out var result)) return result;
            var matches = owner.GetNestedTypes().Where(t => t.Name == name).ToArray();
            if (matches.Length > 1) throw new NotSupportedException("Ambiguous IL2CPP nested type: " + owner.Name + "." + name);
            if (matches.Length == 1) return _nestedClasses.GetOrAdd(key, matches[0]);
            if (optional) return null;
            throw new NotSupportedException("IL2CPP nested type not found: " + owner.Name + "." + name);
        }

        internal IL2CppMethodInfoWrapper Method(IL2CppClassWrapper owner, string name, bool isStatic, string[] parameters)
        {
            RequireClass(owner);
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            var key = (owner.Ptr, name, isStatic, parameters.Length, string.Join("\0", parameters));
            if (_methods.TryGetValue(key, out var cached)) return cached;
            for (var type = owner; type != null && !type.IsNull; type = type.Parent)
            {
                var matches = type.GetMethods().Where(m => m.Name == name && !m.IsGeneric &&
                    ((m.Attributes & MethodAttributes.Static) != 0) == isStatic && m.ParamCount == parameters.Length &&
                    m.GetParameters().Select(p => p.Name).SequenceEqual(parameters)).ToArray();
                if (matches.Length > 1) throw new NotSupportedException("Ambiguous IL2CPP method: " + owner.Name + "." + name);
                if (matches.Length == 1) return _methods.GetOrAdd(key, matches[0]);
            }
            throw new NotSupportedException("IL2CPP method not found: " + owner.Namespace + "." + owner.Name + "." + name +
                "(" + string.Join(", ", parameters) + "). The runtime version may be incompatible.");
        }

        internal IL2CppFieldInfoWrapper Field(IL2CppClassWrapper owner, string name, bool optional)
        {
            RequireClass(owner);
            var key = (owner.Ptr, name);
            if (_fields.TryGetValue(key, out var cached)) return cached;
            for (var type = owner; type != null && !type.IsNull; type = type.Parent)
            {
                var field = type.GetFields().FirstOrDefault(f => f.Name == name && (f.Attributes & FieldAttributes.Static) == 0);
                if (field != null) return _fields.GetOrAdd(key, field);
            }
            if (optional) return null;
            throw new NotSupportedException("IL2CPP field not found: " + owner.Name + "." + name);
        }

        internal TypeMetadata Type(IL2CppTypeWrapper type)
        {
            if (type == null || type.IsNull) throw new ArgumentNullException(nameof(type));
            if (_types.TryGetValue(type.Ptr, out var cached)) return cached;
            var klass = IL2CppClassWrapper.GetClassFromType(type);
            RequireClass(klass);
            return _types.GetOrAdd(type.Ptr, new TypeMetadata(klass, Layout(klass.Ptr)));
        }

        internal ValueLayout Layout(IntPtr type)
        {
            if (type == IntPtr.Zero) throw new ArgumentException("An IL2CPP class pointer is required.", nameof(type));
            if (_layouts.TryGetValue(type, out var cached)) return cached;
            var isValue = Api.IsValueType(type);
            return _layouts.GetOrAdd(type, new ValueLayout(isValue, isValue ? Api.ValueSize(type, out _) : 0));
        }

        internal InvocationMetadata Invocation(IL2CppMethodInfoWrapper method)
        {
            if (_invocations.TryGetValue(method.Ptr, out var cached)) return cached;
            var parameters = method.GetParameters().Select(Type).ToArray();
            var result = new InvocationMetadata(method.Name, (method.Attributes & MethodAttributes.Static) != 0,
                IL2CppClassWrapper.GetMethodDeclaringType(method), parameters);
            return _invocations.GetOrAdd(method.Ptr, result);
        }

        internal FieldMetadata FieldInfo(IL2CppFieldInfoWrapper field)
        {
            if (field == null || field.IsNull) throw new ArgumentNullException(nameof(field));
            if (_fieldInfo.TryGetValue(field.Ptr, out var cached)) return cached;
            var result = new FieldMetadata(field.Name, (field.Attributes & FieldAttributes.Static) != 0,
                IL2CppClassWrapper.GetFieldParent(field), Type(field.Type));
            return _fieldInfo.GetOrAdd(field.Ptr, result);
        }

        private static void RequireClass(IL2CppClassWrapper type)
        {
            if (type == null || type.IsNull) throw new ArgumentException("An IL2CPP class is required.", nameof(type));
        }

        internal readonly struct ValueLayout
        {
            internal readonly bool IsValueType;
            internal readonly int Size;
            internal ValueLayout(bool isValueType, int size) { IsValueType = isValueType; Size = size; }
        }

        internal sealed class TypeMetadata
        {
            internal readonly IL2CppClassWrapper Class;
            internal readonly ValueLayout Layout;
            internal TypeMetadata(IL2CppClassWrapper type, ValueLayout layout) { Class = type; Layout = layout; }
        }

        internal sealed class InvocationMetadata
        {
            internal readonly string Name;
            internal readonly bool IsStatic;
            internal readonly IL2CppClassWrapper DeclaringType;
            internal readonly TypeMetadata[] Parameters;

            internal InvocationMetadata(string name, bool isStatic, IL2CppClassWrapper declaringType, TypeMetadata[] parameters)
            {
                Name = name; IsStatic = isStatic; DeclaringType = declaringType; Parameters = parameters;
            }
        }

        internal sealed class FieldMetadata
        {
            internal readonly string Name;
            internal readonly bool IsStatic;
            internal readonly IL2CppClassWrapper DeclaringType;
            internal readonly TypeMetadata Type;

            internal FieldMetadata(string name, bool isStatic, IL2CppClassWrapper declaringType, TypeMetadata type)
            {
                Name = name; IsStatic = isStatic; DeclaringType = declaringType; Type = type;
            }
        }
    }
}
