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

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
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
        private readonly Dictionary<Popup, IntPtr> _hwndCache = new Dictionary<Popup, IntPtr>();
        private readonly HashSet<Popup> _openPopups = new HashSet<Popup>();
        private Window _ownerWindow;
        private IntPtr _ownerHwnd = IntPtr.Zero;
        private bool _isInitialized = false;
        private bool _needsUpdate = false;
        private DispatcherTimer _periodicCheckTimer;
        private bool _lastTopmostState = false;
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
                _periodicCheckTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _periodicCheckTimer.Tick += OnPeriodicCheck;

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
            _hwndCache.Remove(popup);
            _openPopups.Remove(popup);
        }

        private void OnPopupOpened(object sender, EventArgs e)
        {
            var popup = sender as Popup;
            if (popup == null) return;

            if (popup.Child is FrameworkElement child)
            {
                child.Visibility = Visibility.Visible;
            }

            _openPopups.Add(popup);
            _hwndCache.Remove(popup);

            FixPopupZOrder(popup);

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                FixPopupZOrder(popup);
                if (popup.Child is FrameworkElement child)
                    FixChildPopups(child);
            }), DispatcherPriority.Loaded);

            UpdateTimerState();
        }

        private void OnPopupClosed(object sender, EventArgs e)
        {
            var popup = sender as Popup;
            if (popup == null) return;

            if (popup.Child is FrameworkElement child)
            {
                child.Visibility = Visibility.Collapsed;
            }

            _openPopups.Remove(popup);
            _hwndCache.Remove(popup);

            UpdateTimerState();
        }

        private void UpdateTimerState()
        {
            if (_periodicCheckTimer == null) return;

            if (_openPopups.Count > 0 && CheckShouldBeTopmost())
            {
                if (!_periodicCheckTimer.IsEnabled)
                    _periodicCheckTimer.Start();
            }
            else
            {
                _periodicCheckTimer.Stop();
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
                if (popup.Child is FrameworkElement child)
                    FixChildPopups(child);
            }), DispatcherPriority.Render);
        }

        public void BringToFrontLight(Popup popup)
        {
            if (popup?.Child == null) return;
            FixPopupZOrder(popup);
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
            foreach (var popup in _openPopups)
            {
                FixPopupZOrder(popup);
            }
        }

        public static void NotifyTopmostMaintained()
        {
            for (int i = 0; i < _activeInstances.Count; i++)
            {
                _activeInstances[i].OnOwnerActivated();
            }
        }

        public void OnTopmostSettingChanged()
        {
            var shouldBeTopmost = CheckShouldBeTopmost();

            if (_lastTopmostState != shouldBeTopmost)
            {
                _lastTopmostState = shouldBeTopmost;

                foreach (var popup in _openPopups)
                {
                    _hwndCache.Remove(popup);
                    FixPopupZOrder(popup);
                }
            }

            UpdateTimerState();
        }

        #endregion

        #region 内部实现 - 渲染回调与定时器

        private void OnRendering(object sender, EventArgs e)
        {
            try
            {
                if (_openPopups.Count == 0) return;

                if (_needsUpdate)
                {
                    foreach (var popup in _openPopups)
                    {
                        UpdatePosition(popup);
                    }
                    _needsUpdate = false;
                }

                _topmostCheckCounter++;
                if (_topmostCheckCounter >= TopmostCheckInterval)
                {
                    _topmostCheckCounter = 0;
                    foreach (var popup in _openPopups)
                    {
                        FixPopupZOrder(popup);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] OnRendering error: {ex.Message}");
            }
        }

        private void OnPeriodicCheck(object sender, EventArgs e)
        {
            if (_periodicCheckTimer == null) return;

            try
            {
                if (_openPopups.Count == 0)
                {
                    _periodicCheckTimer.Stop();
                    return;
                }

                foreach (var popup in _openPopups)
                {
                    FixPopupZOrder(popup);
                    if (popup.Child is FrameworkElement child)
                        FixChildPopups(child);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] OnPeriodicCheck error: {ex.Message}");
            }
        }

        #endregion

        #region 核心：修复 Popup Z-Order

        private IntPtr GetPopupHwnd(Popup popup)
        {
            if (_hwndCache.TryGetValue(popup, out IntPtr cached) && IsWindow(cached))
            {
                return cached;
            }

            var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
            if (source?.Handle == IntPtr.Zero || !IsWindow(source.Handle))
            {
                _hwndCache.Remove(popup);
                return IntPtr.Zero;
            }

            var hwnd = source.Handle;
            _hwndCache[popup] = hwnd;
            return hwnd;
        }

        private void FixPopupZOrder(Popup popup)
        {
            if (popup?.Child == null) return;

            try
            {
                var popupHwnd = GetPopupHwnd(popup);
                if (popupHwnd == IntPtr.Zero) return;

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

        private void FixChildPopups(FrameworkElement root)
        {
            if (root == null || !CheckShouldBeTopmost()) return;

            try
            {
                foreach (var childPopup in FindVisualChildren<Popup>(root))
                {
                    if (childPopup.IsOpen && childPopup.Child != null)
                    {
                        var source = PresentationSource.FromVisual(childPopup.Child) as HwndSource;
                        if (source?.Handle != IntPtr.Zero)
                        {
                            SetWindowPos(source.Handle, HWND_TOPMOST, 0, 0, 0, 0,
                                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] FixChildPopups failed: {ex.Message}");
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result) yield return result;
                foreach (var descendant in FindVisualChildren<T>(child))
                    yield return descendant;
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
                if (_periodicCheckTimer != null)
                {
                    _periodicCheckTimer.Stop();
                    _periodicCheckTimer.Tick -= OnPeriodicCheck;
                    _periodicCheckTimer = null;
                }
                foreach (var popup in _registeredPopups)
                {
                    popup.Opened -= OnPopupOpened;
                    popup.Closed -= OnPopupClosed;
                }
                _registeredPopups.Clear();
                _hwndCache.Clear();
                _openPopups.Clear();
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
