using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Ink_Canvas.Helpers;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Screen = System.Windows.Forms.Screen;

namespace Ink_Canvas.Windows
{
    public enum StrokeFileOpenTarget { Screen, Whiteboard, Booth }

    public partial class StrokeFileOpenWindow : Window
    {
        public StrokeFileOpenTarget? SelectedTarget { get; private set; }

        public StrokeFileOpenWindow(string filePath)
        {
            InitializeComponent();
            WindowBackdropHelper.Apply(this);
            FileNameText.Text = Path.GetFileName(filePath);
            FileNameText.ToolTip = filePath;
            Loaded += (_, _) => FitToWorkArea(true);
            LocationChanged += (_, _) => FitToWorkArea(false);
            DpiChanged += (_, _) => Dispatcher.BeginInvoke(
                new Action(() => FitToWorkArea(true)), DispatcherPriority.Loaded);
        }

        private void FitToWorkArea(bool constrainPosition)
        {
            if (!IsLoaded || !IsVisible) return;

            var handle = new WindowInteropHelper(this).Handle;
            var workArea = Screen.FromHandle(handle).WorkingArea;
            var dpi = VisualTreeHelper.GetDpi(this);
            const double margin = 16;

            MaxWidth = Math.Max(1, workArea.Width / dpi.DpiScaleX - margin * 2);
            MaxHeight = Math.Max(1, workArea.Height / dpi.DpiScaleY - margin * 2);
            Width = Math.Min(560, MaxWidth);
            if (!constrainPosition) return;

            UpdateLayout();
            // 在物理像素坐标中限制窗口位置，避免混合 DPI 显示器的原点换算偏移。
            var hwnd = new HWND(handle);
            if (!PInvoke.GetWindowRect(hwnd, out var bounds)) return;
            int left = workArea.Left + (int)(margin * dpi.DpiScaleX);
            int top = workArea.Top + (int)(margin * dpi.DpiScaleY);
            int x = Math.Clamp(bounds.left, left, Math.Max(left, workArea.Right - (int)(margin * dpi.DpiScaleX) - bounds.Width));
            int y = Math.Clamp(bounds.top, top, Math.Max(top, workArea.Bottom - (int)(margin * dpi.DpiScaleY) - bounds.Height));
            if (x != bounds.left || y != bounds.top)
                PInvoke.SetWindowPos(hwnd, HWND.Null, x, y, 0, 0,
                    SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        }

        private void Destination_Click(object sender, RoutedEventArgs e)
        {
            SelectedTarget = (StrokeFileOpenTarget)((Button)sender).Tag;
            DialogResult = true;
        }
    }
}
