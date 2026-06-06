using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    public class PopupManagerHelper : IDisposable
    {
        #region 状态管理

        private static readonly List<PopupManagerHelper> _activeInstances = new List<PopupManagerHelper>();

        private readonly List<Popup> _registeredPopups = new List<Popup>();
        private readonly Dictionary<Popup, IntPtr> _hwndCache = new Dictionary<Popup, IntPtr>();
        private readonly HashSet<Popup> _openPopups = new HashSet<Popup>();
        private Window _ownerWindow;
        private IntPtr _ownerHwnd = IntPtr.Zero;
        private bool _isInitialized = false;
        private bool _needsUpdate = false;
        private bool _lastTopmostState = false;
        private int _topmostCheckCounter = 0;
        private const int TopmostCheckInterval = 25;

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
                if (popup.Child is FrameworkElement child)
                    FixChildPopups(child);
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
                    if (popup.Child is FrameworkElement child)
                        FixChildPopups(child);
                }
            }
        }

        #endregion

        #region 内部实现 - 渲染回调

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
                        if (popup.Child is FrameworkElement child)
                            FixChildPopups(child);
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

        private IntPtr GetPopupHwnd(Popup popup)
        {
            if (_hwndCache.TryGetValue(popup, out IntPtr cached) && NativeWindowHelper.IsWindow(cached))
            {
                return cached;
            }

            var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
            if (source?.Handle == IntPtr.Zero || !NativeWindowHelper.IsWindow(source.Handle))
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
                    NativeWindowHelper.SetWindowPos(popupHwnd, NativeWindowHelper.HWND_TOPMOST, 0, 0, 0, 0,
                        NativeWindowHelper.SWP_NOMOVE | NativeWindowHelper.SWP_NOSIZE | NativeWindowHelper.SWP_NOACTIVATE | NativeWindowHelper.SWP_NOOWNERZORDER);

                    if (_ownerHwnd != IntPtr.Zero)
                    {
                        NativeWindowHelper.SetWindowPos(_ownerHwnd, popupHwnd, 0, 0, 0, 0,
                            NativeWindowHelper.SWP_NOMOVE | NativeWindowHelper.SWP_NOSIZE | NativeWindowHelper.SWP_NOACTIVATE);
                    }
                }
                else
                {
                    int exStyle = NativeWindowHelper.GetWindowLong(popupHwnd, NativeWindowHelper.GWL_EXSTYLE);
                    if ((exStyle & NativeWindowHelper.WS_EX_TOPMOST) != 0)
                    {
                        NativeWindowHelper.SetWindowLong(popupHwnd, NativeWindowHelper.GWL_EXSTYLE, exStyle & ~NativeWindowHelper.WS_EX_TOPMOST);
                    }

                    NativeWindowHelper.SetWindowPos(popupHwnd, NativeWindowHelper.HWND_NOTOPMOST, 0, 0, 0, 0,
                        NativeWindowHelper.SWP_NOMOVE | NativeWindowHelper.SWP_NOSIZE | NativeWindowHelper.SWP_NOACTIVATE);
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
                            NativeWindowHelper.SetWindowPos(source.Handle, NativeWindowHelper.HWND_TOPMOST, 0, 0, 0, 0,
                                NativeWindowHelper.SWP_NOMOVE | NativeWindowHelper.SWP_NOSIZE | NativeWindowHelper.SWP_NOACTIVATE | NativeWindowHelper.SWP_NOOWNERZORDER);
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
                foreach (var popup in _registeredPopups)
                {
                    popup.Opened -= OnPopupOpened;
                    popup.Closed -= OnPopupClosed;
                }
                _registeredPopups.Clear();
                lock (_activeInstances)
                {
                    _activeInstances.Remove(this);
                }
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

        private bool _disposed = false;

        /// <summary>
        /// 释放资源，防止内存泄漏
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Cleanup();
                _disposed = true;
            }
        }

        #endregion
    }
}
