using SnapCardViewHook.Core.Overrides;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SnapCardViewHook.Core.Forms
{
    internal partial class CardOverridesForm : Form
    {
        private readonly CardOverrideStore _store;
        private readonly CardOverrideConfigs _configs;
        private bool _loadingEditor = true;
        private bool _loadingList;
        private bool _loadingConfigs;
        private string _selectedTarget;
        private string _configError;

        public CardOverridesForm(CardOverrideCatalog catalog, CardOverrideStore store, CardOverrideConfigs configs)
        {
            InitializeComponent();
            _store = store;
            _configs = configs;
            Fill(targetCardBox, catalog.Cards.Keys.ToArray());
            Fill(replacementCardBox, catalog.Cards.Keys.ToArray());
            Fill(variantBox, catalog.Variants.Keys.ToArray());
            Fill(surfaceEffectBox, catalog.SurfaceEffects.Keys.ToArray());
            Fill(revealEffectBox, catalog.RevealEffects.Keys.ToArray());
            Fill(borderBox, catalog.Borders.Keys.ToArray());
            Fill(cardBackBox, catalog.CardBacks.Keys.ToArray());
            Fill(factionBox, catalog.Factions.Keys.ToArray());
            try
            {
                _configs.Initialize();
            }
            catch (Exception exception)
            {
                _configError = "Could not load configs: " + exception.Message;
            }
            RefreshConfigs();
            RefreshCards(null);
            if (_configs.HasPendingChanges) saveConfigTimer.Start();
        }

        private static void Fill(SearchComboBox combo, string[] items)
        {
            combo.SetItems(items.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray());
        }

        private void RefreshConfigs(bool updateName = true)
        {
            _loadingConfigs = true;
            configBox.BeginUpdate();
            try
            {
                var names = _configs.GetNames();
                if (!names.SequenceEqual(configBox.Items.Cast<string>()))
                {
                    configBox.Items.Clear();
                    configBox.Items.AddRange(names);
                }
            }
            catch (Exception exception)
            {
                _configError = "Could not list configs: " + exception.Message;
            }
            finally
            {
                configBox.SelectedItem = _configs.CurrentName;
                if (updateName) configNameBox.Text = _configs.CurrentName ?? "";
                configBox.EndUpdate();
                _loadingConfigs = false;
            }
            var hasConfig = _configs.CurrentName != null;
            renameConfigButton.Enabled = hasConfig;
            deleteConfigButton.Enabled = hasConfig;
            targetCardBox.Enabled = hasConfig;
            addCardButton.Enabled = hasConfig;
            rulesList.Enabled = hasConfig;
        }

        private void ChangeConfig(Action change, string selectedTarget = null)
        {
            saveConfigTimer.Stop();
            try
            {
                change();
                _configError = null;
            }
            catch (Exception exception)
            {
                _configError = "Could not change config: " + exception.Message;
                selectedTarget = _selectedTarget;
            }
            RefreshConfigs();
            RefreshCards(selectedTarget);
        }

        private void configBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingConfigs || !(configBox.SelectedItem is string name) || name == _configs.CurrentName) return;
            ChangeConfig(() => _configs.Select(name));
        }

        private void configBox_DropDown(object sender, EventArgs e)
        {
            RefreshConfigs(false);
            UpdateStatus();
        }

        private void createConfigButton_Click(object sender, EventArgs e)
        {
            ChangeConfig(() => _configs.Create(configNameBox.Text));
        }

        private void renameConfigButton_Click(object sender, EventArgs e)
        {
            ChangeConfig(() => _configs.Rename(configNameBox.Text), _selectedTarget);
        }

        private void deleteConfigButton_Click(object sender, EventArgs e)
        {
            if (_configs.CurrentName == null) return;
            if (MessageBox.Show(this, "Delete config \"" + _configs.CurrentName + "\" and all its rules?",
                "Delete override config", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            ChangeConfig(_configs.Delete);
        }

        private void openConfigsFolderButton_Click(object sender, EventArgs e)
        {
            try
            {
                Directory.CreateDirectory(_configs.DirectoryPath);
                Process.Start(new ProcessStartInfo(_configs.DirectoryPath) { UseShellExecute = true });
            }
            catch (Exception exception)
            {
                _configError = "Could not open configs folder: " + exception.Message;
                UpdateStatus();
            }
        }

        private void QueueSave()
        {
            _configs.QueueSave();
            saveConfigTimer.Stop();
            saveConfigTimer.Start();
            UpdateStatus();
        }

        private void saveConfigTimer_Tick(object sender, EventArgs e)
        {
            saveConfigTimer.Stop();
            try
            {
                _configs.Flush();
                _configError = null;
            }
            catch (Exception exception)
            {
                _configError = "Could not save config: " + exception.Message;
            }
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            statusLabel.ForeColor = _configError == null
                ? System.Drawing.SystemColors.ControlText : System.Drawing.Color.Firebrick;
            if (_configError != null)
                statusLabel.Text = _configError;
            else if (_configs.CurrentName == null)
                statusLabel.Text = "Create or select a config to edit its card overrides.";
            else if (_configs.HasPendingChanges)
                statusLabel.Text = "Changes applied. Saving config...";
            else if (_selectedTarget == null)
                statusLabel.Text = "Choose an original card on the left, then click Add card.";
            else
                statusLabel.Text = "Config saved. Reopen the card or deck in SNAP to refresh it.";
        }

        private void RefreshCards(string selectedTarget)
        {
            ListViewItem selected = null;
            _loadingList = true;
            try
            {
                rulesList.BeginUpdate();
                try
                {
                    rulesList.Items.Clear();
                    foreach (var rule in _store.GetRules().OrderBy(rule => rule.TargetCardId, StringComparer.OrdinalIgnoreCase))
                        rulesList.Items.Add(new ListViewItem(rule.ToString())
                        {
                            Tag = rule,
                            Checked = rule.Enabled,
                            ToolTipText = rule.ToString()
                        });
                }
                finally
                {
                    rulesList.EndUpdate();
                }
                selected = FindRuleItem(selectedTarget) ?? rulesList.Items.Cast<ListViewItem>().FirstOrDefault();
                if (selected != null)
                {
                    selected.Selected = true;
                    selected.Focused = true;
                }
                ResizeRuleColumn();
            }
            finally
            {
                _loadingList = false;
            }
            LoadEditor(selected?.Tag as CardOverrideRule);
        }

        private ListViewItem FindRuleItem(string targetCardId)
        {
            return rulesList.Items.Cast<ListViewItem>()
                .FirstOrDefault(item => ((CardOverrideRule)item.Tag).TargetCardId == targetCardId);
        }

        private void ResizeRuleColumn()
        {
            rulesList.AutoResizeColumn(0, ColumnHeaderAutoResizeStyle.ColumnContent);
            ruleColumn.Width = Math.Max(ruleColumn.Width, rulesList.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
        }

        private void LoadEditor(CardOverrideRule saved)
        {
            _loadingEditor = true;
            try
            {
                var rule = saved ?? new CardOverrideRule();
                _selectedTarget = saved?.TargetCardId;
                selectedCardLabel.Text = _selectedTarget ?? "Select a card from the list.";
                SetOption(replaceCardCheckBox, replacementCardBox, rule.ReplacementCardId);
                SetOption(variantCheckBox, variantBox, rule.VariantId);
                ensureVariantMatchCheckBox.Checked = rule.EnsureVariantMatches;
                SetOption(surfaceEffectCheckBox, surfaceEffectBox, rule.SurfaceEffectId);
                SetOption(revealEffectCheckBox, revealEffectBox, rule.RevealEffectId);
                SetOption(borderCheckBox, borderBox, rule.BorderId);
                SetOption(cardBackCheckBox, cardBackBox, rule.CardBackId);
                SetOption(factionCheckBox, factionBox, rule.FactionId);
                costCheckBox.Checked = rule.Cost.HasValue;
                costNumeric.Value = rule.Cost ?? 0;
                powerCheckBox.Checked = rule.Power.HasValue;
                powerNumeric.Value = rule.Power ?? 0;
                force3DCheckBox.Checked = rule.Force3D;
                descriptionCheckBox.Checked = rule.Description != null;
                descriptionTextBox.Text = rule.Description ?? "";
                editorGroup.Enabled = saved != null && _configs.CurrentName != null;
                removeRuleButton.Enabled = editorGroup.Enabled;
                UpdateEnabledControls();
                UpdateStatus();
            }
            finally
            {
                _loadingEditor = false;
            }
        }

        private static void SetOption(CheckBox enabled, SearchComboBox combo, string value)
        {
            enabled.Checked = value != null;
            combo.SetSelection(value);
        }

        private void UpdateEnabledControls()
        {
            replacementCardBox.Enabled = replaceCardCheckBox.Checked;
            variantBox.Enabled = variantCheckBox.Checked;
            ensureVariantMatchCheckBox.Enabled = variantCheckBox.Checked;
            surfaceEffectBox.Enabled = surfaceEffectCheckBox.Checked;
            revealEffectBox.Enabled = revealEffectCheckBox.Checked;
            borderBox.Enabled = borderCheckBox.Checked;
            cardBackBox.Enabled = cardBackCheckBox.Checked;
            factionBox.Enabled = factionCheckBox.Checked;
            costNumeric.Enabled = costCheckBox.Checked;
            powerNumeric.Enabled = powerCheckBox.Checked;
            descriptionTextBox.Enabled = descriptionCheckBox.Checked;
        }

        private void editor_Changed(object sender, EventArgs e)
        {
            if (_loadingEditor || _selectedTarget == null || _configs.CurrentName == null) return;
            if (sender is SearchComboBox combo && combo.IsUpdating) return;
            UpdateEnabledControls();
            SaveSelectedCard();
        }

        private static string ListedSelection(SearchComboBox combo)
        {
            return combo.FindItem(combo.Text.Trim()) as string;
        }

        private void selection_Validating(object sender, CancelEventArgs e)
        {
            var combo = (SearchComboBox)sender;
            var selected = ListedSelection(combo);
            var wasLoading = _loadingEditor;
            _loadingEditor = true;
            try
            {
                combo.SetSelection(selected);
            }
            finally
            {
                _loadingEditor = wasLoading;
            }
            if (combo != targetCardBox) editor_Changed(sender, EventArgs.Empty);
        }

        private static string ReadOption(CheckBox enabled, SearchComboBox combo, bool allowCustom = false)
        {
            if (!enabled.Checked) return null;
            var value = allowCustom ? combo.Text.Trim() : ListedSelection(combo);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private void SaveSelectedCard()
        {
            var item = FindRuleItem(_selectedTarget);
            if (item == null) return;
            var rule = new CardOverrideRule
            {
                TargetCardId = _selectedTarget,
                Enabled = item.Checked,
                ReplacementCardId = ReadOption(replaceCardCheckBox, replacementCardBox),
                VariantId = ReadOption(variantCheckBox, variantBox, true),
                EnsureVariantMatches = ensureVariantMatchCheckBox.Checked,
                SurfaceEffectId = ReadOption(surfaceEffectCheckBox, surfaceEffectBox),
                RevealEffectId = ReadOption(revealEffectCheckBox, revealEffectBox),
                BorderId = ReadOption(borderCheckBox, borderBox),
                CardBackId = ReadOption(cardBackCheckBox, cardBackBox),
                FactionId = ReadOption(factionCheckBox, factionBox),
                Cost = costCheckBox.Checked ? (int?)costNumeric.Value : null,
                Power = powerCheckBox.Checked ? (int?)powerNumeric.Value : null,
                Force3D = force3DCheckBox.Checked,
                Description = descriptionCheckBox.Checked ? descriptionTextBox.Text : null
            };
            _store.Save(rule);
            item.Tag = rule;
            var text = rule.ToString();
            if (item.Text != text)
            {
                item.Text = text;
                item.ToolTipText = text;
                ResizeRuleColumn();
            }
            QueueSave();
        }

        private void rulesList_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_loadingList || _configs.CurrentName == null) return;
            var rule = ((CardOverrideRule)e.Item.Tag).Copy();
            rule.Enabled = e.Item.Checked;
            e.Item.Tag = rule;
            _store.Save(rule);
            QueueSave();
        }

        private void rulesList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingList) return;
            var selected = rulesList.SelectedItems.Cast<ListViewItem>().FirstOrDefault();
            LoadEditor(selected?.Tag as CardOverrideRule);
        }

        private void addCardButton_Click(object sender, EventArgs e)
        {
            if (_configs.CurrentName == null) return;
            var target = ListedSelection(targetCardBox);
            if (target == null)
            {
                statusLabel.Text = "Choose an original card from the list before adding it.";
                targetCardBox.Focus();
                return;
            }
            if (!_store.GetRules().Any(rule => rule.TargetCardId == target))
            {
                _store.Save(new CardOverrideRule { TargetCardId = target });
                QueueSave();
            }
            RefreshCards(target);
            replaceCardCheckBox.Focus();
        }

        private void removeRuleButton_Click(object sender, EventArgs e)
        {
            if (_selectedTarget == null || _configs.CurrentName == null) return;
            _store.Remove(_selectedTarget);
            QueueSave();
            RefreshCards(null);
        }

        private void CardOverridesForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            ValidateChildren(ValidationConstraints.Enabled);
            saveConfigTimer.Stop();
            try
            {
                _configs.Flush();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, "The latest config changes could not be saved.\r\n\r\n" + exception.Message,
                    "Override configs", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void closeButton_Click(object sender, EventArgs e) => Close();
    }
}
