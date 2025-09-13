using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Drawing;
using Ink_Canvas.Helpers;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingPoint = System.Drawing.Point;
using WpfPoint = System.Windows.Point;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfCanvas = System.Windows.Controls.Canvas;

namespace Ink_Canvas
{
    public partial class ScreenshotSelectorWindow : Window
    {
        private bool _isSelecting;
        private bool _isFreehandMode;
        private bool _isAdjusting;
        private bool _isMoving;
        private bool _isCameraMode;
        private WpfPoint _startPoint;
        private WpfPoint _currentPoint;
        private WpfPoint _lastMousePosition;
        private List<WpfPoint> _freehandPoints;
        private Polyline _freehandPolyline;
        private Rect _currentSelection;
        private ControlPointType _activeControlPoint = ControlPointType.None;
        private CameraService _cameraService;
        private Bitmap _capturedCameraImage;

        // 控制点类型枚举
        private enum ControlPointType
        {
            None,
            TopLeft, TopRight, BottomLeft, BottomRight,
            Top, Bottom, Left, Right,
            Move
        }

        public DrawingRectangle? SelectedArea { get; private set; }
        public List<WpfPoint> SelectedPath { get; private set; }
        public Bitmap CameraImage { get; private set; }

        public ScreenshotSelectorWindow()
        {
            InitializeComponent();

            // 设置窗口覆盖所有屏幕
            SetupFullScreenOverlay();

            // 初始化自由绘制模式
            InitializeFreehandMode();

            // 绑定控制点事件
            BindControlPointEvents();

            // 初始化按钮状态 
            InitializeButtonStates();

            // 初始化摄像头服务
            InitializeCameraService();

            // 隐藏提示文字的定时器
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(5);
            timer.Tick += (s, e) =>
            {
                HintTextBorder.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();
        }

        private void InitializeFreehandMode()
        {
            _freehandPoints = new List<WpfPoint>();
            _freehandPolyline = new Polyline
            {
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Fill = Brushes.Transparent
            };
            SelectionCanvas.Children.Add(_freehandPolyline);
        }

        private void InitializeButtonStates()
        {
            RectangleModeButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色
            FreehandModeButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色
            FullScreenButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色
            CameraModeButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色
        }

        private void InitializeCameraService()
        {
            try
            {
                _cameraService = new CameraService();
                _cameraService.FrameReceived += CameraService_FrameReceived;
                _cameraService.ErrorOccurred += CameraService_ErrorOccurred;

                // 初始化摄像头选择下拉框
                RefreshCameraComboBox();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化摄像头服务失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void RefreshCameraComboBox()
        {
            try
            {
                CameraSelectionComboBox.Items.Clear();
                
                if (_cameraService.HasAvailableCameras())
                {
                    var cameraNames = _cameraService.GetCameraNames();
                    foreach (var name in cameraNames)
                    {
                        CameraSelectionComboBox.Items.Add(name);
                    }
                    
                    if (cameraNames.Count > 0)
                    {
                        CameraSelectionComboBox.SelectedIndex = 0;
                    }
                }
                else
                {
                    CameraSelectionComboBox.Items.Add("未找到摄像头设备");
                    CameraSelectionComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"刷新摄像头列表失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void CameraService_FrameReceived(object sender, Bitmap frame)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_isCameraMode && CameraPreviewImage != null && frame != null)
                    {
                        // 克隆帧以避免在转换过程中被释放
                        using (var clonedFrame = frame.Clone() as Bitmap)
                        {
                            if (clonedFrame != null)
                            {
                                var bitmapSource = ConvertBitmapToBitmapSource(clonedFrame);
                                if (bitmapSource != null)
                                {
                                    CameraPreviewImage.Source = bitmapSource;
                                    CameraStatusText.Text = "摄像头已连接";
                                }
                            }
                        }
                    }
                }));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理摄像头帧失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void CameraService_ErrorOccurred(object sender, string error)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    CameraStatusText.Text = $"摄像头错误: {error}";
                }));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理摄像头错误失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private System.Windows.Media.Imaging.BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            try
            {
                using (var memory = new System.IO.MemoryStream())
                {
                    bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                    memory.Position = 0;

                    var bitmapImage = new System.Windows.Media.Imaging.BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = memory;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();

                    return bitmapImage;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"转换位图失败: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        private void BindControlPointEvents()
        {
            // 绑定所有控制点的鼠标事件
            var controlPoints = new[]
            {
                TopLeftControl, TopRightControl, BottomLeftControl, BottomRightControl,
                TopControl, BottomControl, LeftControl, RightControl
            };

            foreach (var control in controlPoints)
            {
                control.MouseLeftButtonDown += ControlPoint_MouseLeftButtonDown;
                control.MouseLeftButtonUp += ControlPoint_MouseLeftButtonUp;
                control.MouseMove += ControlPoint_MouseMove;

                // 确保控制点能够接收鼠标事件
                control.IsHitTestVisible = true;
                control.Focusable = false;

                // 设置控制点的Z-index，确保它们在最上层
                WpfCanvas.SetZIndex(control, 1003);
            }
        }

        private void SetupFullScreenOverlay()
        {
            // 获取所有屏幕的虚拟屏幕边界
            var virtualScreen = SystemInformation.VirtualScreen;

            // 转换为WPF坐标系统
            var dpiScale = GetDpiScale();

            Left = virtualScreen.Left / dpiScale;
            Top = virtualScreen.Top / dpiScale;
            Width = virtualScreen.Width / dpiScale;
            Height = virtualScreen.Height / dpiScale;
        }

        private double GetDpiScale()
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                return source.CompositionTarget.TransformToDevice.M11;
            }
            return 1.0; // 默认DPI
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelSelection();
            }
            else if (e.Key == Key.Enter)
            {
                ConfirmSelection();
            }
        }

