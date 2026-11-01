using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    // Diagnostic only: never restart the renderer or discard ink to recover a slow UI.
    // A single outstanding Input-priority probe measures both blocking and queue starvation.
    internal sealed class UiResponsivenessMonitor : IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private readonly Action<string> _report;
        private readonly Timer _timer;
        private readonly double _thresholdMs;
        private long _probeStarted;
        private int _pending, _reported, _disposed;
        private string _operation = "idle/input-dispatch";

        internal UiResponsivenessMonitor(Dispatcher dispatcher, Action<string> report,
            int intervalMs = 250, int thresholdMs = 750)
        {
            _dispatcher = dispatcher;
            _report = report;
            _thresholdMs = thresholdMs;
            _timer = new Timer(Check, null, intervalMs, intervalMs);
        }

        internal IDisposable EnterOperation(string name) => new Operation(this, name);

        private void Check(object state)
        {
            if (Volatile.Read(ref _disposed) != 0 || _dispatcher.HasShutdownStarted) return;
            if (Interlocked.CompareExchange(ref _pending, 1, 0) == 0)
            {
                Interlocked.Exchange(ref _probeStarted, Stopwatch.GetTimestamp());
                Interlocked.Exchange(ref _reported, 0);
                try { _dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(Acknowledge)); }
                catch (InvalidOperationException) { Interlocked.Exchange(ref _pending, 0); }
                return;
            }
            double elapsed = Stopwatch.GetElapsedTime(Interlocked.Read(ref _probeStarted)).TotalMilliseconds;
            if (elapsed >= _thresholdMs && Interlocked.CompareExchange(ref _reported, 1, 0) == 0)
                Report($"[UIStall] pending={elapsed:F0}ms operation={Volatile.Read(ref _operation)} GC2={GC.CollectionCount(2)} heapMB={GC.GetTotalMemory(false) / 1048576}");
        }

        private void Acknowledge()
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                double elapsed = Stopwatch.GetElapsedTime(Interlocked.Read(ref _probeStarted)).TotalMilliseconds;
                if (elapsed >= _thresholdMs || Volatile.Read(ref _reported) != 0)
                    Report($"[UIStall] recovered={elapsed:F0}ms operation={Volatile.Read(ref _operation)}");
            }
            Interlocked.Exchange(ref _pending, 0);
        }

        private void Report(string message)
        {
            try { _report(message); }
            catch (Exception ex) { Debug.WriteLine(ex); }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) _timer.Dispose();
        }

        private sealed class Operation : IDisposable
        {
            private readonly UiResponsivenessMonitor _owner;
            private readonly string _name, _previous;
            private readonly long _started = Stopwatch.GetTimestamp();
            private bool _disposed;
            internal Operation(UiResponsivenessMonitor owner, string name)
            {
                owner._dispatcher.VerifyAccess();
                _owner = owner; _name = name;
                _previous = Interlocked.Exchange(ref owner._operation, name);
            }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                double elapsed = Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
                if (elapsed >= 250) _owner.Report($"[UIWork] operation={_name} elapsed={elapsed:F0}ms");
                Volatile.Write(ref _owner._operation, _previous);
            }
        }
    }
}
