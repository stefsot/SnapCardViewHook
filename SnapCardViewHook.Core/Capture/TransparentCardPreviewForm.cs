using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SnapCardViewHook.Core.Capture
{
    internal sealed class TransparentCardPreviewForm : Form
    {
        private readonly Size _frameSize;
        private readonly int _rowBytes, _byteCount;
        private IntPtr _dc, _bitmap, _previousBitmap, _bits;

        public TransparentCardPreviewForm(Size frameSize)
        {
            if (frameSize.Width <= 0 || frameSize.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(frameSize));
            _frameSize = frameSize;
            _rowBytes = checked(frameSize.Width * 4);
            _byteCount = checked(_rowBytes * frameSize.Height);
            Text = "Live card overlay";
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = true;
            ClientSize = frameSize;
            try { CreateBuffer(); }
            catch { Dispose(); throw; }
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= 0x00080000; // WS_EX_LAYERED; no color key or whole-window opacity.
                return parameters;
            }
        }

        protected override void OnHandleCreated(EventArgs args)
        {
            base.OnHandleCreated(args);
            if (_dc != IntPtr.Zero) Present();
        }

        protected override void OnPaintBackground(PaintEventArgs args) { }

        public void SetFrame(Bitmap bitmap)
        {
            if (IsDisposed || Disposing) return;
            if (bitmap == null) ClearBuffer();
            else CopyBitmap(bitmap);
            Present();
        }

        private void CreateBuffer()
        {
            _dc = CreateCompatibleDC(IntPtr.Zero);
            if (_dc == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            var info = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = _frameSize.Width, Height = -_frameSize.Height,
                    Planes = 1, BitCount = 32
                }
            };
            _bitmap = CreateDIBSection(_dc, ref info, 0, out _bits, IntPtr.Zero, 0);
            if (_bitmap == IntPtr.Zero || _bits == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var previous = SelectObject(_dc, _bitmap);
            if (previous == IntPtr.Zero || previous == new IntPtr(-1))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            _previousBitmap = previous;
            ClearBuffer();
        }

        private unsafe void CopyBitmap(Bitmap bitmap)
        {
            if (bitmap.Size != _frameSize || bitmap.PixelFormat != PixelFormat.Format32bppPArgb)
                throw new ArgumentException("The overlay requires a matching premultiplied-alpha frame.", nameof(bitmap));
            var data = bitmap.LockBits(new Rectangle(Point.Empty, _frameSize),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                if (Math.Abs((long)data.Stride) < _rowBytes)
                    throw new InvalidOperationException("Invalid preview bitmap stride.");
                for (var y = 0; y < _frameSize.Height; y++)
                    Buffer.MemoryCopy((byte*)data.Scan0 + checked(y * data.Stride),
                        (byte*)_bits + y * _rowBytes, _rowBytes, _rowBytes);
            }
            finally { bitmap.UnlockBits(data); }
        }

        private unsafe void ClearBuffer()
        {
            var pixels = (uint*)_bits;
            for (var i = 0; i < _byteCount / 4; i++) pixels[i] = 0;
        }

        private void Present()
        {
            var destination = Location;
            var source = Point.Empty;
            var size = _frameSize;
            var blend = new BlendFunction { SourceConstantAlpha = 255, AlphaFormat = 1 };
            if (!UpdateLayeredWindow(Handle, IntPtr.Zero, ref destination, ref size,
                _dc, ref source, 0, ref blend, 2)) // AC_SRC_ALPHA / ULW_ALPHA.
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        protected override void Dispose(bool disposing)
        {
            if (_dc != IntPtr.Zero && _previousBitmap != IntPtr.Zero) SelectObject(_dc, _previousBitmap);
            if (_bitmap != IntPtr.Zero) DeleteObject(_bitmap);
            if (_dc != IntPtr.Zero) DeleteDC(_dc);
            _dc = _bitmap = _previousBitmap = _bits = IntPtr.Zero;
            base.Dispose(disposing);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            public uint Size;
            public int Width, Height;
            public ushort Planes, BitCount;
            public uint Compression, SizeImage;
            public int XPelsPerMeter, YPelsPerMeter;
            public uint ColorsUsed, ColorsImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            public BitmapInfoHeader Header;
            public uint Colors;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BlendFunction
        {
            public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
        }

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr dc);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage,
            out IntPtr bits, IntPtr section, uint offset);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr obj);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteDC(IntPtr dc);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr destinationDc,
            ref Point destination, ref Size size, IntPtr sourceDc, ref Point source,
            uint colorKey, ref BlendFunction blend, uint flags);
    }
}
