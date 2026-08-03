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
    /// 抓屏在后台线程执行（见 LiquidGlassBarWindow.CaptureBehindSelfAsync），完成时把整个
    /// <see cref="ScreenSnapshot"/> 帧原子交换到 <see cref="Snapshot"/>——UI 线程总是读到
    /// "位图 + 虚拟屏原点"一致的完整一帧（BackBuffer 语义），旧帧在抓屏期间仍可继续使用。
    /// </summary>
    internal static class LiquidGlassCapture
    {
        private const int SrcCopy = 0x00CC0020;
        private const int SmXVirtualScreen = 76;
        private const int SmYVirtualScreen = 77;
        private const int SmCxVirtualScreen = 78;
        private const int SmCyVirtualScreen = 79;

        private static readonly object SyncLock = new object();
        private static bool _capturing;

        /// <summary>最近一次抓取到的完整帧（冻结位图 + 该帧的虚拟屏原点）。原子引用交换，可跨线程读。</summary>
        internal static ScreenSnapshot Snapshot { get; private set; }

        /// <summary>抓取整个虚拟桌面。失败时保留上一帧，不会把 <see cref="Snapshot"/> 置空。</summary>
        internal static bool Capture()
        {
            lock (SyncLock)
            {
                if (_capturing) return false;   // 防重入：上一次抓屏仍在进行时跳过本次
                _capturing = true;
            }

            try
            {
                int vx = GetSystemMetrics(SmXVirtualScreen);
                int vy = GetSystemMetrics(SmYVirtualScreen);
                int width = GetSystemMetrics(SmCxVirtualScreen);
                int height = GetSystemMetrics(SmCyVirtualScreen);
                if (width <= 0 || height <= 0) return false;

                var bitmap = CaptureRegion(vx, vy, width, height);
                if (bitmap == null) return false;

                // 把位图与坐标包进同一帧，一起交换：读侧不会拿到"新截图 + 旧原点"的错配。
                PublishSnapshot(new ScreenSnapshot(bitmap, vx, vy));
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"液态玻璃背景抓取失败: {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
            finally
            {
                lock (SyncLock)
                {
                    _capturing = false;
                }
            }
        }

        /// <summary>
        /// 把外部抓到的帧（例如 Magnification API 路径）原子交换到 <see cref="Snapshot"/>。
        /// 与 <see cref="Capture"/> 共用同一把锁，保证读侧永远看到完整帧。
        /// </summary>
        internal static void PublishSnapshot(ScreenSnapshot frame)
        {
            if (frame == null) return;
            lock (SyncLock)
            {
                Snapshot = frame;
            }
        }

        internal static void Reset()
        {
            lock (SyncLock)
            {
                Snapshot = null;
            }
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
                // 必须用纯 SRCCOPY，不能用 CAPTUREBLT：CAPTUREBLT 会把分层窗口也抓进截图，
                // 而浮动栏自己就是 AllowsTransparency=True 的分层窗口 → 玻璃背景里出现本体重影。
                // 纯 SRCCOPY 天然排除分层窗口，浮动栏不入镜，无需隐藏也不依赖 WDA 排除的可靠性。
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

    /// <summary>
    /// 一帧完整的桌面快照：冻结位图 + 该帧抓取时的虚拟屏原点。整个对象原子交换，
    /// 保证读侧拿到的位图与坐标来自同一次抓取（BackBuffer 语义下的完整帧）。
    /// </summary>
    internal sealed class ScreenSnapshot
    {
        internal BitmapSource Bitmap { get; }
        internal int VirtualScreenX { get; }
        internal int VirtualScreenY { get; }

        internal ScreenSnapshot(BitmapSource bitmap, int virtualScreenX, int virtualScreenY)
        {
            Bitmap = bitmap;
            VirtualScreenX = virtualScreenX;
            VirtualScreenY = virtualScreenY;
        }
    }
}
