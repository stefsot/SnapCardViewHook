using IL2CppApi.Runtime;
using IL2CppApi.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SnapCardViewHook.Core.Capture
{
    internal sealed partial class UnityCardCapture
    {
        private const int DeckCardCount = 12;
        private enum DeckViewKind { CardSlots, CardList, Lightweight }
        private IntPtr _selectedDeckView;
        private IntPtr[] _deckCardTransforms;
        private SourceCamera _deckCamera;

        private sealed class DeckCandidate
        {
            public IntPtr View;
            public IntPtr[] Cards;
            public SourceCamera Camera;
        }

        private IntPtr[] FindSelectedDeck()
        {
            // Resolve the current game's metadata. These types do not need to exist in
            // the older reference assemblies, and no field offsets are hard-coded.
            var sources = new[]
            {
                (Type: _runtime.Class("App.Collection.dll", "CubeUnity.App.Collection.Landscape", "DeckCardsView", true), Kind: DeckViewKind.CardSlots),
                (Type: _runtime.Class("App.View.dll", "CubeUnity.App.View", "DeckListView", true), Kind: DeckViewKind.CardList),
                (Type: _runtime.Class("App.View.dll", "CubeUnity.App.View", "LightweightDeckView", true), Kind: DeckViewKind.Lightweight)
            };
            var candidates = new List<DeckCandidate>();
            var report = new StringBuilder();
            foreach (var source in sources)
            {
                if (source.Type == null) continue;
                foreach (var view in FindObjects(source.Type))
                {
                    if (!ActiveDeckView(view)) continue;
                    try
                    {
                        var cards = ReadDeckCards(view, source.Type, source.Kind);
                        var renderers = cards.Select(card => FieldReference(card, _cardRenderer, "_CardRenderer")).ToArray();
                        var visible = renderers.Count(Visible);
                        report.AppendLine(source.Type.Name + " (" + ObjectName(view) + "): " + cards.Length +
                            " active cards, " + visible + " visible card renderers.");
                        if (cards.Length != DeckCardCount || visible != DeckCardCount) continue;
                        // The same layout may have multiple controller components.
                        if (candidates.Any(candidate => new HashSet<IntPtr>(candidate.Cards).SetEquals(cards))) continue;
                        var camera = FindSourceCamera(renderers[0], renderers);
                        candidates.Add(new DeckCandidate { View = view, Cards = cards, Camera = camera });
                    }
                    catch (Exception error) when (error is NotSupportedException || error is InvalidOperationException)
                    {
                        report.AppendLine(source.Type.Name + " (" + ObjectName(view) + "): " + error.Message);
                    }
                }
            }

            if (candidates.Count != 1)
                throw new InvalidOperationException((candidates.Count == 0
                    ? "No complete 12-card deck overview was found. Open the deck overview in the game and wait for every card to load."
                    : "More than one 12-card deck view is open. Close the other deck views before capturing.") +
                    "\n\nDeck-view diagnostics:\n" + (report.Length == 0 ? "No supported active deck views were found." : report.ToString()));

            var selected = candidates[0];
            _selectedDeckView = selected.View;
            _deckCamera = selected.Camera;
            return selected.Cards;
        }

        private IntPtr[] ReadDeckCards(IntPtr view, IL2CppClassWrapper type, DeckViewKind kind)
        {
            IEnumerable<IntPtr> cards;
            switch (kind)
            {
                case DeckViewKind.Lightweight:
                    cards = ListFieldReferences(view, type, "_cardViewList");
                    break;
                case DeckViewKind.CardList:
                    var slotType = _runtime.Class("App.View.dll", "CubeUnity.App.View", "DeckListCardSlotView");
                    cards = ListFieldReferences(view, type, "_DeckListCardSlotViews")
                        .Where(Alive).Select(slot => GetReference(slotType, slot, "CardView"));
                    break;
                default:
                    cards = ListFieldReferences(view, type, "_CardSlots")
                        .Where(Alive).SelectMany(slot => Descendants(slot, _cardView));
                    break;
            }
            return cards.Where(card => Alive(card) && _runtime.IsInstance(card, _cardView) &&
                Get<bool>(_gameObject, GameOf(card), "activeInHierarchy")).Distinct().ToArray();
        }

        private IntPtr[] ListFieldReferences(IntPtr instance, IL2CppClassWrapper type, string name)
        {
            var field = _runtime.Field(type, name);
            var list = _runtime.ReadReference(instance, field);
            return _runtime.ListReferences(list, _runtime.ClassFromType(field.Type));
        }

        private bool ActiveDeckView(IntPtr view) => Alive(view) && Get<bool>(_behaviour, view, "isActiveAndEnabled");

        private bool MatchesDeckAnchor(CardCaptureAnchor anchor)
        {
            if (anchor.DeckCards == null || anchor.DeckModels?.Length != _cards.Length ||
                anchor.DeckCardInstanceIds?.Length != _cards.Length || anchor.DeckCardParents?.Length != _cards.Length ||
                !_cards.SequenceEqual(anchor.DeckCards)) return false;
            for (var i = 0; i < _cards.Length; i++)
                if (!Alive(_cards[i]) || CallValue<int>(_object, _cards[i], "GetInstanceID", NoArgs) != anchor.DeckCardInstanceIds[i] ||
                    GetReference(_cardView, _cards[i], "CardModel") != anchor.DeckModels[i] ||
                    GetReference(_transform, _deckCardTransforms[i], "parent") != anchor.DeckCardParents[i]) return false;
            return true;
        }

        private bool IsSelectedDeckCurrent(bool checkReadiness)
        {
            // Track the pooled cards directly. Enumerating every slot's Unity hierarchy on
            // every frame would allocate native arrays and retain roots for the entire video.
            if (_resources == null || !ActiveDeckView(_selectedDeckView) ||
                CallValue<int>(_object, _selectedDeckView, "GetInstanceID", NoArgs) != _anchor.CardInstanceId ||
                !MatchesDeckAnchor(_anchor)) return false;
            for (var i = 0; i < _cards.Length; i++)
            {
                if (!Visible(_mainRenderers[i]) || FieldReference(_cards[i], _cardRenderer, "_CardRenderer") != _mainRenderers[i])
                    return false;
                if (checkReadiness) EnsureReady(_cards[i]);
            }
            return true;
        }

        private bool InCameraViewport(IntPtr camera, CaptureVector3 point)
        {
            var viewport = CallValue<CaptureVector3>(_camera, camera, "WorldToViewportPoint",
                new[] { "UnityEngine.Vector3" }, Il2CppArgument.Value(point));
            return viewport.IsFinite && viewport.Z > 0 && viewport.X >= 0 && viewport.X <= 1 && viewport.Y >= 0 && viewport.Y <= 1;
        }
    }
}
