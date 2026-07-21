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
using System.Windows.Input;
using System.Windows.Media;
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
            if (id == "board.pen")
                UpdateBoardPenIconColor();
        }

        public FrameworkElement FindView(string id)
        {
            return _boardToolbarViews.TryGetValue(id, out var view) ? view : null;
        }

        /// <summary>
        /// 更新白板工具栏画笔图标颜色，使其反映当前画笔颜色。
        /// </summary>
        internal void UpdateBoardPenIconColor()
        {
            if (FindView("board.pen") is not BoardToolbarButton penButton) return;

            var brush = Settings.Appearance.ShowPenColorOnBoardToolbarIcon
                ? new SolidColorBrush(inkCanvas.DefaultDrawingAttributes.Color)
                : Application.Current.TryFindResource("FloatingBarForegroundBrush") as Brush;
            penButton.IconBrush = brush;

            // 工具栏在模式切换时可能会异步重建，确保重建后的视图也应用颜色。
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (FindView("board.pen") is BoardToolbarButton currentPenButton)
                    currentPenButton.IconBrush = Settings.Appearance.ShowPenColorOnBoardToolbarIcon
                        ? new SolidColorBrush(inkCanvas.DefaultDrawingAttributes.Color)
                        : Application.Current.TryFindResource("FloatingBarForegroundBrush") as Brush;
            }), System.Windows.Threading.DispatcherPriority.Loaded);
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

        public void SelectRoaming()
        {
            ActivateBoardRoamingMode();
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
            var newText = CurrentPageInfo;
            foreach (var key in new[] { "board.pageInfo.left", "board.pageInfo.right", "board.pageInfo.center" })
            {
                var view = FindView(key);
                if (view == null) continue;

                TextBlock tb = view as TextBlock;
                if (tb == null)
                {
                    // 如果不是 TextBlock（比如被覆盖成了 Border），查找内部的 TextBlock
                    tb = FindTextBlockInVisualTree(view);
                }

                if (tb != null)
                {
                    tb.Text = newText;
                }
            }
        }

        private static TextBlock FindTextBlockInVisualTree(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is TextBlock tb)
                {
                    return tb;
                }
                var found = FindTextBlockInVisualTree(child);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
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
                UpdateBoardRoamingButtonState();
                CheckEnableTwoFingerGestureBtnColorPrompt();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"MW_BoardToolbarHost: InitializeBoardToolbar 异常: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        internal void RebuildBoardToolbar()
        {
            try
            {
                var host = (IBoardToolbarHost)this;
                BlackboardLeftSidePanel.Children.Clear();
                BlackboardCenterSidePanel.Children.Clear();
                BlackboardRightSidePanel.Children.Clear();
                BoardToolbarRegistry.RebuildToolbar(host, BlackboardLeftSidePanel, BlackboardCenterSidePanel, BlackboardRightSidePanel);
                BindPopupPlacementTargets();
                BindPageInfoClickHandler();
                UpdateBoardToolbarState();
                UpdateBoardRoamingButtonState();
                CheckEnableTwoFingerGestureBtnColorPrompt();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"MW_BoardToolbarHost: RebuildBoardToolbar 异常: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void BindPopupPlacementTargets()
        {
            SetPopupPlacementTarget(BoardTwoFingerGestureBorder, "board.gesture");
            SetPopupPlacementTarget(BoardRoamingPopup, "board.roaming");
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
            // 视频展台特殊模式：按钮状态由 UpdateBoothPagingButtonsState 管理，
            // 跳过白板分页的按钮逻辑（否则 CanAddNewPage 会错误启用"下一页"）
            if (_isVideoPresenterSpecialMode)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdatePageInfo();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }

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

            AttachPagePreviewTouchHandlers();
        }

        private void AttachPagePreviewTouchHandlers()
        {
            const double TouchTapMovementThreshold = 15.0;

            var leftScrollViewer = FindView("board.pageList.leftScrollViewer") as ScrollViewer;
            var rightScrollViewer = FindView("board.pageList.rightScrollViewer") as ScrollViewer;
            var leftPageListView = FindView("board.pageList.left") as System.Windows.Controls.ListView;
            var rightPageListView = FindView("board.pageList.right") as System.Windows.Controls.ListView;

            if (leftScrollViewer != null && leftPageListView != null)
            {
                double leftTouchStartY = 0;
                double leftTouchStartX = 0;
                double leftScrollStartOffset = 0;
                bool leftIsTouching = false;
                bool leftTouchDidScroll = false;

                leftScrollViewer.TouchDown += (s, e) =>
                {
                    leftIsTouching = true;
                    leftTouchDidScroll = false;
                    var pt = e.GetTouchPoint(leftScrollViewer).Position;
                    leftTouchStartX = pt.X;
                    leftTouchStartY = pt.Y;
                    leftScrollStartOffset = leftScrollViewer.VerticalOffset;
                    leftScrollViewer.CaptureTouch(e.TouchDevice);
                    e.Handled = true;
                };
                leftScrollViewer.TouchMove += (s, e) =>
                {
                    if (leftIsTouching)
                    {
                        var pt = e.GetTouchPoint(leftScrollViewer).Position;
                        double deltaY = leftTouchStartY - pt.Y;
                        double deltaX = pt.X - leftTouchStartX;
                        if (!leftTouchDidScroll && (Math.Abs(deltaY) > TouchTapMovementThreshold || Math.Abs(deltaX) > TouchTapMovementThreshold))
                            leftTouchDidScroll = true;
                        if (leftTouchDidScroll)
                            leftScrollViewer.ScrollToVerticalOffset(leftScrollStartOffset + deltaY);
                        e.Handled = true;
                    }
                };
                leftScrollViewer.TouchUp += (s, e) =>
                {
                    if (leftIsTouching && !leftTouchDidScroll)
                    {
                        var pt = e.GetTouchPoint(leftScrollViewer).Position;
                        double dx = pt.X - leftTouchStartX, dy = pt.Y - leftTouchStartY;
                        if (dx * dx + dy * dy <= TouchTapMovementThreshold * TouchTapMovementThreshold)
                            TrySwitchWhiteboardPageByTouchPoint(leftPageListView, leftScrollViewer, pt);
                    }
                    leftIsTouching = false;
                    leftTouchDidScroll = false;
                    leftScrollViewer.ReleaseTouchCapture(e.TouchDevice);
                    e.Handled = true;
                };
            }

            if (rightScrollViewer != null && rightPageListView != null)
            {
                double rightTouchStartY = 0;
                double rightTouchStartX = 0;
                double rightScrollStartOffset = 0;
                bool rightIsTouching = false;
                bool rightTouchDidScroll = false;

                rightScrollViewer.TouchDown += (s, e) =>
                {
                    rightIsTouching = true;
                    rightTouchDidScroll = false;
                    var pt = e.GetTouchPoint(rightScrollViewer).Position;
                    rightTouchStartX = pt.X;
                    rightTouchStartY = pt.Y;
                    rightScrollStartOffset = rightScrollViewer.VerticalOffset;
                    rightScrollViewer.CaptureTouch(e.TouchDevice);
                    e.Handled = true;
                };
                rightScrollViewer.TouchMove += (s, e) =>
                {
                    if (rightIsTouching)
                    {
                        var pt = e.GetTouchPoint(rightScrollViewer).Position;
                        double deltaY = rightTouchStartY - pt.Y;
                        double deltaX = pt.X - rightTouchStartX;
                        if (!rightTouchDidScroll && (Math.Abs(deltaY) > TouchTapMovementThreshold || Math.Abs(deltaX) > TouchTapMovementThreshold))
                            rightTouchDidScroll = true;
                        if (rightTouchDidScroll)
                            rightScrollViewer.ScrollToVerticalOffset(rightScrollStartOffset + deltaY);
                        e.Handled = true;
                    }
                };
                rightScrollViewer.TouchUp += (s, e) =>
                {
                    if (rightIsTouching && !rightTouchDidScroll)
                    {
                        var pt = e.GetTouchPoint(rightScrollViewer).Position;
                        double dx = pt.X - rightTouchStartX, dy = pt.Y - rightTouchStartY;
                        if (dx * dx + dy * dy <= TouchTapMovementThreshold * TouchTapMovementThreshold)
                            TrySwitchWhiteboardPageByTouchPoint(rightPageListView, rightScrollViewer, pt);
                    }
                    rightIsTouching = false;
                    rightTouchDidScroll = false;
                    rightScrollViewer.ReleaseTouchCapture(e.TouchDevice);
                    e.Handled = true;
                };
            }
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
                Background = (Brush)Application.Current.TryFindResource("FloatingBarBackgroundBrush")
                    ?? (Brush)Application.Current.TryFindResource("FloatBarBackground"),
                Opacity = 1,
                BorderBrush = (Brush)Application.Current.TryFindResource("FloatingBarBorderBrush"),
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
            itemBorderFactory.SetResourceReference(Border.BackgroundProperty, "FloatingBarBackgroundBrush");
            itemBorderFactory.SetResourceReference(Border.BorderBrushProperty, "FloatingBarBorderBrush");
            itemBorderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));

            var gridFactory = new FrameworkElementFactory(typeof(Grid));

            var viewboxFactory = new FrameworkElementFactory(typeof(Viewbox));
            viewboxFactory.SetValue(Viewbox.WidthProperty, 160.0);
            viewboxFactory.SetValue(Viewbox.HeightProperty, 120.0);
            viewboxFactory.SetValue(Viewbox.StretchProperty, Stretch.Uniform);

            // Viewbox 是 Decorator，只能有一个子级。用 Grid 作为容器，叠加 InkCanvas/Image/TextBlock
            // 三个元素，通过 Visibility 绑定互斥显示：
            //   - 普通白板页：InkCanvas 可见
            //   - 视频展台照片项：Image 可见（显示照片缩略图）
            //   - 视频展台文字项：TextBlock 可见（居中显示"再次点击返回直播画面"）
            var viewboxContentFactory = new FrameworkElementFactory(typeof(Grid));
            // 给 Grid 设深色背景：视频展台文字项（白字）需要深色底才能看见，
            // 普通白板页的 InkCanvas 会覆盖此背景，照片项的 Image 也会覆盖
            viewboxContentFactory.SetValue(Grid.BackgroundProperty,
                new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)));

            // 共享的 BooleanToVisibilityConverter：用于根据 ShowInk/ShowImage/ShowText 控制 InkCanvas/Image/TextBlock 可见性
            // 使用 WPF 内置的 System.Windows.Controls.BooleanToVisibilityConverter（true=>Visible, false=>Collapsed）
            var boolToVis = new System.Windows.Controls.BooleanToVisibilityConverter();

            // 1) InkCanvas：普通白板页可见（ShowInk=true），视频展台项隐藏
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
            // ShowInk=true => Visible；视频展台项 ShowInk=false => Collapsed
            inkCanvasFactory.SetBinding(UIElement.VisibilityProperty,
                new System.Windows.Data.Binding("ShowInk") { Converter = boolToVis });
            viewboxContentFactory.AppendChild(inkCanvasFactory);

            // 2) Image：仅视频展台照片项可见（ShowImage=true）
            var boothImageFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Image));
            boothImageFactory.SetValue(System.Windows.Controls.Image.StretchProperty, Stretch.Uniform);
            boothImageFactory.SetBinding(System.Windows.Controls.Image.SourceProperty,
                new System.Windows.Data.Binding("BoothImage"));
            boothImageFactory.SetBinding(UIElement.VisibilityProperty,
                new System.Windows.Data.Binding("ShowImage") { Converter = boolToVis });
            viewboxContentFactory.AppendChild(boothImageFactory);

            // 3) TextBlock：仅视频展台文字项可见（ShowText=true），居中显示提示文字（如"再次点击返回直播画面"）
            var boothTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            boothTextFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            boothTextFactory.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            boothTextFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            boothTextFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            boothTextFactory.SetValue(TextBlock.FontSizeProperty, 13.0);
            boothTextFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            boothTextFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            boothTextFactory.SetValue(TextBlock.PaddingProperty, new Thickness(6));
            boothTextFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("BoothText"));
            boothTextFactory.SetBinding(UIElement.VisibilityProperty,
                new System.Windows.Data.Binding("ShowText") { Converter = boolToVis });
            viewboxContentFactory.AppendChild(boothTextFactory);

            viewboxFactory.AppendChild(viewboxContentFactory);

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
            // 绑定 Visibility 到 ShowDeleteButton（直播页=false→Collapsed，照片项/普通白板页=true→Visible）
            deleteBtnFactory.SetBinding(UIElement.VisibilityProperty,
                new System.Windows.Data.Binding("ShowDeleteButton") { Converter = boolToVis });

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
