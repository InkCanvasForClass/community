using System;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    /// <summary>At most one posted callback and one timer for a burst of UI updates.</summary>
    internal sealed class DispatcherCoalescedAction : IDisposable
    {
        private readonly object _sync = new();
        private readonly Dispatcher _dispatcher;
        private readonly Action _action;
        private readonly DispatcherTimer _timer;
        private bool _dirty;
        private bool _posted;
        private bool _disposed;

        internal DispatcherCoalescedAction(Dispatcher dispatcher, TimeSpan interval, Action action)
        {
            if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));
            if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
            dispatcher.VerifyAccess();
            _dispatcher = dispatcher;
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = interval };
            _timer.Tick += OnTick;
        }

        internal void Request()
        {
            lock (_sync)
            {
                if (_disposed || _dispatcher.HasShutdownStarted) return;
                _dirty = true;
                if (_posted) return;
                _posted = true;
            }
            _ = _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                lock (_sync)
                {
                    if (_disposed || !_dirty) return;
                }
                // Do not restart an enabled timer: sustained input must still make progress.
                if (!_timer.IsEnabled) _timer.Start();
            }));
        }

        private void OnTick(object sender, EventArgs args) => Flush();

        internal void Flush()
        {
            _dispatcher.VerifyAccess();
            _timer.Stop();
            lock (_sync)
            {
                _posted = false;
                if (_disposed || !_dirty) return;
                _dirty = false;
            }
            try { _action(); }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[UI coalesced update] {_action.Method.Name}: {ex}", LogHelper.LogType.Warning);
            }
        }

        public void Dispose()
        {
            _dispatcher.VerifyAccess();
            lock (_sync) { _disposed = true; _dirty = false; }
            _timer.Stop();
            _timer.Tick -= OnTick;
        }
    }
}
