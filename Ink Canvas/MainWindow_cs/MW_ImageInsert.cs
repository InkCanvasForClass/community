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
using Ink_Canvas.Helpers;
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

        public ScreenshotResult(Rectangle area, List<Point> path = null)
        {
            Area = area;
            Path = path;
        }
    }

    public partial class MainWindow : Window
    {
        // 截图并插入到画布
        private async Task CaptureScreenshotAndInsert()
        {
            try
            {
                // 隐藏主窗口以避免截图包含窗口本身
                var originalVisibility = Visibility;
                Visibility = Visibility.Hidden;

                // 等待窗口隐藏
                await Task.Delay(200);

                // 启动区域选择截图
                var screenshotResult = await ShowScreenshotSelector();

                // 恢复窗口显示
                Visibility = originalVisibility;

                if (screenshotResult.HasValue && screenshotResult.Value.Area.Width > 0 && screenshotResult.Value.Area.Height > 0)
                {
                    // 截取选定区域
                    using (var originalBitmap = CaptureScreenArea(screenshotResult.Value.Area))
                    {
                        if (originalBitmap != null)
                        {
                            Bitmap finalBitmap = originalBitmap;
                            bool needDisposeFinalBitmap = false;

                            try
                            {
                                // 如果有路径信息，应用形状遮罩
                                if (screenshotResult.Value.Path != null && screenshotResult.Value.Path.Count > 0)
                                {
                                    finalBitmap = ApplyShapeMask(originalBitmap, screenshotResult.Value.Path, screenshotResult.Value.Area);
                                    needDisposeFinalBitmap = true; // 标记需要释放新创建的位图
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

                LogHelper.WriteLogToFile($"成功截取区域: X={x}, Y={y}, Width={width}, Height={height}");
                return bitmap;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"截取屏幕区域失败: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        // 将截图插入到画布
        private async Task InsertScreenshotToCanvas(Bitmap bitmap)
        {
            try
            {
                // 将Bitmap转换为WPF BitmapSource
                var bitmapSource = ConvertBitmapToBitmapSource(bitmap);

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

                ShowNotification("截图已插入到画布");
            }
            catch (Exception ex)
            {
                ShowNotification($"插入截图失败: {ex.Message}");
                LogHelper.WriteLogToFile($"插入截图失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 初始化截图的TransformGroup
        private void InitializeScreenshotTransform(Image image)
        {
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new ScaleTransform(1, 1));
            transformGroup.Children.Add(new TranslateTransform(0, 0));
            transformGroup.Children.Add(new RotateTransform(0));
            image.RenderTransform = transformGroup;
        }

        // 绑定截图事件处理器
        private void BindScreenshotEvents(Image image)
        {
            // 鼠标事件
            image.MouseLeftButtonDown += Element_MouseLeftButtonDown;
            image.MouseLeftButtonUp += Element_MouseLeftButtonUp;
            image.MouseMove += Element_MouseMove;
            image.MouseWheel += Element_MouseWheel;

            // 触摸事件
            image.IsManipulationEnabled = true;
            image.ManipulationDelta += Element_ManipulationDelta;
            image.ManipulationCompleted += Element_ManipulationCompleted;

            // 设置光标
            image.Cursor = Cursors.Hand;
            
            // 禁用InkCanvas对截图的选择处理
            image.IsHitTestVisible = true;
            image.Focusable = false;
        }

        // 专门为截图优化的居中缩放方法
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

                LogHelper.WriteLogToFile($"截图居中完成: 位置({centerX}, {centerY}), 尺寸({newWidth}x{newHeight})");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"截图居中失败: {ex.Message}", LogHelper.LogType.Error);
                // 如果居中失败，使用默认的居中方法作为备选
                CenterAndScaleElement(image);
            }
        }

        // 应用形状遮罩到截图
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

        // 将System.Drawing.Bitmap转换为WPF BitmapSource
        private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            try
            {
                using (var memory = new MemoryStream())
                {
                    bitmap.Save(memory, ImageFormat.Png);
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
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"转换位图失败: {ex.Message}", LogHelper.LogType.Error);
                throw;
            }
        }

        // 获取DPI缩放比例
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