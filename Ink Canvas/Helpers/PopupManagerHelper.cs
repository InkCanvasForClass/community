using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace Ink_Canvas.Helpers
{
    public class PopupManagerHelper
    {
        #region Win32 API

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOPMOST = 0x00000008;

        #endregion

        #region 配置

        public class Config
        {
            public int TopmostCheckInterval { get; set; } = 10;
            public bool UseRenderingSync { get; set; } = true;
            public int InitialTopmostAttempts { get; set; } = 3;
        }

        #endregion

        #region 状态管理

        private readonly List<Popup> _registeredPopups = new List<Popup>();
        private readonly Config _config;
        private bool _isInitialized = false;
        private bool _needsUpdate = false;
        private int _topmostCounter = 0;
        private bool _offsetToggle = true;

        #endregion

        #region 构造函数

        public PopupManagerHelper() : this(new Config()) { }
        public PopupManagerHelper(Config config)
        {
            _config = config ?? new Config();
        }

        #endregion

        #region 条件置顶回调

        public Func<bool> ShouldBeTopmost { get; set; }

        private bool CheckShouldBeTopmost()
        {
            return ShouldBeTopmost == null || ShouldBeTopmost();
        }

        #endregion

        #region 初始化与注册

        public void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                if (_config.UseRenderingSync)
                {
                    CompositionTarget.Rendering += OnRendering;
                }
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] Initialize error: {ex.Message}");
            }
        }

        public void RegisterPopup(Popup popup)
        {
            if (popup == null || _registeredPopups.Contains(popup)) return;

            _registeredPopups.Add(popup);
            popup.Opened += OnPopupOpened;

            if (popup.IsOpen)
            {
                BringToFront(popup);
            }

            System.Diagnostics.Debug.WriteLine($"[PopupManager] Registered popup: {popup.Name ?? "unnamed"}");
        }

        public void UnregisterPopup(Popup popup)
        {
            if (popup == null) return;

            popup.Opened -= OnPopupOpened;
            _registeredPopups.Remove(popup);
            System.Diagnostics.Debug.WriteLine($"[PopupManager] Unregistered popup: {popup.Name ?? "unnamed"}");
        }

        private void OnPopupOpened(object sender, EventArgs e)
        {
            var popup = sender as Popup;
            if (popup == null) return;

            if (!CheckShouldBeTopmost())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    RemoveTopmostFromPopup(popup);
                }), DispatcherPriority.Loaded);
            }
        }

        #endregion

        #region 公共 API

        public void MarkNeedsUpdate()
        {
            _needsUpdate = true;
        }

        public void BringToFront(Popup popup)
        {
            if (!CheckShouldBeTopmost()) return;
            BringToFrontInternal(popup, _config.InitialTopmostAttempts);
        }

        public void BringToFrontLight(Popup popup)
        {
            if (!CheckShouldBeTopmost()) return;
            BringToFrontAsync(popup);
        }

        public void UpdatePosition(Popup popup)
        {
            if (popup == null || !popup.IsOpen || popup.PlacementTarget == null) return;

            try
            {
                var hOffset = popup.HorizontalOffset;
                var vOffset = popup.VerticalOffset;

                if (_offsetToggle)
                {
                    popup.HorizontalOffset = hOffset + 0.001;
                    popup.VerticalOffset = vOffset + 0.001;
                }
                else
                {
                    popup.HorizontalOffset = hOffset - 0.001;
                    popup.VerticalOffset = vOffset - 0.001;
                }

                _offsetToggle = !_offsetToggle;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] UpdatePosition error: {ex.Message}");
            }
        }

        #endregion

        #region 内部实现 - 渲染回调

        private void OnRendering(object sender, EventArgs e)
        {
            try
            {
                if (_needsUpdate)
                {
                    UpdateAllPositions();
                    BringAllToFrontSync();
                    _needsUpdate = false;
                    return;
                }

                MaintainTopmostForAll();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] OnRendering error: {ex.Message}");
            }
        }

        private void UpdateAllPositions()
        {
            foreach (var popup in _registeredPopups)
            {
                UpdatePosition(popup);
            }
        }

        private void BringAllToFrontSync()
        {
            foreach (var popup in _registeredPopups)
            {
                if (popup.IsOpen && popup.PlacementTarget != null)
                {
                    ApplyTopmostState(popup);
                }
            }
        }

        private void MaintainTopmostForAll()
        {
            _topmostCounter++;
            if (_topmostCounter < _config.TopmostCheckInterval) return;
            _topmostCounter = 0;

            foreach (var popup in _registeredPopups)
            {
                if (popup.IsOpen && popup.PlacementTarget != null)
                {
                    ApplyTopmostState(popup);
                }
            }
        }

        #endregion

        #region 内部实现 - Win32 操作

        private void ApplyTopmostState(Popup popup)
        {
            if (popup?.Child == null) return;

            var shouldBeTopmost = CheckShouldBeTopmost();

            try
            {
                var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
                if (source?.Handle == null) return;

                if (shouldBeTopmost)
                {
                    SetWindowPos(
                        source.Handle,
                        HWND_TOPMOST,
                        0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                }
                else
                {
                    RemoveTopmostFromHwnd(source.Handle);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] ApplyTopmostState failed: {ex.Message}");
            }
        }

        private void RemoveTopmostFromPopup(Popup popup)
        {
            if (popup?.Child == null) return;

            try
            {
                var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
                if (source?.Handle == null) return;

                RemoveTopmostFromHwnd(source.Handle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] RemoveTopmostFromPopup failed: {ex.Message}");
            }
        }

        private void RemoveTopmostFromHwnd(IntPtr hwnd)
        {
            SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOPMOST) != 0)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TOPMOST);
            }

            System.Diagnostics.Debug.WriteLine($"[PopupManager] Removed TOPMOST from hwnd={hwnd}");
        }

        private void BringToFrontInternal(Popup popup, int attempts)
        {
            if (popup?.Child == null) return;

            Action bringToTopAction = () =>
            {
                try
                {
                    var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
                    if (source?.Handle == null) return;

                    SetWindowPos(
                        source.Handle,
                        HWND_TOPMOST,
                        0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);

                    System.Diagnostics.Debug.WriteLine($"[PopupManager] Set TOPMOST for {popup.Name ?? "unnamed"}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PopupManager] BringToFront failed: {ex.Message}");
                }
            };

            for (int i = 0; i < attempts; i++)
            {
                DispatcherPriority priority;
                switch (i)
                {
                    case 0:
                        priority = DispatcherPriority.Render;
                        break;
                    case 1:
                        priority = DispatcherPriority.Normal;
                        break;
                    default:
                        priority = DispatcherPriority.Background;
                        break;
                }

                Application.Current.Dispatcher.BeginInvoke(bringToTopAction, priority);
            }
        }

        private void BringToFrontAsync(Popup popup)
        {
            if (popup?.Child == null) return;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
                    if (source?.Handle == null) return;

                    SetWindowPos(
                        source.Handle,
                        HWND_TOPMOST,
                        0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PopupManager] BringToFrontLight failed: {ex.Message}");
                }
            }), DispatcherPriority.Render);
        }

        #endregion

        #region 清理

        public void Cleanup()
        {
            if (!_isInitialized) return;

            try
            {
                CompositionTarget.Rendering -= OnRendering;
                foreach (var popup in _registeredPopups)
                {
                    popup.Opened -= OnPopupOpened;
                }
                _registeredPopups.Clear();
                _isInitialized = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] Cleanup error: {ex.Message}");
            }
        }

        #endregion
    }
}
