using Ink_Canvas.Controls;
using Ink_Canvas.Controls.Toolbar.BoardToolbar;
using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

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
            _pageManager?.UpdatePageInfoDisplay(TextBlockWhiteBoardIndexInfo);
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

                UpdateBoardToolbarState();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"MW_BoardToolbarHost: InitializeBoardToolbar 异常: {ex.Message}", LogHelper.LogType.Error);
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
    }
}
