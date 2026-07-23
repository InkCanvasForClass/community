using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Ink_Canvas.Helpers;

namespace Ink_Canvas.Ink.Native
{
    internal sealed class WetInkWindowHost : IDisposable
    {
        private const string WindowClassName = "InkCanvasForClass.WetInkOverlay.v1";
        private const int WmNcHitTest = 0x0084;
        private const int WmDestroy = 0x0002;
        private const int HtTransparent = -1;
        private const int WsPopup = unchecked((int)0x80000000);
        private const int WsVisible = 0x10000000;
        private const int WsExNoActivate = 0x08000000;
        private const int WsExTransparent = 0x00000020;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoRedirectionBitmap = 0x00200000;
        private const int GwlpWndProc = -4;
        private const int ErrorClassAlreadyExists = 1410;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpShowWindow = 0x0040;
        private const uint SwpHideWindow = 0x0080;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoSize = 0x0001;
        private static readonly IntPtr HwndTop = new IntPtr(0);

        private static readonly object ClassSync = new object();
        private static bool _classRegistered;
        private static WndProcDelegate _classWndProc;

        private readonly WetInkCommandMailbox _mailbox;
        private readonly Action<WetInkRetirementAck> _onRetired;
        private readonly Action<Exception> _onFatalError;
        private readonly Action _onDeviceLost;
        private readonly AutoResetEvent _workEvent = new AutoResetEvent(false);
        private readonly ManualResetEventSlim _threadReady = new ManualResetEventSlim(false);
        private readonly object _targetSync = new object();

        private IntPtr _ownerHwnd;
        private IntPtr _overlayHwnd;
        private Thread _renderThread;
        private WetInkTargetSnapshot _pendingTarget;
        private bool _targetDirty;
        private bool _shutdownRequested;
        private bool _disposed;
        private bool _overlayShouldBeVisible;
        private Exception _threadStartError;
        private IWetInkBatchRenderer _renderer;

        public WetInkWindowHost(
            IntPtr ownerHwnd,
            WetInkCommandMailbox mailbox,
            Action<WetInkRetirementAck> onRetired,
            Action onDeviceLost = null,
            Action<Exception> onFatalError = null)
        {
            if (ownerHwnd == IntPtr.Zero)
                throw new ArgumentOutOfRangeException(nameof(ownerHwnd));
            _ownerHwnd = ownerHwnd;
            _mailbox = mailbox ?? throw new ArgumentNullException(nameof(mailbox));
            _onRetired = onRetired ?? throw new ArgumentNullException(nameof(onRetired));
            _onDeviceLost = onDeviceLost;
            _onFatalError = onFatalError;
        }

        public IntPtr OverlayHandle => _overlayHwnd;

        public bool IsRunning =>
            _renderThread != null
            && _renderThread.IsAlive
            && _overlayHwnd != IntPtr.Zero
            && !_shutdownRequested;

