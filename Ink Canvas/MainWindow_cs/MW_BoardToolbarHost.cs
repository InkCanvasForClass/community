using Ink_Canvas.Controls;
using Ink_Canvas.Controls.Toolbar.BoardToolbar;
using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using iNKORE.UI.WPF.Controls;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using iNKORE.UI.WPF.Modern.Controls;
using SegoeFluentIcons = iNKORE.UI.WPF.Modern.Common.IconKeys.SegoeFluentIcons;

namespace Ink_Canvas
{
    public partial class MainWindow : IBoardToolbarHost
    {
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
            BtnWhiteBoardSwitchPrevious_Click(this, null);
        }

        public void SwitchToNextPage()
        {
            BtnWhiteBoardSwitchNext_Click(this, null);
        }

        public void AddWhiteboardPage()
        {
            BtnWhiteBoardAdd_Click(this, null);
        }

        public void DeleteWhiteboardPage()
        {
            BtnWhiteBoardDelete_Click(this, null);
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
        public bool CanSwitchToPreviousPage => CurrentWhiteboardIndex > 1;
        public bool CanSwitchToNextPage => CurrentWhiteboardIndex < WhiteboardTotalCount;
        public bool CanAddNewPage => WhiteboardTotalCount < 99;
        public bool CanDeletePage => WhiteboardTotalCount > 1;

        public string CurrentPageInfo => $"{CurrentWhiteboardIndex}/{WhiteboardTotalCount}";

        public void UpdatePageInfo()
        {
            var leftPageInfo = FindView("board.pageInfo.left") as TextBlock;
            if (leftPageInfo != null)
            {
                leftPageInfo.Text = CurrentPageInfo;
            }

            var rightPageInfo = FindView("board.pageInfo.right") as TextBlock;
            if (rightPageInfo != null)
            {
                rightPageInfo.Text = CurrentPageInfo;
            }
        }

        private void InitializeBoardToolbar()
        {
            try
            {
                BoardToolbarRegistry.EnsureDefaultConfigExists();

                var host = (IBoardToolbarHost)this;

                BoardToolbarRegistry.RebuildToolbar(host, BlackboardLeftSidePanel, BlackboardCenterSidePanel, BlackboardRightSidePanel);

                BindPopupPlacementTargets();
                BindPageInfoClickHandler();
                CreatePagePreviewUI();
                RefreshBlackBoardSidePageListView();

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

        private void UpdateBoardToolbarState()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdatePageInfo();

                var leftPreviousPageBtn = FindView("board.previousPage.left") as BoardToolbarButton;
                var rightPreviousPageBtn = FindView("board.previousPage.right") as BoardToolbarButton;
                if (leftPreviousPageBtn != null)
                    leftPreviousPageBtn.IsEnabled = CanSwitchToPreviousPage;
                if (rightPreviousPageBtn != null)
                    rightPreviousPageBtn.IsEnabled = CanSwitchToPreviousPage;

                var leftNextPageBtn = FindView("board.nextPage.left") as BoardToolbarButton;
                var rightNextPageBtn = FindView("board.nextPage.right") as BoardToolbarButton;
                var nextPageLabel = CanSwitchToNextPage
                    ? FloatingBarStrings.Board_NextPage
                    : FloatingBarStrings.Board_NewPage;
                if (leftNextPageBtn != null)
                {
                    leftNextPageBtn.IsEnabled = CanSwitchToNextPage || CanAddNewPage;
                    if (leftNextPageBtn.LabelTextBlockControl != null)
                        leftNextPageBtn.LabelTextBlockControl.Text = nextPageLabel;
                }
                if (rightNextPageBtn != null)
                {
                    rightNextPageBtn.IsEnabled = CanSwitchToNextPage || CanAddNewPage;
                    if (rightNextPageBtn.LabelTextBlockControl != null)
                        rightNextPageBtn.LabelTextBlockControl.Text = nextPageLabel;
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
            var leftBtn = FindView("board.pageList.leftBtn") as Border;
            if (leftBtn != null)
            {
                leftBtn.MouseUp += BtnWhiteBoardPageIndex_Click;
            }

            var rightBtn = FindView("board.pageList.rightBtn") as Border;
            if (rightBtn != null)
            {
                rightBtn.MouseUp += BtnWhiteBoardPageIndex_Click;
            }
        }

        private void CreatePagePreviewUI()
        {
            CreatePageListView(
                "board.pageList.leftBorder",
                "board.pageList.left",
                "board.pageList.leftScrollViewer",
                BlackBoardLeftSidePageListView_OnMouseUp,
                -134, -465, -60, 50,
                "board.pageList.leftBtn"
            );

            CreatePageListView(
                "board.pageList.rightBorder",
                "board.pageList.right",
                "board.pageList.rightScrollViewer",
                BlackBoardRightSidePageListView_OnMouseUp,
                -138, -465, -56, 50,
                "board.pageList.rightBtn"
            );
        }

        private void CreatePageListView(
            string borderId,
            string listViewId,
            string scrollViewerId,
            MouseButtonEventHandler mouseUpHandler,
            double marginLeft, double marginTop, double marginRight, double marginBottom,
            string btnId)
        {
            var itemTemplate = CreatePageListItemTemplate(mouseUpHandler);

            var listView = new System.Windows.Controls.ListView
            {
                Name = listViewId.Replace(".", "_"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                SelectionMode = SelectionMode.Single,
                ItemTemplate = itemTemplate,
                ItemsSource = blackBoardSidePageListViewObservableCollection
            };
            ScrollViewer.SetCanContentScroll(listView, false);
            ScrollViewer.SetHorizontalScrollBarVisibility(listView, ScrollBarVisibility.Disabled);
            ScrollViewer.SetVerticalScrollBarVisibility(listView, ScrollBarVisibility.Disabled);

            var scrollViewer = new ScrollViewer
            {
                Name = scrollViewerId.Replace(".", "_"),
                Height = 460,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                CanContentScroll = false,
                Content = listView,
                PanningMode = PanningMode.VerticalOnly,
                IsManipulationEnabled = true
            };

            var border = new Border
            {
                Name = borderId.Replace(".", "_"),
                ClipToBounds = true,
                Margin = new Thickness(marginLeft, marginTop, marginRight, marginBottom),
                CornerRadius = new CornerRadius(8),
                Background = (Brush)Application.Current.TryFindResource("FloatBarBackground"),
                Opacity = 1,
                BorderBrush = (Brush)Application.Current.TryFindResource("BoardFloatBarBorderBrush"),
                BorderThickness = new Thickness(1),
                Child = scrollViewer,
                Visibility = Visibility.Collapsed
            };

            RegisterView(borderId, border);
            RegisterView(listViewId, listView);
            RegisterView(scrollViewerId, scrollViewer);

            var btn = FindView(btnId) as Border;
            if (btn != null)
            {
                var parentPanel = btn.Parent as Panel;
                if (parentPanel != null)
                {
                    var hiddenGrid = new Grid { Width = 0, Margin = new Thickness(0, 0, 0, 5) };
                    hiddenGrid.Children.Add(border);
                    int btnIndex = parentPanel.Children.IndexOf(btn);
                    parentPanel.Children.Insert(btnIndex + 1, hiddenGrid);
                }
            }
        }

        private DataTemplate CreatePageListItemTemplate(MouseButtonEventHandler mouseUpHandler)
        {
            var template = new DataTemplate();

            var outerStackFactory = new FrameworkElementFactory(typeof(SimpleStackPanel));
            outerStackFactory.SetValue(SimpleStackPanel.OrientationProperty, Orientation.Vertical);
            outerStackFactory.AddHandler(Mouse.MouseUpEvent, mouseUpHandler);

            var itemBorderFactory = new FrameworkElementFactory(typeof(Border));
            itemBorderFactory.SetValue(Border.MarginProperty, new Thickness(0, 4, 0, 0));
            itemBorderFactory.SetValue(Border.WidthProperty, 160.0);
            itemBorderFactory.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding { Source = this, Path = new PropertyPath("BoardFloatBarBorderBrush") });
            itemBorderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));

            var gridFactory = new FrameworkElementFactory(typeof(Grid));

            var viewboxFactory = new FrameworkElementFactory(typeof(Viewbox));
            viewboxFactory.SetValue(Viewbox.WidthProperty, 160.0);
            viewboxFactory.SetValue(Viewbox.HeightProperty, 120.0);
            viewboxFactory.SetValue(Viewbox.StretchProperty, Stretch.Uniform);

            var inkCanvasFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.InkCanvas));
            inkCanvasFactory.SetValue(System.Windows.Controls.InkCanvas.EditingModeProperty, InkCanvasEditingMode.None);
            inkCanvasFactory.SetBinding(System.Windows.Controls.InkCanvas.BackgroundProperty,
                new System.Windows.Data.Binding("Background") { Source = GridBackgroundCover, Mode = System.Windows.Data.BindingMode.OneWay });
            inkCanvasFactory.SetBinding(System.Windows.Controls.InkCanvas.StrokesProperty,
                new System.Windows.Data.Binding("Strokes"));
            inkCanvasFactory.SetBinding(FrameworkElement.WidthProperty,
                new System.Windows.Data.Binding("ActualWidth") { Source = inkCanvas, Mode = System.Windows.Data.BindingMode.OneWay });
            inkCanvasFactory.SetBinding(FrameworkElement.HeightProperty,
                new System.Windows.Data.Binding("ActualHeight") { Source = inkCanvas, Mode = System.Windows.Data.BindingMode.OneWay });
            viewboxFactory.AppendChild(inkCanvasFactory);