        private void RectangleModeButton_Click(object sender, RoutedEventArgs e)
        {
            // 重置所有选择状态
            ResetSelectionState();

            _isFreehandMode = false;
            RectangleModeButton.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)); // 蓝色
            FreehandModeButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色
            FullScreenButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色
            HintText.Text = "拖拽鼠标选择矩形区域";
            HintTextBorder.Visibility = Visibility.Visible;
        }

        private void FreehandModeButton_Click(object sender, RoutedEventArgs e)
        {
            // 重置所有选择状态
            ResetSelectionState();

            _isFreehandMode = true;
            FreehandModeButton.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)); // 蓝色
            RectangleModeButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色
            FullScreenButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色
            HintText.Text = "按住鼠标左键绘制任意形状，松开直接截图";
            HintTextBorder.Visibility = Visibility.Visible;
        }

        private void FullScreenButton_Click(object sender, RoutedEventArgs e)
        {
            // 重置所有选择状态
            ResetSelectionState();

            // 设置全屏截图模式
            _isFreehandMode = false;
            _isCameraMode = false;
            FullScreenButton.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)); // 蓝色
            RectangleModeButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色
            FreehandModeButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色
            CameraModeButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色
            
            // 隐藏摄像头预览
            CameraPreviewBorder.Visibility = Visibility.Collapsed;
            
            // 直接执行全屏截图
            PerformFullScreenCapture();
        }

        private void CameraModeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 重置所有选择状态
                ResetSelectionState();

                // 设置摄像头模式
                _isFreehandMode = false;
                _isCameraMode = true;
                CameraModeButton.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)); // 蓝色
                RectangleModeButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色
                FreehandModeButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色
                FullScreenButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色

                // 显示摄像头预览
                CameraPreviewBorder.Visibility = Visibility.Visible;
                HintText.Text = "摄像头预览模式，点击确认截图按钮进行截图";
                HintTextBorder.Visibility = Visibility.Visible;

                // 启动摄像头预览
                if (_cameraService != null && _cameraService.HasAvailableCameras())
                {
                    var selectedIndex = CameraSelectionComboBox.SelectedIndex;
                    if (selectedIndex >= 0 && selectedIndex < _cameraService.GetCameraNames().Count)
                    {
                        _cameraService.StartPreview(selectedIndex);
                    }
                }
                else
                {
                    CameraStatusText.Text = "未找到摄像头设备";
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动摄像头模式失败: {ex.Message}", LogHelper.LogType.Error);
                CameraStatusText.Text = $"启动摄像头失败: {ex.Message}";
            }
        }

        private void CameraSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_isCameraMode && _cameraService != null)
                {
                    var selectedIndex = CameraSelectionComboBox.SelectedIndex;
                    if (selectedIndex >= 0 && selectedIndex < _cameraService.GetCameraNames().Count)
                    {
                        _cameraService.SwitchCamera(selectedIndex);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换摄像头失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void SwitchCameraButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_cameraService != null && _cameraService.HasAvailableCameras())
                {
                    var cameraNames = _cameraService.GetCameraNames();
                    if (cameraNames.Count > 1)
                    {
                        var currentIndex = CameraSelectionComboBox.SelectedIndex;
                        var nextIndex = (currentIndex + 1) % cameraNames.Count;
                        CameraSelectionComboBox.SelectedIndex = nextIndex;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换摄像头失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            // 在自由绘制模式下，确认按钮不执行任何操作
            if (_isFreehandMode)
            {
                return;
            }

            // 在摄像头模式下，执行摄像头截图
            if (_isCameraMode)
            {
                ConfirmCameraCapture();
                return;
            }

            ConfirmSelection();
        }

        private void ConfirmCameraCapture()
        {
            try
            {
                if (_cameraService != null && _cameraService.IsCapturing)
                {
                    // 获取当前帧
                    var currentFrame = _cameraService.GetCurrentFrame();
                    if (currentFrame != null)
                    {
                        // 保存摄像头图像
                        CameraImage = currentFrame.Clone() as Bitmap;
                        
                        // 停止摄像头预览
                        _cameraService.StopPreview();
                        
                        // 设置结果并关闭窗口
                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        CameraStatusText.Text = "无法获取摄像头画面";
                    }
                }
                else
                {
                    CameraStatusText.Text = "摄像头未启动";
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"摄像头截图失败: {ex.Message}", LogHelper.LogType.Error);
                CameraStatusText.Text = $"摄像头截图失败: {ex.Message}";
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelSelection();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 检查是否点击了UI元素，如果是则不处理选择
            var hitElement = e.Source as FrameworkElement;
            if (hitElement != null && (
                hitElement is Ellipse ||
                hitElement is System.Windows.Controls.Button ||
                hitElement is Border ||
                hitElement is TextBlock ||
                hitElement is StackPanel ||
                hitElement is Separator ||
                hitElement.Name == "SizeInfoBorder" ||
                hitElement.Name == "HintText" ||
                hitElement.Name == "AdjustModeHint" ||
                hitElement.Name == "SelectionRectangle"))
            {
                return;
            }

            // 如果正在调整，忽略新的选择
            if (_isAdjusting) return;

            // 如果正在选择，先重置状态
            if (_isSelecting)
            {
                _isSelecting = false;
                ReleaseMouseCapture();
            }

            // 开始新的选择
            _isSelecting = true;
            _startPoint = e.GetPosition(this);
            _currentPoint = _startPoint;

            // 隐藏提示文字
            HintTextBorder.Visibility = Visibility.Collapsed;

            if (_isFreehandMode)
            {
                // 自由绘制模式：开始绘制
                _freehandPoints.Clear();
                _freehandPolyline.Points.Clear();
                _freehandPoints.Add(_startPoint);
                _freehandPolyline.Points.Add(_startPoint);

                // 确保自由绘制路径可见
                _freehandPolyline.Visibility = Visibility.Visible;
            }
            else
            {
                // 矩形模式
                SelectionRectangle.Visibility = Visibility.Visible;
                SizeInfoBorder.Visibility = Visibility.Visible;
            }

            // 捕获鼠标
            CaptureMouse();

            if (!_isFreehandMode)
            {
                UpdateSelection();
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            // 如果正在调整模式且正在移动，不处理窗口级别的鼠标移动
            if (_isAdjusting && _isMoving)
            {
                return;
            }

            if (_isSelecting)
            {
                _currentPoint = e.GetPosition(this);

                if (_isFreehandMode)
                {
                    // 自由绘制模式：添加点到路径
                    _freehandPoints.Add(_currentPoint);
                    _freehandPolyline.Points.Add(_currentPoint);

                    // 确保自由绘制路径可见
                    _freehandPolyline.Visibility = Visibility.Visible;
                }
                else
                {
                    // 矩形模式
                    UpdateSelection();
                }
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 如果正在调整模式且正在移动，不处理窗口级别的鼠标释放
            if (_isAdjusting && _isMoving)
            {
                return;
            }

            if (_isSelecting)
            {
                _isSelecting = false;
                ReleaseMouseCapture();

                if (_isFreehandMode)
                {
                    // 自由绘制模式：一笔完成，直接截图
                    if (_freehandPoints.Count > 1) // 只要有点就可以截图
                    {
                        // 创建路径的副本，避免修改原始列表
                        var pathPoints = new List<WpfPoint>(_freehandPoints);

                        // 简化路径处理，不强制闭合
                        // 如果路径没有闭合，自动添加起始点
                        if (pathPoints.Count > 0)
                        {
                            pathPoints.Add(_startPoint);
                        }

                        // 优化路径：移除重复点和过于接近的点，提高路径质量
                        var optimizedPath = OptimizePath(pathPoints);

                        // 保存选择的路径
                        SelectedPath = optimizedPath;

                        // 计算边界矩形用于截图
                        var bounds = CalculatePathBounds(optimizedPath);

                        // 确保边界矩形有效
                        if (bounds.Width >= 0 && bounds.Height >= 0)
                        {
                            var dpiScale = GetDpiScale();
                            var virtualScreen = SystemInformation.VirtualScreen;

                            int screenX = (int)((bounds.X * dpiScale) + virtualScreen.Left);
                            int screenY = (int)((bounds.Y * dpiScale) + virtualScreen.Top);
                            int screenWidth = (int)(bounds.Width * dpiScale);
                            int screenHeight = (int)(bounds.Height * dpiScale);

                            SelectedArea = new DrawingRectangle(screenX, screenY, screenWidth, screenHeight);
                            DialogResult = true;
                            Close();
                            return;
                        }
                    }

                    // 如果自由绘制失败，清除路径并继续
                    _freehandPoints.Clear();
                    _freehandPolyline.Points.Clear();
                    _freehandPolyline.Visibility = Visibility.Collapsed;
                    return;
                }
                else
                {
                    // 矩形模式：进入调整模式
                    var rect = GetSelectionRectangle();
                    if (rect.Width > 5 && rect.Height > 5) // 最小尺寸检查
                    {
                        _currentSelection = rect;
                        _isAdjusting = true;
                        ShowControlPoints();
                        HintText.Text = "拖拽控制点调整选择区域，或拖拽边框移动位置";
                        HintTextBorder.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        SelectedArea = null;
                        DialogResult = false;
                    }
                }

                if (!_isAdjusting)
                {
                    Close();
                }
            }
        }

        private void ControlPoint_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isAdjusting) return;

            _isMoving = true;
            _lastMousePosition = e.GetPosition(this);

            // 确定当前控制点类型
            var ellipse = sender as Ellipse;
            if (ellipse == TopLeftControl) _activeControlPoint = ControlPointType.TopLeft;
            else if (ellipse == TopRightControl) _activeControlPoint = ControlPointType.TopRight;
            else if (ellipse == BottomLeftControl) _activeControlPoint = ControlPointType.BottomLeft;
            else if (ellipse == BottomRightControl) _activeControlPoint = ControlPointType.BottomRight;
            else if (ellipse == TopControl) _activeControlPoint = ControlPointType.Top;
            else if (ellipse == BottomControl) _activeControlPoint = ControlPointType.Bottom;
            else if (ellipse == LeftControl) _activeControlPoint = ControlPointType.Left;
            else if (ellipse == RightControl) _activeControlPoint = ControlPointType.Right;

            // 捕获鼠标到控制点本身，而不是整个窗口
            ellipse?.CaptureMouse();
            e.Handled = true;
        }

        private void ControlPoint_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isAdjusting || !_isMoving || _activeControlPoint == ControlPointType.None) return;

            try
            {
                var currentPosition = e.GetPosition(this);
                var delta = currentPosition - _lastMousePosition;

                // 根据控制点类型调整选择区域
                AdjustSelection(delta);

                _lastMousePosition = currentPosition;
                e.Handled = true;
            }
            catch (Exception)
            {
                // 如果出现异常，停止移动
                _isMoving = false;
                _activeControlPoint = ControlPointType.None;
                var ellipse = sender as Ellipse;
                ellipse?.ReleaseMouseCapture();
            }
        }

        private void ControlPoint_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isMoving)
            {
                _isMoving = false;
                _activeControlPoint = ControlPointType.None;
                var ellipse = sender as Ellipse;
                ellipse?.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void AdjustSelection(Vector delta)
        {
            var newRect = _currentSelection;

            switch (_activeControlPoint)
            {
                case ControlPointType.TopLeft:
                    newRect.X += delta.X;
                    newRect.Y += delta.Y;
                    newRect.Width -= delta.X;
                    newRect.Height -= delta.Y;
                    break;
                case ControlPointType.TopRight:
                    newRect.Y += delta.Y;
                    newRect.Width += delta.X;
                    newRect.Height -= delta.Y;
                    break;
                case ControlPointType.BottomLeft:
                    newRect.X += delta.X;
                    newRect.Width -= delta.X;
                    newRect.Height += delta.Y;
                    break;
                case ControlPointType.BottomRight:
                    newRect.Width += delta.X;
                    newRect.Height += delta.Y;
                    break;
                case ControlPointType.Top:
                    newRect.Y += delta.Y;
                    newRect.Height -= delta.Y;
                    break;
                case ControlPointType.Bottom:
                    newRect.Height += delta.Y;
                    break;
                case ControlPointType.Left:
                    newRect.X += delta.X;
                    newRect.Width -= delta.X;
                    break;
                case ControlPointType.Right:
                    newRect.Width += delta.X;
                    break;
            }

            // 确保最小尺寸
            if (newRect.Width >= 10 && newRect.Height >= 10)
            {
                _currentSelection = newRect;
                UpdateSelectionDisplay();
            }
        }

        private void ShowControlPoints()
        {
            // 确保选择矩形在调整模式下可见
            SelectionRectangle.Visibility = Visibility.Visible;
            // 设置选择矩形的Z-index，确保它能够接收鼠标事件
            WpfCanvas.SetZIndex(SelectionRectangle, 1001);
            // 确保选择矩形能够接收鼠标事件
            SelectionRectangle.IsHitTestVisible = true;
            ControlPointsCanvas.Visibility = Visibility.Visible;
            UpdateControlPointsPosition();
        }

        private void UpdateControlPointsPosition()
        {
            var rect = _currentSelection;

            // 更新角控制点位置
            WpfCanvas.SetLeft(TopLeftControl, rect.Left - 4);
            WpfCanvas.SetTop(TopLeftControl, rect.Top - 4);

            WpfCanvas.SetLeft(TopRightControl, rect.Right - 4);
            WpfCanvas.SetTop(TopRightControl, rect.Top - 4);

            WpfCanvas.SetLeft(BottomLeftControl, rect.Left - 4);
            WpfCanvas.SetTop(BottomLeftControl, rect.Bottom - 4);

            WpfCanvas.SetLeft(BottomRightControl, rect.Right - 4);
            WpfCanvas.SetTop(BottomRightControl, rect.Bottom - 4);

            // 更新边控制点位置
            WpfCanvas.SetLeft(TopControl, rect.Left + rect.Width / 2 - 4);
            WpfCanvas.SetTop(TopControl, rect.Top - 4);

            WpfCanvas.SetLeft(BottomControl, rect.Left + rect.Width / 2 - 4);
            WpfCanvas.SetTop(BottomControl, rect.Bottom - 4);

            WpfCanvas.SetLeft(LeftControl, rect.Left - 4);
            WpfCanvas.SetTop(LeftControl, rect.Top + rect.Height / 2 - 4);

            WpfCanvas.SetLeft(RightControl, rect.Right - 4);
            WpfCanvas.SetTop(RightControl, rect.Top + rect.Height / 2 - 4);
        }

        private void UpdateSelection()
        {
            var rect = GetSelectionRectangle();

            // 更新选择矩形
            WpfCanvas.SetLeft(SelectionRectangle, rect.X);
            WpfCanvas.SetTop(SelectionRectangle, rect.Y);
            SelectionRectangle.Width = rect.Width;
            SelectionRectangle.Height = rect.Height;

            // 在选择过程中，禁用选择矩形的鼠标事件，避免干扰选择操作
            if (_isSelecting)
            {
                SelectionRectangle.IsHitTestVisible = false;
            }

            // 更新透明选择区域遮罩
            UpdateTransparentSelectionMask(rect);

            // 更新尺寸信息
            SizeInfoText.Text = $"{(int)rect.Width} x {(int)rect.Height}";
            WpfCanvas.SetLeft(SizeInfoBorder, rect.X);
            WpfCanvas.SetTop(SizeInfoBorder, rect.Y - 30);

            // 确保尺寸信息不超出屏幕
            if (WpfCanvas.GetTop(SizeInfoBorder) < 0)
            {
                WpfCanvas.SetTop(SizeInfoBorder, rect.Y + rect.Height + 5);
            }
        }

        private void UpdateTransparentSelectionMask(Rect selectionRect)
        {
            try
            {
                // 更新选择区域的几何体
                SelectionClipGeometry.Rect = selectionRect;

                // 显示透明遮罩，隐藏原始遮罩
                TransparentSelectionMask.Visibility = Visibility.Visible;
                OverlayRectangle.Visibility = Visibility.Collapsed;
            }
            catch (Exception)
            {
                // 如果几何体操作失败，回退到原始遮罩
                TransparentSelectionMask.Visibility = Visibility.Collapsed;
                OverlayRectangle.Visibility = Visibility.Visible;
            }
        }

        private void UpdateSelectionDisplay()
        {
            var rect = _currentSelection;

            // 更新选择矩形
            WpfCanvas.SetLeft(SelectionRectangle, rect.X);
            WpfCanvas.SetTop(SelectionRectangle, rect.Y);
            SelectionRectangle.Width = rect.Width;
            SelectionRectangle.Height = rect.Height;

            // 确保选择矩形在调整模式下能够接收鼠标事件
            if (_isAdjusting)
            {
                SelectionRectangle.IsHitTestVisible = true;
                WpfCanvas.SetZIndex(SelectionRectangle, 1001);
            }

            // 更新透明选择区域遮罩
            UpdateTransparentSelectionMask(rect);

            // 更新控制点位置
            UpdateControlPointsPosition();

            // 更新尺寸信息
            SizeInfoText.Text = $"{(int)rect.Width} x {(int)rect.Height}";
            WpfCanvas.SetLeft(SizeInfoBorder, rect.X);
            WpfCanvas.SetTop(SizeInfoBorder, rect.Y - 30);

            // 确保尺寸信息不超出屏幕
            if (WpfCanvas.GetTop(SizeInfoBorder) < 0)
            {
                WpfCanvas.SetTop(SizeInfoBorder, rect.Y + rect.Height + 5);
            }
        }

        private Rect GetSelectionRectangle()
        {
            double x = Math.Min(_startPoint.X, _currentPoint.X);
            double y = Math.Min(_startPoint.Y, _currentPoint.Y);
            double width = Math.Abs(_currentPoint.X - _startPoint.X);
            double height = Math.Abs(_currentPoint.Y - _startPoint.Y);

            return new Rect(x, y, width, height);
        }

        private void ConfirmSelection()
        {
            // 在自由绘制模式下，不执行确认操作
            if (_isFreehandMode)
            {
                return;
            }

            if (_isAdjusting)
            {
                // 转换为屏幕坐标，考虑DPI缩放
                var dpiScale = GetDpiScale();
                var virtualScreen = SystemInformation.VirtualScreen;

                // 计算实际屏幕坐标
                int screenX = (int)((_currentSelection.X * dpiScale) + virtualScreen.Left);
                int screenY = (int)((_currentSelection.Y * dpiScale) + virtualScreen.Top);
                int screenWidth = (int)(_currentSelection.Width * dpiScale);
                int screenHeight = (int)(_currentSelection.Height * dpiScale);

                SelectedArea = new DrawingRectangle(screenX, screenY, screenWidth, screenHeight);
                DialogResult = true;
            }
            Close();
        }

        private void CancelSelection()
        {
            // 停止摄像头预览
            if (_cameraService != null)
            {
                _cameraService.StopPreview();
            }

            SelectedArea = null;
            SelectedPath = null;
            CameraImage = null;
            DialogResult = false;
            Close();
        }

        private Rect CalculatePathBounds(List<WpfPoint> points)
        {
            if (points == null || points.Count == 0)
                return new Rect();

            double minX = points[0].X;
            double minY = points[0].Y;
            double maxX = points[0].X;
            double maxY = points[0].Y;

            foreach (var point in points)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private List<WpfPoint> OptimizePath(List<WpfPoint> points)
        {
            if (points == null || points.Count < 3)
                return points;

            var optimized = new List<WpfPoint>();
            optimized.Add(points[0]);

            for (int i = 1; i < points.Count - 1; i++)
            {
                var prev = points[i - 1];
                var current = points[i];
                var next = points[i + 1];

                // 计算当前点到前后两点连线的距离
                var distance = DistanceToLine(current, prev, next);

                // 进一步降低阈值，保留更多点，确保路径质量
                if (distance > 0.1) // 从0.5降低到0.1
                {
                    optimized.Add(current);
                }
            }

            optimized.Add(points[points.Count - 1]);
            return optimized;
        }

        private double DistanceToLine(WpfPoint point, WpfPoint lineStart, WpfPoint lineEnd)
        {
            var A = point.X - lineStart.X;
            var B = point.Y - lineStart.Y;
            var C = lineEnd.X - lineStart.X;
            var D = lineEnd.Y - lineStart.Y;

            var dot = A * C + B * D;
            var lenSq = C * C + D * D;

            if (lenSq == 0) return Math.Sqrt(A * A + B * B);

            var param = dot / lenSq;

            double xx, yy;

            if (param < 0)
            {
                xx = lineStart.X;
                yy = lineStart.Y;
            }
            else if (param > 1)
            {
                xx = lineEnd.X;
                yy = lineEnd.Y;
            }
            else
            {
                xx = lineStart.X + param * C;
                yy = lineStart.Y + param * D;
            }

            var dx = point.X - xx;
            var dy = point.Y - yy;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        private void SelectionRectangle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isAdjusting) return;

            _isMoving = true;
            _activeControlPoint = ControlPointType.Move;
            _lastMousePosition = e.GetPosition(this);

            // 捕获鼠标到选择矩形
            SelectionRectangle.CaptureMouse();
            e.Handled = true;
        }

        private void SelectionRectangle_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isAdjusting || !_isMoving || _activeControlPoint != ControlPointType.Move) return;

            try
            {
                var currentPosition = e.GetPosition(this);
                var delta = currentPosition - _lastMousePosition;

                // 移动整个选择区域
                var newRect = _currentSelection;
                newRect.X += delta.X;
                newRect.Y += delta.Y;

                // 确保选择区域不会移出屏幕边界
                var screenBounds = new Rect(0, 0, ActualWidth, ActualHeight);
                if (newRect.Left < 0) newRect.X = 0;
                if (newRect.Top < 0) newRect.Y = 0;
                if (newRect.Right > screenBounds.Right) newRect.X = screenBounds.Right - newRect.Width;
                if (newRect.Bottom > screenBounds.Bottom) newRect.Y = screenBounds.Bottom - newRect.Height;

                _currentSelection = newRect;
                UpdateSelectionDisplay();

                _lastMousePosition = currentPosition;
                e.Handled = true;
            }
            catch (Exception)
            {
                // 如果出现异常，停止移动
                _isMoving = false;
                _activeControlPoint = ControlPointType.None;
                SelectionRectangle.ReleaseMouseCapture();
            }
        }

        private void SelectionRectangle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isMoving && _activeControlPoint == ControlPointType.Move)
            {
                _isMoving = false;
                _activeControlPoint = ControlPointType.None;
                SelectionRectangle.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void PerformFullScreenCapture()
        {
            try
            {
                // 获取虚拟屏幕边界
                var virtualScreen = SystemInformation.VirtualScreen;
                
                // 设置全屏截图区域
                SelectedArea = new DrawingRectangle(virtualScreen.X, virtualScreen.Y, virtualScreen.Width, virtualScreen.Height);
                SelectedPath = null; // 全屏截图不需要路径
                
                // 直接关闭窗口并返回结果
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                // 如果全屏截图失败，记录错误并关闭窗口
                System.Diagnostics.Debug.WriteLine($"全屏截图失败: {ex.Message}");
                DialogResult = false;
                Close();
            }
        }

        private void ResetSelectionState()
        {
            // 重置所有选择相关的状态
            _isSelecting = false;
            _isAdjusting = false;
            _isMoving = false;
            _isCameraMode = false;
            _activeControlPoint = ControlPointType.None;

            // 停止摄像头预览
            if (_cameraService != null)
            {
                _cameraService.StopPreview();
            }

            // 清除自由绘制的内容
            _freehandPoints.Clear();
            _freehandPolyline.Points.Clear();
            _freehandPolyline.Visibility = Visibility.Collapsed;

            // 清除矩形选择的内容
            SelectionRectangle.Visibility = Visibility.Collapsed;
            SelectionRectangle.IsHitTestVisible = false;
            ControlPointsCanvas.Visibility = Visibility.Collapsed;
            SizeInfoBorder.Visibility = Visibility.Collapsed;
            SelectionPath.Visibility = Visibility.Collapsed;
            HintTextBorder.Visibility = Visibility.Collapsed;

            // 隐藏摄像头预览
            CameraPreviewBorder.Visibility = Visibility.Collapsed;

            // 重置遮罩
            TransparentSelectionMask.Visibility = Visibility.Collapsed;
            OverlayRectangle.Visibility = Visibility.Visible;

            // 释放鼠标捕获
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }

            // 释放选择矩形的鼠标捕获
            if (SelectionRectangle.IsMouseCaptured)
            {
                SelectionRectangle.ReleaseMouseCapture();
            }

            // 重置选择区域
            _currentSelection = new Rect();
            SelectedArea = null;
            SelectedPath = null;
            CameraImage = null;

            RectangleModeButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色
            FreehandModeButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色
            FullScreenButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色
            CameraModeButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 清理摄像头资源
                if (_cameraService != null)
                {
                    _cameraService.StopPreview();
                    _cameraService.Dispose();
                    _cameraService = null;
                }

                // 清理摄像头图像
                _capturedCameraImage?.Dispose();
                CameraImage?.Dispose();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"清理资源失败: {ex.Message}", LogHelper.LogType.Error);
            }
            finally
            {
                base.OnClosed(e);
            }
        }
    }
}
