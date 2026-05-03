using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    public class PopupManagerHelper
    {
        #region Win32 API

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOOWNERZORDER = 0x0200;
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

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyTopmostState(popup);
            }), DispatcherPriority.Loaded);
        }

        #endregion

        #region 公共 API

        public void MarkNeedsUpdate()
        {
            _needsUpdate = true;
        }

        public void BringToFront(Popup popup)
        {
            if (popup?.Child == null) return;

            Action bringToTopAction = () =>
            {
                try
                {
                    var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
                    if (source?.Handle == null) return;

                    ApplyTopmostStateToHwnd(source.Handle);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PopupManager] BringToFront failed: {ex.Message}");
                }
            };

            for (int i = 0; i < _config.InitialTopmostAttempts; i++)
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

        public void BringToFrontLight(Popup popup)
        {
            if (popup?.Child == null) return;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
                    if (source?.Handle == null) return;

                    ApplyTopmostStateToHwnd(source.Handle);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PopupManager] BringToFrontLight failed: {ex.Message}");
                }
            }), DispatcherPriority.Render);
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

            try
            {
                var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
                if (source?.Handle == null) return;

                ApplyTopmostStateToHwnd(source.Handle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] ApplyTopmostState failed: {ex.Message}");
            }
        }

        private void ApplyTopmostStateToHwnd(IntPtr hwnd)
        {
            var shouldBeTopmost = CheckShouldBeTopmost();

            try
            {
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

                if (shouldBeTopmost)
                {
                    if ((exStyle & WS_EX_TOPMOST) != 0)
                    {
                        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TOPMOST);
                    }

                    SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
                }
                else
                {
                    if ((exStyle & WS_EX_TOPMOST) != 0)
                    {
                        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TOPMOST);
                    }

                    SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] ApplyTopmostStateToHwnd failed: {ex.Message}");
            }
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
