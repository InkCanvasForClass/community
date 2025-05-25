using Ink_Canvas.Helpers;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using iNKORE.UI.WPF.Modern;
using System.Threading;
using Application = System.Windows.Application;
using Point = System.Windows.Point;
using System.Diagnostics;
using iNKORE.UI.WPF.Modern.Controls;
using System.IO;
using System.Windows.Media.Effects;
using static System.Net.Mime.MediaTypeNames;
using System.Text;
using System.Globalization;
using System.Windows.Data;
using System.Xml.Linq;

namespace Ink_Canvas {
    public partial class MainWindow : Window {
        #region “手勢”按鈕

        /// <summary>
        /// 用於浮動工具欄的“手勢”按鈕和白板工具欄的“手勢”按鈕的點擊事件
        /// </summary>
        private void TwoFingerGestureBorder_MouseUp(object sender, RoutedEventArgs e) {
            if (TwoFingerGestureBorder.Visibility == Visibility.Visible) {
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
            else {
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
        /// 用於更新浮動工具欄的“手勢”按鈕和白板工具欄的“手勢”按鈕的樣式（開啟和關閉狀態）
        /// </summary>
        private void CheckEnableTwoFingerGestureBtnColorPrompt() {
            if (ToggleSwitchEnableMultiTouchMode.IsOn) {
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
            else {
                TwoFingerGestureSimpleStackPanel.Opacity = 1;
                TwoFingerGestureSimpleStackPanel.IsHitTestVisible = true;
                if (Settings.Gesture.IsEnableTwoFingerGesture) {
                    EnableTwoFingerGestureBtn.Source =
                        new BitmapImage(new Uri("/Resources/new-icons/gesture-enabled.png", UriKind.Relative));
                    
                    BoardGesture.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                    BoardGestureGeometry.Brush = new SolidColorBrush(Colors.GhostWhite);
                    BoardGestureGeometry2.Brush = new SolidColorBrush(Colors.GhostWhite);
                    BoardGestureLabel.Foreground = new SolidColorBrush(Colors.GhostWhite);
                    BoardGesture.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                    BoardGestureGeometry.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.EnabledGestureIcon);
                    BoardGestureGeometry2.Geometry = Geometry.Parse("F0 M24,24z M0,0z "+XamlGraphicsIconGeometries.EnabledGestureIconBadgeCheck);
                }
                else {
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
        /// 控制是否顯示浮動工具欄的“手勢”按鈕
        /// </summary>
        private void CheckEnableTwoFingerGestureBtnVisibility(bool isVisible) {
            if (StackPanelCanvasControls.Visibility != Visibility.Visible
                || BorderFloatingBarMainControls.Visibility != Visibility.Visible) {
                EnableTwoFingerGestureBorder.Visibility = Visibility.Collapsed;
            }
            else if (isVisible == true) {
                if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
                    EnableTwoFingerGestureBorder.Visibility = Visibility.Collapsed;
                else EnableTwoFingerGestureBorder.Visibility = Visibility.Visible;
            }
            else {
                EnableTwoFingerGestureBorder.Visibility = Visibility.Collapsed;
            }
        }

        #endregion “手勢”按鈕

        #region 浮動工具欄的拖動實現

        private bool isDragDropInEffect = false;
        private Point pos = new Point();
        private Point downPos = new Point();
        private Point pointDesktop = new Point(-1, -1); //用于记录上次在桌面时的坐标
        private Point pointPPT = new Point(-1, -1); //用于记录上次在PPT中的坐标

        private void SymbolIconEmoji_MouseMove(object sender, MouseEventArgs e) {
            if (isDragDropInEffect) {
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

        private void SymbolIconEmoji_MouseDown(object sender, MouseButtonEventArgs e) {
            if (isViewboxFloatingBarMarginAnimationRunning) {
                ViewboxFloatingBar.BeginAnimation(MarginProperty, null);
                isViewboxFloatingBarMarginAnimationRunning = false;
            }

            isDragDropInEffect = true;
            pos = e.GetPosition(null);
            downPos = e.GetPosition(null);
            GridForFloatingBarDraging.Visibility = Visibility.Visible;
        }

        private void SymbolIconEmoji_MouseUp(object sender, MouseButtonEventArgs e) {
            isDragDropInEffect = false;

            if (e is null || (Math.Abs(downPos.X - e.GetPosition(null).X) <= 10 &&
                              Math.Abs(downPos.Y - e.GetPosition(null).Y) <= 10)) {
                if (BorderFloatingBarMainControls.Visibility == Visibility.Visible) {
                    BorderFloatingBarMainControls.Visibility = Visibility.Collapsed;
                    CheckEnableTwoFingerGestureBtnVisibility(false);
                }
                else {
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
        }

        /// <summary>
        ///     <para>
        ///         易嚴定真，這個多功能函數包括了以下的內容：
        ///     </para>
        ///     <list type="number">
        ///         <item>
        ///             隱藏浮動工具欄和白板模式下的“更多功能”面板
        ///         </item>
        ///         <item>
        ///             隱藏白板模式下和浮動工具欄的畫筆調色盤
        ///         </item>
        ///         <item>
        ///             隱藏白板模式下的“清屏”按鈕（已作廢）
        ///         </item>
        ///         <item>
        ///             負責給Settings設置面板做隱藏動畫
        ///         </item>
        ///         <item>
        ///             隱藏白板模式下和浮動工具欄的“手勢”面板
        ///         </item>
        ///         <item>
        ///             當<c>ToggleSwitchDrawShapeBorderAutoHide</c>開啟時，會自動隱藏白板模式下和浮動工具欄的“形狀”面板
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
        ///             當<c><paramref name="mode"/>=="color" || <paramref name="mode"/>=="pen"</c>時，會高亮浮動工具欄和白板工具欄中的“批註”，“筆”按鈕
        ///         </item>
        ///         <item>
        ///             當<c><paramref name="mode"/>=="eraser"</c>時，會高亮白板工具欄中的“橡皮”和浮動工具欄中的“面積擦”按鈕
        ///         </item>
        ///         <item>
        ///             當<c><paramref name="mode"/>=="eraserByStrokes"</c>時，會高亮白板工具欄中的“橡皮”和浮動工具欄中的“墨跡擦”按鈕
        ///         </item>
        ///         <item>
        ///             當<c><paramref name="mode"/>=="select"</c>時，會高亮浮動工具欄和白板工具欄中的“選擇”，“套索選”按鈕
        ///         </item>
        ///     </list>
        /// </param>
        /// <param name="autoAlignCenter">
        ///     是否自動居中浮動工具欄
        /// </param>
        private async void HideSubPanels(string mode = null, bool autoAlignCenter = false) {
            AnimationsHelper.HideWithSlideAndFade(BorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
            AnimationsHelper.HideWithSlideAndFade(PenPalette);
            AnimationsHelper.HideWithSlideAndFade(BoardPenPalette);
            AnimationsHelper.HideWithSlideAndFade(BoardEraserSizePanel);
            AnimationsHelper.HideWithSlideAndFade(EraserSizePanel);
            AnimationsHelper.HideWithSlideAndFade(BorderDrawShape);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderLeftPageListView);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderRightPageListView);

            if (BorderSettings.Visibility == Visibility.Visible) {
                BorderSettingsMask.IsHitTestVisible = false;
                BorderSettingsMask.Background = null;
                var sb = new Storyboard();

                // 滑动动画
                var slideAnimation = new DoubleAnimation {
                    From = 0, // 滑动距离
                    To = BorderSettings.RenderTransform.Value.OffsetX - 440,
                    Duration = TimeSpan.FromSeconds(0.6)
                };
                slideAnimation.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut };
                Storyboard.SetTargetProperty(slideAnimation,
                    new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

                sb.Children.Add(slideAnimation);

                sb.Completed += (s, _) => {
                    BorderSettings.Visibility = Visibility.Collapsed;
                    isOpeningOrHidingSettingsPane = false;
                };

                BorderSettings.Visibility = Visibility.Visible;
                BorderSettings.RenderTransform = new TranslateTransform();

                isOpeningOrHidingSettingsPane = true;
                sb.Begin((FrameworkElement)BorderSettings);
            }

            AnimationsHelper.HideWithSlideAndFade(TwoFingerGestureBorder);
            AnimationsHelper.HideWithSlideAndFade(EraserSizePanel);
            AnimationsHelper.HideWithSlideAndFade(BoardTwoFingerGestureBorder);
            if (ToggleSwitchDrawShapeBorderAutoHide.IsOn) {
                AnimationsHelper.HideWithSlideAndFade(BorderDrawShape);
                AnimationsHelper.HideWithSlideAndFade(BoardBorderDrawShape);
            }

            if (mode != null) {
                if (mode != "clear") {
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

                    FloatingbarSelectionBG.Visibility = Visibility.Hidden;
                    System.Windows.Controls.Canvas.SetLeft(FloatingbarSelectionBG, 0);
                }

                switch (mode) {
                    case "pen":
                    case "color": {
                        PenIconGeometry.Brush = new SolidColorBrush(Color.FromRgb(30, 58, 138));
                        PenIconGeometry.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.SolidPenIcon);
                        BoardPen.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                        BoardPen.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                        BoardPenGeometry.Brush = new SolidColorBrush(Colors.GhostWhite);
                        BoardPenLabel.Foreground = new SolidColorBrush(Colors.GhostWhite);

                        FloatingbarSelectionBG.Visibility = Visibility.Visible;
                        System.Windows.Controls.Canvas.SetLeft(FloatingbarSelectionBG, 28);
                            break;
                    }
                    case "eraser": {
                        CircleEraserIconGeometry.Brush = new SolidColorBrush(Color.FromRgb(30, 58, 138));
                        CircleEraserIconGeometry.Geometry =
                            Geometry.Parse(XamlGraphicsIconGeometries.SolidEraserCircleIcon);
                        BoardEraser.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                        BoardEraser.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                        BoardEraserGeometry.Brush = new SolidColorBrush(Colors.GhostWhite);
                        BoardEraserLabel.Foreground = new SolidColorBrush(Colors.GhostWhite);

                        FloatingbarSelectionBG.Visibility = Visibility.Visible;
                        System.Windows.Controls.Canvas.SetLeft(FloatingbarSelectionBG, 28 * 3);
                            break;
                    }
                    case "eraserByStrokes": {
                        StrokeEraserIconGeometry.Brush = new SolidColorBrush(Color.FromRgb(30, 58, 138));
                        StrokeEraserIconGeometry.Geometry =
                            Geometry.Parse(XamlGraphicsIconGeometries.SolidEraserStrokeIcon);
                        BoardEraser.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                        BoardEraser.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                        BoardEraserGeometry.Brush = new SolidColorBrush(Colors.GhostWhite);
                        BoardEraserLabel.Foreground = new SolidColorBrush(Colors.GhostWhite);

                        FloatingbarSelectionBG.Visibility = Visibility.Visible;
                        System.Windows.Controls.Canvas.SetLeft(FloatingbarSelectionBG, 28 * 4);
                            break;
                    }
                    case "select": {
                        LassoSelectIconGeometry.Brush = new SolidColorBrush(Color.FromRgb(30, 58, 138));
                        LassoSelectIconGeometry.Geometry =
                            Geometry.Parse(XamlGraphicsIconGeometries.SolidLassoSelectIcon);
                        BoardSelect.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                        BoardSelect.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                        BoardSelectGeometry.Brush = new SolidColorBrush(Colors.GhostWhite);
                        BoardSelectLabel.Foreground = new SolidColorBrush(Colors.GhostWhite);

                        FloatingbarSelectionBG.Visibility = Visibility.Visible;
                        System.Windows.Controls.Canvas.SetLeft(FloatingbarSelectionBG, 28 * 5);
                            break;
                    }
                }


                if (autoAlignCenter) // 控制居中
                {
                    if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible) {
                        await Task.Delay(50);
                        ViewboxFloatingBarMarginAnimation(60);
                    }
                    else if (Topmost == true) //非黑板
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
        private void SymbolIconUndo_MouseUp(object sender, MouseButtonEventArgs e) {
            //if (lastBorderMouseDownObject != sender) return;

            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == SymbolIconUndo && lastBorderMouseDownObject != SymbolIconUndo) return;

            if (!BtnUndo.IsEnabled) return;
            BtnUndo_Click(BtnUndo, null);
            HideSubPanels();
        }

        private void SymbolIconRedo_MouseUp(object sender, MouseButtonEventArgs e) {
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
        private bool isDisplayingOrHidingBlackboard = false;

        private void ImageBlackboard_MouseUp(object sender, MouseButtonEventArgs e)
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
                //进入黑板

                /*
                if (Not_Enter_Blackboard_fir_Mouse_Click) {// BUG-Fixed_tmp：程序启动后直接进入白板会导致后续撤销功能、退出白板无法恢复墨迹
                    BtnColorRed_Click(BorderPenColorRed, null);
                    await Task.Delay(200);
                    SimulateMouseClick.SimulateMouseClickAtTopLeft();
                    await Task.Delay(10);
                    Not_Enter_Blackboard_fir_Mouse_Click = false;
                }
                */
                new Thread(new ThreadStart(() => {
                    Thread.Sleep(100);
                    Application.Current.Dispatcher.Invoke(() => { ViewboxFloatingBarMarginAnimation(60); });
                })).Start();

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

                if (Settings.Appearance.EnableTimeDisplayInWhiteboardMode == true)
                {
                    WaterMarkTime.Visibility = Visibility.Visible;
                    WaterMarkDate.Visibility = Visibility.Visible;
                } else {
                    WaterMarkTime.Visibility = Visibility.Collapsed;
                    WaterMarkDate.Visibility = Visibility.Collapsed;
                }

                if (Settings.Appearance.EnableChickenSoupInWhiteboardMode == true)
                {
                    BlackBoardWaterMark.Visibility = Visibility.Visible;
                } else {
                    BlackBoardWaterMark.Visibility = Visibility.Collapsed;
                }

                if (Settings.Appearance.ChickenSoupSource == 0) {
                    int randChickenSoupIndex = new Random().Next(ChickenSoup.OSUPlayerYuLu.Length);
                    BlackBoardWaterMark.Text = ChickenSoup.OSUPlayerYuLu[randChickenSoupIndex];
                } else if (Settings.Appearance.ChickenSoupSource == 1) {
                    int randChickenSoupIndex = new Random().Next(ChickenSoup.MingYanJingJu.Length);
                    BlackBoardWaterMark.Text = ChickenSoup.MingYanJingJu[randChickenSoupIndex];
                } else if (Settings.Appearance.ChickenSoupSource == 2) {
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

                if (Settings.Automation.IsAutoSaveStrokesAtClear &&
                    inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber) SaveScreenShot(true);

                if (BtnPPTSlideShowEnd.Visibility == Visibility.Collapsed)
                    new Thread(new ThreadStart(() => {
                        Thread.Sleep(300);
                        Application.Current.Dispatcher.Invoke(() => { ViewboxFloatingBarMarginAnimation(100, true); });
                    })).Start();
                else
                    new Thread(new ThreadStart(() => {
                        Thread.Sleep(300);
                        Application.Current.Dispatcher.Invoke(() => { ViewboxFloatingBarMarginAnimation(60); });
                    })).Start();

                if (System.Windows.Controls.Canvas.GetLeft(FloatingbarSelectionBG)!=28) PenIcon_Click(null, null);

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

            new Thread(new ThreadStart(() => {
                Thread.Sleep(200);
                Application.Current.Dispatcher.Invoke(() => { isDisplayingOrHidingBlackboard = false; });
            })).Start();

            SwitchToDefaultPen(null, null);
            CheckColorTheme(true);
        }

        #endregion
        private async void SymbolIconCursor_Click(object sender, RoutedEventArgs e) {
            if (currentMode != 0) {
                ImageBlackboard_MouseUp(null, null);
            }
            else {
                BtnHideInkCanvas_Click(BtnHideInkCanvas, null);

                if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible) {
                    await Task.Delay(100);
                    ViewboxFloatingBarMarginAnimation(60);
                }
            }
        }

        #region 清空畫布按鈕

        private void SymbolIconDelete_MouseUp(object sender, MouseButtonEventArgs e) {

            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == SymbolIconDelete && lastBorderMouseDownObject != SymbolIconDelete) return;

            if (inkCanvas.GetSelectedStrokes().Count > 0) {
                inkCanvas.Strokes.Remove(inkCanvas.GetSelectedStrokes());
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
            }
            else if (inkCanvas.Strokes.Count > 0) {
                if (Settings.Automation.IsAutoSaveStrokesAtClear &&
                    inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber) {
                    if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
                        SaveScreenShot(true, $"{pptName}/{previousSlideID}_{DateTime.Now:HH-mm-ss}");
                    else
                        SaveScreenShot(true);
                }

                BtnClear_Click(null, null);
            }
        }

        #endregion

        #region 主要的工具按鈕事件

        /// <summary>
        ///     浮動工具欄的“套索選”按鈕事件，重定向到舊UI的<c>BtnSelect_Click</c>方法
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">MouseButtonEventArgs</param>
        private void SymbolIconSelect_MouseUp(object sender, MouseButtonEventArgs e)
        {

            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == SymbolIconSelect && lastBorderMouseDownObject != SymbolIconSelect) return;

            FloatingbarSelectionBG.Visibility = Visibility.Visible;
            System.Windows.Controls.Canvas.SetLeft(FloatingbarSelectionBG, 140);
            BtnSelect_Click(null, null);
            HideSubPanels("select");
        }

        #endregion

        private void FloatingBarToolBtnMouseDownFeedback_Panel(object sender, MouseButtonEventArgs e) {
            var s = (Panel)sender;
            lastBorderMouseDownObject = sender;
            if (s == SymbolIconDelete) s.Background = new SolidColorBrush(Color.FromArgb(28, 127, 29, 29));
            else s.Background = new SolidColorBrush(Color.FromArgb(28, 24, 24, 27));
        }

        private void FloatingBarToolBtnMouseLeaveFeedback_Panel(object sender, MouseEventArgs e) {
            var s = (Panel)sender;
            lastBorderMouseDownObject = null;
            s.Background = new SolidColorBrush(Colors.Transparent);
        }

        private void SymbolIconSettings_Click(object sender, RoutedEventArgs e) {
            if (isOpeningOrHidingSettingsPane != false) return;
            HideSubPanels();
            BtnSettings_Click(null, null);
        }

        private async void SymbolIconScreenshot_MouseUp(object sender, MouseButtonEventArgs e) {
            HideSubPanelsImmediately();
            await Task.Delay(50);
            SaveScreenShotToDesktop();
        }

        

        private void ImageCountdownTimer_MouseUp(object sender, MouseButtonEventArgs e) {
            LeftUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            RightUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            AnimationsHelper.HideWithSlideAndFade(BorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);

            new CountdownTimerWindow().Show();
        }

        private void OperatingGuideWindowIcon_MouseUp(object sender, MouseButtonEventArgs e) {
            AnimationsHelper.HideWithSlideAndFade(BorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);

            new OperatingGuideWindow().Show();
        }

        private void SymbolIconRand_MouseUp(object sender, MouseButtonEventArgs e) {
            LeftUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            RightUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            if (lastBorderMouseDownObject != sender) return;

            AnimationsHelper.HideWithSlideAndFade(BorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);

            new RandWindow(Settings).Show();
        }

        public void CheckEraserTypeTab() {
            if (Settings.Canvas.EraserShapeType == 0) {
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
            else {
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

        private void SymbolIconRandOne_MouseUp(object sender, MouseButtonEventArgs e) {
            LeftUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            RightUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            if (lastBorderMouseDownObject != sender) return;

            AnimationsHelper.HideWithSlideAndFade(BorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);

            new RandWindow(Settings,true).ShowDialog();
        }

        private void GridInkReplayButton_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;

            AnimationsHelper.HideWithSlideAndFade(BorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);

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
            new Thread(() => {
                isRestartInkReplay = true;
                while (isRestartInkReplay) {
                    isRestartInkReplay = false;
                    Application.Current.Dispatcher.Invoke(() => {
                        InkCanvasForInkReplay.Strokes.Clear();
                    });
                    foreach (var stroke in strokes) {

                        if (isRestartInkReplay) break;

                        var stylusPoints = new StylusPointCollection();
                        if (stroke.StylusPoints.Count == 629) //圆或椭圆
                        {
                            Stroke s = null;
                            foreach (var stylusPoint in stroke.StylusPoints) {

                                if (isRestartInkReplay) break;

                                while (isPauseInkReplay) {
                                    Thread.Sleep(10);
                                }

                                if (i++ >= 50) {
                                    i = 0;
                                    Thread.Sleep((int)(10 / inkReplaySpeed));
                                    if (isStopInkReplay) return;
                                }

                                Application.Current.Dispatcher.Invoke(() => {
                                    try {
                                        InkCanvasForInkReplay.Strokes.Remove(s);
                                    }
                                    catch { }

                                    stylusPoints.Add(stylusPoint);
                                    s = new Stroke(stylusPoints.Clone());
                                    s.DrawingAttributes = stroke.DrawingAttributes;
                                    InkCanvasForInkReplay.Strokes.Add(s);
                                });
                            }
                        } else {
                            Stroke s = null;
                            foreach (var stylusPoint in stroke.StylusPoints) {

                                if (isRestartInkReplay) break;

                                while (isPauseInkReplay) {
                                    Thread.Sleep(10);
                                }

                                if (i++ >= k) {
                                    i = 0;
                                    Thread.Sleep((int)(10 / inkReplaySpeed));
                                    if (isStopInkReplay) return;
                                }

                                Application.Current.Dispatcher.Invoke(() => {
                                    try {
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
                Application.Current.Dispatcher.Invoke(() => {
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

        private bool isStopInkReplay = false;
        private bool isPauseInkReplay = false;
        private bool isRestartInkReplay = false;
        private double inkReplaySpeed = 1;

        private void InkCanvasForInkReplay_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 2) {
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

        private void InkReplayPlayPauseBorder_OnMouseDown(object sender, MouseButtonEventArgs e) {
            InkReplayPlayPauseBorder.Background = new SolidColorBrush(Color.FromArgb(34, 9, 9, 11));
        }

        private void InkReplayPlayPauseBorder_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            InkReplayPlayPauseBorder.Background = new SolidColorBrush(Colors.Transparent);
            isPauseInkReplay = !isPauseInkReplay;
            InkReplayPanelStatusText.Text = isPauseInkReplay?"已暂停！":"正在重播墨迹...";
            InkReplayPlayButtonImage.Visibility = isPauseInkReplay ? Visibility.Visible: Visibility.Collapsed;
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

        private void SymbolIconTools_MouseUp(object sender, MouseButtonEventArgs e) {

            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == ToolsFloatingBarBtn && lastBorderMouseDownObject != ToolsFloatingBarBtn) return;

            if (BorderTools.Visibility == Visibility.Visible) {
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
            else {
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
                AnimationsHelper.ShowWithSlideFromBottomAndFade(BorderTools);
                AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardBorderTools);
            }
        }

        private bool isViewboxFloatingBarMarginAnimationRunning = false;

        public async void ViewboxFloatingBarMarginAnimation(int MarginFromEdge,
            bool PosXCaculatedWithTaskbarHeight = false) {
            if (MarginFromEdge == 60) MarginFromEdge = 55;
            await Dispatcher.InvokeAsync(() => {
                if (Topmost == false)
                    MarginFromEdge = -60;
                else
                    ViewboxFloatingBar.Visibility = Visibility.Visible;
                isViewboxFloatingBarMarginAnimationRunning = true;

                double dpiScaleX = 1, dpiScaleY = 1;
                var source = PresentationSource.FromVisual(this);
                if (source != null) {
                    dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                    dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
                }

                var windowHandle = new WindowInteropHelper(this).Handle;
                var screen = System.Windows.Forms.Screen.FromHandle(windowHandle);
                double screenWidth = screen.Bounds.Width / dpiScaleX, screenHeight = screen.Bounds.Height / dpiScaleY;
                var toolbarHeight = SystemParameters.PrimaryScreenHeight - SystemParameters.FullPrimaryScreenHeight -
                                    SystemParameters.WindowCaptionHeight;
                pos.X = (screenWidth - ViewboxFloatingBar.ActualWidth * ViewboxFloatingBarScaleTransform.ScaleX) / 2;

                if (PosXCaculatedWithTaskbarHeight == false)
                    pos.Y = screenHeight - MarginFromEdge * ViewboxFloatingBarScaleTransform.ScaleY;
                else if (PosXCaculatedWithTaskbarHeight == true)
                    pos.Y = screenHeight - ViewboxFloatingBar.ActualHeight * ViewboxFloatingBarScaleTransform.ScaleY -
                            toolbarHeight - ViewboxFloatingBarScaleTransform.ScaleY * 3;

                if (MarginFromEdge != -60) {
                    if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible) {
                        if (pointPPT.X != -1 || pointPPT.Y != -1) {
                            if (Math.Abs(pointPPT.Y - pos.Y) > 50)
                                pos = pointPPT;
                            else
                                pointPPT = pos;
                        }
                    }
                    else {
                        if (pointDesktop.X != -1 || pointDesktop.Y != -1) {
                            if (Math.Abs(pointDesktop.Y - pos.Y) > 50)
                                pos = pointDesktop;
                            else
                                pointDesktop = pos;
                        }
                    }
                }

                var marginAnimation = new ThicknessAnimation {
                    Duration = TimeSpan.FromSeconds(0.35),
                    From = ViewboxFloatingBar.Margin,
                    To = new Thickness(pos.X, pos.Y, 0, -20)
                };
                marginAnimation.EasingFunction = new CircleEase();
                ViewboxFloatingBar.BeginAnimation(MarginProperty, marginAnimation);
            });

            await Task.Delay(200);

            await Dispatcher.InvokeAsync(() => {
                ViewboxFloatingBar.Margin = new Thickness(pos.X, pos.Y, -2000, -200);
                if (Topmost == false) ViewboxFloatingBar.Visibility = Visibility.Hidden;
            });
        }

        public async void PureViewboxFloatingBarMarginAnimationInDesktopMode()
        {
            await Dispatcher.InvokeAsync(() => {
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
                var screen = System.Windows.Forms.Screen.FromHandle(windowHandle);
                double screenWidth = screen.Bounds.Width / dpiScaleX, screenHeight = screen.Bounds.Height / dpiScaleY;
                var toolbarHeight = SystemParameters.PrimaryScreenHeight - SystemParameters.FullPrimaryScreenHeight -
                                    SystemParameters.WindowCaptionHeight;
                pos.X = (screenWidth - ViewboxFloatingBar.ActualWidth * ViewboxFloatingBarScaleTransform.ScaleX) / 2;

                pos.Y = screenHeight - ViewboxFloatingBar.ActualHeight * ViewboxFloatingBarScaleTransform.ScaleY -
                        toolbarHeight - ViewboxFloatingBarScaleTransform.ScaleY * 3;

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

            await Dispatcher.InvokeAsync(() => {
                ViewboxFloatingBar.Margin = new Thickness(pos.X, pos.Y, -2000, -200);
            });
        }

        public async void PureViewboxFloatingBarMarginAnimationInPPTMode()
        {
            await Dispatcher.InvokeAsync(() => {
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
                var screen = System.Windows.Forms.Screen.FromHandle(windowHandle);
                double screenWidth = screen.Bounds.Width / dpiScaleX, screenHeight = screen.Bounds.Height / dpiScaleY;
                var toolbarHeight = SystemParameters.PrimaryScreenHeight - SystemParameters.FullPrimaryScreenHeight -
                                    SystemParameters.WindowCaptionHeight;
                pos.X = (screenWidth - ViewboxFloatingBar.ActualWidth * ViewboxFloatingBarScaleTransform.ScaleX) / 2;

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

            await Dispatcher.InvokeAsync(() => {
                ViewboxFloatingBar.Margin = new Thickness(pos.X, pos.Y, -2000, -200);
            });
        }

        private async void CursorIcon_Click(object sender, RoutedEventArgs e) {
            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender==Cursor_Icon && lastBorderMouseDownObject != Cursor_Icon) return;
            // 隱藏高亮
            FloatingbarSelectionBG.Visibility = Visibility.Hidden;
            System.Windows.Controls.Canvas.SetLeft(FloatingbarSelectionBG, 0);

            // 切换前自动截图保存墨迹
            if (inkCanvas.Strokes.Count > 0 &&
                inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber) {
                if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
                    SaveScreenShot(true, $"{pptName}/{previousSlideID}_{DateTime.Now:HH-mm-ss}");
                else SaveScreenShot(true);
            }

            if (BtnPPTSlideShowEnd.Visibility != Visibility.Visible) {
                if (Settings.Canvas.HideStrokeWhenSelecting) {
                    inkCanvas.Visibility = Visibility.Collapsed;
                }
                else {
                    inkCanvas.IsHitTestVisible = false;
                    inkCanvas.Visibility = Visibility.Visible;
                }
            }
            else {
                if (Settings.PowerPointSettings.IsShowStrokeOnSelectInPowerPoint) {
                    inkCanvas.Visibility = Visibility.Visible;
                    inkCanvas.IsHitTestVisible = true;
                }
                else {
                    if (Settings.Canvas.HideStrokeWhenSelecting) {
                        inkCanvas.Visibility = Visibility.Collapsed;
                    }
                    else {
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

            if (currentMode != 0) {
                SaveStrokes();
                RestoreStrokes(true);
            }

            if (BtnSwitchTheme.Content.ToString() == "浅色")
                BtnSwitch.Content = "黑板";
            else
                BtnSwitch.Content = "白板";

            StackPanelPPTButtons.Visibility = Visibility.Visible;
            BtnHideInkCanvas.Content = "显示\n画板";
            CheckEnableTwoFingerGestureBtnVisibility(false);


            StackPanelCanvasControls.Visibility = Visibility.Collapsed;

            if (!isFloatingBarFolded) {
                HideSubPanels("cursor", true);
                await Task.Delay(50);

                if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
                    ViewboxFloatingBarMarginAnimation(60);
                else
                    ViewboxFloatingBarMarginAnimation(100, true);
            }
        }

        private void PenIcon_Click(object sender, RoutedEventArgs e) {

            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == Pen_Icon && lastBorderMouseDownObject != Pen_Icon) return;

            FloatingbarSelectionBG.Visibility = Visibility.Visible;
            System.Windows.Controls.Canvas.SetLeft(FloatingbarSelectionBG, 28);

            if (Pen_Icon.Background == null || StackPanelCanvasControls.Visibility == Visibility.Collapsed) {
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;

                GridTransparencyFakeBackground.Opacity = 1;
                GridTransparencyFakeBackground.Background = new SolidColorBrush(StringToColor("#01FFFFFF"));

                inkCanvas.IsHitTestVisible = true;
                inkCanvas.Visibility = Visibility.Visible;

                GridBackgroundCoverHolder.Visibility = Visibility.Visible;
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

                /*if (forceEraser && currentMode == 0)
                    BtnColorRed_Click(sender, null);*/

                if (GridBackgroundCover.Visibility == Visibility.Collapsed) {
                    if (BtnSwitchTheme.Content.ToString() == "浅色")
                        BtnSwitch.Content = "黑板";
                    else
                        BtnSwitch.Content = "白板";
                    StackPanelPPTButtons.Visibility = Visibility.Visible;
                }
                else {
                    BtnSwitch.Content = "屏幕";
                    StackPanelPPTButtons.Visibility = Visibility.Collapsed;
                }

                BtnHideInkCanvas.Content = "隐藏\n画板";

                StackPanelCanvasControls.Visibility = Visibility.Visible;
                //AnimationsHelper.ShowWithSlideFromLeftAndFade(StackPanelCanvasControls);
                CheckEnableTwoFingerGestureBtnVisibility(true);
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                ColorSwitchCheck();
                HideSubPanels("pen", true);
            }
            else {
                if (inkCanvas.EditingMode == InkCanvasEditingMode.Ink) {
                    if (PenPalette.Visibility == Visibility.Visible) {
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
                    else {
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
                        AnimationsHelper.ShowWithSlideFromBottomAndFade(PenPalette);
                        AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardPenPalette);
                    }
                }
                else {
                    inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    ColorSwitchCheck();
                    HideSubPanels("pen", true);
                }
            }
        }

        private void ColorThemeSwitch_MouseUp(object sender, RoutedEventArgs e) {
            isUselightThemeColor = !isUselightThemeColor;
            if (currentMode == 0) isDesktopUselightThemeColor = isUselightThemeColor;
            CheckColorTheme();
        }

        private void EraserIcon_Click(object sender, RoutedEventArgs e) {

            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == Eraser_Icon && lastBorderMouseDownObject != Eraser_Icon) return;

            FloatingbarSelectionBG.Visibility = Visibility.Visible;
            System.Windows.Controls.Canvas.SetLeft(FloatingbarSelectionBG, 84);

            forceEraser = true;
            forcePointEraser = true;
            if (Settings.Canvas.EraserShapeType == 0) {
                double k = 1;
                switch (Settings.Canvas.EraserSize) {
                    case 0:
                        k = 0.5;
                        break;
                    case 1:
                        k = 0.8;
                        break;
                    case 3:
                        k = 1.25;
                        break;
                    case 4:
                        k = 1.8;
                        break;
                }

                inkCanvas.EraserShape = new EllipseStylusShape(k * 90, k * 90);
            }
            else if (Settings.Canvas.EraserShapeType == 1) {
                double k = 1;
                switch (Settings.Canvas.EraserSize) {
                    case 0:
                        k = 0.7;
                        break;
                    case 1:
                        k = 0.9;
                        break;
                    case 3:
                        k = 1.2;
                        break;
                    case 4:
                        k = 1.6;
                        break;
                }

                inkCanvas.EraserShape = new RectangleStylusShape(k * 90 * 0.6, k * 90);
            }

            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint) {
                if (EraserSizePanel.Visibility == Visibility.Collapsed) {
                    AnimationsHelper.HideWithSlideAndFade(BorderTools);
                    AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
                    AnimationsHelper.HideWithSlideAndFade(PenPalette);
                    AnimationsHelper.HideWithSlideAndFade(BoardPenPalette);
                    AnimationsHelper.HideWithSlideAndFade(BorderDrawShape);
                    AnimationsHelper.HideWithSlideAndFade(BoardBorderDrawShape);
                    AnimationsHelper.HideWithSlideAndFade(BorderTools);
                    AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);
                    AnimationsHelper.ShowWithSlideFromBottomAndFade(EraserSizePanel);
                    AnimationsHelper.ShowWithSlideFromBottomAndFade(BoardEraserSizePanel);
                } else {
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
            }
            else {
                HideSubPanels("eraser");
            }

            inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
            drawingShapeMode = 0;

            inkCanvas_EditingModeChanged(inkCanvas, null);
            CancelSingleFingerDragMode();
        }

        private void EraserIconByStrokes_Click(object sender, RoutedEventArgs e) {

            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == EraserByStrokes_Icon && lastBorderMouseDownObject != EraserByStrokes_Icon) return;

            FloatingbarSelectionBG.Visibility = Visibility.Visible;
            System.Windows.Controls.Canvas.SetLeft(FloatingbarSelectionBG, 112);

            forceEraser = true;
            forcePointEraser = false;

            inkCanvas.EraserShape = new EllipseStylusShape(5, 5);
            inkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
            drawingShapeMode = 0;

            inkCanvas_EditingModeChanged(inkCanvas, null);
            CancelSingleFingerDragMode();

            HideSubPanels("eraserByStrokes");
        }

        private void CursorWithDelIcon_Click(object sender, RoutedEventArgs e) {

            if (lastBorderMouseDownObject != null && lastBorderMouseDownObject is Panel)
                ((Panel)lastBorderMouseDownObject).Background = new SolidColorBrush(Colors.Transparent);
            if (sender == CursorWithDelFloatingBarBtn && lastBorderMouseDownObject != CursorWithDelFloatingBarBtn) return;

            SymbolIconDelete_MouseUp(sender, null);
            CursorIcon_Click(null, null);
        }

        private void SelectIcon_MouseUp(object sender, RoutedEvent e) {
            forceEraser = true;
            drawingShapeMode = 0;
            inkCanvas.IsManipulationEnabled = false;
            if (inkCanvas.EditingMode == InkCanvasEditingMode.Select) {
                var selectedStrokes = new StrokeCollection();
                foreach (var stroke in inkCanvas.Strokes)
                    if (stroke.GetBounds().Width > 0 && stroke.GetBounds().Height > 0)
                        selectedStrokes.Add(stroke);
                inkCanvas.Select(selectedStrokes);
            }
            else {
                inkCanvas.EditingMode = InkCanvasEditingMode.Select;
            }
        }

        private void DrawShapePromptToPen() {
            if (isLongPressSelected == true) {
                HideSubPanels("pen");
            }
            else {
                if (StackPanelCanvasControls.Visibility == Visibility.Visible)
                    HideSubPanels("pen");
                else
                    HideSubPanels("cursor");
            }
        }

        private void CloseBordertools_MouseUp(object sender, MouseButtonEventArgs e) {
            HideSubPanels();
        }

        #region Left Side Panel

        private void BtnFingerDragMode_Click(object sender, RoutedEventArgs e) {
            if (isSingleFingerDragMode) {
                isSingleFingerDragMode = false;
                BtnFingerDragMode.Content = "单指\n拖动";
            }
            else {
                isSingleFingerDragMode = true;
                BtnFingerDragMode.Content = "多指\n拖动";
            }
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e) {
            if (inkCanvas.GetSelectedStrokes().Count != 0) {
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
                inkCanvas.Select(new StrokeCollection());
            }

            var item = timeMachine.Undo();
            ApplyHistoryToCanvas(item);
        }

        private void BtnRedo_Click(object sender, RoutedEventArgs e) {
            if (inkCanvas.GetSelectedStrokes().Count != 0) {
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
                inkCanvas.Select(new StrokeCollection());
            }

            var item = timeMachine.Redo();
            ApplyHistoryToCanvas(item);
        }

        private void Btn_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (!isLoaded) return;
            try {
                if (((Button)sender).IsEnabled)
                    ((UIElement)((Button)sender).Content).Opacity = 1;
                else
                    ((UIElement)((Button)sender).Content).Opacity = 0.25;
            }
            catch { }
        }

        #endregion Left Side Panel

        #region Right Side Panel

        public static bool CloseIsFromButton = false;

        public void BtnExit_Click(object sender, RoutedEventArgs e) {
            CloseIsFromButton = true;
            Close();
        }

        public void BtnRestart_Click(object sender, RoutedEventArgs e) {
            Process.Start(System.Windows.Forms.Application.ExecutablePath, "-m");

            CloseIsFromButton = true;
            Application.Current.Shutdown();
        }

        private void SettingsOverlayClick(object sender, MouseButtonEventArgs e) {
            if (isOpeningOrHidingSettingsPane == true) return;
            BtnSettings_Click(null, null);
        }

        private bool isOpeningOrHidingSettingsPane = false;

        private void BtnSettings_Click(object sender, RoutedEventArgs e) {
            if (BorderSettings.Visibility == Visibility.Visible) {
                HideSubPanels();
            }
            else {
                BorderSettingsMask.IsHitTestVisible = true;
                BorderSettingsMask.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
                SettingsPanelScrollViewer.ScrollToTop();
                var sb = new Storyboard();

                // 滑动动画
                var slideAnimation = new DoubleAnimation {
                    From = BorderSettings.RenderTransform.Value.OffsetX - 440, // 滑动距离
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.6)
                };
                slideAnimation.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut };
                Storyboard.SetTargetProperty(slideAnimation,
                    new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

                sb.Children.Add(slideAnimation);

                sb.Completed += (s, _) => { isOpeningOrHidingSettingsPane = false; };

                BorderSettings.Visibility = Visibility.Visible;
                BorderSettings.RenderTransform = new TranslateTransform();

                isOpeningOrHidingSettingsPane = true;
                sb.Begin((FrameworkElement)BorderSettings);
            }
        }

        private void BtnThickness_Click(object sender, RoutedEventArgs e) { }

        private bool forceEraser = false;


        private void BtnClear_Click(object sender, RoutedEventArgs e) {
            forceEraser = false;
            //BorderClearInDelete.Visibility = Visibility.Collapsed;

            if (currentMode == 0) {
                // 先回到画笔再清屏，避免 TimeMachine 的相关 bug 影响
                if (Pen_Icon.Background == null && StackPanelCanvasControls.Visibility == Visibility.Visible)
                    PenIcon_Click(null, null);
            }
            else {
                if (Pen_Icon.Background == null) PenIcon_Click(null, null);
            }

            if (inkCanvas.Strokes.Count != 0) {
                var whiteboardIndex = CurrentWhiteboardIndex;
                if (currentMode == 0) whiteboardIndex = 0;
                strokeCollections[whiteboardIndex] = inkCanvas.Strokes.Clone();
            }

            ClearStrokes(false);
            inkCanvas.Children.Clear();

            CancelSingleFingerDragMode();

            if (Settings.Canvas.ClearCanvasAndClearTimeMachine) timeMachine.ClearStrokeHistory();
        }

        private bool lastIsInMultiTouchMode = false;

        private void CancelSingleFingerDragMode() {
            if (ToggleSwitchDrawShapeBorderAutoHide.IsOn) CollapseBorderDrawShape();

            GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

            if (isSingleFingerDragMode) BtnFingerDragMode_Click(BtnFingerDragMode, null);
            isLongPressSelected = false;
        }

        private void BtnHideControl_Click(object sender, RoutedEventArgs e) {
            if (StackPanelControl.Visibility == Visibility.Visible)
                StackPanelControl.Visibility = Visibility.Hidden;
            else
                StackPanelControl.Visibility = Visibility.Visible;
        }

        private int currentMode = 0;

        private void BtnSwitch_Click(object sender, RoutedEventArgs e) {
            if (GridTransparencyFakeBackground.Background == Brushes.Transparent) {
                if (currentMode == 0) {
                    currentMode++;
                    GridBackgroundCover.Visibility = Visibility.Collapsed;
                    AnimationsHelper.HideWithSlideAndFade(BlackboardLeftSide);
                    AnimationsHelper.HideWithSlideAndFade(BlackboardCenterSide);
                    AnimationsHelper.HideWithSlideAndFade(BlackboardRightSide);

                    SaveStrokes(true);
                    ClearStrokes(true);
                    RestoreStrokes();

                    if (BtnSwitchTheme.Content.ToString() == "浅色") {
                        BtnSwitch.Content = "黑板";
                        BtnExit.Foreground = Brushes.White;
                    }
                    else {
                        BtnSwitch.Content = "白板";
                        if (isPresentationHaveBlackSpace) {
                            BtnExit.Foreground = Brushes.White;
                            //SymbolIconBtnColorBlackContent.Foreground = Brushes.White;
                            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                        }
                        else {
                            BtnExit.Foreground = Brushes.Black;
                            //SymbolIconBtnColorBlackContent.Foreground = Brushes.White;
                            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                        }
                    }

                    StackPanelPPTButtons.Visibility = Visibility.Visible;
                }

                Topmost = true;
                BtnHideInkCanvas_Click(BtnHideInkCanvas, e);
            }
            else {
                switch (++currentMode % 2) {
                    case 0: //屏幕模式
                        currentMode = 0;
                        GridBackgroundCover.Visibility = Visibility.Collapsed;
                        AnimationsHelper.HideWithSlideAndFade(BlackboardLeftSide);
                        AnimationsHelper.HideWithSlideAndFade(BlackboardCenterSide);
                        AnimationsHelper.HideWithSlideAndFade(BlackboardRightSide);

                        SaveStrokes();
                        ClearStrokes(true);
                        RestoreStrokes(true);

                        if (BtnSwitchTheme.Content.ToString() == "浅色") {
                            BtnSwitch.Content = "黑板";
                            BtnExit.Foreground = Brushes.White;
                            //SymbolIconBtnColorBlackContent.Foreground = Brushes.Black;
                            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                        }
                        else {
                            BtnSwitch.Content = "白板";
                            if (isPresentationHaveBlackSpace) {
                                BtnExit.Foreground = Brushes.White;
                                //SymbolIconBtnColorBlackContent.Foreground = Brushes.White;
                                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                            }
                            else {
                                BtnExit.Foreground = Brushes.Black;
                                //SymbolIconBtnColorBlackContent.Foreground = Brushes.White;
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

                        SaveStrokes(true);
                        ClearStrokes(true);
                        RestoreStrokes();

                        BtnSwitch.Content = "屏幕";
                        if (BtnSwitchTheme.Content.ToString() == "浅色") {
                            BtnExit.Foreground = Brushes.White;
                            //SymbolIconBtnColorBlackContent.Foreground = Brushes.Black;
                            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                        }
                        else {
                            BtnExit.Foreground = Brushes.Black;
                            //SymbolIconBtnColorBlackContent.Foreground = Brushes.White;
                            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                        }

                        if (Settings.Canvas.UsingWhiteboard)
                        {
                            BtnColorBlack_Click(null, null);
                        }
                        else
                        {
                            BtnColorWhite_Click(null, null);
                        }

                        StackPanelPPTButtons.Visibility = Visibility.Collapsed;
                        Topmost = false;
                        break;
                }
            }
        }

        private int BoundsWidth = 5;

        private void BtnHideInkCanvas_Click(object sender, RoutedEventArgs e) {
            if (GridTransparencyFakeBackground.Background == Brushes.Transparent) {
                GridTransparencyFakeBackground.Opacity = 1;
                GridTransparencyFakeBackground.Background = new SolidColorBrush(StringToColor("#01FFFFFF"));
                inkCanvas.IsHitTestVisible = true;
                inkCanvas.Visibility = Visibility.Visible;

                GridBackgroundCoverHolder.Visibility = Visibility.Visible;

                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

                if (GridBackgroundCover.Visibility == Visibility.Collapsed) {
                    if (BtnSwitchTheme.Content.ToString() == "浅色")
                        BtnSwitch.Content = "黑板";
                    else
                        BtnSwitch.Content = "白板";
                    StackPanelPPTButtons.Visibility = Visibility.Visible;
                }
                else {
                    BtnSwitch.Content = "屏幕";
                    StackPanelPPTButtons.Visibility = Visibility.Collapsed;
                }

                BtnHideInkCanvas.Content = "隐藏\n画板";
            }
            else {
                // Auto-clear Strokes 要等待截图完成再清理笔记
                if (BtnPPTSlideShowEnd.Visibility != Visibility.Visible) {
                    if (isLoaded && Settings.Automation.IsAutoClearWhenExitingWritingMode)
                        if (inkCanvas.Strokes.Count > 0) {
                            if (Settings.Automation.IsAutoSaveStrokesAtClear && inkCanvas.Strokes.Count >
                                Settings.Automation.MinimumAutomationStrokeNumber)
                                SaveScreenShot(true);

                            //BtnClear_Click(null, null);
                        }

                    inkCanvas.IsHitTestVisible = true;
                    inkCanvas.Visibility = Visibility.Visible;
                }
                else {
                    if (isLoaded && Settings.Automation.IsAutoClearWhenExitingWritingMode &&
                        !Settings.PowerPointSettings.IsNoClearStrokeOnSelectWhenInPowerPoint)
                        if (inkCanvas.Strokes.Count > 0) {
                            if (Settings.Automation.IsAutoSaveStrokesAtClear && inkCanvas.Strokes.Count >
                                Settings.Automation.MinimumAutomationStrokeNumber)
                                SaveScreenShot(true);

                            //BtnClear_Click(null, null);
                        }


                    if (Settings.PowerPointSettings.IsShowStrokeOnSelectInPowerPoint) {
                        inkCanvas.Visibility = Visibility.Visible;
                        inkCanvas.IsHitTestVisible = true;
                    }
                    else {
                        inkCanvas.IsHitTestVisible = true;
                        inkCanvas.Visibility = Visibility.Visible;
                    }
                }

                GridTransparencyFakeBackground.Opacity = 0;
                GridTransparencyFakeBackground.Background = Brushes.Transparent;

                GridBackgroundCoverHolder.Visibility = Visibility.Collapsed;

                if (currentMode != 0) {
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

            if (GridTransparencyFakeBackground.Background == Brushes.Transparent) {
                StackPanelCanvasControls.Visibility = Visibility.Collapsed;
                CheckEnableTwoFingerGestureBtnVisibility(false);
                HideSubPanels("cursor");
            }
            else {
                AnimationsHelper.ShowWithSlideFromLeftAndFade(StackPanelCanvasControls);
                CheckEnableTwoFingerGestureBtnVisibility(true);
            }
        }

        private void BtnSwitchSide_Click(object sender, RoutedEventArgs e) {
            if (ViewBoxStackPanelMain.HorizontalAlignment == HorizontalAlignment.Right) {
                ViewBoxStackPanelMain.HorizontalAlignment = HorizontalAlignment.Left;
                ViewBoxStackPanelShapes.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else {
                ViewBoxStackPanelMain.HorizontalAlignment = HorizontalAlignment.Right;
                ViewBoxStackPanelShapes.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }

        private void StackPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (((StackPanel)sender).Visibility == Visibility.Visible)
                GridForLeftSideReservedSpace.Visibility = Visibility.Collapsed;
            else
                GridForLeftSideReservedSpace.Visibility = Visibility.Visible;
        }

        #endregion
    }
}