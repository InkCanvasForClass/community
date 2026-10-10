using Ink_Canvas.Services.Shell;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Ink_Canvas
{
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        /// <summary>
        /// 在切页/加页场景下使用：先捕获当前画面到内存并克隆墨迹，然后立即返回；截图与墨迹保存在后台异步执行，不阻塞切页。
        /// 调用方应在调用本方法后立即执行 SaveStrokes、ClearStrokes、切页、RestoreStrokes 等逻辑。
        /// 壳：本体已搬入 <see cref="ScreenshotService"/>。
        /// </summary>
        /// <param name="isHideNotification">是否隐藏保存成功通知</param>
        /// <param name="fileName">截图文件名（可选）</param>
        private void CaptureAndEnqueueScreenshotSave(bool isHideNotification, string fileName = null)
            => _screenshotService.CaptureAndEnqueueScreenshotSave(isHideNotification, fileName);

        /// <summary>
        /// 供插件截图服务调用的全屏捕获入口（调用方负责 Dispose 返回值）。
        /// 壳：本体已搬入 <see cref="ScreenshotService"/>。
        /// </summary>
        internal System.Drawing.Bitmap CapturePluginFullScreen() => ScreenshotService.CaptureScreenshotToBitmap();

        /// <summary>
        /// 供插件截图服务调用的区域捕获入口（调用方负责 Dispose 返回值）。
        /// 壳：本体已搬入 <see cref="ScreenshotService"/>。
        /// </summary>
        internal System.Drawing.Bitmap CapturePluginScreenArea(Rectangle area) => ScreenshotService.CaptureScreenArea(area);

        /// <summary>
        /// 保存截图
        /// 壳：本体已搬入 <see cref="ScreenshotService"/>。
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
            => _screenshotService.SaveScreenShot(isHideNotification, fileName);

        /// <summary>
        /// 保存截图到配置的保存目录
        /// 壳：本体已搬入 <see cref="ScreenshotService"/>。
        /// </summary>
        /// <remarks>
        /// 该方法会：
        /// 1. 根据截图组件设置生成保存路径和文件名
        /// 2. 调用CaptureAndSaveScreenshot方法捕获并保存截图
        /// 3. 如果设置了截图后复制到剪贴板，将截图复制到剪贴板
        /// 4. 如果设置了自动保存墨迹，调用SaveInkCanvasStrokes方法保存墨迹
        /// </remarks>
        internal void SaveScreenShotToDesktop() => _screenshotService.SaveScreenShotToDesktop();

        internal async Task SaveAreaScreenShotToDesktop()
        {
            var originalVisibility = Visibility;
            try
            {
                var inkOverlayPreview = CreateInkOverlayPreviewBitmapSource();
                Visibility = Visibility.Hidden;
                await Task.Delay(200);

                var screenshotResult = await ShowScreenshotSelector(inkOverlayPreview);

                if (!screenshotResult.HasValue)
                {
                    ShowNotification(Properties.MainWindowStrings.Main_Screenshot_Cancelled);
                    return;
                }

                if (screenshotResult.Value.AddToWhiteboard)
                {
                    await AddScreenshotToNewWhiteboardPage(screenshotResult.Value);
                    return;
                }

                if (screenshotResult.Value.Area.Width <= 0 || screenshotResult.Value.Area.Height <= 0)
                {
                    ShowNotification(Properties.MainWindowStrings.Main_Screenshot_NoValidArea);
                    return;
                }

                var savePath = Path.Combine(
                    _screenshotService.GetScreenshotSaveDirectory(),
                    $"{_screenshotService.GetScreenshotFileNameStem()}.png");

                // 执行段（捕获→叠墨→遮罩→落盘→剪贴板）已搬入 ScreenshotService；
                // 成功/失败通知保持原有相对顺序：落盘（+剪贴板）之后、自动保存墨迹之前。
                if (!_screenshotService.SaveAreaScreenshot(screenshotResult.Value, savePath, Settings.Automation.IsCopyScreenshotToClipboard))
                {
                    ShowNotification(Properties.MainWindowStrings.Main_Screenshot_Failed);
                    return;
                }

                ShowNotification(string.Format(Properties.MainWindowStrings.Main_Screenshot_SaveSuccess, savePath));

                if (Settings.Automation.IsAutoSaveStrokesAtScreenshot)
                    SaveInkCanvasStrokes(false);
            }
            catch (Exception ex)
            {
                ShowNotification(string.Format(Properties.MainWindowStrings.Main_Screenshot_FailedWithError, ex.Message));
            }
            finally
            {
                Visibility = originalVisibility;
            }
        }

        private async Task AddScreenshotToNewWhiteboardPage(ScreenshotResult screenshotResult)
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
                    ShowNotification(Properties.MainWindowStrings.Main_Screenshot_NoValidArea);
                    return;
                }

                using (var originalBitmap = CaptureScreenArea(screenshotResult.Area))
                {
                    if (originalBitmap == null)
                    {
                        ShowNotification(Properties.MainWindowStrings.Main_Screenshot_Failed);
                        return;
                    }

                    Bitmap finalBitmap = originalBitmap;
                    bool needDisposeFinalBitmap = false;

                    try
                    {
                        if (screenshotResult.IncludeInk && screenshotResult.InkOverlayBitmapSource != null)
                        {
                            var withInkBitmap = OverlayInkOnCapturedBitmap(finalBitmap, screenshotResult.Area, screenshotResult.InkOverlayBitmapSource);
                            if (withInkBitmap != null && withInkBitmap != finalBitmap)
                            {
                                if (needDisposeFinalBitmap && finalBitmap != originalBitmap)
                                {
                                    finalBitmap.Dispose();
                                }
                                finalBitmap = withInkBitmap;
                                needDisposeFinalBitmap = true;
                            }
                        }

                        if (screenshotResult.Path != null && screenshotResult.Path.Count > 0)
                        {
                            var maskedBitmap = ApplyShapeMask(finalBitmap, screenshotResult.Path, screenshotResult.Area);
                            if (maskedBitmap != null && maskedBitmap != finalBitmap)
                            {
                                if (needDisposeFinalBitmap && finalBitmap != originalBitmap)
                                {
                                    finalBitmap.Dispose();
                                }
                                finalBitmap = maskedBitmap;
                                needDisposeFinalBitmap = true;
                            }
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
                ShowNotification(Properties.MainWindowStrings.Main_Screenshot_ConvertFailed);
                return;
            }

            // 图像已拷贝到内存后再进入白板
            bitmapSourceForClipboard.Freeze();

            if (currentMode != 1)
            {
                SwitchToBoardMode();
                await Task.Delay(150);
            }

            BtnWhiteBoardAdd_Click(null, new RoutedEventArgs());

            await InsertBitmapSourceToCanvas(bitmapSourceForClipboard);
        }
    }
}
