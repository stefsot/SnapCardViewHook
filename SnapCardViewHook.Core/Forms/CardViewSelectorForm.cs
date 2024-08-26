﻿using System;
using System.Collections.Generic;
using System.Linq;
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
            _cardDefList = SnapTypeDataCollector.CardDefId_Fields.Skip(2).ToDictionary(f => f.Name);

            // try to load border data
            GetBorderData(SnapTypeDataCollector.BorderDefList_CollectibleDefs_FieldInfo);

            // populate controls
            surfaceEffectBox.Items.AddRange(_surfaceEffectList.Keys.ToArray());
            revealEffectBox.Items.AddRange(_revealEffectList.Keys.ToArray());
            variantBox.Items.AddRange(_variantList.Keys.ToArray());
            borderBox.Items.AddRange(_borderList.Keys.ToArray());
            cardBox.Items.AddRange(_cardDefList.Keys.ToArray());

            // set hook override
            SnapTypeDataCollector.CardViewInitializeHookOverride = CardViewInitOverride;
        }

        private unsafe void GetBorderData(IL2CppFieldInfoWrapper fieldInfo)
        {
            var borders = (IL2CppArray*) IL2CppHelper.GetStaticFieldValue(fieldInfo.Ptr);

            if (borders == null)
                return;

            if (borders->vector == null || borders->Count <= 0 || borders->Count > 64)
                return;

            var array = &borders->vector;

            for (var i = 0; i < borders->Count; i++)
            {
                var item = array[i];

                if(item == null) 
                    break;

                var borderDef = new BorderDefWrapper(item);
                _borderList.Add(borderDef.Name, new IntPtr(borderDef.BorderDefId));
            }
        }

        public void CardViewInitOverride(
            IntPtr thisPr, IntPtr cardDef, int cost, int power, int rarity,
            IntPtr borderDefId, IntPtr artVariantDefId, IntPtr surfaceEffectDefId,
            IntPtr cardRevealEffectDefId, int cardRevealEffectType, bool showRevealEffectOnStart,
            int logoEffectId, int cardBackDefId, bool isMorph)
        {
            cardDef = GetCardOverride(cardDef, ref cost, ref power, ref artVariantDefId);
            artVariantDefId = GetVariantOverride(artVariantDefId, cardDef);
            surfaceEffectDefId = GetSurfaceEffectOverride(surfaceEffectDefId);
            cardRevealEffectDefId = GetRevealEffectOverride(cardRevealEffectDefId);
            borderDefId = GetBorderOverride(borderDefId);

            if (force3DCheckbox.Checked)
                rarity = 7;

            SnapTypeDataCollector.CardViewInitializeOriginal(thisPr, cardDef, cost, power, rarity, borderDefId, artVariantDefId,
                surfaceEffectDefId, cardRevealEffectDefId, cardRevealEffectType, showRevealEffectOnStart, logoEffectId,
                cardBackDefId, isMorph);
        }

        private IntPtr GetCardOverride(IntPtr original, ref int cost, ref int power, ref IntPtr artVariantDefId)
        {
            if (!overrideCardCheckBox.Checked || cardBox.SelectedItem == null)
                return original;

            var cardDefIdEnumValue = _cardDefList[cardBox.SelectedItem.ToString()].GetDefaultValue();
            var overrideCardDefObjPtr = SnapTypeDataCollector.CardDefList_Find((int)(uint)cardDefIdEnumValue);
            var objWrapper = new CardDefWrapper(overrideCardDefObjPtr);

            cost = objWrapper.Cost;
            power = objWrapper.Power;
            artVariantDefId = IntPtr.Zero;

            return overrideCardDefObjPtr;
        }

        private unsafe IntPtr GetVariantOverride(IntPtr original, IntPtr cardDef)
        {
            if (!overrideVariantCheckBox.Checked || variantBox.SelectedItem == null)
                return original;

            var overridePtr = IL2CppHelper.GetStaticFieldValue(_variantList[variantBox.SelectedItem.ToString()].Ptr);

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

        private void CardViewSelectorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SnapTypeDataCollector.CardViewInitializeHookOverride = null;
        }
    }
}
