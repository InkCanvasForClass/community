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
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOPMOST = 0x00000008;

        #endregion

        #region 状态管理

        private static readonly List<PopupManagerHelper> _activeInstances = new List<PopupManagerHelper>();

        private readonly List<Popup> _registeredPopups = new List<Popup>();
        private Window _ownerWindow;
        private IntPtr _ownerHwnd = IntPtr.Zero;
        private bool _isInitialized = false;
        private bool _needsUpdate = false;
        private int _topmostCheckCounter = 0;
        private const int TopmostCheckInterval = 15;

        #endregion

        #region 条件置顶回调

        public Func<bool> ShouldBeTopmost { get; set; }

        private bool CheckShouldBeTopmost()
        {
            return ShouldBeTopmost == null || ShouldBeTopmost();
        }

        #endregion

        #region 初始化与注册

        public void Initialize(Window ownerWindow)
        {
            if (_isInitialized) return;

            _ownerWindow = ownerWindow;
            if (_ownerWindow != null)
            {
                _ownerHwnd = new WindowInteropHelper(_ownerWindow).Handle;
            }

            try
            {
                CompositionTarget.Rendering += OnRendering;
                _activeInstances.Add(this);
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
            popup.Closed += OnPopupClosed;

            if (popup.Child is FrameworkElement child && !popup.IsOpen)
            {
                child.Visibility = Visibility.Collapsed;
            }

            System.Diagnostics.Debug.WriteLine($"[PopupManager] Registered popup: {popup.Name ?? "unnamed"}");
        }

        public void UnregisterPopup(Popup popup)
        {
            if (popup == null) return;

            popup.Opened -= OnPopupOpened;
            popup.Closed -= OnPopupClosed;
            _registeredPopups.Remove(popup);
        }

        private void OnPopupOpened(object sender, EventArgs e)
        {
            var popup = sender as Popup;
            if (popup == null) return;

            if (popup.Child is FrameworkElement child)
            {
                child.Visibility = Visibility.Visible;
            }

            FixPopupZOrder(popup);

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                FixPopupZOrder(popup);
            }), DispatcherPriority.Loaded);

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                FixPopupZOrder(popup);
            }), DispatcherPriority.Background);
        }

        private void OnPopupClosed(object sender, EventArgs e)
        {
            var popup = sender as Popup;
            if (popup == null) return;

            if (popup.Child is FrameworkElement child)
            {
                child.Visibility = Visibility.Collapsed;
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
            if (popup?.Child == null) return;

            FixPopupZOrder(popup);

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                FixPopupZOrder(popup);
            }), DispatcherPriority.Render);

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                FixPopupZOrder(popup);
            }), DispatcherPriority.Background);
        }

        public void BringToFrontLight(Popup popup)
        {
            BringToFront(popup);
        }

        public void UpdatePosition(Popup popup)
        {
            if (popup == null || !popup.IsOpen || popup.PlacementTarget == null) return;

            try
            {
                var hOffset = popup.HorizontalOffset;
                var vOffset = popup.VerticalOffset;

                popup.HorizontalOffset = hOffset + 0.001;
                popup.VerticalOffset = vOffset + 0.001;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] UpdatePosition error: {ex.Message}");
            }
        }

        public void OnOwnerActivated()
        {
            foreach (var popup in _registeredPopups)
            {
                if (popup.IsOpen)
                {
                    FixPopupZOrder(popup);
                }
            }
        }

        public static void NotifyTopmostMaintained()
        {
            for (int i = 0; i < _activeInstances.Count; i++)
            {
                _activeInstances[i].OnOwnerActivated();
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
                    foreach (var popup in _registeredPopups)
                    {
                        UpdatePosition(popup);
                    }
                    _needsUpdate = false;
                }

                _topmostCheckCounter++;
                if (_topmostCheckCounter >= TopmostCheckInterval)
                {
                    _topmostCheckCounter = 0;
                    foreach (var popup in _registeredPopups)
                    {
                        if (popup.IsOpen)
                        {
                            FixPopupZOrder(popup);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] OnRendering error: {ex.Message}");
            }
        }

        #endregion

        #region 核心：修复 Popup Z-Order

        private void FixPopupZOrder(Popup popup)
        {
            if (popup?.Child == null) return;

            try
            {
                var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
                if (source?.Handle == null) return;

                var popupHwnd = source.Handle;
                var shouldBeTopmost = CheckShouldBeTopmost();

                if (shouldBeTopmost)
                {
                    SetWindowPos(popupHwnd, HWND_TOPMOST, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);

                    if (_ownerHwnd != IntPtr.Zero)
                    {
                        SetWindowPos(_ownerHwnd, popupHwnd, 0, 0, 0, 0,
                            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    }
                }
                else
                {
                    int exStyle = GetWindowLong(popupHwnd, GWL_EXSTYLE);
                    if ((exStyle & WS_EX_TOPMOST) != 0)
                    {
                        SetWindowLong(popupHwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TOPMOST);
                    }

                    SetWindowPos(popupHwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] FixPopupZOrder failed: {ex.Message}");
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
                    popup.Closed -= OnPopupClosed;
                }
                _registeredPopups.Clear();
                _activeInstances.Remove(this);
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
