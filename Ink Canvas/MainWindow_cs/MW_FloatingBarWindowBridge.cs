using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Ink_Canvas.Helpers;
using Ink_Canvas.Windows;

namespace Ink_Canvas
{
    /// <summary>
    /// 浮动工具栏拆窗后的桥接层。
    /// </summary>
    /// <remarks>
    /// 拆分的原则是「只搬视树，不动坐标」：
    /// - <see cref="ViewboxFloatingBar"/>.Margin 仍然是唯一的位置真值，单位和原点都不变
    ///   （相对 MainWindow 客户区）。所有既有定位、居中、折叠代码继续往它写。
    /// - <see cref="FloatingToolbarWindow"/> 的客户区与主窗口客户区逐像素重合，
    ///   因此窗口内坐标天然就是主窗口客户区坐标，Margin 不需要换算；窗口只在
    ///   主窗口移动/缩放/DPI 变化时重新对齐。
    /// - 元素的同名属性直通到新窗口，外部代码（ToolbarRegistry / 高亮 / 弹窗）不用改。
    /// </remarks>
    public partial class MainWindow
    {
        private FloatingToolbarWindow _floatingToolbarWindow;
        private DependencyPropertyDescriptor _mainWindowTopmostDescriptor;
        private EventHandler _mainWindowTopmostChangedHandler;
        private bool _floatingBarWindowHooksAttached;

        private HwndSource _ownerHwndSource;
        private bool _floatingBarZOrderGuardInstalled;
        private bool _floatingBarZOrderAssertPending;
        private DispatcherTimer _floatingBarZOrderWatchdog;

        /// <summary>桥接层是否已经主动显示过工具栏窗口（用于过滤启动早期的主窗口 IsVisibleChanged）。</summary>
        private bool _floatingBarWindowShown;

        #region 视树直通

        internal FloatingToolbarWindow FloatingToolbarWindowInstance => _floatingToolbarWindow;

        internal Viewbox ViewboxFloatingBar => _floatingToolbarWindow?.ViewboxFloatingBar;

        internal ScaleTransform ViewboxFloatingBarScaleTransform => _floatingToolbarWindow?.ViewboxFloatingBarScaleTransform;

        internal Grid GridFloatingBarContainer => _floatingToolbarWindow?.GridFloatingBarContainer;

        internal iNKORE.UI.WPF.Controls.SimpleStackPanel StackPanelFloatingBarRoot => _floatingToolbarWindow?.StackPanelFloatingBarRoot;

        internal Border BorderFloatingBarMoveControls => _floatingToolbarWindow?.BorderFloatingBarMoveControls;

        internal Image FloatingbarHeadIconImg => _floatingToolbarWindow?.FloatingbarHeadIconImg;

        internal System.Windows.Controls.Canvas CanvasFloatingBarSelection => _floatingToolbarWindow?.CanvasFloatingBarSelection;

        internal Border SelectionBGFloatingBar => _floatingToolbarWindow?.SelectionBGFloatingBar;

        internal Border IndicatorBarFloatingBar => _floatingToolbarWindow?.IndicatorBarFloatingBar;

        #endregion

        /// <summary>创建工具栏窗口（不显示）。构造函数里调用，保证后续代码拿到的元素非空。</summary>
        internal void EnsureFloatingToolbarWindow()
        {
            if (_floatingToolbarWindow != null) return;

            _floatingToolbarWindow = new FloatingToolbarWindow(this);

            // 工具栏窗口自己维护「紧贴主窗口上方」的层级。交给 WindowTopmostManager
            // 统一置顶的话，主窗口每次激活重新置顶时都会把它压到画布下面。
            WindowTopmostManager.ExcludeWindow(_floatingToolbarWindow);

            AttachFloatingBarWindowHooks();
            StartFloatingBarZOrderWatchdog();
        }

        /// <summary>显示工具栏窗口。不激活主窗口，也不抢焦点。</summary>
        internal void ShowFloatingToolbarWindow()
        {
            EnsureFloatingToolbarWindow();
            if (_floatingToolbarWindow.Visibility != Visibility.Visible)
            {
                // 设为主窗口的从属窗口：主窗口最小化/隐藏时自动跟走，层级也天然在主窗口之上。
                _floatingToolbarWindow.Owner = this;
                _floatingToolbarWindow.Topmost = Topmost;
                _floatingToolbarWindow.Show();
            }

            _floatingToolbarWindow.SyncToOwnerBounds();
            _floatingToolbarWindow.EnsureAboveOwner();
            SyncFloatingBarHostState();
            _floatingBarWindowShown = true;
        }

