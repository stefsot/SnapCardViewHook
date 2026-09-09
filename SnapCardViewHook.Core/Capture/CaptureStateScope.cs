using System;
using System.Collections.Generic;

namespace SnapCardViewHook.Core.Capture
{
    internal sealed class CaptureStateScope : IDisposable
    {
        private readonly Stack<Action> _restore = new Stack<Action>();
        public void RestoreWith(Action restore) => _restore.Push(restore);
        public void Dispose()
        {
            List<Exception> errors = null;
            while (_restore.Count != 0)
            {
                try { _restore.Pop()(); }
                catch (Exception e) { (errors ??= new List<Exception>()).Add(e); }
            }
            if (errors != null)
                throw new AggregateException("Capture cleanup did not fully succeed. Restart the game before another capture.", errors);
        }
    }
}
