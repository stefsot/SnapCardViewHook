using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

namespace SnapCardViewHook.Core.Capture
{
    internal static class CapturePngWriter
    {
        public static void Write(CapturedCardPixels pixels, CardCaptureOptions options, CancellationToken cancellation)
        {
            if (pixels == null || pixels.Width != options.Width || pixels.Height != options.Height)
                throw new ArgumentException("Invalid capture dimensions.");
            cancellation.ThrowIfCancellationRequested();
            
            using var bitmap = new Bitmap(pixels.Width, pixels.Height, PixelFormat.Format32bppArgb);
            using var encoded = new MemoryStream();
            
            var data = bitmap.LockBits(new Rectangle(0, 0, pixels.Width, pixels.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                CaptureBitmapCopy.WriteRows(pixels, data.Scan0, data.Stride, CapturePixelLayout.StraightBgra, cancellation);
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
