using IL2CppApi.Runtime;
using IL2CppApi.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SnapCardViewHook.Core.AssetLab
{
    internal static class AssetLabNative
    {
        private static readonly Dictionary<(IntPtr, string, int), IL2CppMethodInfoWrapper> Methods =
            new Dictionary<(IntPtr, string, int), IL2CppMethodInfoWrapper>();

        public static IL2CppClassWrapper DefType(Il2CppRuntime runtime, string name) =>
            runtime.Class("SecondDinner.CubeDef.dll", "CubeDef", name);

        public static IL2CppClassWrapper RenderType(Il2CppRuntime runtime, string name) =>
            runtime.Class("SecondDinner.CubeRendering.Card.dll", "SecondDinner.CubeRendering.Card", name);

        public static IL2CppClassWrapper ViewType(Il2CppRuntime runtime, string name) =>
            runtime.Class("App.View.dll", "CubeUnity.App.View", name);

        public static IL2CppClassWrapper EngineType(Il2CppRuntime runtime, string name) =>
            runtime.Class("UnityEngine.CoreModule.dll", "UnityEngine", name);

        public static IL2CppMethodInfoWrapper Method(IL2CppClassWrapper owner, string name, int count = 0)
        {
            var key = (owner.Ptr, name, count);
            if (Methods.TryGetValue(key, out var cached)) return cached;
            for (var type = owner; type != null && !type.IsNull; type = type.Parent)
            {
                var methods = type.GetMethods().Where(m => !m.IsGeneric && m.ParamCount == count &&
                    (m.Attributes & MethodAttributes.Static) == 0).ToArray();
                var matches = methods.Where(m => m.Name == name).ToArray();
                if (matches.Length == 0) matches = methods.Where(m => m.Name.EndsWith("." + name, StringComparison.Ordinal)).ToArray();
                if (matches.Length == 1) return Methods[key] = matches[0];
                if (matches.Length > 1) throw new NotSupportedException("Ambiguous client method: " + owner.Name + "." + name);
            }
            throw new NotSupportedException("Client method not found: " + owner.Name + "." + name);
        }

        public static IntPtr Get(Il2CppRuntime runtime, IntPtr instance, string property) =>
            runtime.Invoke(Method(runtime.ObjectClass(instance), "get_" + property), instance);

        public static string Text(Il2CppRuntime runtime, IntPtr instance, string property) =>
            runtime.StringValue(Get(runtime, instance, property));

        public static string FieldText(Il2CppRuntime runtime, IntPtr instance, string fieldName)
        {
            var field = runtime.Field(runtime.ObjectClass(instance), fieldName);
            return runtime.StringValue(runtime.ReadReference(instance, field));
        }

        public static string Id(Il2CppRuntime runtime, IntPtr instance, string fieldName)
        {
            var field = runtime.Field(runtime.ObjectClass(instance), fieldName);
            return runtime.StringValue(runtime.ClassFromType(field.Type).IsValueType
                ? runtime.ReadField<IntPtr>(instance, field) : runtime.ReadReference(instance, field));
        }

        public static void SetId(Il2CppRuntime runtime, IntPtr instance, string property, string id)
        {
            var method = Method(runtime.ObjectClass(instance), "set_" + property, 1);
            var value = runtime.String(id);
            runtime.Invoke(method, instance, runtime.ClassFromType(method.GetParameters()[0]).IsValueType
                ? Il2CppArgument.Value(value) : Il2CppArgument.Ref(value));
        }

        public static IntPtr Clone(Il2CppRuntime runtime, IntPtr instance)
        {
            if (instance == IntPtr.Zero) throw new InvalidOperationException("Open a card in the game before reconstructing assets.");
            return runtime.Invoke(runtime.Method(runtime.Class("mscorlib.dll", "System", "Object"),
                "MemberwiseClone", false), instance);
        }

        public static bool Alive(Il2CppRuntime runtime, IntPtr instance)
        {
            return instance != IntPtr.Zero && runtime.InvokeValue<bool>(runtime.Method(EngineType(runtime, "Object"),
                "op_Implicit", true, "UnityEngine.Object"), IntPtr.Zero, Il2CppArgument.Ref(instance));
        }

        public static IntPtr[] LoadedObjects(Il2CppRuntime runtime, IL2CppClassWrapper type)
        {
            return runtime.ReferenceArray(runtime.Invoke(runtime.Method(EngineType(runtime, "Resources"),
                "FindObjectsOfTypeAll", true, "System.Type"), IntPtr.Zero, Il2CppArgument.Ref(runtime.TypeObject(type))));
        }

        internal sealed class Sequence : IDisposable
        {
            private readonly Il2CppRuntime _roots = new Il2CppRuntime();
            private readonly IntPtr _iterator;
            private readonly IL2CppMethodInfoWrapper _moveNext, _current;

            public Sequence(IntPtr enumerable)
            {
                try
                {
                    _roots.Retain(enumerable);
                    var type = _roots.ObjectClass(enumerable);
                    IL2CppMethodInfoWrapper method;
                    try { method = Method(type, "System.Collections.IEnumerable.GetEnumerator"); }
                    catch (NotSupportedException) { method = Method(type, "GetEnumerator"); }
                    _iterator = _roots.Invoke(method, enumerable);
                    type = _roots.ObjectClass(_iterator);
                    _moveNext = Method(type, "MoveNext");
                    try { _current = Method(type, "System.Collections.IEnumerator.get_Current"); }
                    catch (NotSupportedException) { _current = Method(type, "get_Current"); }
                }
                catch { _roots.Dispose(); throw; }
            }

            public bool Next(Il2CppRuntime runtime, out IntPtr value)
            {
                if (!runtime.InvokeValue<bool>(_moveNext, _iterator)) { value = IntPtr.Zero; return false; }
                value = runtime.Invoke(_current, _iterator);
                return true;
            }

            public void Dispose()
            {
                try
                {
                    var type = _roots.ObjectClass(_iterator);
                    var dispose = type.GetMethods().FirstOrDefault(m => m.ParamCount == 0 &&
                        (m.Name == "Dispose" || m.Name == "System.IDisposable.Dispose"));
                    if (dispose != null) _roots.Invoke(dispose, _iterator);
                }
                finally { _roots.Dispose(); }
            }
        }
    }
}
