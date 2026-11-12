using IL2CppApi.Wrappers;
using SnapCardViewHook.Core.IL2Cpp;
using System;
using System.Collections.Generic;

namespace SnapCardViewHook.Core.Overrides
{
    internal sealed class CardOverrideCatalog
    {
        public Dictionary<string, IntPtr> Cards { get; } = new Dictionary<string, IntPtr>(StringComparer.Ordinal);
        public Dictionary<string, IntPtr> Variants { get; } = new Dictionary<string, IntPtr>(StringComparer.Ordinal);
        public Dictionary<string, IntPtr> SurfaceEffects { get; } = new Dictionary<string, IntPtr>(StringComparer.Ordinal);
        public Dictionary<string, IntPtr> RevealEffects { get; } = new Dictionary<string, IntPtr>(StringComparer.Ordinal);
        public Dictionary<string, IntPtr> Borders { get; } = new Dictionary<string, IntPtr>(StringComparer.Ordinal);
        public Dictionary<string, IntPtr> CardBacks { get; } = new Dictionary<string, IntPtr>(StringComparer.Ordinal);
        public Dictionary<string, IntPtr> Factions { get; } = new Dictionary<string, IntPtr>(StringComparer.Ordinal);

        public static unsafe CardOverrideCatalog FromClient()
        {
            var catalog = new CardOverrideCatalog();
            ReadFields(catalog.Cards, SnapTypeDataCollector.CardDef_Id_Fields);
            ReadFields(catalog.Variants, SnapTypeDataCollector.ArtVariantDef_Id_Fields);
            ReadFields(catalog.SurfaceEffects, SnapTypeDataCollector.SurfaceEffectDef_Id_Fields);
            ReadFields(catalog.RevealEffects, SnapTypeDataCollector.CardRevealEffectDef_Id_Fields);
            ReadFields(catalog.CardBacks, SnapTypeDataCollector.CardBackDefId_Fields);
            ReadFields(catalog.Factions, SnapTypeDataCollector.FactionDef_Id_Fields);
            foreach (var border in IL2CppHelper.ListToArray((IL2CppList*)SnapTypeDataCollector.BorderDefList_Defs_cached_value))
                Add(catalog.Borders, border);
            return catalog;
        }

        private static void ReadFields(Dictionary<string, IntPtr> values, IL2CppFieldInfoWrapper[] fields)
        {
            foreach (var field in fields ?? Array.Empty<IL2CppFieldInfoWrapper>())
                Add(values, IL2CppHelper.GetStaticFieldValue(field.Ptr));
        }

        private static void Add(Dictionary<string, IntPtr> values, IntPtr pointer)
        {
            var id = new IL2CppStringRef(pointer).GetObject();
            if (!string.IsNullOrEmpty(id)) values[id] = pointer;
        }

        public static IntPtr Find(Dictionary<string, IntPtr> values, string id, IntPtr fallback)
        {
            return id != null && values.TryGetValue(id, out var value) ? value : fallback;
        }
    }
}
