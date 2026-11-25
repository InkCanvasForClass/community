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

        /// <summary>Z 序丢失日志的节流，避免巡检每 400ms 刷一条。</summary>
        private const long ZOrderWarningIntervalMs = 5000;
        private long _lastZOrderWarningTick;

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

        /// <summary>把自己的 Z 序重新排到主窗口（以及它的透明覆盖层）的正上方。</summary>
        /// <remarks>
        /// 主窗口每次被激活（在画布上落笔批注就会激活它）或被原生置顶时，
        /// 会把自己提到同层窗口的最前面，工具栏窗口只镜像了 Topmost 标志、
        /// 没有再次断言自己的相对位置，就会掉到画布下面。
        ///
        /// 另外批注时主窗口会冒出湿墨覆盖层（owner 也是主窗口，创建时间比工具栏晚），
        /// 它天然排在工具栏上面；WindowTopmostManager 还会把这类未纳管窗口提成 TOPMOST。
        /// 所以修复不能只插到主窗口后面，而要排到「主窗口的透明覆盖层」之上。
        /// </remarks>
        internal void EnsureAboveOwner()
        {
            if (!IsLoaded || !IsVisible) return;

            var rawHandle = new WindowInteropHelper(this).Handle;
            if (rawHandle == IntPtr.Zero) return;

            var ownerHandle = new WindowInteropHelper(_owner).Handle;
            if (ownerHandle == IntPtr.Zero) return;

            var toolbar = new HWND(rawHandle);
            var owner = new HWND(ownerHandle);

            // Topmost 必须和主窗口一致，否则非 TOPMOST 的窗口永远插不到 TOPMOST 窗口上面。
            if (Topmost != _owner.Topmost)
            {
                Topmost = _owner.Topmost;
            }

            if (_owner.Topmost)
            {
                // 主窗口处于 TOPMOST 层时，WindowTopmostManager.BoostPopupWindowsAboveChildren
                // 会把湿墨覆盖层等未纳管窗口提成 HWND_TOPMOST，工具栏被排除在那套管理外，
                // 只能自己顶到该层最前面，否则批注落笔后就被覆盖层压住。
                PInvoke.SetWindowPos(toolbar, new HWND(Helpers.NativeWindowHelper.HWND_TOPMOST), 0, 0, 0, 0,
                    SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
                    SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
                    SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
                    SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);
                return;
            }

            // 非置顶时不能用 HWND_TOPMOST（会压住其他应用），改成插到主窗口最上面那个
            // 透明覆盖层之后；没有覆盖层时直接插到主窗口之后。
            var insertAfter = FindTopmostTransparentOverlay(owner);
            if (insertAfter == IntPtr.Zero)
            {
                insertAfter = owner;
            }

            PInvoke.SetWindowPos(toolbar, insertAfter, 0, 0, 0, 0,
                SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
                SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
                SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
                SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);
        }

        /// <summary>检查工具栏窗口是否真的在主窗口及其透明覆盖层上方；不在就修好并记一条日志。</summary>
        /// <returns>检查后是否处于正确层级。</returns>
        internal bool VerifyZOrderAboveOwner()
        {
            if (!IsLoaded || !IsVisible) return true;

            var rawHandle = new WindowInteropHelper(this).Handle;
            var ownerHandle = new WindowInteropHelper(_owner).Handle;
            if (rawHandle == IntPtr.Zero || ownerHandle == IntPtr.Zero) return true;

            var toolbar = new HWND(rawHandle);
            var owner = new HWND(ownerHandle);

            if (IsAboveOwnerAndOverlays(toolbar, owner)) return true;

            long now = Environment.TickCount64;
            if (now - _lastZOrderWarningTick >= ZOrderWarningIntervalMs)
            {
                _lastZOrderWarningTick = now;

                var above = PInvoke.GetWindow(toolbar, GET_WINDOW_CMD.GW_HWNDPREV);
                int ownerExStyle = PInvoke.GetWindowLong(owner, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
                Helpers.LogHelper.WriteLogToFile(
                    $"[浮动工具栏] Z 序丢失，重新排回主窗口上方。toolbar=0x{rawHandle.ToInt64():X} owner=0x{ownerHandle.ToInt64():X} " +
                    $"正上方窗口={DescribeWindow(above)} " +
                    $"Topmost(toolbar/owner)=({Topmost}/{_owner.Topmost}) " +
                    $"ownerRealTopmost={((ownerExStyle & (int)WINDOW_EX_STYLE.WS_EX_TOPMOST) != 0)}",
                    Helpers.LogHelper.LogType.Warning);
            }

            EnsureAboveOwner();
            return false;
        }

        /// <summary>检查工具栏是否在主窗口及其透明覆盖层上方；不在就修好并记一条日志。</summary>
        private static bool IsAboveOwnerAndOverlays(HWND toolbar, HWND owner)
        {
            // 工具栏必须在主窗口之上。
            if (!IsWindowAbove(toolbar, owner)) return false;

            // 主窗口的透明覆盖层（湿墨层）也必须在工具栏之下，否则批注时就把它盖住了。
            var current = owner;
            for (int i = 0; i < 256; i++)
            {
                var above = PInvoke.GetWindow(current, GET_WINDOW_CMD.GW_HWNDPREV);
                if (above == IntPtr.Zero) break;

                if (PInvoke.GetWindow(above, GET_WINDOW_CMD.GW_OWNER) == owner
                    && IsTransparentOverlay(above)
                    && IsWindowAbove(above, toolbar))
                {
                    return false;
                }

                current = above;
            }

            return true;
        }

        /// <summary>upper 是否在 lower 之上：从 lower 沿 Z 序向上（GW_HWNDPREV）能找到 upper 就算。</summary>
        private static bool IsWindowAbove(HWND upper, HWND lower)
        {
            var current = lower;

            for (int i = 0; i < 4096; i++)
            {
                var above = PInvoke.GetWindow(current, GET_WINDOW_CMD.GW_HWNDPREV);
                if (above == IntPtr.Zero) return false;
                if (above == upper) return true;
                current = above;
            }

            return false;
        }

        /// <summary>找主窗口的透明覆盖层里 Z 序最靠上的那个，返回值可能为零。</summary>
        private static HWND FindTopmostTransparentOverlay(HWND owner)
        {
            var result = HWND.Null;
            var current = owner;

            for (int i = 0; i < 256; i++)
            {
                var above = PInvoke.GetWindow(current, GET_WINDOW_CMD.GW_HWNDPREV);
                if (above == IntPtr.Zero) break;

                if (PInvoke.GetWindow(above, GET_WINDOW_CMD.GW_OWNER) == owner && IsTransparentOverlay(above))
                {
                    result = above;
                }

                current = above;
            }

            return result;
        }

        /// <summary>主窗口的透明覆盖层：点击穿透（WS_EX_TRANSPARENT）且不抢焦点（WS_EX_NOACTIVATE）。</summary>
        private static bool IsTransparentOverlay(HWND hwnd)
        {
            const int WsExTransparent = 0x00000020;
            const int WsExNoActivate = unchecked((int)0x08000000);

            int exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            return (exStyle & (WsExTransparent | WsExNoActivate)) == (WsExTransparent | WsExNoActivate);
        }

        /// <summary>把窗口句柄写成「类名 + 标题 + owner + 扩展样式」，用于定位真正压在上面的窗口。</summary>
        private static string DescribeWindow(HWND hwnd)
        {
            if (hwnd == IntPtr.Zero) return "<无>";

            var classBuffer = new char[256];
            int classLength = PInvoke.GetClassName(hwnd, new Span<char>(classBuffer));
            var textBuffer = new char[256];
            int textLength = PInvoke.GetWindowText(hwnd, new Span<char>(textBuffer));

            var className = classLength > 0
                ? new string(classBuffer, 0, Math.Min(classLength, classBuffer.Length))
                : string.Empty;
            var title = textLength > 0
                ? new string(textBuffer, 0, Math.Min(textLength, textBuffer.Length))
                : string.Empty;

            var windowOwner = PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_OWNER);
            int exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

            return $"0x{((IntPtr)hwnd).ToInt64():X}(class='{className}', title='{title}', owner=0x{((IntPtr)windowOwner).ToInt64():X}, exStyle=0x{exStyle:X})";
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
