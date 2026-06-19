using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Ink_Canvas
{
    /// <summary>
    /// MiniWhiteboardWindow.xaml 的交互逻辑
    /// 浮窗小白板，提供简易的书写和绘图功能，支持多页管理和PPT联动
    /// </summary>
    public partial class MiniWhiteboardWindow : Window
    {

        // Page management
        private const int MaxPages = 99;
        private readonly List<StrokeCollection> _pageStrokes = new List<StrokeCollection>();
        private readonly List<TimeMachineHistory[]> _pageHistories = new List<TimeMachineHistory[]>();
        private int _currentPageIndex = 0; // 0-based internal index
        private int _totalCount = 1;

        // Multi-touch window drag
        private readonly Dictionary<int, Point> _touchPoints = new Dictionary<int, Point>();
        private bool _isMultiTouchDragging;
        private Point _multiTouchLastCenter;
        private InkCanvasEditingMode _lastMiniInkCanvasEditingMode = InkCanvasEditingMode.Ink;

        // Undo/redo per page
        private readonly List<bool> _pageLastModeIsRedo = new List<bool>();

        public MiniWhiteboardWindow()
        {
            InitializeComponent();

            // Initialize first page
            _pageStrokes.Add(new StrokeCollection());
            _pageHistories.Add(new TimeMachineHistory[] { });
            _pageLastModeIsRedo.Add(false);

            UpdatePageInfo();
            UpdateToolButtonsState();
        }

        #region Window Events

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Apply settings
            var settings = MainWindow.Settings.MiniWhiteboard ??= new MiniWhiteboardSettings();
            Width = settings.DefaultWidth;
            Height = settings.DefaultHeight;
            Opacity = settings.DefaultOpacity;

            // Apply window backdrop from settings (Mica/Acrylic/None)
            Helpers.WindowBackdropHelper.Apply(this);

            // Apply pen settings
            ApplyPenSettings();

            LogHelper.WriteLogToFile("小白板窗口已打开", LogHelper.LogType.Event);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save current page strokes before closing
            SaveCurrentPage();

            LogHelper.WriteLogToFile("小白板窗口已关闭", LogHelper.LogType.Event);
        }

        #endregion

        #region Multi-Touch Window Drag

        private void RootGrid_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            _touchPoints[e.TouchDevice.Id] = GetTouchScreenPosition(e);
            MiniInkCanvas.CaptureTouch(e.TouchDevice);

            if (_touchPoints.Count >= 2)
            {
                if (!_isMultiTouchDragging)
                {
                    _lastMiniInkCanvasEditingMode = MiniInkCanvas.EditingMode;
                    MiniInkCanvas.EditingMode = InkCanvasEditingMode.None;
                    _isMultiTouchDragging = true;
                }

                _multiTouchLastCenter = GetTouchCenter();
                e.Handled = true;
            }
        }

        private void RootGrid_PreviewTouchMove(object sender, TouchEventArgs e)
        {
            if (!_touchPoints.ContainsKey(e.TouchDevice.Id)) return;

            _touchPoints[e.TouchDevice.Id] = GetTouchScreenPosition(e);

            // 单指移动：不拦截，交给 InkCanvas 绘制
            if (_touchPoints.Count < 2 || !_isMultiTouchDragging) return;

            var center = GetTouchCenter();
            var deltaX = center.X - _multiTouchLastCenter.X;
            var deltaY = center.Y - _multiTouchLastCenter.Y;

            Left += deltaX;
            Top += deltaY;

            _multiTouchLastCenter = center;
            e.Handled = true;
        }

        private void RootGrid_PreviewTouchUp(object sender, TouchEventArgs e)
        {
            _touchPoints.Remove(e.TouchDevice.Id);
            MiniInkCanvas.ReleaseTouchCapture(e.TouchDevice);

            if (_touchPoints.Count < 2 && _isMultiTouchDragging)
            {
                _isMultiTouchDragging = false;
                MiniInkCanvas.EditingMode = _lastMiniInkCanvasEditingMode;
            }

            if (_touchPoints.Count == 0)
            {
                MiniInkCanvas.ReleaseAllTouchCaptures();
            }
        }

        private Point GetTouchCenter()
        {
            double x = 0, y = 0;
            foreach (var pt in _touchPoints.Values)
            {
                x += pt.X;
                y += pt.Y;
            }
            return new Point(x / _touchPoints.Count, y / _touchPoints.Count);
        }

        private Point GetTouchScreenPosition(TouchEventArgs e)
        {
            var point = e.GetTouchPoint(this).Position;
            return PointToScreen(point);
        }

        #endregion

        #region Tool Buttons

        private void PenBtn_Click(object sender, MouseButtonEventArgs e)
        {
            MiniInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            UpdateToolButtonsState();
        }

        private void EraserBtn_Click(object sender, MouseButtonEventArgs e)
        {
            MiniInkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
            UpdateToolButtonsState();
        }

        private void UndoBtn_Click(object sender, MouseButtonEventArgs e)
        {
            if (MiniInkCanvas.Strokes.Count == 0) return;

            SaveCurrentPage();

            // Simple undo: remove last stroke
            var lastStroke = MiniInkCanvas.Strokes[MiniInkCanvas.Strokes.Count - 1];
            MiniInkCanvas.Strokes.Remove(lastStroke);

            // Store in redo history
            var history = _pageHistories[_currentPageIndex];
            if (history == null)
            {
                history = new TimeMachineHistory[] { };
                _pageHistories[_currentPageIndex] = history;
            }
        }

        private void ClearBtn_Click(object sender, MouseButtonEventArgs e)
        {
            if (MiniInkCanvas.Strokes.Count == 0) return;

            SaveCurrentPage();
            MiniInkCanvas.Strokes.Clear();
        }

        private static readonly Brush SelectedBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));

        private void UpdateToolButtonsState()
        {
            bool isInkMode = MiniInkCanvas.EditingMode == InkCanvasEditingMode.Ink;

            var iconFg = FindResource("IconForeground") as Brush ?? Brushes.White;
            var selected = FindResource("BoardFloatBarSelectedBackground") as Brush ?? SelectedBrush;

            // Update pen button visual
            if (PenBtn != null)
            {
                PenBtn.Background = isInkMode ? selected : Brushes.Transparent;
            }

            // Update eraser button visual
            if (EraserBtn != null)
            {
                EraserBtn.Background = !isInkMode ? selected : Brushes.Transparent;
            }
        }

        #endregion

        #region Page Management

        private void PrevPageBtn_Click(object sender, MouseButtonEventArgs e)
        {
            if (_currentPageIndex <= 0) return;
            SwitchToPage(_currentPageIndex - 1);
        }

        private void NextPageBtn_Click(object sender, MouseButtonEventArgs e)
        {
            if (_currentPageIndex >= _totalCount - 1)
            {
                // Add new page
                AddNewPage();
            }
            else
            {
                SwitchToPage(_currentPageIndex + 1);
            }
        }

        private void AddPageBtn_Click(object sender, MouseButtonEventArgs e)
        {
            if (_totalCount >= MaxPages) return;
            AddNewPage();
        }

        private void AddNewPage()
        {
            if (_totalCount >= MaxPages) return;

            SaveCurrentPage();

            _pageStrokes.Add(new StrokeCollection());
            _pageHistories.Add(new TimeMachineHistory[] { });
            _pageLastModeIsRedo.Add(false);
            _totalCount++;

            SwitchToPage(_totalCount - 1);
        }

        private void SwitchToPage(int targetIndex)
        {
            if (targetIndex < 0 || targetIndex >= _totalCount) return;

            // Save current page
            SaveCurrentPage();

            // Switch
            _currentPageIndex = targetIndex;

            // Restore strokes for target page
            MiniInkCanvas.Strokes.Clear();
            if (_pageStrokes[targetIndex] != null && _pageStrokes[targetIndex].Count > 0)
            {
                foreach (var stroke in _pageStrokes[targetIndex])
                {
                    MiniInkCanvas.Strokes.Add(stroke.Clone());
                }
            }

            UpdatePageInfo();
        }

        private void SaveCurrentPage()
        {
            if (_currentPageIndex < 0 || _currentPageIndex >= _pageStrokes.Count) return;

            // Clone current strokes to storage
            var strokes = new StrokeCollection();
            foreach (var stroke in MiniInkCanvas.Strokes)
            {
                strokes.Add(stroke.Clone());
            }
            _pageStrokes[_currentPageIndex] = strokes;
        }

        private void UpdatePageInfo()
        {
            if (PageInfoText != null)
            {
                PageInfoText.Text = $"{_currentPageIndex + 1}/{_totalCount}";
            }
        }

        #endregion

        #region PPT Integration

        // PPT 翻页事件由 MainWindow (MW_PPT.cs) 统一转发到 OnPPTSlideChangedExternal
        // 不再直接订阅 PPTManager.SlideShowNextSlide，避免双重触发

        #endregion

        #region Pen Settings

        // 调色盘颜色索引：0=White, 1=Black, 2=Red, 3=Orange, 4=Yellow, 5=Green, 6=Blue, 7=Purple
        private static readonly Color[] PaletteColors = new Color[]
        {
            Colors.White,
            Colors.Black,
            Color.FromRgb(0xFF, 0x00, 0x00), // Red
            Color.FromRgb(0xFF, 0xA5, 0x00), // Orange
            Color.FromRgb(0xFF, 0xFF, 0x00), // Yellow
            Color.FromRgb(0x16, 0xA3, 0x4A), // Green
            Color.FromRgb(0x25, 0x63, 0xEB), // Blue
            Color.FromRgb(0x93, 0x33, 0xEA), // Purple
        };

        private void ApplyPenSettings()
        {
            var settings = MainWindow.Settings.MiniWhiteboard ??= new MiniWhiteboardSettings();

            // 优先使用 colorIndex，兼容旧的 penColor 字符串
            int colorIdx = settings.CurrentColorIndex;
            if (colorIdx >= 0 && colorIdx < PaletteColors.Length)
            {
                MiniInkCanvas.DefaultDrawingAttributes.Color = PaletteColors[colorIdx];
            }
            else if (!string.IsNullOrEmpty(settings.PenColor) && settings.PenColor.StartsWith("#"))
            {
                try
                {
                    MiniInkCanvas.DefaultDrawingAttributes.Color = (Color)ColorConverter.ConvertFromString(settings.PenColor);
                }
                catch { }
            }

            MiniInkCanvas.DefaultDrawingAttributes.Width = settings.PenWidth;
            MiniInkCanvas.DefaultDrawingAttributes.Height = settings.PenWidth;

            UpdateColorIndicator();
        }

        private void UpdateColorIndicator()
        {
            var settings = MainWindow.Settings.MiniWhiteboard ??= new MiniWhiteboardSettings();
            int idx = settings.CurrentColorIndex;
            if (idx >= 0 && idx < PaletteColors.Length)
            {
                ColorIndicator.Fill = new SolidColorBrush(PaletteColors[idx]);
            }
        }

        #endregion

        #region Color Palette

        private void ColorBtn_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            ColorPalettePopup.IsOpen = !ColorPalettePopup.IsOpen;
        }

        private void ColorSwatch_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Ellipse ellipse && ellipse.Fill is SolidColorBrush brush)
            {
                Color selectedColor = brush.Color;
                MiniInkCanvas.DefaultDrawingAttributes.Color = selectedColor;
                ColorIndicator.Fill = new SolidColorBrush(selectedColor);

                // 保存颜色索引到 settings
                var settings = MainWindow.Settings.MiniWhiteboard ??= new MiniWhiteboardSettings();
                int idx = Array.IndexOf(PaletteColors, selectedColor);
                if (idx >= 0)
                {
                    settings.CurrentColorIndex = idx;
                    settings.PenColor = selectedColor.ToString();
                }

                ColorPalettePopup.IsOpen = false;
                e.Handled = true;
            }
        }

        #endregion

        #region Fold Button

        private void FoldBtn_Click(object _, MouseButtonEventArgs e)
        {
            e.Handled = true;
            SaveCurrentPage();
            Hide();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 外部调用：PPT页面切换时通知小白板（由 MainWindow 转发）
        /// </summary>
        public void OnPPTSlideChangedExternal(int slideIndex)
        {
            if (!MainWindow.Settings.MiniWhiteboard.SyncWithPPTPages) return;
            if (slideIndex < 0) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                while (_totalCount <= slideIndex)
                {
                    _pageStrokes.Add(new StrokeCollection());
                    _pageHistories.Add(new TimeMachineHistory[] { });
                    _pageLastModeIsRedo.Add(false);
                    _totalCount++;
                }

                SwitchToPage(slideIndex);
            }));
        }

        /// <summary>
        /// 获取当前页面索引（0-based）
        /// </summary>
        public int CurrentPageIndex => _currentPageIndex;

        /// <summary>
        /// 获取总页数
        /// </summary>
        public int TotalPageCount => _totalCount;

        /// <summary>
        /// 外部调用：将墨迹插入当前小白板页面，自动缩放至可见范围内
        /// </summary>
        /// <param name="strokes">要插入的墨迹集合（坐标基于主画布全屏坐标系，会被克隆并缩放）</param>
        public void InsertStrokes(StrokeCollection strokes)
        {
            if (strokes == null || strokes.Count == 0) return;

            SaveCurrentPage();

            var cloned = strokes.Clone();

            // 将墨迹坐标从主画布（全屏）映射到小白板（缩放+居中）
            TransformStrokesToMiniCanvas(cloned);

            MiniInkCanvas.Strokes.Add(cloned);

            // 确保新插入的墨迹不处于选中态（参考ICA克隆模式）
            MiniInkCanvas.Select((StrokeCollection)null);

            SaveCurrentPage();
        }

        /// <summary>
        /// 将墨迹坐标从全屏主画布映射到小白板画布（等比缩放+居中）
        /// </summary>
        private void TransformStrokesToMiniCanvas(StrokeCollection strokes)
        {
            if (strokes.Count == 0) return;

            // 主画布尺寸 = 屏幕分辨率（墨迹坐标基于此）
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            if (screen == null) return;
            double mainWidth = screen.Bounds.Width;
            double mainHeight = screen.Bounds.Height;
            if (mainWidth <= 0 || mainHeight <= 0) return;

            // 小白板画布实际渲染尺寸
            double miniWidth = MiniInkCanvas.ActualWidth;
            double miniHeight = MiniInkCanvas.ActualHeight;
            if (miniWidth <= 0 || miniHeight <= 0) return;

            // 避免与屏幕尺寸完全相同时不做无意义变换
            if (Math.Abs(mainWidth - miniWidth) < 1 && Math.Abs(mainHeight - miniHeight) < 1) return;

            // 等比缩放因子（取较小比，确保完全可见）
            double scaleX = miniWidth / mainWidth;
            double scaleY = miniHeight / mainHeight;
            double scale = Math.Min(scaleX, scaleY);

            // 偏移量：缩放后居中
            double scaledWidth = mainWidth * scale;
            double scaledHeight = mainHeight * scale;
            double offsetX = (miniWidth - scaledWidth) / 2.0;
            double offsetY = (miniHeight - scaledHeight) / 2.0;

            var m = new Matrix();
            m.Scale(scale, scale);
            m.Translate(offsetX, offsetY);

            foreach (var stroke in strokes)
            {
                var pts = stroke.StylusPoints;
                var newPts = new StylusPointCollection();
                foreach (var pt in pts)
                {
                    var transformed = m.Transform(new Point(pt.X, pt.Y));
                    newPts.Add(new StylusPoint(transformed.X, transformed.Y, pt.PressureFactor));
                }
                stroke.StylusPoints = newPts;
            }
        }

        #endregion
    }
}
