using IL2CppApi.Wrappers;
using SnapCardViewHook.Core.Data;
using SnapCardViewHook.Core.Helpers;
using SnapCardViewHook.Core.IL2Cpp;
using SnapCardViewHook.Core.Wrappers;
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

        private IntPtr _clonedVariantObj = IntPtr.Zero;

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
            variantBox.Items.AddRange(_variantList?.Keys.ToArray() ?? Array.Empty<string>());
            borderBox.Items.AddRange(_borderList?.Keys.ToArray() ?? Array.Empty<string>());
            cardBox.Items.AddRange(_cardDefList?.Keys.ToArray() ?? Array.Empty<string>());
            cardBackBox.Items.AddRange(_cardBackList?.Keys.ToArray() ?? Array.Empty<string>());
            boardBox.Items.AddRange(_gameBoardList?.Keys.ToArray() ?? Array.Empty<string>());
            factionBox.Items.AddRange(_factionList?.Keys.ToArray() ?? Array.Empty<string>());

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
            cardBox.SelectedItem = id;
        }

        private void BoardViewLoadBoardOverride(IntPtr thisPtr, IntPtr boardDefId)
        {
            boardDefId = GetBoardOverride(boardDefId);

            SnapTypeDataCollector.BoardViewLoadBoardOriginal(thisPtr, boardDefId);
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

            TrackDescriptionEvent(thisPtr);

            SnapTypeDataCollector.CardViewInitializeOriginal(
                thisPtr, cardDef, cost, power, rarity, borderDefId, artVariantDefId,
                surfaceEffectDefId, cardRevealEffectDefId, cardRevealEffectType, showRevealEffectOnStart, 
                logoEffectId, cardBackDefId, isMorph, setTransparentQueue, 
                factionDefId, methodInfo
            );

            TrackDescriptionEvent(thisPtr);
        }

        private void TrackDescriptionEvent(IntPtr cardView)
        {
            if (cardView == IntPtr.Zero)
                return;

            var descriptionEvent = Marshal.ReadIntPtr(
                cardView,
                SnapTypeDataCollector.CardView_LocalizeDescriptionEvent_Field_Offset);

            if (descriptionEvent != IntPtr.Zero)
                _cardDescriptionEvents.TryAdd(descriptionEvent, 0);
        }

        private string GetDescriptionOverride(IntPtr descriptionEvent)
        {
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
            if (!overrideCardCheckBox.Checked || cardBox.SelectedItem == null)
                return original;

            var cardDefId =
                IL2CppHelper.GetStaticFieldValue(_cardDefList[cardBox.SelectedItem.ToString()].Ptr);  
            var overrideCardDefObjPtr = SnapTypeDataCollector.CardDefList_Find(cardDefId);
            
            if(overrideCardDefObjPtr == IntPtr.Zero )
                return original;
            
            var objWrapper = new CardDefWrapper(overrideCardDefObjPtr);

            cost = objWrapper.Cost;
            power = objWrapper.Power;
            artVariantDefId = IntPtr.Zero;

            return overrideCardDefObjPtr;
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
            // remove overrides
            SnapTypeDataCollector.CardViewInitializeHookOverride = null;
            SnapTypeDataCollector.BoardViewLoadBoardHookOverride = null;
            SnapTypeDataCollector.LocalizeStringEventUpdateStringOverride = null;
            _overrideDescription = false;
            _descriptionOverride = null;
            _cardDescriptionEvents.Clear();

            // set default flip state to false
            FlipCard(false);
        }

        private void FlipCard(bool flip)
        {
            SnapTypeDataCollector.ExecuteActionInGameUiThread(() =>
            {
                var instance = SnapTypeDataCollector.CardDetailsCardView_InstancePtr;

                if(instance == IntPtr.Zero) 
                    return;

                SnapTypeDataCollector.CardDetailsCardView_FlipCard(instance, flip, 0);
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

        private void showCatalogButton_Click(object sender, EventArgs e)
        {
            if(_cardCatalogForm == null || _cardCatalogForm.IsDisposed)
                _cardCatalogForm = new CardCatalogForm(this);

            _cardCatalogForm.Show();
        }
    }
}
