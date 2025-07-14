using System;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;

namespace Ink_Canvas {
    public partial class MainWindow : Window {
        private void SaveScreenShot(bool isHideNotification, string fileName = null) {
            var savePath = Settings.Automation.IsSaveScreenshotsInDateFolders
                ? GetDateFolderPath(fileName)
                : GetDefaultFolderPath();

            CaptureAndSaveScreenshot(savePath, isHideNotification);
            
            if (Settings.Automation.IsAutoSaveStrokesAtScreenshot) 
                SaveInkCanvasStrokes(false, false);
        }

        private void SaveScreenShotToDesktop() {
            var desktopPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");

            CaptureAndSaveScreenshot(desktopPath, false);
            
            if (Settings.Automation.IsAutoSaveStrokesAtScreenshot) 
                SaveInkCanvasStrokes(false, false);
        }

        // 提取公共的截图和保存逻辑
        private void CaptureAndSaveScreenshot(string savePath, bool isHideNotification) {
            var rc = System.Windows.Forms.SystemInformation.VirtualScreen;
            
            using (var bitmap = new System.Drawing.Bitmap(rc.Width, rc.Height, PixelFormat.Format32bppArgb))
            using (var memoryGraphics = System.Drawing.Graphics.FromImage(bitmap)) {
                memoryGraphics.CopyFromScreen(rc.X, rc.Y, 0, 0, rc.Size, System.Drawing.CopyPixelOperation.SourceCopy);
                
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
    }
}
