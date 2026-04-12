using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Color = System.Drawing.Color;
using Cursors = System.Windows.Input.Cursors;
using Image = System.Windows.Controls.Image;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Point = System.Windows.Point;
using Size = System.Drawing.Size;

namespace Ink_Canvas
{
    // 截图结果结构体
    public struct ScreenshotResult
    {
        public Rectangle Area;
        public List<Point> Path;
        public Bitmap CameraImage;
        public BitmapSource CameraBitmapSource;
        public bool AddToWhiteboard;
        public bool IncludeInk;
        public BitmapSource InkOverlayBitmapSource;

        public ScreenshotResult(Rectangle area, List<Point> path = null, Bitmap cameraImage = null,
            BitmapSource cameraBitmapSource = null, bool addToWhiteboard = false, bool includeInk = false, BitmapSource inkOverlayBitmapSource = null)
        {
            Area = area;
            Path = path;
            CameraImage = cameraImage;
            CameraBitmapSource = cameraBitmapSource;
            AddToWhiteboard = addToWhiteboard;
            IncludeInk = includeInk;
            InkOverlayBitmapSource = inkOverlayBitmapSource;
        }
    }

    public partial class MainWindow : Window
    {
        /// <summary>
        /// 截图并插入到画布
        /// </summary>
        /// <returns>异步任务</returns>
        /// <remarks>
        /// 该方法会：
        /// 1. 隐藏主窗口以避免截图包含窗口本身
        /// 2. 启动区域选择截图
        /// 3. 恢复窗口显示
        /// 4. 处理截图结果并插入到画布
        /// 5. 支持摄像头截图和区域截图
        /// </remarks>
        private async Task CaptureScreenshotAndInsert()
        {
            try
            {
                var inkOverlayPreview = CreateInkOverlayPreviewBitmapSource();

                // 隐藏主窗口以避免截图包含窗口本身
                var originalVisibility = Visibility;
                Visibility = Visibility.Hidden;

                // 等待窗口隐藏
                await Task.Delay(200);

                // 启动区域选择截图
                var screenshotResult = await ShowScreenshotSelector(inkOverlayPreview);

                // 恢复窗口显示
                Visibility = originalVisibility;

                if (screenshotResult.HasValue)
                {
                    if (screenshotResult.Value.AddToWhiteboard)
                    {
                        await AddScreenshotToNewWhiteboardPage(screenshotResult.Value);
                        return;
                    }

                    // 检查是否是摄像头截图
                    if (screenshotResult.Value.CameraBitmapSource != null)
                    {
                        // 摄像头截图（使用BitmapSource）
                        await InsertBitmapSourceToCanvas(screenshotResult.Value.CameraBitmapSource, "摄像头截图已插入到画布", "插入摄像头截图失败");
                    }
                    else if (screenshotResult.Value.CameraImage != null)
                    {
                        // 摄像头截图（使用Bitmap）
                        await InsertScreenshotToCanvas(screenshotResult.Value.CameraImage);
                    }
                    else if (screenshotResult.Value.Area.Width > 0 && screenshotResult.Value.Area.Height > 0)
                    {
                        // 屏幕截图
                        using (var originalBitmap = CaptureScreenArea(screenshotResult.Value.Area))
                        {
                            if (originalBitmap != null)
                            {
                                Bitmap finalBitmap = originalBitmap;
                                bool needDisposeFinalBitmap = false;

                                try
                                {
                                    if (screenshotResult.Value.IncludeInk && screenshotResult.Value.InkOverlayBitmapSource != null)
                                    {
                                        var withInkBitmap = OverlayInkOnCapturedBitmap(finalBitmap, screenshotResult.Value.Area, screenshotResult.Value.InkOverlayBitmapSource);
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

                                    // 如果有路径信息，应用形状遮罩
                                    if (screenshotResult.Value.Path != null && screenshotResult.Value.Path.Count > 0)
                                    {
                                        var maskedBitmap = ApplyShapeMask(finalBitmap, screenshotResult.Value.Path, screenshotResult.Value.Area);
                                        if (maskedBitmap != null && maskedBitmap != finalBitmap)
                                        {
                                            if (needDisposeFinalBitmap && finalBitmap != originalBitmap)
                                            {
                                                finalBitmap.Dispose();
                                            }
                                            finalBitmap = maskedBitmap;
                                            needDisposeFinalBitmap = true; // 标记需要释放新创建的位图
                                        }
                                    }

                                    // 将截图转换为WPF Image并插入到画布
                                    await InsertScreenshotToCanvas(finalBitmap);
                                }
                                finally
                                {
                                    // 如果创建了新的位图，需要释放它
                                    if (needDisposeFinalBitmap && finalBitmap != originalBitmap)
                                    {
                                        finalBitmap.Dispose();
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    ShowNotification("截图已取消");
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"截图失败: {ex.Message}");
                Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 直接全屏截图并插入到画布
        /// </summary>
        /// <returns>异步任务</returns>
        /// <remarks>
        /// 该方法会：
        /// 1. 隐藏主窗口以避免截图包含窗口本身
        /// 2. 获取虚拟屏幕边界
        /// 3. 截取全屏
        /// 4. 将截图转换为WPF Image并插入到画布
        /// 5. 恢复窗口显示
        /// </remarks>
        private async Task CaptureFullScreenAndInsert()
        {
            try
            {
                // 隐藏主窗口以避免截图包含窗口本身
                var originalVisibility = Visibility;
                Visibility = Visibility.Hidden;

                // 等待窗口隐藏
                await Task.Delay(200);

                // 获取虚拟屏幕边界
                var virtualScreen = SystemInformation.VirtualScreen;
                var fullScreenArea = new Rectangle(virtualScreen.X, virtualScreen.Y, virtualScreen.Width, virtualScreen.Height);

                // 截取全屏
                using (var fullScreenBitmap = CaptureScreenArea(fullScreenArea))
                {
                    if (fullScreenBitmap != null)
                    {
                        // 将截图转换为WPF Image并插入到画布
                        await InsertScreenshotToCanvas(fullScreenBitmap);
                    }
                    else
                    {
                        ShowNotification("全屏截图失败");
                    }
                }

                // 恢复窗口显示
                Visibility = originalVisibility;
            }
            catch (Exception ex)
            {
                ShowNotification($"全屏截图失败: {ex.Message}");
                Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 显示截图区域选择器并返回用户的截图结果（区域截图或摄像头截图）。
        /// </summary>
        /// <param name="inkOverlayPreview">当用户选择包含墨迹的区域截图时，用于作为墨迹叠加的预览 <see cref="BitmapSource"/>；可为 <c>null</c>。</param>
        /// <returns>若用户确认截图则返回 <see cref="ScreenshotResult"/>，否则返回 <c>null</c>。返回的结果可能为摄像头截图或区域截图，摄像头截图会包含 <see cref="ScreenshotResult.CameraBitmapSource"/> 或 <see cref="ScreenshotResult.CameraImage"/>，区域截图会包含有效的区域与路径。</returns>
        /// <remarks>
        /// 该方法会：
        /// 1. 在 UI 线程（通过 <see cref="Application.Current.Dispatcher"/>）上显示截图选择器窗口 <see cref="ScreenshotSelectorWindow"/>；
        /// 2. 获取用户选择的区域截图或摄像头截图；
        /// 3. 根据用户选择构建并返回 <see cref="ScreenshotResult"/>；
        /// 4. 若用户取消对话框或未确认截图，返回 <c>null</c>；
        /// 5. 方法内部捕获异常并记录日志（不会向调用方抛出异常），如需外部处理请调整实现以重新抛出或传回错误信息。
        /// </remarks>
        private async Task<ScreenshotResult?> ShowScreenshotSelector(BitmapSource inkOverlayPreview = null)
        {
            ScreenshotResult? result = null;

            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var selectorWindow = new ScreenshotSelectorWindow(inkOverlayPreview);
                    if (selectorWindow.ShowDialog() == true)
                    {
                        // 检查是否是摄像头截图
                        if (selectorWindow.CameraBitmapSource != null)
                        {
                            result = new ScreenshotResult(
                                Rectangle.Empty, // 摄像头截图不需要区域
                                null, // 摄像头截图不需要路径
                                null, // 不再使用Bitmap
                                selectorWindow.CameraBitmapSource, // 摄像头BitmapSource
                                selectorWindow.ShouldAddToWhiteboard,
                                false,
                                null
                            );
                        }
                        else if (selectorWindow.CameraImage != null)
                        {
                            result = new ScreenshotResult(
                                Rectangle.Empty, // 摄像头截图不需要区域
                                null, // 摄像头截图不需要路径
                                selectorWindow.CameraImage, // 摄像头图像
                                null,
                                selectorWindow.ShouldAddToWhiteboard,
                                false,
                                null
                            );
                        }
                        else
                        {
                            result = new ScreenshotResult(
                                selectorWindow.SelectedArea.Value,
                                selectorWindow.SelectedPath,
                                null,
                                null,
                                selectorWindow.ShouldAddToWhiteboard,
                                selectorWindow.IncludeInkInScreenshot,
                                selectorWindow.IncludeInkInScreenshot ? inkOverlayPreview : null
                            );
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"显示截图选择器失败: {ex.Message}", LogHelper.LogType.Error);
            }

            return result;
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
        private Bitmap CaptureScreenArea(Rectangle area)
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

        private BitmapSource CreateInkOverlayPreviewBitmapSource()
        {
            try
            {
                if (inkCanvas == null)
                {
                    return null;
                }

                if ((inkCanvas.Strokes?.Count ?? 0) == 0 && inkCanvas.Children.Count == 0)
                {
                    return null;
                }

                if (inkCanvas.ActualWidth <= 0 || inkCanvas.ActualHeight <= 0)
                {
                    return null;
                }

                var virtualScreen = SystemInformation.VirtualScreen;
                var source = PresentationSource.FromVisual(inkCanvas);
                var transformToDevice = source?.CompositionTarget?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;

                // PointToScreen 返回WPF坐标（DIP），统一转换为设备像素后再与 VirtualScreen 对齐
                var inkTopLeftDip = inkCanvas.PointToScreen(new Point(0, 0));
                var inkTopLeftPx = transformToDevice.Transform(inkTopLeftDip);
                var offsetX = inkTopLeftPx.X - virtualScreen.Left;
                var offsetY = inkTopLeftPx.Y - virtualScreen.Top;
                var widthPx = inkCanvas.ActualWidth * transformToDevice.M11;
                var heightPx = inkCanvas.ActualHeight * transformToDevice.M22;

                var drawingVisual = new DrawingVisual();
                using (var dc = drawingVisual.RenderOpen())
                {
                    // 使用完整 InkCanvas 视觉树，确保包含图片等子元素
                    var visualBrush = new VisualBrush(inkCanvas)
                    {
                        Stretch = Stretch.Fill
                    };
                    dc.DrawRectangle(visualBrush, null, new Rect(offsetX, offsetY, widthPx, heightPx));
                }

                var rtb = new RenderTargetBitmap(
                    Math.Max(1, virtualScreen.Width),
                    Math.Max(1, virtualScreen.Height),
                    96,
                    96,
                    PixelFormats.Pbgra32);
                rtb.Render(drawingVisual);
                rtb.Freeze();
                return rtb;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"创建截图墨迹预览失败: {ex.Message}", LogHelper.LogType.Warning);
                return null;
            }
        }

        private Bitmap OverlayInkOnCapturedBitmap(Bitmap capturedBitmap, Rectangle captureArea, BitmapSource inkOverlayBitmapSource)
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

        private Bitmap ConvertBitmapSourceToBitmap(BitmapSource bitmapSource)
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
        /// 将截图插入到画布
        /// </summary>
        /// <param name="bitmap">要插入的位图</param>
        /// <returns>异步任务</returns>
        /// <remarks>
        /// 该方法会：
        /// 1. 验证位图有效性
        /// 2. 将Bitmap转换为WPF BitmapSource
        /// 3. 创建WPF Image控件
        /// 4. 生成唯一名称
        /// 5. 初始化TransformGroup
        /// 6. 设置截图属性，避免被InkCanvas选择系统处理
        /// 7. 初始化InkCanvas选择设置
        /// 8. 等待图片加载完成后进行居中处理
        /// 9. 添加到画布
        /// 10. 提交历史记录
        /// 11. 插入图片后切换到选择模式并刷新浮动栏高光显示
        /// </remarks>
        private async Task InsertScreenshotToCanvas(Bitmap bitmap)
        {
            try
            {
                // 验证位图有效性
                if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0)
                {
                    ShowNotification("无效的截图");
                    return;
                }

                // 将Bitmap转换为WPF BitmapSource
                var bitmapSource = ConvertBitmapToBitmapSource(bitmap);

                if (bitmapSource == null)
                {
                    ShowNotification("转换截图失败");
                    return;
                }

                // 创建WPF Image控件
                var image = new Image
                {
                    Source = bitmapSource,
                    Stretch = Stretch.Uniform
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

                // 生成唯一名称
                string timestamp = "screenshot_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
                image.Name = timestamp;

                // 初始化TransformGroup
                InitializeScreenshotTransform(image);

                // 设置截图属性，避免被InkCanvas选择系统处理
                image.IsHitTestVisible = true;
                image.Focusable = false;

                // 初始化InkCanvas选择设置
                InitializeInkCanvasSelectionSettings();

                // 等待图片加载完成后再进行居中处理
                image.Loaded += (sender, e) =>
                {
                    // 确保在UI线程中执行
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CenterAndScaleScreenshot(image);
                        // 绑定事件处理器
                        BindScreenshotEvents(image);
                    }), DispatcherPriority.Loaded);
                };

                // 添加到画布
                inkCanvas.Children.Add(image);

                // 提交历史记录
                timeMachine.CommitElementInsertHistory(image);

                // 插入图片后切换到选择模式并刷新浮动栏高光显示
                SetCurrentToolMode(InkCanvasEditingMode.Select);
                UpdateCurrentToolMode("select");
                HideSubPanels("select");

                ShowNotification("截图已插入到画布");
            }
            catch (Exception ex)
            {
                ShowNotification($"插入截图失败: {ex.Message}");
                LogHelper.WriteLogToFile($"插入截图失败: {ex.Message}", LogHelper.LogType.Error);
            }
            finally
            {
                bitmap?.Dispose();
            }
        }

        /// <summary>
        /// 将BitmapSource插入到画布（用于摄像头截图）
        /// </summary>
        /// <param name="bitmapSource">要插入的BitmapSource</param>
        /// <returns>异步任务</returns>
        /// <remarks>
        /// 该方法会：
        /// 1. 创建WPF Image控件
        /// 2. 生成唯一名称
        /// 3. 初始化TransformGroup
        /// 4. 设置截图属性，避免被InkCanvas选择系统处理
        /// 5. 初始化InkCanvas选择设置
        /// 6. 等待图片加载完成后进行居中处理
        /// 7. 添加到画布
        /// 8. 提交历史记录
        /// 9. 插入图片后切换到选择模式并刷新浮动栏高光显示
        /// </remarks>
        private async Task InsertBitmapSourceToCanvas(BitmapSource bitmapSource, string successMessage = "截图已插入到画布", string failureMessagePrefix = "插入截图失败")
        {
            try
            {
                // 创建WPF Image控件
                var image = new Image
                {
                    Source = bitmapSource,
                    Stretch = Stretch.Uniform
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

                // 生成唯一名称
                string timestamp = "camera_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
                image.Name = timestamp;

                // 初始化TransformGroup
                InitializeScreenshotTransform(image);

                // 设置截图属性，避免被InkCanvas选择系统处理
                image.IsHitTestVisible = true;
                image.Focusable = false;

                // 初始化InkCanvas选择设置
                InitializeInkCanvasSelectionSettings();

                // 等待图片加载完成后再进行居中处理
                image.Loaded += (sender, e) =>
                {
                    // 确保在UI线程中执行
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CenterAndScaleScreenshot(image);
                        // 绑定事件处理器
                        BindScreenshotEvents(image);
                    }), DispatcherPriority.Loaded);
                };

                // 添加到画布
                inkCanvas.Children.Add(image);

                // 提交历史记录
                timeMachine.CommitElementInsertHistory(image);

                // 插入图片后切换到选择模式并刷新浮动栏高光显示
                SetCurrentToolMode(InkCanvasEditingMode.Select);
                UpdateCurrentToolMode("select");
                HideSubPanels("select");

                ShowNotification(successMessage);
            }
            catch (Exception ex)
            {
                ShowNotification($"{failureMessagePrefix}: {ex.Message}");
                LogHelper.WriteLogToFile($"插入摄像头截图失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 初始化截图的TransformGroup
        /// </summary>
        /// <param name="image">要初始化的Image控件</param>
        /// <remarks>
        /// 该方法会为截图创建一个包含缩放、平移和旋转变换的TransformGroup。
        /// </remarks>
        private void InitializeScreenshotTransform(Image image)
        {
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new ScaleTransform(1, 1));
            transformGroup.Children.Add(new TranslateTransform(0, 0));
            transformGroup.Children.Add(new RotateTransform(0));
            image.RenderTransform = transformGroup;
        }

        /// <summary>
        /// 绑定截图事件处理器
        /// </summary>
        /// <param name="image">要绑定事件的Image控件</param>
        /// <remarks>
        /// 该方法会为截图绑定以下事件：
        /// 1. 鼠标事件（按下、释放、移动、滚轮）
        /// 2. 触摸事件（按下、释放、操作）
        /// 3. 设置光标为手形
        /// 4. 禁用InkCanvas对截图的选择处理
        /// </remarks>
        private void BindScreenshotEvents(Image image)
        {
            // 鼠标事件
            image.MouseLeftButtonDown += Element_MouseLeftButtonDown;
            image.MouseLeftButtonUp += Element_MouseLeftButtonUp;
            image.MouseMove += Element_MouseMove;
            image.MouseWheel += Element_MouseWheel;

            // 触摸事件
            image.TouchDown += Element_TouchDown;
            image.TouchUp += Element_TouchUp;
            image.IsManipulationEnabled = true;
            image.ManipulationDelta += Element_ManipulationDelta;
            image.ManipulationCompleted += Element_ManipulationCompleted;

            // 设置光标
            image.Cursor = Cursors.Hand;

            // 禁用InkCanvas对截图的选择处理
            image.IsHitTestVisible = true;
            image.Focusable = false;
        }

        /// <summary>
        /// 专门为截图优化的居中缩放方法
        /// </summary>
        /// <param name="image">要居中缩放的Image控件</param>
        /// <remarks>
        /// 该方法会：
        /// 1. 确保图片已加载
        /// 2. 获取画布的实际尺寸
        /// 3. 如果画布尺寸为0，使用窗口尺寸作为备选
        /// 4. 如果仍然为0，使用屏幕尺寸
        /// 5. 计算最大允许尺寸（画布的80%）
        /// 6. 获取图片的原始尺寸
        /// 7. 计算缩放比例
        /// 8. 如果图片本身比最大尺寸小，不进行缩放
        /// 9. 计算新的尺寸
        /// 10. 设置图片尺寸
        /// 11. 计算居中位置
        /// 12. 确保位置不为负数
        /// 13. 设置位置
        /// 14. 保持滚轮缩放和拖动功能
        /// </remarks>
        private void CenterAndScaleScreenshot(Image image)
        {
            try
            {
                // 确保图片已加载
                if (image.Source == null || image.ActualWidth == 0 || image.ActualHeight == 0)
                {
                    return;
                }

                // 获取画布的实际尺寸
                double canvasWidth = inkCanvas.ActualWidth;
                double canvasHeight = inkCanvas.ActualHeight;

                // 如果画布尺寸为0，使用窗口尺寸作为备选
                if (canvasWidth <= 0 || canvasHeight <= 0)
                {
                    canvasWidth = ActualWidth;
                    canvasHeight = ActualHeight;
                }

                // 如果仍然为0，使用屏幕尺寸
                if (canvasWidth <= 0 || canvasHeight <= 0)
                {
                    canvasWidth = SystemParameters.PrimaryScreenWidth;
                    canvasHeight = SystemParameters.PrimaryScreenHeight;
                }

                // 计算最大允许尺寸（画布的80%）
                double maxWidth = canvasWidth * 0.8;
                double maxHeight = canvasHeight * 0.8;

                // 获取图片的原始尺寸
                double originalWidth = image.Source.Width;
                double originalHeight = image.Source.Height;

                // 计算缩放比例
                double scaleX = maxWidth / originalWidth;
                double scaleY = maxHeight / originalHeight;
                double scale = Math.Min(scaleX, scaleY);

                // 如果图片本身比最大尺寸小，不进行缩放
                if (scale > 1.0)
                {
                    scale = 1.0;
                }

                // 计算新的尺寸
                double newWidth = originalWidth * scale;
                double newHeight = originalHeight * scale;

                // 设置图片尺寸
                image.Width = newWidth;
                image.Height = newHeight;

                // 计算居中位置
                double centerX = (canvasWidth - newWidth) / 2;
                double centerY = (canvasHeight - newHeight) / 2;

                // 确保位置不为负数
                centerX = Math.Max(0, centerX);
                centerY = Math.Max(0, centerY);

                // 设置位置
                InkCanvas.SetLeft(image, centerX);
                InkCanvas.SetTop(image, centerY);

                // 这样可以保持滚轮缩放和拖动功能
                if (image.RenderTransform == null || image.RenderTransform == Transform.Identity)
                {
                    // 只有在没有TransformGroup时才创建
                    InitializeScreenshotTransform(image);
                }

            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"截图居中失败: {ex.Message}", LogHelper.LogType.Error);
                // 如果居中失败，使用默认的居中方法作为备选
                CenterAndScaleElement(image);
            }
        }

        /// <summary>
        /// 应用形状遮罩到截图
        /// </summary>
        /// <param name="bitmap">要应用遮罩的位图</param>
        /// <param name="path">遮罩路径</param>
        /// <param name="area">截图区域</param>
        /// <returns>应用遮罩后的位图</returns>
        /// <remarks>
        /// 该方法会：
        /// 1. 验证路径参数
        /// 2. 获取DPI缩放比例
        /// 3. 创建结果位图，确保支持透明度
        /// 4. 首先将整个位图设置为透明
        /// 5. 创建路径
        /// 6. 转换WPF坐标到GDI+坐标，考虑DPI缩放和屏幕偏移
        /// 7. 添加路径
        /// 8. 验证路径是否有效
        /// 9. 设置裁剪区域为路径内部
        /// 10. 在裁剪区域内绘制原始图像
        /// 11. 重置裁剪区域，确保后续操作不受影响
        /// </remarks>
        private Bitmap ApplyShapeMask(Bitmap bitmap, List<Point> path, Rectangle area)
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
                var dpiScale = GetDpiScale();
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
        private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
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
                            wpfPixelFormat = PixelFormats.Bgr24;
                            break;
                        case PixelFormat.Format32bppArgb:
                            wpfPixelFormat = PixelFormats.Bgra32;
                            break;
                        case PixelFormat.Format32bppRgb:
                            wpfPixelFormat = PixelFormats.Bgr32;
                            break;
                        default:
                            wpfPixelFormat = PixelFormats.Bgr24;
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
        private BitmapSource ConvertBitmapToBitmapSourceFallback(Bitmap bitmap)
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
        private BitmapSource ConvertBitmapToBitmapSourceSimple(Bitmap bitmap)
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

        /// <summary>
        /// 获取DPI缩放比例
        /// </summary>
        /// <returns>DPI缩放比例</returns>
        /// <remarks>
        /// 该方法会从当前窗口的PresentationSource获取DPI缩放比例。
        /// 如果无法获取，则返回默认值1.0。
        /// </remarks>
        private double GetDpiScale()
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                return source.CompositionTarget.TransformToDevice.M11;
            }
            return 1.0; // 默认DPI
        }
    }
}
