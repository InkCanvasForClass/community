using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace Ink_Canvas.Ink.WinRT
{
    /// <summary>Bounded ink-thread to UI bridge; normal input retains its full eraser path.</summary>
    internal sealed class WinRTInkPalmDispatcher
    {
        private readonly object _sync = new();
        private readonly Queue<WinRTInkPalmEvent> _pending = new();
        private readonly Dispatcher _dispatcher;
        private readonly Action<WinRTInkPalmEvent> _apply;
        private bool _scheduled;

        internal WinRTInkPalmDispatcher(Dispatcher dispatcher, Action<WinRTInkPalmEvent> apply)
        {
            _dispatcher = dispatcher;
            _apply = apply;
        }

        internal void Post(WinRTInkPalmEvent input)
        {
            lock (_sync)
            {
                if (_pending.Count >= 256)
                {
                    // Overload must terminate the old transaction. A subsequent move
                    // can start a fresh eraser transaction without enabling inking.
                    _pending.Clear();
                    _pending.Enqueue(new(0, WinRTInkPalmPhase.End, default, 0, 0));
                }
                _pending.Enqueue(input);
                if (_scheduled) return;
                _scheduled = true;
            }
            _ = _dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(Drain));
        }

        private void Drain()
        {
            WinRTInkPalmEvent[] batch;
            lock (_sync)
            {
                batch = _pending.ToArray();
                _pending.Clear();
                _scheduled = false;
            }
            foreach (var input in batch)
            {
                try { _apply(input); }
                catch (Exception ex)
                {
                    Helpers.LogHelper.WriteLogToFile($"[WinRTInk] Palm dispatch failed: {ex}", Helpers.LogHelper.LogType.Warning);
                    try { _apply(new(0, WinRTInkPalmPhase.End, default, 0, 0)); }
                    catch (Exception cleanup) { Helpers.LogHelper.WriteLogToFile(cleanup.ToString(), Helpers.LogHelper.LogType.Warning); }
                }
            }
        }
    }
}
