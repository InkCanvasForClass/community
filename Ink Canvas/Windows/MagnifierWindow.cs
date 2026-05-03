using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Ink_Canvas.Windows
{
    /// <summary>
    /// 使用 Win32 Magnification API (Magnification.dll)。
    /// </summary>
    internal class MagnifierWindow : Window
    {
        #region P/Invoke

        private const string MagnifierClassName = "Magnifier";
        private const int WS_CHILD = 0x40000000;
        private const int WS_VISIBLE = 0x10000000;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int left, top, right, bottom; public RECT(int l, int t, int r, int b) { left = l; top = t; right = r; bottom = b; } }

        [StructLayout(LayoutKind.Sequential)]
        private struct MAGTRANSFORM
        {
            public float m00, m01, m02;
            public float m10, m11, m12;
            public float m20, m21, m22;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle,
            int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);
        [DllImport("user32.dll")] private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("Magnification.dll")] private static extern bool MagInitialize();
        [DllImport("Magnification.dll")] private static extern bool MagUninitialize();
        [DllImport("Magnification.dll")] private static extern bool MagSetWindowSource(IntPtr hwnd, RECT rect);
        [DllImport("Magnification.dll")] private static extern bool MagSetWindowTransform(IntPtr hwnd, ref MAGTRANSFORM pTransform);
        [DllImport("Magnification.dll")] private static extern bool MagSetWindowFilterList(IntPtr hwnd, int dwFilterMode, int count, IntPtr[] pHWND);
        private const int MW_FILTERMODE_EXCLUDE = 0;

        #endregion

        #region 单例

        private static MagnifierWindow _instance;
        public static bool HasInstance => _instance != null;
        public static event EventHandler Closed2;

        public static void Show(float zoom)
        {
            if (_instance != null) { _instance.Activate(); _instance.Zoom = zoom; return; }
            _instance = new MagnifierWindow { Zoom = zoom };
            _instance.Closed += (s, e) =>
            {
                _instance = null;
                Closed2?.Invoke(null, EventArgs.Empty);
            };
            _instance.Show();
        }

        public static void HideInstance() => _instance?.Close();
        public static void SetZoom(float zoom) { if (_instance != null) _instance.Zoom = zoom; }

        #endregion

        // 选择框（放大区域）在 Canvas 中的几何属性 (DIP)
        private double _boxLeft = 200;
        private double _boxTop = 150;
        private double _boxWidth = 520;
        private double _boxHeight = 360;
        private const double MinBoxW = 200;
        private const double MinBoxH = 140;

        private System.Windows.Controls.Canvas _canvas;
        private Rectangle _overlay;          // 半透明遮罩
        private Border _selectionBorder;     // 白色边框选择框
        private MagnifierHost _magHost;
        private System.Windows.Controls.Border _toolbar;
        private Ellipse[] _handles;          // 8 个控点
        private ToggleButton _blackoutButton;
        private bool _blackoutOn;

        private IntPtr _magHwnd;
        private HwndSource _hwndSource;
        private DispatcherTimer _timer;
        private bool _magInitialized;

        private float _zoom = 2.0f;
        public float Zoom
        {
            get => _zoom;
            set
            {
                _zoom = Math.Max(1.1f, Math.Min(8.0f, value));
                if (_magHwnd != IntPtr.Zero) ApplyTransform();
            }
        }

        private MagnifierWindow()
        {
            Title = "聚焦放大镜";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            WindowState = WindowState.Maximized;

            _canvas = new System.Windows.Controls.Canvas { Background = Brushes.Transparent };

            // 半透明遮罩：固定浅色，挖空选择框
            _overlay = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(102, 0, 0, 0)),
                IsHitTestVisible = false
            };
            _canvas.Children.Add(_overlay);

            // 选择框
            _selectionBorder = new Border
            {
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1.5),
                Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)), // 几乎透明，可命中
                Cursor = Cursors.SizeAll,
                SnapsToDevicePixels = true
            };
            _selectionBorder.MouseLeftButtonDown += SelectionBorder_MouseDown;
            _selectionBorder.MouseMove += SelectionBorder_MouseMove;
            _selectionBorder.MouseLeftButtonUp += SelectionBorder_MouseUp;
            _magHost = new MagnifierHost(this);
            _selectionBorder.Child = _magHost;
            _canvas.Children.Add(_selectionBorder);

            // 8 个控点
            _handles = new Ellipse[8];
            string[] tags = { "NW", "N", "NE", "E", "SE", "S", "SW", "W" };
            Cursor[] cursors = { Cursors.SizeNWSE, Cursors.SizeNS, Cursors.SizeNESW, Cursors.SizeWE,
                                 Cursors.SizeNWSE, Cursors.SizeNS, Cursors.SizeNESW, Cursors.SizeWE };
            for (int i = 0; i < 8; i++)
            {
                var h = new Ellipse
                {
                    Width = 12,
                    Height = 12,
                    Fill = Brushes.White,
                    Stroke = new SolidColorBrush(Color.FromArgb(220, 0, 0, 0)),
                    StrokeThickness = 1,
                    Cursor = cursors[i],
                    Tag = tags[i]
                };
                h.MouseLeftButtonDown += Handle_MouseDown;
                h.MouseMove += Handle_MouseMove;
                h.MouseLeftButtonUp += Handle_MouseUp;
                _handles[i] = h;
                _canvas.Children.Add(h);
            }

            _toolbar = BuildToolbar();
            _canvas.Children.Add(_toolbar);

            Content = _canvas;

            Loaded += (s, e) =>
            {
                LayoutAll();
            };
            SizeChanged += (s, e) => LayoutAll();
            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
        }

        private System.Windows.Controls.Border BuildToolbar()
        {
            var bar = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush(Color.FromArgb(245, 26, 26, 26)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 4, 8, 4),
                SnapsToDevicePixels = true
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };

            _blackoutButton = new ToggleButton { Content = MakeBtnContent("💡", "关灯") };
            StyleToolButton(_blackoutButton);
            _blackoutButton.Checked += (s, e) => SetBlackout(true);
            _blackoutButton.Unchecked += (s, e) => SetBlackout(false);

            var closeBtn = new System.Windows.Controls.Button { Content = MakeBtnContent("✕", "关闭") };
            StyleToolButton(closeBtn);
            closeBtn.Click += (s, e) => Close();

            sp.Children.Add(_blackoutButton);
            sp.Children.Add(closeBtn);
            bar.Child = sp;
            return bar;
        }

        private static FrameworkElement MakeBtnContent(string icon, string text)
        {
            var sp = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = icon, FontSize = 16, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = text, FontSize = 11, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 0) });
            return sp;
        }

        private static void StyleToolButton(ButtonBase b)
        {
            b.Width = 56;
            b.Height = 46;
            b.Margin = new Thickness(3, 0, 3, 0);
            b.Background = Brushes.Transparent;
            b.BorderThickness = new Thickness(0);
            b.Foreground = Brushes.White;
            b.Cursor = Cursors.Hand;
            b.Padding = new Thickness(0);
            b.Template = BuildToolButtonTemplate();
        }

        private static ControlTemplate BuildToolButtonTemplate()
        {
            var t = new ControlTemplate(typeof(ButtonBase));
            var border = new FrameworkElementFactory(typeof(System.Windows.Controls.Border));
            border.Name = "Bd";
            border.SetValue(System.Windows.Controls.Border.BackgroundProperty, new TemplateBindingExtension(System.Windows.Controls.Control.BackgroundProperty));
            border.SetValue(System.Windows.Controls.Border.CornerRadiusProperty, new CornerRadius(6));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(cp);
            t.VisualTree = border;

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(System.Windows.Controls.Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), "Bd"));
            t.Triggers.Add(hover);

            var checkedT = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
            checkedT.Setters.Add(new Setter(System.Windows.Controls.Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69)), "Bd"));
            t.Triggers.Add(checkedT);
            return t;
        }

        #region 布局

        private void LayoutAll()
        {
            double w = _canvas.ActualWidth;
            double h = _canvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            // 首次居中
            if (_boxLeft + _boxWidth > w) _boxLeft = Math.Max(0, w - _boxWidth);
            if (_boxTop + _boxHeight > h) _boxTop = Math.Max(0, h - _boxHeight);

            // 遮罩占满
            _overlay.Width = w;
            _overlay.Height = h;
            System.Windows.Controls.Canvas.SetLeft(_overlay, 0);
            System.Windows.Controls.Canvas.SetTop(_overlay, 0);
            UpdateOverlayClip();

            // 选择框
            _selectionBorder.Width = _boxWidth;
            _selectionBorder.Height = _boxHeight;
            System.Windows.Controls.Canvas.SetLeft(_selectionBorder, _boxLeft);
            System.Windows.Controls.Canvas.SetTop(_selectionBorder, _boxTop);

            // 8 个控点（中心定位）
            PositionHandle(0, _boxLeft, _boxTop);                              // NW
            PositionHandle(1, _boxLeft + _boxWidth / 2, _boxTop);              // N
            PositionHandle(2, _boxLeft + _boxWidth, _boxTop);                  // NE
            PositionHandle(3, _boxLeft + _boxWidth, _boxTop + _boxHeight / 2); // E
            PositionHandle(4, _boxLeft + _boxWidth, _boxTop + _boxHeight);    // SE
            PositionHandle(5, _boxLeft + _boxWidth / 2, _boxTop + _boxHeight); // S
            PositionHandle(6, _boxLeft, _boxTop + _boxHeight);                 // SW
            PositionHandle(7, _boxLeft, _boxTop + _boxHeight / 2);             // W

            // 工具栏：选择框正下方居中；若超出则放上方
            _toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double tw = _toolbar.DesiredSize.Width, th = _toolbar.DesiredSize.Height;
            double tx = _boxLeft + _boxWidth / 2 - tw / 2;
            double ty = _boxTop + _boxHeight + 12;
            if (ty + th > h) ty = _boxTop - th - 12;
            tx = Math.Max(8, Math.Min(w - tw - 8, tx));
            System.Windows.Controls.Canvas.SetLeft(_toolbar, tx);
            System.Windows.Controls.Canvas.SetTop(_toolbar, ty);
        }

        private void PositionHandle(int i, double cx, double cy)
        {
            var dot = _handles[i];
            System.Windows.Controls.Canvas.SetLeft(dot, cx - dot.Width / 2);
            System.Windows.Controls.Canvas.SetTop(dot, cy - dot.Height / 2);
        }

        private void UpdateOverlayClip()
        {
            // 用 EvenOdd Geometry 在 overlay 上挖空选择框区域
            double w = _canvas.ActualWidth;
            double h = _canvas.ActualHeight;
            if (w <= 0 || h <= 0) { _overlay.Clip = null; return; }

            var outer = new RectangleGeometry(new Rect(0, 0, w, h));
            var inner = new RectangleGeometry(new Rect(_boxLeft, _boxTop, _boxWidth, _boxHeight));
            var combined = new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
            _overlay.Clip = combined;
        }

        #endregion

        #region 拖动

        private bool _draggingBox;
        private Point _dragStart;
        private double _dragBoxLeft, _dragBoxTop;

        private void SelectionBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _draggingBox = true;
            _dragStart = e.GetPosition(_canvas);
            _dragBoxLeft = _boxLeft;
            _dragBoxTop = _boxTop;
            _selectionBorder.CaptureMouse();
            e.Handled = true;
        }

        private void SelectionBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_draggingBox) return;
            var p = e.GetPosition(_canvas);
            _boxLeft = Math.Max(0, Math.Min(_canvas.ActualWidth - _boxWidth, _dragBoxLeft + p.X - _dragStart.X));
            _boxTop = Math.Max(0, Math.Min(_canvas.ActualHeight - _boxHeight, _dragBoxTop + p.Y - _dragStart.Y));
            LayoutAll();
        }

        private void SelectionBorder_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_draggingBox) return;
            _draggingBox = false;
            _selectionBorder.ReleaseMouseCapture();
        }

        #endregion

        #region 缩放

        private bool _resizing;
        private string _resizeTag;
        private Point _resizeStart;
        private double _rL, _rT, _rW, _rH;

        private void Handle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var el = (FrameworkElement)sender;
            _resizing = true;
            _resizeTag = (string)el.Tag;
            _resizeStart = e.GetPosition(_canvas);
            _rL = _boxLeft; _rT = _boxTop; _rW = _boxWidth; _rH = _boxHeight;
            el.CaptureMouse();
            e.Handled = true;
        }

        private void Handle_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_resizing) return;
            var p = e.GetPosition(_canvas);
            double dx = p.X - _resizeStart.X;
            double dy = p.Y - _resizeStart.Y;

            double nL = _rL, nT = _rT, nW = _rW, nH = _rH;
            switch (_resizeTag)
            {
                case "NW": nL = _rL + dx; nT = _rT + dy; nW = _rW - dx; nH = _rH - dy; break;
                case "N": nT = _rT + dy; nH = _rH - dy; break;
                case "NE": nT = _rT + dy; nW = _rW + dx; nH = _rH - dy; break;
                case "E": nW = _rW + dx; break;
                case "SE": nW = _rW + dx; nH = _rH + dy; break;
                case "S": nH = _rH + dy; break;
                case "SW": nL = _rL + dx; nW = _rW - dx; nH = _rH + dy; break;
                case "W": nL = _rL + dx; nW = _rW - dx; break;
            }
            if (nW < MinBoxW) { if (_resizeTag.Contains("W")) nL -= MinBoxW - nW; nW = MinBoxW; }
            if (nH < MinBoxH) { if (_resizeTag.Contains("N")) nT -= MinBoxH - nH; nH = MinBoxH; }

            // 限制在窗口内
            nL = Math.Max(0, nL);
            nT = Math.Max(0, nT);
            if (nL + nW > _canvas.ActualWidth) nW = _canvas.ActualWidth - nL;
            if (nT + nH > _canvas.ActualHeight) nH = _canvas.ActualHeight - nT;

            _boxLeft = nL; _boxTop = nT; _boxWidth = nW; _boxHeight = nH;
            LayoutAll();
        }

        private void Handle_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_resizing) return;
            _resizing = false;
            ((FrameworkElement)sender).ReleaseMouseCapture();
        }

        #endregion

        #region 关灯

        private void SetBlackout(bool on)
        {
            _blackoutOn = on;
            _overlay.Fill = new SolidColorBrush(on
                ? Color.FromArgb(245, 0, 0, 0)
                : Color.FromArgb(102, 0, 0, 0));
        }

        #endregion

        #region Magnifier 子窗口

        private class MagnifierHost : HwndHost
        {
            private readonly MagnifierWindow _owner;
            public IntPtr MagHwnd { get; private set; }
            public MagnifierHost(MagnifierWindow owner) { _owner = owner; }

            protected override HandleRef BuildWindowCore(HandleRef hwndParent)
            {
                if (!MagInitialize()) throw new InvalidOperationException("MagInitialize 失败");
                _owner._magInitialized = true;

                MagHwnd = CreateWindowEx(
                    0, MagnifierClassName, "ICCMagnifier",
                    WS_CHILD | WS_VISIBLE,
                    0, 0, 100, 100,
                    hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                _owner._magHwnd = MagHwnd;
                return new HandleRef(this, MagHwnd);
            }

            protected override void DestroyWindowCore(HandleRef hwnd)
            {
                if (hwnd.Handle != IntPtr.Zero) DestroyWindow(hwnd.Handle);
                MagHwnd = IntPtr.Zero;
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            ApplyTransform();
            if (_hwndSource != null && _magHwnd != IntPtr.Zero)
                MagSetWindowFilterList(_magHwnd, MW_FILTERMODE_EXCLUDE, 1, new[] { _hwndSource.Handle });

            _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
            _timer.Tick += OnTick;
            _timer.Start();
        }

        private void ApplyTransform()
        {
            if (_magHwnd == IntPtr.Zero) return;
            var m = new MAGTRANSFORM
            {
                m00 = _zoom,
                m01 = 0,
                m02 = 0,
                m10 = 0,
                m11 = _zoom,
                m12 = 0,
                m20 = 0,
                m21 = 0,
                m22 = 1.0f
            };
            MagSetWindowTransform(_magHwnd, ref m);
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_magHwnd == IntPtr.Zero || _magHost == null) return;
            if (_magHost.RenderSize.Width <= 0) return;

            // 选择框在屏幕物理像素坐标
            var hostOffset = _magHost.TransformToAncestor(this).Transform(new Point(0, 0));
            var tl = PointToScreen(hostOffset);
            var br = PointToScreen(new Point(hostOffset.X + _magHost.RenderSize.Width,
                                             hostOffset.Y + _magHost.RenderSize.Height));
            int viewW = (int)(br.X - tl.X);
            int viewH = (int)(br.Y - tl.Y);
            if (viewW <= 0 || viewH <= 0) return;

            int srcW = Math.Max(1, (int)(viewW / _zoom));
            int srcH = Math.Max(1, (int)(viewH / _zoom));
            int cx = (int)((tl.X + br.X) / 2);
            int cy = (int)((tl.Y + br.Y) / 2);
            var src = new RECT(cx - srcW / 2, cy - srcH / 2,
                               cx - srcW / 2 + srcW, cy - srcH / 2 + srcH);
            MagSetWindowSource(_magHwnd, src);
            InvalidateRect(_magHwnd, IntPtr.Zero, false);
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            if (_timer != null) { _timer.Stop(); _timer.Tick -= OnTick; _timer = null; }
            if (_magInitialized) { MagUninitialize(); _magInitialized = false; }
            base.OnClosed(e);
        }
    }
}