using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace SnapCardViewHook.Core.Capture
{
    internal sealed class CardVideoCaptureForm : Form
    {
        private readonly NumericUpDown _width = new NumericUpDown { Minimum = 64, Maximum = 4096, Value = 800, Increment = 64 };
        private readonly NumericUpDown _height = new NumericUpDown { Minimum = 64, Maximum = 4096, Value = 800, Increment = 64 };
        private readonly NumericUpDown _padding = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 0 };
        private readonly NumericUpDown _fps = new NumericUpDown { Minimum = 1, Maximum = 60, Value = 30 };
        private readonly NumericUpDown _duration = new NumericUpDown { Minimum = 1, Maximum = 600, Value = 5 };
        private readonly NumericUpDown _quality = new NumericUpDown { Minimum = 0, Maximum = 32, Value = 4 };
        private readonly NumericUpDown _ramLimit = new NumericUpDown { Minimum = 64, Maximum = 65536, Value = 4096, Increment = 256, ThousandsSeparator = true };
        private readonly Label _memoryEstimate = new Label { AutoSize = false };
        private readonly CheckBox _transparent = new CheckBox { Text = "Transparent background (premultiplied alpha)", AutoSize = true };
        private readonly CheckBox _shadow = new CheckBox { Text = "Include the card's shadow", AutoSize = true };
        private readonly TextBox _ffmpeg = new TextBox { Text = CardVideoOptions.DefaultFfmpegPath };
        private readonly Button _browse = new Button { Text = "Browse..." };
        private readonly Button _record = new Button { Text = "Record MOV..." };
        private readonly Button _stopButton = new Button { Text = "Stop and encode", Enabled = false };
        private readonly Button _open = new Button { Text = "Open MOV", Enabled = false };
        private readonly Label _status = new Label { AutoSize = false };
        private CancellationTokenSource _stop;
        private bool _closeAfterStop;
        private bool _encoding;
        private string _savedPath;

        public CardVideoCaptureForm(CardCaptureOptions frames)
        {
            Text = "Record card video";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleDimensions = new SizeF(6, 13);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(590, 562);
            _width.Value = frames.Width; _height.Value = frames.Height; _padding.Value = (decimal)frames.PaddingPercent;
            _transparent.Checked = frames.TransparentBackground; _shadow.Checked = frames.IncludeShadow;
            Add(new Label
            {
                Text = "Keep the same card's details open in the game, each frame is rendered in isolation.", AutoSize = false
            }, 16, 14, 558, 40);
            Add(new Label { Text = "Width", AutoSize = true }, 16, 68, 45, 20);
            Add(_width, 64, 64, 96, 24);
            Add(new Label { Text = "Height", AutoSize = true }, 190, 68, 45, 20);
            Add(_height, 241, 64, 96, 24);
            Add(new Label { Text = "Padding per side (%)", AutoSize = true }, 16, 106, 135, 20);
            Add(_padding, 154, 102, 75, 24);
            Add(new Label { Text = "FPS", AutoSize = true }, 264, 106, 32, 20);
            Add(_fps, 304, 102, 66, 24);
            Add(new Label { Text = "Seconds", AutoSize = true }, 402, 106, 55, 20);
            Add(_duration, 468, 102, 80, 24);
            Add(_transparent, 16, 140, 390, 24);
            Add(_shadow, 16, 168, 270, 24);
            Add(new Label { Text = "ProRes quality", AutoSize = true }, 16, 207, 108, 20);
            Add(_quality, 130, 203, 70, 24);
            Add(new Label
            {
                Text = "0 = Auto. 1-32: lower = better quality / larger files.\nAlpha is kept at every quality setting.", AutoSize = false
            }, 216, 198, 358, 38);
            Add(new Label { Text = "RAM limit (MB)", AutoSize = true }, 16, 245, 108, 20);
            Add(_ramLimit, 130, 241, 100, 24);
            Add(new Label { Text = "Buffered frames only, not total game RAM.", AutoSize = true }, 246, 245, 328, 20);
            Add(_memoryEstimate, 16, 277, 558, 32);
            Add(new Label { Text = "Local FFmpeg executable location", AutoSize = true }, 16, 316, 558, 20);
            Add(_ffmpeg, 16, 340, 454, 24);
            Add(_browse, 482, 338, 92, 28);
            Add(new Label
            {
                Text = "Record to RAM first, then encode MOV/ProRes 4444 (no audio). No frame files.\nSlow capture repeats frames to preserve timing, lower resolution/FPS if needed.", AutoSize = false
            }, 16, 382, 558, 38);
            Add(_status, 16, 429, 558, 76);
            Add(_record, 16, 518, 135, 28);
            Add(_stopButton, 165, 518, 135, 28);
            Add(_open, 449, 518, 125, 28);
            _status.Text = "Ready";
            _width.ValueChanged += (sender, args) => UpdateMemoryEstimate();
            _height.ValueChanged += (sender, args) => UpdateMemoryEstimate();
            _fps.ValueChanged += (sender, args) => UpdateMemoryEstimate();
            _duration.ValueChanged += (sender, args) => UpdateMemoryEstimate();
            _ramLimit.ValueChanged += (sender, args) => UpdateMemoryEstimate();
            _transparent.CheckedChanged += (sender, args) => UpdateMemoryEstimate();
            UpdateMemoryEstimate();
            _browse.Click += BrowseClicked;
            _record.Click += RecordClicked;
            _stopButton.Click += (sender, args) => RequestStop();
            _open.Click += OpenClicked;
            FormClosing += (sender, args) =>
            {
                if (_stop == null) return;
                args.Cancel = true;
                _closeAfterStop = true;
                RequestStop();
            };
        }

        private void Add(Control control, int x, int y, int width, int height)
        {
            control.SetBounds(x, y, width, height);
            Controls.Add(control);
        }

        private void UpdateMemoryEstimate()
        {
            var megabytes = (double)(_width.Value * _height.Value * 4 * (_transparent.Checked ? 3 : 1) * _fps.Value * _duration.Value) / (1024 * 1024);
            var exceedsLimit = megabytes > (double)_ramLimit.Value;
            _memoryEstimate.Text = $"Estimated frame RAM at target FPS: {megabytes:N0} MB.\n" +
                (exceedsLimit ? "Above the RAM limit: capture will stop early and encode. Raise the limit if safe." : "Encoding starts after capture. Frames stay in RAM until consumed by the encoder.");
            _memoryEstimate.ForeColor = exceedsLimit ? Color.DarkRed : SystemColors.ControlText;
        }

        private void BrowseClicked(object sender, EventArgs args)
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "Select your existing FFmpeg executable", Filter = "FFmpeg executable (ffmpeg.exe)|ffmpeg.exe|Executables (*.exe)|*.exe",
                CheckFileExists = true, Multiselect = false
            })
            {
                if (File.Exists(_ffmpeg.Text)) dialog.FileName = _ffmpeg.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) _ffmpeg.Text = dialog.FileName;
            }
        }

        private async void RecordClicked(object sender, EventArgs args)
        {
            if (_stop != null) return;
            using (var dialog = new SaveFileDialog
            {
                Title = "Save isolated card video", Filter = "ProRes 4444 movie (*.mov)|*.mov", DefaultExt = "mov", AddExtension = true,
                FileName = "Card-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + (_transparent.Checked ? "-premultiplied" : "") + ".mov",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                OverwritePrompt = true, CheckPathExists = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var options = new CardVideoOptions
                {
                    Frames = new CardCaptureOptions
                    {
                        Width = (int)_width.Value, Height = (int)_height.Value, PaddingPercent = (float)_padding.Value,
                        TransparentBackground = _transparent.Checked, IncludeShadow = _shadow.Checked, OutputPath = dialog.FileName
                    },
                    FfmpegPath = _ffmpeg.Text, FramesPerSecond = (int)_fps.Value, DurationSeconds = (int)_duration.Value,
                    ProResQuantizer = (int)_quality.Value, RamLimitMegabytes = (int)_ramLimit.Value
                };
                using (var stop = new CancellationTokenSource())
                {
                    _stop = stop;
                    _closeAfterStop = false;
                    _encoding = false;
                    SetWorking(true);
                    _status.Text = "Waiting for the first game frame... Keep the card-details view open";
                    var acceptProgress = true;
                    var progress = new Progress<CardVideoProgress>(value =>
                    {
                        // Ignore queued progress from an earlier recording or a closing window.
                        if (IsDisposed || _stop != stop || !acceptProgress) return;
                        _encoding = value.Encoding || value.Finalizing;
                        if (_encoding) _stopButton.Enabled = false;
                        if (_encoding)
                        {
                            var phase = value.Finalizing ? "Finalizing MOV" : "Capture finished; encoding from RAM";
                            _status.Text = $"{phase}: {value.VideoSeconds:0.00}s encoded.\n" +
                                $"{value.CapturedFrames} captured; {value.EncodedFrames} written; {value.RepeatedFrames} repeated for timing.";
                        }
                        else
                        {
                            var phase = stop.IsCancellationRequested ? "Stopping capture" : "Recording to RAM";
                            _status.Text = $"{phase}: {value.VideoSeconds:0.00}s / {options.DurationSeconds}s; {value.CapturedFrames} frames.\n" +
                                $"Buffered: {value.BufferedBytes / (1024.0 * 1024):N0} / {options.RamLimitMegabytes:N0} MB. FFmpeg has not started.";
                        }
                    });
                    try
                    {
                        var result = await CardVideoCapture.RecordAsync(options, stop.Token, progress);
                        acceptProgress = false;
                        if (IsDisposed) return;
                        _savedPath = result.OutputPath;
                        _status.Text = $"Saved {result.VideoSeconds:0.00}s; {result.CapturedFrames} captured / {result.RepeatedFrames} repeated.\n{_savedPath}";
                        if (!string.IsNullOrEmpty(result.Warning))
                            CardCaptureForm.ShowCaptureError(this,
                                new InvalidOperationException(result.Warning + "\n\nCaptured frames were saved to:\n" + result.OutputPath),
                                "Movie saved with a warning");
                    }
                    catch (OperationCanceledException)
                    {
                        acceptProgress = false;
                        if (!IsDisposed) _status.Text = "Stopped before the first complete frame. No movie was created.";
                    }
                    catch (Exception error)
                    {
                        acceptProgress = false;
                        if (IsDisposed) return;
                        _status.Text = "Recording failed. Existing files were not overwritten";
                        CardCaptureForm.ShowCaptureError(this, error);
                    }
                    finally
                    {
                        _stop = null;
                        if (!IsDisposed)
                        {
                            SetWorking(false);
                            if (_closeAfterStop) Close();
                        }
                    }
                }
            }
        }

        private void RequestStop()
        {
            if (_stop == null) return;
            if (_encoding)
            {
                _status.Text = "Capture has finished. Waiting for buffered frames to encode and the MOV to finalize...";
                return;
            }
            if (_stop.IsCancellationRequested) return;
            _stopButton.Enabled = false;
            _status.Text = "Stopping capture, then encoding the frames buffered in RAM...";
            _stop.Cancel();
        }

        private void SetWorking(bool working)
        {
            _record.Enabled = !working; _stopButton.Enabled = working;
            _width.Enabled = !working; _height.Enabled = !working; _padding.Enabled = !working;
            _fps.Enabled = !working; _duration.Enabled = !working;
            _quality.Enabled = !working; _ramLimit.Enabled = !working;
            _transparent.Enabled = !working; _shadow.Enabled = !working;
            _ffmpeg.Enabled = !working; _browse.Enabled = !working;
            _open.Enabled = !working && _savedPath != null;
        }

        private void OpenClicked(object sender, EventArgs args)
        {
            if (_savedPath == null) return;
            try { Process.Start(new ProcessStartInfo(_savedPath) { UseShellExecute = true }); }
            catch (Exception error) { MessageBox.Show(this, error.Message, "Could not open MOV", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }
}
