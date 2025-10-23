using SnapCardViewHook.Core.IL2Cpp;
using System;
using System.Collections.Generic;
using System.Linq;

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

        public void* Attributes => *(void**)(Ptr + SnapTypeDataCollector.CardDef_Attributes_Field_Offset);

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

        public string[] GetTokens()
        {
            if (Attributes == null)
                return Array.Empty<string>();

            // public abstract class DefAttributes<TSelf>
            // - 0x18
            //   <Cards>k__BackingField
            var cards = *(IL2CppDictionary**)(new IntPtr(Attributes) + 0x18);

            if(cards == null)
                return Array.Empty<string>();

            if (cards->_entries == null)
                return Array.Empty<string>();

            if (cards->_entries->Count == 0)
                return Array.Empty<string>();

            var v = (IL2CppDictionary_Entry*) &cards->_entries->vector;

            for(var i = 0; i < cards->_entries->Count; i++)
            {
                var entry = v[i];

                if (entry.hashCode < 0)
                    continue;

                // compare for enum CardAttributeType.Card_Token = 0
                if (new IntPtr(entry.key) != IntPtr.Zero)
                    continue;

                var cardList = (IL2CppList*)entry.value;
                var defIds = IL2CppHelper.EnumerateList(cardList);

                return defIds.Select(p => new IL2CppStringRef(p).GetObject()).ToArray();
            }

            return Array.Empty<string>();
        }
    
    
        public Dictionary<string, int[]> GetAttributes()
        {
            var d = new Dictionary<string, int[]>();

            if (Attributes == null)
                return d;

            // public abstract class DefAttributes<TSelf>
            // - 0x10
            //   <Data>k__BackingField
            var data = *(IL2CppDictionary**)(new IntPtr(Attributes) + 0x10);

            if (data == null)
                return d;

            if (data->_entries == null)
                return d;

            if (data->_entries->Count == 0)
                return d;

            var v = (IL2CppDictionary_Entry*)&data->_entries->vector;
            var enumNames = SnapTypeDataCollector.DataAttributeType_Fields
                .Select((value, index) => new { index, value })
                .ToDictionary(x => x.index, x => x.value.Name);

            for (var i = 0; i < data->_entries->Count; i++)
            {
                var entry = v[i];

                if (entry.hashCode < 0)
                    continue;

                if(entry.value == null)
                    break;

                var values = IL2CppHelper.EnumerateList((IL2CppList*)entry.value).Select(vv => unchecked((int) vv.ToInt64())).ToArray();
                d.Add(enumNames[(int)entry.key], values);

                return d;
            }

            return d;
        }
    }
}
