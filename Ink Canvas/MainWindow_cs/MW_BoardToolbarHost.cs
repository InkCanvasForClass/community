using Ink_Canvas.Controls;
using Ink_Canvas.Controls.Toolbar.BoardToolbar;
using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas
{
    public partial class MainWindow : IBoardToolbarHost
    {
        private WhiteboardPageManager _pageManager;
        private Dictionary<string, FrameworkElement> _boardToolbarViews = new Dictionary<string, FrameworkElement>();

        MainWindow IBoardToolbarHost.Window => this;

        public void RegisterView(string id, FrameworkElement view)
        {
            _boardToolbarViews[id] = view;
        }

        public FrameworkElement FindView(string id)
        {
            return _boardToolbarViews.TryGetValue(id, out var view) ? view : null;
        }

        public void SwitchToPreviousPage()
        {
            if (!_pageManager.CanGoToPrevious) return;

            if (currentSelectedElement != null)
            {
                var previousEditingMode = inkCanvas.EditingMode;
                UnselectElement(currentSelectedElement);
                inkCanvas.EditingMode = previousEditingMode;
                currentSelectedElement = null;
            }

            VideoPresenter_BeforePageLeave();
            SaveStrokes();

            ClearStrokes(true);
            _pageManager.GoToPreviousPage();

            RestoreStrokes();
            VideoPresenter_OnPageChanged();
            UpdateBoardToolbarState();
        }

        public void SwitchToNextPage()
        {
            if (!_pageManager.CanGoToNext)
            {
                AddWhiteboardPage();
                return;
            }

            if (Settings.Automation.IsAutoSaveScreenshotAtClear &&
                inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber)
                CaptureAndEnqueueScreenshotSave(isHideNotification: true);

            if (currentSelectedElement != null)
            {
                var previousEditingMode = inkCanvas.EditingMode;
                UnselectElement(currentSelectedElement);
                inkCanvas.EditingMode = previousEditingMode;
                currentSelectedElement = null;
            }

            VideoPresenter_BeforePageLeave();
            SaveStrokes();

            ClearStrokes(true);
            _pageManager.GoToNextPage();

            RestoreStrokes();
            VideoPresenter_OnPageChanged();
            UpdateBoardToolbarState();
        }

        public void AddWhiteboardPage()
        {
            MarkCurrentPageInkChanged();
            if (!_pageManager.CanAddNewPage) return;

            if (Settings.Automation.IsAutoSaveScreenshotAtClear &&
                inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber)
                CaptureAndEnqueueScreenshotSave(isHideNotification: true);

            if (currentSelectedElement != null)
            {
                var previousEditingMode = inkCanvas.EditingMode;
                UnselectElement(currentSelectedElement);
                inkCanvas.EditingMode = previousEditingMode;
                currentSelectedElement = null;
            }

            VideoPresenter_BeforePageLeave();
            SaveStrokes();
            ClearStrokes(true);

            _pageManager.AddNewPage();

            RestoreStrokes();
            VideoPresenter_OnPageChanged();
            UpdateBoardToolbarState();

            var leftPageListView = FindView("board.pageList.left") as System.Windows.Controls.ListView;
            if (leftPageListView?.Visibility == Visibility.Visible)
            {
                RefreshBlackBoardSidePageListView();
            }
        }

        public void DeleteWhiteboardPage()
        {
            if (!_pageManager.CanDeletePage) return;

            if (IsPageFrozen(_pageManager.CurrentIndex))
            {
                ShowNotification(MainWindowStrings.Main_Board_FrozenCannotDelete);
                return;
            }

            if (currentSelectedElement != null)
            {
                var previousEditingMode = inkCanvas.EditingMode;
                UnselectElement(currentSelectedElement);
                inkCanvas.EditingMode = previousEditingMode;
                currentSelectedElement = null;
            }

            ClearStrokes(true);
            _pageManager.DeletePage(_pageManager.CurrentIndex);
            RestoreStrokes();
            UpdateBoardToolbarState();

            var leftBorder = FindView("board.pageList.leftBorder") as Border;
            var rightBorder = FindView("board.pageList.rightBorder") as Border;
            if (leftBorder?.Visibility == Visibility.Visible ||
                rightBorder?.Visibility == Visibility.Visible)
                RefreshBlackBoardSidePageListView();
        }

        public void ToggleGesture()
        {
            TwoFingerGestureBorder_MouseUp(null, null);
        }

        public void ChangeBackgroundColor()
        {
            BoardChangeBackgroundColorBtn_MouseUp(null, null);
        }

        public void SelectTool()
        {
            BoardLassoIcon_Click(null, null);
        }

        public void SelectPen()
        {
            PenIcon_Click(null, null);
        }

        public void SelectEraser()
        {
            BoardEraserIcon_Click(null, null);
        }

        public void SelectStrokeEraser()
        {
            BoardEraserIconByStrokes_Click(null, null);
        }

        public void SelectShape()
        {
            ImageDrawShape_MouseUp(null, null);
        }

        public void InsertImage()
        {
            InsertImageOptions_MouseUp(null, null);
        }

        public void Undo()
        {
            SymbolIconUndo_MouseUp(null, null);
        }

        public void Redo()
        {
            SymbolIconRedo_MouseUp(null, null);
        }

        public void ToggleInkFreeze()
        {
            BoardInkFreeze_MouseUp(null, null);
        }

        public void OpenTools()
        {
            SymbolIconTools_MouseUp(null, null);
        }

        public void ExitWhiteboard()
        {
            ImageBlackboard_MouseUp(null, null);
        }

        public bool CanUndo => IsUndoEnabled;
        public bool CanRedo => IsRedoEnabled;
        public bool CanSwitchToPreviousPage => _pageManager?.CanGoToPrevious ?? false;
        public bool CanSwitchToNextPage => _pageManager?.CanGoToNext ?? false;
        public bool CanAddNewPage => _pageManager?.CanAddNewPage ?? false;
        public bool CanDeletePage => _pageManager?.CanDeletePage ?? false;

        public string CurrentPageInfo => _pageManager?.PageInfo ?? "1/1";

        public void UpdatePageInfo()
        {
            var pageInfoTextBlock = FindView("board.pageInfo") as TextBlock;
            if (pageInfoTextBlock != null)
            {
                _pageManager?.UpdatePageInfoDisplay(pageInfoTextBlock);
            }
        }

        private void InitializeBoardToolbar()
        {
            try
            {
                _pageManager = new WhiteboardPageManager();
                _pageManager.PageChanged += OnWhiteboardPageChanged;

                BoardToolbarRegistry.EnsureDefaultConfigExists();

                var host = (IBoardToolbarHost)this;

                BoardToolbarRegistry.RebuildToolbar(host, BlackboardLeftSidePanel, BlackboardCenterSidePanel, BlackboardRightSidePanel);

                BindPopupPlacementTargets();
                BindPageInfoClickHandler();
                CreatePagePreviewUI();

                UpdateBoardToolbarState();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"MW_BoardToolbarHost: InitializeBoardToolbar 异常: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void BindPopupPlacementTargets()
        {
            SetPopupPlacementTarget(BoardTwoFingerGestureBorder, "board.gesture");
            SetPopupPlacementTarget(BackgroundPalette, "board.backgroundColor");
            SetPopupPlacementTarget(BoardPenPalette, "board.pen");
            SetPopupPlacementTarget(BoardEraserSizePanel, "board.eraser");
            SetPopupPlacementTarget(BoardBorderDrawShape, "board.shape");
            SetPopupPlacementTarget(BoardImageOptionsPanel, "board.insertImage");
            SetPopupPlacementTarget(BoardBorderToolsPopup, "board.tools");
        }

        private void SetPopupPlacementTarget(Popup popup, string buttonId)
        {
            if (popup == null) return;
            var btn = FindView(buttonId);
            if (btn != null)
            {
                popup.PlacementTarget = btn;
            }
        }

        private void OnWhiteboardPageChanged()
        {
            UpdateBoardToolbarState();
        }

        private void UpdateBoardToolbarState()
        {
            if (_pageManager == null) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdatePageInfo();

                var previousPageBtn = FindView("board.previousPage") as BoardToolbarButton;
                if (previousPageBtn != null)
                {
                    previousPageBtn.IsEnabled = CanSwitchToPreviousPage;
                }

                var nextPageBtn = FindView("board.nextPage") as BoardToolbarButton;
                if (nextPageBtn != null)
                {
                    nextPageBtn.IsEnabled = CanSwitchToNextPage;
                    if (nextPageBtn.LabelTextBlockControl != null)
                    {
                        nextPageBtn.LabelTextBlockControl.Text = _pageManager.CanGoToNext
                            ? FloatingBarStrings.Board_NextPage
                            : FloatingBarStrings.Board_NewPage;
                    }
                }

                var undoBtn = FindView("board.undo") as BoardToolbarButton;
                if (undoBtn != null)
                {
                    undoBtn.IsEnabled = CanUndo;
                }

                var redoBtn = FindView("board.redo") as BoardToolbarButton;
                if (redoBtn != null)
                {
                    redoBtn.IsEnabled = CanRedo;
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void BindPageInfoClickHandler()
        {
            var pageInfoBtn = FindView("board.pageList.rightBtn") as Border;
            if (pageInfoBtn != null)
            {
                pageInfoBtn.MouseDown += (s, e) => BtnWhiteBoardPageIndex_Click(s, e);
            }
        }

        private void CreatePagePreviewUI()
        {
            var template = new DataTemplate();

            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)));
            borderFactory.SetValue(Border.MarginProperty, new Thickness(2));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(4));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

            var gridFactory = new FrameworkElementFactory(typeof(Grid));
            var row1 = new FrameworkElementFactory(typeof(RowDefinition));
            row1.SetValue(RowDefinition.HeightProperty, new GridLength(60));
            var row2 = new FrameworkElementFactory(typeof(RowDefinition));
            row2.SetValue(RowDefinition.HeightProperty, GridLength.Auto);
            gridFactory.AppendChild(row1);
            gridFactory.AppendChild(row2);

            var inkCanvasFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.InkCanvas));
            inkCanvasFactory.SetValue(Grid.RowProperty, 0);
            inkCanvasFactory.SetValue(System.Windows.Controls.InkCanvas.BackgroundProperty, Brushes.White);
            inkCanvasFactory.AddHandler(FrameworkElement.LoadedEvent, new RoutedEventHandler((s, e) =>
            {
                if (s is System.Windows.Controls.InkCanvas ic && ic.DataContext is PageListViewItem item)
                {
                    ic.Strokes.Clear();
                    if (item.Strokes != null)
                    {
                        ic.Strokes.Add(item.Strokes);
                    }
                }
            }));

            var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
            textBlockFactory.SetValue(Grid.RowProperty, 1);
            textBlockFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Index"));
            textBlockFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            textBlockFactory.SetValue(TextBlock.ForegroundProperty, Application.Current.TryFindResource("FloatBarForeground") as Brush ?? Brushes.White);
            textBlockFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
            textBlockFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 2, 0, 0));

            gridFactory.AppendChild(inkCanvasFactory);
            gridFactory.AppendChild(textBlockFactory);
            borderFactory.AppendChild(gridFactory);
            template.VisualTree = borderFactory;

            var rightListView = new System.Windows.Controls.ListView
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                SelectionMode = SelectionMode.Single,
                ItemTemplate = template
            };
            rightListView.MouseUp += BlackBoardRightSidePageListView_OnMouseUp;

            var rightScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = rightListView
            };

            var rightBorder = new Border
            {
                Width = 120,
                MaxHeight = 400,
                CornerRadius = new CornerRadius(8),
                Background = Application.Current.TryFindResource("FloatBarBackground") as Brush ?? new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Child = rightScrollViewer,
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 10, 70)
            };

            RegisterView("board.pageList.right", rightListView);
            RegisterView("board.pageList.rightScrollViewer", rightScrollViewer);
            RegisterView("board.pageList.rightBorder", rightBorder);

            var parentGrid = BlackboardRightSidePanel?.Parent as Grid;
            var mainGrid = parentGrid?.Parent as Grid;
            if (mainGrid != null)
            {
                mainGrid.Children.Add(rightBorder);
            }
        }
    }
}
