using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Ink;
using System.Windows.Media.Imaging;
using Color = System.Drawing.Color;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Point = System.Windows.Point;
using Size = System.Drawing.Size;

namespace Ink_Canvas.Services.Shell
{
    /// <summary>
    /// 截图服务（寄生提取）。承载从主窗口截图 partial（MW_Screenshot.cs）与
    /// 插图 partial（MW_ImageInsert.cs）搬出的截图执行段：屏幕捕获（GDI CopyFromScreen）、
    /// 位图加工（墨迹叠加、形状遮罩、格式转换）、编码落盘与剪贴板写入。
    /// 区域选择的交互段（选区窗口、窗口显隐编排、贴图回白板编排）仍留在主窗口壳中。
    /// 禁止引用主窗口类型：设置、墨迹快照、白板页索引、DPI 缩放、通知与 UI 线程调度
    /// 均经 <see cref="Hooks"/> 委托注入（统一收敛为 ISyncService 前的过渡形态）。
    /// </summary>
    internal sealed class ScreenshotService
    {
        /// <summary>
        /// 截图服务的外部依赖委托集合。由主窗口在 <c>CreateScreenshotService</c> 中装配。
        /// </summary>
        internal sealed class Hooks
        {
            /// <summary>读取应用设置（SettingsManager.Settings）。</summary>
            public Func<Ink_Canvas.Settings> GetSettings { get; set; }
            /// <summary>读取当前画布墨迹笔画数（可空，语义同 inkCanvas?.Strokes?.Count）。</summary>
            public Func<int?> GetInkStrokeCount { get; set; }
            /// <summary>克隆当前画布墨迹集合（调用方保证笔画数大于 0）。</summary>
            public Func<StrokeCollection> CloneInkStrokes { get; set; }
            /// <summary>读取当前白板页索引。</summary>
            public Func<int> GetCurrentWhiteboardIndex { get; set; }
            /// <summary>读取当前窗口的 DPI 缩放比例。</summary>
            public Func<double> GetDpiScale { get; set; }
            /// <summary>显示截图保存成功通知（参数为保存路径，资源格式化由主窗口侧完成）。</summary>
            public Action<string> ShowScreenshotSaveSuccess { get; set; }
            /// <summary>在 UI 线程上执行委托（过渡形态，后续收敛为 ISyncService）。</summary>
            public Action<Action> UiInvoke { get; set; }
            /// <summary>截图后自动保存墨迹（对应主窗口 SaveInkCanvasStrokes(false)）。</summary>
            public Action SaveInkStrokesAtScreenshot { get; set; }
        }

        private readonly Hooks _hooks;

        public ScreenshotService(Hooks hooks)
        {
            _hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
        }

        /// <summary>
        /// 在切页/加页场景下使用：先捕获当前画面到内存并克隆墨迹，然后立即返回；截图与墨迹保存在后台异步执行，不阻塞切页。
        /// 调用方应在调用本方法后立即执行 SaveStrokes、ClearStrokes、切页、RestoreStrokes 等逻辑。
        /// </summary>
        /// <param name="isHideNotification">是否隐藏保存成功通知</param>
        /// <param name="fileName">截图文件名（可选）</param>
        internal void CaptureAndEnqueueScreenshotSave(bool isHideNotification, string fileName = null)
        {
            var savePath = _hooks.GetSettings().Automation.IsSaveScreenshotsInDateFolders
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

                if (_hooks.GetSettings().Automation.IsAutoSaveStrokesAtScreenshot && _hooks.GetInkStrokeCount() > 0)
                {
                    strokesToSave = _hooks.CloneInkStrokes();
                    pageIndexForStrokes = _hooks.GetCurrentWhiteboardIndex();
                    var basePath = _hooks.GetSettings().Automation.AutoSavedStrokesLocation
                        + @"\Auto Saved - BlackBoard Strokes";
                    if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);
                    string stem;
                    if (_hooks.GetSettings().Automation.IsUseCustomSaveFileName)
                    {
                        stem = SaveFileNameHelper.Render(_hooks.GetSettings().Automation.CustomSaveFileNameTemplate,
                            new SaveFileNameContext
                            {
                                Mode = "BlackBoard",
                                Type = "Auto",
                                Page = pageIndexForStrokes,
                                Count = strokesToSave.Count
                            });
                    }
                    else
                    {
                        stem = $"{DateTime.Now:yyyy-MM-dd HH-mm-ss-fff} Page-{pageIndexForStrokes} StrokesCount-{strokesToSave.Count}";
                    }
                    strokeSavePath = Path.Combine(basePath, stem + ".icstk");
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
                        Helpers.ScreenshotImageSaveHelper.Save(bitmapToSave, path);
                        bitmapToSave.Dispose();
                    }

