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
        private static IReadOnlyDictionary<string, CardDefWrapper> _cardsById;

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

        public static CardDefWrapper FindCard(string cardDefId)
        {
            if (string.IsNullOrEmpty(cardDefId) || _cardsById == null)
                return null;

            return _cardsById.TryGetValue(cardDefId, out var card) ? card : null;
        }

        private static void Initialize()
        {
            if (Cards != null)
                return;

            SnapTypeDataCollector.EnsureLoaded();

            if (SnapTypeDataCollector.CardDef_Id_Fields == null)
                return;

            var cards = SnapTypeDataCollector.CardDef_Id_Fields
                .Select(id => FindCard(IL2CppHelper.GetStaticFieldValue(id.Ptr)))
                .Where(c => c != IntPtr.Zero)
                .Select(c => new CardDefWrapper(c))
                .ToList();

            var cardsById = new Dictionary<string, CardDefWrapper>(StringComparer.Ordinal);
            foreach (var card in cards)
            {
                var id = card.GetId();
                if (!string.IsNullOrEmpty(id))
                    cardsById[id] = card;
            }

            Cards = cards;
            _cardsById = cardsById;
        }
    }
}
