using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using Ink_Canvas.Helpers;
using Ink_Canvas.Helpers;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using Size = System.Drawing.Size;

namespace Ink_Canvas {
    // 截图结果结构体
    public struct ScreenshotResult
    {
        public System.Drawing.Rectangle Area;
        public List<System.Windows.Point> Path;
        
        public ScreenshotResult(System.Drawing.Rectangle area, List<System.Windows.Point> path = null)
        {
            Area = area;
            Path = path;
        }
    }
    
    public partial class MainWindow : Window {
        private void SaveScreenShot(bool isHideNotification, string fileName = null) {
            var savePath = Settings.Automation.IsSaveScreenshotsInDateFolders
                ? GetDateFolderPath(fileName)
                : GetDefaultFolderPath();

            CaptureAndSaveScreenshot(savePath, isHideNotification);
            
            if (Settings.Automation.IsAutoSaveStrokesAtScreenshot) 
                SaveInkCanvasStrokes(false);
        }

        private void SaveScreenShotToDesktop() {
            var desktopPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");

            CaptureAndSaveScreenshot(desktopPath, false);
            
            if (Settings.Automation.IsAutoSaveStrokesAtScreenshot) 
                SaveInkCanvasStrokes(false);
        }

        // 提取公共的截图和保存逻辑
        private void CaptureAndSaveScreenshot(string savePath, bool isHideNotification) {
            var rc = SystemInformation.VirtualScreen;
            
            using (var bitmap = new Bitmap(rc.Width, rc.Height, PixelFormat.Format32bppArgb))
            using (var memoryGraphics = Graphics.FromImage(bitmap)) {
                memoryGraphics.CopyFromScreen(rc.X, rc.Y, 0, 0, rc.Size, CopyPixelOperation.SourceCopy);
                
                // 确保目录存在
                var directory = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory);
                }
                
                bitmap.Save(savePath, ImageFormat.Png);
            }
            
