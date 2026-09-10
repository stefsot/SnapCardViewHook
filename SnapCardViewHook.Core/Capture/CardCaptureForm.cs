using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace SnapCardViewHook.Core.Capture
{
    internal sealed class CardCaptureForm : Form
    {
        private readonly NumericUpDown _width = new NumericUpDown { Minimum = 64, Maximum = 4096, Value = 800, Increment = 64 };
        private readonly NumericUpDown _height = new NumericUpDown { Minimum = 64, Maximum = 4096, Value = 800, Increment = 64 };
        private readonly NumericUpDown _padding = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 0 };
        private readonly CheckBox _transparent = new CheckBox { Text = "Transparent background (experimental)", AutoSize = true };
        private readonly CheckBox _shadow = new CheckBox { Text = "Include the card's shadow", AutoSize = true };
        private readonly Label _status = new Label { AutoSize = false };
        private readonly Button _capture = new Button { Text = "Capture PNG..." };
        private readonly Button _cancel = new Button { Text = "Cancel", Enabled = false };
        private readonly Button _open = new Button { Text = "Open PNG", Enabled = false };
        private CancellationTokenSource _cancellation;
        private string _savedPath;

        public CardCaptureForm()
        {
            Text = "Capture card";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleDimensions = new SizeF(6, 13);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(510, 326);
            Add(new Label { Text = "Open the card's details in the game and wait for its effects to load.\nThis renders the card into a separate image; it is not a screenshot.", AutoSize = false }, 16, 14, 478, 40);
            Add(new Label { Text = "Width", AutoSize = true }, 16, 68, 45, 20);
            Add(_width, 64, 64, 96, 24);
            Add(new Label { Text = "Height", AutoSize = true }, 190, 68, 45, 20);
            Add(_height, 241, 64, 96, 24);
            Add(new Label { Text = "Padding per side (%)", AutoSize = true }, 16, 106, 135, 20);
            Add(_padding, 154, 102, 75, 24);
            Add(_transparent, 16, 140, 330, 24);
            Add(_shadow, 16, 168, 270, 24);
            Add(_status, 16, 205, 478, 66);
            Add(_capture, 16, 282, 135, 28);
            Add(_cancel, 165, 282, 90, 28);
            Add(_open, 369, 282, 125, 28);
            _status.Text = "Default: opaque black background. Transparency uses three isolated renders; additive glow is approximated for PNG.";
            _capture.Click += CaptureClicked;
            _cancel.Click += (sender, args) => _cancellation?.Cancel();
            _open.Click += OpenClicked;
            FormClosing += (sender, args) => _cancellation?.Cancel();
        }

        private void Add(Control control, int x, int y, int width, int height)
        {
            control.SetBounds(x, y, width, height);
            Controls.Add(control);
        }

        internal CardCaptureOptions GetFrameOptions() => new CardCaptureOptions
        {
            Width = (int)_width.Value, Height = (int)_height.Value, PaddingPercent = (float)_padding.Value,
            TransparentBackground = _transparent.Checked, IncludeShadow = _shadow.Checked
        };

        private async void CaptureClicked(object sender, EventArgs args)
        {
            if (_cancellation != null) return;
            using (var dialog = new SaveFileDialog
            {
                Title = "Save isolated card image", Filter = "PNG image (*.png)|*.png", DefaultExt = "png", AddExtension = true,
                FileName = "Card-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".png",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                OverwritePrompt = true, CheckPathExists = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var options = GetFrameOptions();
                options.OutputPath = dialog.FileName;
                using (var cancellation = new CancellationTokenSource())
                {
                    _cancellation = cancellation;
                    SetWorking(true);
                    _status.Text = options.TransparentBackground
                        ? "Waiting for the game, rendering color and transparency passes, then saving PNG..."
                        : "Waiting for the game, rendering the isolated card, then saving PNG...";
                    try
                    {
                        var result = await CardImageCapture.CaptureAsync(options, cancellation.Token);
                        if (IsDisposed) return;
                        _savedPath = Path.GetFullPath(options.OutputPath);
                        _status.Text = $"Saved {result.Width} x {result.Height}; {result.RendererCount} card renderers.\n{_savedPath}";
                        _open.Enabled = true;
                    }
                    catch (OperationCanceledException)
                    {
                        if (!IsDisposed) _status.Text = "Capture canceled. Any in-progress Unity render finishes and restores its state before returning.";
                    }
                    catch (Exception e)
                    {
                        if (IsDisposed) return;
                        _status.Text = "Capture failed. No existing file was overwritten.";
                        ShowCaptureError(this, e);
                    }
                    finally
                    {
                        _cancellation = null;
                        if (!IsDisposed) SetWorking(false);
                    }
                }
            }
        }

        private void SetWorking(bool working)
        {
            _capture.Enabled = !working; _cancel.Enabled = working;
            _width.Enabled = !working; _height.Enabled = !working; _padding.Enabled = !working;
            _transparent.Enabled = !working; _shadow.Enabled = !working;
            _open.Enabled = !working && _savedPath != null;
        }

        internal static void ShowCaptureError(IWin32Window owner, Exception error, string title = "Card capture failed")
        {
            using (var dialog = new Form
            {
                Text = title, StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(800, 480), MinimumSize = new Size(500, 300), MinimizeBox = false, ShowInTaskbar = false
            })
            {
                var details = new TextBox
                {
                    Multiline = true, ReadOnly = true, WordWrap = false, ScrollBars = ScrollBars.Both,
                    Dock = DockStyle.Fill, Text = error.ToString()
                };
                var buttons = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6)
                };
                var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.OK };
                var copy = new Button { Text = "Copy details", AutoSize = true };
                copy.Click += (sender, args) =>
                {
                    try { Clipboard.SetText(details.Text); }
                    catch (Exception e) { MessageBox.Show(dialog, e.Message, "Could not copy details", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                };
                buttons.Controls.Add(close);
                buttons.Controls.Add(copy);
                dialog.Controls.Add(details);
                dialog.Controls.Add(buttons);
                dialog.AcceptButton = close;
                dialog.CancelButton = close;
                details.Select(0, 0);
                dialog.ShowDialog(owner);
            }
        }

        private void OpenClicked(object sender, EventArgs args)
        {
            if (_savedPath == null) return;
            try { Process.Start(new ProcessStartInfo(_savedPath) { UseShellExecute = true }); }
            catch (Exception e) { MessageBox.Show(this, e.Message, "Could not open PNG", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }
}
