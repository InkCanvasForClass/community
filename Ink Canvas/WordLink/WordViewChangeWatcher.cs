using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Ink_Canvas.WordLink
{
    // Created on the WPF dispatcher: out-of-context WinEvents use its message loop.
    // Callbacks only schedule work; they never call Word or accessibility COM.
    internal sealed class WordViewChangeWatcher : IDisposable
    {
        private readonly Action _changed;
        private readonly Action<bool> _scrolled;
        private readonly WinEventProc _callback;
        private readonly List<IntPtr> _hooks = new();
        private IntPtr _window;
        private bool _scrollGesture;
        private int _eventFailures;
        internal WordViewChangeWatcher(Action changed, Action<bool> scrolled = null) { _changed = changed; _scrolled = scrolled; _callback = OnEvent; }
        internal void Observe(long window)
        {
            if (_window.ToInt64() == window) return;
            Dispose(); _window = new IntPtr(window);
            if (_window == IntPtr.Zero || GetWindowThreadProcessId(_window, out uint process) == 0) return;
            Hook(0x000A, 0x000B, process); // move/size start/end
            Hook(0x0012, 0x0013, process); // scroll start/end
            Hook(0x800B, 0x800B, process); // object location
            Hook(0x800E, 0x800E, process); // scrollbar/zoom value
            Hook(0x8015, 0x8015, process); // content scrolled
            Hook(0x8006, 0x8006, process); // selection changed in a facing-page view
        }
        private void Hook(uint first, uint last, uint process)
        {
            IntPtr hook = SetWinEventHook(first, last, IntPtr.Zero, _callback, process, 0, 2);
            if (hook != IntPtr.Zero) _hooks.Add(hook);
        }
        private void OnEvent(IntPtr hook, uint type, IntPtr window, int obj, int child, uint thread, uint time)
        {
            // Ignore caret motion and late callbacks from a previous registration.
            if (!_hooks.Contains(hook) || obj == -8 || _window == IntPtr.Zero) return;
            if (window == _window || GetAncestor(window, 2) == _window)
            {
                if (type == 0x0012) _scrollGesture = true;
                if (type == 0x0013) _scrollGesture = false;
                try
                {
                    bool directScroll = _scrollGesture || type is 0x0012 or 0x0013 or 0x8015;
                    bool documentMoved = type == 0x800B && obj == -4 && IsDocumentWindow(window);
                    if (_scrolled != null && (directScroll || documentMoved || type == 0x800E && obj is -5 or -6)) _scrolled(directScroll);
                    else _changed();
                }
                catch (Exception ex)
                {
                    if (++_eventFailures == 1 || _eventFailures % 100 == 0)
                        Helpers.LogHelper.WriteLogToFile($"[WordLink] View notification failed: {ex.GetType().Name}", Helpers.LogHelper.LogType.Warning);
                }
            }
        }
        public void Dispose()
        {
            foreach (IntPtr hook in _hooks) UnhookWinEvent(hook);
            _hooks.Clear(); _window = IntPtr.Zero;
            _scrollGesture = false;
        }
        private delegate void WinEventProc(IntPtr hook, uint type, IntPtr window, int obj, int child, uint thread, uint time);
        private static bool IsDocumentWindow(IntPtr window)
        {
            var name = new StringBuilder(32);
            return GetClassName(window, name, name.Capacity) > 0 && name.ToString() == "_WwG";
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
        [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module, WinEventProc callback, uint process, uint thread, uint flags);
        [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    }
}
