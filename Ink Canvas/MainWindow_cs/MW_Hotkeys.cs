using System.Windows;
using System.Windows.Input;

namespace Ink_Canvas {
    public partial class MainWindow : Window {
        private void Window_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (StackPanelPPTControls.Visibility != Visibility.Visible || currentMode != 0) return;
            if (e.Delta >= 120)
                BtnPPTSlidesUp_Click(BtnPPTSlidesUp, null);
            else if (e.Delta <= -120) BtnPPTSlidesDown_Click(BtnPPTSlidesDown, null);
        }

        private void Main_Grid_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (StackPanelPPTControls.Visibility != Visibility.Visible || currentMode != 0) return;

            if (e.Key == Key.Down || e.Key == Key.PageDown || e.Key == Key.Right || e.Key == Key.N ||
                e.Key == Key.Space) BtnPPTSlidesDown_Click(BtnPPTSlidesDown, null);
            if (e.Key == Key.Up || e.Key == Key.PageUp || e.Key == Key.Left || e.Key == Key.P)
                BtnPPTSlidesUp_Click(BtnPPTSlidesUp, null);
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
            if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible) BtnPPTSlideShowEnd_Click(BtnPPTSlideShowEnd, null);
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