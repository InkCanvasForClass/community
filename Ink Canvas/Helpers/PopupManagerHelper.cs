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
    /// <summary>
    /// WPF Popup 管理器 - 提供置顶和拖动跟随功能
    /// 
    /// 功能：
    /// 1. Topmost 管理：确保 Popup 始终在其他控件之上
    /// 2. 拖动跟随：让 Popup 在父容器拖动时平滑跟随移动
    /// 
    /// 使用方式：
    /// var manager = new PopupManagerHelper();
    /// manager.Initialize();
    /// manager.RegisterPopup(myPopup);
    /// </summary>
    public class PopupManagerHelper
    {
        #region Win32 API

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        #endregion

        #region 配置

        /// <summary>
        /// PopupManagerHelper 配置选项
        /// </summary>
        public class Config
        {
            public int TopmostCheckInterval { get; set; } = 10; // 每 N 帧检查一次置顶（默认10帧≈160ms）
            public bool UseRenderingSync { get; set; } = true; // 是否使用渲染同步
            public int InitialTopmostAttempts { get; set; } = 3; // 初始显示时的置顶次数
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

        /// <summary>
        /// 创建 PopupManagerHelper 实例（使用默认配置）
        /// </summary>
        public PopupManagerHelper() : this(new Config()) { }

        /// <summary>
        /// 创建 PopupManagerHelper 实例（自定义配置）
        /// </summary>
        /// <param name="config">配置选项</param>
        public PopupManagerHelper(Config config)
        {
            _config = config ?? new Config();
        }

        #endregion

        #region 初始化与注册

        /// <summary>
        /// 初始化管理器（订阅渲染事件，通常在 Window_Loaded 中调用一次）
        /// </summary>
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

        /// <summary>
        /// 注册需要管理的 Popup 控件
        /// </summary>
        /// <param name="popup">要管理的 Popup</param>
        public void RegisterPopup(Popup popup)
        {
            if (popup == null || _registeredPopups.Contains(popup)) return;

            _registeredPopups.Add(popup);

            // 如果 Popup 已经打开，立即执行强力置顶
            if (popup.IsOpen)
            {
                BringToFront(popup);
            }

            System.Diagnostics.Debug.WriteLine($"[PopupManager] Registered popup: {popup.Name ?? "unnamed"}");
        }

        /// <summary>
        /// 注销不再管理的 Popup 控件
        /// </summary>
        /// <param name="popup">要注销的 Popup</param>
        public void UnregisterPopup(Popup popup)
        {
            if (popup == null) return;

            _registeredPopups.Remove(popup);
            System.Diagnostics.Debug.WriteLine($"[PopupManager] Unregistered popup: {popup.Name ?? "unnamed"}");
        }

        #endregion

        #region 公共 API - 供外部调用

        /// <summary>
        /// 标记需要更新位置（在拖动事件中调用）
        /// </summary>
        public void MarkNeedsUpdate()
        {
            _needsUpdate = true;
        }

        /// <summary>
        /// 强制将 Popup 提升到最顶层（多次调用确保生效）
        /// 用于初始显示或动画完成后
        /// </summary>
        /// <param name="popup">要置顶的 Popup</param>
        public void BringToFront(Popup popup)
        {
            BringToFrontInternal(popup, _config.InitialTopmostAttempts);
        }

        /// <summary>
        /// 轻量级置顶（单次调用，用于拖动时或定期保顶）
        /// </summary>
        /// <param name="popup">要置顶的 Popup</param>
        public void BringToFrontLight(Popup popup)
        {
            BringToFrontAsync(popup);
        }

        /// <summary>
        /// 更新 Popup 位置（通过 Offset 微调，不重建窗口）
        /// </summary>
        /// <param name="popup">要更新位置的 Popup</param>
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

        /// <summary>
        /// 渲染周期回调（每帧自动触发）
        /// 处理位置更新和置顶维护
        /// </summary>
        private void OnRendering(object sender, EventArgs e)
        {
            try
            {
                if (_needsUpdate)
                {
                    // 拖动中：更新位置 + 同步置顶
                    UpdateAllPositions();
                    BringAllToFrontSync();
                    _needsUpdate = false;
                    return;
                }

                // 静止时：低频保顶（使用计数器节流）
                MaintainTopmostForAll();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] OnRendering error: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新所有已注册 Popup 的位置
        /// </summary>
        private void UpdateAllPositions()
        {
            foreach (var popup in _registeredPopups)
            {
                UpdatePosition(popup);
            }
        }

        /// <summary>
        /// 为所有打开的 Popup 执行轻量级置顶（拖动时每帧调用）
        /// 使用同步调用确保时序正确
        /// </summary>
        private void BringAllToFrontSync()
        {
            foreach (var popup in _registeredPopups)
            {
                if (popup.IsOpen && popup.PlacementTarget != null)
                {
                    BringToFrontSync(popup);
                }
            }
        }

        /// <summary>
        /// 为所有已注册的打开的 Popup 维持置顶状态
        /// 使用计数器降低调用频率，使用同步调用避免闪烁
        /// </summary>
        private void MaintainTopmostForAll()
        {
            _topmostCounter++;
            if (_topmostCounter < _config.TopmostCheckInterval) return;
            _topmostCounter = 0;

            foreach (var popup in _registeredPopups)
            {
                if (popup.IsOpen && popup.PlacementTarget != null)
                {
                    BringToFrontSync(popup);  // 改用同步版本
                }
            }
        }

        #endregion

        #region 内部实现 - Win32 操作

        /// <summary>
        /// 同步置顶（直接调用，无异步延迟）
        /// 用于拖动时和定期保顶，确保时序正确
        /// </summary>
        private void BringToFrontSync(Popup popup)
        {
            if (popup?.Child == null) return;

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
                System.Diagnostics.Debug.WriteLine($"[PopupManager] BringToFrontSync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 多次尝试将 Popup 置顶（异步版本，仅用于初始显示）
        /// </summary>
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

        /// <summary>
        /// 异步轻量级置顶（单次调用）
        /// </summary>
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

        /// <summary>
        /// 清理资源（在窗口关闭时调用）
        /// </summary>
        public void Cleanup()
        {
            if (!_isInitialized) return;

            try
            {
                CompositionTarget.Rendering -= OnRendering;
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
