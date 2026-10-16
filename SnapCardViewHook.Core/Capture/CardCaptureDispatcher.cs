using IL2CppApi.Runtime;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SnapCardViewHook.Core.Capture
{
    internal static class CardCaptureDispatcher
    {
        public static async Task<CapturedCardPixels> CaptureAsync(CardCaptureOptions options,
            CardCaptureAnchor anchor, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var completion = new TaskCompletionSource<CapturedCardPixels>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stage = 0; // 0 = queued; 1 = native work/cleanup running; 2 = done or abandoned.
            
            using (cancellation.Register(() =>
            {
                if (Interlocked.CompareExchange(ref stage, 2, 0) == 0) completion.TrySetCanceled();
            }))
            {
                SnapTypeDataCollector.ExecuteActionInGameUiThread(() => 
                {
                    if (Interlocked.CompareExchange(ref stage, 1, 0) != 0) 
                        return;
                    
                    try
                    {
                        cancellation.ThrowIfCancellationRequested();
                        CapturedCardPixels pixels;
                        using (var runtime = new Il2CppRuntime())
                        using (var capture = new UnityCardCapture(runtime))
                        {
                            capture.Prepare(options, cancellation, anchor);
                            var timestamp = Stopwatch.GetTimestamp();
                            pixels = capture.Capture(cancellation);
                            pixels.CaptureTimestamp = timestamp;
                        }
                        completion.TrySetResult(pixels);
                    }
                    catch (OperationCanceledException) { completion.TrySetCanceled(); }
                    catch (Exception error) { completion.TrySetException(error); }
                    finally { Volatile.Write(ref stage, 2); }
                });
                return await completion.Task.ConfigureAwait(false);
            }
        }

        // Called by image/video workers. GPU frames are already in their requested format.
        public static void PreparePixels(CapturedCardPixels pixels, CancellationToken cancellation)
        {
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            cancellation.ThrowIfCancellationRequested();
            if (!pixels.NeedsCpuProcessing) return;
            if (pixels.Width <= 0 || pixels.Height <= 0 ||
                pixels.Data?.Length != checked(pixels.Width * pixels.Height * 4) ||
                !Enum.IsDefined(typeof(CapturePixelLayout), pixels.Layout))
                throw new ArgumentException("Invalid CPU capture frame.", nameof(pixels));

            var transparent = pixels.MatteBlackRgba != null || pixels.MatteWhiteRgba != null;
            if (transparent)
            {
                if (pixels.Layout == CapturePixelLayout.StraightBgra)
                    CaptureAlphaReconstruction.Apply(pixels.Data, pixels.MatteBlackRgba,
                        pixels.MatteWhiteRgba, pixels.LinearColorSpace, cancellation);
                else
                    CaptureAlphaReconstruction.ApplyPremultipliedVideo(pixels.Data, pixels.MatteBlackRgba,
                        pixels.MatteWhiteRgba, pixels.LinearColorSpace, cancellation);
            }

            var swapRedBlue = pixels.Layout != CapturePixelLayout.PremultipliedRgba;
            if (!transparent || swapRedBlue)
            {
                for (var i = 0; i < pixels.Data.Length; i += 4)
                {
                    if ((i & 16383) == 0) cancellation.ThrowIfCancellationRequested();
                    if (!transparent) pixels.Data[i + 3] = 255;
                    if (swapRedBlue)
                    {
                        var red = pixels.Data[i];
                        pixels.Data[i] = pixels.Data[i + 2];
                        pixels.Data[i + 2] = red;
                    }
                }
            }
            cancellation.ThrowIfCancellationRequested();
            pixels.MatteBlackRgba = pixels.MatteWhiteRgba = null;
            pixels.NeedsCpuProcessing = false;
        }
    }
}
