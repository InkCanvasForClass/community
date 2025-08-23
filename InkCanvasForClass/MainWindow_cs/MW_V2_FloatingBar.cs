using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;

namespace Ink_Canvas {
    public partial class MainWindow : PerformanceTransparentWin {

        public FloatingToolBarV2 FloatingToolBarV2;

        private void InitFloatingToolbarV2() {
            FloatingToolBarV2 = new FloatingToolBarV2();
            FloatingToolBarV2.Topmost = false;
            FloatingToolBarV2.Show();
            FloatingToolBarV2.Owner = this;

            FloatingToolBarV2.FloatingBarToolSelectionChanged += FloatingToolBarV2_ToolSelectionChanged;
            FloatingToolBarV2.FloatingBarToolButtonClicked += FloatingToolBarV2_ToolButtonClicked;
        }

        #region 工具切换

        private void SwitchToCursorMode() {
            // 结束未完成的形状绘制
            if (ShapeDrawingV2Layer.IsInShapeDrawingMode) ShapeDrawingV2Layer.EndShapeDrawing();

            // 切换前自动截图保存墨迹
            if (inkCanvas.Strokes.Count > 0 &&
                inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber) {
                if (BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible) SavePPTScreenshot($"{pptName}/{previousSlideID}_{DateTime.Now:HH-mm-ss}");
                else SaveScreenshot(true);
            }


            inkCanvas.Visibility = Settings.Canvas.HideStrokeWhenSelecting ? Visibility.Collapsed : Visibility.Visible;
            inkCanvas.IsHitTestVisible = false;
            SetTransparentHitThrough();

            GridBackgroundCoverHolder.Visibility = Visibility.Collapsed;
            inkCanvas.Select(new StrokeCollection());
            GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

            RectangleSelectionHitTestBorder.Visibility = Visibility.Collapsed;

            if (currentMode != 0) {
                SaveStrokes();
                RestoreStrokes(true);
            }
        }

        private void SwitchToPenMode() {
            // 结束未完成的形状绘制
            if (ShapeDrawingV2Layer.IsInShapeDrawingMode) ShapeDrawingV2Layer.EndShapeDrawing();

            inkCanvas.EditingMode = InkCanvasEditingMode.Ink;

            SetTransparentNotHitThrough();
            inkCanvas.IsHitTestVisible = true;
            inkCanvas.Visibility = Visibility.Visible;

            GridBackgroundCoverHolder.Visibility = Visibility.Visible;
            GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

            ColorSwitchCheck();
        }

        private void ClearInkCanvasStrokes(bool isClearTimeMachineHistory, bool isErasedByCode) {
            if (inkCanvas.GetSelectedStrokes().Count > 0) {
                inkCanvas.Strokes.Remove(inkCanvas.GetSelectedStrokes());
                // cancel
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
                inkCanvas.Opacity = 1;
                InkSelectionStrokesOverlay.Visibility = Visibility.Collapsed;
                InkSelectionStrokesBackgroundInkCanvas.Visibility = Visibility.Collapsed;
                InkSelectionStrokesOverlay.DrawStrokes(new StrokeCollection(), new Matrix());
                UpdateStrokeSelectionBorder(false, null);
                RectangleSelectionHitTestBorder.Visibility = Visibility.Visible;
            } else if (inkCanvas.Strokes.Count > 0) {
                if (Settings.Automation.IsAutoSaveStrokesAtClear &&
                    inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber) {
                    if (BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible)
                        SavePPTScreenshot($"{pptName}/{previousSlideID}_{DateTime.Now:HH-mm-ss}");
                    else
                        SaveScreenshot(true);
                }

                forceEraser = false;

                if (currentMode == 0) {
                    // 先回到画笔再清屏，避免 TimeMachine 的相关 bug 影响
                    if (Pen_Icon.Background == null && StackPanelCanvasControls.Visibility == Visibility.Visible) SwitchToPenMode();
                } else if (Pen_Icon.Background == null) SwitchToPenMode();

                if (inkCanvas.Strokes.Count != 0) {
                    var whiteboardIndex = CurrentWhiteboardIndex;
                    if (currentMode == 0) whiteboardIndex = 0;
                    strokeCollections[whiteboardIndex] = inkCanvas.Strokes.Clone();
                }

                ClearStrokes(false);
                inkCanvas.Children.Clear();

                CancelSingleFingerDragMode();

                if (isClearTimeMachineHistory) {
                    inkCanvas.Strokes.Clear();
                    timeMachine.ClearStrokeHistory();
                } else {
                    _currentCommitType = CommitReason.ClearingCanvas;
                    if (isErasedByCode) _currentCommitType = CommitReason.CodeInput;
                    inkCanvas.Strokes.Clear();
                    _currentCommitType = CommitReason.UserInput;
                }
            }
        }

        #endregion

        private void FloatingToolBarV2_ToolSelectionChanged(object sender, EventArgs e) {
            var item = (FloatingBarItem)sender;
            if (item.ToolType == ICCToolsEnum.CursorMode) SwitchToCursorMode();
            if (item.ToolType == ICCToolsEnum.PenMode) SwitchToPenMode();
        }

        private void FloatingToolBarV2_ToolButtonClicked(object sender, EventArgs e) {
            var item = (FloatingBarItem)sender;
            if (item.Name == "Clear") ClearInkCanvasStrokes(Settings.Canvas.ClearCanvasAndClearTimeMachine,false);
        }
    }
}