            if (!isHideNotification) {
                ShowNotification($"截图成功保存至 {savePath}");
            }
        }

        // 获取日期文件夹路径
        private string GetDateFolderPath(string fileName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                fileName = DateTime.Now.ToString("HH-mm-ss");
            }
            
            var basePath = Settings.Automation.AutoSavedStrokesLocation;
            var dateFolder = DateTime.Now.ToString("yyyyMMdd");
            
            return Path.Combine(
                basePath, 
                "Auto Saved - Screenshots", 
                dateFolder, 
                $"{fileName}.png");
        }

        // 获取默认文件夹路径
        private string GetDefaultFolderPath() {
            var basePath = Settings.Automation.AutoSavedStrokesLocation;
            var screenshotsFolder = Path.Combine(basePath, "Auto Saved - Screenshots");

            if (!Directory.Exists(screenshotsFolder)) {
                Directory.CreateDirectory(screenshotsFolder);
            }

            return Path.Combine(
                screenshotsFolder,
                $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
        }

        // 截图并复制到剪贴板
        private async Task CaptureScreenshotToClipboard() {
            try {
                // 隐藏主窗口以避免截图包含窗口本身
                var originalVisibility = this.Visibility;
                this.Visibility = Visibility.Hidden;

                // 等待窗口隐藏
                await Task.Delay(200);

                // 启动区域选择截图
                var screenshotResult = await ShowScreenshotSelector();

                // 恢复窗口显示
                this.Visibility = originalVisibility;

                if (screenshotResult.HasValue && screenshotResult.Value.Area.Width > 0 && screenshotResult.Value.Area.Height > 0)
                {
                    // 截取选定区域
                    using (var originalBitmap = CaptureScreenArea(screenshotResult.Value.Area))
                    {
                        if (originalBitmap != null)
                        {
                            Bitmap finalBitmap = originalBitmap;
                            
                            // 如果有路径信息，应用形状遮罩
                            if (screenshotResult.Value.Path != null && screenshotResult.Value.Path.Count > 0)
                            {
                                finalBitmap = ApplyShapeMask(originalBitmap, screenshotResult.Value.Path, screenshotResult.Value.Area);
                            }
                            
                            // 将截图复制到剪贴板
                            CopyBitmapToClipboard(finalBitmap);

                            // 等待窗口完全显示后自动粘贴
                            await Task.Delay(100);
                            await AutoPasteScreenshot();
                        }
                    }
                }
                else
                {
                    ShowNotification("截图已取消");
                }
            }
            catch (Exception ex) {
                ShowNotification($"截图失败: {ex.Message}");
                this.Visibility = Visibility.Visible;
            }
        }

        // 显示截图区域选择器
        private async Task<ScreenshotResult?> ShowScreenshotSelector()
        {
            ScreenshotResult? result = null;

            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var selectorWindow = new ScreenshotSelectorWindow();
                    if (selectorWindow.ShowDialog() == true)
                    {
                        result = new ScreenshotResult(
                            selectorWindow.SelectedArea.Value,
                            selectorWindow.SelectedPath
                        );
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"显示截图选择器失败: {ex.Message}", LogHelper.LogType.Error);
            }

            return result;
        }

        // 截取指定屏幕区域
        private Bitmap CaptureScreenArea(System.Drawing.Rectangle area)
        {
            try
            {
                // 确保区域在有效范围内
                var virtualScreen = SystemInformation.VirtualScreen;

                // 调整区域边界，确保不超出屏幕范围
                int x = Math.Max(area.X, virtualScreen.X);
                int y = Math.Max(area.Y, virtualScreen.Y);
                int right = Math.Min(area.Right, virtualScreen.Right);
                int bottom = Math.Min(area.Bottom, virtualScreen.Bottom);

                int width = Math.Max(1, right - x);
                int height = Math.Max(1, bottom - y);

                var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    // 设置高质量渲染
                    graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                    // 截取屏幕区域
                    graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
                }

                LogHelper.WriteLogToFile($"成功截取区域: X={x}, Y={y}, Width={width}, Height={height}", LogHelper.LogType.Info);
                return bitmap;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"截取屏幕区域失败: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        // 自动粘贴截图到画布
        private async Task AutoPasteScreenshot() {
            try {
                // 只在白板模式下自动粘贴
                if (currentMode == 1) {
                    await PasteImageFromClipboard();
                    ShowNotification("截图已自动插入到画布");
                } else {
                    ShowNotification("截图已复制到剪贴板，可在白板模式下粘贴");
                }
            }
            catch (Exception ex) {
                ShowNotification($"自动粘贴截图失败: {ex.Message}");
                LogHelper.WriteLogToFile($"自动粘贴截图失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 将Bitmap复制到剪贴板
        private void CopyBitmapToClipboard(Bitmap bitmap) {
            try {
                // 将System.Drawing.Bitmap转换为WPF BitmapSource
                var bitmapSource = ConvertBitmapToBitmapSource(bitmap);

                // 复制到剪贴板
                Clipboard.SetImage(bitmapSource);
            }
            catch (Exception ex) {
                ShowNotification($"复制到剪贴板失败: {ex.Message}");
            }
        }

        // 应用形状遮罩到截图
        private Bitmap ApplyShapeMask(Bitmap bitmap, List<System.Windows.Point> path, System.Drawing.Rectangle area)
        {
            try
            {
                // 创建遮罩位图
                using (var maskBitmap = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb))
                using (var maskGraphics = Graphics.FromImage(maskBitmap))
                {
                    // 设置高质量渲染
                    maskGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                    maskGraphics.CompositingQuality = CompositingQuality.HighQuality;

                    // 创建路径
                    using (var pathGraphics = new GraphicsPath())
                    {
                        // 转换WPF坐标到GDI+坐标
                        var points = new PointF[path.Count];
                        for (int i = 0; i < path.Count; i++)
                        {
                            points[i] = new PointF(
                                (float)(path[i].X - area.X),
                                (float)(path[i].Y - area.Y)
                            );
                        }

                        // 添加路径
                        pathGraphics.AddPolygon(points);

                        // 填充路径内部为白色（保留区域）
                        maskGraphics.FillPath(Brushes.White, pathGraphics);
                    }

                    // 创建结果位图
                    var resultBitmap = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
                    using (var resultGraphics = Graphics.FromImage(resultBitmap))
                    {
                        // 设置高质量渲染
                        resultGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                        resultGraphics.CompositingQuality = CompositingQuality.HighQuality;

                        // 使用遮罩合成图像
                        resultGraphics.DrawImage(bitmap, 0, 0);
                        resultGraphics.DrawImage(maskBitmap, 0, 0);
                    }

                    return resultBitmap;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用形状遮罩失败: {ex.Message}", LogHelper.LogType.Error);
                return bitmap; // 如果失败，返回原始图像
            }
        }

        // 将System.Drawing.Bitmap转换为WPF BitmapSource
        private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap) {
            using (var memory = new MemoryStream()) {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
        }
    }
}
