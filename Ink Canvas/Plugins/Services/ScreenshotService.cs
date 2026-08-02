using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="IScreenshotService"/> 的宿主实现：包装 MainWindow 的截图核心
    /// （<see cref="MainWindow.CapturePluginFullScreen"/> / <see cref="MainWindow.CapturePluginScreenArea"/>），
    /// 把 System.Drawing.Bitmap 转换为 WPF <see cref="BitmapSource"/>。
    /// </summary>
    internal sealed class ScreenshotService : IScreenshotService
    {
        private readonly MainWindow _mainWindow;

        public ScreenshotService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public BitmapSource CaptureFullScreen()
        {
            try
            {
                using (var bitmap = _mainWindow.CapturePluginFullScreen())
                {
                    return bitmap == null ? null : BitmapToSource(bitmap);
                }
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"ScreenshotService.CaptureFullScreen failed: {ex.Message}", Helpers.LogHelper.LogType.Error);
                return null;
            }
        }

        public BitmapSource CaptureScreenArea(int x, int y, int width, int height)
        {
            try
            {
                if (width <= 0 || height <= 0) return null;
                using (var bitmap = _mainWindow.CapturePluginScreenArea(new Rectangle(x, y, width, height)))
                {
                    return bitmap == null ? null : BitmapToSource(bitmap);
                }
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"ScreenshotService.CaptureScreenArea failed: {ex.Message}", Helpers.LogHelper.LogType.Error);
                return null;
            }
        }

        public bool SaveFullScreenToFile(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath)) return false;
                using (var bitmap = _mainWindow.CapturePluginFullScreen())
                {
                    if (bitmap == null) return false;
                    bitmap.Save(filePath, ImageFormat.Png);
                }
                return true;
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"ScreenshotService.SaveFullScreenToFile failed: {ex.Message}", Helpers.LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 把 GDI+ 位图转换为 WPF BitmapSource 并 Freeze。调用方负责 Dispose 传入位图。
        /// </summary>
        private static BitmapSource BitmapToSource(Bitmap bitmap)
        {
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero,
                    Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
