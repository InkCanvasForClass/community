using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Application = System.Windows.Application;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Size = System.Drawing.Size;

namespace Ink_Canvas
{
    // 截图结果结构体
    public struct ScreenshotResult
    {
        public Rectangle Area;
        public List<System.Windows.Point> Path;

        public ScreenshotResult(Rectangle area, List<System.Windows.Point> path = null)
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
                var image = new System.Windows.Controls.Image
                {
                    Source = bitmapSource,
                    Stretch = Stretch.Uniform
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

                // 生成唯一名称
                string timestamp = "screenshot_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
                image.Name = timestamp;

                // 居中并缩放图片
                CenterAndScaleElement(image);

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

        // 应用形状遮罩到截图
        private Bitmap ApplyShapeMask(Bitmap bitmap, List<System.Windows.Point> path, Rectangle area)
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
                    resultGraphics.Clear(System.Drawing.Color.Transparent);
                    
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