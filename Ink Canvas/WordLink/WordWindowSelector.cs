using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Ink_Canvas.WordLink
{
    internal static class WordWindowSelector
    {
        internal static uint InputStamp()
        {
            var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
            // On failure retain periodic verification rather than suppressing it.
            return GetLastInputInfo(ref info) ? info.Time : unchecked((uint)Environment.TickCount64);
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct LastInputInfo { public uint Size, Time; }
        [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LastInputInfo info);

        internal static long GetTarget(long previous)
        {
            IntPtr window = GetAncestor(GetForegroundWindow(), 2);
            if (window == IntPtr.Zero) return 0;
            GetWindowThreadProcessId(window, out uint pid);
            if (pid == Environment.ProcessId) return IsWordWindow(new IntPtr(previous)) ? previous : 0;
            if (!IsWordWindow(window)) return 0;
            IntPtr old = new(previous);
            if (old != window && IsWordWindow(old))
            {
                GetWindowThreadProcessId(old, out uint oldPid);
                var name = new StringBuilder(64); GetClassName(window, name, name.Capacity);
                if (oldPid == pid && name.ToString() != "OpusApp")
                {
                    IntPtr owner = GetWindow(window, 4);
                    for (int depth = 0; owner != IntPtr.Zero && depth < 8; depth++)
                    {
                        if (GetAncestor(owner, 2) == old) return previous;
                        IntPtr next = GetWindow(owner, 4);
                        if (next == owner) break;
                        owner = next;
                    }
                }
            }
            return window.ToInt64();
        }

        internal static bool IsWordWindow(IntPtr window)
        {
            if (window == IntPtr.Zero || !IsWindow(window) || !IsWindowVisible(window) || IsIconic(window)) return false;
            GetWindowThreadProcessId(window, out uint pid);
            try
            {
                using var process = Process.GetProcessById((int)pid);
                return process.ProcessName.Equals("WINWORD", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
        }
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int capacity);
    }
}
