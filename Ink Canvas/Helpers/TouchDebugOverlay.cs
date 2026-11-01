using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 右上角触摸调试浮窗：不抢焦点、不吃鼠标，只按固定节拍刷新文本。
    /// 文本由 MainWindow 提供，里面放 HID 触点、手势会话、WPF 捕获状态等。
    /// </summary>
    internal sealed class TouchDebugOverlay : IDisposable
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int HTTRANSPARENT = -1;
        private const int MA_NOACTIVATE = 3;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private readonly Func<string> _textProvider;
        private readonly DispatcherTimer _timer;
        private Window _window;
        private TextBlock _text;
        private Window _owner;
        private bool _disposed;

        public TouchDebugOverlay(Func<string> textProvider)
        {
            _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
            _timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(120),
            };
            _timer.Tick += (s, e) => Update();
        }

        public bool IsVisible => _window?.IsVisible == true;

        public void Show(Window owner)
        {
            if (_disposed) return;
            _owner = owner;
            EnsureWindow(owner);
            if (_window == null) return;
            if (!_window.IsVisible) _window.Show();
            _timer.Start();
            Update();
        }

        public void Hide()
        {
            _timer.Stop();
            if (_window?.IsVisible == true) _window.Hide();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();
            try
            {
                if (_window != null)
                {
                    _window.Close();
                    _window = null;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("[TouchDebugOverlay] 关闭失败: " + ex.Message, LogHelper.LogType.Warning);
            }
        }

        private void EnsureWindow(Window owner)
        {
            if (_window != null) return;

            _text = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                LineHeight = 16,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.NoWrap,
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x00, 0x00, 0x00)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                Child = _text,
            };

            _window = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                ShowActivated = false,
                Focusable = false,
                Topmost = false,
                ResizeMode = ResizeMode.NoResize,
                SizeToContent = SizeToContent.WidthAndHeight,
                MaxWidth = 560,
                Content = border,
            };

            _window.SourceInitialized += (s, e) => MakeClickThrough();
            _window.Loaded += (s, e) => Position();
            if (owner != null && owner.IsLoaded)
            {
                try { _window.Owner = owner; } catch { }
            }
        }

        private void MakeClickThrough()
        {
            IntPtr handle = new WindowInteropHelper(_window).Handle;
            if (handle == IntPtr.Zero) return;

            int exStyle = GetWindowLong(handle, GWL_EXSTYLE);
            SetWindowLong(handle, GWL_EXSTYLE,
                exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

            HwndSource source = HwndSource.FromHwnd(handle);
            source?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCHITTEST)
            {
                handled = true;
                return new IntPtr(HTTRANSPARENT);
            }
            if (msg == WM_MOUSEACTIVATE)
            {
                handled = true;
                return new IntPtr(MA_NOACTIVATE);
            }
            return IntPtr.Zero;
        }

        private void Update()
        {
            if (_window == null || !_window.IsVisible) return;

            string text = _textProvider();
            if (_text != null && !string.Equals(_text.Text, text, StringComparison.Ordinal))
                _text.Text = text;

            Position();
        }

        private void Position()
        {
            if (_window == null) return;
            Window owner = _owner;
            if (owner == null || owner.WindowState == WindowState.Minimized) return;

            double width = _window.ActualWidth > 0 ? _window.ActualWidth : _window.Width;
            double left = owner.Left + owner.ActualWidth - width - 24;
            double top = owner.Top + 24;

            Rect workArea = SystemParameters.WorkArea;
            left = Math.Max(workArea.Left + 8, Math.Min(left, workArea.Right - width - 8));
            top = Math.Max(workArea.Top + 8, Math.Min(top, workArea.Bottom - 48));

            _window.Left = left;
            _window.Top = top;
        }
    }
}
