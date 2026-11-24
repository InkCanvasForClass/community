using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Ink_Canvas.Windows
{
    /// <summary>
    /// 浮动工具栏的承载窗口。
    /// </summary>
    /// <remarks>
    /// 这个窗口只做三件事：
    /// 1. 把工具栏可视树从 MainWindow 里搬出来，让它不再参与主窗口布局；
    /// 2. 让窗口客户区与 MainWindow 客户区逐像素重合，于是窗口内坐标 == 主窗口客户区坐标，
    ///    既有定位/居中/折叠代码照旧写 ViewboxFloatingBar.Margin 就能摆到原来的位置；
    /// 3. 空白像素返回 HTTRANSPARENT，让点击穿透到画布。
    ///
    /// 它**不**接管动画：拆分阶段所有动画仍在原处跑，写的还是 ViewboxFloatingBar 的
    /// Margin / RenderTransform。
    ///
    /// 为什么整块覆盖而不是只开一个小窗：WPF 在 FrameworkElement 的实际尺寸小于期望尺寸时
    /// 会自动加 layout clip，布局位置落在窗口之外的子元素会被整体裁掉。
    /// 实测同一个窗口里，布局在窗口内的元素能正常上屏，而越界后再用 RenderTransform
    /// 拉回窗口内的一律不显示。所以工具栏必须始终布局在窗口内，
    /// 而 Margin 是动画逐帧改写的量，窗口就必须大到能容纳它。
    /// </remarks>
    public partial class FloatingToolbarWindow : Helpers.PerformanceTransparentWin
    {
        private readonly MainWindow _owner;

        /// <summary>头部已按下、正在等 MouseUp 的拖动会话。</summary>
        private bool _dragSession;

        /// <summary>当前是否要求整个窗口点击穿透（拖动时用）。</summary>
        private bool _forceHitTestTransparent;

        internal FloatingToolbarWindow(MainWindow owner)
        {
            _owner = owner;

            InitializeComponent();

            // 拆分前，点在工具栏上的按下事件会冒泡到主窗口的 PreviewMouseDown，
            // 用来收起快捷面板。现在工具栏在别的窗口里，显式转发一次保持原语义。
            RootGrid.PreviewMouseDown += (s, e) => _owner.HandleAutoCollapseOnMouseDown(e);
            RootGrid.MouseMove += RootGrid_MouseMove;
            RootGrid.MouseUp += RootGrid_MouseUp;
            RootGrid.LostMouseCapture += RootGrid_LostMouseCapture;

            SourceInitialized += OnSourceInitialized;
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            var handle = new HWND(new WindowInteropHelper(this).Handle);

            // 不抢焦点、不进 Alt+Tab。Inkeys 的同款做法：没有 WM_MOUSEACTIVATE，
            // 完全靠窗口样式把激活这件事排除掉。
            int exStyle = PInvoke.GetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            PInvoke.SetWindowLong(handle,
                WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
                exStyle | (int)WINDOW_EX_STYLE.WS_EX_NOACTIVATE | (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW);

            if (HwndSource.FromHwnd(handle) is HwndSource source)
            {
                source.AddHook(WndProc);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTTRANSPARENT = -1;
            const int HTCLIENT = 1;

            if (msg != WM_NCHITTEST) return IntPtr.Zero;

            if (_forceHitTestTransparent || ContentHost.Visibility != Visibility.Visible)
            {
                handled = true;
                return new IntPtr(HTTRANSPARENT);
            }

            // lParam 是屏幕物理像素坐标，先转成窗口内的 DIP 再做命中测试。
            long packed = lParam.ToInt64();
            int screenX = unchecked((short)(packed & 0xFFFF));
            int screenY = unchecked((short)((packed >> 16) & 0xFFFF));

            Point local;
            try
            {
                // 必须在 RootGrid 上做命中测试：Window 自身的 Background 会让
                // Window.InputHitTest 对任何一点都返回非空，穿透就失效了。
                local = RootGrid.PointFromScreen(new Point(screenX, screenY));
            }
            catch
            {
                return IntPtr.Zero;
            }

            // 命中测试为空说明这片像素完全透明，交还给下面的窗口（画布）。
            var hit = RootGrid.InputHitTest(local);
            if (hit == null)
            {
                handled = true;
                return new IntPtr(HTTRANSPARENT);
            }

            handled = true;
            return new IntPtr(HTCLIENT);
        }

        /// <summary>把窗口摆到 MainWindow 客户区的位置和大小上。</summary>
        internal void SyncToOwnerBounds()
        {
            if (!IsLoaded) return;

            var rawHandle = new WindowInteropHelper(this).Handle;
            if (rawHandle == IntPtr.Zero) return;

            Point origin;
            Point corner;
            try
            {
                origin = _owner.PointToScreen(new Point(0, 0));
                // 用两个角点换算，顺带把 WindowChrome / 边框等差异吃掉。
                corner = _owner.PointToScreen(new Point(_owner.ActualWidth, _owner.ActualHeight));
            }
            catch
            {
                return;
            }

            int x = (int)Math.Round(origin.X);
            int y = (int)Math.Round(origin.Y);
            int width = (int)Math.Round(corner.X - origin.X);
            int height = (int)Math.Round(corner.Y - origin.Y);
            if (width <= 0 || height <= 0) return;

            PInvoke.SetWindowPos(new HWND(rawHandle), new HWND(IntPtr.Zero), x, y, width, height,
                SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
                SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
                SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);
        }

        /// <summary>MainWindow 的 Margin / 位置 / 尺寸 / DPI 变化后重新对齐窗口。</summary>
        internal void OnOwnerGeometryChanged()
        {
            SyncToOwnerBounds();
        }

        private void SymbolIconEmoji_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragSession = true;
            _owner.HandleFloatingBarHeadMouseDown(e);

            // 捕获放在窗口根上：拖动到窗口外（甚至拖出画布）也能继续收到 MouseMove。
            Mouse.Capture(RootGrid);
            e.Handled = true;
        }

        private void RootGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragSession) return;
            _owner.HandleFloatingBarHeadMouseMove(e);
        }

        private void RootGrid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragSession) return;

            _dragSession = false;
            _owner.HandleFloatingBarHeadMouseUp(e);
            Mouse.Capture(null);
            e.Handled = true;
        }

        private void RootGrid_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (!_dragSession) return;

            // 捕获被系统抢走（比如切窗口）时也要收尾，但**不能**当成一次点击，
            // 否则会意外折叠/展开工具栏。
            _dragSession = false;
            _owner.CancelFloatingBarHeadDrag();
        }

        /// <summary>拖动期间让整个窗口点击穿透，把事件让给 MainWindow 的拖动覆盖层。</summary>
        internal void SetHitTestTransparent(bool transparent)
        {
            if (_forceHitTestTransparent == transparent) return;
            _forceHitTestTransparent = transparent;
        }
    }
}
