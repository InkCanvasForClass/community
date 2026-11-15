using Ink_Canvas.Plugins;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private PresentationSourceDescriptor _selectionPresentation;
        private bool _presentationElementSelection;
        private bool _selectionCanvasHitTest;
        private Visibility _selectionCanvasVisibility;
        private Brush _selectionFakeBackground;
        private double _selectionFakeOpacity;
        private Visibility _selectionCoverVisibility;

        internal void AttachPresentationSelection(PresentationSourceDescriptor source)
        {
            DetachPresentationSelection();
            if (!source.SupportsElementSelection) return;
            _selectionPresentation = source;
            if (_currentToolMode == "select") TryEnterPresentationElementSelection();
        }

        // 复用现有套索工具的逻辑模式和高亮，但将输入交给课件，不启用墨迹套索。
        private bool TryEnterPresentationElementSelection()
        {
            if (_selectionPresentation == null || IsWhiteboardMode) return false;
            if (_presentationElementSelection) return true;
            DisableEraserOverlay();
            CancelBoardRoamingInteraction();
            drawingShapeMode = 0;
            forceEraser = false;
            forcePointEraser = false;
            if (!SetCurrentToolMode(InkCanvasEditingMode.None)) return true;
            _selectionCanvasHitTest = inkCanvas.IsHitTestVisible;
            _selectionCanvasVisibility = inkCanvas.Visibility;
            _selectionFakeBackground = GridTransparencyFakeBackground.Background;
            _selectionFakeOpacity = GridTransparencyFakeBackground.Opacity;
            _selectionCoverVisibility = GridBackgroundCoverHolder.Visibility;
            _presentationElementSelection = true;
            UpdateCurrentToolMode("select");
            AbortAllActiveTouchInputs();
            inkCanvas.Select(new System.Windows.Ink.StrokeCollection());
            inkCanvas.ReleaseMouseCapture();
            inkCanvas.ReleaseAllTouchCaptures();
            inkCanvas.IsHitTestVisible = false;
            inkCanvas.Visibility = Visibility.Visible;
            GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
            GridTransparencyFakeBackground.Opacity = 0;
            GridTransparencyFakeBackground.Background = Brushes.Transparent;
            GridBackgroundCoverHolder.Visibility = Visibility.Collapsed;
            SetTransparentHitThrough();
            SetFloatingBarHighlightPosition("select");
            HideSubPanels("select");
            NotifyPresentationElementSelection(true);
            return true;
        }

        // 在所有逻辑工具切换入口统一退出，避免橡皮/图形/自动化遗漏恢复。
        private void LeavePresentationElementSelection()
        {
            if (!_presentationElementSelection) return;
            _presentationElementSelection = false;
            SetTransparentNotHitThrough();
            inkCanvas.IsHitTestVisible = _selectionCanvasHitTest;
            inkCanvas.Visibility = _selectionCanvasVisibility;
            GridTransparencyFakeBackground.Background = _selectionFakeBackground;
            GridTransparencyFakeBackground.Opacity = _selectionFakeOpacity;
            GridBackgroundCoverHolder.Visibility = _selectionCoverVisibility;
            NotifyPresentationElementSelection(false);
        }

        private void NotifyPresentationElementSelection(bool active)
        {
            try { _selectionPresentation?.ElementSelectionChanged?.Invoke(active); }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"课件选择模式同步失败: {ex}", Helpers.LogHelper.LogType.Error);
                if (active)
                {
                    LeavePresentationElementSelection();
                    UpdateCurrentToolMode("cursor");
                    HideSubPanels("cursor");
                    ApplyCursorInputRouting();
                }
            }
        }

        internal void DetachPresentationSelection()
        {
            var wasSelecting = _presentationElementSelection;
            LeavePresentationElementSelection();
            _selectionPresentation = null;
            if (wasSelecting)
            {
                UpdateCurrentToolMode("cursor");
                SetFloatingBarHighlightPosition("cursor");
                SetCurrentToolMode(InkCanvasEditingMode.None);
            }
        }
    }
}
