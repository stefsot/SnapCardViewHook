using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SnapCardViewHook.Core.IL2Cpp
{
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct IL2CppDictionary
    {
        [FieldOffset(0x10)]
        public IL2CppArray* _buckets;
        [FieldOffset(0x18)]
        public IL2CppArray* _entries;
        [FieldOffset(0x20)]
        public int _count;
        [FieldOffset(0x24)]
        public int _freeList;
        [FieldOffset(0x28)]
        public int _freeCount;
        [FieldOffset(0x2C)]
        public int _version;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IL2CppDictionary_Entry<TKey, TValue>
        where TKey : unmanaged
        where TValue : unmanaged
    {
        public int hashCode;
        public int next;
        public TKey key;
        public TValue value;
    }
}
