using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Ink_Canvas.Helpers;
using static Ink_Canvas.Popups.ColorPalette;

namespace Ink_Canvas {
    public partial class MainWindow : PerformanceTransparentWin {
        private void Window_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (BorderFloatingBarExitPPTBtn.Visibility != Visibility.Visible || currentMode != 0) return;
            if (e.Delta >= 120)
                BtnPPTSlidesUp_Click(null, null);
            else if (e.Delta <= -120) BtnPPTSlidesDown_Click(null, null);
        }

        private void Main_Grid_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible || currentMode == 0) {
                if (e.Key == Key.Down || e.Key == Key.PageDown || e.Key == Key.Right || e.Key == Key.N ||
                    e.Key == Key.Space) BtnPPTSlidesDown_Click(null, null);
                if (e.Key == Key.Up || e.Key == Key.PageUp || e.Key == Key.Left || e.Key == Key.P)
                    BtnPPTSlidesUp_Click(null, null);
            };
            if (e.Key == Key.LeftCtrl) {
                Trace.WriteLine("KeyDown");
                isControlKeyDown = true;
                ControlKeyDownEvent?.Invoke(this,e);
            }
            if (e.Key == Key.LeftShift) {
                Trace.WriteLine("KeyDown");
                isShiftKeyDown = true;
                ShiftKeyDownEvent?.Invoke(this,e);
            }
        }

        public bool isControlKeyDown = false;
        public bool isShiftKeyDown = false;

        public event EventHandler<KeyEventArgs> ControlKeyDownEvent;
        public event EventHandler<KeyEventArgs> ShiftKeyDownEvent;
        public event EventHandler<KeyEventArgs> ControlKeyUpEvent;
        public event EventHandler<KeyEventArgs> ShiftKeyUpEvent;

        private void Main_Grid_PreviewKeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.LeftCtrl) {
                isControlKeyDown = false;
                ControlKeyUpEvent?.Invoke(this,e);
            };
            if (e.Key == Key.LeftShift) {
                isShiftKeyDown = false;
                ShiftKeyUpEvent?.Invoke(this,e);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) KeyExit(null, null);
        }

        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void HotKey_Undo(object sender, ExecutedRoutedEventArgs e) {
            try {
                SymbolIconUndo_MouseUp(lastBorderMouseDownObject, null);
            }
            catch { }
        }

        private void HotKey_Redo(object sender, ExecutedRoutedEventArgs e) {
            try {
                SymbolIconRedo_MouseUp(lastBorderMouseDownObject, null);
            }
            catch { }
        }

        private void HotKey_Clear(object sender, ExecutedRoutedEventArgs e) {
            SymbolIconDelete_MouseUp(lastBorderMouseDownObject, null);
        }


        private void KeyExit(object sender, ExecutedRoutedEventArgs e) {
            if (BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible) BtnPPTSlideShowEnd_Click(null, null);
        }

        private void KeyChangeToDrawTool(object sender, ExecutedRoutedEventArgs e) {
            PenIcon_Click(lastBorderMouseDownObject, null);
        }

        private void KeyChangeToQuitDrawTool(object sender, ExecutedRoutedEventArgs e) {
            if (currentMode != 0) ImageBlackboard_MouseUp(lastBorderMouseDownObject, null);
            CursorIcon_Click(lastBorderMouseDownObject, null);
        }

        private void KeyChangeToSelect(object sender, ExecutedRoutedEventArgs e) {
            if (StackPanelCanvasControls.Visibility == Visibility.Visible)
                SymbolIconSelect_MouseUp(lastBorderMouseDownObject, null);
        }

        private void KeyChangeToEraser(object sender, ExecutedRoutedEventArgs e) {
            if (StackPanelCanvasControls.Visibility == Visibility.Visible) {
                if (Eraser_Icon.Background != null)
                    EraserIconByStrokes_Click(lastBorderMouseDownObject, null);
                else
                    EraserIcon_Click(lastBorderMouseDownObject, null);
            }
        }

        private void KeyChangeToBoard(object sender, ExecutedRoutedEventArgs e) {
            ImageBlackboard_MouseUp(lastBorderMouseDownObject, null);
        }

        private void KeyCapture(object sender, ExecutedRoutedEventArgs e) {
            SaveScreenShotToDesktop();
        }

        private void KeyDrawLine(object sender, ExecutedRoutedEventArgs e) {
            if (StackPanelCanvasControls.Visibility == Visibility.Visible) BtnDrawLine_Click(lastMouseDownSender, null);
        }

        private void KeyHide(object sender, ExecutedRoutedEventArgs e) {
            SymbolIconEmoji_MouseUp(null, null);
        }
    }
}