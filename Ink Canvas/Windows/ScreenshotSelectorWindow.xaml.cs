using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using DrawingRectangle = System.Drawing.Rectangle;
// 为了避免命名冲突，使用别名
using WpfCanvas = System.Windows.Controls.Canvas;

namespace Ink_Canvas
{
    public partial class ScreenshotSelectorWindow : Window
    {
        private bool _isSelecting = false;
        private bool _isFreehandMode = false;
        private System.Windows.Point _startPoint;
        private System.Windows.Point _currentPoint;
        private List<System.Windows.Point> _freehandPoints;
        private Polyline _freehandPolyline;

        public DrawingRectangle? SelectedArea { get; private set; }
        public List<System.Windows.Point> SelectedPath { get; private set; }

        public ScreenshotSelectorWindow()
        {
            InitializeComponent();

            // 设置窗口覆盖所有屏幕
            SetupFullScreenOverlay();

            // 初始化自由绘制模式
            InitializeFreehandMode();

            // 隐藏提示文字的定时器
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(5);
            timer.Tick += (s, e) =>
            {
                HintText.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();
        }

        private void InitializeFreehandMode()
        {
            _freehandPoints = new List<System.Windows.Point>();
            _freehandPolyline = new Polyline
            {
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };
            SelectionCanvas.Children.Add(_freehandPolyline);
        }

        private void SetupFullScreenOverlay()
        {
            // 获取所有屏幕的虚拟屏幕边界
            var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;

            // 转换为WPF坐标系统
            var dpiScale = GetDpiScale();

            this.Left = virtualScreen.Left / dpiScale;
            this.Top = virtualScreen.Top / dpiScale;
            this.Width = virtualScreen.Width / dpiScale;
            this.Height = virtualScreen.Height / dpiScale;
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
                // 取消截图
                SelectedArea = null;
                SelectedPath = null;
                DialogResult = false;
                Close();
            }
        }

        private void RectangleModeButton_Click(object sender, RoutedEventArgs e)
        {
            _isFreehandMode = false;
            RectangleModeButton.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)); // 蓝色
            FreehandModeButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色
            HintText.Text = "拖拽鼠标选择矩形区域";

            // 清除自由绘制的内容
            _freehandPoints.Clear();
            _freehandPolyline.Points.Clear();
            SelectionPath.Visibility = Visibility.Collapsed;
            SelectionRectangle.Visibility = Visibility.Collapsed;
        }

        private void FreehandModeButton_Click(object sender, RoutedEventArgs e)
        {
            _isFreehandMode = true;
            FreehandModeButton.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)); // 蓝色
            RectangleModeButton.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // 灰色
            HintText.Text = "按住鼠标左键绘制任意形状，松开完成";

            // 清除矩形选择的内容
            SelectionRectangle.Visibility = Visibility.Collapsed;
            _freehandPoints.Clear();
            _freehandPolyline.Points.Clear();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isSelecting = true;
            _startPoint = e.GetPosition(this);
            _currentPoint = _startPoint;

            // 隐藏提示文字
            HintText.Visibility = Visibility.Collapsed;

            if (_isFreehandMode)
            {
                // 自由绘制模式
                _freehandPoints.Clear();
                _freehandPolyline.Points.Clear();
                _freehandPoints.Add(_startPoint);
                _freehandPolyline.Points.Add(_startPoint);
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
            if (_isSelecting)
            {
                _currentPoint = e.GetPosition(this);

                if (_isFreehandMode)
                {
                    // 自由绘制模式：添加点到路径
                    _freehandPoints.Add(_currentPoint);
                    _freehandPolyline.Points.Add(_currentPoint);
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
            if (_isSelecting)
            {
                _isSelecting = false;
                ReleaseMouseCapture();

                if (_isFreehandMode)
                {
                    // 自由绘制模式：完成路径
                    if (_freehandPoints.Count > 3) // 至少需要3个点形成有效路径
                    {
                        // 闭合路径
                        _freehandPoints.Add(_startPoint);
                        _freehandPolyline.Points.Add(_startPoint);

                        // 保存选择的路径
                        SelectedPath = new List<System.Windows.Point>(_freehandPoints);

                        // 计算边界矩形用于截图
                        var bounds = CalculatePathBounds(_freehandPoints);
                        var dpiScale = GetDpiScale();
                        var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;

                        int screenX = (int)((bounds.X * dpiScale) + virtualScreen.Left);
                        int screenY = (int)((bounds.Y * dpiScale) + virtualScreen.Top);
                        int screenWidth = (int)(bounds.Width * dpiScale);
                        int screenHeight = (int)(bounds.Height * dpiScale);

                        SelectedArea = new DrawingRectangle(screenX, screenY, screenWidth, screenHeight);
                        DialogResult = true;
                    }
                    else
                    {
                        SelectedArea = null;
                        SelectedPath = null;
                        DialogResult = false;
                    }
                }
                else
                {
                    // 矩形模式
                    var rect = GetSelectionRectangle();
                    if (rect.Width > 5 && rect.Height > 5) // 最小尺寸检查
                    {
                        // 转换为屏幕坐标，考虑DPI缩放
                        var dpiScale = GetDpiScale();
                        var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;

                        // 计算实际屏幕坐标
                        int screenX = (int)((rect.X * dpiScale) + virtualScreen.Left);
                        int screenY = (int)((rect.Y * dpiScale) + virtualScreen.Top);
                        int screenWidth = (int)(rect.Width * dpiScale);
                        int screenHeight = (int)(rect.Height * dpiScale);

                        SelectedArea = new DrawingRectangle(screenX, screenY, screenWidth, screenHeight);
                        DialogResult = true;
                    }
                    else
                    {
                        SelectedArea = null;
                        DialogResult = false;
                    }
                }

                Close();
            }
        }

        private void UpdateSelection()
        {
            var rect = GetSelectionRectangle();

            // 更新选择矩形
            WpfCanvas.SetLeft(SelectionRectangle, rect.X);
            WpfCanvas.SetTop(SelectionRectangle, rect.Y);
            SelectionRectangle.Width = rect.Width;
            SelectionRectangle.Height = rect.Height;

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

        private Rect CalculatePathBounds(List<System.Windows.Point> points)
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
    }
}
