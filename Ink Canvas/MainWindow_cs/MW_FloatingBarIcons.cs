using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Image = System.Windows.Controls.Image;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Panel = System.Windows.Controls.Panel;
using Point = System.Windows.Point;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {

        #region "手勢"按鈕

        /// <summary>
        /// 用於浮動工具欄的"手勢"按鈕和白板工具欄的"手勢"按鈕的點擊事件
        /// </summary>
        private void TwoFingerGestureBorder_MouseUp(object sender, RoutedEventArgs e)
        {
            if (TwoFingerGestureBorder.Visibility == Visibility.Visible)
            {
                AnimationsHelper.HideWithSlideAndFade(EraserSizePanel);
                AnimationsHelper.HideWithSlideAndFade(BorderTools);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
                AnimationsHelper.HideWithSlideAndFade(PenPalette);
                AnimationsHelper.HideWithSlideAndFade(BoardPenPalette);
                AnimationsHelper.HideWithSlideAndFade(BorderDrawShape);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderDrawShape);
                AnimationsHelper.HideWithSlideAndFade(BoardEraserSizePanel);
                AnimationsHelper.HideWithSlideAndFade(BorderTools);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
                AnimationsHelper.HideWithSlideAndFade(TwoFingerGestureBorder);
                AnimationsHelper.HideWithSlideAndFade(BoardTwoFingerGestureBorder);
            }
            else
            {
                AnimationsHelper.HideWithSlideAndFade(EraserSizePanel);
                AnimationsHelper.HideWithSlideAndFade(BorderTools);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
                AnimationsHelper.HideWithSlideAndFade(PenPalette);
                AnimationsHelper.HideWithSlideAndFade(BoardPenPalette);
                AnimationsHelper.HideWithSlideAndFade(BorderDrawShape);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderDrawShape);
                AnimationsHelper.HideWithSlideAndFade(BoardEraserSizePanel);
                AnimationsHelper.HideWithSlideAndFade(BorderTools);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
                AnimationsHelper.ShowWithSlideFromBottomAndFade(TwoFingerGestureBorder);
                AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardTwoFingerGestureBorder);
            }
        }

        /// <summary>
        /// 用於更新浮動工具欄的"手勢"按鈕和白板工具欄的"手勢"按鈕的樣式（開啟和關閉狀態）
        /// </summary>
        private void CheckEnableTwoFingerGestureBtnColorPrompt()
        {
            if (ToggleSwitchEnableMultiTouchMode.IsOn)
            {
                TwoFingerGestureSimpleStackPanel.Opacity = 0.5;
                TwoFingerGestureSimpleStackPanel.IsHitTestVisible = false;
                EnableTwoFingerGestureBtn.Source =
                    new BitmapImage(new Uri("/Resources/new-icons/gesture.png", UriKind.Relative));

                BoardGesture.Background = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                BoardGestureGeometry.Brush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                BoardGestureGeometry2.Brush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                BoardGestureLabel.Foreground = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                BoardGesture.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                BoardGestureGeometry.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.DisabledGestureIcon);
                BoardGestureGeometry2.Geometry = Geometry.Parse("F0 M24,24z M0,0z");
            }
            else
            {
                TwoFingerGestureSimpleStackPanel.Opacity = 1;
                TwoFingerGestureSimpleStackPanel.IsHitTestVisible = true;
                if (Settings.Gesture.IsEnableTwoFingerGesture)
                {
                    EnableTwoFingerGestureBtn.Source =
                        new BitmapImage(new Uri("/Resources/new-icons/gesture-enabled.png", UriKind.Relative));

                    BoardGesture.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                    BoardGestureGeometry.Brush = new SolidColorBrush(Colors.GhostWhite);
                    BoardGestureGeometry2.Brush = new SolidColorBrush(Colors.GhostWhite);
                    BoardGestureLabel.Foreground = new SolidColorBrush(Colors.GhostWhite);
                    BoardGesture.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                    BoardGestureGeometry.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.EnabledGestureIcon);
                    BoardGestureGeometry2.Geometry = Geometry.Parse("F0 M24,24z M0,0z " + XamlGraphicsIconGeometries.EnabledGestureIconBadgeCheck);
                }
                else
                {
                    EnableTwoFingerGestureBtn.Source =
                        new BitmapImage(new Uri("/Resources/new-icons/gesture.png", UriKind.Relative));

                    BoardGesture.Background = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                    BoardGestureGeometry.Brush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    BoardGestureGeometry2.Brush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    BoardGestureLabel.Foreground = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    BoardGesture.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                    BoardGestureGeometry.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.DisabledGestureIcon);
                    BoardGestureGeometry2.Geometry = Geometry.Parse("F0 M24,24z M0,0z");
                }
            }
        }

        /// <summary>
        /// 控制是否顯示浮動工具欄的"手勢"按鈕
        /// </summary>
        private void CheckEnableTwoFingerGestureBtnVisibility(bool isVisible)
        {
            // 在PPT模式下始终隐藏手势按钮
            if (currentMode == 0 || BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
            {
                EnableTwoFingerGestureBorder.Visibility = Visibility.Collapsed;
                return;
            }

            if (StackPanelCanvasControls.Visibility != Visibility.Visible
                || BorderFloatingBarMainControls.Visibility != Visibility.Visible)
            {
                EnableTwoFingerGestureBorder.Visibility = Visibility.Collapsed;
            }
            else if (isVisible)
            {
                EnableTwoFingerGestureBorder.Visibility = Visibility.Visible;
            }
            else
            {
                EnableTwoFingerGestureBorder.Visibility = Visibility.Collapsed;
            }
        }

        #endregion "手勢"按鈕

        #region 浮動工具欄的拖動實現

        private bool isDragDropInEffect;
        private Point pos;
        private Point downPos;
        private Point pointDesktop = new Point(-1, -1); //用于记录上次在桌面时的坐标
        private Point pointPPT = new Point(-1, -1); //用于记录上次在PPT中的坐标

        private void SymbolIconEmoji_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragDropInEffect)
            {
                var xPos = e.GetPosition(null).X - pos.X + ViewboxFloatingBar.Margin.Left;
                var yPos = e.GetPosition(null).Y - pos.Y + ViewboxFloatingBar.Margin.Top;
                ViewboxFloatingBar.Margin = new Thickness(xPos, yPos, -2000, -200);

                pos = e.GetPosition(null);
                if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
                    pointPPT = new Point(xPos, yPos);
                else
                    pointDesktop = new Point(xPos, yPos);
            }
        }

        private void SymbolIconEmoji_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (isViewboxFloatingBarMarginAnimationRunning)
            {
                ViewboxFloatingBar.BeginAnimation(MarginProperty, null);
                isViewboxFloatingBarMarginAnimationRunning = false;
            }

            isDragDropInEffect = true;
            pos = e.GetPosition(null);
            downPos = e.GetPosition(null);
            GridForFloatingBarDraging.Visibility = Visibility.Visible;
        }

        internal void SymbolIconEmoji_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isDragDropInEffect = false;

            if (e is null || (Math.Abs(downPos.X - e.GetPosition(null).X) <= 10 &&
                              Math.Abs(downPos.Y - e.GetPosition(null).Y) <= 10))
            {
                if (BorderFloatingBarMainControls.Visibility == Visibility.Visible)
                {
                    BorderFloatingBarMainControls.Visibility = Visibility.Collapsed;
                    CheckEnableTwoFingerGestureBtnVisibility(false);
                }
                else
                {
                    BorderFloatingBarMainControls.Visibility = Visibility.Visible;
                    CheckEnableTwoFingerGestureBtnVisibility(true);
                }
            }

            GridForFloatingBarDraging.Visibility = Visibility.Collapsed;
        }

        #endregion 浮動工具欄的拖動實現

        #region 隱藏子面板和按鈕背景高亮

        /// <summary>
        /// 隱藏形狀繪製面板
        /// </summary>
        private void CollapseBorderDrawShape()
        {
            AnimationsHelper.HideWithSlideAndFade(BorderDrawShape);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderDrawShape);
        }

        /// <summary>
        ///     <c>HideSubPanels</c>的青春版。目前需要修改<c>BorderSettings</c>的關閉機制（改為動畫關閉）。
        /// </summary>
        private void HideSubPanelsImmediately()
        {
            BorderTools.Visibility = Visibility.Collapsed;
            BoardBorderTools.Visibility = Visibility.Collapsed;
            PenPalette.Visibility = Visibility.Collapsed;
            BoardPenPalette.Visibility = Visibility.Collapsed;
            BoardEraserSizePanel.Visibility = Visibility.Collapsed;
            EraserSizePanel.Visibility = Visibility.Collapsed;
            BorderSettings.Visibility = Visibility.Collapsed;
            BoardBorderLeftPageListView.Visibility = Visibility.Collapsed;
            BoardBorderRightPageListView.Visibility = Visibility.Collapsed;
            BoardImageOptionsPanel.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        ///     <para>
        ///         易嚴定真，這個多功能函數包括了以下的內容：
        ///     </para>
        ///     <list type="number">
        ///         <item>
        ///             隱藏浮動工具欄和白板模式下的"更多功能"面板
        ///         </item>
        ///         <item>
        ///             隱藏白板模式下和浮動工具欄的畫筆調色盤
        ///         </item>
        ///         <item>
        ///             隱藏白板模式下的"清屏"按鈕（已作廢）
        ///         </item>
        ///         <item>
        ///             負責給Settings設置面板做隱藏動畫
        ///         </item>
        ///         <item>
        ///             隱藏白板模式下和浮動工具欄的"手勢"面板
        ///         </item>
        ///         <item>
        ///             當<c>ToggleSwitchDrawShapeBorderAutoHide</c>開啟時，會自動隱藏白板模式下和浮動工具欄的"形狀"面板
        ///         </item>
        ///         <item>
        ///             按需高亮指定的浮動工具欄和白板工具欄中的按鈕，通過param：<paramref name="mode"/> 來指定
        ///         </item>
        ///         <item>
        ///             將浮動工具欄自動居中，通過param：<paramref name="autoAlignCenter"/>
        ///         </item>
        ///     </list>
        /// </summary>
        /// <param name="mode">
        ///     <para>
        ///         按需高亮指定的浮動工具欄和白板工具欄中的按鈕，有下面幾種情況：
        ///     </para>
        ///     <list type="number">
        ///         <item>
        ///             當<c><paramref name="mode"/>==null</c>時，不會執行任何有關操作
        ///         </item>
        ///         <item>
        ///             當<c><paramref name="mode"/>!="clear"</c>時，會先取消高亮所有工具欄按鈕，然後根據下面的情況進行高亮處理
        ///         </item>
        ///         <item>
        ///             當<c><paramref name="mode"/>=="color" || <paramref name="mode"/>=="pen"</c>時，會高亮浮動工具欄和白板工具欄中的"批註"，"筆"按鈕
        ///         </item>
        ///         <item>
        ///             當<c><paramref name="mode"/>=="eraser"</c>時，會高亮白板工具欄中的"橡皮"和浮動工具欄中的"面積擦"按鈕
        ///         </item>
        ///         <item>
        ///             當<c><paramref name="mode"/>=="eraserByStrokes"</c>時，會高亮白板工具欄中的"橡皮"和浮動工具欄中的"墨跡擦"按鈕
        ///         </item>
        ///         <item>
        ///             當<c><paramref name="mode"/>=="select"</c>時，會高亮浮動工具欄和白板工具欄中的"選擇"，"套索選"按鈕
        ///         </item>
        ///     </list>
        /// </param>
        /// <param name="autoAlignCenter">
        ///     是否自動居中浮動工具欄
        /// </param>
        private async void HideSubPanels(string mode = null, bool autoAlignCenter = false)
        {
            AnimationsHelper.HideWithSlideAndFade(BorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
            AnimationsHelper.HideWithSlideAndFade(PenPalette);
            AnimationsHelper.HideWithSlideAndFade(BoardPenPalette);
            AnimationsHelper.HideWithSlideAndFade(BoardEraserSizePanel);
            AnimationsHelper.HideWithSlideAndFade(EraserSizePanel);
            AnimationsHelper.HideWithSlideAndFade(BorderDrawShape);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderLeftPageListView);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderRightPageListView);
            AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);

            // 隐藏背景设置面板
            var bgPalette = LogicalTreeHelper.FindLogicalNode(this, "BackgroundPalette") as Border;
            if (bgPalette != null)
            {
                AnimationsHelper.HideWithSlideAndFade(bgPalette);
            }

            if (BorderSettings.Visibility == Visibility.Visible)
            {
                // 设置蒙版为不可点击，并移除背景
                BorderSettingsMask.IsHitTestVisible = false;
                BorderSettingsMask.Background = null;
                var sb = new Storyboard();

                // 滑动动画
                var slideAnimation = new DoubleAnimation
                {
                    From = 0, // 滑动距离
                    To = BorderSettings.RenderTransform.Value.OffsetX - 440,
                    Duration = TimeSpan.FromSeconds(0.6)
                };
                slideAnimation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                Storyboard.SetTargetProperty(slideAnimation,
                    new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

                sb.Children.Add(slideAnimation);

                sb.Completed += (s, _) =>
                {
                    BorderSettings.Visibility = Visibility.Collapsed;
                    isOpeningOrHidingSettingsPane = false;
                };

                BorderSettings.Visibility = Visibility.Visible;
                BorderSettings.RenderTransform = new TranslateTransform();

                isOpeningOrHidingSettingsPane = true;
                sb.Begin(BorderSettings);
            }

            AnimationsHelper.HideWithSlideAndFade(TwoFingerGestureBorder);
            AnimationsHelper.HideWithSlideAndFade(EraserSizePanel);
            AnimationsHelper.HideWithSlideAndFade(BoardTwoFingerGestureBorder);
            if (ToggleSwitchDrawShapeBorderAutoHide.IsOn)
            {
                AnimationsHelper.HideWithSlideAndFade(BorderDrawShape);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderDrawShape);
            }

            if (mode != null)
            {
                if (mode != "clear")
                {
                    CursorIconGeometry.Brush = new SolidColorBrush(Color.FromRgb(27, 27, 27));
                    CursorIconGeometry.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.LinedCursorIcon);
                    PenIconGeometry.Brush = new SolidColorBrush(Color.FromRgb(27, 27, 27));
                    PenIconGeometry.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.LinedPenIcon);
                    StrokeEraserIconGeometry.Brush = new SolidColorBrush(Color.FromRgb(27, 27, 27));
                    StrokeEraserIconGeometry.Geometry =
                        Geometry.Parse(XamlGraphicsIconGeometries.LinedEraserStrokeIcon);
                    CircleEraserIconGeometry.Brush = new SolidColorBrush(Color.FromRgb(27, 27, 27));
                    CircleEraserIconGeometry.Geometry =
                        Geometry.Parse(XamlGraphicsIconGeometries.LinedEraserCircleIcon);
                    LassoSelectIconGeometry.Brush = new SolidColorBrush(Color.FromRgb(27, 27, 27));
                    LassoSelectIconGeometry.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.LinedLassoSelectIcon);

                    BoardPen.Background = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                    BoardSelect.Background = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                    BoardEraser.Background = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                    BoardSelectGeometry.Brush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    BoardPenGeometry.Brush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    BoardEraserGeometry.Brush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    BoardPenLabel.Foreground = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    BoardSelectLabel.Foreground = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    BoardEraserLabel.Foreground = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    BoardSelect.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                    BoardEraser.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                    BoardPen.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));

                    HideFloatingBarHighlight();
                }

                switch (mode)
                {
                    case "pen":
                    case "color":
                        {
                            PenIconGeometry.Brush = new SolidColorBrush(Color.FromRgb(30, 58, 138));
                            PenIconGeometry.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.SolidPenIcon);
                            BoardPen.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                            BoardPen.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                            BoardPenGeometry.Brush = new SolidColorBrush(Colors.GhostWhite);
                            BoardPenLabel.Foreground = new SolidColorBrush(Colors.GhostWhite);

                            SetFloatingBarHighlightPosition("pen");
                            break;
                        }
                    case "eraser":
                        {
                            CircleEraserIconGeometry.Brush = new SolidColorBrush(Color.FromRgb(30, 58, 138));
                            CircleEraserIconGeometry.Geometry =
                                Geometry.Parse(XamlGraphicsIconGeometries.SolidEraserCircleIcon);
                            BoardEraser.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                            BoardEraser.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                            BoardEraserGeometry.Brush = new SolidColorBrush(Colors.GhostWhite);
                            BoardEraserLabel.Foreground = new SolidColorBrush(Colors.GhostWhite);

                            SetFloatingBarHighlightPosition("eraser");
                            break;
                        }
                    case "eraserByStrokes":
                        {
                            StrokeEraserIconGeometry.Brush = new SolidColorBrush(Color.FromRgb(30, 58, 138));
                            StrokeEraserIconGeometry.Geometry =
                                Geometry.Parse(XamlGraphicsIconGeometries.SolidEraserStrokeIcon);
                            BoardEraser.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                            BoardEraser.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                            BoardEraserGeometry.Brush = new SolidColorBrush(Colors.GhostWhite);
                            BoardEraserLabel.Foreground = new SolidColorBrush(Colors.GhostWhite);

                            SetFloatingBarHighlightPosition("eraserByStrokes");
                            break;
                        }
                    case "select":
                        {
                            LassoSelectIconGeometry.Brush = new SolidColorBrush(Color.FromRgb(30, 58, 138));
                            LassoSelectIconGeometry.Geometry = 
                                Geometry.Parse(XamlGraphicsIconGeometries.SolidLassoSelectIcon);
                            BoardSelect.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                            BoardSelect.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                            BoardSelectGeometry.Brush = new SolidColorBrush(Colors.GhostWhite);
                            BoardSelectLabel.Foreground = new SolidColorBrush(Colors.GhostWhite);

                            SetFloatingBarHighlightPosition("select");
                            break;
                        }
                    case "cursor":
                        {
                            CursorIconGeometry.Brush = new SolidColorBrush(Color.FromRgb(30, 58, 138));
                            CursorIconGeometry.Geometry = 
                                Geometry.Parse(XamlGraphicsIconGeometries.LinedCursorIcon);
                            BoardPen.Background = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                            BoardPen.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                            BoardPenGeometry.Brush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                            BoardPenLabel.Foreground = new SolidColorBrush(Color.FromRgb(24, 24, 27));

                            SetFloatingBarHighlightPosition("cursor");
                            break;
                        }
                    case "shape":
                        {
                            // 对图形模式进行特殊处理，不修改按钮UI状态
                            // 只隐藏相关面板，但保持图形绘制模式
                            break;
                        }
                }


                if (autoAlignCenter) // 控制居中
                {
                    if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
                    {
                        await Task.Delay(50);
                        ViewboxFloatingBarMarginAnimation(60);
                    }
                    else if (Topmost) //非黑板
                    {
                        await Task.Delay(50);
                        ViewboxFloatingBarMarginAnimation(100, true);
                    }
                    else //黑板
                    {
                        await Task.Delay(50);
                        ViewboxFloatingBarMarginAnimation(60);
                    }
                }
            }

            await Task.Delay(150);
            isHidingSubPanelsWhenInking = false;
        }

        #endregion

        #region 撤銷重做按鈕

        internal void SymbolIconUndo_MouseUp(object sender, MouseButtonEventArgs e)
        {
            //if (lastBorderMouseDownObject != sender) return;

            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == SymbolIconUndo && lastBorderMouseDownObject != SymbolIconUndo) return;

            if (!BtnUndo.IsEnabled) return;
            BtnUndo_Click(BtnUndo, null);
            HideSubPanels();
        }

        internal void SymbolIconRedo_MouseUp(object sender, MouseButtonEventArgs e)
        {
            //if (lastBorderMouseDownObject != sender) return;

            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == SymbolIconRedo && lastBorderMouseDownObject != SymbolIconRedo) return;

            if (!BtnRedo.IsEnabled) return;
            BtnRedo_Click(BtnRedo, null);
            HideSubPanels();
        }

        #endregion

        #region 白板按鈕和退出白板模式按鈕

        //private bool Not_Enter_Blackboard_fir_Mouse_Click = true;
        private bool isDisplayingOrHidingBlackboard;

        internal void ImageBlackboard_MouseUp(object sender, MouseButtonEventArgs e)
        {

            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == WhiteboardFloatingBarBtn && lastBorderMouseDownObject != WhiteboardFloatingBarBtn) return;

            LeftUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            RightUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            if (isDisplayingOrHidingBlackboard) return;
            isDisplayingOrHidingBlackboard = true;

            UnFoldFloatingBar_MouseUp(null, null);

            if (inkCanvas.EditingMode == InkCanvasEditingMode.Select) PenIcon_Click(null, null);

            if (currentMode == 0)
            {
                LeftBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                RightBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                LeftSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                RightSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                //進入黑板

                /*
                if (Not_Enter_Blackboard_fir_Mouse_Click) {// BUG-Fixed_tmp：程序启动后直接进入白板会导致后续撤销功能、退出白板无法恢复墨迹
                    BtnColorRed_Click(BorderPenColorRed, null);
                    await Task.Delay(200);
                    SimulateMouseClick.SimulateMouseClickAtTopLeft();
                    await Task.Delay(10);
                    Not_Enter_Blackboard_fir_Mouse_Click = false;
                }
                */
                new Thread(() =>
                {
                    Thread.Sleep(100);
                    Application.Current.Dispatcher.Invoke(() => { ViewboxFloatingBarMarginAnimation(60); });
                }).Start();

                HideSubPanels();

                if (GridTransparencyFakeBackground.Background == Brushes.Transparent)
                {
                    if (currentMode == 1)
                    {
                        currentMode = 0;
                        GridBackgroundCover.Visibility = Visibility.Collapsed;
                        AnimationsHelper.HideWithSlideAndFade(BlackboardLeftSide);
                        AnimationsHelper.HideWithSlideAndFade(BlackboardCenterSide);
                        AnimationsHelper.HideWithSlideAndFade(BlackboardRightSide);
                    }

                    BtnHideInkCanvas_Click(BtnHideInkCanvas, null);
                }

                if (Settings.Gesture.AutoSwitchTwoFingerGesture) // 自动关闭多指书写、开启双指移动
                {
                    ToggleSwitchEnableTwoFingerTranslate.IsOn = true;
                    if (isInMultiTouchMode) ToggleSwitchEnableMultiTouchMode.IsOn = false;
                }

                if (Settings.Appearance.EnableTimeDisplayInWhiteboardMode)
                {
                    WaterMarkTime.Visibility = Visibility.Visible;
                    WaterMarkDate.Visibility = Visibility.Visible;
                }
                else
                {
                    WaterMarkTime.Visibility = Visibility.Collapsed;
                    WaterMarkDate.Visibility = Visibility.Collapsed;
                }

                if (Settings.Appearance.EnableChickenSoupInWhiteboardMode)
                {
                    BlackBoardWaterMark.Visibility = Visibility.Visible;
                }
                else
                {
                    BlackBoardWaterMark.Visibility = Visibility.Collapsed;
                }

                if (Settings.Appearance.ChickenSoupSource == 0)
                {
                    int randChickenSoupIndex = new Random().Next(ChickenSoup.OSUPlayerYuLu.Length);
                    BlackBoardWaterMark.Text = ChickenSoup.OSUPlayerYuLu[randChickenSoupIndex];
                }
                else if (Settings.Appearance.ChickenSoupSource == 1)
                {
                    int randChickenSoupIndex = new Random().Next(ChickenSoup.MingYanJingJu.Length);
                    BlackBoardWaterMark.Text = ChickenSoup.MingYanJingJu[randChickenSoupIndex];
                }
                else if (Settings.Appearance.ChickenSoupSource == 2)
                {
                    int randChickenSoupIndex = new Random().Next(ChickenSoup.GaoKaoPhrases.Length);
                    BlackBoardWaterMark.Text = ChickenSoup.GaoKaoPhrases[randChickenSoupIndex];
                }

                if (Settings.Canvas.UsingWhiteboard)
                {
                    ICCWaterMarkDark.Visibility = Visibility.Visible;
                    ICCWaterMarkWhite.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ICCWaterMarkWhite.Visibility = Visibility.Visible;
                    ICCWaterMarkDark.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                //关闭黑板
                HideSubPanelsImmediately();

                if (StackPanelPPTControls.Visibility == Visibility.Visible)
                {
                    var dops = Settings.PowerPointSettings.PPTButtonsDisplayOption.ToString();
                    var dopsc = dops.ToCharArray();
                    if (dopsc[0] == '2' && isDisplayingOrHidingBlackboard == false) AnimationsHelper.ShowWithFadeIn(LeftBottomPanelForPPTNavigation);
                    if (dopsc[1] == '2' && isDisplayingOrHidingBlackboard == false) AnimationsHelper.ShowWithFadeIn(RightBottomPanelForPPTNavigation);
                    if (dopsc[2] == '2' && isDisplayingOrHidingBlackboard == false) AnimationsHelper.ShowWithFadeIn(LeftSidePanelForPPTNavigation);
                    if (dopsc[3] == '2' && isDisplayingOrHidingBlackboard == false) AnimationsHelper.ShowWithFadeIn(RightSidePanelForPPTNavigation);
                }
                // 修复PPT放映时点击白板按钮后翻页按钮不显示的问题
                if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
                {
                    // 强制显示PPT翻页按钮
                    LeftBottomPanelForPPTNavigation.Visibility = Visibility.Visible;
                    RightBottomPanelForPPTNavigation.Visibility = Visibility.Visible;
                    LeftSidePanelForPPTNavigation.Visibility = Visibility.Visible;
                    RightSidePanelForPPTNavigation.Visibility = Visibility.Visible;
                }

                if (Settings.Automation.IsAutoSaveStrokesAtClear &&
                    inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber) SaveScreenShot(true);

                if (BtnPPTSlideShowEnd.Visibility == Visibility.Collapsed)
                    new Thread(() =>
                    {
                        Thread.Sleep(300);
                        Application.Current.Dispatcher.Invoke(() => { ViewboxFloatingBarMarginAnimation(100, true); });
                    }).Start();
                else
                    new Thread(() =>
                    {
                        Thread.Sleep(300);
                        Application.Current.Dispatcher.Invoke(() => { ViewboxFloatingBarMarginAnimation(60); });
                    }).Start();

                if (System.Windows.Controls.Canvas.GetLeft(FloatingbarSelectionBG) != 28) PenIcon_Click(null, null);

                if (Settings.Gesture.AutoSwitchTwoFingerGesture) // 自动启用多指书写
                    ToggleSwitchEnableTwoFingerTranslate.IsOn = false;
                // 2024.5.2 need to be tested
                // if (!isInMultiTouchMode) ToggleSwitchEnableMultiTouchMode.IsOn = true;
                WaterMarkTime.Visibility = Visibility.Collapsed;
                WaterMarkDate.Visibility = Visibility.Collapsed;
                BlackBoardWaterMark.Visibility = Visibility.Collapsed;
                ICCWaterMarkDark.Visibility = Visibility.Collapsed;
                ICCWaterMarkWhite.Visibility = Visibility.Collapsed;
            }

            BtnSwitch_Click(BtnSwitch, null);

            if (currentMode == 0 && inkCanvas.Strokes.Count == 0 && BtnPPTSlideShowEnd.Visibility != Visibility.Visible)
                CursorIcon_Click(null, null);

            BtnExit.Foreground = Brushes.White;
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;

            new Thread(() =>
            {
                Thread.Sleep(200);
                Application.Current.Dispatcher.Invoke(() => { isDisplayingOrHidingBlackboard = false; });
            }).Start();

            SwitchToDefaultPen(null, null);
            CheckColorTheme(true);
        }

        #endregion
        private async void SymbolIconCursor_Click(object sender, RoutedEventArgs e)
        {
            if (currentMode != 0)
            {
                ImageBlackboard_MouseUp(null, null);
            }
            else
            {
                BtnHideInkCanvas_Click(BtnHideInkCanvas, null);

                if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
                {
                    await Task.Delay(100);
                    ViewboxFloatingBarMarginAnimation(60);
                }
            }
        }

        #region 清空畫布按鈕

        internal void SymbolIconDelete_MouseUp(object sender, MouseButtonEventArgs e)
        {

            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == SymbolIconDelete && lastBorderMouseDownObject != SymbolIconDelete) return;

            if (inkCanvas.GetSelectedStrokes().Count > 0)
            {
                inkCanvas.Strokes.Remove(inkCanvas.GetSelectedStrokes());
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
            }
            else if (inkCanvas.Strokes.Count > 0)
            {
                if (Settings.Automation.IsAutoSaveStrokesAtClear &&
                    inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber)
                {
                    if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
                    {
                        var currentSlide = _pptManager?.GetCurrentSlideNumber() ?? 0;
                        var presentationName = _pptManager?.GetPresentationName() ?? "";
                        SaveScreenShot(true, $"{presentationName}/{currentSlide}_{DateTime.Now:HH-mm-ss}");
                    }
                    else
                        SaveScreenShot(true);
                }

                BtnClear_Click(null, null);
            }
        }

        #endregion

        /// <summary>
        /// 面积擦子面板的清空墨迹按钮事件处理
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">RoutedEventArgs</param>
        private void EraserPanelSymbolIconDelete_MouseUp(object sender, RoutedEventArgs e)
        {
            SymbolIconDelete_MouseUp(null, null);
        }

        #region 主要的工具按鈕事件

        /// <summary>
        ///     浮動工具欄的"套索選"按鈕事件，重定向到舊UI的<c>BtnSelect_Click</c>方法
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">MouseButtonEventArgs</param>
        internal void SymbolIconSelect_MouseUp(object sender, MouseButtonEventArgs e)
        {

            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == SymbolIconSelect && lastBorderMouseDownObject != SymbolIconSelect) return;

            BtnSelect_Click(null, null);
            HideSubPanels("select");

        }

        #endregion

        private void FloatingBarToolBtnMouseDownFeedback_Panel(object sender, MouseButtonEventArgs e)
        {
            if (sender is Panel panel)
            {
                lastBorderMouseDownObject = sender;
                if (panel == SymbolIconDelete) panel.Background = new SolidColorBrush(Color.FromArgb(28, 127, 29, 29));
                else panel.Background = new SolidColorBrush(Color.FromArgb(28, 24, 24, 27));
            }
            else if (sender is Border border)
            {
                lastBorderMouseDownObject = sender;
                // 对于快捷调色板的颜色球，不改变背景颜色，只添加透明度效果
                if (border.Name?.StartsWith("QuickColor") == true)
                {
                    // 保存原始颜色并添加透明度
                    var originalColor = border.Background as SolidColorBrush;
                    if (originalColor != null)
                    {
                        border.Background = new SolidColorBrush(Color.FromArgb(180, originalColor.Color.R, originalColor.Color.G, originalColor.Color.B));
                    }
                }
                else
                {
                    border.Background = new SolidColorBrush(Color.FromArgb(28, 24, 24, 27));
                }
            }
        }

        private void FloatingBarToolBtnMouseLeaveFeedback_Panel(object sender, MouseEventArgs e)
        {
            if (sender is Panel panel)
            {
                lastBorderMouseDownObject = null;
                panel.Background = new SolidColorBrush(Colors.Transparent);
            }
            else if (sender is Border border)
            {
                lastBorderMouseDownObject = null;
                // 对于快捷调色板的颜色球，恢复原始颜色
                if (border.Name?.StartsWith("QuickColor") == true)
                {
                    // 根据颜色球名称恢复对应的颜色
                    switch (border.Name)
                    {
                        case "QuickColorWhite":
                        case "QuickColorWhiteSingle":
                            border.Background = new SolidColorBrush(Colors.White);
                            break;
                        case "QuickColorOrange":
                        case "QuickColorOrangeSingle":
                            border.Background = new SolidColorBrush(Color.FromRgb(251, 150, 80));
                            break;
                        case "QuickColorYellow":
                        case "QuickColorYellowSingle":
                            border.Background = new SolidColorBrush(Colors.Yellow);
                            break;
                        case "QuickColorBlack":
                        case "QuickColorBlackSingle":
                            border.Background = new SolidColorBrush(Colors.Black);
                            break;
                        case "QuickColorBlue":
                            border.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                            break;
                        case "QuickColorRed":
                        case "QuickColorRedSingle":
                            border.Background = new SolidColorBrush(Colors.Red);
                            break;
                        case "QuickColorGreen":
                        case "QuickColorGreenSingle":
                            border.Background = new SolidColorBrush(Color.FromRgb(22, 163, 74)); 
                            break;
                        case "QuickColorPurple":
                            border.Background = new SolidColorBrush(Color.FromRgb(147, 51, 234));
                            break;
                    }
                }
                else
                {
                    border.Background = new SolidColorBrush(Colors.Transparent);
                }
            }
        }

        private void SymbolIconSettings_Click(object sender, RoutedEventArgs e)
        {
            if (isOpeningOrHidingSettingsPane) return;
            HideSubPanels();
            BtnSettings_Click(null, null);
        }
        private async void SymbolIconScreenshot_MouseUp(object sender, MouseButtonEventArgs e) {
            HideSubPanelsImmediately();
            await Task.Delay(50);
            SaveScreenShotToDesktop();
        }

        private void ImageCountdownTimer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            LeftUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            RightUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            AnimationsHelper.HideWithSlideAndFade(BorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);

            new CountdownTimerWindow().Show();
        }

        private void OperatingGuideWindowIcon_MouseUp(object sender, MouseButtonEventArgs e)
        {
            AnimationsHelper.HideWithSlideAndFade(BorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);

            new OperatingGuideWindow().Show();
        }

        private void SymbolIconRand_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // 如果控件被隐藏，不处理事件
            if (RandomDrawPanel.Visibility != Visibility.Visible) return;

            LeftUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            RightUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;

            AnimationsHelper.HideWithSlideAndFade(BorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);

            new RandWindow(Settings).Show();
        }

        public void CheckEraserTypeTab()
        {
            if (Settings.Canvas.EraserShapeType == 0)
            {
                CircleEraserTabButton.Background = new SolidColorBrush(Color.FromArgb(85, 59, 130, 246));
                CircleEraserTabButton.Opacity = 1;
                CircleEraserTabButtonText.FontWeight = FontWeights.Bold;
                CircleEraserTabButtonText.Margin = new Thickness(2, 0.5, 0, 0);
                CircleEraserTabButtonText.FontSize = 9.5;
                CircleEraserTabButtonIndicator.Visibility = Visibility.Visible;
                RectangleEraserTabButton.Background = new SolidColorBrush(Colors.Transparent);
                RectangleEraserTabButton.Opacity = 0.75;
                RectangleEraserTabButtonText.FontWeight = FontWeights.Normal;
                RectangleEraserTabButtonText.FontSize = 9;
                RectangleEraserTabButtonText.Margin = new Thickness(2, 1, 0, 0);
                RectangleEraserTabButtonIndicator.Visibility = Visibility.Collapsed;

                BoardCircleEraserTabButton.Background = new SolidColorBrush(Color.FromArgb(85, 59, 130, 246));
                BoardCircleEraserTabButton.Opacity = 1;
                BoardCircleEraserTabButtonText.FontWeight = FontWeights.Bold;
                BoardCircleEraserTabButtonText.Margin = new Thickness(2, 0.5, 0, 0);
                BoardCircleEraserTabButtonText.FontSize = 9.5;
                BoardCircleEraserTabButtonIndicator.Visibility = Visibility.Visible;
                BoardRectangleEraserTabButton.Background = new SolidColorBrush(Colors.Transparent);
                BoardRectangleEraserTabButton.Opacity = 0.75;
                BoardRectangleEraserTabButtonText.FontWeight = FontWeights.Normal;
                BoardRectangleEraserTabButtonText.FontSize = 9;
                BoardRectangleEraserTabButtonText.Margin = new Thickness(2, 1, 0, 0);
                BoardRectangleEraserTabButtonIndicator.Visibility = Visibility.Collapsed;
            }
            else
            {
                RectangleEraserTabButton.Background = new SolidColorBrush(Color.FromArgb(85, 59, 130, 246));
                RectangleEraserTabButton.Opacity = 1;
                RectangleEraserTabButtonText.FontWeight = FontWeights.Bold;
                RectangleEraserTabButtonText.Margin = new Thickness(2, 0.5, 0, 0);
                RectangleEraserTabButtonText.FontSize = 9.5;
                RectangleEraserTabButtonIndicator.Visibility = Visibility.Visible;
                CircleEraserTabButton.Background = new SolidColorBrush(Colors.Transparent);
                CircleEraserTabButton.Opacity = 0.75;
                CircleEraserTabButtonText.FontWeight = FontWeights.Normal;
                CircleEraserTabButtonText.FontSize = 9;
                CircleEraserTabButtonText.Margin = new Thickness(2, 1, 0, 0);
                CircleEraserTabButtonIndicator.Visibility = Visibility.Collapsed;

                BoardRectangleEraserTabButton.Background = new SolidColorBrush(Color.FromArgb(85, 59, 130, 246));
                BoardRectangleEraserTabButton.Opacity = 1;
                BoardRectangleEraserTabButtonText.FontWeight = FontWeights.Bold;
                BoardRectangleEraserTabButtonText.Margin = new Thickness(2, 0.5, 0, 0);
                BoardRectangleEraserTabButtonText.FontSize = 9.5;
                BoardRectangleEraserTabButtonIndicator.Visibility = Visibility.Visible;
                BoardCircleEraserTabButton.Background = new SolidColorBrush(Colors.Transparent);
                BoardCircleEraserTabButton.Opacity = 0.75;
                BoardCircleEraserTabButtonText.FontWeight = FontWeights.Normal;
                BoardCircleEraserTabButtonText.FontSize = 9;
                BoardCircleEraserTabButtonText.Margin = new Thickness(2, 1, 0, 0);
                BoardCircleEraserTabButtonIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private void SymbolIconRandOne_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // 如果控件被隐藏，不处理事件
            if (SingleDrawPanel.Visibility != Visibility.Visible) return;

            LeftUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            RightUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;

            AnimationsHelper.HideWithSlideAndFade(BorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);

            // 检查是否启用了直接调用ClassIsland点名功能
            if (Settings.RandSettings.DirectCallCiRand)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "classisland://plugins/IslandCaller/Simple/1",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("无法调用ClassIsland点名：" + ex.Message);

                    // 调用失败时回退到默认的随机点名窗口
                    new RandWindow(Settings, true).ShowDialog();
                }
            }
            else
            {
                // 使用默认的随机点名窗口
                new RandWindow(Settings, true).ShowDialog();
            }
        }

        private void GridInkReplayButton_MouseUp(object sender, MouseButtonEventArgs e)
        {
            //if (lastBorderMouseDownObject != sender) return;

            AnimationsHelper.HideWithSlideAndFade(BorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);

            CollapseBorderDrawShape();

            InkCanvasForInkReplay.Visibility = Visibility.Visible;
            InkCanvasGridForInkReplay.Visibility = Visibility.Hidden;
            InkCanvasGridForInkReplay.IsHitTestVisible = false;
            FloatingbarUIForInkReplay.Visibility = Visibility.Hidden;
            FloatingbarUIForInkReplay.IsHitTestVisible = false;
            BlackboardUIGridForInkReplay.Visibility = Visibility.Hidden;
            BlackboardUIGridForInkReplay.IsHitTestVisible = false;

            AnimationsHelper.ShowWithFadeIn(BorderInkReplayToolBox);
            InkReplayPanelStatusText.Text = "正在重播墨迹...";
            InkReplayPlayPauseBorder.Background = new SolidColorBrush(Colors.Transparent);
            InkReplayPlayButtonImage.Visibility = Visibility.Collapsed;
            InkReplayPauseButtonImage.Visibility = Visibility.Visible;

            isStopInkReplay = false;
            isPauseInkReplay = false;
            isRestartInkReplay = false;
            inkReplaySpeed = 1;
            InkCanvasForInkReplay.Strokes.Clear();
            var strokes = inkCanvas.Strokes.Clone();
            if (inkCanvas.GetSelectedStrokes().Count != 0) strokes = inkCanvas.GetSelectedStrokes().Clone();
            int k = 1, i = 0;
            new Thread(() =>
            {
                isRestartInkReplay = true;
                while (isRestartInkReplay)
                {
                    isRestartInkReplay = false;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        InkCanvasForInkReplay.Strokes.Clear();
                    });
                    foreach (var stroke in strokes)
                    {

                        if (isRestartInkReplay) break;

                        var stylusPoints = new StylusPointCollection();
                        if (stroke.StylusPoints.Count == 629) //圆或椭圆
                        {
                            Stroke s = null;
                            foreach (var stylusPoint in stroke.StylusPoints)
                            {

                                if (isRestartInkReplay) break;

                                while (isPauseInkReplay)
                                {
                                    Thread.Sleep(10);
                                }

                                if (i++ >= 50)
                                {
                                    i = 0;
                                    Thread.Sleep((int)(10 / inkReplaySpeed));
                                    if (isStopInkReplay) return;
                                }

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        InkCanvasForInkReplay.Strokes.Remove(s);
                                    }
                                    catch { }

                                    stylusPoints.Add(stylusPoint);
                                    s = new Stroke(stylusPoints.Clone());
                                    s.DrawingAttributes = stroke.DrawingAttributes;
                                    InkCanvasForInkReplay.Strokes.Add(s);
                                });
                            }
                        }
                        else
                        {
                            Stroke s = null;
                            foreach (var stylusPoint in stroke.StylusPoints)
                            {

                                if (isRestartInkReplay) break;

                                while (isPauseInkReplay)
                                {
                                    Thread.Sleep(10);
                                }

                                if (i++ >= k)
                                {
                                    i = 0;
                                    Thread.Sleep((int)(10 / inkReplaySpeed));
                                    if (isStopInkReplay) return;
                                }

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        InkCanvasForInkReplay.Strokes.Remove(s);
                                    }
                                    catch { }

                                    stylusPoints.Add(stylusPoint);
                                    s = new Stroke(stylusPoints.Clone());
                                    s.DrawingAttributes = stroke.DrawingAttributes;
                                    InkCanvasForInkReplay.Strokes.Add(s);
                                });
                            }
                        }
                    }
                }

                Thread.Sleep(100);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    InkCanvasForInkReplay.Visibility = Visibility.Collapsed;
                    InkCanvasGridForInkReplay.Visibility = Visibility.Visible;
                    InkCanvasGridForInkReplay.IsHitTestVisible = true;
                    AnimationsHelper.HideWithFadeOut(BorderInkReplayToolBox);
                    FloatingbarUIForInkReplay.Visibility = Visibility.Visible;
                    FloatingbarUIForInkReplay.IsHitTestVisible = true;
                    BlackboardUIGridForInkReplay.Visibility = Visibility.Visible;
                    BlackboardUIGridForInkReplay.IsHitTestVisible = true;
                });
            }).Start();
        }

        private bool isStopInkReplay;
        private bool isPauseInkReplay;
        private bool isRestartInkReplay;
        private double inkReplaySpeed = 1;

        private void InkCanvasForInkReplay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                InkCanvasForInkReplay.Visibility = Visibility.Collapsed;
                InkCanvasGridForInkReplay.Visibility = Visibility.Visible;
                InkCanvasGridForInkReplay.IsHitTestVisible = true;
                FloatingbarUIForInkReplay.Visibility = Visibility.Visible;
                FloatingbarUIForInkReplay.IsHitTestVisible = true;
                BlackboardUIGridForInkReplay.Visibility = Visibility.Visible;
                BlackboardUIGridForInkReplay.IsHitTestVisible = true;
                AnimationsHelper.HideWithFadeOut(BorderInkReplayToolBox);
                isStopInkReplay = true;
            }
        }

        private void InkReplayPlayPauseBorder_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            InkReplayPlayPauseBorder.Background = new SolidColorBrush(Color.FromArgb(34, 9, 9, 11));
        }

        private void InkReplayPlayPauseBorder_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            InkReplayPlayPauseBorder.Background = new SolidColorBrush(Colors.Transparent);
            isPauseInkReplay = !isPauseInkReplay;
            InkReplayPanelStatusText.Text = isPauseInkReplay ? "已暂停！" : "正在重播墨迹...";
            InkReplayPlayButtonImage.Visibility = isPauseInkReplay ? Visibility.Visible : Visibility.Collapsed;
            InkReplayPauseButtonImage.Visibility = !isPauseInkReplay ? Visibility.Visible : Visibility.Collapsed;
        }

        private void InkReplayStopButtonBorder_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            InkReplayStopButtonBorder.Background = new SolidColorBrush(Color.FromArgb(34, 9, 9, 11));
        }

        private void InkReplayStopButtonBorder_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            InkReplayStopButtonBorder.Background = new SolidColorBrush(Colors.Transparent);
            InkCanvasForInkReplay.Visibility = Visibility.Collapsed;
            InkCanvasGridForInkReplay.Visibility = Visibility.Visible;
            InkCanvasGridForInkReplay.IsHitTestVisible = true;
            FloatingbarUIForInkReplay.Visibility = Visibility.Visible;
            FloatingbarUIForInkReplay.IsHitTestVisible = true;
            BlackboardUIGridForInkReplay.Visibility = Visibility.Visible;
            BlackboardUIGridForInkReplay.IsHitTestVisible = true;
            AnimationsHelper.HideWithFadeOut(BorderInkReplayToolBox);
            isStopInkReplay = true;
        }

        private void InkReplayReplayButtonBorder_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            InkReplayReplayButtonBorder.Background = new SolidColorBrush(Color.FromArgb(34, 9, 9, 11));
        }

        private void InkReplayReplayButtonBorder_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            InkReplayReplayButtonBorder.Background = new SolidColorBrush(Colors.Transparent);
            isRestartInkReplay = true;
            isPauseInkReplay = false;
            InkReplayPanelStatusText.Text = "正在重播墨迹...";
            InkReplayPlayButtonImage.Visibility = Visibility.Collapsed;
            InkReplayPauseButtonImage.Visibility = Visibility.Visible;
        }

        private void InkReplaySpeedButtonBorder_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            InkReplaySpeedButtonBorder.Background = new SolidColorBrush(Color.FromArgb(34, 9, 9, 11));
        }

        private void InkReplaySpeedButtonBorder_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            InkReplaySpeedButtonBorder.Background = new SolidColorBrush(Colors.Transparent);
            inkReplaySpeed = inkReplaySpeed == 0.5 ? 1 :
                inkReplaySpeed == 1 ? 2 :
                inkReplaySpeed == 2 ? 4 :
                inkReplaySpeed == 4 ? 8 : 0.5;
            InkReplaySpeedTextBlock.Text = inkReplaySpeed + "x";
        }

        private void SymbolIconTools_MouseUp(object sender, MouseButtonEventArgs e)
        {

            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == ToolsFloatingBarBtn && lastBorderMouseDownObject != ToolsFloatingBarBtn) return;

            if (BorderTools.Visibility == Visibility.Visible)
            {
                AnimationsHelper.HideWithSlideAndFade(EraserSizePanel);
                AnimationsHelper.HideWithSlideAndFade(BorderTools);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
                AnimationsHelper.HideWithSlideAndFade(PenPalette);
                AnimationsHelper.HideWithSlideAndFade(BoardPenPalette);
                AnimationsHelper.HideWithSlideAndFade(BorderDrawShape);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderDrawShape);
                AnimationsHelper.HideWithSlideAndFade(BoardEraserSizePanel);
                AnimationsHelper.HideWithSlideAndFade(BorderTools);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
                AnimationsHelper.HideWithSlideAndFade(TwoFingerGestureBorder);
                AnimationsHelper.HideWithSlideAndFade(BoardTwoFingerGestureBorder);
                AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);
            }
            else
            {
                AnimationsHelper.HideWithSlideAndFade(EraserSizePanel);
                AnimationsHelper.HideWithSlideAndFade(BorderTools);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
                AnimationsHelper.HideWithSlideAndFade(PenPalette);
                AnimationsHelper.HideWithSlideAndFade(BoardPenPalette);
                AnimationsHelper.HideWithSlideAndFade(BorderDrawShape);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderDrawShape);
                AnimationsHelper.HideWithSlideAndFade(BoardEraserSizePanel);
                AnimationsHelper.HideWithSlideAndFade(TwoFingerGestureBorder);
                AnimationsHelper.HideWithSlideAndFade(BoardTwoFingerGestureBorder);
                AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);
                AnimationsHelper.ShowWithSlideFromBottomAndFade(BorderTools);
                AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardBorderTools);
            }
        }

        private bool isViewboxFloatingBarMarginAnimationRunning;

        public async void ViewboxFloatingBarMarginAnimation(int MarginFromEdge,
            bool PosXCaculatedWithTaskbarHeight = false)
        {
            if (MarginFromEdge == 60) MarginFromEdge = 55;
            await Dispatcher.InvokeAsync(() =>
            {
                if (Topmost == false)
                    MarginFromEdge = -60;
                else
                    ViewboxFloatingBar.Visibility = Visibility.Visible;
                isViewboxFloatingBarMarginAnimationRunning = true;

                double dpiScaleX = 1, dpiScaleY = 1;
                var source = PresentationSource.FromVisual(this);
                if (source != null)
                {
                    dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                    dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
                }

                var windowHandle = new WindowInteropHelper(this).Handle;
                var screen = Screen.FromHandle(windowHandle);
                double screenWidth = screen.Bounds.Width / dpiScaleX, screenHeight = screen.Bounds.Height / dpiScaleY;
                // 仅计算Windows任务栏高度，不考虑其他程序对工作区的影响
                var toolbarHeight = ForegroundWindowInfo.GetTaskbarHeight(screen, dpiScaleY);
                
                // 计算浮动栏位置，考虑快捷调色盘的显示状态
                double floatingBarWidth = ViewboxFloatingBar.ActualWidth * ViewboxFloatingBarScaleTransform.ScaleX;
                
                // 如果快捷调色盘显示，确保有足够空间
                if ((QuickColorPalettePanel != null && QuickColorPalettePanel.Visibility == Visibility.Visible) ||
                    (QuickColorPaletteSingleRowPanel != null && QuickColorPaletteSingleRowPanel.Visibility == Visibility.Visible))
                {
                    // 根据显示模式调整宽度
                    if (Settings.Appearance.QuickColorPaletteDisplayMode == 0)
                    {
                        // 单行显示模式，自适应宽度，但需要足够空间显示6个颜色
                        floatingBarWidth = Math.Max(floatingBarWidth, 120 * ViewboxFloatingBarScaleTransform.ScaleX);
                    }
                    else
                    {
                        // 双行显示模式，宽度较大
                        floatingBarWidth = Math.Max(floatingBarWidth, 820 * ViewboxFloatingBarScaleTransform.ScaleX);
                    }
                }
                
                pos.X = (screenWidth - floatingBarWidth) / 2;

                if (PosXCaculatedWithTaskbarHeight == false)
                {
                    // 如果任务栏高度为0(隐藏状态),则使用固定边距
                    if (toolbarHeight == 0)
                    {
                        pos.Y = screenHeight - MarginFromEdge * ViewboxFloatingBarScaleTransform.ScaleY;
                        LogHelper.WriteLogToFile($"任务栏隐藏,使用固定边距: {MarginFromEdge}");
                    }
                    else
                    {
                        pos.Y = screenHeight - MarginFromEdge * ViewboxFloatingBarScaleTransform.ScaleY;
                    }
                }
                else if (PosXCaculatedWithTaskbarHeight)
                {
                    // 如果任务栏高度为0(隐藏状态),则使用固定高度
                    if (toolbarHeight == 0)
                    {
                        pos.Y = screenHeight - ViewboxFloatingBar.ActualHeight * ViewboxFloatingBarScaleTransform.ScaleY -
                               3 * ViewboxFloatingBarScaleTransform.ScaleY;
                        LogHelper.WriteLogToFile($"任务栏隐藏,使用固定高度: {ViewboxFloatingBar.ActualHeight}");
                    }
                    else
                    {
                        pos.Y = screenHeight - ViewboxFloatingBar.ActualHeight * ViewboxFloatingBarScaleTransform.ScaleY -
                               toolbarHeight - ViewboxFloatingBarScaleTransform.ScaleY * 3;
                    }
                }

                if (MarginFromEdge != -60)
                {
                    if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
                    {
                        if (pointPPT.X != -1 || pointPPT.Y != -1)
                        {
                            if (Math.Abs(pointPPT.Y - pos.Y) > 50)
                                pos = pointPPT;
                            else
                                pointPPT = pos;
                        }
                    }
                    else
                    {
                        if (pointDesktop.X != -1 || pointDesktop.Y != -1)
                        {
                            if (Math.Abs(pointDesktop.Y - pos.Y) > 50)
                                pos = pointDesktop;
                            else
                                pointDesktop = pos;
                        }
                    }
                }

                var marginAnimation = new ThicknessAnimation
                {
                    Duration = TimeSpan.FromSeconds(0.35),
                    From = ViewboxFloatingBar.Margin,
                    To = new Thickness(pos.X, pos.Y, 0, -20)
                };
                marginAnimation.EasingFunction = new CircleEase();
                ViewboxFloatingBar.BeginAnimation(MarginProperty, marginAnimation);
            });

            await Task.Delay(200);

            await Dispatcher.InvokeAsync(() =>
            {
                ViewboxFloatingBar.Margin = new Thickness(pos.X, pos.Y, -2000, -200);
                if (Topmost == false) ViewboxFloatingBar.Visibility = Visibility.Hidden;
            });
        }

        public async void PureViewboxFloatingBarMarginAnimationInDesktopMode()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                ViewboxFloatingBar.Visibility = Visibility.Visible;
                isViewboxFloatingBarMarginAnimationRunning = true;

                double dpiScaleX = 1, dpiScaleY = 1;
                var source = PresentationSource.FromVisual(this);
                if (source != null)
                {
                    dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                    dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
                }

                var windowHandle = new WindowInteropHelper(this).Handle;
                var screen = Screen.FromHandle(windowHandle);
                double screenWidth = screen.Bounds.Width / dpiScaleX, screenHeight = screen.Bounds.Height / dpiScaleY;
                // 仅计算Windows任务栏高度，不考虑其他程序对工作区的影响
                var toolbarHeight = ForegroundWindowInfo.GetTaskbarHeight(screen, dpiScaleY);
                
                // 计算浮动栏位置，考虑快捷调色盘的显示状态
                double floatingBarWidth = ViewboxFloatingBar.ActualWidth * ViewboxFloatingBarScaleTransform.ScaleX;
                
                // 如果快捷调色盘显示，确保有足够空间
                if ((QuickColorPalettePanel != null && QuickColorPalettePanel.Visibility == Visibility.Visible) ||
                    (QuickColorPaletteSingleRowPanel != null && QuickColorPaletteSingleRowPanel.Visibility == Visibility.Visible))
                {
                    // 根据显示模式调整宽度
                    if (Settings.Appearance.QuickColorPaletteDisplayMode == 0)
                    {
                        // 单行显示模式，自适应宽度，但需要足够空间显示6个颜色
                        floatingBarWidth = Math.Max(floatingBarWidth, 120 * ViewboxFloatingBarScaleTransform.ScaleX);
                    }
                    else
                    {
                        // 双行显示模式，宽度较大
                        floatingBarWidth = Math.Max(floatingBarWidth, 850 * ViewboxFloatingBarScaleTransform.ScaleX);
                    }
                }
                
                pos.X = (screenWidth - floatingBarWidth) / 2;

                // 如果任务栏高度为0(隐藏状态),则使用固定边距
                if (toolbarHeight == 0)
                {
                    pos.Y = screenHeight - ViewboxFloatingBar.ActualHeight * ViewboxFloatingBarScaleTransform.ScaleY -
                           3 * ViewboxFloatingBarScaleTransform.ScaleY;
                    LogHelper.WriteLogToFile($"任务栏隐藏,使用固定高度: {ViewboxFloatingBar.ActualHeight}");
                }
                else
                {
                    pos.Y = screenHeight - ViewboxFloatingBar.ActualHeight * ViewboxFloatingBarScaleTransform.ScaleY -
                           toolbarHeight - ViewboxFloatingBarScaleTransform.ScaleY * 3;
                }

                if (pointDesktop.X != -1 || pointDesktop.Y != -1) pointDesktop = pos;

                var marginAnimation = new ThicknessAnimation
                {
                    Duration = TimeSpan.FromSeconds(0.35),
                    From = ViewboxFloatingBar.Margin,
                    To = new Thickness(pos.X, pos.Y, 0, -20)
                };
                marginAnimation.EasingFunction = new CircleEase();
                ViewboxFloatingBar.BeginAnimation(MarginProperty, marginAnimation);
            });

            await Task.Delay(349);

            await Dispatcher.InvokeAsync(() =>
            {
                ViewboxFloatingBar.Margin = new Thickness(pos.X, pos.Y, -2000, -200);
            });
        }

        public async void PureViewboxFloatingBarMarginAnimationInPPTMode()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                ViewboxFloatingBar.Visibility = Visibility.Visible;
                isViewboxFloatingBarMarginAnimationRunning = true;

                double dpiScaleX = 1, dpiScaleY = 1;
                var source = PresentationSource.FromVisual(this);
                if (source != null)
                {
                    dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                    dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
                }

                var windowHandle = new WindowInteropHelper(this).Handle;
                var screen = Screen.FromHandle(windowHandle);
                double screenWidth = screen.Bounds.Width / dpiScaleX, screenHeight = screen.Bounds.Height / dpiScaleY;
                // 仅计算Windows任务栏高度，不考虑其他程序对工作区的影响
                var toolbarHeight = ForegroundWindowInfo.GetTaskbarHeight(screen, dpiScaleY);
                
                // 计算浮动栏位置，考虑快捷调色盘的显示状态
                double floatingBarWidth = ViewboxFloatingBar.ActualWidth * ViewboxFloatingBarScaleTransform.ScaleX;
                
                // 如果快捷调色盘显示，确保有足够空间
                if ((QuickColorPalettePanel != null && QuickColorPalettePanel.Visibility == Visibility.Visible) ||
                    (QuickColorPaletteSingleRowPanel != null && QuickColorPaletteSingleRowPanel.Visibility == Visibility.Visible))
                {
                    // 根据显示模式调整宽度
                    if (Settings.Appearance.QuickColorPaletteDisplayMode == 0)
                    {
                        // 单行显示模式，自适应宽度，但需要足够空间显示6个颜色
                        floatingBarWidth = Math.Max(floatingBarWidth, 120 * ViewboxFloatingBarScaleTransform.ScaleX);
                    }
                    else
                    {
                        // 双行显示模式，宽度较大
                        floatingBarWidth = Math.Max(floatingBarWidth, 820 * ViewboxFloatingBarScaleTransform.ScaleX);
                    }
                }
                
                pos.X = (screenWidth - floatingBarWidth) / 2;

                pos.Y = screenHeight - 55 * ViewboxFloatingBarScaleTransform.ScaleY;

                if (pointPPT.X != -1 || pointPPT.Y != -1)
                {
                    pointPPT = pos;
                }

                var marginAnimation = new ThicknessAnimation
                {
                    Duration = TimeSpan.FromSeconds(0.35),
                    From = ViewboxFloatingBar.Margin,
                    To = new Thickness(pos.X, pos.Y, 0, -20)
                };
                marginAnimation.EasingFunction = new CircleEase();
                ViewboxFloatingBar.BeginAnimation(MarginProperty, marginAnimation);
            });

            await Task.Delay(349);

            await Dispatcher.InvokeAsync(() =>
            {
                ViewboxFloatingBar.Margin = new Thickness(pos.X, pos.Y, -2000, -200);
            });
        }

        internal async void CursorIcon_Click(object sender, RoutedEventArgs e)
        {
            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == Cursor_Icon && lastBorderMouseDownObject != Cursor_Icon) return;

            // 禁用高级橡皮擦系统
            DisableAdvancedEraserSystem();

            // 隱藏高亮
            HideFloatingBarHighlight();

            // 切换前自动截图保存墨迹
            if (inkCanvas.Strokes.Count > 0 &&
                inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber)
            {
                if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
                {
                    var currentSlide = _pptManager?.GetCurrentSlideNumber() ?? 0;
                    var presentationName = _pptManager?.GetPresentationName() ?? "";
                    SaveScreenShot(true, $"{presentationName}/{currentSlide}_{DateTime.Now:HH-mm-ss}");
                }
                else SaveScreenShot(true);
            }

            if (BtnPPTSlideShowEnd.Visibility != Visibility.Visible)
            {
                if (Settings.Canvas.HideStrokeWhenSelecting)
                {
                    inkCanvas.Visibility = Visibility.Collapsed;
                }
                else
                {
                    inkCanvas.IsHitTestVisible = false;
                    inkCanvas.Visibility = Visibility.Visible;
                }
            }
            else
            {
                if (Settings.PowerPointSettings.IsShowStrokeOnSelectInPowerPoint)
                {
                    inkCanvas.Visibility = Visibility.Visible;
                    inkCanvas.IsHitTestVisible = true;
                }
                else
                {
                    if (Settings.Canvas.HideStrokeWhenSelecting)
                    {
                        inkCanvas.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        inkCanvas.IsHitTestVisible = false;
                        inkCanvas.Visibility = Visibility.Visible;
                    }
                }
            }

            GridTransparencyFakeBackground.Opacity = 0;
            GridTransparencyFakeBackground.Background = Brushes.Transparent;

            GridBackgroundCoverHolder.Visibility = Visibility.Collapsed;
            inkCanvas.Select(new StrokeCollection());
            GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

            if (currentMode != 0)
            {
                SaveStrokes();
                RestoreStrokes(true);
                LogHelper.WriteLogToFile($"退出白板模式，恢复备份墨迹。当前模式：{(BtnPPTSlideShowEnd.Visibility == Visibility.Visible ? "PPT放映" : "桌面")}", LogHelper.LogType.Trace);
            }

            if (BtnSwitchTheme.Content.ToString() == "浅色")
                BtnSwitch.Content = "黑板";
            else
                BtnSwitch.Content = "白板";

            StackPanelPPTButtons.Visibility = Visibility.Visible;
            BtnHideInkCanvas.Content = "显示\n画板";
            CheckEnableTwoFingerGestureBtnVisibility(false);


            StackPanelCanvasControls.Visibility = Visibility.Collapsed;
            
            // 在鼠标模式下隐藏快捷调色盘
            if (QuickColorPalettePanel != null)
            {
                QuickColorPalettePanel.Visibility = Visibility.Collapsed;
            }
            if (QuickColorPaletteSingleRowPanel != null)
            {
                QuickColorPaletteSingleRowPanel.Visibility = Visibility.Collapsed;
            }

            if (!isFloatingBarFolded)
            {
                HideSubPanels("cursor", true);
                await Task.Delay(50);

                if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
                    ViewboxFloatingBarMarginAnimation(60);
                else
                    ViewboxFloatingBarMarginAnimation(100, true);
            }
        }

        internal void PenIcon_Click(object sender, RoutedEventArgs e)
        {

            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == Pen_Icon && lastBorderMouseDownObject != Pen_Icon) return;

            // 禁用高级橡皮擦系统
            DisableAdvancedEraserSystem();

            // 修复：从橡皮擦切换到批注模式时，退出多指书写模式
            // 这解决了从橡皮擦切换为批注时被锁定为多指书写的问题
            ExitMultiTouchModeIfNeeded();

            SetFloatingBarHighlightPosition("pen");

            // 记录当前是否已经是批注模式且是否为高光显示模式
            bool wasInInkMode = inkCanvas.EditingMode == InkCanvasEditingMode.Ink;
            bool wasHighlighter = drawingAttributes.IsHighlighter;

            // 禁止几何绘制模式下切换到Ink
            if (drawingShapeMode != 0)
            {
                return;
            }

            if (Pen_Icon.Background == null || StackPanelCanvasControls.Visibility == Visibility.Collapsed)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;

                GridTransparencyFakeBackground.Opacity = 1;
                GridTransparencyFakeBackground.Background = new SolidColorBrush(StringToColor("#01FFFFFF"));

                inkCanvas.IsHitTestVisible = true;
                inkCanvas.Visibility = Visibility.Visible;

                GridBackgroundCoverHolder.Visibility = Visibility.Visible;
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

                /*if (forceEraser && currentMode == 0)
                    BtnColorRed_Click(sender, null);*/

                if (GridBackgroundCover.Visibility == Visibility.Collapsed)
                {
                    if (BtnSwitchTheme.Content.ToString() == "浅色")
                        BtnSwitch.Content = "黑板";
                    else
                        BtnSwitch.Content = "白板";
                    StackPanelPPTButtons.Visibility = Visibility.Visible;
                }
                else
                {
                    BtnSwitch.Content = "屏幕";
                    StackPanelPPTButtons.Visibility = Visibility.Collapsed;
                }

                BtnHideInkCanvas.Content = "隐藏\n画板";

                StackPanelCanvasControls.Visibility = Visibility.Visible;
                //AnimationsHelper.ShowWithSlideFromLeftAndFade(StackPanelCanvasControls);
                CheckEnableTwoFingerGestureBtnVisibility(true);
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                
                // 在批注模式下显示快捷调色盘（如果设置中启用了）
                if (Settings.Appearance.IsShowQuickColorPalette && QuickColorPalettePanel != null && QuickColorPaletteSingleRowPanel != null)
                {
                    // 根据显示模式选择显示哪个面板
                    if (Settings.Appearance.QuickColorPaletteDisplayMode == 0)
                    {
                        // 单行显示模式
                        QuickColorPalettePanel.Visibility = Visibility.Collapsed;
                        QuickColorPaletteSingleRowPanel.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        // 双行显示模式
                        QuickColorPalettePanel.Visibility = Visibility.Visible;
                        QuickColorPaletteSingleRowPanel.Visibility = Visibility.Collapsed;
                    }
                }

                // 修复：从线擦切换到批注时，保持之前的笔类型状态
                // 如果之前是荧光笔模式，则保持荧光笔状态；否则重置为默认笔模式
                forceEraser = false;
                forcePointEraser = false;
                drawingShapeMode = 0;
                
                // 保持之前的笔类型状态，而不是强制重置
                if (!wasHighlighter)
                {
                    penType = 0;
                    drawingAttributes.IsHighlighter = false;
                    drawingAttributes.StylusTip = StylusTip.Ellipse;
                }
                // 如果之前是荧光笔模式，则保持荧光笔属性
                else if (penType == 1)
                {
                    drawingAttributes.IsHighlighter = true;
                    drawingAttributes.StylusTip = StylusTip.Rectangle;
                    drawingAttributes.Width = Settings.Canvas.HighlighterWidth / 2;
                    drawingAttributes.Height = Settings.Canvas.HighlighterWidth;
                }

                ColorSwitchCheck();
                HideSubPanels("pen", true);
            }
            else
            {
                if (wasInInkMode)
                {
                    // 修复：从线擦切换到批注时，确保正确重置状态
                    if (forceEraser)
                    {
                        // 从橡皮擦模式切换过来，保持之前的笔类型状态
                        forceEraser = false;
                        forcePointEraser = false;
                        drawingShapeMode = 0;
                        
                        // 保持之前的笔类型状态，而不是强制重置
                        if (!wasHighlighter)
                        {
                            penType = 0;
                            drawingAttributes.IsHighlighter = false;
                            drawingAttributes.StylusTip = StylusTip.Ellipse;
                        }
                        // 如果之前是荧光笔模式，则保持荧光笔属性
                        else if (penType == 1)
                        {
                            drawingAttributes.IsHighlighter = true;
                            drawingAttributes.StylusTip = StylusTip.Rectangle;
                            drawingAttributes.Width = Settings.Canvas.HighlighterWidth / 2;
                            drawingAttributes.Height = Settings.Canvas.HighlighterWidth;
                        }

                        // 在非白板模式下，从线擦切换到批注时不直接弹出子面板
                        if (currentMode != 1)
                        {
                            HideSubPanels("pen", true);
                            return;
                        }
                    }

                    if (PenPalette.Visibility == Visibility.Visible)
                    {
                        AnimationsHelper.HideWithSlideAndFade(EraserSizePanel);
                        AnimationsHelper.HideWithSlideAndFade(BorderTools);
                        AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
                        AnimationsHelper.HideWithSlideAndFade(PenPalette);
                        AnimationsHelper.HideWithSlideAndFade(BoardPenPalette);
                        AnimationsHelper.HideWithSlideAndFade(BorderDrawShape);
                        AnimationsHelper.HideWithSlideAndFade(BoardBorderDrawShape);
                        AnimationsHelper.HideWithSlideAndFade(BoardEraserSizePanel);
                        AnimationsHelper.HideWithSlideAndFade(BorderTools);
                        AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
                        AnimationsHelper.HideWithSlideAndFade(TwoFingerGestureBorder);
                        AnimationsHelper.HideWithSlideAndFade(BoardTwoFingerGestureBorder);
                        AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);
                    }
                    else
                    {
                        AnimationsHelper.HideWithSlideAndFade(EraserSizePanel);
                        AnimationsHelper.HideWithSlideAndFade(BorderTools);
                        AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
                        AnimationsHelper.HideWithSlideAndFade(BorderDrawShape);
                        AnimationsHelper.HideWithSlideAndFade(BoardBorderDrawShape);
                        AnimationsHelper.HideWithSlideAndFade(BoardEraserSizePanel);
                        AnimationsHelper.HideWithSlideAndFade(BorderTools);
                        AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
                        AnimationsHelper.HideWithSlideAndFade(TwoFingerGestureBorder);
                        AnimationsHelper.HideWithSlideAndFade(BoardTwoFingerGestureBorder);
                        AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);
                        AnimationsHelper.ShowWithSlideFromBottomAndFade(PenPalette);
                        AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardPenPalette);
                    }
                }
                else
                {
                    // 切换到批注模式时，确保保存当前图片信息
                    if (currentMode != 0)
                    {
                        SaveStrokes();
                    }
                    inkCanvas.EditingMode = InkCanvasEditingMode.Ink;

                    // 修复：从线擦切换到批注时，保持之前的笔类型状态
                    forceEraser = false;
                    forcePointEraser = false;
                    drawingShapeMode = 0;
                    
                    // 保持之前的笔类型状态，而不是强制重置
                    if (!wasHighlighter)
                    {
                        penType = 0;
                        drawingAttributes.IsHighlighter = false;
                        drawingAttributes.StylusTip = StylusTip.Ellipse;
                    }
                    // 如果之前是荧光笔模式，则保持荧光笔属性
                    else if (penType == 1)
                    {
                        drawingAttributes.IsHighlighter = true;
                        drawingAttributes.StylusTip = StylusTip.Rectangle;
                        drawingAttributes.Width = Settings.Canvas.HighlighterWidth / 2;
                        drawingAttributes.Height = Settings.Canvas.HighlighterWidth;
                    }

                    ColorSwitchCheck();
                    HideSubPanels("pen", true);
                }
            }
            
            
            // 延迟半秒后再刷新快捷键状态
            Task.Delay(500).ContinueWith(_ => Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => RefreshHotkeyState())));

            // 修复：从线擦切换到批注时，保持之前的笔类型状态
            forceEraser = false;
            forcePointEraser = false;
            drawingShapeMode = 0;
        }

        private void ColorThemeSwitch_MouseUp(object sender, RoutedEventArgs e)
        {
            isUselightThemeColor = !isUselightThemeColor;
            if (currentMode == 0) isDesktopUselightThemeColor = isUselightThemeColor;
            CheckColorTheme();
        }

        internal void EraserIcon_Click(object sender, RoutedEventArgs e)
        {
            EnterMultiTouchModeIfNeeded();
            bool isAlreadyEraser = inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint;
            forceEraser = false;
            forcePointEraser = true;
            drawingShapeMode = 0;

            // 切换到橡皮擦模式时，确保保存当前图片信息
            if (!isAlreadyEraser && currentMode != 0)
            {
                SaveStrokes();
            }

            // 启用新的高级橡皮擦系统
            EnableAdvancedEraserSystem();

            // 使用新的高级橡皮擦系统
            inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
            ApplyAdvancedEraserShape(); // 使用新的橡皮擦形状应用方法
            SetCursorBasedOnEditingMode(inkCanvas);
            HideSubPanels("eraser"); // 高亮橡皮按钮

            // 显示橡皮擦视觉反馈（用于测试）
            // 注意：eraserVisualBorder在MW_Eraser.cs中定义，这里无法直接访问
            Trace.WriteLine($"Advanced Eraser: Eraser button clicked, current size: {currentEraserSize}, circle: {isCurrentEraserCircle}");

            if (isAlreadyEraser)
            {
                // 已是橡皮状态，再次点击才弹出/收起面板
                if (EraserSizePanel.Visibility == Visibility.Collapsed)
                {
                    AnimationsHelper.ShowWithSlideFromBottomAndFade(EraserSizePanel);
                    if (BoardEraserSizePanel != null)
                        AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardEraserSizePanel);
                }
                else
                {
                    AnimationsHelper.HideWithSlideAndFade(EraserSizePanel);
                    if (BoardEraserSizePanel != null)
                        AnimationsHelper.HideWithSlideAndFade(BoardEraserSizePanel);
                }
            }
        }

        private void BoardEraserIcon_Click(object sender, RoutedEventArgs e)
        {
            EnterMultiTouchModeIfNeeded();
            bool isAlreadyEraser = inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint;
            forceEraser = false;
            forcePointEraser = true;
            drawingShapeMode = 0;

            // 启用新的高级橡皮擦系统
            EnableAdvancedEraserSystem();

            // 使用新的高级橡皮擦系统
            inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
            ApplyAdvancedEraserShape(); // 使用新的橡皮擦形状应用方法
            SetCursorBasedOnEditingMode(inkCanvas);
            HideSubPanels("eraser"); // 高亮橡皮按钮

            if (isAlreadyEraser)
            {
                // 已是橡皮状态，再次点击才弹出/收起面板
                if (BoardEraserSizePanel != null && BoardEraserSizePanel.Visibility == Visibility.Collapsed)
                {
                    AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardEraserSizePanel);
                    AnimationsHelper.ShowWithSlideFromBottomAndFade(EraserSizePanel);
                }
                else
                {
                    if (BoardEraserSizePanel != null)
                        AnimationsHelper.HideWithSlideAndFade(BoardEraserSizePanel);
                    AnimationsHelper.HideWithSlideAndFade(EraserSizePanel);
                }
            }
        }

        private void EraserIconByStrokes_Click(object sender, RoutedEventArgs e)
        {
            EnterMultiTouchModeIfNeeded();

            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == EraserByStrokes_Icon && lastBorderMouseDownObject != EraserByStrokes_Icon) return;

            // 禁用高级橡皮擦系统
            DisableAdvancedEraserSystem();

            forceEraser = true;
            forcePointEraser = false;

            inkCanvas.EraserShape = new EllipseStylusShape(5, 5);
            inkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
            drawingShapeMode = 0;

            // 修复：切换到线擦时，保存当前的笔类型状态，而不是强制重置
            // 这样从线擦切换回批注时，可以恢复之前的荧光笔状态
            // penType 和 drawingAttributes 的状态将在 PenIcon_Click 中根据 wasHighlighter 来恢复

            inkCanvas_EditingModeChanged(inkCanvas, null);
            CancelSingleFingerDragMode();

            HideSubPanels("eraserByStrokes");

        }

        private void CursorWithDelIcon_Click(object sender, RoutedEventArgs e)
        {

            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == CursorWithDelFloatingBarBtn && lastBorderMouseDownObject != CursorWithDelFloatingBarBtn) return;

            SymbolIconDelete_MouseUp(sender, null);
            CursorIcon_Click(null, null);
        }

        // 快捷调色盘事件处理方法
        private void QuickColorWhite_Click(object sender, RoutedEventArgs e)
        {
            SetQuickColor(Colors.White);
        }

        private void QuickColorOrange_Click(object sender, RoutedEventArgs e)
        {
                            SetQuickColor(Color.FromRgb(251, 150, 80)); // 橙色
        }

        private void QuickColorYellow_Click(object sender, RoutedEventArgs e)
        {
            SetQuickColor(Colors.Yellow);
        }

        private void QuickColorBlack_Click(object sender, RoutedEventArgs e)
        {
            SetQuickColor(Colors.Black);
        }

        private void QuickColorBlue_Click(object sender, RoutedEventArgs e)
        {
            SetQuickColor(Color.FromRgb(37, 99, 235)); // 蓝色
        }

        private void QuickColorRed_Click(object sender, RoutedEventArgs e)
        {
            SetQuickColor(Colors.Red);
        }

                    private void QuickColorGreen_Click(object sender, RoutedEventArgs e)
        {
            SetQuickColor(Color.FromRgb(22, 163, 74));
        }

        private void QuickColorPurple_Click(object sender, RoutedEventArgs e)
        {
            SetQuickColor(Color.FromRgb(147, 51, 234)); 
        }

        private void SetQuickColor(Color color)
        {
            // 确保当前处于批注模式
            if (inkCanvas.EditingMode != InkCanvasEditingMode.Ink)
            {
                PenIcon_Click(null, null);
            }

            // 设置画笔颜色
            drawingAttributes.Color = color;
            inkCanvas.DefaultDrawingAttributes.Color = color;

            // 如果当前是荧光笔模式，同时更新荧光笔颜色和属性
            if (penType == 1)
            {
                // 根据颜色设置对应的荧光笔颜色索引
                if (color == Colors.White || IsColorSimilar(color, Color.FromRgb(250, 250, 250), 10))
                {
                    highlighterColor = 101; // 白色荧光笔
                }
                else if (color == Colors.Black)
                {
                    highlighterColor = 100; // 黑色荧光笔
                }
                else if (color == Colors.Yellow || IsColorSimilar(color, Color.FromRgb(234, 179, 8), 15) ||
                         IsColorSimilar(color, Color.FromRgb(250, 204, 21), 15) ||
                         IsColorSimilar(color, Color.FromRgb(253, 224, 71), 15))
                {
                    highlighterColor = 103; // 黄色荧光笔
                }
                else if (color == Color.FromRgb(255, 165, 0) || color == Color.FromRgb(251, 150, 80) || IsColorSimilar(color, Color.FromRgb(249, 115, 22), 20) ||
                         IsColorSimilar(color, Color.FromRgb(234, 88, 12), 20) ||
                         IsColorSimilar(color, Color.FromRgb(251, 146, 60), 20) ||
                         IsColorSimilar(color, Color.FromRgb(253, 126, 20), 20))
                {
                    highlighterColor = 109; // 橙色荧光笔
                }
                else if (color == Color.FromRgb(37, 99, 235))
                {
                    highlighterColor = 106; // 蓝色荧光笔
                }
                else if (color == Colors.Red || IsColorSimilar(color, Color.FromRgb(220, 38, 38), 15) ||
                         IsColorSimilar(color, Color.FromRgb(239, 68, 68), 15))
                {
                    highlighterColor = 102; // 红色荧光笔
                }
                else if (color == Colors.Green || IsColorSimilar(color, Color.FromRgb(22, 163, 74), 15))
                {
                    highlighterColor = 104; // 绿色荧光笔
                }
                else if (color == Color.FromRgb(147, 51, 234))
                {
                    highlighterColor = 107; // 紫色荧光笔
                }

                // 确保荧光笔属性正确设置
                drawingAttributes.Width = Settings.Canvas.HighlighterWidth / 2;
                drawingAttributes.Height = Settings.Canvas.HighlighterWidth;
                drawingAttributes.StylusTip = StylusTip.Rectangle;
                drawingAttributes.IsHighlighter = true;
                
                inkCanvas.DefaultDrawingAttributes.Width = Settings.Canvas.HighlighterWidth / 2;
                inkCanvas.DefaultDrawingAttributes.Height = Settings.Canvas.HighlighterWidth;
                inkCanvas.DefaultDrawingAttributes.StylusTip = StylusTip.Rectangle;
                inkCanvas.DefaultDrawingAttributes.IsHighlighter = true;
                
                // 确保荧光笔颜色索引正确更新
                inkCanvas.DefaultDrawingAttributes.Color = drawingAttributes.Color;
            }

            // 更新颜色状态
            if (currentMode == 0)
            {
                // 桌面模式
                if (color == Colors.White) lastDesktopInkColor = 5;
                else if (color == Color.FromRgb(251, 150, 80)) lastDesktopInkColor = 8; // 橙色
                else if (color == Colors.Yellow) lastDesktopInkColor = 4;
                else if (color == Colors.Black) lastDesktopInkColor = 0;
                else if (color == Color.FromRgb(37, 99, 235)) lastDesktopInkColor = 3; // 蓝色
                else if (color == Colors.Red) lastDesktopInkColor = 1;
                else if (color == Colors.Green || color == Color.FromRgb(22, 163, 74)) lastDesktopInkColor = 2; 
                else if (color == Color.FromRgb(147, 51, 234)) lastDesktopInkColor = 6; // 紫色
            }
            else
            {
                // 白板模式
                if (color == Colors.White) lastBoardInkColor = 5;
                else if (color == Color.FromRgb(251, 150, 80)) lastBoardInkColor = 8; // 橙色
                else if (color == Colors.Yellow) lastBoardInkColor = 4;
                else if (color == Colors.Black) lastBoardInkColor = 0;
                else if (color == Color.FromRgb(37, 99, 235)) lastBoardInkColor = 3; // 蓝色
                else if (color == Colors.Red) lastBoardInkColor = 1;
                else if (color == Colors.Green || color == Color.FromRgb(22, 163, 74)) lastBoardInkColor = 2; 
                else if (color == Color.FromRgb(147, 51, 234)) lastBoardInkColor = 6; // 紫色
            }

            // 更新快捷调色盘选择指示器
            UpdateQuickColorPaletteIndicator(color);

            // 更新颜色显示
            ColorSwitchCheck();
            
            // 如果当前是荧光笔模式，调用ColorSwitchCheck确保颜色索引正确更新
            if (penType == 1)
            {
                ColorSwitchCheck();
            }
        }

        private void UpdateQuickColorPaletteIndicator(Color selectedColor)
        {
            // 隐藏所有check图标（双行显示）
            QuickColorWhiteCheck.Visibility = Visibility.Collapsed;
            QuickColorOrangeCheck.Visibility = Visibility.Collapsed;
            QuickColorYellowCheck.Visibility = Visibility.Collapsed;
            QuickColorBlackCheck.Visibility = Visibility.Collapsed;
            QuickColorBlueCheck.Visibility = Visibility.Collapsed;
            QuickColorRedCheck.Visibility = Visibility.Collapsed;
            QuickColorGreenCheck.Visibility = Visibility.Collapsed;
            QuickColorPurpleCheck.Visibility = Visibility.Collapsed;
            
            // 隐藏所有check图标（单行显示）
            QuickColorWhiteCheckSingle.Visibility = Visibility.Collapsed;
            QuickColorOrangeCheckSingle.Visibility = Visibility.Collapsed;
            QuickColorYellowCheckSingle.Visibility = Visibility.Collapsed;
            QuickColorBlackCheckSingle.Visibility = Visibility.Collapsed;
            QuickColorRedCheckSingle.Visibility = Visibility.Collapsed;
            QuickColorGreenCheckSingle.Visibility = Visibility.Collapsed;

            // 显示当前选中颜色的check图标
            // 在荧光笔模式下，使用更宽松的颜色匹配
            int tolerance = (penType == 1) ? 25 : 15; // 荧光笔模式使用更大的容差
            
            if (IsColorSimilar(selectedColor, Colors.White, tolerance) || IsColorSimilar(selectedColor, Color.FromRgb(250, 250, 250), tolerance))
            {
                QuickColorWhiteCheck.Visibility = Visibility.Visible;
                QuickColorWhiteCheckSingle.Visibility = Visibility.Visible;
            }
            else if (IsColorSimilar(selectedColor, Colors.Black, tolerance))
            {
                QuickColorBlackCheck.Visibility = Visibility.Visible;
                QuickColorBlackCheckSingle.Visibility = Visibility.Visible;
            }
            else if (IsColorSimilar(selectedColor, Colors.Yellow, tolerance) || 
                     IsColorSimilar(selectedColor, Color.FromRgb(234, 179, 8), tolerance) ||
                     IsColorSimilar(selectedColor, Color.FromRgb(250, 204, 21), tolerance) ||
                     IsColorSimilar(selectedColor, Color.FromRgb(253, 224, 71), tolerance))
            {
                QuickColorYellowCheck.Visibility = Visibility.Visible;
                QuickColorYellowCheckSingle.Visibility = Visibility.Visible;
            }
            else if (IsColorSimilar(selectedColor, Color.FromRgb(255, 165, 0), tolerance) || 
                     IsColorSimilar(selectedColor, Color.FromRgb(251, 150, 80), tolerance) ||
                     IsColorSimilar(selectedColor, Color.FromRgb(249, 115, 22), tolerance) ||
                     IsColorSimilar(selectedColor, Color.FromRgb(234, 88, 12), tolerance) ||
                     IsColorSimilar(selectedColor, Color.FromRgb(251, 146, 60), tolerance) ||
                     IsColorSimilar(selectedColor, Color.FromRgb(253, 126, 20), tolerance))
            {
                QuickColorOrangeCheck.Visibility = Visibility.Visible;
                QuickColorOrangeCheckSingle.Visibility = Visibility.Visible;
            }
            else if (IsColorSimilar(selectedColor, Color.FromRgb(37, 99, 235), tolerance))
            {
                QuickColorBlueCheck.Visibility = Visibility.Visible;
                // 单行显示模式没有蓝色，所以不设置单行的check
            }
            else if (IsColorSimilar(selectedColor, Colors.Red, tolerance) || 
                     IsColorSimilar(selectedColor, Color.FromRgb(220, 38, 38), tolerance) ||
                     IsColorSimilar(selectedColor, Color.FromRgb(239, 68, 68), tolerance))
            {
                QuickColorRedCheck.Visibility = Visibility.Visible;
                QuickColorRedCheckSingle.Visibility = Visibility.Visible;
            }
            else if (IsColorSimilar(selectedColor, Color.FromRgb(22, 163, 74), tolerance))
            {
                QuickColorGreenCheck.Visibility = Visibility.Visible;
                QuickColorGreenCheckSingle.Visibility = Visibility.Visible;
            }
            else if (IsColorSimilar(selectedColor, Color.FromRgb(147, 51, 234), tolerance))
            {
                QuickColorPurpleCheck.Visibility = Visibility.Visible;
                // 单行显示模式没有紫色，所以不设置单行的check
            }
        }

        /// <summary>
        /// 检查两个颜色是否相似（允许一定的误差范围）
        /// </summary>
        private bool IsColorSimilar(Color color1, Color color2, int tolerance = 15)
        {
            int rDiff = Math.Abs(color1.R - color2.R);
            int gDiff = Math.Abs(color1.G - color2.G);
            int bDiff = Math.Abs(color1.B - color2.B);
            
            return rDiff <= tolerance && gDiff <= tolerance && bDiff <= tolerance;
        }

        private void SelectIcon_MouseUp(object sender, RoutedEventArgs e)
        {
            // 禁用高级橡皮擦系统
            DisableAdvancedEraserSystem();

            forceEraser = true;
            drawingShapeMode = 0;
            inkCanvas.IsManipulationEnabled = false;
            if (inkCanvas.EditingMode == InkCanvasEditingMode.Select)
            {
                var selectedStrokes = new StrokeCollection();
                foreach (var stroke in inkCanvas.Strokes)
                    if (stroke.GetBounds().Width > 0 && stroke.GetBounds().Height > 0)
                        selectedStrokes.Add(stroke);
                inkCanvas.Select(selectedStrokes);
            }
            else
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.Select;
            }
        }

        private void DrawShapePromptToPen()
        {
            if (isLongPressSelected)
            {
                // 如果是长按选中的状态，只隐藏面板，不切换到笔模式
                HideSubPanels("shape");
            }
            else
            {
                if (StackPanelCanvasControls.Visibility == Visibility.Visible)
                    HideSubPanels("pen");
                else
                    HideSubPanels("cursor");
            }
        }

        private void CloseBordertools_MouseUp(object sender, MouseButtonEventArgs e)
        {
            HideSubPanels();
        }

        #region Left Side Panel

        private void BtnFingerDragMode_Click(object sender, RoutedEventArgs e)
        {
            if (isSingleFingerDragMode)
            {
                isSingleFingerDragMode = false;
                BtnFingerDragMode.Content = "单指\n拖动";
            }
            else
            {
                isSingleFingerDragMode = true;
                BtnFingerDragMode.Content = "多指\n拖动";
            }
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            if (inkCanvas.GetSelectedStrokes().Count != 0)
            {
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
                inkCanvas.Select(new StrokeCollection());
            }

            var item = timeMachine.Undo();
            ApplyHistoryToCanvas(item);
        }

        private void BtnRedo_Click(object sender, RoutedEventArgs e)
        {
            if (inkCanvas.GetSelectedStrokes().Count != 0)
            {
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
                inkCanvas.Select(new StrokeCollection());
            }

            var item = timeMachine.Redo();
            ApplyHistoryToCanvas(item);
        }

        private void Btn_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!isLoaded) return;
            try
            {
                if (((Button)sender).IsEnabled)
                    ((UIElement)((Button)sender).Content).Opacity = 1;
                else
                    ((UIElement)((Button)sender).Content).Opacity = 0.25;
            }
            catch { }
        }

        #endregion Left Side Panel

        #region Right Side Panel

        public static bool CloseIsFromButton;

        public void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            App.IsAppExitByUser = true;
            // 不设置 CloseIsFromButton = true，让它也经过确认流程
            Close();
        }

        public void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(System.Windows.Forms.Application.ExecutablePath, "-m");
            App.IsAppExitByUser = true;
            // 不设置 CloseIsFromButton = true，让它也经过确认流程
            Close();
        }

        private void SettingsOverlayClick(object sender, MouseButtonEventArgs e)
        {
            if (isOpeningOrHidingSettingsPane) return;

            // 获取点击的位置
            Point clickPoint = e.GetPosition(BorderSettingsMask);

            // 获取BorderSettings的位置和大小
            Point settingsPosition = BorderSettings.TranslatePoint(new Point(0, 0), BorderSettingsMask);
            Rect settingsRect = new Rect(
                settingsPosition.X,
                settingsPosition.Y,
                BorderSettings.ActualWidth,
                BorderSettings.ActualHeight
            );

            // 如果点击位置不在设置界面内部，才关闭设置界面
            if (!settingsRect.Contains(clickPoint))
            {
                BtnSettings_Click(null, null);
            }
        }

        private bool isOpeningOrHidingSettingsPane;

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (BorderSettings.Visibility == Visibility.Visible)
            {
                HideSubPanels();
            }
            else
            {
                // 设置蒙版为可点击，并添加半透明背景
                BorderSettingsMask.IsHitTestVisible = true;
                BorderSettingsMask.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
                SettingsPanelScrollViewer.ScrollToTop();
                var sb = new Storyboard();

                // 滑动动画
                var slideAnimation = new DoubleAnimation
                {
                    From = BorderSettings.RenderTransform.Value.OffsetX - 440, // 滑动距离
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.6)
                };
                slideAnimation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                Storyboard.SetTargetProperty(slideAnimation,
                    new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

                sb.Children.Add(slideAnimation);

                sb.Completed += (s, _) => { isOpeningOrHidingSettingsPane = false; };

                BorderSettings.Visibility = Visibility.Visible;
                BorderSettings.RenderTransform = new TranslateTransform();

                isOpeningOrHidingSettingsPane = true;
                sb.Begin(BorderSettings);
            }
        }

        private void BtnThickness_Click(object sender, RoutedEventArgs e) { }

        private bool forceEraser;


        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            forceEraser = false;
            //BorderClearInDelete.Visibility = Visibility.Collapsed;

            if (currentMode == 0)
            {
                // 先回到画笔再清屏，避免 TimeMachine 的相关 bug 影响
                if (Pen_Icon.Background == null && StackPanelCanvasControls.Visibility == Visibility.Visible)
                    PenIcon_Click(null, null);
            }
            else
            {
                if (Pen_Icon.Background == null) PenIcon_Click(null, null);
            }

            if (inkCanvas.Strokes.Count != 0)
            {
                var whiteboardIndex = CurrentWhiteboardIndex;
                if (currentMode == 0) whiteboardIndex = 0;
                strokeCollections[whiteboardIndex] = inkCanvas.Strokes.Clone();
            }

            ClearStrokes(false);
            // 保存非笔画元素（如图片）
            var preservedElements = PreserveNonStrokeElements();
            inkCanvas.Children.Clear();
            // 恢复非笔画元素
            RestoreNonStrokeElements(preservedElements);

            CancelSingleFingerDragMode();

            if (Settings.Canvas.ClearCanvasAndClearTimeMachine) timeMachine.ClearStrokeHistory();
        }

        private bool lastIsInMultiTouchMode;

        private void CancelSingleFingerDragMode()
        {
            if (ToggleSwitchDrawShapeBorderAutoHide.IsOn) CollapseBorderDrawShape();

            GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

            if (isSingleFingerDragMode) BtnFingerDragMode_Click(BtnFingerDragMode, null);
            isLongPressSelected = false;
        }

        private void BtnHideControl_Click(object sender, RoutedEventArgs e)
        {
            if (StackPanelControl.Visibility == Visibility.Visible)
                StackPanelControl.Visibility = Visibility.Hidden;
            else
                StackPanelControl.Visibility = Visibility.Visible;
        }

        private int currentMode;

        private void BtnSwitch_Click(object sender, RoutedEventArgs e)
        {
            if (GridTransparencyFakeBackground.Background == Brushes.Transparent)
            {
                if (currentMode == 0)
                {
                    currentMode++;
                    GridBackgroundCover.Visibility = Visibility.Collapsed;
                    AnimationsHelper.HideWithSlideAndFade(BlackboardLeftSide);
                    AnimationsHelper.HideWithSlideAndFade(BlackboardCenterSide);
                    AnimationsHelper.HideWithSlideAndFade(BlackboardRightSide);

                    DeselectUIElement();

                    // 在PPT模式下隐藏手势面板和手势按钮
                    AnimationsHelper.HideWithSlideAndFade(TwoFingerGestureBorder);
                    AnimationsHelper.HideWithSlideAndFade(BoardTwoFingerGestureBorder);
                    EnableTwoFingerGestureBorder.Visibility = Visibility.Collapsed;

                    SaveStrokes(true);
                    ClearStrokes(true);
                    RestoreStrokes(true);


                    if (BtnSwitchTheme.Content.ToString() == "浅色")
                    {
                        BtnSwitch.Content = "黑板";
                        BtnExit.Foreground = Brushes.White;
                    }
                    else
                    {
                        BtnSwitch.Content = "白板";
                        if (isPresentationHaveBlackSpace)
                        {
                            BtnExit.Foreground = Brushes.White;
                            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                        }
                        else
                        {
                            BtnExit.Foreground = Brushes.Black;
                            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                        }
                    }

                    StackPanelPPTButtons.Visibility = Visibility.Visible;
                }

                Topmost = true;
                BtnHideInkCanvas_Click(BtnHideInkCanvas, e);
            }
            else
            {
                switch (++currentMode % 2)
                {
                    case 0: //屏幕模式
                        currentMode = 0;
                        GridBackgroundCover.Visibility = Visibility.Collapsed;
                        AnimationsHelper.HideWithSlideAndFade(BlackboardLeftSide);
                        AnimationsHelper.HideWithSlideAndFade(BlackboardCenterSide);
                        AnimationsHelper.HideWithSlideAndFade(BlackboardRightSide);

                        // 取消任何UI元素的选择
                        DeselectUIElement();

                        // 在PPT模式下隐藏手势面板和手势按钮
                        AnimationsHelper.HideWithSlideAndFade(TwoFingerGestureBorder);
                        AnimationsHelper.HideWithSlideAndFade(BoardTwoFingerGestureBorder);
                        EnableTwoFingerGestureBorder.Visibility = Visibility.Collapsed;

                        SaveStrokes();
                        ClearStrokes(true);
                        RestoreStrokes(true);

                        if (BtnSwitchTheme.Content.ToString() == "浅色")
                        {
                            BtnSwitch.Content = "黑板";
                            BtnExit.Foreground = Brushes.White;
                            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                        }
                        else
                        {
                            BtnSwitch.Content = "白板";
                            if (isPresentationHaveBlackSpace)
                            {
                                BtnExit.Foreground = Brushes.White;
                                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                            }
                            else
                            {
                                BtnExit.Foreground = Brushes.Black;
                                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                            }
                        }

                        StackPanelPPTButtons.Visibility = Visibility.Visible;
                        Topmost = true;
                        break;
                    case 1: //黑板或白板模式
                        currentMode = 1;
                        GridBackgroundCover.Visibility = Visibility.Visible;
                        AnimationsHelper.ShowWithSlideFromBottomAndFade(BlackboardLeftSide);
                        AnimationsHelper.ShowWithSlideFromBottomAndFade(BlackboardCenterSide);
                        AnimationsHelper.ShowWithSlideFromBottomAndFade(BlackboardRightSide);

                        // 取消任何UI元素的选择
                        DeselectUIElement();

                        SaveStrokes(true);
                        ClearStrokes(true);

                        // 总是恢复备份墨迹，不管是否在PPT模式
                        // PPT墨迹和白板墨迹应该分别管理，不应该互相影响
                        RestoreStrokes();

                        BtnSwitch.Content = "屏幕";
                        if (BtnSwitchTheme.Content.ToString() == "浅色")
                        {
                            BtnExit.Foreground = Brushes.White;
                            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                        }
                        else
                        {
                            BtnExit.Foreground = Brushes.Black;
                            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                        }

                        if (Settings.Canvas.UsingWhiteboard)
                        {
                            // 如果有自定义背景色并且是白板模式，应用自定义背景色
                            if (CustomBackgroundColor.HasValue)
                            {
                                GridBackgroundCover.Background = new SolidColorBrush(CustomBackgroundColor.Value);
                            }
                            // 白板模式下设置墨迹颜色为黑色
                            CheckLastColor(0);
                            forceEraser = false;
                            ColorSwitchCheck();
                        }
                        else
                        {
                            // 黑板模式下设置墨迹颜色为白色
                            CheckLastColor(5);
                            forceEraser = false;
                            ColorSwitchCheck();
                        }

                        StackPanelPPTButtons.Visibility = Visibility.Collapsed;
                        Topmost = false;
                        break;
                }
            }
        }

        private int BoundsWidth = 5;

        private void BtnHideInkCanvas_Click(object sender, RoutedEventArgs e)
        {
            if (GridTransparencyFakeBackground.Background == Brushes.Transparent)
            {
                GridTransparencyFakeBackground.Opacity = 1;
                GridTransparencyFakeBackground.Background = new SolidColorBrush(StringToColor("#01FFFFFF"));
                inkCanvas.IsHitTestVisible = true;
                inkCanvas.Visibility = Visibility.Visible;

                GridBackgroundCoverHolder.Visibility = Visibility.Visible;

                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

                if (GridBackgroundCover.Visibility == Visibility.Collapsed)
                {
                    if (BtnSwitchTheme.Content.ToString() == "浅色")
                        BtnSwitch.Content = "黑板";
                    else
                        BtnSwitch.Content = "白板";
                    StackPanelPPTButtons.Visibility = Visibility.Visible;
                }
                else
                {
                    BtnSwitch.Content = "屏幕";
                    StackPanelPPTButtons.Visibility = Visibility.Collapsed;
                }

                BtnHideInkCanvas.Content = "隐藏\n画板";
            }
            else
            {
                // Auto-clear Strokes 要等待截图完成再清理笔记
                if (BtnPPTSlideShowEnd.Visibility != Visibility.Visible)
                {
                    if (isLoaded && Settings.Automation.IsAutoClearWhenExitingWritingMode)
                        if (inkCanvas.Strokes.Count > 0)
                        {
                            if (Settings.Automation.IsAutoSaveStrokesAtClear && inkCanvas.Strokes.Count >
                                Settings.Automation.MinimumAutomationStrokeNumber)
                                SaveScreenShot(true);

                            //BtnClear_Click(null, null);
                        }

                    inkCanvas.IsHitTestVisible = true;
                    inkCanvas.Visibility = Visibility.Visible;
                }
                else
                {
                    if (isLoaded && Settings.Automation.IsAutoClearWhenExitingWritingMode &&
                        !Settings.PowerPointSettings.IsNoClearStrokeOnSelectWhenInPowerPoint)
                        if (inkCanvas.Strokes.Count > 0)
                        {
                            if (Settings.Automation.IsAutoSaveStrokesAtClear && inkCanvas.Strokes.Count >
                                Settings.Automation.MinimumAutomationStrokeNumber)
                                SaveScreenShot(true);

                            //BtnClear_Click(null, null);
                        }


                    if (Settings.PowerPointSettings.IsShowStrokeOnSelectInPowerPoint)
                    {
                        inkCanvas.Visibility = Visibility.Visible;
                        inkCanvas.IsHitTestVisible = true;
                    }
                    else
                    {
                        inkCanvas.IsHitTestVisible = true;
                        inkCanvas.Visibility = Visibility.Visible;
                    }
                }

                GridTransparencyFakeBackground.Opacity = 0;
                GridTransparencyFakeBackground.Background = Brushes.Transparent;

                GridBackgroundCoverHolder.Visibility = Visibility.Collapsed;

                if (currentMode != 0)
                {
                    SaveStrokes();
                    RestoreStrokes(true);
                }

                if (BtnSwitchTheme.Content.ToString() == "浅色")
                    BtnSwitch.Content = "黑板";
                else
                    BtnSwitch.Content = "白板";

                StackPanelPPTButtons.Visibility = Visibility.Visible;
                BtnHideInkCanvas.Content = "显示\n画板";
            }

            if (GridTransparencyFakeBackground.Background == Brushes.Transparent)
            {
                StackPanelCanvasControls.Visibility = Visibility.Collapsed;
                CheckEnableTwoFingerGestureBtnVisibility(false);
                HideSubPanels("cursor");
            }
            else
            {
                AnimationsHelper.ShowWithSlideFromLeftAndFade(StackPanelCanvasControls);
                CheckEnableTwoFingerGestureBtnVisibility(true);
            }
        }

        private void BtnSwitchSide_Click(object sender, RoutedEventArgs e)
        {
            if (ViewBoxStackPanelMain.HorizontalAlignment == HorizontalAlignment.Right)
            {
                ViewBoxStackPanelMain.HorizontalAlignment = HorizontalAlignment.Left;
                ViewBoxStackPanelShapes.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                ViewBoxStackPanelMain.HorizontalAlignment = HorizontalAlignment.Right;
                ViewBoxStackPanelShapes.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }

        private void StackPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (((StackPanel)sender).Visibility == Visibility.Visible)
                GridForLeftSideReservedSpace.Visibility = Visibility.Collapsed;
            else
                GridForLeftSideReservedSpace.Visibility = Visibility.Visible;
        }

        #endregion

        private void InsertImageOptions_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // Hide other sub-panels first
            HideSubPanelsImmediately();

            // Show the image options panel
            if (BoardImageOptionsPanel.Visibility == Visibility.Collapsed)
            {
                AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardImageOptionsPanel);
            }
            else
            {
                AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);
            }
        }

        private void CloseImageOptionsPanel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);
        }

        private async void ImageOptionScreenshot_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // Hide the options panel
            AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);

            // Wait a bit for the panel to hide
            await Task.Delay(100);

            // Capture screenshot and copy to clipboard
            await CaptureScreenshotToClipboard();
        }

        private async void ImageOptionSelectFile_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // Hide the options panel
            AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);

            // Open file dialog to select image
            var dialog = new OpenFileDialog
            {
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif"
            };
            if (dialog.ShowDialog() == true)
            {
                string filePath = dialog.FileName;
                Image image = await CreateAndCompressImageAsync(filePath);
                if (image != null)
                {
                    string timestamp = "img_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
                    image.Name = timestamp;

                    CenterAndScaleElement(image);
                    inkCanvas.Children.Add(image);

                    // 添加鼠标事件处理，使图片可以被选择
                    image.MouseDown += UIElement_MouseDown;
                    image.IsManipulationEnabled = true;

                    timeMachine.CommitElementInsertHistory(image);
                }
            }
        }

        // Keep the old method for backward compatibility
        private async void InsertImage_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif"
            };
            if (dialog.ShowDialog() == true)
            {
                string filePath = dialog.FileName;
                Image image = await CreateAndCompressImageAsync(filePath); // 补充image定义
                if (image != null)
                {
                    string timestamp = "img_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
                    image.Name = timestamp;

                    CenterAndScaleElement(image);
                    inkCanvas.Children.Add(image);

                    // 添加鼠标事件处理，使图片可以被选择
                    image.MouseDown += UIElement_MouseDown;
                    image.IsManipulationEnabled = true;

                    timeMachine.CommitElementInsertHistory(image);
                }
            }
        }

        #region 动态按钮位置计算和高光显示

        /// <summary>
        /// 获取浮动栏中指定按钮的位置
        /// </summary>
        /// <param name="buttonName">按钮的名称</param>
        /// <returns>按钮在浮动栏中的相对位置</returns>
        private double GetFloatingBarButtonPosition(string buttonName)
        {
            try
            {
                // 获取浮动栏容器
                var floatingBarPanel = StackPanelFloatingBar;
                if (floatingBarPanel == null) return 0;

                double currentPosition = 0;
                
                // 遍历浮动栏中的所有子元素
                foreach (var child in floatingBarPanel.Children)
                {
                    if (child is UIElement element)
                    {
                        // 检查是否是我们要找的按钮
                        if (IsTargetButton(element, buttonName))
                        {
                            return currentPosition;
                        }
                        
                        // 累加当前元素的位置
                        currentPosition += GetElementWidth(element);
                    }
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取按钮位置失败: {ex.Message}", LogHelper.LogType.Error);
                return 0;
            }
        }

        /// <summary>
        /// 检查元素是否是目标按钮
        /// </summary>
        private bool IsTargetButton(UIElement element, string buttonName)
        {
            if (element is FrameworkElement fe)
            {
                return fe.Name == buttonName;
            }
            return false;
        }

        /// <summary>
        /// 获取元素的宽度
        /// </summary>
        private double GetElementWidth(UIElement element)
        {
            if (element is FrameworkElement fe)
            {
                // 对于SimpleStackPanel，使用其实际宽度
                if (fe.GetType().Name == "SimpleStackPanel")
                {
                    return fe.ActualWidth > 0 ? fe.ActualWidth : 28; // 默认宽度28
                }
                
                // 对于其他元素，使用其宽度或默认宽度
                return fe.ActualWidth > 0 ? fe.ActualWidth : 28;
            }
            return 28; // 默认宽度
        }

        /// <summary>
        /// 设置浮动栏高光显示位置
        /// </summary>
        /// <param name="mode">模式名称</param>
        private void SetFloatingBarHighlightPosition(string mode)
        {
            try
            {
                if (FloatingbarSelectionBG == null) return;

                double position = 0;
                double buttonWidth = 28; // 每个按钮的默认宽度
                double highlightWidth = 28; // 高光的默认宽度
                
                // 检查快捷调色盘是否显示及其实际宽度
                bool isQuickColorPaletteVisible = false;
                double quickColorPaletteWidth = 0;
                string quickColorPaletteMode = "none";
                
                if (QuickColorPalettePanel != null && QuickColorPalettePanel.Visibility == Visibility.Visible)
                {
                    isQuickColorPaletteVisible = true;
                    quickColorPaletteWidth = QuickColorPalettePanel.ActualWidth > 0 ? QuickColorPalettePanel.ActualWidth : 60;
                    quickColorPaletteMode = "double";
                }
                else if (QuickColorPaletteSingleRowPanel != null && QuickColorPaletteSingleRowPanel.Visibility == Visibility.Visible)
                {
                    isQuickColorPaletteVisible = true;
                    quickColorPaletteWidth = QuickColorPaletteSingleRowPanel.ActualWidth > 0 ? QuickColorPaletteSingleRowPanel.ActualWidth : 120;
                    quickColorPaletteMode = "single";
                }

                // 获取实际按钮宽度，如果获取不到则使用默认值
                double cursorWidth = Cursor_Icon?.ActualWidth > 0 ? Cursor_Icon.ActualWidth : buttonWidth;
                double penWidth = Pen_Icon?.ActualWidth > 0 ? Pen_Icon.ActualWidth : buttonWidth;
                double deleteWidth = SymbolIconDelete?.ActualWidth > 0 ? SymbolIconDelete.ActualWidth : buttonWidth;
                double eraserWidth = Eraser_Icon?.ActualWidth > 0 ? Eraser_Icon.ActualWidth : buttonWidth;
                double eraserByStrokesWidth = EraserByStrokes_Icon?.ActualWidth > 0 ? EraserByStrokes_Icon.ActualWidth : buttonWidth;
                double selectWidth = SymbolIconSelect?.ActualWidth > 0 ? SymbolIconSelect.ActualWidth : buttonWidth;

                // 获取高光的实际宽度
                double actualHighlightWidth = FloatingbarSelectionBG.ActualWidth > 0 ? FloatingbarSelectionBG.ActualWidth : highlightWidth;
                
                double marginOffset = 0; 
                
                // 快捷调色盘的Margin：Margin="4,0,4,0"，所以总宽度需要加上8像素
                double quickColorPaletteTotalWidth = isQuickColorPaletteVisible ? quickColorPaletteWidth + 8 : 0;
                
                // 根据模式计算位置，确保高光居中对齐按钮
                switch (mode)
                {
                    case "cursor":
                        // 鼠标按钮位置：marginOffset + (cursorWidth - actualHighlightWidth) / 2
                        position = marginOffset + (cursorWidth - actualHighlightWidth) / 2;
                        break;
                    case "pen":
                    case "color":
                        // 批注按钮位置：marginOffset + cursorWidth + (penWidth - actualHighlightWidth) / 2
                        position = marginOffset + cursorWidth + (penWidth - actualHighlightWidth) / 2;
                        break;
                    case "eraser":
                        if (isQuickColorPaletteVisible)
                        {
                            // 有快捷调色盘时：鼠标 + 批注 + 快捷调色盘(包含Margin) + 清空 + (面积擦 - 高光) / 2
                            position = marginOffset + cursorWidth + penWidth + quickColorPaletteTotalWidth + deleteWidth + (eraserWidth - actualHighlightWidth) / 2;
                        }
                        else
                        {
                            // 没有快捷调色盘时：鼠标 + 批注 + 清空 + (面积擦 - 高光) / 2
                            position = marginOffset + cursorWidth + penWidth + deleteWidth + (eraserWidth - actualHighlightWidth) / 2;
                        }
                        break;
                    case "eraserByStrokes":
                        if (isQuickColorPaletteVisible)
                        {
                            // 有快捷调色盘时：鼠标 + 批注 + 快捷调色盘(包含Margin) + 清空 + 面积擦 + (线擦 - 高光) / 2
                            position = marginOffset + cursorWidth + penWidth + quickColorPaletteTotalWidth + deleteWidth + eraserWidth + (eraserByStrokesWidth - actualHighlightWidth) / 2;
                        }
                        else
                        {
                            // 没有快捷调色盘时：鼠标 + 批注 + 清空 + 面积擦 + (线擦 - 高光) / 2
                            position = marginOffset + cursorWidth + penWidth + deleteWidth + eraserWidth + (eraserByStrokesWidth - actualHighlightWidth) / 2;
                        }
                        break;
                    case "select":
                        if (isQuickColorPaletteVisible)
                        {
                            // 有快捷调色盘时：鼠标 + 批注 + 快捷调色盘(包含Margin) + 清空 + 面积擦 + 线擦 + (套索选 - 高光) / 2
                            position = marginOffset + cursorWidth + penWidth + quickColorPaletteTotalWidth + deleteWidth + eraserWidth + eraserByStrokesWidth + (selectWidth - actualHighlightWidth) / 2;
                        }
                        else
                        {
                            // 没有快捷调色盘时：鼠标 + 批注 + 清空 + 面积擦 + 线擦 + (套索选 - 高光) / 2
                            position = marginOffset + cursorWidth + penWidth + deleteWidth + eraserWidth + eraserByStrokesWidth + (selectWidth - actualHighlightWidth) / 2;
                        }
                        break;
                    case "shape":
                        if (isQuickColorPaletteVisible)
                        {
                            // 有快捷调色盘时：鼠标 + 批注 + 快捷调色盘(包含Margin) + 清空 + 面积擦 + 线擦 + 套索选 + (几何 - 高光) / 2
                            position = marginOffset + cursorWidth + penWidth + quickColorPaletteTotalWidth + deleteWidth + eraserWidth + eraserByStrokesWidth + selectWidth + (buttonWidth - actualHighlightWidth) / 2;
                        }
                        else
                        {
                            // 没有快捷调色盘时：鼠标 + 批注 + 清空 + 面积擦 + 线擦 + 套索选 + (几何 - 高光) / 2
                            position = marginOffset + cursorWidth + penWidth + deleteWidth + eraserWidth + eraserByStrokesWidth + selectWidth + (buttonWidth - actualHighlightWidth) / 2;
                        }
                        break;
                    default:
                        position = marginOffset;
                        break;
                }

                // 设置高光位置
                FloatingbarSelectionBG.Visibility = Visibility.Visible;
                System.Windows.Controls.Canvas.SetLeft(FloatingbarSelectionBG, position);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"设置高光位置失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 隐藏浮动栏高光显示
        /// </summary>
        private void HideFloatingBarHighlight()
        {
            if (FloatingbarSelectionBG != null)
            {
                FloatingbarSelectionBG.Visibility = Visibility.Hidden;
                System.Windows.Controls.Canvas.SetLeft(FloatingbarSelectionBG, 0);
            }
        }

        #endregion

    }
}
