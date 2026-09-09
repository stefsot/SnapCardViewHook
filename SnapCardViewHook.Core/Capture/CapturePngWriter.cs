using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace SnapCardViewHook.Core.Capture
{
    internal static class CapturePngWriter
    {
        public static void Write(CapturedCardPixels pixels, CardCaptureOptions options, CancellationToken cancellation)
        {
            if (pixels == null || pixels.Rgba == null || pixels.Width != options.Width || pixels.Height != options.Height ||
                pixels.Rgba.Length != checked(pixels.Width * pixels.Height * 4))
                throw new ArgumentException("Invalid RGBA capture buffer.");
            cancellation.ThrowIfCancellationRequested();
            
            using var bitmap = new Bitmap(pixels.Width, pixels.Height, PixelFormat.Format32bppArgb);
            using var encoded = new MemoryStream();
            
            var data = bitmap.LockBits(new Rectangle(0, 0, pixels.Width, pixels.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[checked(pixels.Width * 4)];
                for (var y = 0; y < pixels.Height; y++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    // unity ReadPixels starts at the lower left, GDI+ expects top-down BGRA rows.
                    var source = checked((pixels.Height - 1 - y) * row.Length);
                    for (var x = 0; x < row.Length; x += 4)
                    {
                        row[x] = pixels.Rgba[source + x + 2];
                        row[x + 1] = pixels.Rgba[source + x + 1];
                        row[x + 2] = pixels.Rgba[source + x];
                        row[x + 3] = options.TransparentBackground ? pixels.Rgba[source + x + 3] : (byte)255;
                    }
                    Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, checked(y * data.Stride)), row.Length);
                }
            }
            finally { bitmap.UnlockBits(data); }
            
            bitmap.Save(encoded, ImageFormat.Png);
            
            cancellation.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(options.OutputPath));

            using var output = new FileStream(options.OutputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            
            encoded.Position = 0;
            encoded.CopyTo(output);
            output.Flush();
        }
    }
}
