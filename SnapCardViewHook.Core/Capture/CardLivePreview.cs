using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using IL2CppApi.Runtime;

namespace SnapCardViewHook.Core.Capture
{
    internal sealed class CardLivePreview : IDisposable
    {
        private readonly CardCaptureOptions _options;
        private readonly long _interval;
        private readonly Action _callback;
        private readonly ConcurrentQueue<CapturedCardPixels> _available = new ConcurrentQueue<CapturedCardPixels>();
        private static readonly CapturedCardPixels EmptyFrame = new CapturedCardPixels();
        private Il2CppRuntime _runtime;
        private UnityCardCapture _capture;
        private CardCaptureAnchor _anchor = new CardCaptureAnchor();
        private CapturedCardPixels _latest;
        private long _nextFrame, _refreshAt, _validateAt;
        private int _stopped;

        public CardLivePreview(CardCaptureOptions options, int fps)
        {
            _options = new CardCaptureOptions
            {
                Width = options.Width, Height = options.Height, PaddingPercent = options.PaddingPercent,
                IncludeShadow = options.IncludeShadow, TransparentBackground = true
            };
            _interval = Stopwatch.Frequency / Math.Max(1, Math.Min(60, fps));
            _callback = Update;
            var length = checked(options.Width * options.Height * 4);
            for (var i = 0; i < 2; i++)
                _available.Enqueue(new CapturedCardPixels
                {
                    Width = options.Width, Height = options.Height,
                    Rgba = new byte[length], MatteBlackRgba = new byte[length], MatteWhiteRgba = new byte[length]
                });
        }

        public void Start() => SnapTypeDataCollector.RegisterGameUiCallback(_callback);
        public CapturedCardPixels TakeFrame() => Interlocked.Exchange(ref _latest, null);

        // A frame stays with the display worker until conversion has finished.
        public void ReturnFrame(CapturedCardPixels frame)
        {
            if (frame == null || frame == EmptyFrame || Volatile.Read(ref _stopped) != 0) return;
            _available.Enqueue(frame);
            if (Volatile.Read(ref _stopped) != 0)
                while (_available.TryDequeue(out _)) { }
        }

        private void Update()
        {
            if (Volatile.Read(ref _stopped) != 0)
            {
                SnapTypeDataCollector.RemoveGameUiCallback(_callback);
                ResetCapture();
                Interlocked.Exchange(ref _latest, null);
                return;
            }
            var now = Stopwatch.GetTimestamp();
            if (now < _nextFrame) return;
            _nextFrame = now + _interval;
            using (var gate = CardCaptureGate.TryEnter())
            {
                CapturedCardPixels frame = null;
                try
                {
                    if (gate == null)
                    {
                        ResetCapture(keepFraming: true);
                        Clear();
                        return;
                    }
                    // Do no native work if the display still has an unconsumed frame.
                    if (Volatile.Read(ref _latest) != null) return;
                    if (_capture != null)
                    {
                        var checkReadiness = now >= _validateAt;
                        if (!_capture.IsSelectedCardCurrent(checkReadiness)) ResetCapture();
                        else if (now >= _refreshAt) ResetCapture(keepFraming: true);
                        if (checkReadiness) _validateAt = now + Stopwatch.Frequency / 4;
                    }
                    if (_capture == null)
                    {
                        _runtime = new Il2CppRuntime();
                        _capture = new UnityCardCapture(_runtime);
                        _capture.Prepare(_options, CancellationToken.None, _anchor);
                        // Occasional recycling bounds native roots and refreshes scene isolation lists.
                        _refreshAt = now + Stopwatch.Frequency * 30;
                        _validateAt = now + Stopwatch.Frequency / 4;
                    }
                    if (!_available.TryDequeue(out frame)) return;
                    _capture.Capture(CancellationToken.None, frame);
                    Publish(frame);
                    frame = null;
                }
                catch (Exception)
                {
                    ResetCapture();
                    Clear();
                    _nextFrame = now + Stopwatch.Frequency / 4;
                }
                finally { ReturnFrame(frame); }
            }
        }

        private void Clear() => Publish(EmptyFrame);

        private void Publish(CapturedCardPixels frame)
        {
            if (Volatile.Read(ref _stopped) != 0) return;
            ReturnFrame(Interlocked.Exchange(ref _latest, frame));
            if (Volatile.Read(ref _stopped) != 0) Interlocked.Exchange(ref _latest, null);
        }

        private void ResetCapture(bool keepFraming = false)
        {
            if (!keepFraming) _anchor = new CardCaptureAnchor();
            try { _capture?.Dispose(); }
            catch (Exception error) { Debug.WriteLine(error); }
            finally
            {
                _capture = null;
                try { _runtime?.Dispose(); }
                catch (Exception error) { Debug.WriteLine(error); }
                _runtime = null;
            }
        }

        // Native cleanup stays on the owning game thread, even when the window closes.
        public void Dispose()
        {
            Interlocked.Exchange(ref _stopped, 1);
            Interlocked.Exchange(ref _latest, null);
            while (_available.TryDequeue(out _)) { }
        }
    }
}
