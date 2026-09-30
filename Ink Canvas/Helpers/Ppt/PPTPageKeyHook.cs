using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 在 PowerPoint 放映期间捕获 PageUp/PageDown 的低级键盘钩子。
    /// </summary>
    internal sealed class PPTPageKeyHook : IDisposable
    {
        private const int WhKeyboardLl = 13;
        private const int WmKeyDown = 0x0100;
        private const int WmKeyUp = 0x0101;
        private const int WmSysKeyDown = 0x0104;
        private const int WmSysKeyUp = 0x0105;
        private const uint VkPrior = 0x21;
        private const uint VkNext = 0x22;

        private readonly Dispatcher _dispatcher;
        private readonly Action _previousPageAction;
        private readonly Action _nextPageAction;
        private readonly LowLevelKeyboardProc _hookProc;
        private IntPtr _hookHandle;
        private bool _pageUpPressed;
        private bool _pageDownPressed;
        private bool _isDisposed;

        internal PPTPageKeyHook(Dispatcher dispatcher, Action previousPageAction, Action nextPageAction)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _previousPageAction = previousPageAction ?? throw new ArgumentNullException(nameof(previousPageAction));
            _nextPageAction = nextPageAction ?? throw new ArgumentNullException(nameof(nextPageAction));
            _hookProc = HookCallback;
        }

        internal bool IsInstalled => _hookHandle != IntPtr.Zero;

        internal bool Install()
        {
            if (_isDisposed || IsInstalled) return IsInstalled;

            IntPtr moduleHandle = GetModuleHandle(Process.GetCurrentProcess().MainModule?.ModuleName);
            _hookHandle = SetWindowsHookEx(WhKeyboardLl, _hookProc, moduleHandle, 0);
            if (_hookHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                LogHelper.WriteLogToFile(
                    $"安装 PPT PageUp/PageDown 低级键盘钩子失败: {new Win32Exception(error).Message} ({error})",
                    LogHelper.LogType.Error);
                return false;
            }

            LogHelper.WriteLogToFile("PPT PageUp/PageDown 低级键盘钩子已启用", LogHelper.LogType.Event);
            return true;
        }

        internal void Uninstall()
        {
            if (!IsInstalled)
            {
                ResetPressedState();
                return;
            }

            IntPtr hookHandle = _hookHandle;
            _hookHandle = IntPtr.Zero;
            ResetPressedState();

            if (!UnhookWindowsHookEx(hookHandle))
            {
                int error = Marshal.GetLastWin32Error();
                LogHelper.WriteLogToFile(
                    $"卸载 PPT PageUp/PageDown 低级键盘钩子失败: {new Win32Exception(error).Message} ({error})",
                    LogHelper.LogType.Warning);
                return;
            }

            LogHelper.WriteLogToFile("PPT PageUp/PageDown 低级键盘钩子已禁用", LogHelper.LogType.Event);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && IsInstalled)
            {
                uint virtualKey = unchecked((uint)Marshal.ReadInt32(lParam));
                bool isKeyDown = wParam == (IntPtr)WmKeyDown || wParam == (IntPtr)WmSysKeyDown;
                bool isKeyUp = wParam == (IntPtr)WmKeyUp || wParam == (IntPtr)WmSysKeyUp;

                if (virtualKey == VkPrior)
                {
                    HandlePageKey(isKeyDown, isKeyUp, ref _pageUpPressed, _previousPageAction);
                    if (isKeyDown || isKeyUp) return (IntPtr)1;
                }
                else if (virtualKey == VkNext)
                {
                    HandlePageKey(isKeyDown, isKeyUp, ref _pageDownPressed, _nextPageAction);
                    if (isKeyDown || isKeyUp) return (IntPtr)1;
                }
            }

            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private void HandlePageKey(bool isKeyDown, bool isKeyUp, ref bool isPressed, Action action)
        {
            if (isKeyDown)
            {
                if (isPressed) return;
                isPressed = true;
                _dispatcher.BeginInvoke(action, DispatcherPriority.Input);
            }
            else if (isKeyUp)
            {
                isPressed = false;
            }
        }

        private void ResetPressedState()
        {
            _pageUpPressed = false;
            _pageDownPressed = false;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            Uninstall();
            _isDisposed = true;
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(
            int idHook,
            LowLevelKeyboardProc lpfn,
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
