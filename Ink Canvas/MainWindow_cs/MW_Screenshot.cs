using Ink_Canvas.Helpers;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Ink;
using System.Windows.Media.Imaging;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        private bool _isAreaScreenshotInProgress;

        /// <summary>
        /// 在切页/加页场景下使用：先捕获当前画面到内存并克隆墨迹，然后立即返回；截图与墨迹保存在后台异步执行，不阻塞切页。
        /// 调用方应在调用本方法后立即执行 SaveStrokes、ClearStrokes、切页、RestoreStrokes 等逻辑。
        /// </summary>
        /// <param name="isHideNotification">是否隐藏保存成功通知</param>
        /// <param name="fileName">截图文件名（可选）</param>
        private void CaptureAndEnqueueScreenshotSave(bool isHideNotification, string fileName = null)
        {
            var savePath = Settings.Automation.IsSaveScreenshotsInDateFolders
                ? GetDateFolderPath(fileName)
                : GetDefaultFolderPath();

            System.Drawing.Bitmap bitmap = null;
            StrokeCollection strokesToSave = null;
            int pageIndexForStrokes = 0;
            string strokeSavePath = null;

            try
            {
                bitmap = CaptureScreenshotToBitmap();
                if (bitmap == null) return;

                if (Settings.Automation.IsAutoSaveStrokesAtScreenshot && inkCanvas.Strokes.Count > 0)
                {
                    strokesToSave = inkCanvas.Strokes.Clone();
                    pageIndexForStrokes = CurrentWhiteboardIndex;
                    var basePath = Settings.Automation.AutoSavedStrokesLocation
                        + @"\Auto Saved - BlackBoard Strokes";
                    if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);
                    strokeSavePath = Path.Combine(basePath,
                        $"{DateTime.Now:yyyy-MM-dd HH-mm-ss-fff} Page-{pageIndexForStrokes} StrokesCount-{strokesToSave.Count}.icstk");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"CaptureAndEnqueueScreenshotSave 捕获失败: {ex}", LogHelper.LogType.Error);
                bitmap?.Dispose();
                return;
            }

            var bitmapToSave = bitmap;
            var path = savePath;
            var hideNotification = isHideNotification;

            _ = Task.Run(async () =>
            {
                try
                {
                    if (bitmapToSave != null)
                    {
                        var directory = Path.GetDirectoryName(path);
                        if (!Directory.Exists(directory))
                            Directory.CreateDirectory(directory);
                        bitmapToSave.Save(path, ImageFormat.Png);
                        bitmapToSave.Dispose();
                    }

                    if (strokesToSave != null && !string.IsNullOrEmpty(strokeSavePath))
                    {
                        using (var fs = new FileStream(strokeSavePath, FileMode.Create))
                        {
                            strokesToSave.Save(fs);
                        }
                    }

                    if (!hideNotification && !string.IsNullOrEmpty(path))
                    {
                        Dispatcher.Invoke(() => ShowNotification($"截图成功保存至 {path}"));
                    }

                    // 使用上传帮助类上传到所有启用的服务
                    await Helpers.UploadHelper.UploadFileAsync(path);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"后台保存截图/墨迹失败: {ex}", LogHelper.LogType.Error);
                    bitmapToSave?.Dispose();
                }
            });
        }

        /// <summary>
        /// 将当前屏幕内容捕获为位图（仅内存，不写文件）。调用方或后台任务负责 Dispose。
        /// </summary>
        private System.Drawing.Bitmap CaptureScreenshotToBitmap()
        {
            var rc = SystemInformation.VirtualScreen;
            var bitmap = new System.Drawing.Bitmap(rc.Width, rc.Height, PixelFormat.Format32bppArgb);
            using (var memoryGraphics = Graphics.FromImage(bitmap))
            {
                memoryGraphics.CompositingQuality = CompositingQuality.HighQuality;
                memoryGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                memoryGraphics.SmoothingMode = SmoothingMode.HighQuality;
                memoryGraphics.CompositingMode = CompositingMode.SourceOver;
                memoryGraphics.CopyFromScreen(rc.X, rc.Y, 0, 0, rc.Size, CopyPixelOperation.SourceCopy);
            }
            return bitmap;
        }

        /// <summary>
        /// 保存截图
        /// </summary>
        /// <param name="isHideNotification">是否隐藏通知</param>
        /// <param name="fileName">文件名</param>
        /// <remarks>
        /// 该方法会：
        /// 1. 根据设置确定保存路径
        /// 2. 调用CaptureAndSaveScreenshot方法捕获并保存截图
        /// 3. 如果设置了自动保存墨迹，调用SaveInkCanvasStrokes方法保存墨迹
        /// </remarks>
        private void SaveScreenShot(bool isHideNotification, string fileName = null)
        {
            var savePath = Settings.Automation.IsSaveScreenshotsInDateFolders
                ? GetDateFolderPath(fileName)
                : GetDefaultFolderPath();

            CaptureAndSaveScreenshot(savePath, isHideNotification);

            if (Settings.Automation.IsAutoSaveStrokesAtScreenshot)
                SaveInkCanvasStrokes(false);
        }

        /// <summary>
        /// 保存截图到桌面
        /// </summary>
        /// <remarks>
        /// 该方法会：
        /// 1. 生成桌面路径和文件名
        /// 2. 调用CaptureAndSaveScreenshot方法捕获并保存截图到桌面
        /// 3. 如果设置了自动保存墨迹，调用SaveInkCanvasStrokes方法保存墨迹
        /// </remarks>
        internal void SaveScreenShotToDesktop()
        {
            var desktopPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");

            CaptureAndSaveScreenshot(desktopPath, false);

            if (Settings.Automation.IsAutoSaveStrokesAtScreenshot)
                SaveInkCanvasStrokes(false);
        }

        private struct AnnotationSuspendState
        {
            public bool WasInAnnotationMode;
            public int OriginalMode;
            public System.Windows.Media.Brush OriginalFakeBackground;
            public double OriginalFakeBackgroundOpacity;
            public Visibility OriginalBackgroundCoverHolderVisibility;
            public Visibility OriginalCanvasControlsVisibility;
            public object OriginalHideInkCanvasContent;
        }

        private AnnotationSuspendState SuspendAnnotationForAreaScreenshotIfNeeded()
        {
            var state = new AnnotationSuspendState
            {
                WasInAnnotationMode = GridTransparencyFakeBackground.Background != System.Windows.Media.Brushes.Transparent,
                OriginalMode = currentMode,
                OriginalFakeBackground = GridTransparencyFakeBackground.Background,
                OriginalFakeBackgroundOpacity = GridTransparencyFakeBackground.Opacity,
                OriginalBackgroundCoverHolderVisibility = GridBackgroundCoverHolder.Visibility,
                OriginalCanvasControlsVisibility = StackPanelCanvasControls.Visibility,
                OriginalHideInkCanvasContent = BtnHideInkCanvas.Content
            };

            if (!state.WasInAnnotationMode)
            {
                return state;
            }

            // 仅暂停批注视觉态，避免调用 BtnHideInkCanvas_Click 触发自动截图/上传及白板状态读写。
            GridTransparencyFakeBackground.Opacity = 0;
            GridTransparencyFakeBackground.Background = System.Windows.Media.Brushes.Transparent;
            GridBackgroundCoverHolder.Visibility = Visibility.Collapsed;
            StackPanelCanvasControls.Visibility = Visibility.Collapsed;
            CheckEnableTwoFingerGestureBtnVisibility(false);
            HideSubPanels("cursor");
            BtnHideInkCanvas.Content = "显示\n画板";

            return state;
        }

        private void RestoreAnnotationAfterAreaScreenshot(AnnotationSuspendState state)
        {
            if (!state.WasInAnnotationMode || currentMode != state.OriginalMode)
            {
                return;
            }

            GridTransparencyFakeBackground.Opacity = state.OriginalFakeBackgroundOpacity;
            GridTransparencyFakeBackground.Background = state.OriginalFakeBackground;
            GridBackgroundCoverHolder.Visibility = state.OriginalBackgroundCoverHolderVisibility;
            StackPanelCanvasControls.Visibility = state.OriginalCanvasControlsVisibility;
            CheckEnableTwoFingerGestureBtnVisibility(state.OriginalCanvasControlsVisibility == Visibility.Visible);
            BtnHideInkCanvas.Content = state.OriginalHideInkCanvasContent;
        }

        internal async Task SaveAreaScreenShotToDesktop()
        {
            if (_isAreaScreenshotInProgress)
            {
                ShowNotification("截图进行中，请先完成当前截图");
                return;
            }

            _isAreaScreenshotInProgress = true;

            var annotationState = SuspendAnnotationForAreaScreenshotIfNeeded();
            var originalFloatingBarVisibility = ViewboxFloatingBar.Visibility;
            var shouldRestoreFloatingBarVisibility = true;
            try
            {
                if (annotationState.WasInAnnotationMode)
                {
                    // 等待一次 UI 刷新，确保批注暂停状态已完成。
                    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
                }

                // 从浮动栏触发选区截图时，临时隐藏浮动栏，避免遮挡选区与误入截图。
                if (originalFloatingBarVisibility == Visibility.Visible)
                {
                    ViewboxFloatingBar.Visibility = Visibility.Collapsed;
                    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
                }

                // 选区截图时确保墨迹层可见，避免从浮动栏触发时出现“先隐藏再截图”。
                if (inkCanvas.Visibility != Visibility.Visible)
                {
                    inkCanvas.Visibility = Visibility.Visible;
                }

                // 等待一次 UI 刷新，确保可见性状态已生效。
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);

                var screenshotResult = await ShowScreenshotSelector();

                if (!screenshotResult.HasValue)
                {
                    ShowNotification("截图已取消");
                    return;
                }

                if (screenshotResult.Value.AddToWhiteboard)
                {
                    // 仅在白板接管流程已确认完成时，才跳过本方法对浮动栏可见性的恢复。
                    var whiteboardHandoffCompleted = await AddScreenshotToNewWhiteboardPage(screenshotResult.Value);
                    if (whiteboardHandoffCompleted)
                    {
                        shouldRestoreFloatingBarVisibility = false;
                    }
                    return;
                }

                if (screenshotResult.Value.Area.Width <= 0 || screenshotResult.Value.Area.Height <= 0)
                {
                    ShowNotification("未选择有效截图区域");
                    return;
                }

                var desktopPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");

                using (var originalBitmap = CaptureScreenAreaWithOptionalInk(screenshotResult.Value.Area, screenshotResult.Value.IncludeInk))
                {
                    if (originalBitmap == null)
                    {
                        ShowNotification("截图失败");
                        return;
                    }

                    Bitmap finalBitmap = originalBitmap;
                    bool needDisposeFinalBitmap = false;

                    try
                    {
                        if (screenshotResult.Value.Path != null && screenshotResult.Value.Path.Count > 0)
                        {
                            finalBitmap = ApplyShapeMask(originalBitmap, screenshotResult.Value.Path, screenshotResult.Value.Area);
                            needDisposeFinalBitmap = true;
                        }

                        var directory = Path.GetDirectoryName(desktopPath);
                        if (!Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        finalBitmap.Save(desktopPath, ImageFormat.Png);
                        ShowNotification($"截图成功保存至 {desktopPath}");
                    }
                    finally
                    {
                        if (needDisposeFinalBitmap && finalBitmap != originalBitmap)
                        {
                            finalBitmap.Dispose();
                        }
                    }
                }

                if (Settings.Automation.IsAutoSaveStrokesAtScreenshot)
                    SaveInkCanvasStrokes(false);
            }
            catch (Exception ex)
            {
                ShowNotification($"截图失败: {ex.Message}");
            }
            finally
            {
                _isAreaScreenshotInProgress = false;
                if (shouldRestoreFloatingBarVisibility)
                {
                    ViewboxFloatingBar.Visibility = originalFloatingBarVisibility;
                }
                RestoreAnnotationAfterAreaScreenshot(annotationState);
            }
        }

        private async Task<bool> AddScreenshotToNewWhiteboardPage(ScreenshotResult screenshotResult)
        {
            // 先在当前场景准备截图数据，再进白板，避免误截到白板页面
            BitmapSource bitmapSourceForClipboard = null;

            // 摄像头截图（BitmapSource）
            if (screenshotResult.CameraBitmapSource != null)
            {
                bitmapSourceForClipboard = screenshotResult.CameraBitmapSource;
            }
            // 摄像头截图（Bitmap）
            else if (screenshotResult.CameraImage != null)
            {
                bitmapSourceForClipboard = ConvertBitmapToBitmapSource(screenshotResult.CameraImage);
            }
            else
            {
                if (screenshotResult.Area.Width <= 0 || screenshotResult.Area.Height <= 0)
                {
                    ShowNotification("未选择有效截图区域");
                    return false;
                }

                using (var originalBitmap = CaptureScreenAreaWithOptionalInk(screenshotResult.Area, screenshotResult.IncludeInk))
                {
                    if (originalBitmap == null)
                    {
                        ShowNotification("截图失败");
                        return false;
                    }

                    Bitmap finalBitmap = originalBitmap;
                    bool needDisposeFinalBitmap = false;

                    try
                    {
                        if (screenshotResult.Path != null && screenshotResult.Path.Count > 0)
                        {
                            finalBitmap = ApplyShapeMask(originalBitmap, screenshotResult.Path, screenshotResult.Area);
                            needDisposeFinalBitmap = true;
                        }

                        bitmapSourceForClipboard = ConvertBitmapToBitmapSource(finalBitmap);
                    }
                    finally
                    {
                        if (needDisposeFinalBitmap && finalBitmap != originalBitmap)
                        {
                            finalBitmap.Dispose();
                        }
                    }
                }
            }

            if (bitmapSourceForClipboard == null)
            {
                ShowNotification("截图转换失败");
                return false;
            }

            // 图像已拷贝到内存后再进入白板
            bitmapSourceForClipboard.Freeze();

            if (currentMode != 1)
            {
                SwitchToBoardMode();
                await Task.Delay(150);
            }

            BtnWhiteBoardAdd_Click(null, EventArgs.Empty);

            await InsertBitmapSourceToCanvas(bitmapSourceForClipboard);
            return true;
        }

        /// <summary>
        /// 提取公共的截图和保存逻辑
        /// </summary>
        /// <param name="savePath">保存路径</param>
        /// <param name="isHideNotification">是否隐藏通知</param>
        /// <remarks>
        /// 该方法会：
        /// 1. 获取虚拟屏幕边界
        /// 2. 创建位图并设置高质量渲染
        /// 3. 从屏幕复制内容到位图
        /// 4. 确保保存目录存在
        /// 5. 保存为PNG格式
        /// 6. 如果不隐藏通知，显示保存成功通知
        /// 7. 异步上传截图到Dlass
        /// </remarks>
        private void CaptureAndSaveScreenshot(string savePath, bool isHideNotification)
        {
            var rc = SystemInformation.VirtualScreen;

            using (var bitmap = new Bitmap(rc.Width, rc.Height, PixelFormat.Format32bppArgb))
            using (var memoryGraphics = Graphics.FromImage(bitmap))
            {
                // 设置高质量渲染
                memoryGraphics.CompositingQuality = CompositingQuality.HighQuality;
                memoryGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                memoryGraphics.SmoothingMode = SmoothingMode.HighQuality;
                memoryGraphics.CompositingMode = CompositingMode.SourceOver;

                memoryGraphics.CopyFromScreen(rc.X, rc.Y, 0, 0, rc.Size, CopyPixelOperation.SourceCopy);

                // 确保目录存在
                var directory = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 使用PNG格式保存，确保透明度信息不丢失
                bitmap.Save(savePath, ImageFormat.Png);
            }

            if (!isHideNotification)
            {
                Task.Delay(100).ContinueWith(t =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ShowNotification($"截图成功保存至 {savePath}");
                    });
                });
            }
            _ = Task.Run(async () =>
            {
                try
                {
                    // 使用上传帮助类上传到所有启用的服务
                    await Helpers.UploadHelper.UploadFileAsync(savePath);
                }
                catch (Exception)
                {
                }
            });
        }

        /// <summary>
        /// 获取日期文件夹路径
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <returns>日期文件夹路径</returns>
        /// <remarks>
        /// 该方法会：
        /// 1. 如果文件名为空，使用当前时间作为文件名
        /// 2. 获取基础路径和日期文件夹名
        /// 3. 组合路径并返回
        /// </remarks>
        private string GetDateFolderPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
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

        /// <summary>
        /// 获取默认文件夹路径
        /// </summary>
        /// <returns>默认文件夹路径</returns>
        /// <remarks>
        /// 该方法会：
        /// 1. 获取基础路径
        /// 2. 组合截图文件夹路径
        /// 3. 确保截图文件夹存在
        /// 4. 生成文件名并组合完整路径返回
        /// </remarks>
        private string GetDefaultFolderPath()
        {
            var basePath = Settings.Automation.AutoSavedStrokesLocation;
            var screenshotsFolder = Path.Combine(basePath, "Auto Saved - Screenshots");

            if (!Directory.Exists(screenshotsFolder))
            {
                Directory.CreateDirectory(screenshotsFolder);
            }

            return Path.Combine(
                screenshotsFolder,
                $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
        }
    }
}
