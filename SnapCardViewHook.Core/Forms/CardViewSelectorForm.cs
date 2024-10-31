using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using IL2CppApi.Wrappers;
using SnapCardViewHook.Core.IL2Cpp;
using SnapCardViewHook.Core.Wrappers;

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

        private IntPtr _clonedVariantObj = IntPtr.Zero;

        public CardViewSelectorForm()
        {
            InitializeComponent();
        }

        private unsafe void CardViewSelectorForm_Load(object sender, EventArgs e)
        {
            SnapTypeDataCollector.EnsureLoaded();

            // initialize lists
            _variantList = SnapTypeDataCollector.ArtVariantDef_Id_Fields.ToDictionary(f => f.Name);
            _surfaceEffectList = SnapTypeDataCollector.SurfaceEffectDef_Id_Fields.ToDictionary(f => f.Name);
            _revealEffectList = SnapTypeDataCollector.CardRevealEffectDef_Id_Fields.ToDictionary(f => f.Name);
            _borderList = new Dictionary<string, IntPtr>();
            _cardDefList = SnapTypeDataCollector.CardDef_Id_Fields.ToDictionary(f => f.Name);
            _cardBackList = SnapTypeDataCollector.CardBackDefId_Fields.Skip(2).ToDictionary(f => f.Name);

            // try to load border data
            GetBorderData();

            // populate controls
            surfaceEffectBox.Items.AddRange(_surfaceEffectList.Keys.ToArray());
            revealEffectBox.Items.AddRange(_revealEffectList.Keys.ToArray());
            variantBox.Items.AddRange(_variantList.Keys.ToArray());
            borderBox.Items.AddRange(_borderList.Keys.ToArray());
            cardBox.Items.AddRange(_cardDefList.Keys.ToArray());
            cardBackBox.Items.AddRange(_cardBackList.Keys.ToArray());

            // set hook override
            SnapTypeDataCollector.CardViewInitializeHookOverride = CardViewInitOverride;
        }

        private unsafe void GetBorderData()
        {
            var borders = (IL2CppList*)SnapTypeDataCollector.BorderDefList_Defs_cached_value;

            if (borders == null)
                return;

            if (borders->Size == 0)
                return;

            var array = &borders->Array->vector;

            for (var i = 0; i < borders->Size; i++)
            {
                var item = array[i];

                if (item == null) 
                    break;

                var strCast = (IL2CppString*)item;
                _borderList.Add(new string(strCast->chars), new IntPtr(item));
            }
        }

        public void CardViewInitOverride(
            IntPtr thisPtr, IntPtr cardDef, int cost, int power, int rarity,
            IntPtr borderDefId, IntPtr artVariantDefId, IntPtr surfaceEffectDefId,
            IntPtr cardRevealEffectDefId, int cardRevealEffectType, bool showRevealEffectOnStart,
            int logoEffectId, int cardBackDefId, bool isMorph)
        {
            cardDef = GetCardOverride(cardDef, ref cost, ref power, ref artVariantDefId);
            artVariantDefId = GetVariantOverride(artVariantDefId, cardDef);
            surfaceEffectDefId = GetSurfaceEffectOverride(surfaceEffectDefId);
            cardRevealEffectDefId = GetRevealEffectOverride(cardRevealEffectDefId);
            borderDefId = GetBorderOverride(borderDefId);
            cardBackDefId = GetCardBackOverride(cardBackDefId);

            if (force3DCheckbox.Checked)
                rarity = 7;

            SnapTypeDataCollector.CardViewInitializeOriginal(thisPtr, cardDef, cost, power, rarity, borderDefId, artVariantDefId,
                surfaceEffectDefId, cardRevealEffectDefId, cardRevealEffectType, showRevealEffectOnStart, logoEffectId,
                cardBackDefId, isMorph);
        }

        private int GetCardBackOverride(int original)
        {
            if (!overrideCardBackCheckBox.Checked || cardBackBox.SelectedItem == null)
                return original;

            return IL2CppHelper.GetStaticFieldValue(_cardBackList[cardBackBox.SelectedItem.ToString()].Ptr).ToInt32();
        }

        private IntPtr GetCardOverride(IntPtr original, ref int cost, ref int power, ref IntPtr artVariantDefId)
        {
            if (!overrideCardCheckBox.Checked || cardBox.SelectedItem == null)
                return original;

            var cardDefIdEnumValue =
                IL2CppHelper.GetStaticFieldValue(_cardDefList[cardBox.SelectedItem.ToString()].Ptr);  
            var overrideCardDefObjPtr = SnapTypeDataCollector.CardDefList_Find(cardDefIdEnumValue);
            
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
                var variantCardDefId = *(int*)(cardToArtVariantDef +
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

        private void flipCardCheckBox_CheckedChanged_1(object sender, EventArgs e)
        {
            var @checked = flipCardCheckBox.Checked;
            SnapTypeDataCollector.ExecuteActionInGameUiThread(() =>
            {
                var instance = SnapTypeDataCollector.CardDetailsCardView_InstancePtr;
                SnapTypeDataCollector.CardDetailsCardView_FlipCard(instance, @checked, 0);
            });
        }

        private void CardViewSelectorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SnapTypeDataCollector.CardViewInitializeHookOverride = null;
            SnapTypeDataCollector.ExecuteActionInGameUiThread(() =>
            {
                var instance = SnapTypeDataCollector.CardDetailsCardView_InstancePtr;
                SnapTypeDataCollector.CardDetailsCardView_FlipCard(instance, false, 0);
            });
        }

        private IntPtr CloneIL2CppObject(IntPtr obj, int size)
        {
            if(obj == IntPtr.Zero)
                return IntPtr.Zero;

            var cloned = Marshal.AllocHGlobal(size);

            unsafe
            {
                var source = (byte*)obj;
                var target = (byte*)cloned;

                for(var i = 0; i < size; i++)
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
    }
}
