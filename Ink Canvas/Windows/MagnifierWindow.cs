using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas.Windows
{
    /// <summary>
    /// 可拖动、可调整大小的屏幕放大镜窗口。
    /// 底层使用 Win32 Magnification API (Magnification.dll) 将窗口当前所在区域下方的屏幕内容放大显示。
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
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("Magnification.dll")] private static extern bool MagInitialize();
        [DllImport("Magnification.dll")] private static extern bool MagUninitialize();
        [DllImport("Magnification.dll")] private static extern bool MagSetWindowSource(IntPtr hwnd, RECT rect);
        [DllImport("Magnification.dll")] private static extern bool MagSetWindowTransform(IntPtr hwnd, ref MAGTRANSFORM pTransform);
        [DllImport("Magnification.dll")] private static extern bool MagSetWindowFilterList(IntPtr hwnd, int dwFilterMode, int count, IntPtr[] pHWND);
        private const int MW_FILTERMODE_EXCLUDE = 0;

        #endregion

        private static MagnifierWindow _instance;
        public static bool HasInstance => _instance != null;

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

        public static void Show(float zoom)
        {
            if (_instance != null)
            {
                _instance.Activate();
                _instance.Zoom = zoom;
                return;
            }
            _instance = new MagnifierWindow { Zoom = zoom };
            _instance.Closed += (s, e) => _instance = null;
            _instance.Show();
        }

        public static void HideInstance()
        {
            _instance?.Close();
        }

        public static void SetZoom(float zoom)
        {
            if (_instance != null) _instance.Zoom = zoom;
        }

        private MagnifierWindow()
        {
            Title = "聚焦放大镜";
            Width = 420;
            Height = 280;
            MinWidth = 160;
            MinHeight = 120;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Topmost = true;
            ShowInTaskbar = false;
            AllowsTransparency = false;
            Background = new SolidColorBrush(Color.FromRgb(31, 31, 31));
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            BuildChrome();
        }

        private void BuildChrome()
        {
            var root = new DockPanel { LastChildFill = true };

            // 标题栏
            var titleBar = new Border
            {
                Height = 28,
                Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x2A, 0x2A, 0x2A)),
                Cursor = Cursors.SizeAll
            };
            DockPanel.SetDock(titleBar, Dock.Top);
            titleBar.MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };

            var titleGrid = new Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleText = new TextBlock
            {
                Text = "聚焦放大镜",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                Foreground = Brushes.White,
                FontSize = 12
            };
            Grid.SetColumn(titleText, 0);
            titleGrid.Children.Add(titleText);

            var closeBtn = new Button
            {
                Content = "✕",
                Width = 40,
                Height = 28,
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 12,
                Cursor = Cursors.Arrow
            };
            closeBtn.Click += (s, e) => Close();
            Grid.SetColumn(closeBtn, 1);
            titleGrid.Children.Add(closeBtn);

            titleBar.Child = titleGrid;
            root.Children.Add(titleBar);

            // 放大镜承载区（由 Win32 子窗口填充）
            _magHost = new MagnifierHost(this);
            root.Children.Add(_magHost);

            Content = root;
        }

        private MagnifierHost _magHost;

        private class MagnifierHost : HwndHost
        {
            private readonly MagnifierWindow _owner;
            public IntPtr MagHwnd { get; private set; }

            public MagnifierHost(MagnifierWindow owner) { _owner = owner; }

            protected override HandleRef BuildWindowCore(HandleRef hwndParent)
            {
                if (!MagInitialize())
                {
                    throw new InvalidOperationException("MagInitialize 失败");
                }
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
                if (hwnd.Handle != IntPtr.Zero)
                {
                    DestroyWindow(hwnd.Handle);
                }
                MagHwnd = IntPtr.Zero;
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwndSource = (HwndSource)PresentationSource.FromVisual(this);

            ApplyTransform();

            // 把本窗口排除在采样源外，避免自我递归
            if (_hwndSource != null && _magHwnd != IntPtr.Zero)
            {
                MagSetWindowFilterList(_magHwnd, MW_FILTERMODE_EXCLUDE, 1, new[] { _hwndSource.Handle });
            }

            _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
            _timer.Tick += OnTick;
            _timer.Start();
        }

        private void ApplyTransform()
        {
            if (_magHwnd == IntPtr.Zero) return;
            var m = new MAGTRANSFORM
            {
                m00 = _zoom, m01 = 0, m02 = 0,
                m10 = 0, m11 = _zoom, m12 = 0,
                m20 = 0, m21 = 0, m22 = 1.0f
            };
            MagSetWindowTransform(_magHwnd, ref m);
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_magHwnd == IntPtr.Zero || _hwndSource == null) return;

            // 当前放大区域 = 放大镜承载控件在屏幕上的位置/尺寸
            var hostSize = _magHost.RenderSize;
            if (hostSize.Width <= 0 || hostSize.Height <= 0) return;

            // 承载控件相对窗口的位置
            var hostOffset = _magHost.TransformToAncestor(this).Transform(new Point(0, 0));
            var screenTopLeft = PointToScreen(hostOffset);
            var screenBottomRight = PointToScreen(new Point(hostOffset.X + hostSize.Width, hostOffset.Y + hostSize.Height));

            int viewW = (int)(screenBottomRight.X - screenTopLeft.X);
            int viewH = (int)(screenBottomRight.Y - screenTopLeft.Y);
            if (viewW <= 0 || viewH <= 0) return;

            int srcW = Math.Max(1, (int)(viewW / _zoom));
            int srcH = Math.Max(1, (int)(viewH / _zoom));
            int srcCx = (int)((screenTopLeft.X + screenBottomRight.X) / 2);
            int srcCy = (int)((screenTopLeft.Y + screenBottomRight.Y) / 2);

            var src = new RECT(srcCx - srcW / 2, srcCy - srcH / 2,
                               srcCx - srcW / 2 + srcW, srcCy - srcH / 2 + srcH);
            MagSetWindowSource(_magHwnd, src);
            InvalidateRect(_magHwnd, IntPtr.Zero, false);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= OnTick;
                _timer = null;
            }
            if (_magInitialized)
            {
                MagUninitialize();
                _magInitialized = false;
            }
            base.OnClosed(e);
        }
    }
}