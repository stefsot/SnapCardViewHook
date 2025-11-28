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
    }

    public unsafe struct IL2CppDictionary_Entry
    {
        public int hashCode;
        public int next;
        public void* key;
        public void* value;
    };
}
