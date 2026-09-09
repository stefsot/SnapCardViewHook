using System;
using System.Threading;
using System.Threading.Tasks;

namespace SnapCardViewHook.Core.Capture
{
    internal static class CardImageCapture
    {
        public static async Task<CapturedCardPixels> CaptureAsync(CardCaptureOptions options, CancellationToken cancellation)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var snapshot = options.Snapshot();
            
            using (CardCaptureGate.Enter())
            {
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, timeout.Token))
                {
                    var token = linked.Token;
                    try
                    {
                        var pixels = await CardCaptureDispatcher.CaptureAsync(snapshot, null, token).ConfigureAwait(false);
                        await Task.Run(() =>
                        {
                            CardCaptureDispatcher.PreparePixels(pixels, snapshot.TransparentBackground, token);
                            CapturePngWriter.Write(pixels, snapshot, token);
                        }, token).ConfigureAwait(false);
                        return pixels;
                    }
                    catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellation.IsCancellationRequested)
                    {
                        throw new TimeoutException("Capture timed out. Keep the game running with the card-details view open, then try again.");
                    }
                }
            }
        }
    }
}
