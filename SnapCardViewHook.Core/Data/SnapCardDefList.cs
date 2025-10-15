using SnapCardViewHook.Core.IL2Cpp;
using SnapCardViewHook.Core.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapCardViewHook.Core.Data
{
    internal static class SnapCardDefList
    {
        public static IReadOnlyList<CardDefWrapper> Cards { get; private set; } = null;

        static SnapCardDefList()
        {
            // SnapTypeDataCollector may throw
            // consider moving it outside static constructor
            Initialize();
        }

        public static IntPtr FindCard(IntPtr cardDefId)
        {
            return SnapTypeDataCollector.CardDefList_Find(cardDefId);
        }

        private static void Initialize()
        {
            if (Cards != null)
                return;

            SnapTypeDataCollector.EnsureLoaded();

            if (SnapTypeDataCollector.CardDef_Id_Fields == null)
                return;

            Cards = SnapTypeDataCollector.CardDef_Id_Fields
                .Select(id => FindCard(IL2CppHelper.GetStaticFieldValue(id.Ptr)))
                .Where(c => c != IntPtr.Zero)
                .Select(c => new CardDefWrapper(c))
                .ToList();
        }
    }
}