            var indexBorderFactory = new FrameworkElementFactory(typeof(Border));
            indexBorderFactory.SetValue(Border.MarginProperty, new Thickness(4));
            indexBorderFactory.SetValue(Border.WidthProperty, 36.0);
            indexBorderFactory.SetValue(Border.HeightProperty, 20.0);
            indexBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            indexBorderFactory.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            indexBorderFactory.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Top);
            indexBorderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(204, 9, 9, 11)));

            var indexTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            indexTextFactory.SetValue(TextBlock.FontSizeProperty, 14.0);
            indexTextFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            indexTextFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            indexTextFactory.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Consolas"));
            indexTextFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            indexTextFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Index"));
            indexBorderFactory.AppendChild(indexTextFactory);

            var deleteBtnFactory = new FrameworkElementFactory(typeof(Button));
            deleteBtnFactory.SetValue(Button.WidthProperty, 24.0);
            deleteBtnFactory.SetValue(Button.HeightProperty, 24.0);
            deleteBtnFactory.SetValue(Button.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            deleteBtnFactory.SetValue(Button.VerticalAlignmentProperty, VerticalAlignment.Top);
            deleteBtnFactory.SetValue(Button.MarginProperty, new Thickness(4));
            deleteBtnFactory.SetValue(Button.ToolTipProperty, FloatingBarStrings.Board_DeleteThisPage);
            deleteBtnFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(WhiteBoardPageListItem_DeleteClick));
            deleteBtnFactory.SetValue(Button.BackgroundProperty, new SolidColorBrush(Color.FromArgb(204, 0, 0, 0)));
            deleteBtnFactory.SetValue(Button.ForegroundProperty, Brushes.White);
            deleteBtnFactory.SetValue(Button.BorderThicknessProperty, new Thickness(0));
            deleteBtnFactory.SetValue(Button.PaddingProperty, new Thickness(0));
            deleteBtnFactory.SetValue(Button.CursorProperty, System.Windows.Input.Cursors.Hand);

            var fontIconFactory = new FrameworkElementFactory(typeof(iNKORE.UI.WPF.Modern.Controls.FontIcon));
            fontIconFactory.SetValue(iNKORE.UI.WPF.Modern.Controls.FontIcon.IconProperty,
                SegoeFluentIcons.Delete);
            deleteBtnFactory.AppendChild(fontIconFactory);

            gridFactory.AppendChild(viewboxFactory);
            gridFactory.AppendChild(indexBorderFactory);
            gridFactory.AppendChild(deleteBtnFactory);

            itemBorderFactory.AppendChild(gridFactory);
            outerStackFactory.AppendChild(itemBorderFactory);
            template.VisualTree = outerStackFactory;

            return template;
        }
    }
}
