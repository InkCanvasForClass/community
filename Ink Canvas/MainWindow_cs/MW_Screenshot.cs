using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using Ink_Canvas.Helpers;
using Clipboard = System.Windows.Clipboard;

namespace Ink_Canvas {
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

                var rc = SystemInformation.VirtualScreen;
                using (var bitmap = new Bitmap(rc.Width, rc.Height, PixelFormat.Format32bppArgb))
                using (var memoryGraphics = Graphics.FromImage(bitmap)) {
                    memoryGraphics.CopyFromScreen(rc.X, rc.Y, 0, 0, rc.Size, CopyPixelOperation.SourceCopy);

                    // 将截图复制到剪贴板
                    CopyBitmapToClipboard(bitmap);

                    // 恢复窗口显示
                    this.Visibility = originalVisibility;

                    // 等待窗口完全显示后自动粘贴
                    await Task.Delay(100);
                    await AutoPasteScreenshot();
                }
            }
            catch (Exception ex) {
                ShowNotification($"截图失败: {ex.Message}");
                this.Visibility = Visibility.Visible;
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
