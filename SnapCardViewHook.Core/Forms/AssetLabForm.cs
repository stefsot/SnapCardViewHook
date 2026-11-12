using SnapCardViewHook.Core.AssetLab;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SnapCardViewHook.Core.Forms
{
    internal partial class AssetLabForm : Form
    {
        private readonly AssetLabController _controller;
        private AssetLabEntry[] _entries = Array.Empty<AssetLabEntry>();
        private CancellationTokenSource _scanCancellation;
        private bool _busy, _closing;
        private string _shownError;

        public AssetLabForm(AssetLabController controller)
        {
            _controller = controller;
            InitializeComponent();
            kindBox.SelectedIndex = 0;
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await Scan();
        }

        private async void scanButton_Click(object sender, EventArgs e) => await Scan();

        private async Task Scan()
        {
            if (_busy || _closing) return;
            _scanCancellation?.Dispose();
            _scanCancellation = new CancellationTokenSource();
            _entries = Array.Empty<AssetLabEntry>();
            FilterEntries();
            SetBusy(true);
            cancelButton.Enabled = true;
            statusLabel.Text = "Reading the running client's definitions and local asset index...";
            try
            {
                var progress = new Progress<string>(message =>
                {
                    if (!_closing && !IsDisposed) statusLabel.Text = message;
                });
                var entries = await _controller.ScanAsync(progress, _scanCancellation.Token);
                if (_closing) return;
                _entries = entries;
                FilterEntries();
                statusLabel.Text = entries.Length.ToString("N0") + " asset groups; " + entries.Count(e => e.Stray).ToString("N0") +
                    " unlisted or incomplete entries; " + entries.Count(e => e.Stray && e.CanPreview).ToString("N0") + " ready to preview.";
            }
            catch (OperationCanceledException) { if (!_closing) statusLabel.Text = "Scan cancelled."; }
            catch (Exception error) { ShowError(error); }
            finally { if (!_closing) { cancelButton.Enabled = false; SetBusy(false); } }
        }

        private void cancelButton_Click(object sender, EventArgs e) => _scanCancellation?.Cancel();

        private void filter_Changed(object sender, EventArgs e) => FilterEntries();

        private void FilterEntries()
        {
            var selected = SelectedEntry?.AssetId;
            var query = Normalize(searchBox.Text);
            var values = _entries.Where(entry => (showDefinedBox.Checked || entry.Stray) &&
                (kindBox.SelectedIndex <= 0 || entry.Variant == (kindBox.SelectedIndex == 2)) &&
                (query.Length == 0 || Normalize(entry.AssetId + " " + entry.CardId + " " + entry.Name + " " + entry.Status).Contains(query)))
                .ToArray();
            assetsGrid.DataSource = new BindingList<AssetLabEntry>(values);
            var index = Array.FindIndex(values, value => value.AssetId == selected);
            if (index >= 0) assetsGrid.CurrentCell = assetsGrid.Rows[index].Cells[0];
            LoadSelection();
        }

        private static string Normalize(string value) => new string((value ?? string.Empty).Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant).ToArray());

        private AssetLabEntry SelectedEntry => assetsGrid.CurrentRow?.DataBoundItem as AssetLabEntry;

        private void assetsGrid_SelectionChanged(object sender, EventArgs e) => LoadSelection();

        private void LoadSelection()
        {
            var entry = SelectedEntry;
            nameBox.Text = entry?.Name ?? string.Empty;
            descriptionBox.Text = entry?.Description ?? string.Empty;
            costBox.Value = Math.Max(costBox.Minimum, Math.Min(costBox.Maximum, entry?.Cost ?? 0));
            powerBox.Value = Math.Max(powerBox.Minimum, Math.Min(powerBox.Maximum, entry?.Power ?? 0));
            sourceLabel.Text = entry == null ? string.Empty : entry.HasCardData ? "Values from client" : "Placeholder values";
            detailsBox.Text = entry?.Details ?? string.Empty;
            bakeSetBox.DataSource = entry?.Bakes.ToArray() ?? Array.Empty<AssetLabBake>();
            bakedBox.Checked = entry != null && entry.CanPreviewBaked && (!entry.CanPreviewMaterials || entry.HasWipMaterials);
            UpdateActions();
        }

        private async void previewButton_Click(object sender, EventArgs e)
        {
            var entry = SelectedEntry;
            if (_busy || entry == null || !entry.CanPreview) return;
            SetBusy(true);
            _shownError = null;
            statusLabel.Text = "Reconstructing " + entry.AssetId + "...";
            try
            {
                var name = string.IsNullOrWhiteSpace(nameBox.Text) ? AssetLabEntry.FriendlyName(entry.CardId) : nameBox.Text.Trim();
                var bake = bakedBox.Checked ? bakeSetBox.SelectedItem as AssetLabBake : null;
                if (bakedBox.Checked && bake == null) throw new InvalidOperationException("Select an available bake to preview.");
                var status = await _controller.PreviewAsync(entry, name, descriptionBox.Text, (int)costBox.Value, (int)powerBox.Value, bake);
                if (!_closing) statusLabel.Text = status;
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { ShowError(error); }
            finally { if (!_closing) SetBusy(false); }
        }

        private async void stopButton_Click(object sender, EventArgs e)
        {
            if (_busy) return;
            SetBusy(true);
            try
            {
                await _controller.StopAsync();
                if (!_closing) statusLabel.Text = "Preview stopped. Normal card views restored.";
            }
            catch (Exception error) { ShowError(error); }
            finally { if (!_closing) SetBusy(false); }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            scanButton.Enabled = !busy;
            assetsGrid.Enabled = !busy;
            previewTabs.Enabled = !busy;
            filterPanel.Enabled = !busy;
            showDefinedBox.Enabled = !busy;
            UpdateActions();
        }

        private void UpdateActions()
        {
            var entry = SelectedEntry;
            bakedBox.Enabled = !_busy && entry?.CanPreviewBaked == true && entry.CanPreviewMaterials;
            bakeSetBox.Enabled = !_busy && bakedBox.Checked && entry?.CanPreviewBaked == true;
            previewButton.Enabled = !_busy && (bakedBox.Checked
                ? entry?.CanPreviewBaked == true && bakeSetBox.SelectedItem is AssetLabBake
                : entry?.CanPreviewMaterials == true);
            stopButton.Enabled = !_busy && _controller.Active;
        }

        private void bakedBox_CheckedChanged(object sender, EventArgs e) => UpdateActions();

        private void statusTimer_Tick(object sender, EventArgs e)
        {
            UpdateActions();
            var error = _controller.Error;
            if (error == null || error == _shownError) return;
            _shownError = error;
            statusLabel.Text = "Preview stopped: " + error;
        }

        private void ShowError(Exception error)
        {
            if (_closing || IsDisposed) return;
            statusLabel.Text = error.Message;
            MessageBox.Show(this, error.ToString(), "Asset Lab", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _closing = true;
            statusTimer.Stop();
            _scanCancellation?.Cancel();
            _scanCancellation?.Dispose();
            _controller.Close();
            base.OnFormClosed(e);
        }
    }
}
