using System;
using System.Drawing;
using System.Drawing.Imaging;
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
        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer { Interval = 16 };
        private readonly CardLivePreview _preview;
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
            var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = _picture.BackColor };
            viewport.Controls.Add(_picture);
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40 };
            var background = new Button { Text = "Background...", AutoSize = true };
            background.Click += (sender, args) =>
            {
                using (var dialog = new ColorDialog { Color = _picture.BackColor, FullOpen = true })
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        viewport.BackColor = _picture.BackColor = dialog.Color;
            };
            toolbar.Controls.Add(background);
            Controls.Add(viewport);
            Controls.Add(toolbar);
            _timer.Tick += DisplayFrame;
            Shown += (sender, args) => { _preview.Start(); _timer.Start(); };
        }

        private async void DisplayFrame(object sender, EventArgs args)
        {
            if (_converting) return;
            var pixels = _preview.TakeFrame();
            if (pixels == null) return;
            if (pixels.Rgba == null) { _picture.Image = null; return; }
            _converting = true;
            var bitmap = _back;
            _back = null;
            try
            {
                await Task.Run(() =>
                {
                    bitmap ??= new Bitmap(pixels.Width, pixels.Height, PixelFormat.Format32bppPArgb);
                    WriteBitmap(pixels, bitmap);
                });
                if (IsDisposed || Disposing) return;
                _picture.Image = bitmap;
                _back = _front;
                _front = bitmap;
                bitmap = null;
            }
            catch (Exception error)
            {
                System.Diagnostics.Debug.WriteLine(error);
                if (!IsDisposed && !Disposing) _picture.Image = null;
            }
            finally
            {
                bitmap?.Dispose();
                _preview.ReturnFrame(pixels);
                _converting = false;
            }
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
                _timer.Stop();
                _timer.Dispose();
                _preview.Dispose();
                _picture.Image = null;
                _front?.Dispose(); _front = null;
                _back?.Dispose(); _back = null;
            }
            base.Dispose(disposing);
        }
    }
}
