using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SnapCardViewHook.Core.Capture
{
    internal sealed class CardLivePreviewForm : Form
    {
        private readonly PictureBox _picture = new PictureBox
        {
            BackColor = Color.FromArgb(48, 48, 48), SizeMode = PictureBoxSizeMode.Normal
        };
        private readonly Panel _viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        private readonly CheckBox _transparent = new CheckBox
        {
            Text = "Transparent", AutoSize = true, Margin = new Padding(8, 8, 3, 3)
        };
        private readonly CardLivePreview _preview;
        private SynchronizationContext _uiContext;
        private TransparentCardPreviewForm _overlay;
        private Size _coloredClientSize;
        private FormWindowState _coloredWindowState;
        private Bitmap _front, _back;
        private bool _converting;

        public CardLivePreviewForm(CardCaptureOptions options, int fps)
        {
            Text = "Live card preview";
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(options.Width, options.Height + 40);
            MinimumSize = new Size(320, 320);
            StartPosition = FormStartPosition.CenterParent;
            _preview = new CardLivePreview(options, fps);
            _picture.Size = new Size(options.Width, options.Height);
            _viewport.BackColor = _picture.BackColor;
            _viewport.Controls.Add(_picture);
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40 };
            var background = new Button { Text = "Background...", AutoSize = true };
            background.Click += (sender, args) =>
            {
                using (var dialog = new ColorDialog { Color = _picture.BackColor, FullOpen = true })
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        _viewport.BackColor = _picture.BackColor = dialog.Color;
            };
            toolbar.Controls.Add(background);
            toolbar.Controls.Add(_transparent);
            _transparent.CheckedChanged += TransparencyChanged;
            Controls.Add(_viewport);
            Controls.Add(toolbar);
            Shown += (sender, args) =>
            {
                _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
                _preview.FrameReady += OnFrameReady;
                _preview.Start();
            };
        }

        private void TransparencyChanged(object sender, EventArgs args)
        {
            if (!_transparent.Checked) { RestoreColoredPreview(); return; }
            _coloredWindowState = WindowState;
            WindowState = FormWindowState.Normal;
            _coloredClientSize = ClientSize;
            try
            {
                _overlay = new TransparentCardPreviewForm(_picture.Size);
                _overlay.FormClosed += OverlayClosed;
                _viewport.Visible = false;
                MinimumSize = Size.Empty;
                FormBorderStyle = FormBorderStyle.FixedSingle;
                MaximizeBox = false;
                ClientSize = new Size(Math.Max(320, _coloredClientSize.Width), 40);
                PositionOverlay();
                _overlay.SetFrame(_picture.Image as Bitmap);
                _overlay.Show(this);
            }
            catch (Exception error)
            {
                _transparent.Checked = false;
                MessageBox.Show(this, error.Message, "Transparent preview", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestoreColoredPreview()
        {
            CloseOverlay();
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = new Size(320, 320);
            ClientSize = _coloredClientSize;
            _viewport.Visible = true;
            WindowState = _coloredWindowState;
        }

        private void OverlayClosed(object sender, FormClosedEventArgs args)
        {
            _overlay = null;
            if (args.CloseReason != CloseReason.FormOwnerClosing && !IsDisposed && !Disposing)
                _transparent.Checked = false;
        }

        private void CloseOverlay()
        {
            var overlay = _overlay;
            _overlay = null;
            if (overlay == null) return;
            overlay.FormClosed -= OverlayClosed;
            overlay.Dispose();
        }

        private void PositionOverlay()
        {
            if (_overlay == null || WindowState == FormWindowState.Minimized) return;
            _overlay.Location = new Point(PointToScreen(Point.Empty).X, Bottom);
        }

        protected override void OnLocationChanged(EventArgs args)
        {
            base.OnLocationChanged(args);
            PositionOverlay();
        }

        protected override void OnSizeChanged(EventArgs args)
        {
            base.OnSizeChanged(args);
            PositionOverlay();
        }

        private void ShowFrame(Bitmap bitmap)
        {
            _picture.Image = bitmap;
            try { _overlay?.SetFrame(bitmap); }
            catch (Exception error)
            {
                System.Diagnostics.Debug.WriteLine(error);
                _transparent.Checked = false;
            }
        }

        private void OnFrameReady()
        {
            // Post through the UI context so form handle recreation cannot lose a notification.
            try { _uiContext.Post(DisplayFrame, null); }
            catch (InvalidOperationException) { } // The UI thread has shut down.
        }

        private async void DisplayFrame(object state)
        {
            if (_converting || IsDisposed || Disposing) return;
            _converting = true;
            try
            {
                while (!IsDisposed && !Disposing)
                {
                    var pixels = _preview.TakeFrame();
                    if (pixels == null) break;
                    Bitmap bitmap = null;
                    try
                    {
                        if (pixels.Rgba == null) { ShowFrame(null); continue; }
                        bitmap = _back;
                        _back = null;
                        await Task.Run(() =>
                        {
                            bitmap ??= new Bitmap(pixels.Width, pixels.Height, PixelFormat.Format32bppPArgb);
                            WriteBitmap(pixels, bitmap);
                        });
                        if (IsDisposed || Disposing) return;
                        ShowFrame(bitmap);
                        _back = _front;
                        _front = bitmap;
                        bitmap = null;
                    }
                    catch (Exception error)
                    {
                        System.Diagnostics.Debug.WriteLine(error);
                        if (!IsDisposed && !Disposing) ShowFrame(null);
                    }
                    finally
                    {
                        bitmap?.Dispose();
                        _preview.ReturnFrame(pixels);
                    }
                }
            }
            finally { _converting = false; }
        }

        private static void WriteBitmap(CapturedCardPixels pixels, Bitmap bitmap)
        {
            var data = bitmap.LockBits(new Rectangle(0, 0, pixels.Width, pixels.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                CaptureAlphaReconstruction.WritePremultipliedBgra(pixels, data.Scan0, data.Stride);
            }
            finally { bitmap.UnlockBits(data); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _preview.FrameReady -= OnFrameReady;
                _preview.Dispose();
                CloseOverlay();
                _picture.Image = null;
                _front?.Dispose(); _front = null;
                _back?.Dispose(); _back = null;
            }
            base.Dispose(disposing);
        }
    }
}
