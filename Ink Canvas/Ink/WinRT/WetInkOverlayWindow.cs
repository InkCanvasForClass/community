using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vortice.DirectComposition;

namespace Ink_Canvas.Ink.WinRT
{
    /// <summary>
    /// Transparent overlay HWND that hosts the WinRT InkPresenter's DirectComposition
    /// visual. It is a child of the main window, stays WS_VISIBLE at all times, and is
    /// either parked far off-screen (no live wet ink) or positioned exactly over the
    /// main window. Unlike the retired custom renderer overlay, this window MUST receive
    /// pointer input so the InkPresenter's ink-enabled area gets the pen/touch/mouse.
    /// Hit-testing is dynamic: WM_NCHITTEST returns HTCLIENT over the canvas area and
    /// HTTRANSPARENT over UI chrome / foreign windows, so the app UI stays clickable.
    /// </summary>
    internal sealed class WetInkOverlayWindow : IDisposable
    {
        internal const string WindowClassName = "InkCanvasForClass.WinRTInkOverlay.v1";

        private const int WmNcHitTest = 0x0084;
        private const int WmNcCreate = 0x0081;
        private const int WmNcDestroy = 0x0082;
        private const int WmMouseActivate = 0x0021;
        private const int MaNoActivate = 3;
        private const int WmDestroy = 0x0002;
        private const int HtClient = 1;
        private const int HtTransparent = -1;
        private const int WsPopup = unchecked((int)0x80000000);
        private const int WsVisible = 0x10000000;
        private const int WsExNoActivate = 0x08000000;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoRedirectionBitmap = 0x00200000;
        private const int ErrorClassAlreadyExists = 1410;
        private const int HiddenPosition = -100000;

        private static readonly object ClassSync = new object();
        private static bool _classRegistered;
        private static WndProcDelegate _classWndProc;

        private readonly IntPtr _ownerHwnd;
        private readonly Func<int, int, bool> _hitTestCallback;
        private IntPtr _overlayHwnd;
        private GCHandle _gcHandle;
        private bool _shouldShowOnScreen;
        private int _boundsX = HiddenPosition;
        private int _boundsY = HiddenPosition;
        private int _boundsWidth = 1;
        private int _boundsHeight = 1;
        private bool _disposed;

        private IDCompositionDevice _compositionDevice;
        private IDCompositionDevice3 _compositionDevice3;
        private IDCompositionTarget _compositionTarget;
        private IDCompositionVisual _compositionVisual;

        /// <summary>
        /// UI-thread dispatcher captured at EnsureCreated time. Ink-thread callbacks use this
        /// to marshal dry-commit work back onto the UI thread (WPF is not thread-affine here).
        /// </summary>
        internal System.Windows.Threading.Dispatcher UiDispatcher { get; private set; }

        public WetInkOverlayWindow(IntPtr ownerHwnd, Func<int, int, bool> hitTestCallback)
        {
            if (ownerHwnd == IntPtr.Zero)
                throw new ArgumentOutOfRangeException(nameof(ownerHwnd));
            _ownerHwnd = ownerHwnd;
            _hitTestCallback = hitTestCallback ?? throw new ArgumentNullException(nameof(hitTestCallback));
        }

        public IntPtr OverlayHandle => _overlayHwnd;

        public IDCompositionVisual RootVisual => _compositionVisual;

        public IDCompositionDevice CompositionDevice => _compositionDevice;

        public IDCompositionDevice3 CompositionDevice3 => _compositionDevice3;

        internal void InvokeOnUiThread(Action action)
        {
            if (action == null || UiDispatcher == null)
                return;
            UiDispatcher.BeginInvoke(action);
        }

        /// <summary>
        /// Creates the HWND and the DirectComposition tree rooted at it. Must run on the
        /// UI thread (same thread that creates windows for this app).
        /// </summary>
        public void EnsureCreated()
        {
            if (_overlayHwnd != IntPtr.Zero)
                return;
            UiDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            EnsureWindowClassRegistered();

            var gcHandle = GCHandle.Alloc(this);
            _gcHandle = gcHandle;
            _overlayHwnd = CreateWindowEx(
                WsExNoActivate | WsExToolWindow | WsExNoRedirectionBitmap,
                WindowClassName,
                "ICC WinRT Ink Overlay",
                WsPopup | WsVisible,
                HiddenPosition,
                HiddenPosition,
                Math.Max(1, _boundsWidth),
                Math.Max(1, _boundsHeight),
                _ownerHwnd,
                IntPtr.Zero,
                GetModuleHandle(null),
                GCHandle.ToIntPtr(gcHandle));

            if (_overlayHwnd == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "CreateWindowEx failed for WinRT ink overlay.");

            CreateCompositionTree();
            PlaceOverlay();
        }

        public void SetBounds(int x, int y, int width, int height)
        {
            _boundsX = x;
            _boundsY = y;
            _boundsWidth = Math.Max(1, width);
            _boundsHeight = Math.Max(1, height);
            if (_overlayHwnd != IntPtr.Zero)
                PlaceOverlay();
        }

        /// <summary>
        /// Parks the overlay off-screen when no wet ink is live. Avoids ShowWindow/HideWindow
        /// (DWM composition re-layout flashes on wet→dry handoff).
        /// </summary>
        public void SetOnScreen(bool visible)
        {
            _shouldShowOnScreen = visible;
            if (_overlayHwnd != IntPtr.Zero)
                PlaceOverlay();
        }

