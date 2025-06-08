using System;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
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
                
                var directory = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory);
                }

                try {
                    // 新增双重目录检查
                    Directory.CreateDirectory(directory); // 防止多线程场景下的竞争条件
                    bitmap.Save(savePath, ImageFormat.Png);
                }
                catch (Exception ex) when (ex is IOException || 
                                         ex is UnauthorizedAccessException || 
                                         ex is ExternalException) { // 新增GDI+异常捕获
                    // 改进备用路径处理
                    var docPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "Auto Saved - Screenshots",
                        DateTime.Now.ToString("yyyyMMdd"),
                        Path.GetFileNameWithoutExtension(savePath) + "_retry.png"); // 添加重试后缀

                    try {
                        var docDir = Path.GetDirectoryName(docPath);
                        Directory.CreateDirectory(docDir);
                        bitmap.Save(docPath, ImageFormat.Png);
                        savePath = docPath;
                    }
                    catch (Exception fallbackEx) {
                        // 最终错误处理
                        if (!isHideNotification) {
                            ShowNotification($"截图保存失败: {fallbackEx.Message}");
                        }
                        return;
                    }
                }
            }
            
            if (!isHideNotification) {
                try {
                    ShowNotification($"截图成功保存至 {savePath}");
                }
                catch {
                    // 防止通知系统自身异常导致崩溃
                }
            }
        }

        // 获取日期文件夹路径
        private string GetDateFolderPath(string fileName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                fileName = DateTime.Now.ToString("HH-mm-ss");
            }
            
            var basePath = Settings.Automation.AutoSavedStrokesLocation;
            var dateFolder = DateTime.Now.ToString("yyyyMMdd");
            var fullPath = Path.Combine(
                basePath, 
                "Auto Saved - Screenshots", 
                dateFolder);

            try {
                if (!Directory.Exists(fullPath)) {
                    Directory.CreateDirectory(fullPath);
                }
            }
            catch (Exception) {
                // 如果创建失败则使用文档目录
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                fullPath = Path.Combine(basePath, "Auto Saved - Screenshots", dateFolder);
                Directory.CreateDirectory(fullPath);
            }
            
            return Path.Combine(fullPath, $"{fileName}.png");
        }

        private string GetDefaultFolderPath() {
            var basePath = Settings.Automation.AutoSavedStrokesLocation;
            var screenshotsFolder = Path.Combine(basePath, "Auto Saved - Screenshots");

            try {
                if (!Directory.Exists(screenshotsFolder)) {
                    Directory.CreateDirectory(screenshotsFolder);
                }
            }
            catch (Exception) {
                // 如果创建失败则使用文档目录
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                screenshotsFolder = Path.Combine(basePath, "Auto Saved - Screenshots");
                Directory.CreateDirectory(screenshotsFolder);
            }
            
            return Path.Combine(
                screenshotsFolder, 
                $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
        }
    }
}