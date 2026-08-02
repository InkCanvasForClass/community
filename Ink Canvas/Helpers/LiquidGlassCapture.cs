using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 液态玻璃浮动栏的背景来源：用 GDI BitBlt 抓取整个虚拟桌面，缓存为冻结的 <see cref="BitmapSource"/>。
    /// 浮动栏只需按自身屏幕区域裁剪这张缓存图，移动时无需重新截屏。
    /// </summary>
    internal static class LiquidGlassCapture
    {
        private const int SrcCopy = 0x00CC0020;
        private const int CaptureBlt = 0x40000000;
        private const int SmXVirtualScreen = 76;
        private const int SmYVirtualScreen = 77;
        private const int SmCxVirtualScreen = 78;
        private const int SmCyVirtualScreen = 79;

        /// <summary>最近一次抓取到的整屏快照（已 Freeze，可跨线程读取）。</summary>
        internal static BitmapSource Snapshot { get; private set; }

        internal static int VirtualScreenX { get; private set; }
        internal static int VirtualScreenY { get; private set; }

        /// <summary>抓取整个虚拟桌面。失败时保留上一帧，不会把 <see cref="Snapshot"/> 置空。</summary>
        internal static bool Capture()
        {
            try
            {
                VirtualScreenX = GetSystemMetrics(SmXVirtualScreen);
                VirtualScreenY = GetSystemMetrics(SmYVirtualScreen);
                int width = GetSystemMetrics(SmCxVirtualScreen);
                int height = GetSystemMetrics(SmCyVirtualScreen);
                if (width <= 0 || height <= 0) return false;

                var bitmap = CaptureRegion(VirtualScreenX, VirtualScreenY, width, height);
                if (bitmap == null) return false;

                Snapshot = bitmap;
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"液态玻璃背景抓取失败: {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
        }

        internal static void Reset()
        {
            Snapshot = null;
        }

        private static BitmapSource CaptureRegion(int x, int y, int width, int height)
        {
            IntPtr screenDc = IntPtr.Zero;
            IntPtr memDc = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                screenDc = GetDC(IntPtr.Zero);
                if (screenDc == IntPtr.Zero) return null;

                memDc = CreateCompatibleDC(screenDc);
                if (memDc == IntPtr.Zero) return null;

                hBitmap = CreateCompatibleBitmap(screenDc, width, height);
                if (hBitmap == IntPtr.Zero) return null;

                oldBitmap = SelectObject(memDc, hBitmap);
                // CAPTUREBLT 让分层窗口也进入截图，避免玻璃背景缺块
                if (!BitBlt(memDc, 0, 0, width, height, screenDc, x, y, SrcCopy | CaptureBlt))
                    BitBlt(memDc, 0, 0, width, height, screenDc, x, y, SrcCopy);

                var bitmap = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(width, height));
                bitmap.Freeze();
                return bitmap;
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero && memDc != IntPtr.Zero) SelectObject(memDc, oldBitmap);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                if (memDc != IntPtr.Zero) DeleteDC(memDc);
                if (screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height,
            IntPtr hdcSrc, int xSrc, int ySrc, int rop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
    }
}