        private void CreateCompositionTree()
        {
            // Custom drying requires the DComp 3 device to be supplied to
            // IInkPresenterDesktop.SetRootVisual and committed by IInkCommitRequestHandler.
            _compositionDevice = DComp.DCompositionCreateDevice3<IDCompositionDevice>(null);
            _compositionDevice3 = _compositionDevice.QueryInterface<IDCompositionDevice3>();
            _compositionDevice.CreateTargetForHwnd(_overlayHwnd, true, out _compositionTarget)
                .CheckError();
            _compositionDevice.CreateVisual(out _compositionVisual).CheckError();
            _compositionTarget.SetRoot(_compositionVisual).CheckError();
            _compositionDevice.Commit().CheckError();
        }

        private void PlaceOverlay()
        {
            if (_overlayHwnd == IntPtr.Zero)
                return;

            var x = _shouldShowOnScreen ? _boundsX : HiddenPosition;
            var y = _shouldShowOnScreen ? _boundsY : HiddenPosition;

            SetWindowPos(
                _overlayHwnd,
                IntPtr.Zero,
                x,
                y,
                Math.Max(1, _boundsWidth),
                Math.Max(1, _boundsHeight),
                0x0010 /* SWP_NOACTIVATE */ | 0x0004 /* SWP_NOZORDER */ | 0x0040 /* SWP_SHOWWINDOW */);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            var hwndForEntry = _overlayHwnd;

            if (_compositionVisual != null)
            {
                try { _compositionVisual.Dispose(); } catch { }
                _compositionVisual = null;
            }
            if (_compositionTarget != null)
            {
                try { _compositionTarget.Dispose(); } catch { }
                _compositionTarget = null;
            }
            if (_compositionDevice3 != null)
            {
                try { _compositionDevice3.Dispose(); } catch { }
                _compositionDevice3 = null;
            }
            if (_compositionDevice != null)
            {
                try { _compositionDevice.Dispose(); } catch { }
                _compositionDevice = null;
            }

            if (_overlayHwnd != IntPtr.Zero)
            {
                var hwnd = _overlayHwnd;
                _overlayHwnd = IntPtr.Zero;
                DestroyWindow(hwnd);
            }

            if (_gcHandle.IsAllocated)
            {
                _gcHandle.Free();
                _gcHandle = default;
            }
            WindowsByHwnd.Remove(hwndForEntry);
        }

        private static void EnsureWindowClassRegistered()
        {
            lock (ClassSync)
            {
                if (_classRegistered)
                    return;

                _classWndProc = OverlayWndProc;
                var wndClass = new WndClassEx
                {
                    cbSize = (uint)Marshal.SizeOf<WndClassEx>(),
                    style = 0,
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_classWndProc),
                    cbClsExtra = 0,
                    cbWndExtra = 0,
                    hInstance = GetModuleHandle(null),
                    hIcon = IntPtr.Zero,
                    hCursor = IntPtr.Zero,
                    hbrBackground = IntPtr.Zero,
                    lpszMenuName = null,
                    lpszClassName = WindowClassName,
                    hIconSm = IntPtr.Zero
                };

                var atom = RegisterClassEx(ref wndClass);
                if (atom == 0)
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error != ErrorClassAlreadyExists)
                        throw new System.ComponentModel.Win32Exception(
                            error,
                            "RegisterClassEx failed for WinRT ink overlay.");
                }

                _classRegistered = true;
            }
        }

        private static IntPtr OverlayWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WmNcHitTest)
            {
                // lParam is screen coordinates packed: LOWORD = x, HIWORD = y.
                var x = (short)((uint)lParam & 0xFFFF);
                var y = (short)((uint)lParam >> 16);
                var window = GetWindowInstance(hwnd);
                if (window?._hitTestCallback != null && window._hitTestCallback(x, y))
                    return new IntPtr(HtClient);
                return new IntPtr(HtTransparent);
            }
            if (msg == WmNcCreate)
            {
                // GWLP_USERDATA does not support a per-instance value in a static class
                // window proc. Store the managed instance on the class and look it up by
                // HWND so WM_NCHITTEST can reach the per-window hit-test callback.
                var createStruct = (CREATESTRUCT)Marshal.PtrToStructure(lParam, typeof(CREATESTRUCT));
                if (createStruct.lpCreateParams != IntPtr.Zero)
                {
                    var handle = GCHandle.FromIntPtr(createStruct.lpCreateParams);
                    WindowsByHwnd[hwnd] = handle.Target as WetInkOverlayWindow;
                }
                return new IntPtr(1);
            }
            if (msg == WmNcDestroy)
            {
                WindowsByHwnd.Remove(hwnd);
                return IntPtr.Zero;
            }
            if (msg == WmMouseActivate)
            {
                // WS_EX_NOACTIVATE already prevents activation; returning MA_NOACTIVATE
                // additionally stops the first click from being eaten by activation.
                return new IntPtr(MaNoActivate);
            }
            if (msg == WmDestroy)
                return IntPtr.Zero;
            return DefWindowProc(hwnd, msg, wParam, lParam);
        }

        private static WetInkOverlayWindow GetWindowInstance(IntPtr hwnd)
        {
            return WindowsByHwnd.TryGetValue(hwnd, out var window) ? window : null;
        }

        private static readonly Dictionary<IntPtr, WetInkOverlayWindow> WindowsByHwnd =
            new Dictionary<IntPtr, WetInkOverlayWindow>();

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREATESTRUCT
        {
            public IntPtr lpCreateParams;
            public IntPtr hInstance;
            public IntPtr hMenu;
            public IntPtr hwndParent;
            public int cy;
            public int cx;
            public int y;
            public int x;
            public int style;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszClass;
            public uint dwExStyle;
        }

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WndClassEx
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ushort RegisterClassEx(ref WndClassEx lpwcx);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);
    }
}
