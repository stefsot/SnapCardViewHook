using SnapCardViewHook.Core.IL2Cpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapCardViewHook.Core.Wrappers
{
    internal unsafe class CardDefWrapper : MonoObjectWrapper
    {
        public CardDefWrapper(IntPtr ptr) : base(ptr)
        {
        }

        public CardDefWrapper(void* ptr) : base(ptr)
        {
        }

        public string Name
        {
            get
            {
                var strPtr = *(void**)(Ptr + SnapTypeDataCollector.CardDef_Name_Field_Offset);
                return new IL2CppStringRef(strPtr).GetObject();
            }
        }

        public int CardDefId => *(int*)(Ptr + SnapTypeDataCollector.CardDef_CardDefId_Field_Offset);

        public int Cost => *(int*)(Ptr + SnapTypeDataCollector.CardDef_Cost_Field_Offset);

        public int Power => *(int*)(Ptr + SnapTypeDataCollector.CardDef_Power_Field_Offset);
    }
}
