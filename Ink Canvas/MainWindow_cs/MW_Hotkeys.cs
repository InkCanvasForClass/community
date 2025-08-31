using System.Windows;
using System.Windows.Input;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (StackPanelPPTControls.Visibility != Visibility.Visible || currentMode != 0) return;

            // 直接发送翻页请求到PPT放映软件，不通过软件处理
            if (e.Delta >= 120)
            {
                // 上一页 - 发送PageUp键到PPT放映窗口
                SendKeyToPPTSlideShow(true);
            }
            else if (e.Delta <= -120)
            {
                // 下一页 - 发送PageDown键到PPT放映窗口
                SendKeyToPPTSlideShow(false);
            }
        }

        private void Main_Grid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (StackPanelPPTControls.Visibility != Visibility.Visible || currentMode != 0) return;

            // 直接发送翻页请求到PPT放映软件，不通过软件处理
            if (e.Key == Key.Down || e.Key == Key.PageDown || e.Key == Key.Right || e.Key == Key.N ||
                e.Key == Key.Space)
            {
                e.Handled = true; // 阻止事件继续传播
                SendKeyToPPTSlideShow(false); // 下一页
            }
            else if (e.Key == Key.Up || e.Key == Key.PageUp || e.Key == Key.Left || e.Key == Key.P)
            {
                e.Handled = true; // 阻止事件继续传播
                SendKeyToPPTSlideShow(true); // 上一页
            }
        }

        // 保留PPT翻页快捷键处理
        // 以下方法保留供全局快捷键调用

        private void HotKey_Undo(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                SymbolIconUndo_MouseUp(lastBorderMouseDownObject, null);
            }
            catch { }
        }

        private void HotKey_Redo(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                SymbolIconRedo_MouseUp(lastBorderMouseDownObject, null);
            }
            catch { }
        }

        private void HotKey_Clear(object sender, ExecutedRoutedEventArgs e)
        {
            SymbolIconDelete_MouseUp(lastBorderMouseDownObject, null);
        }


        internal void KeyExit(object sender, ExecutedRoutedEventArgs e)
        {
            if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible) BtnPPTSlideShowEnd_Click(BtnPPTSlideShowEnd, null);
        }

        private void KeyChangeToDrawTool(object sender, ExecutedRoutedEventArgs e)
        {
            PenIcon_Click(lastBorderMouseDownObject, null);
        }

        internal void KeyChangeToQuitDrawTool(object sender, ExecutedRoutedEventArgs e)
        {
            if (currentMode != 0)
            {
                // 在白板模式下，alt+q 退出白板模式
                ImageBlackboard_MouseUp(lastBorderMouseDownObject, null);
            }
            else
            {
                // 在非白板模式下，alt+q 切换到鼠标模式
                CursorIcon_Click(lastBorderMouseDownObject, null);
            }
        }

        private void KeyChangeToSelect(object sender, ExecutedRoutedEventArgs e)
        {
            if (StackPanelCanvasControls.Visibility == Visibility.Visible)
                SymbolIconSelect_MouseUp(lastBorderMouseDownObject, null);
        }

        private void KeyChangeToEraser(object sender, ExecutedRoutedEventArgs e)
        {
            if (StackPanelCanvasControls.Visibility == Visibility.Visible)
            {
                if (Eraser_Icon.Background != null)
                    EraserIconByStrokes_Click(lastBorderMouseDownObject, null);
                else
                    EraserIcon_Click(lastBorderMouseDownObject, null);
            }
        }

        private void KeyChangeToBoard(object sender, ExecutedRoutedEventArgs e)
        {
            ImageBlackboard_MouseUp(lastBorderMouseDownObject, null);
        }

        private void KeyCapture(object sender, ExecutedRoutedEventArgs e)
        {
            SaveScreenShotToDesktop();
        }

        private void KeyDrawLine(object sender, ExecutedRoutedEventArgs e)
        {
            if (StackPanelCanvasControls.Visibility == Visibility.Visible) BtnDrawLine_Click(lastMouseDownSender, null);
        }

        private void KeyHide(object sender, ExecutedRoutedEventArgs e)
        {
            SymbolIconEmoji_MouseUp(null, null);
        }
    }
}