using System;
using System.Windows;
using System.Windows.Input;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 鼠标滚轮事件处理，用于PPT翻页
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标滚轮事件参数</param>
        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (BtnPPTSlideShowEnd.Visibility != Visibility.Visible || currentMode != 0) return;
            if (e.Delta >= 120)
            {
                BtnPPTSlidesUp_Click(null, null);
            }
            else if (e.Delta <= -120)
            {
                BtnPPTSlidesDown_Click(null, null);
            }
        }

        /// <summary>
        /// 键盘按键预览事件处理，用于PPT翻页
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">键盘事件参数</param>
        private void Main_Grid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (BtnPPTSlideShowEnd.Visibility != Visibility.Visible || currentMode != 0) return;

            if (e.Key == Key.Down || e.Key == Key.PageDown || e.Key == Key.Right || e.Key == Key.N || e.Key == Key.Space)
            {
                BtnPPTSlidesDown_Click(null, null);
            }
            if (e.Key == Key.Up || e.Key == Key.PageUp || e.Key == Key.Left || e.Key == Key.P)
            {
                BtnPPTSlidesUp_Click(null, null);
            }
        }


        /// <summary>
        /// 撤销操作热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        private void HotKey_Undo(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                SymbolIconUndo_MouseUp(lastBorderMouseDownObject, null);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        /// <summary>
        /// 重做操作热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        private void HotKey_Redo(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                SymbolIconRedo_MouseUp(lastBorderMouseDownObject, null);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        /// <summary>
        /// 清空画布热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        private void HotKey_Clear(object sender, ExecutedRoutedEventArgs e)
        {
            SymbolIconDelete_MouseUp(lastBorderMouseDownObject, null);
        }


        /// <summary>
        /// 退出PPT放映热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        internal void KeyExit(object sender, ExecutedRoutedEventArgs e)
        {
            if (currentMode != 0)
            {
                ImageBlackboard_MouseUp(lastBorderMouseDownObject, null);
                return;
            }

            if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible) BtnPPTSlideShowEnd_Click(BtnPPTSlideShowEnd, null);
        }

        /// <summary>
        /// 切换到绘图工具热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        private void KeyChangeToDrawTool(object sender, ExecutedRoutedEventArgs e)
        {
            PenIcon_Click(lastBorderMouseDownObject, null);
        }

        /// <summary>
        /// 退出绘图工具热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        /// <remarks>
        /// 在白板模式下，alt+q 退出白板模式
        /// 在非白板模式下，alt+q 切换到鼠标模式
        /// </remarks>
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

        /// <summary>
        /// 切换到选择工具热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        /// <remarks>仅当画布控件面板可见时生效</remarks>
        private void KeyChangeToSelect(object sender, ExecutedRoutedEventArgs e)
        {
            if (StackPanelCanvasControls.Visibility == Visibility.Visible)
                SymbolIconSelect_MouseUp(lastBorderMouseDownObject, null);
        }

        /// <summary>
        /// 切换到橡皮擦工具热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        /// <remarks>仅当画布控件面板可见时生效，根据当前橡皮擦状态选择相应的橡皮擦模式</remarks>
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

        /// <summary>
        /// 切换到白板模式热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        private void KeyChangeToBoard(object sender, ExecutedRoutedEventArgs e)
        {
            ImageBlackboard_MouseUp(lastBorderMouseDownObject, null);
        }

        /// <summary>
        /// 屏幕截图热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        private void KeyCapture(object sender, ExecutedRoutedEventArgs e)
        {
            SaveScreenShotToDesktop();
        }

        /// <summary>
        /// 绘制直线热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        /// <remarks>仅当画布控件面板可见时生效</remarks>
        private void KeyDrawLine(object sender, ExecutedRoutedEventArgs e)
        {
            if (StackPanelCanvasControls.Visibility == Visibility.Visible) BtnDrawLine_Click(lastMouseDownSender, null);
        }

        /// <summary>
        /// 隐藏工具栏热键处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">执行路由事件参数</param>
        private void KeyHide(object sender, ExecutedRoutedEventArgs e)
        {
            SymbolIconEmoji_MouseUp(null, null);
        }
    }
}
