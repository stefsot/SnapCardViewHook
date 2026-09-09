using System;

namespace IL2CppApi.Runtime
{
    public struct Il2CppArgument
    {
        public IntPtr Reference;
        public byte[] ValueBytes;
        public static Il2CppArgument Ref(IntPtr value) => new Il2CppArgument { Reference = value };
        public static unsafe Il2CppArgument Value<T>(T value) where T : unmanaged
        {
            var data = new byte[sizeof(T)];
            fixed (byte* ptr = data) *(T*)ptr = value;
            return new Il2CppArgument { ValueBytes = data };
        }
    }
}
