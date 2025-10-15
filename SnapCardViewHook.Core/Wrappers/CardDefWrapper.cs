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
                var _ = *(void**)(Ptr + SnapTypeDataCollector.CardDef_Name_Field_Offset);
                return new IL2CppStringRef(_).GetObject();
            }
        }

        public string Description
        {
            get
            {
                var _ = *(void**)(Ptr + SnapTypeDataCollector.CardDef_Description_Field_Offset);
                return new IL2CppStringRef(_).GetObject();
            }
        }

        public IntPtr CardDefId => *(IntPtr*)(Ptr + SnapTypeDataCollector.CardDef_CardDefId_Field_Offset);

        public int Cost => *(int*)(Ptr + SnapTypeDataCollector.CardDef_Cost_Field_Offset);

        public int Power => *(int*)(Ptr + SnapTypeDataCollector.CardDef_Power_Field_Offset);

        public IL2CppList* SeriesStartDates => *(IL2CppList**)(Ptr + SnapTypeDataCollector.CardDef_SeriesStartDates_Field_Offset);

        //
        //

        public string GetId()
        {
            return new IL2CppStringRef(CardDefId).GetObject();
        }

        public bool IsObtainable()
        {
            if (SeriesStartDates == null)
                return false;

            if (SeriesStartDates->Size == 0)
                return false;

            return true;
        }

        public DateTime GetEarliestEnabledDate()
        {
            if (SeriesStartDates == null)
                return DateTime.MinValue;

            if (SeriesStartDates->Size == 0)
                return DateTime.MinValue;

            var array = &SeriesStartDates->Array->vector;
            var date = DateTime.MaxValue;

            for (var i = 0; i < SeriesStartDates->Size; i++)
            {
                // item is of type public class TimedCardSeries : TimedData<CardSeriesDefId>
                var item = array[i];
                // 0x18 is offset for <StartDate>k__BackingField
                var d = *(DateTime*)(new IntPtr(item) + 0x18); 

                if (d < date) 
                    date = d;
            }

            return date;
        }
    }
}