        public void Start(WetInkTargetSnapshot initialTarget)
        {
            EnsureNotDisposed();
            if (initialTarget == null)
                throw new ArgumentNullException(nameof(initialTarget));
            if (_renderThread != null)
                throw new InvalidOperationException("The wet ink window host is already started.");

            EnsureWindowClassRegistered();
            CreateOverlayWindow(initialTarget);
            lock (_targetSync)
            {
                _pendingTarget = initialTarget;
                _targetDirty = true;
            }

            _shutdownRequested = false;
            _threadReady.Reset();
            _renderThread = new Thread(RenderThreadMain)
            {
                IsBackground = true,
                Name = "ICC-WetInk-DComp"
            };
            _renderThread.SetApartmentState(ApartmentState.MTA);
            _renderThread.Start();

            if (!_threadReady.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("The wet ink render thread failed to start.");
            if (_threadStartError != null)
                throw new InvalidOperationException(
                    "The wet ink renderer failed to initialize.",
                    _threadStartError);
        }

        public void UpdateTarget(WetInkTargetSnapshot target)
        {
            EnsureNotDisposed();
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            lock (_targetSync)
            {
                _pendingTarget = target;
                _targetDirty = true;
            }

            RepositionOverlay(target);
            SignalWork();
        }

        /// <summary>
        /// Shows the overlay when there is wet ink to render and hides it when
        /// there is none. A visible-but-empty overlay would otherwise sit on top
        /// of the main window's own content (floating bar, toolbar) and steal
        /// activation / interfere with hit-testing even though mouse messages
        /// pass through it.
        /// </summary>
        public void SetOverlayVisible(bool visible)
        {
            _overlayShouldBeVisible = visible;
            if (_disposed || _overlayHwnd == IntPtr.Zero || _shutdownRequested)
                return;

            ApplyOverlayVisibility();
        }

        private void ApplyOverlayVisibility()
        {
            var flags = SwpNoActivate | SwpNoZOrder | SwpNoMove | SwpNoSize;
            if (_overlayShouldBeVisible)
                flags |= SwpShowWindow;
            else
                flags |= SwpHideWindow;

            NativeWindowHelper.SetWindowPos(
                _overlayHwnd,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                flags);
        }

        public void SignalWork()
        {
            if (_disposed || _shutdownRequested)
                return;
            _workEvent.Set();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _shutdownRequested = true;

            try
            {
                _mailbox.EnqueueBoundary(new WetInkBoundaryCommand(
                    WetInkBoundaryCommandKind.Shutdown,
                    0));
            }
            catch
            {
                // best-effort shutdown signal
            }

            _workEvent.Set();
            if (_renderThread != null && _renderThread.IsAlive)
                _renderThread.Join(TimeSpan.FromSeconds(3));

            DestroyOverlayWindow();
            _workEvent.Dispose();
            _threadReady.Dispose();
        }

        private void RenderThreadMain()
        {
            try
            {
                _renderer = new DirectCompositionInkRenderer();
                WetInkTargetSnapshot target;
                lock (_targetSync)
                {
                    target = _pendingTarget;
                    _targetDirty = false;
                }

                _renderer.BindTarget(_overlayHwnd, target);
                _threadStartError = null;
            }
            catch (Exception ex)
            {
                _threadStartError = ex;
                _threadReady.Set();
                RaiseFatal(ex);
                return;
            }

            _threadReady.Set();

            try
            {
                while (!_shutdownRequested)
                {
                    // Short wait so the thread wakes quickly after SignalWork and
                    // presents the first wet-ink point with minimal pen-down latency.
                    _workEvent.WaitOne(1);
                    PumpOnce();
                }

                PumpOnce();
            }
            catch (Exception ex)
            {
                RaiseFatal(ex);
            }
            finally
            {
                if (_renderer != null)
                {
                    try { _renderer.Dispose(); }
                    catch { /* best-effort */ }
                    _renderer = null;
                }
            }
        }

        private void PumpOnce()
        {
            if (_renderer == null)
                return;

            WetInkTargetSnapshot target = null;
            var targetDirty = false;
            lock (_targetSync)
            {
                if (_targetDirty)
                {
                    target = _pendingTarget;
                    _targetDirty = false;
                    targetDirty = true;
                }
            }

            if (targetDirty && target != null)
            {
                try
                {
                    _renderer.UpdateTarget(target);
                }
                catch (Exception ex)
                {
                    HandleApplyFailure(WetInkApplyResult.Failed(ex));
                    return;
                }
            }

            WetInkMailboxBatch batch;
            try
            {
                batch = _mailbox.Drain();
            }
            catch (Exception ex)
            {
                RaiseFatal(ex);
                return;
            }

            if ((batch == null
                    || (batch.OrderedItems.Count == 0
                        && batch.BoundaryCommands.Count == 0
                        && batch.RenderSnapshots.Count == 0))
                && !targetDirty)
            {
                return;
            }

            WetInkApplyResult result;
            try
            {
                result = _renderer.Apply(batch);
            }
            catch (Exception ex)
            {
                result = WetInkApplyResult.Failed(ex);
            }

            HandleApplyResult(result);
        }

        private void HandleApplyResult(WetInkApplyResult result)
        {
            if (result == null)
                return;

            if (result.Status == WetInkApplyStatus.DeviceLost
                || result.Status == WetInkApplyStatus.Failed)
            {
                HandleApplyFailure(result);
                return;
            }

            if (result.RetirementAcks == null)
                return;

            for (var i = 0; i < result.RetirementAcks.Count; i++)
            {
                var ack = result.RetirementAcks[i];
                try { _onRetired(ack); }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile(
                        $"[WetInk] Retirement ack callback failed: {ex}",
                        LogHelper.LogType.Error);
                }
            }
        }

