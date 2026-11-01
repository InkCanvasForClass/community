using System.Runtime.InteropServices;
using System.Text;
using InkCanvasWordLink;

namespace InkCanvasWordLinkHost;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect
    {
        public int Left, Top, Right, Bottom;
        internal WordRect ToDto() => new() { X = Left, Y = Top, Width = Right - Left, Height = Bottom - Top };
    }
    internal delegate bool EnumProc(IntPtr hwnd, IntPtr parameter);
    internal static IntPtr FindDocument(IntPtr root)
    {
        IntPtr best = IntPtr.Zero;
        long area = 0;
        EnumChildWindows(root, (hwnd, _) =>
        {
            var name = new StringBuilder(128); GetClassName(hwnd, name, name.Capacity);
            if (name.ToString() == "_WwG" && IsWindowVisible(hwnd) && GetWindowRect(hwnd, out var rect))
            {
                long candidateArea = (long)(rect.Right - rect.Left) * (rect.Bottom - rect.Top);
                if (candidateArea > area) { area = candidateArea; best = hwnd; }
            }
            return true;
        }, IntPtr.Zero);
        return best;
    }
    [DllImport("oleacc.dll")] internal static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint id, ref Guid iid, [MarshalAs(UnmanagedType.IDispatch)] out object value);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr hwnd, EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int capacity);
}
