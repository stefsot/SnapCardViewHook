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

        public static void PrepareVideoPixels(CapturedCardPixels pixels, bool transparent, CancellationToken cancellation)
        {
            if (!transparent)
            {
                PreparePixels(pixels, false, cancellation);
                return;
            }
            CaptureAlphaReconstruction.ApplyPremultipliedVideo(pixels.Rgba, pixels.MatteBlackRgba,
                pixels.MatteWhiteRgba, pixels.LinearColorSpace, cancellation);
            pixels.MatteBlackRgba = null;
            pixels.MatteWhiteRgba = null;
        }

        public static void PreparePixels(CapturedCardPixels pixels, bool transparent, CancellationToken cancellation)
        {
            if (transparent)
            {
                CaptureAlphaReconstruction.Apply(pixels.Rgba, pixels.MatteBlackRgba,
                    pixels.MatteWhiteRgba, pixels.LinearColorSpace, cancellation);
                pixels.MatteBlackRgba = null;
                pixels.MatteWhiteRgba = null;
            }
            else
            {
                for (var i = 3; i < pixels.Rgba.Length; i += 4)
                {
                    if ((i & 16383) == 3) cancellation.ThrowIfCancellationRequested();
                    pixels.Rgba[i] = 255;
                }
            }
        }
    }
}