                    if (strokesToSave != null && !string.IsNullOrEmpty(strokeSavePath))
                    {
                        // 原子写：tmp + File.Replace/Move，避免 FileMode.Create 直接截断后
                        // 写入中途失败（磁盘满/进程被杀）让 .icstk 停在 0 字节。
                        var tmpStrokePath = strokeSavePath + ".tmp";
                        try
                        {
                            using (var fs = new FileStream(tmpStrokePath, FileMode.Create))
                            {
                                strokesToSave.Save(fs);
                            }
                            if (File.Exists(strokeSavePath))
                                File.Replace(tmpStrokePath, strokeSavePath, null);
                            else
                                File.Move(tmpStrokePath, strokeSavePath);
                        }
                        catch
                        {
                            try { if (File.Exists(tmpStrokePath)) File.Delete(tmpStrokePath); } catch (Exception ex) { LogService.LogException(ex); }
                            throw;
                        }
                    }

                    if (!hideNotification && !string.IsNullOrEmpty(path))
                    {
                        _hooks.UiInvoke(() => _hooks.ShowScreenshotSaveSuccess(path));
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
        internal static System.Drawing.Bitmap CaptureScreenshotToBitmap()
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
        /// 截取指定屏幕区域
        /// </summary>
        /// <param name="area">要截取的屏幕区域</param>
        /// <returns>截取的位图</returns>
        /// <remarks>
        /// 该方法会：
        /// 1. 确保区域在有效范围内
        /// 2. 调整区域边界，确保不超出屏幕范围
        /// 3. 创建支持透明度的位图
        /// 4. 设置高质量渲染
        /// 5. 截取屏幕区域
        /// </remarks>
        internal static Bitmap CaptureScreenArea(Rectangle area)
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

                // 创建支持透明度的位图
                var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    // 设置高质量渲染
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.CompositingMode = CompositingMode.SourceOver;

                    // 截取屏幕区域
                    graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
                }

                return bitmap;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"截取屏幕区域失败: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        /// <summary>
        /// 将截图写入剪贴板，同时提供 PNG 流、Bitmap、FileDrop 三种格式。
        /// 仅写入 Bitmap 格式时 Win+V 剪贴板历史不收录、资源管理器右键也不能粘贴为文件，
        /// 故用 DataObject 多格式写入：
        /// - PNG 流：Win+V 历史 / 现代应用可识别
        /// - Bitmap：老式应用兼容
        /// - FileDrop：资源管理器右键可粘贴为 .png 文件（需传入已保存的文件路径）
        /// 注意：带上 FileDrop 后，部分聊天软件可能优先把贴图当作发送文件处理。
        /// </summary>
        internal static void CopyImageToClipboard(BitmapSource image, string filePath = null)
        {
            if (image == null) return;

            var pngStream = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(pngStream);
            pngStream.Position = 0;

            var data = new System.Windows.DataObject();
            data.SetData("PNG", pngStream);                              // Win+V 历史 / 现代应用
            data.SetData(System.Windows.DataFormats.Bitmap, image);    // 老式 Bitmap 兼容
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                data.SetData(System.Windows.DataFormats.FileDrop, new[] { filePath }); // 资源管理器右键粘贴为文件
            }
            System.Windows.Clipboard.SetDataObject(data, true);
        }

