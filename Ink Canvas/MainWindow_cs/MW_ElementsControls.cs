using Ink_Canvas.Controls;
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
        /// <summary>
        /// 当前选中的可操作元素
        /// </summary>
        private FrameworkElement currentSelectedElement;

        /// <summary>
        /// 是否正在拖动
        /// </summary>
        private bool isDragging;

        /// <summary>
        /// 拖动起始点
        /// </summary>
        private Point dragStartPoint;

        /// <summary>页码侧栏当前订阅 <see cref="PdfEmbeddedView.PageNavigationStateChanged"/> 的 PDF 视图。</summary>
        private PdfEmbeddedView _pdfPageSidebarEventSource;

        private bool _pdfSidebarPositionRefreshPending;

        /// <summary>为 true 时，下一次成功算出 PDF 边界后的侧栏定位使用宿主 Visual 变换（仅用于刚插入/恢复 PDF 的首帧对齐）。</summary>
        private bool _pdfSidebarNextPositionUseHostTransform;

        #region Image
        /// <summary>
        /// 处理图片插入按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 打开文件选择对话框，选择图片文件
        /// - 创建并压缩图片
        /// - 设置图片属性，避免被InkCanvas选择系统处理
        /// - 初始化InkCanvas选择设置
        /// - 添加图片到画布
        /// - 等待图片加载完成后进行后续处理
        /// - 初始化TransformGroup
        /// - 居中缩放图片
        /// - 绑定事件处理器
        /// - 提交到时间机器历史记录
        /// - 插入图片后切换到选择模式并刷新浮动栏高光显示
        /// </remarks>
        private async void BtnImageInsert_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "图片与 PDF|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.pdf|图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif|PDF|*.pdf";

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;

                FrameworkElement element = await CreateAndCompressImageAsync(filePath);

                if (element != null)
                {
                    string timestamp = "img_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
                    element.Name = timestamp;

                    // 设置图片属性，避免被InkCanvas选择系统处理
                    element.IsHitTestVisible = true;
                    element.Focusable = false;

                    // 初始化InkCanvas选择设置
                    InitializeInkCanvasSelectionSettings();

                    // 先添加到画布
                    inkCanvas.Children.Add(element);

                    // 等待图片加载完成后再进行后续处理
                    element.Loaded += (s, args) =>
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            // 初始化TransformGroup
                            InitializeElementTransform(element);

                            // 居中缩放
                            CenterAndScaleElement(element);

                            // 最后绑定事件处理器
                            BindElementEvents(element);

                            if (element is PdfEmbeddedView)
                                _pdfSidebarNextPositionUseHostTransform = true;
                            SyncPdfPageSidebarWithCanvas();

                            LogHelper.WriteLogToFile($"图片插入完成: {element.Name}");
                        }), DispatcherPriority.Loaded);
                    };

                    timeMachine.CommitElementInsertHistory(element);

                    // 插入图片后切换到选择模式并刷新浮动栏高光显示
                    SetCurrentToolMode(InkCanvasEditingMode.Select);
                    UpdateCurrentToolMode("select");
                    HideSubPanels("select");
                }
            }
        }

        /// <summary>
        /// 初始化元素的TransformGroup
        /// </summary>
        /// <param name="element">要初始化的元素</param>
        /// <remarks>
        /// - 创建TransformGroup
        /// - 添加ScaleTransform、TranslateTransform和RotateTransform
        /// - 设置元素的RenderTransform
        /// </remarks>
        private void InitializeElementTransform(FrameworkElement element)
        {
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new ScaleTransform(1, 1));
            transformGroup.Children.Add(new TranslateTransform(0, 0));
            transformGroup.Children.Add(new RotateTransform(0));
            element.RenderTransform = transformGroup;
        }

        /// <summary>
        /// 绑定元素事件处理器
        /// </summary>
        /// <param name="element">要绑定事件的元素</param>
        /// <remarks>
        /// - 绑定鼠标事件（MouseLeftButtonDown、MouseLeftButtonUp、MouseMove、MouseWheel）
        /// - 启用触摸操作
        /// - 绑定触摸事件（ManipulationDelta、ManipulationCompleted）
        /// - 设置光标为手形
        /// - 禁用InkCanvas对图片的选择处理
        /// </remarks>
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

        /// <summary>
        /// 处理元素鼠标左键按下事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 检查编辑模式是否为选择模式
        /// - 取消之前选中的元素
        /// - 选中当前元素
        /// - 开始拖动
        /// - 捕获鼠标
        /// - 设置光标为全尺寸光标
        /// </remarks>
        private void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                if (inkCanvas.EditingMode != InkCanvasEditingMode.Select)
                {
                    e.Handled = false;
                    return;
                }

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

        /// <summary>
        /// 处理元素鼠标左键释放事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 停止拖动
        /// - 释放鼠标捕获
        /// - 恢复光标为手形
        /// </remarks>
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

        /// <summary>
        /// 处理元素触摸释放事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 停止拖动
        /// - 释放触摸捕获
        /// - 恢复光标为手形
        /// </remarks>
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

        /// <summary>
        /// 处理元素鼠标移动事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 检查是否正在拖动且鼠标已捕获
        /// - 获取当前鼠标位置
        /// - 应用鼠标拖动变换
        /// - 如果是图片元素，更新工具栏位置
        /// - 如果是图片元素，更新选择点位置
        /// - 更新拖动起始点
        /// </remarks>
        private void Element_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && isDragging && element.IsMouseCaptured)
            {
                var currentPoint = e.GetPosition(inkCanvas);

                // 使用鼠标拖动的完整实现机制
                ApplyMouseDragTransform(element, currentPoint, dragStartPoint);

                // 如果是图片元素，更新工具栏位置
                if (IsBitmapLikeCanvasElement(element) && BorderImageSelectionControl?.Visibility == Visibility.Visible)
                {
                    UpdateImageSelectionToolbarPosition(element);
                }

                // 如果是图片元素，更新选择点位置
                if (IsBitmapLikeCanvasElement(element) && ImageResizeHandlesCanvas?.Visibility == Visibility.Visible)
                {
                    UpdateImageResizeHandlesPosition(GetElementActualBounds(element));
                }

                dragStartPoint = currentPoint;
                e.Handled = true;
            }
        }

        /// <summary>
        /// 处理元素鼠标滚轮事件 - 缩放
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 应用滚轮缩放变换
        /// - 如果是图片元素，更新工具栏位置
        /// - 如果是图片元素，更新选择点位置
        /// </remarks>
        private void Element_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is FrameworkElement element)
            {


                // 使用滚轮缩放的核心机制
                ApplyWheelScaleTransform(element, e);

                // 如果是图片元素，更新工具栏位置
                if (IsBitmapLikeCanvasElement(element) && BorderImageSelectionControl?.Visibility == Visibility.Visible)
                {
                    UpdateImageSelectionToolbarPosition(element);
                }

                // 如果是图片元素，更新选择点位置
                if (IsBitmapLikeCanvasElement(element) && ImageResizeHandlesCanvas?.Visibility == Visibility.Visible)
                {
                    UpdateImageResizeHandlesPosition(GetElementActualBounds(element));
                }

                e.Handled = true;
            }
        }

        /// <summary>
        /// 处理元素触摸按下事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 检查编辑模式是否为选择模式
        /// - 取消之前选中的元素
        /// - 选中当前元素
        /// - 开始拖动
        /// - 捕获触摸
        /// - 设置光标为全尺寸光标
        /// </remarks>
        private void Element_TouchDown(object sender, TouchEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                if (inkCanvas.EditingMode != InkCanvasEditingMode.Select)
                {
                    e.Handled = false;
                    return;
                }

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

        /// <summary>
        /// 处理元素触摸操作事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 检查是否是双指手势
        /// - 双指手势时，让画布级别的手势处理
        /// - 单指手势时，应用触摸拖动变换
        /// - 如果是图片元素，更新工具栏位置
        /// - 如果是图片元素，更新选择点位置
        /// </remarks>
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
                if (IsBitmapLikeCanvasElement(element) && BorderImageSelectionControl?.Visibility == Visibility.Visible)
                {
                    UpdateImageSelectionToolbarPosition(element);
                }

                // 如果是图片元素，更新选择点位置
                if (IsBitmapLikeCanvasElement(element) && ImageResizeHandlesCanvas?.Visibility == Visibility.Visible)
                {
                    UpdateImageResizeHandlesPosition(GetElementActualBounds(element));
                }

                e.Handled = true;
            }
        }

        /// <summary>
        /// 处理元素触摸操作完成事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 可以在这里添加操作完成后的处理逻辑
        /// </remarks>
        private void Element_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            // 可以在这里添加操作完成后的处理逻辑
        }

        /// <summary>
        /// 应用平移变换到元素
        /// </summary>
        /// <param name="element">要变换的元素</param>
        /// <param name="deltaX">X轴偏移量</param>
        /// <param name="deltaY">Y轴偏移量</param>
        /// <remarks>
        /// - 获取元素的TransformGroup
        /// - 查找TranslateTransform
        /// - 应用平移变换
        /// </remarks>
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

        /// <summary>
        /// 应用缩放变换到元素
        /// </summary>
        /// <param name="element">要变换的元素</param>
        /// <param name="scaleFactor">缩放因子</param>
        /// <param name="center">缩放中心</param>
        /// <remarks>
        /// - 获取元素的TransformGroup
        /// - 查找ScaleTransform
        /// - 设置缩放中心
        /// - 应用缩放
        /// - 限制缩放范围（0.1到5.0）
        /// </remarks>
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

        /// <summary>
        /// 应用旋转变换到元素
        /// </summary>
        /// <param name="element">要变换的元素</param>
        /// <param name="angle">旋转角度</param>
        /// <remarks>
        /// - 获取元素的TransformGroup
        /// - 查找RotateTransform
        /// - 应用旋转变换
        /// </remarks>
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

        /// <summary>
        /// 选中元素
        /// </summary>
        /// <param name="element">要选中的元素</param>
        /// <remarks>
        /// - 设置当前选中元素
        /// - 根据元素类型显示不同的选择工具栏
        /// - 如果是图片元素，显示图片选择工具栏和缩放选择点
        /// - 如果不是图片元素，隐藏图片选择工具栏和缩放选择点
        /// - 确保选择框不显示，避免蓝色边框
        /// - 禁用InkCanvas的选择功能，去除控制点
        /// - 保持选择模式，这样用户可以直接点击墨迹来选择
        /// </remarks>
        private void SelectElement(FrameworkElement element)
        {
            currentSelectedElement = element;

            // 根据元素类型显示不同的选择工具栏
            if (IsBitmapLikeCanvasElement(element))
            {
                // 显示图片选择工具栏并设置位置
                if (BorderImageSelectionControl != null)
                {
                    // 计算工具栏位置（内部会同步 PDF 右侧栏位置）
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

            SyncPdfPageSidebarWithCanvas();
        }

        /// <summary>
        /// 取消选中元素
        /// </summary>
        /// <param name="element">要取消选中的元素</param>
        /// <remarks>
        /// - 隐藏图片选择工具栏
        /// - 隐藏图片缩放选择点
        /// - 确保选择框隐藏
        /// - 确保InkCanvas处于选择模式，这样用户可以直接点击墨迹来选择
        /// </remarks>
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

            SyncPdfPageSidebarWithCanvas();
        }

        /// <summary>
        /// 应用矩阵变换到元素
        /// </summary>
        /// <param name="element">要变换的元素</param>
        /// <param name="matrix">变换矩阵</param>
        /// <remarks>
        /// - 获取元素的RenderTransform，如果不存在则创建新的TransformGroup
        /// - 创建MatrixTransform
        /// - 将MatrixTransform添加到TransformGroup
        /// </remarks>
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

        /// <summary>
        /// 处理滚轮缩放的核心机制
        /// </summary>
        /// <param name="element">要缩放的元素</param>
        /// <param name="e">鼠标滚轮事件参数</param>
        /// <remarks>
        /// - 根据滚轮方向确定缩放比例（向上1.1倍，向下0.9倍）
        /// - 计算选中元素的中心点作为缩放中心
        /// - 创建 Matrix 对象并应用 ScaleAt 变换
        /// - 对选中的图片元素应用矩阵变换
        /// - 对选中的笔画应用 Transform 方法
        /// - 包含异常处理
        /// </remarks>
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

        /// <summary>
        /// 矩阵变换的完整实现
        /// </summary>
        /// <param name="element">要变换的元素</param>
        /// <param name="matrix">变换矩阵</param>
        /// <param name="saveHistory">是否保存历史记录</param>
        /// <remarks>
        /// - 获取元素的 RenderTransform，如果不存在则创建新的 TransformGroup
        /// - 保存初始变换状态用于历史记录
        /// - 创建新的 TransformGroup 并添加 MatrixTransform
        /// - 将新的变换组添加到现有的变换组中
        /// - 如果启用了历史记录，提交变换历史
        /// - 包含异常处理
        /// </remarks>
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

        /// <summary>
        /// 鼠标拖动的完整实现机制
        /// </summary>
        /// <param name="element">要拖动的元素</param>
        /// <param name="currentPoint">当前鼠标位置</param>
        /// <param name="startPoint">起始鼠标位置</param>
        /// <remarks>
        /// - 计算鼠标移动的位移向量
        /// - 创建 Matrix 对象并应用 Translate 变换
        /// - 对选中的图片元素应用矩阵变换
        /// - 对选中的笔画应用变换
        /// - 更新选择框的位置（如果有选择框）
        /// - 包含异常处理
        /// </remarks>
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

        /// <summary>
        /// 更新选择框位置
        /// </summary>
        /// <param name="delta">位移向量</param>
        /// <remarks>
        /// - 更新选择框位置的逻辑
        /// - 更新 BorderStrokeSelectionControl 的位置
        /// - 包含异常处理
        /// </remarks>
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

        /// <summary>
        /// 提交变换历史
        /// </summary>
        /// <param name="element">变换的元素</param>
        /// <param name="initialTransform">初始变换</param>
        /// <param name="finalTransform">最终变换</param>
        /// <remarks>
        /// - 提交变换历史到时间机器的逻辑
        /// - 记录变换前后的状态
        /// - 包含异常处理
        /// </remarks>
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

        /// <summary>
        /// 触摸拖动的完整实现
        /// </summary>
        /// <param name="element">要操作的元素</param>
        /// <param name="e">操作事件参数</param>
        /// <remarks>
        /// - 支持单指拖动和多指手势
        /// - 可以同时进行平移、旋转和缩放
        /// - 通过 ManipulationDelta 获取手势变化信息
        /// - 应用平移
        /// - 支持两指缩放和旋转操作
        /// - 应用变换到元素
        /// - 应用变换到选中的笔画
        /// - 包含异常处理
        /// </remarks>
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

        /// <summary>
        /// 创建并压缩图片
        /// </summary>
        /// <param name="filePath">图片文件路径</param>
        /// <returns>创建的Image对象</returns>
        /// <remarks>
        /// - 创建文件依赖目录
        /// - 复制文件到依赖目录
        /// - 创建BitmapImage
        /// - 如果图片尺寸大于1920x1080且设置了压缩图片，则压缩图片
        /// - 否则使用原始尺寸
        /// - 返回创建的Image对象
        /// </remarks>
        /// <summary>与图片选择工具栏、缩放控制点联动的画布位图类元素（普通图片或多页 PDF 嵌入）。</summary>
        private static bool IsBitmapLikeCanvasElement(FrameworkElement fe)
        {
            return fe is Image || fe is PdfEmbeddedView;
        }

        private async Task<FrameworkElement> CreateAndCompressImageAsync(string filePath)
        {
            string fileExtension = Path.GetExtension(filePath);
            if (string.Equals(fileExtension, ".pdf", StringComparison.OrdinalIgnoreCase))
                return await CreateAndCompressImageFromPdfAsync(filePath);

            string savePath = Path.Combine(Settings.Automation.AutoSavedStrokesLocation, "File Dependency");
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

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

        /// <summary>
        /// 插入完整 PDF：嵌入控件内可翻页，右下角显示页码（类似希沃白板交互）。
        /// </summary>
        private async Task<PdfEmbeddedView> CreateAndCompressImageFromPdfAsync(string filePath)
        {
            try
            {
                string savePath = Path.Combine(Settings.Automation.AutoSavedStrokesLocation, "File Dependency");
                if (!Directory.Exists(savePath))
                    Directory.CreateDirectory(savePath);

                string timestamp = "img_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
                string newFilePath = Path.Combine(savePath, timestamp + ".pdf");
                await Task.Run(() => File.Copy(filePath, newFilePath, true));

                uint pageCount = await PdfWinRtHelper.GetPageCountAsync(newFilePath);
                if (pageCount == 0)
                {
                    ShowNotification("无法打开 PDF（可能已加密、损坏或不支持）。");
                    return null;
                }

                bool compress = isLoaded && Settings.Canvas.IsCompressPicturesUploaded;
                var view = new PdfEmbeddedView();
                await view.InitializeAsync(newFilePath, pageCount, compress);
                view.Tag = filePath;
                return view;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"插入 PDF 失败: {ex.Message}", LogHelper.LogType.Error);
                ShowNotification($"插入 PDF 失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>从保存的 <see cref="CanvasElementInfo"/> 恢复 PDF（与打开墨迹时的图片恢复流程一致，不单独写入时间轴）。</summary>
        private async Task RestorePdfFromElementInfoAsync(CanvasElementInfo info)
        {
            if (info == null || inkCanvas == null) return;
            if (!string.Equals(info.Type, "Pdf", StringComparison.OrdinalIgnoreCase)) return;
            if (string.IsNullOrEmpty(info.SourcePath) || !File.Exists(info.SourcePath)) return;

            try
            {
                uint pageCount = await PdfWinRtHelper.GetPageCountAsync(info.SourcePath);
                if (pageCount == 0) return;

                bool compress = isLoaded && Settings.Canvas.IsCompressPicturesUploaded;
                uint initial = 0;
                if (info.PdfCurrentPage.HasValue)
                    initial = (uint)Math.Max(0, Math.Min(info.PdfCurrentPage.Value, (int)pageCount - 1));

                var view = new PdfEmbeddedView
                {
                    Name = "pdf_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff")
                };
                await view.InitializeAsync(info.SourcePath, pageCount, compress, initial);

                if (info.Width > 0) view.Width = info.Width;
                if (info.Height > 0) view.Height = info.Height;

                InkCanvas.SetLeft(view, info.Left);
                InkCanvas.SetTop(view, info.Top);

                InitializeElementTransform(view);
                BindElementEvents(view);
                inkCanvas.Children.Add(view);
                _pdfSidebarNextPositionUseHostTransform = true;
                SyncPdfPageSidebarWithCanvas();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"从 .elements.json 恢复 PDF 失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion

        #region Media
        /// <summary>
        /// 处理媒体插入按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 打开文件选择对话框，选择媒体文件
        /// - 读取媒体文件字节
        /// - 创建MediaElement
        /// - 居中缩放MediaElement
        /// - 设置位置并添加到画布
        /// - 设置LoadedBehavior和UnloadedBehavior为Manual
        /// - 媒体加载完成后播放并立即暂停
        /// - 提交到时间机器历史记录
        /// </remarks>
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

        /// <summary>
        /// 创建MediaElement
        /// </summary>
        /// <param name="filePath">媒体文件路径</param>
        /// <returns>创建的MediaElement对象</returns>
        /// <remarks>
        /// - 创建文件依赖目录
        /// - 创建MediaElement
        /// - 设置Source、名称、LoadedBehavior和UnloadedBehavior
        /// - 设置宽度和高度
        /// - 复制文件到依赖目录
        /// - 更新Source为新文件路径
        /// - 返回创建的MediaElement对象
        /// </remarks>
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

        /// <summary>
        /// 居中并缩放元素
        /// </summary>
        /// <param name="element">要居中缩放的元素</param>
        /// <remarks>
        /// - 确保元素已加载且有有效尺寸
        /// - 如果元素尺寸无效，等待加载完成后再处理
        /// - 获取画布的实际尺寸
        /// - 如果画布尺寸为0，使用窗口尺寸作为备选
        /// - 如果仍然为0，使用屏幕尺寸
        /// - 计算最大允许尺寸（画布的70%）
        /// - 获取元素的当前尺寸
        /// - 计算缩放比例
        /// - 如果元素本身比最大尺寸小，不进行缩放
        /// - 计算新的尺寸
        /// - 设置元素尺寸
        /// - 计算居中位置
        /// - 确保位置不为负数
        /// - 设置位置
        /// - 保持TransformGroup，不清除RenderTransform
        /// - 只有在没有TransformGroup时才创建
        /// - 包含异常处理
        /// </remarks>
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

        /// <summary>
        /// 初始化InkCanvas选择设置
        /// </summary>
        /// <remarks>
        /// - 清除当前选择，避免显示控制点
        /// - 设置编辑模式为非选择模式
        /// </remarks>
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

        /// <summary>
        /// 更新图片选择工具栏位置
        /// </summary>
        /// <param name="element">图片元素</param>
        /// <remarks>
        /// - 获取元素的实际边界（考虑变换）
        /// - 计算工具栏位置（显示在图片下方）
        /// - 确保工具栏不超出画布边界
        /// - 设置工具栏位置
        /// - 包含异常处理
        /// </remarks>
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

                var pdfTarget = GetPdfSidebarTargetElement();
                if (pdfTarget != null && BorderPdfPageSidebar != null && BorderPdfPageSidebar.Visibility == Visibility.Visible)
                    UpdatePdfPageSidebarPosition(pdfTarget);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新图片选择工具栏位置失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private const double PdfPageSidebarGap = 10;

        /// <summary>
        /// 侧栏绑定的 PDF：若当前选中的是 PDF 则用该项；否则用画布上最后一个 PdfEmbeddedView。
        /// </summary>
        private PdfEmbeddedView GetPdfSidebarTargetElement()
        {
            if (inkCanvas == null) return null;
            var pdfs = inkCanvas.Children.OfType<PdfEmbeddedView>().ToList();
            if (pdfs.Count == 0) return null;
            if (currentSelectedElement is PdfEmbeddedView sel && pdfs.Contains(sel))
                return sel;
            return pdfs[pdfs.Count - 1];
        }

        private void AttachPdfPageSidebarEvents(PdfEmbeddedView pdf)
        {
            if (pdf == null || _pdfPageSidebarEventSource == pdf) return;
            DetachPdfPageSidebarEvents();
            _pdfPageSidebarEventSource = pdf;
            _pdfPageSidebarEventSource.PageNavigationStateChanged += SelectedPdf_PageNavigationStateChanged;
            _pdfPageSidebarEventSource.LayoutUpdated += OnPdfSidebarTargetLayoutUpdated;
        }

        private void DetachPdfPageSidebarEvents()
        {
            if (_pdfPageSidebarEventSource != null)
            {
                _pdfPageSidebarEventSource.PageNavigationStateChanged -= SelectedPdf_PageNavigationStateChanged;
                _pdfPageSidebarEventSource.LayoutUpdated -= OnPdfSidebarTargetLayoutUpdated;
                _pdfPageSidebarEventSource = null;
            }
        }

        private void OnPdfSidebarTargetLayoutUpdated(object sender, EventArgs e)
        {
            if (BorderPdfPageSidebar?.Visibility != Visibility.Visible || inkCanvas == null) return;
            if (!(sender is PdfEmbeddedView p) || !ReferenceEquals(_pdfPageSidebarEventSource, p)) return;
            if (!inkCanvas.Children.Contains(p))
                SyncPdfPageSidebarWithCanvas();
            else
                RequestPdfSidebarPositionRefresh();
        }

        /// <summary>在下一帧合并更新侧栏位置（初始布局、翻页改尺寸后 ActualWidth 仍为 0 时尤其需要）。</summary>
        private void RequestPdfSidebarPositionRefresh()
        {
            if (_pdfSidebarPositionRefreshPending) return;
            _pdfSidebarPositionRefreshPending = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _pdfSidebarPositionRefreshPending = false;
                try
                {
                    var t = GetPdfSidebarTargetElement();
                    if (t == null || BorderPdfPageSidebar?.Visibility != Visibility.Visible)
                    {
                        SyncPdfPageSidebarWithCanvas();
                        return;
                    }

                    if (!inkCanvas.Children.Contains(t))
                    {
                        SyncPdfPageSidebarWithCanvas();
                        return;
                    }

                    t.UpdateLayout();
                    inkCanvas.UpdateLayout();
                    UpdatePdfPageSidebarPosition(t);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"PDF 侧栏延迟定位失败: {ex.Message}", LogHelper.LogType.Warning);
                }
            }), DispatcherPriority.Render);
        }

        /// <summary>
        /// 画布上存在 PDF 时始终显示右侧页码栏并跟随目标 PDF；无任何 PDF 时隐藏。
        /// </summary>
        private void SyncPdfPageSidebarWithCanvas()
        {
            if (BorderPdfPageSidebar == null || inkCanvas == null) return;

            // 屏幕模式（已退出白板/黑板）下不显示侧栏，避免画布仍含 PDF 时栏残留在桌面上
            if (currentMode == 0)
            {
                DetachPdfPageSidebarEvents();
                BorderPdfPageSidebar.Visibility = Visibility.Collapsed;
                ResetPdfSidebarToIdle();
                return;
            }

            var pdf = GetPdfSidebarTargetElement();
            if (pdf == null)
            {
                DetachPdfPageSidebarEvents();
                BorderPdfPageSidebar.Visibility = Visibility.Collapsed;
                ResetPdfSidebarToIdle();
                return;
            }

            AttachPdfPageSidebarEvents(pdf);
            BorderPdfPageSidebar.Visibility = Visibility.Visible;
            UpdatePdfSidebarFromPdf(pdf);
            pdf.UpdateLayout();
            inkCanvas.UpdateLayout();
            UpdatePdfPageSidebarPosition(pdf);
            RequestPdfSidebarPositionRefresh();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var t = GetPdfSidebarTargetElement();
                if (t != null && BorderPdfPageSidebar?.Visibility == Visibility.Visible && inkCanvas.Children.Contains(t))
                    UpdatePdfPageSidebarPosition(t);
            }), DispatcherPriority.ContextIdle);
        }

        /// <summary>
        /// 将 PDF 专用页码栏贴在当前 PDF 右侧。常态与早期实现一致：画布坐标 + <c>Measure(Width, ∞)</c>；仅在 <see cref="_pdfSidebarNextPositionUseHostTransform"/> 为 true 时用宿主 Visual 变换对齐首帧。
        /// </summary>
        private void UpdatePdfPageSidebarPosition(FrameworkElement element)
        {
            try
            {
                if (BorderPdfPageSidebar == null || inkCanvas == null || !(element is PdfEmbeddedView pdfEl))
                    return;

                if (!inkCanvas.Children.Contains(pdfEl))
                {
                    SyncPdfPageSidebarWithCanvas();
                    return;
                }

                bool wantHostOnce = _pdfSidebarNextPositionUseHostTransform;

                // 插入首帧：先布局再取界，便于 Transform 与墨迹一致；常态与最初 PDF 侧栏实现一致，不在此强制 UpdateLayout。
                if (wantHostOnce)
                    pdfEl.UpdateLayout();

                Rect b = GetElementActualBounds(pdfEl);
                if (b.Width <= 0 || b.Height <= 0 || double.IsNaN(b.Width) || double.IsNaN(b.Height))
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var t = GetPdfSidebarTargetElement();
                        if (t is PdfEmbeddedView pe && inkCanvas.Children.Contains(pe) && BorderPdfPageSidebar?.Visibility == Visibility.Visible)
                            UpdatePdfPageSidebarPosition(pe);
                    }), DispatcherPriority.Loaded);
                    return;
                }

                Visual sidebarHost = VisualTreeHelper.GetParent(BorderPdfPageSidebar) as Visual;
                double left = 0, top = 0, maxLeft = 0, maxTop = 0;
                bool hostOk = false;

                if (wantHostOnce && sidebarHost != null)
                {
                    BorderPdfPageSidebar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double sidebarW = BorderPdfPageSidebar.DesiredSize.Width;
                    double sidebarH = BorderPdfPageSidebar.DesiredSize.Height;
                    if (sidebarW <= 0)
                        sidebarW = BorderPdfPageSidebar.Width;
                    if (sidebarH <= 0)
                        sidebarH = BorderPdfPageSidebar.ActualHeight;
                    if (sidebarH <= 0)
                        sidebarH = 220;

                    try
                    {
                        GeneralTransform inkToHost = inkCanvas.TransformToVisual(sidebarHost);
                        Point tl = inkToHost.Transform(new Point(b.Left, b.Top));
                        Point br = inkToHost.Transform(new Point(b.Right, b.Bottom));
                        double rightX = Math.Max(tl.X, br.X);
                        double midY = (tl.Y + br.Y) * 0.5;
                        left = rightX + PdfPageSidebarGap;
                        top = midY - sidebarH * 0.5;

                        var feHost = sidebarHost as FrameworkElement;
                        double hostW = feHost != null && feHost.ActualWidth > 0 ? feHost.ActualWidth : inkCanvas.ActualWidth;
                        double hostH = feHost != null && feHost.ActualHeight > 0 ? feHost.ActualHeight : inkCanvas.ActualHeight;
                        maxLeft = Math.Max(0, hostW - sidebarW);
                        maxTop = Math.Max(0, hostH - sidebarH);

                        if (left > maxLeft)
                        {
                            double leftEdge = Math.Min(tl.X, br.X);
                            double leftAlt = leftEdge - PdfPageSidebarGap - sidebarW;
                            if (leftAlt >= 0)
                                left = leftAlt;
                        }

                        hostOk = true;
                    }
                    catch
                    {
                        hostOk = false;
                    }
                }

                if (!hostOk)
                {
                    // 与 ea74592「PDF 侧栏」初版一致：固定宽度测量竖向所需高度，再按墨迹边界与 inkCanvas 尺寸夹紧。
                    BorderPdfPageSidebar.Measure(new Size(BorderPdfPageSidebar.Width, double.PositiveInfinity));
                    double sidebarW = BorderPdfPageSidebar.DesiredSize.Width;
                    double sidebarH = BorderPdfPageSidebar.DesiredSize.Height;
                    if (sidebarW <= 0)
                        sidebarW = BorderPdfPageSidebar.Width;
                    if (sidebarH <= 0)
                        sidebarH = BorderPdfPageSidebar.ActualHeight;
                    if (sidebarH <= 0)
                        sidebarH = 220;

                    left = b.Right + PdfPageSidebarGap;
                    top = b.Top + (b.Height * 0.5) - (sidebarH * 0.5);
                    maxLeft = Math.Max(0, inkCanvas.ActualWidth - sidebarW);
                    maxTop = Math.Max(0, inkCanvas.ActualHeight - sidebarH);
                    if (left > maxLeft)
                    {
                        double leftAlt = b.Left - PdfPageSidebarGap - sidebarW;
                        if (leftAlt >= 0)
                            left = leftAlt;
                    }
                }

                if (wantHostOnce)
                    _pdfSidebarNextPositionUseHostTransform = false;

                left = Math.Max(0, Math.Min(left, maxLeft));
                top = Math.Max(0, Math.Min(top, maxTop));

                BorderPdfPageSidebar.Margin = new Thickness(left, top, 0, 0);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新 PDF 右侧页码栏位置失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 获取元素的实际边界（考虑变换）
        /// </summary>
        /// <param name="element">要获取边界的元素</param>
        /// <returns>元素的实际边界</returns>
        /// <remarks>
        /// - 获取元素的Left和Top位置
        /// - 如果值为NaN，设为0
        /// - 获取元素的宽度和高度
        /// - 检查是否有RenderTransform
        /// - 如果有变换，使用变换后的边界
        /// - 变换失败时回退到简单计算
        /// - 没有变换时直接使用位置和大小
        /// - 包含异常处理
        /// - 回退到基本计算
        /// </remarks>
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

        /// <summary>
        /// 处理图片克隆功能
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 检查当前选中元素是否为图片
        /// - 创建克隆图片
        /// - 添加到画布
        /// - 初始化变换
        /// - 绑定事件
        /// - 记录历史
        /// - 包含异常处理
        /// </remarks>
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

        /// <summary>
        /// 处理图片克隆到新页面功能
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 检查当前选中元素是否为图片
        /// - 创建新页面
        /// - 创建克隆图片
        /// - 设置图片属性，避免被InkCanvas选择系统处理
        /// - 初始化变换
        /// - 绑定事件
        /// - 添加到新页面的画布
        /// - 记录历史
        /// - 包含异常处理
        /// </remarks>
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

        /// <summary>
        /// 处理图片左旋转功能
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 检查当前是否有选中元素
        /// - 应用旋转变换（向左旋转45度）
        /// - 如果是图片元素，更新工具栏位置
        /// - 包含异常处理
        /// </remarks>
        private void BorderImageRotateLeft_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (currentSelectedElement != null)
                {
                    ApplyRotateTransform(currentSelectedElement, -45);

                    // 更新工具栏位置
                    if (IsBitmapLikeCanvasElement(currentSelectedElement) && BorderImageSelectionControl?.Visibility == Visibility.Visible)
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

        /// <summary>
        /// 处理图片右旋转功能
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 检查当前是否有选中元素
        /// - 应用旋转变换（向右旋转45度）
        /// - 如果是图片元素，更新工具栏位置
        /// - 包含异常处理
        /// </remarks>
        private void BorderImageRotateRight_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (currentSelectedElement != null)
                {
                    ApplyRotateTransform(currentSelectedElement, 45);

                    // 更新工具栏位置
                    if (IsBitmapLikeCanvasElement(currentSelectedElement) && BorderImageSelectionControl?.Visibility == Visibility.Visible)
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

        /// <summary>
        /// 处理图片缩放减小功能
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 检查当前是否有选中元素
        /// - 计算元素中心点
        /// - 应用缩放变换（缩小到0.9倍）
        /// - 如果是图片元素，更新工具栏位置
        /// - 包含异常处理
        /// </remarks>
        private void GridImageScaleDecrease_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (currentSelectedElement != null)
                {
                    var elementCenter = new Point(currentSelectedElement.ActualWidth / 2, currentSelectedElement.ActualHeight / 2);
                    ApplyScaleTransform(currentSelectedElement, 0.9, elementCenter);

                    // 更新工具栏位置
                    if (IsBitmapLikeCanvasElement(currentSelectedElement) && BorderImageSelectionControl?.Visibility == Visibility.Visible)
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

        /// <summary>
        /// 处理图片缩放增大功能
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// - 检查当前是否有选中元素
        /// - 计算元素中心点
        /// - 应用缩放变换（放大到1.1倍）
        /// - 如果是图片元素，更新工具栏位置
        /// - 包含异常处理
        /// </remarks>
        private void GridImageScaleIncrease_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (currentSelectedElement != null)
                {
                    var elementCenter = new Point(currentSelectedElement.ActualWidth / 2, currentSelectedElement.ActualHeight / 2);
                    ApplyScaleTransform(currentSelectedElement, 1.1, elementCenter);

                    // 更新工具栏位置
                    if (IsBitmapLikeCanvasElement(currentSelectedElement) && BorderImageSelectionControl?.Visibility == Visibility.Visible)
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

        private void ResetPdfSidebarToIdle()
        {
            if (TextBlockPdfSidebarPageLabel != null)
            {
                TextBlockPdfSidebarPageLabel.Text = "— / —";
                TextBlockPdfSidebarPageLabel.Opacity = 0.55;
            }

            if (BorderPdfSidebarPagePrev != null)
            {
                BorderPdfSidebarPagePrev.Opacity = 0.35;
                BorderPdfSidebarPagePrev.IsHitTestVisible = false;
            }

            if (BorderPdfSidebarPageNext != null)
            {
                BorderPdfSidebarPageNext.Opacity = 0.35;
                BorderPdfSidebarPageNext.IsHitTestVisible = false;
            }
        }

        private void UpdatePdfSidebarFromPdf(PdfEmbeddedView pdf)
        {
            if (pdf == null) return;

            if (TextBlockPdfSidebarPageLabel != null)
            {
                TextBlockPdfSidebarPageLabel.Text = pdf.PageLabelText;
                TextBlockPdfSidebarPageLabel.Opacity = 1.0;
            }

            bool prevOk = pdf.CanGoPrevious;
            bool nextOk = pdf.CanGoNext;
            if (BorderPdfSidebarPagePrev != null)
            {
                BorderPdfSidebarPagePrev.Opacity = prevOk ? 1.0 : 0.35;
                BorderPdfSidebarPagePrev.IsHitTestVisible = prevOk;
            }

            if (BorderPdfSidebarPageNext != null)
            {
                BorderPdfSidebarPageNext.Opacity = nextOk ? 1.0 : 0.35;
                BorderPdfSidebarPageNext.IsHitTestVisible = nextOk;
            }
        }

        private void SelectedPdf_PageNavigationStateChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (sender is PdfEmbeddedView pdf)
                {
                    if (!inkCanvas.Children.Contains(pdf))
                    {
                        SyncPdfPageSidebarWithCanvas();
                        return;
                    }

                    UpdatePdfSidebarFromPdf(pdf);
                    UpdatePdfPageSidebarPosition(pdf);
                    RequestPdfSidebarPositionRefresh();
                }
                if (currentSelectedElement != null && IsBitmapLikeCanvasElement(currentSelectedElement))
                {
                    UpdateImageSelectionToolbarPosition(currentSelectedElement);
                    if (ImageResizeHandlesCanvas?.Visibility == Visibility.Visible)
                        UpdateImageResizeHandlesPosition(GetElementActualBounds(currentSelectedElement));
                }
            }), DispatcherPriority.Loaded);
        }

        private async void BorderPdfSidebarPagePrev_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var pdf = GetPdfSidebarTargetElement();
                if (pdf != null && pdf.CanGoPrevious)
                    await pdf.GoToPreviousPageAsync();
                SyncPdfPageSidebarWithCanvas();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PDF 上一页失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private async void BorderPdfSidebarPageNext_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var pdf = GetPdfSidebarTargetElement();
                if (pdf != null && pdf.CanGoNext)
                    await pdf.GoToNextPageAsync();
                SyncPdfPageSidebarWithCanvas();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PDF 下一页失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 处理图片删除功能
        /// </summary>
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

                    var toRemove = currentSelectedElement;
                    // 从画布中移除
                    inkCanvas.Children.Remove(toRemove);

                    // 清除选中状态
                    UnselectElement(toRemove);
                    currentSelectedElement = null;

                    // 恢复到删除前的编辑模式
                    inkCanvas.EditingMode = previousEditingMode;

                    SyncPdfPageSidebarWithCanvas();

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
                if (IsBitmapLikeCanvasElement(currentSelectedElement) && sender is Ellipse ellipse)
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
                if (isResizingImage && IsBitmapLikeCanvasElement(currentSelectedElement) && sender is Ellipse ellipse)
                {
                    var currentPoint = e.GetPosition(inkCanvas);
                    ResizeImageByHandle(currentSelectedElement, imageResizeStartPoint, currentPoint, activeResizeHandle);
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
        private void ResizeImageByHandle(FrameworkElement element, Point startPoint, Point currentPoint, string handleName)
        {
            try
            {
                if (element.RenderTransform is TransformGroup transformGroup)
                {
                    var scaleTransform = transformGroup.Children.OfType<ScaleTransform>().FirstOrDefault();
                    var translateTransform = transformGroup.Children.OfType<TranslateTransform>().FirstOrDefault();

                    if (scaleTransform == null || translateTransform == null) return;

                    // 获取图片的当前边界
                    Rect currentBounds = GetElementActualBounds(element);
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
                    UpdateImageResizeHandlesPosition(GetElementActualBounds(element));

                    if (BorderImageSelectionControl?.Visibility == Visibility.Visible)
                        UpdateImageSelectionToolbarPosition(element);
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
