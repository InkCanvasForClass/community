using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 托盘菜单打开期间的低级鼠标钩子：点击菜单以外的任意位置时回调，用于收起菜单。
    /// <para>
    /// WPF 的 ContextMenu 靠鼠标捕获感知"点到了外面"，但本应用常处于无焦点 / 置顶模式，
    /// 弹窗拿不到捕获，点桌面或别的程序菜单不会消失（只有点回自己的窗口才行）。
    /// 低级鼠标钩子是全局的，不依赖谁在前台，因此能可靠地识别外部点击。
    /// </para>
    /// <para>
    /// 钩子回调运行在安装它的线程（UI 线程）上，回调里只做派发，不直接操作 UI 状态。
    /// </para>
    /// </summary>
    internal sealed class TrayMenuDismissHook : IDisposable
    {
        private const int WH_MOUSE_LL = 14;

        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;

        // 安装瞬间仍在路上的那一下点击（打开菜单的托盘点击）不应立刻把菜单关掉
        private const int SuppressWindowMs = 250;

        private readonly Dispatcher _dispatcher;
        private readonly Action _onOutsideMouseDown;
        private readonly LowLevelMouseProc _hookProc;

        private IntPtr _hookHandle = IntPtr.Zero;
        private Rect _menuScreenRect = Rect.Empty;
        private int _suppressUntilTick;
        private bool _isDisposed;

        internal TrayMenuDismissHook(Dispatcher dispatcher, Action onOutsideMouseDown)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _onOutsideMouseDown = onOutsideMouseDown ?? throw new ArgumentNullException(nameof(onOutsideMouseDown));
            _hookProc = HookCallback;
        }

        internal bool IsInstalled => _hookHandle != IntPtr.Zero;

        /// <summary>更新菜单在屏幕上的矩形（物理像素）。传 Rect.Empty 时视为不可用，不做外部判定。</summary>
        internal void UpdateMenuRect(Rect screenRect)
        {
            _menuScreenRect = screenRect;
        }

        internal bool Install()
        {
            if (_isDisposed || IsInstalled) return IsInstalled;

            IntPtr moduleHandle = GetModuleHandle(null);
            _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _hookProc, moduleHandle, 0);
            if (_hookHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                LogHelper.WriteLogToFile(
                    $"安装托盘菜单鼠标钩子失败: {new Win32Exception(error).Message} ({error})",
                    LogHelper.LogType.Warning);
                return false;
            }

            _suppressUntilTick = Environment.TickCount + SuppressWindowMs;
            return true;
        }

        internal void Uninstall()
        {
            if (!IsInstalled) return;

            IntPtr handle = _hookHandle;
            _hookHandle = IntPtr.Zero;
            _menuScreenRect = Rect.Empty;

            if (!UnhookWindowsHookEx(handle))
            {
                int error = Marshal.GetLastWin32Error();
                LogHelper.WriteLogToFile(
                    $"卸载托盘菜单鼠标钩子失败: {new Win32Exception(error).Message} ({error})",
                    LogHelper.LogType.Warning);
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            Uninstall();
            _isDisposed = true;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = wParam.ToInt32();
                if ((message == WM_LBUTTONDOWN || message == WM_RBUTTONDOWN || message == WM_MBUTTONDOWN) &&
                    unchecked(Environment.TickCount - _suppressUntilTick) >= 0)
                {
                    // 菜单矩形未知时不做外部判定——误判会让菜单没法点。
                    // 点在外面就派发一次收起；点在菜单内（含菜单项）不处理。
                    if (_menuScreenRect != Rect.Empty && !_menuScreenRect.IsEmpty)
                    {
                        var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                        if (!_menuScreenRect.Contains(info.pt.X, info.pt.Y))
                        {
                            _dispatcher.BeginInvoke(_onOutsideMouseDown, DispatcherPriority.Input);
                        }
                    }
                }
            }

            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(
            int idHook,
            LowLevelMouseProc lpfn,
            IntPtr hMod,
            uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