        /// <summary>
        /// 区域截图执行段：捕获区域位图 → 按需叠加墨迹 → 按需应用形状遮罩 → PNG 落盘 → 按需写剪贴板。
        /// </summary>
        /// <param name="screenshotResult">选区窗口返回的截图结果（区域、路径、墨迹叠加信息）。</param>
        /// <param name="savePath">保存路径。</param>
        /// <param name="copyToClipboard">是否在保存后复制到剪贴板。</param>
        /// <returns>捕获与保存成功返回 <c>true</c>；捕获失败返回 <c>false</c>（通知由调用方壳发出）。</returns>
        internal bool SaveAreaScreenshot(ScreenshotResult screenshotResult, string savePath, bool copyToClipboard)
        {
            using (var originalBitmap = CaptureScreenArea(screenshotResult.Area))
            {
                if (originalBitmap == null)
                {
                    return false;
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

                    var directory = Path.GetDirectoryName(savePath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    finalBitmap.Save(savePath, ImageFormat.Png);

                    // 截图后复制到剪贴板
                    if (copyToClipboard)
                    {
                        try { CopyImageToClipboard(ConvertBitmapToBitmapSource(finalBitmap), savePath); }
                        catch (Exception ex) { LogHelper.WriteLogToFile($"截图复制到剪贴板失败: {ex}", LogHelper.LogType.Warning); }
                    }

                    return true;
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

        /// <summary>
        /// 提取公共的截图和保存逻辑
        /// </summary>
        /// <param name="savePath">保存路径</param>
        /// <param name="isHideNotification">是否隐藏通知</param>
        /// <param name="copyToClipboard">是否在保存后复制到剪贴板</param>
        /// <remarks>
        /// 该方法会：
        /// 1. 获取虚拟屏幕边界
        /// 2. 创建位图并设置高质量渲染
        /// 3. 从屏幕复制内容到位图
        /// 4. 确保保存目录存在
        /// 5. 保存为PNG格式
        /// 6. 若 copyToClipboard 为真，将截图复制到剪贴板
        /// 7. 如果不隐藏通知，显示保存成功通知
        /// 8. 异步上传截图到Dlass
        /// </remarks>
        internal void CaptureAndSaveScreenshot(string savePath, bool isHideNotification, bool copyToClipboard = false)
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
                LogHelper.WriteLogToFile(
                    $"[Screenshot] 截图已保存: {savePath} ({rc.Width}x{rc.Height}), 复制到剪贴板={copyToClipboard}",
                    LogHelper.LogType.Info);

                // 截图后复制到剪贴板
                if (copyToClipboard)
                {
                    try { CopyImageToClipboard(ConvertBitmapToBitmapSource(bitmap), savePath); }
                    catch (Exception ex) { LogHelper.WriteLogToFile($"截图复制到剪贴板失败: {ex}", LogHelper.LogType.Warning); }
                }
            }

            if (!isHideNotification)
            {
                Task.Delay(100).ContinueWith(t =>
                {
                    _hooks.UiInvoke(() => _hooks.ShowScreenshotSaveSuccess(savePath));
                });
            }
            _ = Task.Run(async () =>
            {
                try
                {
                    // 使用上传帮助类上传到所有启用的服务
                    await Helpers.UploadHelper.UploadFileAsync(savePath);
                }
                catch (Exception ex)
                {
                    LogService.LogException(ex);
                }
            });
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
        /// 3. 如果设置了自动保存墨迹，调用保存墨迹逻辑
        /// </remarks>
        internal void SaveScreenShot(bool isHideNotification, string fileName = null)
        {
            var savePath = _hooks.GetSettings().Automation.IsSaveScreenshotsInDateFolders
                ? GetDateFolderPath(fileName)
                : GetDefaultFolderPath();

            CaptureAndSaveScreenshot(savePath, isHideNotification);

            if (_hooks.GetSettings().Automation.IsAutoSaveStrokesAtScreenshot)
                _hooks.SaveInkStrokesAtScreenshot();
        }

        /// <summary>
        /// 保存截图到配置的保存目录
        /// </summary>
        /// <remarks>
        /// 该方法会：
        /// 1. 根据截图组件设置生成保存路径和文件名
        /// 2. 调用CaptureAndSaveScreenshot方法捕获并保存截图
        /// 3. 如果设置了截图后复制到剪贴板，将截图复制到剪贴板
        /// 4. 如果设置了自动保存墨迹，调用保存墨迹逻辑
        /// </remarks>
        internal void SaveScreenShotToDesktop()
        {
            var savePath = Path.Combine(
                GetScreenshotSaveDirectory(),
                $"{GetScreenshotFileNameStem()}.png");

            CaptureAndSaveScreenshot(savePath, false, _hooks.GetSettings().Automation.IsCopyScreenshotToClipboard);

            if (_hooks.GetSettings().Automation.IsAutoSaveStrokesAtScreenshot)
                _hooks.SaveInkStrokesAtScreenshot();
        }

        /// <summary>
        /// 获取截图保存目录。
        /// 仅当"截图保存到指定位置"开关开启时使用自定义位置，否则回退到桌面。
        /// </summary>
        internal string GetScreenshotSaveDirectory()
        {
            if (_hooks.GetSettings().Automation.IsSaveScreenshotToCustomLocation)
            {
                var location = _hooks.GetSettings().Automation.ScreenshotSaveLocation;
                if (!string.IsNullOrWhiteSpace(location))
                    return location;
            }
            return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
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
        internal string GetDateFolderPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = _hooks.GetSettings().Automation.IsUseCustomSaveFileName
                    ? SaveFileNameHelper.Render(_hooks.GetSettings().Automation.CustomSaveFileNameTemplate,
                        new SaveFileNameContext { Mode = "Screenshot", Type = "Auto", Count = _hooks.GetInkStrokeCount() })
                    : DateTime.Now.ToString("HH-mm-ss");
            }

            var basePath = _hooks.GetSettings().Automation.AutoSavedStrokesLocation;
            var dateFolder = DateTime.Now.ToString("yyyyMMdd");
            var safeRelativePath = SanitizeScreenshotRelativePath(fileName);

            return Path.Combine(
                basePath,
                "Auto Saved - Screenshots",
                dateFolder,
                safeRelativePath + Helpers.ScreenshotImageSaveHelper.GetExtension());
        }

        private static string SanitizeScreenshotRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return DateTime.Now.ToString("HH-mm-ss");

            var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
            var parts = relativePath
                .Split(separators, StringSplitOptions.RemoveEmptyEntries)
                .Select(SanitizePathPart)
                .Where(part => !string.IsNullOrWhiteSpace(part));

            var sanitized = Path.Combine(parts.ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? DateTime.Now.ToString("HH-mm-ss") : sanitized;
        }

        private static string SanitizePathPart(string pathPart)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var chars = pathPart.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray();
            var sanitized = new string(chars).Trim().TrimEnd('.', ' ');
            return sanitized == "." || sanitized == ".." ? "_" : sanitized;
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
        internal string GetDefaultFolderPath()
        {
            var basePath = _hooks.GetSettings().Automation.AutoSavedStrokesLocation;
            var screenshotsFolder = Path.Combine(basePath, "Auto Saved - Screenshots");

            if (!Directory.Exists(screenshotsFolder))
            {
                Directory.CreateDirectory(screenshotsFolder);
            }

            return Path.Combine(
                screenshotsFolder,
                GetScreenshotFileNameStem() + Helpers.ScreenshotImageSaveHelper.GetExtension());
        }

        internal string GetScreenshotFileNameStem()
        {
            if (_hooks.GetSettings().Automation.IsUseCustomSaveFileName)
            {
                return SaveFileNameHelper.Render(_hooks.GetSettings().Automation.CustomSaveFileNameTemplate,
                    new SaveFileNameContext { Mode = "Screenshot", Type = "Auto", Count = _hooks.GetInkStrokeCount() });
            }
            return DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        }

        internal static Bitmap OverlayInkOnCapturedBitmap(Bitmap capturedBitmap, Rectangle captureArea, BitmapSource inkOverlayBitmapSource)
        {
            if (capturedBitmap == null || inkOverlayBitmapSource == null)
            {
                return capturedBitmap;
            }

            try
            {
                var virtualScreen = SystemInformation.VirtualScreen;
                var sourceRect = new Rectangle(
                    captureArea.X - virtualScreen.X,
                    captureArea.Y - virtualScreen.Y,
                    captureArea.Width,
                    captureArea.Height);

                sourceRect.Intersect(new Rectangle(0, 0, inkOverlayBitmapSource.PixelWidth, inkOverlayBitmapSource.PixelHeight));
                if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
                {
                    return capturedBitmap;
                }

                using (var inkOverlayBitmap = ConvertBitmapSourceToBitmap(inkOverlayBitmapSource))
                {
                    if (inkOverlayBitmap == null)
                    {
                        return capturedBitmap;
                    }

                    Bitmap resultBitmap = null;
                    try
                    {
                        resultBitmap = new Bitmap(capturedBitmap.Width, capturedBitmap.Height, PixelFormat.Format32bppArgb);
                        using (var g = Graphics.FromImage(resultBitmap))
                        {
                            g.DrawImage(capturedBitmap, 0, 0, capturedBitmap.Width, capturedBitmap.Height);

                            var targetRect = new Rectangle(0, 0, Math.Min(sourceRect.Width, capturedBitmap.Width), Math.Min(sourceRect.Height, capturedBitmap.Height));
                            g.DrawImage(inkOverlayBitmap, targetRect, sourceRect, GraphicsUnit.Pixel);
                        }

                        return resultBitmap;
                    }
                    catch
                    {
                        resultBitmap?.Dispose();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"叠加截图墨迹失败: {ex.Message}", LogHelper.LogType.Warning);
                return capturedBitmap;
            }
        }

        internal static Bitmap ConvertBitmapSourceToBitmap(BitmapSource bitmapSource)
        {
            if (bitmapSource == null)
            {
                return null;
            }

            using (var memoryStream = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(memoryStream);
                memoryStream.Position = 0;
                using (var tempBitmap = new Bitmap(memoryStream))
                {
                    return new Bitmap(tempBitmap);
                }
            }
        }

        /// <summary>
        /// 应用形状遮罩到位图
        /// </summary>
        /// <param name="bitmap">要遮罩的位图</param>
        /// <param name="path">遮罩路径（WPF 坐标点集）</param>
        /// <param name="area">截图区域</param>
        /// <returns>遮罩后的位图，或路径无效/失败时的原位图</returns>
        /// <remarks>
        /// 该方法会：
        /// 1. 验证路径参数
        /// 2. 获取DPI缩放比例
        /// 3. 创建结果位图，确保支持透明度
        /// 4. 将整个位图设置为透明
        /// 5. 设置高质量渲染
        /// 6. 转换WPF坐标到GDI+坐标，考虑DPI缩放和屏幕偏移
        /// 7. 添加路径
        /// 8. 验证路径是否有效
        /// 9. 设置裁剪区域为路径内部
        /// 10. 在裁剪区域内绘制原始图像
        /// 11. 重置裁剪区域，确保后续操作不受影响
        /// </remarks>
        internal Bitmap ApplyShapeMask(Bitmap bitmap, List<Point> path, Rectangle area)
        {
            try
            {
                // 验证路径参数
                if (path == null || path.Count < 3)
                {
                    LogHelper.WriteLogToFile("路径点数不足，无法应用形状遮罩", LogHelper.LogType.Warning);
                    return bitmap;
                }

                // 获取DPI缩放比例
                var dpiScale = _hooks.GetDpiScale();
                var virtualScreen = SystemInformation.VirtualScreen;

                // 创建结果位图，确保支持透明度
                var resultBitmap = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);

                // 首先将整个位图设置为透明
                using (var resultGraphics = Graphics.FromImage(resultBitmap))
                {
                    // 清除位图，设置为完全透明
                    resultGraphics.Clear(Color.Transparent);

                    // 设置高质量渲染
                    resultGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                    resultGraphics.CompositingQuality = CompositingQuality.HighQuality;
                    resultGraphics.CompositingMode = CompositingMode.SourceOver;

                    // 创建路径
                    using (var pathGraphics = new GraphicsPath())
                    {
                        // 转换WPF坐标到GDI+坐标，考虑DPI缩放和屏幕偏移
                        var points = new PointF[path.Count];
                        for (int i = 0; i < path.Count; i++)
                        {
                            // 将WPF坐标转换为实际屏幕坐标，然后相对于截图区域计算偏移
                            double screenX = (path[i].X * dpiScale) + virtualScreen.Left;
                            double screenY = (path[i].Y * dpiScale) + virtualScreen.Top;

                            // 计算相对于截图区域的坐标
                            float relativeX = (float)(screenX - area.X);
                            float relativeY = (float)(screenY - area.Y);

                            // 确保坐标在有效范围内
                            relativeX = Math.Max(0, Math.Min(relativeX, bitmap.Width - 1));
                            relativeY = Math.Max(0, Math.Min(relativeY, bitmap.Height - 1));

                            points[i] = new PointF(relativeX, relativeY);
                        }

                        // 添加路径 - 使用FillMode.Winding确保路径正确填充
                        pathGraphics.FillMode = FillMode.Winding;
                        pathGraphics.AddPolygon(points);

                        // 验证路径是否有效
                        if (!pathGraphics.IsVisible(0, 0) && pathGraphics.GetBounds().Width > 0 && pathGraphics.GetBounds().Height > 0)
                        {
                            // 设置裁剪区域为路径内部
                            resultGraphics.SetClip(pathGraphics);

                            // 在裁剪区域内绘制原始图像
                            resultGraphics.DrawImage(bitmap, 0, 0);

                            // 重置裁剪区域，确保后续操作不受影响
                            resultGraphics.ResetClip();
                        }
                        else
                        {
                            LogHelper.WriteLogToFile("生成的路径无效，返回透明图像", LogHelper.LogType.Warning);
                            // 如果路径无效，返回透明图像
                            return resultBitmap;
                        }
                    }
                }

                return resultBitmap;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用形状遮罩失败: {ex.Message}", LogHelper.LogType.Error);
                return bitmap;
            }
        }

        /// <summary>
        /// 将System.Drawing.Bitmap转换为WPF BitmapSource
        /// </summary>
        /// <param name="bitmap">要转换的位图</param>
        /// <returns>转换后的BitmapSource</returns>
        /// <remarks>
        /// 该方法会：
        /// 1. 验证位图有效性
        /// 2. 验证位图尺寸
        /// 3. 使用更安全的方法转换位图
        /// 4. 根据像素格式选择合适的WPF像素格式
        /// 5. 创建BitmapSource
        /// 6. 冻结BitmapSource以提高性能
        /// 7. 如果转换失败，尝试使用备用方法
        /// </remarks>
        internal static BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            try
            {
                // 验证位图有效性
                if (bitmap == null)
                    return null;

                // 验证位图尺寸
                if (bitmap.Width <= 0 || bitmap.Height <= 0)
                    return null;

                // 使用更安全的方法转换位图
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.ReadOnly,
                    bitmap.PixelFormat);

                try
                {
                    // 根据像素格式选择合适的WPF像素格式
                    System.Windows.Media.PixelFormat wpfPixelFormat;
                    switch (bitmap.PixelFormat)
                    {
                        case PixelFormat.Format24bppRgb:
                            wpfPixelFormat = System.Windows.Media.PixelFormats.Bgr24;
                            break;
                        case PixelFormat.Format32bppArgb:
                            wpfPixelFormat = System.Windows.Media.PixelFormats.Bgra32;
                            break;
                        case PixelFormat.Format32bppRgb:
                            wpfPixelFormat = System.Windows.Media.PixelFormats.Bgr32;
                            break;
                        default:
                            wpfPixelFormat = System.Windows.Media.PixelFormats.Bgr24;
                            break;
                    }

                    var bitmapSource = BitmapSource.Create(
                        bitmapData.Width,
                        bitmapData.Height,
                        bitmap.HorizontalResolution,
                        bitmap.VerticalResolution,
                        wpfPixelFormat,
                        null,
                        bitmapData.Scan0,
                        bitmapData.Stride * bitmapData.Height,
                        bitmapData.Stride);

                    bitmapSource.Freeze();
                    return bitmapSource;
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"转换位图失败: {ex.Message}", LogHelper.LogType.Error);

                // 尝试使用备用方法：内存流转换
                try
                {
                    return ConvertBitmapToBitmapSourceFallback(bitmap);
                }
                catch (Exception fallbackEx)
                {
                    LogHelper.WriteLogToFile($"备用转换方法也失败: {fallbackEx.Message}", LogHelper.LogType.Error);

                    // 最后尝试：使用最简单的转换方法
                    try
                    {
                        return ConvertBitmapToBitmapSourceSimple(bitmap);
                    }
                    catch (Exception simpleEx)
                    {
                        LogHelper.WriteLogToFile($"简单转换方法也失败: {simpleEx.Message}", LogHelper.LogType.Error);
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// 备用的位图转换方法（使用内存流）
        /// </summary>
        /// <param name="bitmap">要转换的位图</param>
        /// <returns>转换后的BitmapSource</returns>
        /// <remarks>
        /// 该方法会：
        /// 1. 验证位图有效性
        /// 2. 创建一个新的位图，确保格式正确
        /// 3. 在内存流中保存为PNG格式
        /// 4. 创建BitmapImage并加载内存流中的数据
        /// 5. 冻结BitmapImage以提高性能
        /// </remarks>
        private static BitmapSource ConvertBitmapToBitmapSourceFallback(Bitmap bitmap)
        {
            try
            {
                // 验证位图有效性
                if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0)
                    return null;

                // 创建一个新的位图，确保保留Alpha通道
                using (var convertedBitmap = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb))
                {
                    using (var graphics = Graphics.FromImage(convertedBitmap))
                    {
                        graphics.CompositingMode = CompositingMode.SourceCopy;
                        graphics.DrawImage(bitmap, 0, 0);
                    }

                    using (var memory = new MemoryStream())
                    {
                        convertedBitmap.Save(memory, ImageFormat.Png);
                        memory.Position = 0;

                        var bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.StreamSource = memory;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze();

                        return bitmapImage;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"备用转换方法失败: {ex.Message}", LogHelper.LogType.Error);
                throw;
            }
        }

        /// <summary>
        /// 最简单的位图转换方法
        /// </summary>
        /// <param name="bitmap">要转换的位图</param>
        /// <returns>转换后的BitmapSource</returns>
        /// <remarks>
        /// 该方法会：
        /// 1. 验证位图有效性
        /// 2. 使用最基础的方法：直接保存为PNG然后加载
        /// 3. 创建临时文件
        /// 4. 将位图保存为PNG格式到临时文件
        /// 5. 创建BitmapImage并加载临时文件
        /// 6. 冻结BitmapImage以提高性能
        /// 7. 清理临时文件
        /// </remarks>
        private static BitmapSource ConvertBitmapToBitmapSourceSimple(Bitmap bitmap)
        {
            try
            {
                if (bitmap == null)
                    return null;

                // 使用最基础的方法：直接保存为PNG然后加载
                var tempFile = Path.GetTempFileName() + ".png";

                try
                {
                    bitmap.Save(tempFile, ImageFormat.Png);

                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = new Uri(tempFile);
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();

                    return bitmapImage;
                }
                finally
                {
                    // 清理临时文件
                    try
                    {
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }
                    }
                    catch (Exception deleteEx)
                    {
                        LogHelper.WriteLogToFile($"删除临时文件失败: {deleteEx.Message}", LogHelper.LogType.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"简单转换方法失败: {ex.Message}", LogHelper.LogType.Error);
                throw;
            }
        }
    }
}
