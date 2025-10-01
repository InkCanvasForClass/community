using Ink_Canvas.Helpers;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.IO.Path;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        // 当前选中的可操作元素
        private FrameworkElement currentSelectedElement;
        private bool isDragging;
        private Point dragStartPoint;

        #region Image
        private async void BtnImageInsert_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg; *.jpeg; *.png; *.bmp)|*.jpg;*.jpeg;*.png;*.bmp";

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;

                Image image = await CreateAndCompressImageAsync(filePath);

                if (image != null)
                {
                    string timestamp = "img_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
                    image.Name = timestamp;

                    // 设置图片属性，避免被InkCanvas选择系统处理
                    image.IsHitTestVisible = true;
                    image.Focusable = false;

                    // 初始化InkCanvas选择设置
                    InitializeInkCanvasSelectionSettings();

                    // 先添加到画布
                    inkCanvas.Children.Add(image);

                    // 等待图片加载完成后再进行后续处理
                    image.Loaded += (s, args) =>
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            // 初始化TransformGroup
                            InitializeElementTransform(image);

                            // 居中缩放
                            CenterAndScaleElement(image);

                            // 最后绑定事件处理器
                            BindElementEvents(image);

                            LogHelper.WriteLogToFile($"图片插入完成: {image.Name}");
                        }), DispatcherPriority.Loaded);
                    };

                    timeMachine.CommitElementInsertHistory(image);

                    // 插入图片后切换到选择模式并刷新浮动栏高光显示
                    SetCurrentToolMode(InkCanvasEditingMode.Select);
                    UpdateCurrentToolMode("select");
                    HideSubPanels("select");
                }
            }
        }

        // 初始化元素的TransformGroup
        private void InitializeElementTransform(FrameworkElement element)
        {
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new ScaleTransform(1, 1));
            transformGroup.Children.Add(new TranslateTransform(0, 0));
            transformGroup.Children.Add(new RotateTransform(0));
            element.RenderTransform = transformGroup;
        }

        // 绑定元素事件处理器
        private void BindElementEvents(FrameworkElement element)
        {
            // 鼠标事件
            element.MouseLeftButtonDown += Element_MouseLeftButtonDown;
            element.MouseLeftButtonUp += Element_MouseLeftButtonUp;
            element.MouseMove += Element_MouseMove;
            element.MouseWheel += Element_MouseWheel;

            // 触摸事件
            element.IsManipulationEnabled = true;
            element.ManipulationDelta += Element_ManipulationDelta;
            element.ManipulationCompleted += Element_ManipulationCompleted;

            // 设置光标
            element.Cursor = Cursors.Hand;

            // 禁用InkCanvas对图片的选择处理
            element.IsHitTestVisible = true;
            element.Focusable = false;
        }

        // 鼠标左键按下事件
        private void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                // 取消之前选中的元素
                if (currentSelectedElement != null && currentSelectedElement != element)
                {
                    // 保存当前编辑模式
                    var previousEditingMode = inkCanvas.EditingMode;
                    UnselectElement(currentSelectedElement);
                    // 恢复编辑模式
                    inkCanvas.EditingMode = previousEditingMode;
                }

                // 选中当前元素
                SelectElement(element);

                // 开始拖动
                isDragging = true;
                dragStartPoint = e.GetPosition(inkCanvas);
                element.CaptureMouse();
                element.Cursor = Cursors.SizeAll;

                e.Handled = true;
            }
        }

        // 鼠标左键释放事件
        private void Element_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                isDragging = false;
                element.ReleaseMouseCapture();
                element.Cursor = Cursors.Hand;

                e.Handled = true;
            }
        }

        // 触摸释放事件
        private void Element_TouchUp(object sender, TouchEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                isDragging = false;
                element.ReleaseTouchCapture(e.TouchDevice);
                element.Cursor = Cursors.Hand;

                e.Handled = true;
            }
        }

        // 鼠标移动事件
        private void Element_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && isDragging && element.IsMouseCaptured)
            {
                var currentPoint = e.GetPosition(inkCanvas);

                // 使用鼠标拖动的完整实现机制
                ApplyMouseDragTransform(element, currentPoint, dragStartPoint);

                // 如果是图片元素，更新工具栏位置
                if (element is Image && BorderImageSelectionControl?.Visibility == Visibility.Visible)
                {
                    UpdateImageSelectionToolbarPosition(element);
                }

                // 如果是图片元素，更新选择点位置
                if (element is Image && ImageResizeHandlesCanvas?.Visibility == Visibility.Visible)
                {
                    UpdateImageResizeHandlesPosition(GetElementActualBounds(element));
                }

                dragStartPoint = currentPoint;
                e.Handled = true;
            }
        }

        // 鼠标滚轮事件 - 缩放
        private void Element_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is FrameworkElement element)
            {


                // 使用滚轮缩放的核心机制
                ApplyWheelScaleTransform(element, e);

                // 如果是图片元素，更新工具栏位置
                if (element is Image && BorderImageSelectionControl?.Visibility == Visibility.Visible)
                {
                    UpdateImageSelectionToolbarPosition(element);
                }

                // 如果是图片元素，更新选择点位置
                if (element is Image && ImageResizeHandlesCanvas?.Visibility == Visibility.Visible)
                {
                    UpdateImageResizeHandlesPosition(GetElementActualBounds(element));
                }

                e.Handled = true;
            }
        }

        // 触摸按下事件
        private void Element_TouchDown(object sender, TouchEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                // 取消之前选中的元素
                if (currentSelectedElement != null && currentSelectedElement != element)
                {
                    // 保存当前编辑模式
                    var previousEditingMode = inkCanvas.EditingMode;
                    UnselectElement(currentSelectedElement);
                    // 恢复编辑模式
                    inkCanvas.EditingMode = previousEditingMode;
                }

                // 选中当前元素
                SelectElement(element);

                // 开始拖动
                isDragging = true;
                dragStartPoint = e.GetTouchPoint(inkCanvas).Position;
                element.CaptureTouch(e.TouchDevice);
                element.Cursor = Cursors.SizeAll;

                e.Handled = true;
            }
        }

        // 触摸操作事件
        private void Element_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                // 检查是否是双指手势
                if (e.Manipulators.Count() >= 2)
                {
                    // 双指手势时，不处理单个元素的手势，让画布级别的手势处理
                    // 这样可以实现图片与墨迹的同步移动
                    e.Handled = false;
                    return;
                }

                // 单指手势时，使用触摸拖动的完整实现
                ApplyTouchManipulationTransform(element, e);

                // 如果是图片元素，更新工具栏位置
                if (element is Image && BorderImageSelectionControl?.Visibility == Visibility.Visible)
                {
                    UpdateImageSelectionToolbarPosition(element);
                }

                // 如果是图片元素，更新选择点位置
                if (element is Image && ImageResizeHandlesCanvas?.Visibility == Visibility.Visible)
                {
                    UpdateImageResizeHandlesPosition(GetElementActualBounds(element));
                }

                e.Handled = true;
            }
        }

        // 触摸操作完成事件
        private void Element_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            // 可以在这里添加操作完成后的处理逻辑
        }

        // 应用平移变换
        private void ApplyTranslateTransform(FrameworkElement element, double deltaX, double deltaY)
        {
            if (element.RenderTransform is TransformGroup transformGroup)
            {
                var translateTransform = transformGroup.Children.OfType<TranslateTransform>().FirstOrDefault();
                if (translateTransform != null)
                {
                    translateTransform.X += deltaX;
                    translateTransform.Y += deltaY;
                }
            }
        }

        // 应用缩放变换
        private void ApplyScaleTransform(FrameworkElement element, double scaleFactor, Point center)
        {
            if (element.RenderTransform is TransformGroup transformGroup)
            {
                var scaleTransform = transformGroup.Children.OfType<ScaleTransform>().FirstOrDefault();
                if (scaleTransform != null)
                {
                    // 设置缩放中心
                    scaleTransform.CenterX = center.X;
                    scaleTransform.CenterY = center.Y;

                    // 应用缩放
                    scaleTransform.ScaleX *= scaleFactor;
                    scaleTransform.ScaleY *= scaleFactor;

                    // 限制缩放范围
                    scaleTransform.ScaleX = Math.Max(0.1, Math.Min(scaleTransform.ScaleX, 5.0));
                    scaleTransform.ScaleY = Math.Max(0.1, Math.Min(scaleTransform.ScaleY, 5.0));
                }
            }
        }

        // 应用旋转变换
        private void ApplyRotateTransform(FrameworkElement element, double angle)
        {
            if (element.RenderTransform is TransformGroup transformGroup)
            {
                var rotateTransform = transformGroup.Children.OfType<RotateTransform>().FirstOrDefault();
                if (rotateTransform != null)
                {
                    rotateTransform.Angle += angle;
                }
            }
        }

        // 选中元素
        private void SelectElement(FrameworkElement element)
        {
            currentSelectedElement = element;

            // 根据元素类型显示不同的选择工具栏
            if (element is Image)
            {
                // 显示图片选择工具栏并设置位置
                if (BorderImageSelectionControl != null)
                {
                    // 计算工具栏位置
                    UpdateImageSelectionToolbarPosition(element);
                    BorderImageSelectionControl.Visibility = Visibility.Visible;
                }

                // 显示图片缩放选择点
                ShowImageResizeHandles(element);

                // 墨迹选择工具栏通过GridInkCanvasSelectionCover的可见性来控制
                // 不需要直接设置BorderStrokeSelectionControl.Visibility
            }
            else
            {
                // 隐藏图片选择工具栏
                if (BorderImageSelectionControl != null)
                {
                    BorderImageSelectionControl.Visibility = Visibility.Collapsed;
                }

                // 隐藏图片缩放选择点
                HideImageResizeHandles();

                // 墨迹选择工具栏通过GridInkCanvasSelectionCover的可见性来控制
                // 不需要直接设置BorderStrokeSelectionControl.Visibility
            }

            // 确保选择框不显示，避免蓝色边框
            if (GridInkCanvasSelectionCover != null)
            {
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
            }

            // 禁用InkCanvas的选择功能，去除控制点
            if (inkCanvas != null)
            {
                // 清除当前选择
                inkCanvas.Select(new StrokeCollection());
                // 保持选择模式，这样用户可以直接点击墨迹来选择
                inkCanvas.EditingMode = InkCanvasEditingMode.Select;
            }
        }

        // 取消选中元素
        private void UnselectElement(FrameworkElement element)
        {
            // 去除选中效果

            // 隐藏图片选择工具栏
            if (BorderImageSelectionControl != null)
            {
                BorderImageSelectionControl.Visibility = Visibility.Collapsed;
            }

            // 隐藏图片缩放选择点
            HideImageResizeHandles();

            // 墨迹选择工具栏通过GridInkCanvasSelectionCover的可见性来控制
            // 不需要直接设置BorderStrokeSelectionControl.Visibility

            // 确保选择框隐藏
            if (GridInkCanvasSelectionCover != null)
            {
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
            }

            // 确保InkCanvas处于选择模式，这样用户可以直接点击墨迹来选择
            if (inkCanvas != null)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.Select;
            }
        }

        // 应用矩阵变换到元素
        private void ApplyElementMatrixTransform(FrameworkElement element, Matrix matrix)
        {
            if (element.RenderTransform is TransformGroup transformGroup)
            {
                // 创建MatrixTransform
                var matrixTransform = new MatrixTransform(matrix);

                // 将MatrixTransform添加到TransformGroup
                transformGroup.Children.Add(matrixTransform);
            }
        }

        // 滚轮缩放的核心机制
        private void ApplyWheelScaleTransform(FrameworkElement element, MouseWheelEventArgs e)
        {
            try
            {
                // 根据滚轮方向确定缩放比例（向上1.1倍，向下0.9倍）
                double scaleFactor = e.Delta > 0 ? 1.1 : 0.9;

                // 计算选中元素的中心点作为缩放中心
                var elementCenter = new Point(element.ActualWidth / 2, element.ActualHeight / 2);

                // 创建 Matrix 对象并应用 ScaleAt 变换
                var matrix = new Matrix();
                matrix.ScaleAt(scaleFactor, scaleFactor, elementCenter.X, elementCenter.Y);

                // 对选中的图片元素调用 ApplyElementMatrixTransform
                ApplyElementMatrixTransform(element, matrix);

                // 对选中的笔画应用 Transform 方法（如果有选中的笔画）
                var selectedStrokes = inkCanvas.GetSelectedStrokes();
                foreach (var stroke in selectedStrokes)
                {
                    stroke.Transform(matrix, false);
                }


            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"滚轮缩放失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 矩阵变换的完整实现
        private void ApplyMatrixTransformToElement(FrameworkElement element, Matrix matrix, bool saveHistory = true)
        {
            try
            {
                // 获取元素的 RenderTransform，如果不存在则创建新的 TransformGroup
                TransformGroup transformGroup = element.RenderTransform as TransformGroup;
                if (transformGroup == null)
                {
                    transformGroup = new TransformGroup();
                    element.RenderTransform = transformGroup;
                }

                // 保存初始变换状态用于历史记录
                var initialTransform = transformGroup.Clone();

                // 创建新的 TransformGroup 并添加 MatrixTransform
                var newTransformGroup = new TransformGroup();
                newTransformGroup.Children.Add(new MatrixTransform(matrix));

                // 将新的变换组添加到现有的变换组中
                transformGroup.Children.Add(newTransformGroup);

                // 如果启用了历史记录，提交变换历史
                if (saveHistory)
                {
                    CommitTransformHistory(element, initialTransform, transformGroup);
                }


            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"矩阵变换失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 鼠标拖动的完整实现机制
        private void ApplyMouseDragTransform(FrameworkElement element, Point currentPoint, Point startPoint)
        {
            try
            {
                // 计算鼠标移动的位移向量
                var delta = currentPoint - startPoint;

                // 创建 Matrix 对象并应用 Translate 变换
                var matrix = new Matrix();
                matrix.Translate(delta.X, delta.Y);

                // 对选中的图片元素应用矩阵变换
                ApplyMatrixTransformToElement(element, matrix, false);

                // 对选中的笔画应用变换
                var selectedStrokes = inkCanvas.GetSelectedStrokes();
                foreach (var stroke in selectedStrokes)
                {
                    stroke.Transform(matrix, false);
                }

                // 更新选择框的位置（如果有选择框）
                UpdateSelectionBorderPosition(delta);


            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"鼠标拖动失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 更新选择框位置
        private void UpdateSelectionBorderPosition(Vector delta)
        {
            try
            {
                // 这里可以添加更新选择框位置的逻辑
                // 例如更新 BorderStrokeSelectionControl 的位置
                if (BorderStrokeSelectionControl != null)
                {
                    var currentMargin = BorderStrokeSelectionControl.Margin;
                    BorderStrokeSelectionControl.Margin = new Thickness(
                        currentMargin.Left + delta.X,
                        currentMargin.Top + delta.Y,
                        currentMargin.Right,
                        currentMargin.Bottom
                    );
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新选择框位置失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 提交变换历史
        private void CommitTransformHistory(FrameworkElement element, TransformGroup initialTransform, TransformGroup finalTransform)
        {
            try
            {
                // 这里可以添加提交变换历史到时间机器的逻辑
                // 例如记录变换前后的状态
                LogHelper.WriteLogToFile($"变换历史已记录: 元素={element.Name}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"提交变换历史失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 触摸拖动的完整实现
        private void ApplyTouchManipulationTransform(FrameworkElement element, ManipulationDeltaEventArgs e)
        {
            try
            {
                var md = e.DeltaManipulation;
                var matrix = new Matrix();

                // 支持单指拖动和多指手势
                // 可以同时进行平移、旋转和缩放

                // 通过 ManipulationDelta 获取手势变化信息
                var translation = md.Translation;
                var rotation = md.Rotation;
                var scale = md.Scale;

                // 应用平移
                if (translation.X != 0 || translation.Y != 0)
                {
                    matrix.Translate(translation.X, translation.Y);
                }

                // 支持两指缩放和旋转操作
                if (e.Manipulators.Count() >= 2)
                {
                    var center = e.ManipulationOrigin;

                    // 应用缩放
                    if (scale.X != 1.0 || scale.Y != 1.0)
                    {
                        matrix.ScaleAt(scale.X, scale.Y, center.X, center.Y);
                    }

                    // 应用旋转
                    if (rotation != 0)
                    {
                        matrix.RotateAt(rotation, center.X, center.Y);
                    }
                }

                // 应用变换到元素
                ApplyMatrixTransformToElement(element, matrix, false);

                // 应用变换到选中的笔画
                var selectedStrokes = inkCanvas.GetSelectedStrokes();
                foreach (var stroke in selectedStrokes)
                {
                    stroke.Transform(matrix, false);
                }


            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"触摸操作失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private async Task<Image> CreateAndCompressImageAsync(string filePath)
        {
            string savePath = Path.Combine(Settings.Automation.AutoSavedStrokesLocation, "File Dependency");
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            string fileExtension = Path.GetExtension(filePath);
            string timestamp = "img_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
            string newFilePath = Path.Combine(savePath, timestamp + fileExtension);

            await Task.Run(() => File.Copy(filePath, newFilePath, true));

            return await Dispatcher.InvokeAsync(() =>
            {
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri(newFilePath);
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();

                int width = bitmapImage.PixelWidth;
                int height = bitmapImage.PixelHeight;

                Image image = new Image();
                // 设置拉伸模式为Fill，支持任意比例缩放
                image.Stretch = Stretch.Fill;

                if (isLoaded && Settings.Canvas.IsCompressPicturesUploaded && (width > 1920 || height > 1080))
                {
                    double scaleX = 1920.0 / width;
                    double scaleY = 1080.0 / height;
                    double scale = Math.Min(scaleX, scaleY);

                    TransformedBitmap transformedBitmap = new TransformedBitmap(bitmapImage, new ScaleTransform(scale, scale));

                    image.Source = transformedBitmap;
                    image.Width = transformedBitmap.PixelWidth;
                    image.Height = transformedBitmap.PixelHeight;
                }
                else
                {
                    image.Source = bitmapImage;
                    image.Width = width;
                    image.Height = height;
                }

                return image;
            });
        }
        #endregion

        #region Media
        private async void BtnMediaInsert_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Media files (*.mp4; *.avi; *.wmv)|*.mp4;*.avi;*.wmv";

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;

                byte[] mediaBytes = await Task.Run(() => File.ReadAllBytes(filePath));

                MediaElement mediaElement = await CreateMediaElementAsync(filePath);

                if (mediaElement != null)
                {
                    CenterAndScaleElement(mediaElement);

                    InkCanvas.SetLeft(mediaElement, 0);
                    InkCanvas.SetTop(mediaElement, 0);
                    inkCanvas.Children.Add(mediaElement);

                    mediaElement.LoadedBehavior = MediaState.Manual;
                    mediaElement.UnloadedBehavior = MediaState.Manual;
                    mediaElement.Loaded += async (_, args) =>
                    {
                        mediaElement.Play();
                        await Task.Delay(100);
                        mediaElement.Pause();
                    };

                    timeMachine.CommitElementInsertHistory(mediaElement);
                }
            }
        }

        private async Task<MediaElement> CreateMediaElementAsync(string filePath)
        {
            string savePath = Path.Combine(Settings.Automation.AutoSavedStrokesLocation, "File Dependency");
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }
            return await Dispatcher.InvokeAsync(() =>
            {
                MediaElement mediaElement = new MediaElement();
                mediaElement.Source = new Uri(filePath);
                string timestamp = "media_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
                mediaElement.Name = timestamp;
                mediaElement.LoadedBehavior = MediaState.Manual;
                mediaElement.UnloadedBehavior = MediaState.Manual;

                mediaElement.Width = 256;
                mediaElement.Height = 256;

                string fileExtension = Path.GetExtension(filePath);
                string newFilePath = Path.Combine(savePath, mediaElement.Name + fileExtension);

                File.Copy(filePath, newFilePath, true);

                mediaElement.Source = new Uri(newFilePath);

                return mediaElement;
            });
        }
        #endregion

        #region Image Operations

        /// <summary>
        /// 旋转图片
        /// </summary>
        /// <param name="image">要旋转的图片</param>
        /// <param name="angle">旋转角度（正数为顺时针，负数为逆时针）</param>
        private void RotateImage(Image image, double angle)
        {
            if (image == null) return;

            try
            {
                // 获取当前的变换
                var transformGroup = image.RenderTransform as TransformGroup ?? new TransformGroup();

                // 查找现有的旋转变换
                RotateTransform rotateTransform = null;
                foreach (Transform transform in transformGroup.Children)
                {
                    if (transform is RotateTransform rt)
                    {
                        rotateTransform = rt;
                        break;
                    }
                }

                // 如果没有旋转变换，创建一个新的
                if (rotateTransform == null)
                {
                    rotateTransform = new RotateTransform();
                    transformGroup.Children.Add(rotateTransform);
                }

                // 设置旋转中心为图片中心
                rotateTransform.CenterX = image.ActualWidth / 2;
                rotateTransform.CenterY = image.ActualHeight / 2;

                // 累加旋转角度
                rotateTransform.Angle = (rotateTransform.Angle + angle) % 360;

                // 应用变换
                image.RenderTransform = transformGroup;

                // 提交到时间机器以支持撤销
                // 注意：旋转操作目前不支持撤销，因为需要更复杂的历史记录机制
            }
            catch (Exception ex)
            {
                // 记录错误但不中断程序
                Debug.WriteLine($"旋转图片时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 克隆图片
        /// </summary>
        /// <param name="image">要克隆的图片</param>
        private Image CloneImage(Image image)
        {
            if (image == null) return null;

            try
            {
                // 创建图片的副本
                var clonedImage = new Image
                {
                    Source = image.Source,
                    Width = image.Width,
                    Height = image.Height,
                    Stretch = image.Stretch,
                    RenderTransform = image.RenderTransform?.Clone()
                };

                // 设置位置，稍微偏移以避免重叠
                InkCanvas.SetLeft(clonedImage, InkCanvas.GetLeft(image) + 20);
                InkCanvas.SetTop(clonedImage, InkCanvas.GetTop(image) + 20);

                // 设置图片属性，避免被InkCanvas选择系统处理
                clonedImage.IsHitTestVisible = true;
                clonedImage.Focusable = false;

                // 初始化变换
                InitializeElementTransform(clonedImage);

                // 绑定事件
                BindElementEvents(clonedImage);

                // 添加到画布
                inkCanvas.Children.Add(clonedImage);

                // 提交到时间机器以支持撤销
                timeMachine.CommitElementInsertHistory(clonedImage);

                return clonedImage;
            }
            catch (Exception ex)
            {
                // 记录错误但不中断程序
                LogHelper.WriteLogToFile($"克隆图片时发生错误: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        /// <summary>
        /// 克隆图片到新页面
        /// </summary>
        /// <param name="image">要克隆的图片</param>
        private void CloneImageToNewBoard(Image image)
        {
            if (image == null) return;

            try
            {
                // 创建图片的副本
                var clonedImage = new Image
                {
                    Source = image.Source,
                    Width = image.Width,
                    Height = image.Height,
                    Stretch = image.Stretch,
                    RenderTransform = image.RenderTransform?.Clone()
                };

                // 设置位置，稍微偏移以避免重叠
                InkCanvas.SetLeft(clonedImage, InkCanvas.GetLeft(image) + 20);
                InkCanvas.SetTop(clonedImage, InkCanvas.GetTop(image) + 20);

                // 创建新页面
                BtnWhiteBoardAdd_Click(null, null);

                // 添加到新页面的画布
                inkCanvas.Children.Add(clonedImage);

                // 提交到时间机器以支持撤销
                timeMachine.CommitElementInsertHistory(clonedImage);
            }
            catch (Exception ex)
            {
                // 记录错误但不中断程序
                Debug.WriteLine($"克隆图片到新页面时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 缩放图片
        /// </summary>
        /// <param name="image">要缩放的图片</param>
        /// <param name="scaleFactor">缩放因子（大于1为放大，小于1为缩小）</param>
        private void ScaleImage(Image image, double scaleFactor)
        {
            if (image == null) return;

            try
            {
                // 获取当前的变换
                var transformGroup = image.RenderTransform as TransformGroup ?? new TransformGroup();

                // 查找现有的缩放变换
                ScaleTransform scaleTransform = null;
                foreach (Transform transform in transformGroup.Children)
                {
                    if (transform is ScaleTransform st)
                    {
                        scaleTransform = st;
                        break;
                    }
                }

                // 如果没有缩放变换，创建一个新的
                if (scaleTransform == null)
                {
                    scaleTransform = new ScaleTransform();
                    transformGroup.Children.Add(scaleTransform);
                }

                // 设置缩放中心为图片中心
                scaleTransform.CenterX = image.ActualWidth / 2;
                scaleTransform.CenterY = image.ActualHeight / 2;

                // 应用缩放因子
                scaleTransform.ScaleX *= scaleFactor;
                scaleTransform.ScaleY *= scaleFactor;

                // 应用变换
                image.RenderTransform = transformGroup;

                // 提交到时间机器以支持撤销
                // 注意：缩放操作目前不支持撤销，因为需要更复杂的历史记录机制
            }
            catch (Exception ex)
            {
                // 记录错误但不中断程序
                Debug.WriteLine($"缩放图片时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 删除图片
        /// </summary>
        /// <param name="image">要删除的图片</param>
        private void DeleteImage(Image image)
        {
            if (image == null) return;

            try
            {
                // 从画布中移除图片
                if (inkCanvas.Children.Contains(image))
                {
                    inkCanvas.Children.Remove(image);

                    // 提交到时间机器以支持撤销
                    timeMachine.CommitElementRemoveHistory(image);
                }
            }
            catch (Exception ex)
            {
                // 记录错误但不中断程序
                Debug.WriteLine($"删除图片时发生错误: {ex.Message}");
            }
        }

        #endregion

        private void CenterAndScaleElement(FrameworkElement element)
        {
            try
            {
                // 确保元素已加载且有有效尺寸
                if (element == null || element.ActualWidth <= 0 || element.ActualHeight <= 0)
                {
                    // 如果元素尺寸无效，等待加载完成后再处理
                    element.Loaded += (sender, e) =>
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            CenterAndScaleElement(element);
                        }), DispatcherPriority.Loaded);
                    };
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

                // 计算最大允许尺寸（画布的70%）
                double maxWidth = canvasWidth * 0.7;
                double maxHeight = canvasHeight * 0.7;

                // 获取元素的当前尺寸
                double elementWidth = element.ActualWidth;
                double elementHeight = element.ActualHeight;

                // 计算缩放比例
                double scaleX = maxWidth / elementWidth;
                double scaleY = maxHeight / elementHeight;
                double scale = Math.Min(scaleX, scaleY);

                // 如果元素本身比最大尺寸小，不进行缩放
                if (scale > 1.0)
                {
                    scale = 1.0;
                }

                // 计算新的尺寸
                double newWidth = elementWidth * scale;
                double newHeight = elementHeight * scale;

                // 设置元素尺寸
                element.Width = newWidth;
                element.Height = newHeight;

                // 计算居中位置
                double centerX = (canvasWidth - newWidth) / 2;
                double centerY = (canvasHeight - newHeight) / 2;

                // 确保位置不为负数
                centerX = Math.Max(0, centerX);
                centerY = Math.Max(0, centerY);

                // 设置位置
                InkCanvas.SetLeft(element, centerX);
                InkCanvas.SetTop(element, centerY);

                // 保持TransformGroup，不清除RenderTransform
                // 这样可以保持滚轮缩放和拖动功能
                if (element.RenderTransform == null || element.RenderTransform == Transform.Identity)
                {
                    // 只有在没有TransformGroup时才创建
                    InitializeElementTransform(element);
                }

                LogHelper.WriteLogToFile($"元素居中完成: 位置({centerX}, {centerY}), 尺寸({newWidth}x{newHeight})");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"元素居中失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 初始化InkCanvas选择设置
        private void InitializeInkCanvasSelectionSettings()
        {
            if (inkCanvas != null)
            {
                // 清除当前选择，避免显示控制点
                inkCanvas.Select(new StrokeCollection());
                // 设置编辑模式为非选择模式
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
            }
        }

        // 更新图片选择工具栏位置
        private void UpdateImageSelectionToolbarPosition(FrameworkElement element)
        {
            try
            {
                if (BorderImageSelectionControl == null || element == null) return;

                // 获取元素的实际边界（考虑变换）
                Rect elementBounds = GetElementActualBounds(element);

                // 计算工具栏位置（显示在图片下方）
                double toolbarLeft = elementBounds.Left + (elementBounds.Width / 2) - (BorderImageSelectionControl.ActualWidth / 2);
                double toolbarTop = elementBounds.Bottom + 10; // 图片下方10像素

                // 确保工具栏不超出画布边界
                double maxLeft = inkCanvas.ActualWidth - BorderImageSelectionControl.ActualWidth;
                double maxTop = inkCanvas.ActualHeight - BorderImageSelectionControl.ActualHeight;

                toolbarLeft = Math.Max(0, Math.Min(toolbarLeft, maxLeft));
                toolbarTop = Math.Max(0, Math.Min(toolbarTop, maxTop));

                // 设置工具栏位置
                BorderImageSelectionControl.Margin = new Thickness(toolbarLeft, toolbarTop, 0, 0);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新图片选择工具栏位置失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 获取元素的实际边界（考虑变换）
        private Rect GetElementActualBounds(FrameworkElement element)
        {
            try
            {
                var left = InkCanvas.GetLeft(element);
                var top = InkCanvas.GetTop(element);

                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;

                var width = element.ActualWidth > 0 ? element.ActualWidth : element.Width;
                var height = element.ActualHeight > 0 ? element.ActualHeight : element.Height;

                // 检查是否有RenderTransform
                if (element.RenderTransform != null && element.RenderTransform != Transform.Identity)
                {
                    try
                    {
                        // 如果有变换，使用变换后的边界
                        var transform = element.TransformToAncestor(inkCanvas);
                        var elementBounds = new Rect(0, 0, width, height);
                        var transformedBounds = transform.TransformBounds(elementBounds);
                        return transformedBounds;
                    }
                    catch
                    {
                        // 变换失败时回退到简单计算
                        return new Rect(left, top, width, height);
                    }
                }

                // 没有变换时直接使用位置和大小
                return new Rect(left, top, width, height);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取元素实际边界失败: {ex.Message}", LogHelper.LogType.Error);
                // 回退到基本计算
                var left = InkCanvas.GetLeft(element);
                var top = InkCanvas.GetTop(element);
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;
                return new Rect(left, top, element.ActualWidth, element.ActualHeight);
            }
        }

        #region Image Selection Toolbar Event Handlers

        // 图片克隆功能
        private void BorderImageClone_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (currentSelectedElement is Image originalImage)
                {
                    // 创建克隆图片
                    Image clonedImage = CloneImage(originalImage);

                    // 添加到画布
                    inkCanvas.Children.Add(clonedImage);

                    // 初始化变换
                    InitializeElementTransform(clonedImage);

                    // 绑定事件
                    BindElementEvents(clonedImage);

                    // 记录历史
                    timeMachine.CommitElementInsertHistory(clonedImage);

                    LogHelper.WriteLogToFile($"图片克隆完成: {clonedImage.Name}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"图片克隆失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 图片克隆到新页面
        private void BorderImageCloneToNewBoard_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (currentSelectedElement is Image originalImage)
                {
                    // 创建新页面
                    BtnWhiteBoardAdd_Click(null, null);

                    // 创建克隆图片（不添加到当前画布，因为已经创建了新页面）
                    Image clonedImage = CreateClonedImage(originalImage);

                    if (clonedImage != null)
                    {
                        // 设置图片属性，避免被InkCanvas选择系统处理
                        clonedImage.IsHitTestVisible = true;
                        clonedImage.Focusable = false;

                        // 初始化变换
                        InitializeElementTransform(clonedImage);

                        // 绑定事件
                        BindElementEvents(clonedImage);

                        // 添加到新页面的画布
                        inkCanvas.Children.Add(clonedImage);

                        // 记录历史
                        timeMachine.CommitElementInsertHistory(clonedImage);

                        LogHelper.WriteLogToFile($"图片克隆到新页面完成: {clonedImage.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"图片克隆到新页面失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 图片左旋转
        private void BorderImageRotateLeft_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (currentSelectedElement != null)
                {
                    ApplyRotateTransform(currentSelectedElement, -45);

                    // 更新工具栏位置
                    if (currentSelectedElement is Image && BorderImageSelectionControl?.Visibility == Visibility.Visible)
                    {
                        UpdateImageSelectionToolbarPosition(currentSelectedElement);
                    }

                    LogHelper.WriteLogToFile("图片左旋转完成");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"图片左旋转失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 图片右旋转
        private void BorderImageRotateRight_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (currentSelectedElement != null)
                {
                    ApplyRotateTransform(currentSelectedElement, 45);

                    // 更新工具栏位置
                    if (currentSelectedElement is Image && BorderImageSelectionControl?.Visibility == Visibility.Visible)
                    {
                        UpdateImageSelectionToolbarPosition(currentSelectedElement);
                    }

                    LogHelper.WriteLogToFile("图片右旋转完成");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"图片右旋转失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 图片缩放减小
        private void GridImageScaleDecrease_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (currentSelectedElement != null)
                {
                    var elementCenter = new Point(currentSelectedElement.ActualWidth / 2, currentSelectedElement.ActualHeight / 2);
                    ApplyScaleTransform(currentSelectedElement, 0.9, elementCenter);

                    // 更新工具栏位置
                    if (currentSelectedElement is Image && BorderImageSelectionControl?.Visibility == Visibility.Visible)
                    {
                        UpdateImageSelectionToolbarPosition(currentSelectedElement);
                    }

                    LogHelper.WriteLogToFile("图片缩放减小完成");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"图片缩放减小失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 图片缩放增大
        private void GridImageScaleIncrease_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (currentSelectedElement != null)
                {
                    var elementCenter = new Point(currentSelectedElement.ActualWidth / 2, currentSelectedElement.ActualHeight / 2);
                    ApplyScaleTransform(currentSelectedElement, 1.1, elementCenter);

                    // 更新工具栏位置
                    if (currentSelectedElement is Image && BorderImageSelectionControl?.Visibility == Visibility.Visible)
                    {
                        UpdateImageSelectionToolbarPosition(currentSelectedElement);
                    }

                    LogHelper.WriteLogToFile("图片缩放增大完成");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"图片缩放增大失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 图片删除
        private void BorderImageDelete_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (currentSelectedElement != null)
                {
                    // 保存删除前的编辑模式
                    var previousEditingMode = inkCanvas.EditingMode;

                    // 记录删除历史
                    timeMachine.CommitElementRemoveHistory(currentSelectedElement);

                    // 从画布中移除
                    inkCanvas.Children.Remove(currentSelectedElement);

                    // 清除选中状态
                    UnselectElement(currentSelectedElement);
                    currentSelectedElement = null;

                    // 恢复到删除前的编辑模式
                    inkCanvas.EditingMode = previousEditingMode;

                    LogHelper.WriteLogToFile($"图片删除完成，已恢复到编辑模式: {previousEditingMode}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"图片删除失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 克隆图片的辅助方法（只创建图片，不添加到画布）
        private Image CreateClonedImage(Image originalImage)
        {
            try
            {
                Image clonedImage = new Image();

                // 复制图片源
                if (originalImage.Source is BitmapSource bitmapSource)
                {
                    clonedImage.Source = bitmapSource;
                }

                // 复制属性
                clonedImage.Width = originalImage.Width;
                clonedImage.Height = originalImage.Height;
                clonedImage.Stretch = originalImage.Stretch;
                clonedImage.StretchDirection = originalImage.StretchDirection;

                // 复制位置（在新页面中居中显示）
                double left = InkCanvas.GetLeft(originalImage);
                double top = InkCanvas.GetTop(originalImage);
                InkCanvas.SetLeft(clonedImage, left + 20); // 稍微偏移位置
                InkCanvas.SetTop(clonedImage, top + 20);

                // 复制变换
                if (originalImage.RenderTransform is TransformGroup originalTransformGroup)
                {
                    clonedImage.RenderTransform = originalTransformGroup.Clone();
                }

                // 设置名称
                string timestamp = "img_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
                clonedImage.Name = timestamp;

                return clonedImage;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"克隆图片失败: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        /// <summary>
        /// 克隆墨迹集合
        /// </summary>
        /// <param name="strokes">要克隆的墨迹集合</param>
        /// <returns>克隆后的墨迹集合</returns>
        private StrokeCollection CloneStrokes(StrokeCollection strokes)
        {
            if (strokes == null || strokes.Count == 0) return new StrokeCollection();

            try
            {
                // 创建墨迹集合的副本
                var clonedStrokes = strokes.Clone();

                // 为每个墨迹添加位置偏移以避免重叠
                foreach (var stroke in clonedStrokes)
                {
                    var offsetPoints = new StylusPointCollection();
                    foreach (var point in stroke.StylusPoints)
                    {
                        offsetPoints.Add(new StylusPoint(point.X + 20, point.Y + 20, point.PressureFactor));
                    }
                    stroke.StylusPoints = offsetPoints;
                }

                // 添加到画布
                inkCanvas.Strokes.Add(clonedStrokes);

                // 提交到时间机器以支持撤销
                timeMachine.CommitStrokeUserInputHistory(clonedStrokes);

                LogHelper.WriteLogToFile($"墨迹克隆完成: {clonedStrokes.Count} 个墨迹");
                return clonedStrokes;
            }
            catch (Exception ex)
            {
                // 记录错误但不中断程序
                LogHelper.WriteLogToFile($"克隆墨迹时发生错误: {ex.Message}", LogHelper.LogType.Error);
                return new StrokeCollection();
            }
        }

        /// <summary>
        /// 克隆墨迹集合到新页面
        /// </summary>
        /// <param name="strokes">要克隆的墨迹集合</param>
        private void CloneStrokesToNewBoard(StrokeCollection strokes)
        {
            if (strokes == null || strokes.Count == 0) return;

            try
            {
                // 创建墨迹集合的副本
                var clonedStrokes = strokes.Clone();

                // 为每个墨迹添加位置偏移以避免重叠
                foreach (var stroke in clonedStrokes)
                {
                    var offsetPoints = new StylusPointCollection();
                    foreach (var point in stroke.StylusPoints)
                    {
                        offsetPoints.Add(new StylusPoint(point.X + 20, point.Y + 20, point.PressureFactor));
                    }
                    stroke.StylusPoints = offsetPoints;
                }

                // 创建新页面
                BtnWhiteBoardAdd_Click(null, null);

                // 添加到新页面的画布
                inkCanvas.Strokes.Add(clonedStrokes);

                // 提交到时间机器以支持撤销
                timeMachine.CommitStrokeUserInputHistory(clonedStrokes);

                LogHelper.WriteLogToFile($"墨迹克隆到新页面完成: {clonedStrokes.Count} 个墨迹");
            }
            catch (Exception ex)
            {
                // 记录错误但不中断程序
                LogHelper.WriteLogToFile($"克隆墨迹到新页面时发生错误: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion

        #region Image Resize Handles

        // 图片缩放选择点相关变量
        private bool isResizingImage = false;
        private Point imageResizeStartPoint;
        private string activeResizeHandle = "";

        // 显示图片缩放选择点
        private void ShowImageResizeHandles(FrameworkElement element)
        {
            try
            {
                if (ImageResizeHandlesCanvas == null || element == null) return;

                // 获取元素的实际边界
                Rect elementBounds = GetElementActualBounds(element);

                // 设置选择点位置
                UpdateImageResizeHandlesPosition(elementBounds);

                // 显示选择点
                ImageResizeHandlesCanvas.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"显示图片缩放选择点失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 隐藏图片缩放选择点
        private void HideImageResizeHandles()
        {
            try
            {
                if (ImageResizeHandlesCanvas != null)
                {
                    ImageResizeHandlesCanvas.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"隐藏图片缩放选择点失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 更新图片缩放选择点位置
        private void UpdateImageResizeHandlesPosition(Rect elementBounds)
        {
            try
            {
                if (ImageResizeHandlesCanvas == null) return;

                ImageResizeHandlesCanvas.Margin = new Thickness(elementBounds.Left, elementBounds.Top, 0, 0);

                // 四个角控制点
                System.Windows.Controls.Canvas.SetLeft(ImageTopLeftHandle, -4);
                System.Windows.Controls.Canvas.SetTop(ImageTopLeftHandle, -4);

                System.Windows.Controls.Canvas.SetLeft(ImageTopRightHandle, elementBounds.Width - 4);
                System.Windows.Controls.Canvas.SetTop(ImageTopRightHandle, -4);

                System.Windows.Controls.Canvas.SetLeft(ImageBottomLeftHandle, -4);
                System.Windows.Controls.Canvas.SetTop(ImageBottomLeftHandle, elementBounds.Height - 4);

                System.Windows.Controls.Canvas.SetLeft(ImageBottomRightHandle, elementBounds.Width - 4);
                System.Windows.Controls.Canvas.SetTop(ImageBottomRightHandle, elementBounds.Height - 4);

                // 四个边控制点
                System.Windows.Controls.Canvas.SetLeft(ImageTopHandle, elementBounds.Width / 2 - 4);
                System.Windows.Controls.Canvas.SetTop(ImageTopHandle, -4);

                System.Windows.Controls.Canvas.SetLeft(ImageBottomHandle, elementBounds.Width / 2 - 4);
                System.Windows.Controls.Canvas.SetTop(ImageBottomHandle, elementBounds.Height - 4);

                System.Windows.Controls.Canvas.SetLeft(ImageLeftHandle, -4);
                System.Windows.Controls.Canvas.SetTop(ImageLeftHandle, elementBounds.Height / 2 - 4);

                System.Windows.Controls.Canvas.SetLeft(ImageRightHandle, elementBounds.Width - 4);
                System.Windows.Controls.Canvas.SetTop(ImageRightHandle, elementBounds.Height / 2 - 4);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新图片缩放选择点位置失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 图片缩放选择点鼠标按下事件
        private void ImageResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (currentSelectedElement is Image image && sender is Ellipse ellipse)
                {
                    isResizingImage = true;
                    imageResizeStartPoint = e.GetPosition(inkCanvas);
                    
                    // 确定是哪个控制点
                    activeResizeHandle = ellipse.Name;

                    // 捕获鼠标
                    ellipse.CaptureMouse();
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"图片缩放选择点鼠标按下事件失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 图片缩放选择点鼠标释放事件
        private void ImageResizeHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (isResizingImage && sender is Ellipse ellipse)
                {
                    isResizingImage = false;
                    ellipse.ReleaseMouseCapture();
                    activeResizeHandle = "";
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"图片缩放选择点鼠标释放事件失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 图片缩放选择点鼠标移动事件
        private void ImageResizeHandle_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (isResizingImage && currentSelectedElement is Image image && sender is Ellipse ellipse)
                {
                    var currentPoint = e.GetPosition(inkCanvas);
                    ResizeImageByHandle(image, imageResizeStartPoint, currentPoint, activeResizeHandle);
                    imageResizeStartPoint = currentPoint;
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"图片缩放选择点鼠标移动事件失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 根据控制点缩放图片
        private void ResizeImageByHandle(Image image, Point startPoint, Point currentPoint, string handleName)
        {
            try
            {
                if (image.RenderTransform is TransformGroup transformGroup)
                {
                    var scaleTransform = transformGroup.Children.OfType<ScaleTransform>().FirstOrDefault();
                    var translateTransform = transformGroup.Children.OfType<TranslateTransform>().FirstOrDefault();

                    if (scaleTransform == null || translateTransform == null) return;

                    // 获取图片的当前边界
                    Rect currentBounds = GetElementActualBounds(image);
                    double deltaX = currentPoint.X - startPoint.X;
                    double deltaY = currentPoint.Y - startPoint.Y;

                    // 计算缩放比例
                    double scaleX = 1.0;
                    double scaleY = 1.0;
                    double translateX = 0;
                    double translateY = 0;

                    switch (handleName)
                    {
                        case "ImageTopLeftHandle":
                            scaleX = (currentBounds.Width - deltaX) / currentBounds.Width;
                            scaleY = (currentBounds.Height - deltaY) / currentBounds.Height;
                            translateX = deltaX;
                            translateY = deltaY;
                            break;
                        case "ImageTopRightHandle":
                            scaleX = (currentBounds.Width + deltaX) / currentBounds.Width;
                            scaleY = (currentBounds.Height - deltaY) / currentBounds.Height;
                            translateY = deltaY;
                            break;
                        case "ImageBottomLeftHandle":
                            scaleX = (currentBounds.Width - deltaX) / currentBounds.Width;
                            scaleY = (currentBounds.Height + deltaY) / currentBounds.Height;
                            translateX = deltaX;
                            break;
                        case "ImageBottomRightHandle":
                            scaleX = (currentBounds.Width + deltaX) / currentBounds.Width;
                            scaleY = (currentBounds.Height + deltaY) / currentBounds.Height;
                            break;
                        case "ImageTopHandle":
                            scaleY = (currentBounds.Height - deltaY) / currentBounds.Height;
                            translateY = deltaY;
                            break;
                        case "ImageBottomHandle":
                            scaleY = (currentBounds.Height + deltaY) / currentBounds.Height;
                            break;
                        case "ImageLeftHandle":
                            scaleX = (currentBounds.Width - deltaX) / currentBounds.Width;
                            translateX = deltaX;
                            break;
                        case "ImageRightHandle":
                            scaleX = (currentBounds.Width + deltaX) / currentBounds.Width;
                            break;
                    }

                    // 限制缩放范围
                    scaleX = Math.Max(0.1, Math.Min(scaleX, 5.0));
                    scaleY = Math.Max(0.1, Math.Min(scaleY, 5.0));

                    // 应用缩放
                    scaleTransform.ScaleX *= scaleX;
                    scaleTransform.ScaleY *= scaleY;

                    // 应用平移
                    translateTransform.X += translateX;
                    translateTransform.Y += translateY;

                    // 更新选择点位置
                    UpdateImageResizeHandlesPosition(GetElementActualBounds(image));
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"根据控制点缩放图片失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion
    }
}
