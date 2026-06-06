using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas
{
    /// <summary>
    /// MiniWhiteboardWindow.xaml 的交互逻辑
    /// 浮窗小白板，提供简易的书写和绘图功能，支持多页管理和PPT联动
    /// </summary>
    public partial class MiniWhiteboardWindow : Window
    {
        private readonly MainWindow _mainWindow;

        // Page management
        private const int MaxPages = 99;
        private readonly List<StrokeCollection> _pageStrokes = new List<StrokeCollection>();
        private readonly List<TimeMachineHistory[]> _pageHistories = new List<TimeMachineHistory[]>();
        private int _currentPageIndex = 0; // 0-based internal index
        private int _totalCount = 1;

        // Undo/redo per page
        private readonly List<bool> _pageLastModeIsRedo = new List<bool>();

        public MiniWhiteboardWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;

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

            // Subscribe to PPT slide change events if sync is enabled
            if (settings.SyncWithPptPages)
            {
                SubscribeToPPTEvents();
            }

            LogHelper.WriteLogToFile("小白板窗口已打开", LogHelper.LogType.Event);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save current page strokes before closing
            SaveCurrentPage();

            // Unsubscribe from PPT events
            UnsubscribeFromPPTEvents();

            LogHelper.WriteLogToFile("小白板窗口已关闭", LogHelper.LogType.Event);
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

        private void RedoBtn_Click(object sender, MouseButtonEventArgs e)
        {
            // Redo would need proper TimeMachineHistory integration
            // For now, this is a placeholder
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

        private void SubscribeToPPTEvents()
        {
            try
            {
                if (_mainWindow?.PPTManager != null)
                {
                    _mainWindow.PPTManager.SlideShowNextSlide += OnPPTSlideChanged;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"小白板订阅PPT事件失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void UnsubscribeFromPPTEvents()
        {
            try
            {
                if (_mainWindow?.PPTManager != null)
                {
                    _mainWindow.PPTManager.SlideShowNextSlide -= OnPPTSlideChanged;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"小白板取消订阅PPT事件失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void OnPPTSlideChanged(object slideShowWindow)
        {
            try
            {
                if (!MainWindow.Settings.MiniWhiteboard.SyncWithPptPages) return;

                // Get current slide index from PPT manager
                var pptManager = _mainWindow?.PPTManager;
                if (pptManager == null) return;

                // The slide index is 1-based in PPT, convert to 0-based for our pages
                int slideIndex = GetCurrentSlideIndex(pptManager);
                if (slideIndex < 0) return;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Ensure we have enough pages
                    while (_totalCount <= slideIndex)
                    {
                        _pageStrokes.Add(new StrokeCollection());
                        _pageHistories.Add(new TimeMachineHistory[] { });
                        _pageLastModeIsRedo.Add(false);
                        _totalCount++;
                    }

                    // Switch to the corresponding page (0-based)
                    SwitchToPage(slideIndex);
                }));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"小白板处理PPT翻页失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private int GetCurrentSlideIndex(IPPTLinkManager pptManager)
        {
            try
            {
                // Use reflection or public API to get current slide index
                // The PPTManager exposes slide info through various means
                // For now, we use the main window's tracked position
                return _mainWindow.CurrentPPTSlideIndex;
            }
            catch
            {
                return -1;
            }
        }

        #endregion

        #region Pen Settings

        private void ApplyPenSettings()
        {
            var settings = MainWindow.Settings.MiniWhiteboard ??= new MiniWhiteboardSettings();

            // Parse pen color (default: white for dark board background)
            Color penColor = Colors.White;
            if (!string.IsNullOrEmpty(settings.PenColor) && settings.PenColor.StartsWith("#"))
            {
                try
                {
                    penColor = (Color)ColorConverter.ConvertFromString(settings.PenColor);
                }
                catch { }
            }

            // Apply to canvas
            MiniInkCanvas.DefaultDrawingAttributes.Color = penColor;
            MiniInkCanvas.DefaultDrawingAttributes.Width = settings.PenWidth;
            MiniInkCanvas.DefaultDrawingAttributes.Height = settings.PenWidth;
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
        /// 外部调用：PPT页面切换时通知小白板
        /// </summary>
        public void OnPPTSlideChangedExternal(int slideIndex)
        {
            if (!MainWindow.Settings.MiniWhiteboard.SyncWithPptPages) return;
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

        #endregion
    }
}