        private void AttachFloatingBarWindowHooks()
        {
            if (_floatingBarWindowHooksAttached) return;
            _floatingBarWindowHooksAttached = true;

            // 窗口客户区跟随主窗口客户区；主窗口移动 / 尺寸变化 / DPI 变化都要重新对齐。
            LocationChanged += (s, e) => _floatingToolbarWindow?.OnOwnerGeometryChanged();
            SizeChanged += (s, e) => _floatingToolbarWindow?.OnOwnerGeometryChanged();
            DpiChanged += (s, e) => _floatingToolbarWindow?.OnOwnerGeometryChanged();

            // 顶层层级跟着主窗口：主窗口置顶时工具栏也必须置顶。
            _mainWindowTopmostDescriptor = DependencyPropertyDescriptor.FromProperty(
                Window.TopmostProperty, typeof(Window));
            _mainWindowTopmostChangedHandler = (s, e) => SyncFloatingBarTopmost();
            _mainWindowTopmostDescriptor?.AddValueChanged(this, _mainWindowTopmostChangedHandler);

            // 主窗口的 Z 序变化（激活、原生置顶）不会经过上面的 DP 通知，
            // 必须直接监听窗口消息，把工具栏重新排回主窗口正上方。
            if (PresentationSource.FromVisual(this) is HwndSource)
            {
                InstallFloatingBarZOrderGuard();
            }
            else
            {
                SourceInitialized += (s, e) => InstallFloatingBarZOrderGuard();
            }

            // FloatingbarUIForInkReplay 仍留在主窗口（闲置迷你栏在它下面），
            // 它的显隐/命中开关要继续控制整个浮动栏体系。
            FloatingbarUIForInkReplay.IsVisibleChanged += (s, e) => SyncFloatingBarHostState();
            FloatingbarUIForInkReplay.IsHitTestVisibleChanged += (s, e) => SyncFloatingBarHostState();

            IsVisibleChanged += (s, e) =>
            {
                // 启动阶段主窗口 Show() 会先触发一次 IsVisibleChanged，
                // 那时还没定过位，直接 Show 会在屏幕左上角闪一下，所以只跟随后续变化。
                if (_floatingToolbarWindow == null || !_floatingBarWindowShown) return;
                if (IsVisible)
                {
                    _floatingToolbarWindow.Show();
                    _floatingToolbarWindow.EnsureAboveOwner();
                }
                else
                {
                    _floatingToolbarWindow.Hide();
                }
            };

            Closed += (s, e) => DisposeFloatingToolbarWindow();
        }

        private void SyncFloatingBarHostState()
        {
            if (_floatingToolbarWindow == null) return;

            _floatingToolbarWindow.ContentHost.Visibility =
                FloatingbarUIForInkReplay.IsVisible ? Visibility.Visible : Visibility.Collapsed;

            _floatingToolbarWindow.SetHitTestTransparent(!FloatingbarUIForInkReplay.IsHitTestVisible);
        }

        private void SyncFloatingBarTopmost()
        {
            if (_floatingToolbarWindow == null) return;
            _floatingToolbarWindow.Topmost = Topmost;
            _floatingToolbarWindow.EnsureAboveOwner();
        }

        /// <summary>监听主窗口的窗口消息，捕获激活 / 原生置顶导致的 Z 序变化。</summary>
        private void InstallFloatingBarZOrderGuard()
        {
            if (_floatingBarZOrderGuardInstalled) return;
            if (PresentationSource.FromVisual(this) is not HwndSource source) return;

            _ownerHwndSource = source;
            _ownerHwndSource.AddHook(FloatingBarOwnerZOrderHook);
            _floatingBarZOrderGuardInstalled = true;
        }

        private IntPtr FloatingBarOwnerZOrderHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_ACTIVATE = 0x0006;
            const int WM_WINDOWPOSCHANGED = 0x0047;

            if (msg == WM_ACTIVATE || msg == WM_WINDOWPOSCHANGED)
            {
                QueueFloatingBarZOrderAssert();
            }

            return IntPtr.Zero;
        }

