using System;
using System.Threading;

namespace SnapCardViewHook.Core.Capture
{
    internal sealed class CardCaptureGate : IDisposable
    {
        private static int _busy;
        private int _released;

        private CardCaptureGate() { }

        public static CardCaptureGate Enter()
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                throw new InvalidOperationException("Another image capture or video recording is already in progress.");
            return new CardCaptureGate();
        }

        public static CardCaptureGate TryEnter() =>
            Interlocked.CompareExchange(ref _busy, 1, 0) == 0 ? new CardCaptureGate() : null;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) Volatile.Write(ref _busy, 0);
        }
    }
}
