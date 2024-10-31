using SnapCardViewHook.Core.IL2Cpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapCardViewHook.Core.Wrappers
{
    internal unsafe class BorderDefWrapper : MonoObjectWrapper
    {
        public BorderDefWrapper(IntPtr ptr) : base(ptr)
        {
        }

        public BorderDefWrapper(void* ptr) : base(ptr)
        {
        }

        public IntPtr BorderDefId => *(IntPtr*)(Ptr + SnapTypeDataCollector.BorderDef_BorderDefId_Field_Offset);

        public string Name
        {
            get
            {
                var strPtr = *(void**)(Ptr + SnapTypeDataCollector.BorderDef_Name_Field_Offset);
                return new IL2CppStringRef(strPtr).GetObject();
            }
        }
    }
}