        /// <summary>把重排合并成一次调度，避免连续消息里反复调用 SetWindowPos。</summary>
        private void QueueFloatingBarZOrderAssert()
        {
            if (_floatingBarZOrderAssertPending) return;
            _floatingBarZOrderAssertPending = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                _floatingBarZOrderAssertPending = false;
                _floatingToolbarWindow?.EnsureAboveOwner();
            }), DispatcherPriority.Input);
        }

        /// <summary>
        /// 低频兜底巡检：主窗口一旦真的把工具栏压回下面，就立刻修回并记一条日志。
        /// 平时只读 Z 序，不改变任何状态。
        /// </summary>
        private void StartFloatingBarZOrderWatchdog()
        {
            if (_floatingBarZOrderWatchdog != null) return;

            _floatingBarZOrderWatchdog = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _floatingBarZOrderWatchdog.Tick += (s, e) => _floatingToolbarWindow?.VerifyZOrderAboveOwner();
            _floatingBarZOrderWatchdog.Start();
        }

        private void DisposeFloatingToolbarWindow()
        {
            if (_floatingToolbarWindow == null) return;

            if (_mainWindowTopmostDescriptor != null && _mainWindowTopmostChangedHandler != null)
            {
                _mainWindowTopmostDescriptor.RemoveValueChanged(this, _mainWindowTopmostChangedHandler);
            }
            _mainWindowTopmostDescriptor = null;
            _mainWindowTopmostChangedHandler = null;

            if (_floatingBarZOrderGuardInstalled && _ownerHwndSource != null)
            {
                _ownerHwndSource.RemoveHook(FloatingBarOwnerZOrderHook);
            }
            _floatingBarZOrderGuardInstalled = false;
            _ownerHwndSource = null;

            _floatingBarZOrderWatchdog?.Stop();
            _floatingBarZOrderWatchdog = null;

            try
            {
                _floatingToolbarWindow.Close();
            }
            catch
            {
                // 关窗期间的异常不该影响主窗口退出流程
            }

            _floatingToolbarWindow = null;
            _floatingBarWindowHooksAttached = false;
        }

        /// <summary>
        /// 量出浮动栏在 MainWindow 客户区坐标系下的包围盒（DIP，与 Margin 同一坐标系）。
        /// </summary>
        /// <remarks>
        /// 拆窗后 ViewboxFloatingBar 不再是 MainWindow 的可视化后代，
        /// TransformToAncestor / TransformBounds 会抛 InvalidOperationException。
        /// 这里改用屏幕坐标中转：两端屏幕点相减，再除以主窗口 DPI 即得客户区 DIP。
        /// 窗口尚未拿到 HwndSource（启动早期）时返回 <see cref="Rect.Empty"/>，
        /// 调用方的 Contains 会自然返回 false，与拆分前「不在浮动栏内」的语义一致。
        /// </remarks>
        internal Rect GetFloatingBarBoundsInMainWindow()
        {
            var viewbox = ViewboxFloatingBar;
            if (viewbox == null) return Rect.Empty;

            try
            {
                // LayoutTransform 缩放不在 ActualWidth 里，TransformBounds 当年是算进去的，
                // 这里手动补上，否则调大工具栏缩放后判定区域会偏小。
                var scaleX = ViewboxFloatingBarScaleTransform?.ScaleX ?? 1.0;
                var scaleY = ViewboxFloatingBarScaleTransform?.ScaleY ?? 1.0;
                var width = viewbox.ActualWidth * scaleX;
                var height = viewbox.ActualHeight * scaleY;
                if (width <= 0 || height <= 0) return Rect.Empty;

                var barOrigin = viewbox.PointToScreen(new Point(0, 0));
                var ownerOrigin = PointToScreen(new Point(0, 0));
                var dpi = VisualTreeHelper.GetDpi(this);

                return new Rect(
                    (barOrigin.X - ownerOrigin.X) / dpi.DpiScaleX,
                    (barOrigin.Y - ownerOrigin.Y) / dpi.DpiScaleY,
                    width,
                    height);
            }
            catch (InvalidOperationException)
            {
                return Rect.Empty;
            }
        }

        #region 拖动转发（子窗口事件 → 主窗口既有拖动逻辑）

        internal void HandleFloatingBarHeadMouseDown(MouseButtonEventArgs e)
            => SymbolIconEmoji_MouseDown(null, e);

        internal void HandleFloatingBarHeadMouseMove(MouseEventArgs e)
            => SymbolIconEmoji_MouseMove(null, e);

        internal void HandleFloatingBarHeadMouseUp(MouseButtonEventArgs e)
            => SymbolIconEmoji_MouseUp(null, e);

        /// <summary>拖动被系统打断时的收尾：只复位状态，不触发点击语义。</summary>
        internal void CancelFloatingBarHeadDrag()
        {
            isDragDropInEffect = false;
            GridForFloatingBarDraging.Visibility = Visibility.Collapsed;
        }

        #endregion
    }
}
