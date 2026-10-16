using System;
using System.Threading;

namespace SnapCardViewHook.Core.Capture
{
    internal static class CaptureBitmapCopy
    {
        public static unsafe void WriteRows(CapturedCardPixels pixels, IntPtr destination, int stride,
            CapturePixelLayout layout, CancellationToken cancellation = default)
        {
            if (pixels == null || pixels.NeedsCpuProcessing || pixels.Width <= 0 || pixels.Height <= 0 || pixels.Layout != layout ||
                (layout != CapturePixelLayout.StraightBgra && layout != CapturePixelLayout.PremultipliedBgra))
                throw new ArgumentException("Invalid BGRA capture frame.", nameof(pixels));
            var rowBytes = checked(pixels.Width * 4);
            if (destination == IntPtr.Zero || Math.Abs((long)stride) < rowBytes ||
                pixels.Data?.Length != checked(rowBytes * pixels.Height))
                throw new ArgumentException("Invalid bitmap destination or capture buffer.");
            fixed (byte* source = pixels.Data)
            {
                for (var y = 0; y < pixels.Height; y++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    // Unity rows are bottom-up, GDI+ rows are top-down. All pixel
                    // math and channel conversion have already finished.
                    Buffer.MemoryCopy(source + checked((pixels.Height - 1 - y) * rowBytes),
                        (byte*)destination + checked(y * stride), rowBytes, rowBytes);
                }
            }
        }
    }
}
