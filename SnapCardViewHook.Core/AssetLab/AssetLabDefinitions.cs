using SnapCardViewHook.Core.Helpers;
using SnapCardViewHook.Core.IL2Cpp;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace SnapCardViewHook.Core.AssetLab
{
    internal static class AssetLabDefinitions
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr FindCardDelegate(IntPtr id, IntPtr methodInfo);

        private static FindCardDelegate _original;
        private static Definition _active;

        public static void EnsureInstalled()
        {
            if (_original != null) return;
            _original = HookManager.CreateHook(SnapTypeDataCollector.CardDefList_Find_methodPtr,
                new FindCardDelegate(FindCard), "Asset Lab missing card definition");
        }

        public static void Set(object owner, string id, IntPtr card)
        {
            Volatile.Write(ref _active, new Definition { Owner = owner, Id = id, Card = card });
        }

        public static void Clear(object owner)
        {
            var active = Volatile.Read(ref _active);
            if (active != null && ReferenceEquals(active.Owner, owner))
                Interlocked.CompareExchange(ref _active, null, active);
        }

        private static IntPtr FindCard(IntPtr id, IntPtr methodInfo)
        {
            var original = _original;
            if (original == null) return IntPtr.Zero;
            var card = original(id, methodInfo);
            if (card != IntPtr.Zero || id == IntPtr.Zero) return card;
            var active = Volatile.Read(ref _active);
            if (active == null) return card;
            return string.Equals(new IL2CppStringRef(id).GetObject(), active.Id, StringComparison.Ordinal)
                ? active.Card : card;
        }

        private sealed class Definition
        {
            public object Owner;
            public string Id;
            public IntPtr Card;
        }
    }
}