        private void HandleApplyFailure(WetInkApplyResult result)
        {
            if (result.Status == WetInkApplyStatus.DeviceLost)
            {
                try { _onDeviceLost?.Invoke(); }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile(
                        $"[WetInk] Device-lost callback failed: {ex}",
                        LogHelper.LogType.Error);
                }
            }

            if (result.Error != null)
            {
                LogHelper.WriteLogToFile(
                    $"[WetInk] Renderer apply failed ({result.Status}): {result.Error}",
                    LogHelper.LogType.Error);
            }
        }

        private void RaiseFatal(Exception ex)
        {
            LogHelper.WriteLogToFile(
                $"[WetInk] Render thread fatal error: {ex}",
                LogHelper.LogType.Error);
            try { _onFatalError?.Invoke(ex); }
            catch
            {
                // never throw from the render thread callback path
            }
        }

        private void CreateOverlayWindow(WetInkTargetSnapshot target)
        {
            var bounds = target.ScreenBounds;
            var style = WsPopup | (target.IsVisible && _overlayShouldBeVisible ? WsVisible : 0);
            var exStyle = WsExNoActivate
                | WsExTransparent
                | WsExToolWindow
                | WsExNoRedirectionBitmap;

            _overlayHwnd = CreateWindowEx(
                exStyle,
                WindowClassName,
                "ICC Wet Ink Overlay",
                style,
                bounds.X,
                bounds.Y,
                Math.Max(1, bounds.Width),
                Math.Max(1, bounds.Height),
                _ownerHwnd,
                IntPtr.Zero,
                GetModuleHandle(null),
                IntPtr.Zero);

            if (_overlayHwnd == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateWindowEx failed for wet ink overlay.");

            // Owned popups stay above their owner; only force visibility/position.
            NativeWindowHelper.SetWindowPos(
                _overlayHwnd,
                HwndTop,
                bounds.X,
                bounds.Y,
                Math.Max(1, bounds.Width),
                Math.Max(1, bounds.Height),
                SwpNoActivate | SwpShowWindow | SwpNoZOrder);
        }

        private void RepositionOverlay(WetInkTargetSnapshot target)
        {
            if (_overlayHwnd == IntPtr.Zero)
                return;

            var bounds = target.ScreenBounds;
            var flags = SwpNoActivate | SwpNoZOrder;
            if (target.IsVisible && _overlayShouldBeVisible)
                flags |= SwpShowWindow;
            else
                flags |= SwpHideWindow;

            NativeWindowHelper.SetWindowPos(
                _overlayHwnd,
                IntPtr.Zero,
                bounds.X,
                bounds.Y,
                Math.Max(1, bounds.Width),
                Math.Max(1, bounds.Height),
                flags);

            if (!target.IsVisible)
            {
                NativeWindowHelper.SetWindowPos(
                    _overlayHwnd,
                    IntPtr.Zero,
                    0,
                    0,
                    0,
                    0,
                    SwpNoActivate | SwpNoMove | SwpNoSize | SwpHideWindow);
            }
        }

        private void DestroyOverlayWindow()
        {
            if (_overlayHwnd == IntPtr.Zero)
                return;
            var hwnd = _overlayHwnd;
            _overlayHwnd = IntPtr.Zero;
            DestroyWindow(hwnd);
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
                        throw new Win32Exception(error, "RegisterClassEx failed for wet ink overlay.");
                }

                _classRegistered = true;
            }
        }

        private static IntPtr OverlayWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WmNcHitTest)
                return new IntPtr(HtTransparent);
            if (msg == WmDestroy)
                return IntPtr.Zero;
            return DefWindowProc(hwnd, msg, wParam, lParam);
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WetInkWindowHost));
        }

        private delegate IntPtr WndProcDelegate(
            IntPtr hwnd,
            uint msg,
            IntPtr wParam,
            IntPtr lParam);

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
        private static extern IntPtr DefWindowProc(
            IntPtr hWnd,
            uint msg,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
