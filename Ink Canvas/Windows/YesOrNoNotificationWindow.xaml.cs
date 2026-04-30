using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Ink_Canvas
{
    /// <summary>
    /// Interaction logic for RestoreHiddenSlidesWindow.xaml
    /// </summary>
    public partial class YesOrNoNotificationWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOOWNERZORDER = 0x0200;

        private readonly Action _yesAction;
        private readonly Action _noAction;
        private readonly Action _windowClose;
        private DispatcherTimer _topmostCheckTimer;
        private IntPtr _hwnd;

        public YesOrNoNotificationWindow(string text, Action yesAction = null, Action noAction = null, Action windowClose = null)
        {
            _yesAction = yesAction;
            _noAction = noAction;
            _windowClose = windowClose;
            InitializeComponent();
            Label.Text = text;

            Loaded += YesOrNoNotificationWindow_Loaded;
            Closed += YesOrNoNotificationWindow_Closed;
        }

        private void YesOrNoNotificationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;

            Topmost = true;
            Activate();
            Focus();

            if (_hwnd != IntPtr.Zero)
            {
                SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);
            }

            _topmostCheckTimer = new DispatcherTimer();
            _topmostCheckTimer.Interval = TimeSpan.FromMilliseconds(100);
            _topmostCheckTimer.Tick += TopmostCheckTimer_Tick;
            _topmostCheckTimer.Start();
        }

        private void TopmostCheckTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_hwnd == IntPtr.Zero) return;

                var foregroundWindow = GetForegroundWindow();

                if (foregroundWindow != _hwnd && IsWindowVisible(_hwnd))
                {
                    Topmost = true;
                    Activate();
                    Focus();

                    SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);
                }
            }
            catch (Exception)
            {
            }
        }

        private void YesOrNoNotificationWindow_Closed(object sender, EventArgs e)
        {
            if (_topmostCheckTimer != null)
            {
                _topmostCheckTimer.Stop();
                _topmostCheckTimer.Tick -= TopmostCheckTimer_Tick;
                _topmostCheckTimer = null;
            }
        }

        private void ButtonYes_Click(object sender, RoutedEventArgs e)
        {
            if (_yesAction == null)
            {
                Close();
                return;
            }

            _yesAction.Invoke();
            Close();

        }

        private void ButtonNo_Click(object sender, RoutedEventArgs e)
        {
            if (_noAction == null)
            {
                Close();
                return;
            }

            _noAction.Invoke();
            Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _windowClose?.Invoke();
        }
    }
}