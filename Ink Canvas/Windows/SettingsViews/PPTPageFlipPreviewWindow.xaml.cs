using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Ink_Canvas.Windows.SettingsViews
{
    /// <summary>
    /// 全屏预览的背景窗口：仅承载 PPT 背景图，位于 SettingsWindow 之下、MainWindow 之上。
    /// 4 个翻页按钮已拆分到 <see cref="PPTPageFlipPreviewOverlayWindow"/>（顶层，浮在 SettingsWindow 之上）。
    /// </summary>
    public partial class PPTPageFlipPreviewWindow : Window
    {
        public static PPTPageFlipPreviewWindow ActiveInstance { get; private set; }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        public PPTPageFlipPreviewWindow()
        {
            InitializeComponent();
            ActiveInstance = this;

            Closed += PPTPageFlipPreviewWindow_Closed;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);

                // 精确定位到主屏幕整个边界（与 MainWindow 一致，避免 Maximized 导致的几像素溢出）
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                Left = screen.Bounds.X;
                Top = screen.Bounds.Y;
                Width = screen.Bounds.Width;
                Height = screen.Bounds.Height;
                MoveWindow(hwnd, screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set window styles: {ex}");
            }
        }

        private void PPTPageFlipPreviewWindow_Closed(object sender, EventArgs e)
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }
        }

        /// <summary>
        /// 刷新预览：4 个翻页按钮的状态更新已委托给顶层 Overlay 窗口。
        /// </summary>
        public void UpdatePreview()
        {
            PPTPageFlipPreviewOverlayWindow.ActiveInstance?.UpdatePreview();
        }
    }
}
