using IL2CppApi.Wrappers;
using IL2CppApi.Runtime;
using SnapCardViewHook.Core.Data;
using SnapCardViewHook.Core.Capture;
using SnapCardViewHook.Core.Helpers;
using SnapCardViewHook.Core.IL2Cpp;
using SnapCardViewHook.Core.Wrappers;
using SnapCardViewHook.Core.Overrides;
using SnapCardViewHook.Core.AssetLab;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Contexts;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SnapCardViewHook.Core.Forms
{
    public partial class CardViewSelectorForm : Form
    {
        private Dictionary<string, IL2CppFieldInfoWrapper> _variantList;
        private Dictionary<string, IL2CppFieldInfoWrapper> _surfaceEffectList;
        private Dictionary<string, IL2CppFieldInfoWrapper> _revealEffectList;
        private Dictionary<string, IntPtr> _borderList;
        private Dictionary<string, IL2CppFieldInfoWrapper> _cardDefList;
        private Dictionary<string, IL2CppFieldInfoWrapper> _cardBackList;
        private Dictionary<string, IL2CppFieldInfoWrapper> _gameBoardList;
        private Dictionary<string, IL2CppFieldInfoWrapper> _factionList;

        private readonly ConcurrentDictionary<IntPtr, byte> _cardDescriptionEvents =
            new ConcurrentDictionary<IntPtr, byte>();
        private volatile string _descriptionOverride;
        private volatile bool _overrideDescription;
        private volatile string _replacementCardKey;
        // Null means all cards; empty means filtering is enabled without a valid selection.
        private volatile string _replacementTargetId;

        private readonly CardOverrideStore _overrideStore = new CardOverrideStore();
        private CardOverrideCatalog _overrideCatalog;
        private CardOverrideConfigs _overrideConfigs;
        private CardViewOverrides _cardOverrides;
        private CardOverridesForm _cardOverridesForm;
        private readonly AssetLabController _assetLab = new AssetLabController();
        private AssetLabForm _assetLabForm;

        private IntPtr _clonedVariantObj = IntPtr.Zero;

        [StructLayout(LayoutKind.Sequential)]
        private struct NullableSingle
        {
            public byte HasValue;
            public float Value;
        }

        public CardViewSelectorForm()
        {
            InitializeComponent();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            TopMost = true;
            BringToFront();
            Activate();

            BeginInvoke(new Action(() => TopMost = false));
        }

        private unsafe void CardViewSelectorForm_Load(object sender, EventArgs e)
        {
            SnapTypeDataCollector.EnsureLoaded();

            // initialize lists
            _variantList = SnapTypeDataCollector.ArtVariantDef_Id_Fields?.ToDictionary(f => f.Name);
            _surfaceEffectList = SnapTypeDataCollector.SurfaceEffectDef_Id_Fields?.ToDictionary(f => f.Name);
            _revealEffectList = SnapTypeDataCollector.CardRevealEffectDef_Id_Fields?.ToDictionary(f => f.Name);
            _borderList = new Dictionary<string, IntPtr>();
            _cardDefList =  SnapTypeDataCollector.CardDef_Id_Fields?.ToDictionary(f => f.Name);
            _cardBackList = SnapTypeDataCollector.CardBackDefId_Fields?.ToDictionary(f => f.Name);
            _gameBoardList = SnapTypeDataCollector.GameBoardDef_Id_Fields?.ToDictionary(f => f.Name);
            _factionList = SnapTypeDataCollector.FactionDef_Id_Fields?.ToDictionary(f => f.Name);

            // try to load border data
            GetBorderData();

            // populate controls
            surfaceEffectBox.Items.AddRange(_surfaceEffectList?.Keys.ToArray() ?? Array.Empty<string>());
            revealEffectBox.Items.AddRange(_revealEffectList?.Keys.ToArray() ?? Array.Empty<string>());
            variantBox.SetItems(_variantList?.Keys.ToArray() ?? Array.Empty<string>());
            borderBox.Items.AddRange(_borderList?.Keys.ToArray() ?? Array.Empty<string>());
            cardBox.SetItems(_cardDefList?.Keys.ToArray() ?? Array.Empty<string>());
            replacementTargetCardBox.SetItems(_cardDefList?.Keys.ToArray() ?? Array.Empty<string>());
            cardBackBox.Items.AddRange(_cardBackList?.Keys.ToArray() ?? Array.Empty<string>());
            boardBox.Items.AddRange(_gameBoardList?.Keys.ToArray() ?? Array.Empty<string>());
            factionBox.Items.AddRange(_factionList?.Keys.ToArray() ?? Array.Empty<string>());
            replacementCard_Changed(this, EventArgs.Empty);
            replacementTarget_Changed(this, EventArgs.Empty);

            _overrideCatalog = CardOverrideCatalog.FromClient();
            _cardOverrides = new CardViewOverrides(_overrideCatalog, _overrideStore);

            // set hook override
            SnapTypeDataCollector.CardViewInitializeHookOverride = CardViewInitOverride;
            SnapTypeDataCollector.BoardViewLoadBoardHookOverride = BoardViewLoadBoardOverride;
            SnapTypeDataCollector.LocalizeStringEventUpdateStringOverride = GetDescriptionOverride;

        }

        private unsafe void GetBorderData()
        {
            var borderList = (IL2CppList*)SnapTypeDataCollector.BorderDefList_Defs_cached_value;
            var borderDefs = IL2CppHelper.ListToArray(borderList);

            IL2CppHelper.EnumerateList(borderList, (item, i) =>
            {
                if (item == IntPtr.Zero)
                    return;

                var s = (IL2CppString*)item;
                _borderList.Add(new string(s->chars), item);
            });
        }

        internal void SetCardOverride(string id)
        {
            cardBox.SetSelection(id);
        }

        private void showOverridesButton_Click(object sender, EventArgs e)
        {
            if (_overrideConfigs == null) _overrideConfigs = new CardOverrideConfigs(_overrideStore);
            if (_cardOverridesForm == null || _cardOverridesForm.IsDisposed)
                _cardOverridesForm = new CardOverridesForm(_overrideCatalog, _overrideStore, _overrideConfigs);
            if (!_cardOverridesForm.Visible) _cardOverridesForm.Show(this);
            _cardOverridesForm.BringToFront();
            _cardOverridesForm.Activate();
        }

        private void showAssetLabButton_Click(object sender, EventArgs e)
        {
            if (_assetLabForm == null || _assetLabForm.IsDisposed)
                _assetLabForm = new AssetLabForm(_assetLab);
            if (!_assetLabForm.Visible) _assetLabForm.Show(this);
            _assetLabForm.BringToFront();
            _assetLabForm.Activate();
        }

        private void BoardViewLoadBoardOverride(IntPtr thisPtr, IntPtr boardDefId, IntPtr methodInfo)
        {
            boardDefId = GetBoardOverride(boardDefId);

            SnapTypeDataCollector.BoardViewLoadBoardOriginal(thisPtr, boardDefId, methodInfo);
        }

        private IntPtr GetBoardOverride(IntPtr original)
        {
            if (!overrideBoardCheckBox.Checked || boardBox.SelectedItem == null)
                return original;

            return IL2CppHelper.GetStaticFieldValue(_gameBoardList[boardBox.SelectedItem.ToString()].Ptr);
        }

        private void CardViewInitOverride(
            IntPtr thisPtr, IntPtr cardDef, int cost, int power, int rarity,
            IntPtr borderDefId, IntPtr artVariantDefId, IntPtr surfaceEffectDefId,
            IntPtr cardRevealEffectDefId, int cardRevealEffectType, bool showRevealEffectOnStart,
            int logoEffectId, IntPtr cardBackDefId, bool isMorph, bool setTransparentQueue,
            IntPtr factionDefId, IntPtr methodInfo)
        {
            if (_assetLab.TryOverride(thisPtr, cardDef, cost, power, rarity, borderDefId, artVariantDefId,
                surfaceEffectDefId, cardRevealEffectDefId, cardRevealEffectType, showRevealEffectOnStart,
                logoEffectId, cardBackDefId, isMorph, setTransparentQueue, factionDefId, methodInfo)) return;

            var originalCardDef = cardDef;

            // Match the incoming card before replacement changes its definition and defaults.
            cardDef = GetCardOverride(cardDef, ref cost, ref power, ref artVariantDefId);
            artVariantDefId = GetVariantOverride(artVariantDefId, cardDef);
            surfaceEffectDefId = GetSurfaceEffectOverride(surfaceEffectDefId);
            cardRevealEffectDefId = GetRevealEffectOverride(cardRevealEffectDefId);
            borderDefId = GetBorderOverride(borderDefId);
            cardBackDefId = GetCardBackOverride(cardBackDefId);
            factionDefId = GetFactionOverride(factionDefId);

            if (changeCostCheckBox.Checked)
                cost = Convert.ToInt32(costNumeric.Value);

            if (changePowerCheckBox.Checked)
                power = Convert.ToInt32(powerNumeric.Value);

            if (force3DCheckbox.Checked)
                rarity = 7;

            _cardOverrides.Apply(originalCardDef, ref cardDef, ref cost, ref power, ref rarity,
                ref borderDefId, ref artVariantDefId, ref surfaceEffectDefId,
                ref cardRevealEffectDefId, ref cardBackDefId, ref factionDefId);

            TrackDescriptionEvent(thisPtr, originalCardDef);

            SnapTypeDataCollector.CardViewInitializeOriginal(
                thisPtr, cardDef, cost, power, rarity, borderDefId, artVariantDefId,
                surfaceEffectDefId, cardRevealEffectDefId, cardRevealEffectType, showRevealEffectOnStart, 
                logoEffectId, cardBackDefId, isMorph, setTransparentQueue, 
                factionDefId, methodInfo
            );

            TrackDescriptionEvent(thisPtr, originalCardDef);
        }

        private void TrackDescriptionEvent(IntPtr cardView, IntPtr originalCardDef)
        {
            if (cardView == IntPtr.Zero)
                return;

            var descriptionEvent = Marshal.ReadIntPtr(
                cardView,
                SnapTypeDataCollector.CardView_LocalizeDescriptionEvent_Field_Offset);

            if (descriptionEvent != IntPtr.Zero)
            {
                _cardDescriptionEvents.TryAdd(descriptionEvent, 0);
                _cardOverrides.TrackDescription(descriptionEvent, originalCardDef);
            }
        }

        private string GetDescriptionOverride(IntPtr descriptionEvent)
        {
            var assetText = _assetLab.DescriptionOverride(descriptionEvent);
            if (assetText != null) return assetText;
            var perCardText = _cardOverrides.GetDescriptionOverride(descriptionEvent);
            if (perCardText != null) return perCardText;

            if (!_overrideDescription || !_cardDescriptionEvents.ContainsKey(descriptionEvent))
                return null;

            return _descriptionOverride;
        }

        private IntPtr GetCardBackOverride(IntPtr original)
        {
            if (!overrideCardBackCheckBox.Checked || cardBackBox.SelectedItem == null)
                return original;

            return IL2CppHelper.GetStaticFieldValue(_cardBackList[cardBackBox.SelectedItem.ToString()].Ptr);
        }

        private IntPtr GetCardOverride(IntPtr original, ref int cost, ref int power, ref IntPtr artVariantDefId)
        {
            var replacement = _replacementCardKey;
            if (!overrideCardCheckBox.Checked || replacement == null || !MatchesReplacementTarget(original))
                return original;

            var cardDefId =
                IL2CppHelper.GetStaticFieldValue(_cardDefList[replacement].Ptr);
            var overrideCardDefObjPtr = SnapTypeDataCollector.CardDefList_Find(cardDefId);
            
            if(overrideCardDefObjPtr == IntPtr.Zero )
                return original;
            
            var objWrapper = new CardDefWrapper(overrideCardDefObjPtr);

            cost = objWrapper.Cost;
            power = objWrapper.Power;
            artVariantDefId = IntPtr.Zero;

            return overrideCardDefObjPtr;
        }

        private bool MatchesReplacementTarget(IntPtr original)
        {
            // The hook reads one UI-published value, without accessing the target controls.
            var targetId = _replacementTargetId;
            if (targetId == null) return true;
            return targetId.Length != 0 && original != IntPtr.Zero &&
                string.Equals(new CardDefWrapper(original).GetId(), targetId, StringComparison.Ordinal);
        }

        private static string ListedCardSelection(SearchComboBox combo)
        {
            return combo.FindItem(combo.Text.Trim()) as string;
        }

        private static void CommitCardSelection(SearchComboBox combo)
        {
            combo.SetSelection(ListedCardSelection(combo));
        }

        private void cardSelection_Validating(object sender, System.ComponentModel.CancelEventArgs e)
        {
            CommitCardSelection((SearchComboBox)sender);
        }

        private void cardSelection_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter || e.Handled) return;
            var combo = (SearchComboBox)sender;
            CommitCardSelection(combo);
            combo.DroppedDown = false;
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void replacementCard_Changed(object sender, EventArgs e)
        {
            if (cardBox.IsUpdating) return;
            // Do not keep using the previous selection while the user types an invalid value.
            _replacementCardKey = ListedCardSelection(cardBox);
        }

        private void replacementTarget_Changed(object sender, EventArgs e)
        {
            if (replacementTargetCardBox.IsUpdating) return;
            replacementTargetCardBox.Enabled = onlyReplaceTargetCardCheckBox.Checked;
            if (!onlyReplaceTargetCardCheckBox.Checked)
            {
                _replacementTargetId = null;
                return;
            }

            // Resolve the ID's value rather than assuming its field name or native pointer
            // is its identity. No selection must never broaden replacement to all cards.
            _replacementTargetId = string.Empty;
            var targetId = string.Empty;
            var selected = ListedCardSelection(replacementTargetCardBox);
            if (selected != null &&
                _cardDefList != null && _cardDefList.TryGetValue(selected, out var field))
            {
                var nativeId = IL2CppHelper.GetStaticFieldValue(field.Ptr);
                targetId = new IL2CppStringRef(nativeId).GetObject() ?? string.Empty;
            }
            _replacementTargetId = targetId;
        }

        private unsafe IntPtr GetVariantOverride(IntPtr original, IntPtr cardDef)
        {
            if (!overrideVariantCheckBox.Checked || (variantBox.SelectedItem == null && variantBox.Text.Length == 0))
                return original;

            IntPtr variantToOverride;

            if (variantBox.SelectedItem == null)
            {
                if (!_variantList.TryGetValue(variantBox.Text, out var variantFieldInfo))
                {
                    // create new obj instance
                    if (_clonedVariantObj == IntPtr.Zero)
                    {
                        var modelObj = IL2CppHelper.GetStaticFieldValue(_variantList.Last().Value.Ptr);
                        _clonedVariantObj = CloneIL2CppObject(modelObj, 1024);
                    }

                    variantToOverride = _clonedVariantObj;
                    SetIdValue(variantToOverride, variantBox.Text);
                }
                else
                {
                    variantToOverride = IL2CppHelper.GetStaticFieldValue(variantFieldInfo.Ptr);
                }
            }
            else
            {
                variantToOverride = IL2CppHelper.GetStaticFieldValue(_variantList[variantBox.SelectedItem.ToString()].Ptr);
            }

            var overridePtr = variantToOverride;

            if (!ensureVariantMatchCheckbox.Checked)
                return overridePtr;

            var cardToArtVariantDef = SnapTypeDataCollector.CardToArtVariantDefList_Find(overridePtr);

            if (cardToArtVariantDef != IntPtr.Zero)
            {
                var variantCardDefId = *(IntPtr*)(cardToArtVariantDef +
                                               SnapTypeDataCollector.CardToArtVariantDef_CardDefId_Field_Offset);

                if (variantCardDefId != new CardDefWrapper(cardDef).CardDefId)
                    return original;
            }

            return overridePtr;
        }

        private IntPtr GetSurfaceEffectOverride(IntPtr original)
        {
            if (!overrideSurfaceEffectCheckBox.Checked || surfaceEffectBox.SelectedItem == null)
                return original;

            return IL2CppHelper.GetStaticFieldValue(_surfaceEffectList[surfaceEffectBox.SelectedItem.ToString()].Ptr);
        }

        private IntPtr GetRevealEffectOverride(IntPtr original)
        {
            if (!overrideRevealEffectCheckBox.Checked || revealEffectBox.SelectedItem == null)
                return original;

            return IL2CppHelper.GetStaticFieldValue(_revealEffectList[revealEffectBox.SelectedItem.ToString()].Ptr);
        }

        private IntPtr GetBorderOverride(IntPtr original)
        {
            if (!overrideBorderCheckBox.Checked || borderBox.SelectedItem == null)
                return original;

            return _borderList[borderBox.SelectedItem.ToString()];
        }

        private IntPtr GetFactionOverride(IntPtr original)
        {
            if (!overrideFactionCheckBox.Checked || factionBox.SelectedItem == null)
                return original;

            return IL2CppHelper.GetStaticFieldValue(_factionList[factionBox.SelectedItem.ToString()].Ptr);
        }

        private void flipCardCheckBox_CheckedChanged_1(object sender, EventArgs e)
        {
            FlipCard(flipCardCheckBox.Checked);
        }

        private void CardViewSelectorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _assetLab.Close();
            // remove overrides
            SnapTypeDataCollector.CardViewInitializeHookOverride = null;
            SnapTypeDataCollector.BoardViewLoadBoardHookOverride = null;
            SnapTypeDataCollector.LocalizeStringEventUpdateStringOverride = null;
            _overrideDescription = false;
            _descriptionOverride = null;
            _replacementCardKey = null;
            _replacementTargetId = null;
            _cardDescriptionEvents.Clear();
            _cardOverrides?.Clear();

            // set default flip state to false
            // FlipCard(false);
        }

        private void FlipCard(bool flip)
        {
            SnapTypeDataCollector.ExecuteActionInGameUiThread(() =>
            {
                var instance = SnapTypeDataCollector.CardDetailsCardView_InstancePtr;

                if (instance == IntPtr.Zero)
                    return;

                using var runtime = new Il2CppRuntime();
                runtime.Invoke(SnapTypeDataCollector.CardDetailsCardViewFlipCardMethod, instance,
                    Il2CppArgument.Value(flip), Il2CppArgument.Value(new NullableSingle { HasValue = 1, Value = 0f }));
            });
        }

        #region experimental

        private IntPtr CloneIL2CppObject(IntPtr obj, int size)
        {
            if (obj == IntPtr.Zero)
                return IntPtr.Zero;

            var cloned = Marshal.AllocHGlobal(size);

            unsafe
            {
                var source = (byte*)obj;
                var target = (byte*)cloned;

                for (var i = 0; i < size; i++)
                    target[i] = source[i];
            }

            return cloned;
        }

        private unsafe void SetIdValue(IntPtr idObj, string value)
        {
            var str = (IL2CppString*)idObj;
            str->length = value.Length;

            var chars = &str->chars;

            for (var i = 0; i < value.Length; i++)
                *chars[i] = value[i];

            *chars[value.Length] = (char)0;
        }

        private void descriptionTextBox_TextChanged(object sender, EventArgs e)
        {
            _descriptionOverride = descriptionTextBox.Text;
        }

        private void overrideDescriptionCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            _descriptionOverride = descriptionTextBox.Text;
            _overrideDescription = overrideDescriptionCheckBox.Checked;
        }

        #endregion

        private CardCatalogForm _cardCatalogForm;
        private CardCaptureForm _cardCaptureForm;
        private CardVideoCaptureForm _cardVideoCaptureForm;
        private CardCaptureForm _deckCaptureForm;
        private CardVideoCaptureForm _deckVideoCaptureForm;

        private void capturePngMenuItem_Click(object sender, EventArgs e)
        {
            if (_cardCaptureForm == null || _cardCaptureForm.IsDisposed)
                _cardCaptureForm = new CardCaptureForm();
            if (!_cardCaptureForm.Visible)
                _cardCaptureForm.Show(this);
            _cardCaptureForm.BringToFront();
        }

        private void captureVideoMenuItem_Click(object sender, EventArgs e)
        {
            if (_cardVideoCaptureForm == null || _cardVideoCaptureForm.IsDisposed)
            {
                var frames = _cardCaptureForm != null && !_cardCaptureForm.IsDisposed
                    ? _cardCaptureForm.GetFrameOptions()
                    : new CardCaptureOptions { Width = 800, Height = 1200, PaddingPercent = 0 };
                _cardVideoCaptureForm = new CardVideoCaptureForm(frames);
            }
            if (!_cardVideoCaptureForm.Visible)
                _cardVideoCaptureForm.Show(this);
            _cardVideoCaptureForm.BringToFront();
        }

        private void captureDeckPngMenuItem_Click(object sender, EventArgs e)
        {
            if (_deckCaptureForm == null || _deckCaptureForm.IsDisposed)
                _deckCaptureForm = new CardCaptureForm(CaptureSubject.Deck);
            if (!_deckCaptureForm.Visible)
                _deckCaptureForm.Show(this);
            _deckCaptureForm.BringToFront();
        }

        private void captureDeckVideoMenuItem_Click(object sender, EventArgs e)
        {
            if (_deckVideoCaptureForm == null || _deckVideoCaptureForm.IsDisposed)
            {
                var frames = _deckCaptureForm != null && !_deckCaptureForm.IsDisposed
                    ? _deckCaptureForm.GetFrameOptions()
                    : new CardCaptureOptions { Subject = CaptureSubject.Deck, Width = 1600, Height = 1064, PaddingPercent = 3 };
                _deckVideoCaptureForm = new CardVideoCaptureForm(frames);
            }
            if (!_deckVideoCaptureForm.Visible)
                _deckVideoCaptureForm.Show(this);
            _deckVideoCaptureForm.BringToFront();
        }

        private void showCatalogButton_Click(object sender, EventArgs e)
        {
            if(_cardCatalogForm == null || _cardCatalogForm.IsDisposed)
                _cardCatalogForm = new CardCatalogForm(this);

            _cardCatalogForm.Show();
        }
    }
}
