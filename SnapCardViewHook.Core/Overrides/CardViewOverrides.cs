using SnapCardViewHook.Core.IL2Cpp;
using SnapCardViewHook.Core.Wrappers;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace SnapCardViewHook.Core.Overrides
{
    internal sealed class CardViewOverrides
    {
        private readonly CardOverrideCatalog _catalog;
        private readonly CardOverrideStore _store;
        private readonly ConcurrentDictionary<IntPtr, string> _descriptionCards = new ConcurrentDictionary<IntPtr, string>();

        public CardViewOverrides(CardOverrideCatalog catalog, CardOverrideStore store)
        {
            _catalog = catalog;
            _store = store;
        }

        public void Apply(
            IntPtr originalCardDef, ref IntPtr cardDef, ref int cost, ref int power, ref int rarity,
            ref IntPtr borderDefId, ref IntPtr artVariantDefId, ref IntPtr surfaceEffectDefId,
            ref IntPtr cardRevealEffectDefId, ref IntPtr cardBackDefId, ref IntPtr factionDefId)
        {
            var sourceId = originalCardDef == IntPtr.Zero ? null : new CardDefWrapper(originalCardDef).GetId();
            var rule = _store.Resolve(sourceId);
            if (rule != null)
            {
                if (rule.ReplacementCardId != null && _catalog.Cards.TryGetValue(rule.ReplacementCardId, out var replacementId))
                {
                    var replacement = SnapTypeDataCollector.CardDefList_Find(replacementId);
                    if (replacement != IntPtr.Zero)
                    {
                        var card = new CardDefWrapper(replacement);
                        cardDef = replacement;
                        cost = card.Cost;
                        power = card.Power;
                        artVariantDefId = IntPtr.Zero;
                    }
                }

                artVariantDefId = ApplyVariant(rule, cardDef, artVariantDefId);
                surfaceEffectDefId = CardOverrideCatalog.Find(_catalog.SurfaceEffects, rule.SurfaceEffectId, surfaceEffectDefId);
                cardRevealEffectDefId = CardOverrideCatalog.Find(_catalog.RevealEffects, rule.RevealEffectId, cardRevealEffectDefId);
                borderDefId = CardOverrideCatalog.Find(_catalog.Borders, rule.BorderId, borderDefId);
                cardBackDefId = CardOverrideCatalog.Find(_catalog.CardBacks, rule.CardBackId, cardBackDefId);
                factionDefId = CardOverrideCatalog.Find(_catalog.Factions, rule.FactionId, factionDefId);
                cost = rule.Cost ?? cost;
                power = rule.Power ?? power;
                if (rule.Force3D) rarity = 7;
            }
        }

        private IntPtr ApplyVariant(CardOverrideRule rule, IntPtr cardDef, IntPtr original)
        {
            if (rule.VariantId == null) return original;
            var variant = CardOverrideCatalog.Find(_catalog.Variants, rule.VariantId, IntPtr.Zero);
            if (variant == IntPtr.Zero) variant = IL2CppHelper.NewString(rule.VariantId);
            if (!rule.EnsureVariantMatches || cardDef == IntPtr.Zero) return variant;

            var mapping = SnapTypeDataCollector.CardToArtVariantDefList_Find(variant);
            if (mapping != IntPtr.Zero)
            {
                var variantCardId = Marshal.ReadIntPtr(mapping, SnapTypeDataCollector.CardToArtVariantDef_CardDefId_Field_Offset);
                if (!string.Equals(new IL2CppStringRef(variantCardId).GetObject(),
                        new CardDefWrapper(cardDef).GetId(), StringComparison.Ordinal))
                    return original;
            }
            return variant;
        }

        public void TrackDescription(IntPtr descriptionEvent, IntPtr originalCardDef)
        {
            if (descriptionEvent == IntPtr.Zero) return;
            var sourceId = originalCardDef == IntPtr.Zero ? null : new CardDefWrapper(originalCardDef).GetId();
            if (sourceId == null) _descriptionCards.TryRemove(descriptionEvent, out _);
            else _descriptionCards[descriptionEvent] = sourceId;
        }

        public string GetDescriptionOverride(IntPtr descriptionEvent)
        {
            return _descriptionCards.TryGetValue(descriptionEvent, out var sourceId)
                ? _store.Resolve(sourceId)?.Description : null;
        }

        public void Clear()
        {
            _store.Clear();
            _descriptionCards.Clear();
        }
    }
}
