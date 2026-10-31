using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    internal readonly record struct WheelTarget(IntPtr Window, uint Process, uint Thread);
    internal readonly record struct WheelPacket(WheelTarget Target, int Delta, ushort Keys, int X, int Y);

    /// <summary>UI-owned, bounded wheel batching. Never changes focus, window styles or injects input.</summary>
    internal sealed class MouseWheelForwarder : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly LinkedList<WheelPacket> _pending = new();
        private readonly Func<WheelPacket, bool> _post;
        private readonly Action _forwarded;
        private bool _disposed;
        private int _failures;

        internal MouseWheelForwarder(Dispatcher dispatcher, Action forwarded = null, Func<WheelPacket, bool> post = null)
        {
            dispatcher.VerifyAccess();
            _post = post ?? Post;
            _forwarded = forwarded;
            _timer = new DispatcherTimer(DispatcherPriority.Input, dispatcher) { Interval = TimeSpan.FromMilliseconds(8) };
            _timer.Tick += Tick;
        }

        internal bool Forward(int delta, ushort keys)
        {
            _timer.Dispatcher.VerifyAccess();
            if (_disposed || delta == 0 || !GetCursorPos(out var point)) return false;
            var tail = _pending.Last;
            var target = tail != null && tail.Value.X == point.X && tail.Value.Y == point.Y
                ? tail.Value.Target : FindTarget(point.X, point.Y, (uint)Environment.ProcessId);
            if (target.Window == IntPtr.Zero) return false;
            Enqueue(new WheelPacket(target, delta, keys, point.X, point.Y));
            return true;
        }

        internal void Enqueue(WheelPacket packet)
        {
            _timer.Dispatcher.VerifyAccess();
            if (_disposed || packet.Delta == 0 || packet.Target.Window == IntPtr.Zero) return;
            if (packet.Delta < short.MinValue || packet.Delta > short.MaxValue) throw new ArgumentOutOfRangeException(nameof(packet));
            var tail = _pending.Last;
            if (tail != null && tail.Value.Target == packet.Target && tail.Value.Keys == packet.Keys
                && Math.Sign(tail.Value.Delta) == Math.Sign(packet.Delta)
                && (long)tail.Value.Delta + packet.Delta is >= short.MinValue and <= short.MaxValue)
                tail.Value = packet with { Delta = tail.Value.Delta + packet.Delta };
            else
            {
                // Bound memory even while the pointer crosses many child windows.
                // Flush posts asynchronously, so an unresponsive recipient cannot block us.
                if (_pending.Count >= 64) Flush();
                _pending.AddLast(packet);
            }
            if (!_timer.IsEnabled) _timer.Start();
        }

        private void Tick(object sender, EventArgs e) => Flush();
        internal void Flush()
        {
            _timer.Dispatcher.VerifyAccess();
            _timer.Stop();
            bool sent = false;
            while (!_disposed && _pending.First != null)
            {
                var packet = _pending.First.Value; _pending.RemoveFirst();
                if (_post(packet)) sent = true;
                else if (++_failures == 1 || _failures % 100 == 0)
                    LogHelper.WriteLogToFile($"[MouseWheel] Post failed: {Marshal.GetLastWin32Error()}; count={_failures}", LogHelper.LogType.Warning);
            }
            if (sent) _forwarded?.Invoke();
        }

        internal static WheelTarget FindTarget(int x, int y, uint excludedProcess)
        {
            IntPtr window = GetTopWindow(IntPtr.Zero);
            for (int i = 0; window != IntPtr.Zero && i < 512; i++, window = GetWindow(window, 2))
            {
                uint thread = GetWindowThreadProcessId(window, out uint process);
                if (thread == 0 || process == excludedProcess || !IsWindowVisible(window) || !GetWindowRect(window, out var rect)
                    || x < rect.Left || x >= rect.Right || y < rect.Top || y >= rect.Bottom) continue;
                if ((GetWindowLong(window, -20) & 0x20) != 0) continue;
                if (DwmGetWindowAttribute(window, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0) continue;
                if (!IsWindowEnabled(window)) return default;
                IntPtr target = window;
                for (int depth = 0; depth < 32; depth++)
                {
                    var point = new Point { X = x, Y = y };
                    if (!ScreenToClient(target, ref point)) break;
                    IntPtr child = ChildWindowFromPointEx(target, point, 7);
                    if (child == IntPtr.Zero || child == target) break;
                    target = child;
                }
                thread = GetWindowThreadProcessId(target, out process);
                return thread == 0 || process == excludedProcess ? default : new WheelTarget(target, process, thread);
            }
            return default;
        }

        private static bool Post(WheelPacket packet)
        {
            uint thread = GetWindowThreadProcessId(packet.Target.Window, out uint process);
            if (thread != packet.Target.Thread || process != packet.Target.Process || process == Environment.ProcessId) return false;
            if (!IsWindowVisible(packet.Target.Window) || !IsWindowEnabled(GetAncestor(packet.Target.Window, 2))) return false;
            return PostMessage(packet.Target.Window, 0x020A, Pack(packet.Keys, packet.Delta), Pack(packet.X, packet.Y));
        }
        private static IntPtr Pack(int low, int high) => new(unchecked((int)((uint)(ushort)low | ((uint)(ushort)high << 16))));

        public void Dispose()
        {
            _timer.Dispatcher.VerifyAccess();
            _disposed = true; _timer.Stop(); _timer.Tick -= Tick; _pending.Clear();
        }

        [StructLayout(LayoutKind.Sequential)] private struct Point { internal int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct Rect { internal int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")] private static extern IntPtr GetTopWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr window);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr window, ref Point point);
        [DllImport("user32.dll")] private static extern IntPtr ChildWindowFromPointEx(IntPtr window, Point point, uint flags);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, uint attribute, out int value, int size);
    }
}
