using Ink_Canvas.Properties;
using Ink_Canvas.Controls;
using Ink_Canvas.Controls.Toolbar;
using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Panel = System.Windows.Controls.Panel;
using Point = System.Windows.Point;

namespace Ink_Canvas
{
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        /// <summary>
        /// 当前工具模式
        /// </summary>
        private string _currentToolMode = "cursor";

        private static Windows.SettingsViews.SettingsWindow _settingsWindow = null;

        #region "手勢"按鈕

        /// <summary>
        /// 用於浮動工具欄的"手勢"按鈕和白板工具欄的"手勢"按鈕的點擊事件
        /// </summary>
        internal void TwoFingerGestureBorder_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TwoFingerGestureBorder.IsOpen || BoardTwoFingerGestureBorder.IsOpen)
            {
                AnimationsHelper.HidePopupWithSlideAndFade(TwoFingerGestureBorder);
                AnimationsHelper.HidePopupWithSlideAndFade(BoardTwoFingerGestureBorder);
            }
            else
            {
                HideSubPanels();
                if (currentMode == 0)
                {
                    AnimationsHelper.ShowPopupWithSlideAndFade(TwoFingerGestureBorder);
                    _popupManager?.BringToFront(TwoFingerGestureBorder);
                }
                else
                {
                    AnimationsHelper.ShowPopupWithSlideAndFade(BoardTwoFingerGestureBorder);
                    _popupManager?.BringToFront(BoardTwoFingerGestureBorder);
                }
            }
        }

        /// <summary>
        /// 用於更新浮動工具欄的"手勢"按鈕和白板工具欄的"手勢"按鈕的樣式（開啟和關閉狀態）
        /// </summary>
        private void CheckEnableTwoFingerGestureBtnColorPrompt()
        {
            // 根据主题选择手势图标和颜色
            bool isDarkTheme = Settings.Appearance.Theme == 1 ||
                               (Settings.Appearance.Theme == 2 && !ThemeHelper.IsSystemThemeLight());
            bool isLightTheme = !isDarkTheme;
            string gestureIconPath = isLightTheme ? "/Resources/new-icons/gesture.png" : "/Resources/new-icons/gesture_white.png";

            // 根据主题设置白板模式下的颜色
            Color boardBgColor, boardIconColor, boardTextColor, boardBorderColor;
            if (isLightTheme)
            {
                boardBgColor = Color.FromRgb(244, 244, 245);
                boardIconColor = Color.FromRgb(24, 24, 27);
                boardTextColor = Color.FromRgb(24, 24, 27);
                boardBorderColor = Color.FromRgb(161, 161, 170);
            }
            else
            {
                boardBgColor = Color.FromRgb(39, 39, 42);
                boardIconColor = Color.FromRgb(244, 244, 245);
                boardTextColor = Color.FromRgb(244, 244, 245);
                boardBorderColor = Color.FromRgb(113, 113, 122);
            }

            bool floatingBarAnyOn = Settings.Gesture.IsEnableMultiTouchMode
                || Settings.Gesture.IsEnableTwoFingerZoom
                || Settings.Gesture.IsEnableTwoFingerTranslate
                || Settings.Gesture.IsEnableTwoFingerRotation;
            bool boardAnyOn = Settings.Gesture.IsEnableMultiTouchModeBoard
                || Settings.Gesture.IsEnableTwoFingerZoomBoard
                || Settings.Gesture.IsEnableTwoFingerTranslateBoard
                || Settings.Gesture.IsEnableTwoFingerRotationBoard;

            TwoFingerGestureSimpleStackPanel.Opacity = 1;
            TwoFingerGestureSimpleStackPanel.IsHitTestVisible = true;

            if (floatingBarAnyOn)
            {
                if (Gesture_Icon != null)
                {
                    Gesture_Icon.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.EnabledGestureIcon);
                    Gesture_Icon.Badge.Geometry = Geometry.Parse("F0 M24,24z M0,0z " + XamlGraphicsIconGeometries.EnabledGestureIconBadgeCheck);
                    Gesture_Icon.IconBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                    Gesture_Icon.Badge.Brush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                }
            }
            else
            {
                if (Gesture_Icon != null)
                {
                    Gesture_Icon.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.DisabledGestureIcon);
                    Gesture_Icon.Badge.Geometry = Geometry.Parse("F0 M24,24z M0,0z");
                    Gesture_Icon.IconBrush = isDarkTheme
                        ? new SolidColorBrush(Color.FromRgb(244, 244, 245))
                        : new SolidColorBrush(Color.FromRgb(24, 24, 27));
                }
            }

            if (boardAnyOn)
            {
                BoardGesture.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                BoardGesture.IconGeometryDrawing.Brush = new SolidColorBrush(Colors.GhostWhite);
                BoardGesture.IconGeometryDrawing2.Brush = new SolidColorBrush(Colors.GhostWhite);
                BoardGesture.Foreground = new SolidColorBrush(Colors.GhostWhite);
                BoardGesture.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                BoardGesture.IconGeometryDrawing.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.EnabledGestureIcon);
                BoardGesture.IconGeometryDrawing2.Geometry = Geometry.Parse("F0 M24,24z M0,0z " + XamlGraphicsIconGeometries.EnabledGestureIconBadgeCheck);
            }
            else
            {
                BoardGesture.Background = new SolidColorBrush(boardBgColor);
                BoardGesture.IconGeometryDrawing.Brush = new SolidColorBrush(boardIconColor);
                BoardGesture.IconGeometryDrawing2.Brush = new SolidColorBrush(boardIconColor);
                BoardGesture.Foreground = new SolidColorBrush(boardTextColor);
                BoardGesture.BorderBrush = new SolidColorBrush(boardBorderColor);
                BoardGesture.IconGeometryDrawing.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.DisabledGestureIcon);
                BoardGesture.IconGeometryDrawing2.Geometry = Geometry.Parse("F0 M24,24z M0,0z");
            }
        }

        /// <summary>
        /// 控制是否顯示浮動工具欄的"手勢"按鈕
        /// </summary>
        private void CheckEnableTwoFingerGestureBtnVisibility(bool isVisible)
        {
            UpdateToolbarComponentVisibility();
        }

        #endregion "手勢"按鈕

        #region 浮動工具欄的拖動實現

        /// <summary>
        /// 是否正在拖动浮动工具栏
        /// </summary>
        private bool isDragDropInEffect;
        /// <summary>
        /// 当前位置
        /// </summary>
        private Point pos;
        /// <summary>
        /// 按下鼠标时的位置
        /// </summary>
        private Point downPos;
        /// <summary>
        /// 用于记录上次在桌面时的坐标
        /// </summary>
        private Point pointDesktop = new Point(-1, -1);
        /// <summary>
        /// 用于记录上次在PPT中的坐标
        /// </summary>
        private Point pointPPT = new Point(-1, -1);
        private DispatcherTimer _floatingBarScreenFollowTimer;
        private string _lastFloatingBarScreenDeviceName;
        private string _lastCanvasScreenDeviceName;
        private bool _isRebuildingCanvasForScreen;

        /// <summary>
        /// Popup 管理器（负责置顶和拖动跟随）
        /// </summary>
        private double _cachedFloatingBarWidth;
        private double _cachedFloatingBarHeadWidth;
        private double _cachedScreenWidth;
        private DateTime _lastFloatingBarSizeCacheTime;

        private void RefreshFloatingBarSizeCache(bool force = false)
        {
            var now = DateTime.Now;
            if (!force && (now - _lastFloatingBarSizeCacheTime).TotalMilliseconds < 100)
                return;

            var scale = GetFloatingBarScaleX();
            _cachedFloatingBarWidth = GetElementWidthForFloatingBar(ViewboxFloatingBar, 200) * scale;
            var dragElement = FindDragHandleInRoot();
            _cachedFloatingBarHeadWidth = GetElementWidthForFloatingBar(dragElement, 50) * scale;
            _cachedScreenWidth = GetFloatingBarScreenWidth(Settings.Advanced.IsEnableAvoidFullScreenHelper);
            _lastFloatingBarSizeCacheTime = now;
        }

        private PopupManagerHelper _popupManager;

        /// <summary>
        /// 浮动工具栏移动事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标事件参数</param>
        private void SymbolIconEmoji_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragDropInEffect)
            {
                var currentPos = e.GetPosition(null);
                var xPos = currentPos.X - pos.X + ViewboxFloatingBar.Margin.Left;
                var yPos = currentPos.Y - pos.Y + ViewboxFloatingBar.Margin.Top;
                ViewboxFloatingBar.Margin = new Thickness(xPos, yPos, -2000, -200);

                pos = currentPos;

                if (IsFloatingBarContentVisible())
                {
                    RefreshFloatingBarSizeCache();

                    var headLeft = ViewboxFloatingBar.Margin.Left + (isFloatingBarHeadOnRight ? Math.Max(0, _cachedFloatingBarWidth - _cachedFloatingBarHeadWidth) : 0);

                    bool shouldFlip;
                    if (!isFloatingBarHeadOnRight && headLeft + _cachedFloatingBarWidth > _cachedScreenWidth)
                        shouldFlip = true;
                    else if (isFloatingBarHeadOnRight && headLeft + _cachedFloatingBarWidth <= _cachedScreenWidth)
                        shouldFlip = false;
                    else
                        shouldFlip = isFloatingBarHeadOnRight;

                    if (shouldFlip != isFloatingBarHeadOnRight)
                    {
                        var savedHeadLeft = headLeft;
                        SetFloatingBarHeadPlacement(shouldFlip);

                        RefreshFloatingBarSizeCache(true);

                        double newLeft;
                        if (shouldFlip)
                            newLeft = savedHeadLeft - Math.Max(0, _cachedFloatingBarWidth - _cachedFloatingBarHeadWidth);
                        else
                            newLeft = savedHeadLeft;

                        newLeft = ClampFloatingBarLeft(newLeft, _cachedFloatingBarWidth, _cachedScreenWidth);
                        ViewboxFloatingBar.Margin = new Thickness(newLeft, ViewboxFloatingBar.Margin.Top, -2000, -200);
                    }
                }

                var currentMargin = ViewboxFloatingBar.Margin;
                if (IsInPptPresentationMode)
                    pointPPT = new Point(currentMargin.Left, currentMargin.Top);
                else
                    pointDesktop = new Point(currentMargin.Left, currentMargin.Top);

                _popupManager?.MarkNeedsUpdate();

                if (BorderTools.IsOpen) _popupManager?.BringToFront(BorderTools);
                if (BoardBorderToolsPopup.IsOpen) _popupManager?.BringToFront(BoardBorderToolsPopup);
                if (BorderDrawShape.IsOpen) _popupManager?.BringToFront(BorderDrawShape);
                if (BoardBorderDrawShape.IsOpen) _popupManager?.BringToFront(BoardBorderDrawShape);
            }
        }

        /// <summary>
        /// 初始化 Popup 管理器（创建实例、注册 Popup、启动跟随系统）
        /// 在 Window_Loaded 中调用一次
        /// </summary>
        internal void InitializePopupManager()
        {
            try
            {
                _popupManager = new PopupManagerHelper();

                _popupManager.ShouldBeTopmost = () => Settings.Advanced.IsAlwaysOnTop;

                _popupManager.RegisterPopup(BorderTools);
                _popupManager.RegisterPopup(BoardBorderToolsPopup);
                _popupManager.RegisterPopup(BorderDrawShape);
                _popupManager.RegisterPopup(BoardBorderDrawShape);
                _popupManager.RegisterPopup(PenPalette);
                _popupManager.RegisterPopup(BoardPenPalette);
                _popupManager.RegisterPopup(EraserSizePanel);
                _popupManager.RegisterPopup(BoardEraserSizePanel);
                _popupManager.RegisterPopup(BoardImageOptionsPanel);
                _popupManager.RegisterPopup(TwoFingerGestureBorder);
                _popupManager.RegisterPopup(BoardTwoFingerGestureBorder);
                _popupManager.RegisterPopup(BackgroundPalette);

                _popupManager.Initialize(this);

                System.Diagnostics.Debug.WriteLine("[PopupManager] Initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupManager] Initialize error: {ex.Message}");
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

        /// <summary>
        /// 浮动工具栏鼠标释放事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        internal void SymbolIconEmoji_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isDragDropInEffect = false;

            var isClick = e is null || (Math.Abs(downPos.X - e.GetPosition(null).X) <= 10 &&
                                        Math.Abs(downPos.Y - e.GetPosition(null).Y) <= 10);

            if (isClick)
            {
                var headLeft = GetCurrentFloatingBarHeadLeft();
                if (IsFloatingBarContentVisible())
                {
                    SetFloatingBarContentVisibility(false);
                    UpdateToolbarComponentVisibility();
                    PlaceFloatingBarAfterHeadToggle(headLeft, false);
                }
                else
                {
                    SetFloatingBarContentVisibility(true);
                    UpdateToolbarComponentVisibility();
                    PlaceFloatingBarAfterHeadToggle(headLeft, true);
                }
            }
            else
            {
                PlaceFloatingBarAfterHeadToggle(
                    GetCurrentFloatingBarHeadLeft(),
                    IsFloatingBarContentVisible());
                _popupManager?.MarkNeedsUpdate();
            }

            // 每次点击或拖动结束后都重新定位高光
            SetFloatingBarHighlightPosition(_currentToolMode);

            GridForFloatingBarDraging.Visibility = Visibility.Collapsed;
        }

        #endregion 浮動工具欄的拖動實現

        #region 隱藏子面板和按鈕背景高亮

        /// <summary>
        /// 隐藏形状绘制面板
        /// </summary>
        private void CollapseBorderDrawShape()
        {
            AnimationsHelper.HidePopupWithSlideAndFade(BorderDrawShape);
            AnimationsHelper.HidePopupWithSlideAndFade(BoardBorderDrawShape);
        }

        /// <summary>
        /// HideSubPanels的简化版，立即隐藏所有子面板，无动画效果
        /// </summary>
        private void HideSubPanelsImmediately()
        {
            BorderTools.IsOpen = false;
            BoardBorderToolsPopup.IsOpen = false;
            PenPalette.IsOpen = false;
            BoardPenPalette.IsOpen = false;
            BoardEraserSizePanel.IsOpen = false;
            EraserSizePanel.IsOpen = false;
            BoardBorderLeftPageListView.Visibility = Visibility.Collapsed;
            BoardBorderRightPageListView.Visibility = Visibility.Collapsed;
            BoardImageOptionsPanel.IsOpen = false;
            TwoFingerGestureBorder.IsOpen = false;
            BoardTwoFingerGestureBorder.IsOpen = false;
            // 添加隐藏图形工具的二级菜单面板
            BorderDrawShape.IsOpen = false;
            BoardBorderDrawShape.IsOpen = false;

            BackgroundPalette.IsOpen = false;
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
        internal async void HideSubPanels(string mode = null, bool autoAlignCenter = false)
        {
            mode = NormalizeToolModeForFreeze(mode);

            AnimationsHelper.HidePopupWithSlideAndFade(BorderTools);
            AnimationsHelper.HidePopupWithSlideAndFade(BoardBorderToolsPopup);
            AnimationsHelper.HidePopupWithSlideAndFade(PenPalette);
            AnimationsHelper.HidePopupWithSlideAndFade(BoardPenPalette);
            AnimationsHelper.HidePopupWithSlideAndFade(BoardEraserSizePanel);
            AnimationsHelper.HidePopupWithSlideAndFade(EraserSizePanel);
            AnimationsHelper.HidePopupWithSlideAndFade(BorderDrawShape);
            AnimationsHelper.HidePopupWithSlideAndFade(BoardBorderDrawShape);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderLeftPageListView);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderRightPageListView);
            AnimationsHelper.HidePopupWithSlideAndFade(BoardImageOptionsPanel);
            AnimationsHelper.HidePopupWithSlideAndFade(TwoFingerGestureBorder);
            AnimationsHelper.HidePopupWithSlideAndFade(BoardTwoFingerGestureBorder);

            AnimationsHelper.HidePopupWithSlideAndFade(BackgroundPalette);

            if (mode != null)
            {
                if (mode != "clear")
                {
                    if (Cursor_Icon != null) { Cursor_Icon.Icon.Brush = new SolidColorBrush(FloatBarForegroundColor); Cursor_Icon.Icon.Geometry = Geometry.Parse(GetCorrectIcon("cursor", false)); }
                    if (Pen_Icon != null) { Pen_Icon.Icon.Brush = new SolidColorBrush(FloatBarForegroundColor); Pen_Icon.Icon.Geometry = Geometry.Parse(GetCorrectIcon("pen", false)); }
                    if (EraserByStrokes_Icon != null) { EraserByStrokes_Icon.Icon.Brush = new SolidColorBrush(FloatBarForegroundColor); EraserByStrokes_Icon.Icon.Geometry = Geometry.Parse(GetCorrectIcon("eraserStroke", false)); }
                    if (Eraser_Icon != null) { Eraser_Icon.Icon.Brush = new SolidColorBrush(FloatBarForegroundColor); Eraser_Icon.Icon.Geometry = Geometry.Parse(GetCorrectIcon("eraserCircle", false)); }
                    if (SymbolIconSelect != null) { SymbolIconSelect.Icon.Brush = new SolidColorBrush(FloatBarForegroundColor); SymbolIconSelect.Icon.Geometry = Geometry.Parse(GetCorrectIcon("lassoSelect", false)); }

                    bool isDarkThemeForButtons = Settings.Appearance.Theme == 1 ||
                                                 (Settings.Appearance.Theme == 2 && !ThemeHelper.IsSystemThemeLight());
                    if (isDarkThemeForButtons)
                    {
                        BoardPen.Background = new SolidColorBrush(Color.FromRgb(42, 42, 42));
                        BoardSelect.Background = new SolidColorBrush(Color.FromRgb(42, 42, 42));
                        BoardEraser.Background = new SolidColorBrush(Color.FromRgb(42, 42, 42));
                        BoardStrokeEraser.Background = new SolidColorBrush(Color.FromRgb(42, 42, 42));
                        BoardSelect.IconGeometryDrawing.Brush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                        BoardPen.IconGeometryDrawing.Brush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                        BoardEraser.IconGeometryDrawing.Brush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                        BoardStrokeEraser.IconGeometryDrawing.Brush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                        BoardPen.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                        BoardSelect.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                        BoardEraser.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                        BoardStrokeEraser.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                        BoardSelect.BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85));
                        BoardEraser.BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85));
                        BoardStrokeEraser.BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85));
                        BoardPen.BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85));
                    }
                    else
                    {
                        BoardPen.Background = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                        BoardSelect.Background = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                        BoardEraser.Background = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                        BoardStrokeEraser.Background = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                        BoardSelect.IconGeometryDrawing.Brush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                        BoardPen.IconGeometryDrawing.Brush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                        BoardEraser.IconGeometryDrawing.Brush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                        BoardStrokeEraser.IconGeometryDrawing.Brush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                        BoardPen.Foreground = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                        BoardSelect.Foreground = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                        BoardEraser.Foreground = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                        BoardStrokeEraser.Foreground = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                        BoardSelect.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                        BoardEraser.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                        BoardStrokeEraser.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                        BoardPen.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                    }
                }

                // 根据主题选择高光颜色
                Color highlightColor;
                bool isDarkTheme = Settings.Appearance.Theme == 1 ||
                                   (Settings.Appearance.Theme == 2 && !ThemeHelper.IsSystemThemeLight());

                if (isDarkTheme)
                {
                    highlightColor = Color.FromRgb(102, 204, 255); // #66ccff for dark theme
                }
                else
                {
                    highlightColor = Color.FromRgb(30, 58, 138); // Keep current color for light theme
                }

                switch (mode)
                {
                    case "pen":
                    case "color":
                        {
                            Pen_Icon.Icon.Brush = new SolidColorBrush(highlightColor);
                            Pen_Icon.Icon.Geometry = Geometry.Parse(GetCorrectIcon("pen", true));
                            BoardPen.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                            BoardPen.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                            BoardPen.IconGeometryDrawing.Brush = new SolidColorBrush(Colors.GhostWhite);
                            BoardPen.Foreground = new SolidColorBrush(Colors.GhostWhite);

                            SetFloatingBarHighlightPosition("pen");
                            break;
                        }
                    case "eraser":
                        {
                            Eraser_Icon.Icon.Brush = new SolidColorBrush(highlightColor);
                            Eraser_Icon.Icon.Geometry =
                                Geometry.Parse(GetCorrectIcon("eraserCircle", true));
                            BoardEraser.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                            BoardEraser.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                            BoardEraser.IconGeometryDrawing.Brush = new SolidColorBrush(Colors.GhostWhite);
                            BoardEraser.Foreground = new SolidColorBrush(Colors.GhostWhite);

                            SetFloatingBarHighlightPosition("eraser");
                            break;
                        }
                    case "eraserByStrokes":
                        {
                            EraserByStrokes_Icon.Icon.Brush = new SolidColorBrush(highlightColor);
                            EraserByStrokes_Icon.Icon.Geometry =
                                Geometry.Parse(GetCorrectIcon("eraserStroke", true));
                            BoardStrokeEraser.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                            BoardStrokeEraser.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                            BoardStrokeEraser.IconGeometryDrawing.Brush = new SolidColorBrush(Colors.GhostWhite);
                            BoardStrokeEraser.Foreground = new SolidColorBrush(Colors.GhostWhite);

                            SetFloatingBarHighlightPosition("eraserByStrokes");
                            break;
                        }
                    case "select":
                        {
                            SymbolIconSelect.Icon.Brush = new SolidColorBrush(highlightColor);
                            SymbolIconSelect.Icon.Geometry =
                                Geometry.Parse(GetCorrectIcon("lassoSelect", true));
                            BoardSelect.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                            BoardSelect.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                            BoardSelect.IconGeometryDrawing.Brush = new SolidColorBrush(Colors.GhostWhite);
                            BoardSelect.Foreground = new SolidColorBrush(Colors.GhostWhite);

                            SetFloatingBarHighlightPosition("select");
                            break;
                        }
                    case "cursor":
                        {
                            Cursor_Icon.Icon.Brush = new SolidColorBrush(highlightColor);
                            Cursor_Icon.Icon.Geometry =
                                Geometry.Parse(GetCorrectIcon("cursor", true));
                            bool isDarkThemeForCursor = Settings.Appearance.Theme == 1 ||
                                                        (Settings.Appearance.Theme == 2 && !ThemeHelper.IsSystemThemeLight());
                            if (isDarkThemeForCursor)
                            {
                                BoardPen.Background = new SolidColorBrush(Color.FromRgb(42, 42, 42));
                                BoardPen.BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85));
                                BoardPen.IconGeometryDrawing.Brush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                                BoardPen.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));

                                BoardEraser.Background = new SolidColorBrush(Color.FromRgb(42, 42, 42));
                                BoardEraser.BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85));
                                BoardEraser.IconGeometryDrawing.Brush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                                BoardEraser.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));

                                BoardStrokeEraser.Background = new SolidColorBrush(Color.FromRgb(42, 42, 42));
                                BoardStrokeEraser.BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85));
                                BoardStrokeEraser.IconGeometryDrawing.Brush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                                BoardStrokeEraser.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));

                                BoardSelect.Background = new SolidColorBrush(Color.FromRgb(42, 42, 42));
                                BoardSelect.BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85));
                                BoardSelect.IconGeometryDrawing.Brush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                                BoardSelect.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));

                                if (BoardInkFreezeBtn != null)
                                {
                                    BoardInkFreezeBtn.Background = new SolidColorBrush(Color.FromRgb(42, 42, 42));
                                    BoardInkFreezeBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85));
                                    BoardInkFreezeBtn.IconBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                                    BoardInkFreezeBtn.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                                }
                            }
                            else
                            {
                                BoardPen.Background = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                                BoardPen.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                                BoardPen.IconGeometryDrawing.Brush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                                BoardPen.Foreground = new SolidColorBrush(Color.FromRgb(24, 24, 27));

                                BoardEraser.Background = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                                BoardEraser.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                                BoardEraser.IconGeometryDrawing.Brush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                                BoardEraser.Foreground = new SolidColorBrush(Color.FromRgb(24, 24, 27));

                                BoardStrokeEraser.Background = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                                BoardStrokeEraser.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                                BoardStrokeEraser.IconGeometryDrawing.Brush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                                BoardStrokeEraser.Foreground = new SolidColorBrush(Color.FromRgb(24, 24, 27));

                                BoardSelect.Background = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                                BoardSelect.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                                BoardSelect.IconGeometryDrawing.Brush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                                BoardSelect.Foreground = new SolidColorBrush(Color.FromRgb(24, 24, 27));

                                if (BoardInkFreezeBtn != null)
                                {
                                    BoardInkFreezeBtn.Background = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                                    BoardInkFreezeBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                                    BoardInkFreezeBtn.IconBrush = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                                    BoardInkFreezeBtn.Foreground = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                                }
                            }

                            SetFloatingBarHighlightPosition("cursor");
                            break;
                        }
                    case "shape":
                        {
                            break;
                        }
                }


                if (autoAlignCenter) // 控制居中
                {
                    if (IsInPptPresentationMode)
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

        /// <summary>
        /// 撤销按钮点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        internal void SymbolIconUndo_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("撤销冻结页面内容")) return;
            if (!IsUndoEnabled) return;
            BtnUndo_Click(null, null);
            HideSubPanels();
        }

        /// <summary>
        /// 重做按钮点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        internal void SymbolIconRedo_MouseUp(object sender, RoutedEventArgs e)
        {
            if (TryBlockFrozenPageMutation("重做冻结页面内容")) return;
            if (!IsRedoEnabled) return;
            BtnRedo_Click(null, null);
            HideSubPanels();
        }

        #endregion

        #region 白板按鈕和退出白板模式按鈕

        /// <summary>
        /// 是否正在显示或隐藏黑板
        /// </summary>
        private bool isDisplayingOrHidingBlackboard;

        /// <summary>
        /// 白板按钮点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        internal void ImageBlackboard_MouseUp(object sender, MouseButtonEventArgs e)
        {

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

                    BtnHideInkCanvas_Click(null, null);
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

                _ = UpdateChickenSoupTextAsync().ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        try
                        {
                            LogHelper.WriteLogToFile($"进入白板模式时更新名言失败: {t.Exception?.GetBaseException().Message}", LogHelper.LogType.Warning);
                        }
                        catch
                        {
                        }
                        if (Settings.Appearance.EnableChickenSoupInWhiteboardMode && Settings.Appearance.ChickenSoupSource != 3)
                        {
                            try
                            {
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
                                else if (Settings.Appearance.ChickenSoupSource == 4)
                                {
                                    int randChickenSoupIndex = new Random().Next(ChickenSoup.PhigrosTips.Length);
                                    BlackBoardWaterMark.Text = ChickenSoup.PhigrosTips[randChickenSoupIndex];
                                }
                            }
                            catch
                            {
                                BlackBoardWaterMark.Visibility = Visibility.Collapsed;
                            }
                        }
                        else if (Settings.Appearance.EnableChickenSoupInWhiteboardMode && Settings.Appearance.ChickenSoupSource == 3)
                        {
                            BlackBoardWaterMark.Text = Properties.MainWindowStrings.Main_Hitokoto_Unavailable;
                        }
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());

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

                ViewboxFloatingBar.Visibility = Visibility.Collapsed;
            }
            else
            {
                //关闭黑板
                HideSubPanelsImmediately();

                // 只有在PPT放映模式下且页数有效时才显示翻页按钮
                if (ArePptControlsVisible &&
                    IsInPptPresentationMode &&
                    PPTManager?.IsInSlideShow == true &&
                    PPTManager?.SlidesCount > 0)
                {
                    var dops = Settings.PowerPointSettings.PPTButtonsDisplayOption.ToString();
                    var dopsc = dops.ToCharArray();
                    if (dopsc[0] == '2' && !isDisplayingOrHidingBlackboard) AnimationsHelper.ShowWithFadeIn(LeftBottomPanelForPPTNavigation);
                    if (dopsc[1] == '2' && !isDisplayingOrHidingBlackboard) AnimationsHelper.ShowWithFadeIn(RightBottomPanelForPPTNavigation);
                    if (dopsc[2] == '2' && !isDisplayingOrHidingBlackboard) AnimationsHelper.ShowWithFadeIn(LeftSidePanelForPPTNavigation);
                    if (dopsc[3] == '2' && !isDisplayingOrHidingBlackboard) AnimationsHelper.ShowWithFadeIn(RightSidePanelForPPTNavigation);
                }
                else
                {
                    // 如果不在放映模式或页数无效，隐藏所有翻页按钮
                    LeftBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                    RightBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                    LeftSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                    RightSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                }

                // 使用PPT UI管理器来正确更新翻页按钮显示状态，确保遵循用户设置
                _pptUIManager?.UpdateNavigationPanelsVisibility();

                if (Settings.Automation.IsAutoSaveScreenshotAtClear &&
                    inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber) CaptureAndEnqueueScreenshotSave(true);

                if (!IsInPptPresentationMode)
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

                if (GetSelectionBGLeft() != 28) PenIcon_Click(null, null);

                WaterMarkTime.Visibility = Visibility.Collapsed;
                WaterMarkDate.Visibility = Visibility.Collapsed;
                BlackBoardWaterMark.Visibility = Visibility.Collapsed;
                ICCWaterMarkDark.Visibility = Visibility.Collapsed;
                ICCWaterMarkWhite.Visibility = Visibility.Collapsed;

                // 新增：退出白板模式时恢复基础浮动栏的显示
                ViewboxFloatingBar.Visibility = Visibility.Visible;
            }

            SwitchBackground(null, null);

            if (currentMode == 0)
            {
                // 根据当前编辑模式正确设置工具模式和高光位置
                if (inkCanvas.EditingMode == InkCanvasEditingMode.None)
                {
                    UpdateCurrentToolMode("cursor");
                    SetFloatingBarHighlightPosition("cursor");
                }
                else if (inkCanvas.EditingMode == InkCanvasEditingMode.Ink)
                {
                    UpdateCurrentToolMode("pen");
                    SetFloatingBarHighlightPosition("pen");
                }
                else if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
                {
                    UpdateCurrentToolMode("eraser");
                    SetFloatingBarHighlightPosition("eraser");
                }
                else if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke)
                {
                    UpdateCurrentToolMode("eraserByStrokes");
                    SetFloatingBarHighlightPosition("eraserByStrokes");
                }
                else if (inkCanvas.EditingMode == InkCanvasEditingMode.Select)
                {
                    UpdateCurrentToolMode("select");
                    SetFloatingBarHighlightPosition("select");
                }
            }

            if (currentMode == 0 && inkCanvas.Strokes.Count == 0 && !IsInPptPresentationMode)
                CursorIcon_Click(null, null);

            { /* Old UI removed */ }
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
        /// <summary>
        /// 光标图标点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        private async void SymbolIconCursor_Click(object sender, RoutedEventArgs e)
        {
            if (currentMode != 0)
            {
                ImageBlackboard_MouseUp(null, null);
            }
            else
            {
                BtnHideInkCanvas_Click(null, null);

                if (IsInPptPresentationMode)
                {
                    await Task.Delay(100);
                    ViewboxFloatingBarMarginAnimation(60);
                }
            }
        }

        #region 清空畫布按鈕

        /// <summary>
        /// 清空画布按钮点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        internal void SymbolIconDelete_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("清除冻结页面内容")) return;
            if (inkCanvas.GetSelectedStrokes().Count > 0)
            {
                inkCanvas.Strokes.Remove(inkCanvas.GetSelectedStrokes());
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
            }
            else if (inkCanvas.Strokes.Count > 0)
            {
                if (Settings.Automation.IsAutoSaveScreenshotAtClear &&
                    inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber)
                {
                    if (IsInPptPresentationMode)
                    {
                        var currentSlide = _pptManager?.GetCurrentSlideNumber() ?? 0;
                        var presentationName = _pptManager?.GetPresentationName() ?? "";
                        CaptureAndEnqueueScreenshotSave(true, $"{presentationName}/{currentSlide}_{DateTime.Now:HH-mm-ss}");
                    }
                    else
                        CaptureAndEnqueueScreenshotSave(true);
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
            PenIcon_Click(null, null);
            SymbolIconDelete_MouseUp(null, null);
        }

        #region 主要的工具按鈕事件

        /// <summary>
        /// 浮动工具栏的"套索选"按钮事件，重定向到旧UI的BtnSelect_Click方法
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        internal void SymbolIconSelect_MouseUp(object sender, MouseButtonEventArgs e)
        {

            if (lastBorderMouseDownObject is Panel panel)
                panel.Background = new SolidColorBrush(Colors.Transparent);

            BtnSelect_Click(null, null);

            // 更新模式缓存
            UpdateCurrentToolMode("select");

            HideSubPanels("select");

        }

        #endregion

        /// <summary>
        /// 浮动工具栏按钮鼠标按下反馈效果处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        private void FloatingBarToolBtnMouseDownFeedback_Panel(object sender, MouseButtonEventArgs e)
        {
            if (sender is Panel panel)
            {
                lastBorderMouseDownObject = sender;
                panel.Background = new SolidColorBrush(Color.FromArgb(28, 24, 24, 27));
            }
            else if (sender is Border border)
            {
                lastBorderMouseDownObject = sender;
                if (border.Name?.StartsWith("QuickColor") == true)
                {
                    if (border.Background is SolidColorBrush originalColor)
                    {
                        border.Background = new SolidColorBrush(Color.FromArgb(180, originalColor.Color.R, originalColor.Color.G, originalColor.Color.B));
                    }
                }
                else
                {
                    border.Background = new SolidColorBrush(Color.FromArgb(28, 24, 24, 27));
                }
            }
            else if (sender is Ink_Canvas.Controls.ColorPickerButton colorPicker)
            {
                lastBorderMouseDownObject = sender;
            }
        }

        /// <summary>
        /// 浮动工具栏按钮鼠标离开反馈效果处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标事件参数</param>
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
            else if (sender is Ink_Canvas.Controls.ColorPickerButton colorPicker)
            {
                lastBorderMouseDownObject = null;
            }
        }

        /// <summary>
        /// 设置图标点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        private void SymbolIconSettings_Click(object sender, MouseButtonEventArgs e)
        {
            HideSubPanels();
            BtnSettings_Click(null, null);
        }
        /// <summary>
        /// 截图图标点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        private async void SymbolIconScreenshot_MouseUp(object sender, MouseButtonEventArgs e)
        {
            HideSubPanelsImmediately();
            await Task.Delay(50);

            // 白板模式下默认全屏截图到桌面；其余模式默认调用可选区截图
            if (currentMode == 1)
            {
                SaveScreenShotToDesktop();
            }
            else
            {
                await SaveAreaScreenShotToDesktop();
            }
        }

        /// <summary>
        /// 倒计时计时器图标点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        private void ImageCountdownTimer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            LeftUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            RightUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            AnimationsHelper.HidePopupWithSlideAndFade(BorderTools);
            AnimationsHelper.HidePopupWithSlideAndFade(BoardBorderToolsPopup);
            AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);

            if (Settings.RandSettings?.UseNewStyleUI == true)
            {
                if (TimerContainer != null && TimerControl != null)
                {
                    // 每次打开计时器窗口时重置计时器
                    TimerControl.ResetTimerState();

                    // 根据DPI缩放因子调整TimerContainer的尺寸
                    AdjustTimerContainerSize();

                    TimerContainer.Visibility = Visibility.Visible;
                    if (MinimizedTimerContainer != null)
                    {
                        MinimizedTimerContainer.Visibility = Visibility.Collapsed;
                    }
                    TimerControl.CloseRequested += (s, args) =>
                    {
                        TimerContainer.Visibility = Visibility.Collapsed;
                        if (MinimizedTimerContainer != null)
                        {
                            MinimizedTimerContainer.Visibility = Visibility.Collapsed;
                        }
                    };
                }
            }
            else
            {
                if (currentMode == 1)
                {
                    Topmost = false;
                }

                var timerWindow = CountdownTimerWindow.CreateTimerWindow();
                timerWindow.Show();
                if (currentMode == 1)
                {
                    timerWindow.Topmost = true;
                }
            }
        }

        /// <summary>
        /// 操作指南窗口图标点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        private void OperatingGuideWindowIcon_MouseUp(object sender, MouseButtonEventArgs e)
        {
            AnimationsHelper.HidePopupWithSlideAndFade(BorderTools);
            AnimationsHelper.HidePopupWithSlideAndFade(BoardBorderToolsPopup);
            AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);

            new OperatingGuideWindow().Show();
        }

        /// <summary>
        /// 随机点名图标点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        private void SymbolIconRand_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // 如果控件被隐藏，不处理事件
            if (BoardRandomDrawToolBtn.Visibility != Visibility.Visible) return;

            LeftUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            RightUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;

            AnimationsHelper.HidePopupWithSlideAndFade(BorderTools);
            AnimationsHelper.HidePopupWithSlideAndFade(BoardBorderToolsPopup);
            AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);

            // 根据设置决定使用哪个点名窗口
            if (Settings.RandSettings.UseNewRollCallUI)
            {
                // 使用新点名UI - 随机抽模式
                var rollCallWindow = new NewStyleRollCallWindow(Settings, false);
                rollCallWindow.Owner = this;
                rollCallWindow.ShowDialog();
            }
            else
            {
                // 使用默认的随机点名窗口
                var randWindow = new RandWindow(Settings);
                randWindow.Show();

                // 使用延迟确保窗口完全显示后再强制置顶
                randWindow.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // 强制激活窗口
                    randWindow.Activate();
                    randWindow.Focus();

                    // 设置置顶
                    randWindow.Topmost = true;

                    // 使用Win32 API强制置顶
                    var hwnd = new WindowInteropHelper(randWindow).Handle;
                    if (hwnd != IntPtr.Zero)
                    {
                        const int WS_EX_TOPMOST = 0x00000008;
                        const int GWL_EXSTYLE = -20;
                        const int SWP_NOMOVE = 0x0002;
                        const int SWP_NOSIZE = 0x0001;
                        const int SWP_SHOWWINDOW = 0x0040;
                        const int SWP_NOOWNERZORDER = 0x0200;
                        var HWND_TOPMOST = new IntPtr(-1);

                        // 设置窗口样式为置顶
                        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOPMOST);

                        // 强制置顶
                        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                            SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"强制置顶RandWindow失败: {ex.Message}", LogHelper.LogType.Error);
                }
            }), DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// 检查并更新橡皮擦类型标签的状态
        /// </summary>
        public void CheckEraserTypeTab()
        {
            if (EraserTypeTab != null)
                EraserTypeTab.SelectedIndex = Settings.Canvas.EraserShapeType;
            if (BoardEraserTypeTab != null)
                BoardEraserTypeTab.SelectedIndex = Settings.Canvas.EraserShapeType;
        }

        /// <summary>
        /// 单次点名图标点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        private void SymbolIconRandOne_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // 如果控件被隐藏，不处理事件
            if (BoardSingleDrawToolBtn.Visibility != Visibility.Visible) return;

            LeftUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            RightUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;

            AnimationsHelper.HidePopupWithSlideAndFade(BorderTools);
            AnimationsHelper.HidePopupWithSlideAndFade(BoardBorderToolsPopup);
            AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);

            // 检查是否启用了外部点名功能
            if (Settings.RandSettings.DirectCallCiRand)
            {
                try
                {
                    string[] protocols;
                    switch (Settings.RandSettings.ExternalCallerType)
                    {
                        case 0: // ClassIsland点名
                            protocols = ExternalCallerLauncher.GetProtocolsByType(0);
                            break;
                        case 1: // SecRandom点名
                            protocols = ExternalCallerLauncher.GetProtocolsByType(1);
                            break;
                        case 2: // NamePicker点名
                            protocols = ExternalCallerLauncher.GetProtocolsByType(2);
                            break;
                        default:
                            protocols = ExternalCallerLauncher.GetProtocolsByType(0);
                            break;
                    }

                    if (!ExternalCallerLauncher.TryLaunch(protocols, out Exception lastException))
                    {
                        throw lastException ?? new InvalidOperationException("external caller protocols are unavailable");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(Properties.MainWindowStrings.Main_RollCall_CannotCallExternal, ex.Message));

                    // 调用失败时回退到相应的点名窗口
                    if (Settings.RandSettings.UseNewRollCallUI)
                    {
                        var rollCallWindow = new NewStyleRollCallWindow(Settings, true); // 单次抽模式
                        rollCallWindow.Owner = this;
                        rollCallWindow.ShowDialog();
                    }
                    else
                    {
                        var randWindow = new RandWindow(Settings, true);
                        randWindow.Owner = this;
                        randWindow.ShowDialog();
                    }
                }
            }
            else
            {
                // 根据设置决定使用哪个点名窗口
                if (Settings.RandSettings.UseNewRollCallUI)
                {
                    // 使用新点名UI - 单次抽模式
                    var rollCallWindow = new NewStyleRollCallWindow(Settings, true);
                    rollCallWindow.Owner = this;
                    rollCallWindow.ShowDialog();
                }
                else
                {
                    // 使用默认的随机点名窗口
                    var randWindow = new RandWindow(Settings, true);
                    randWindow.Owner = this;
                    randWindow.ShowDialog();
                }
            }
        }

        /// <summary>
        /// 墨迹重播按钮点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        private void GridInkReplayButton_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("重播冻结页面内容")) return;
            //if (lastBorderMouseDownObject != sender) return;

            AnimationsHelper.HidePopupWithSlideAndFade(BorderTools);
            AnimationsHelper.HidePopupWithSlideAndFade(BoardBorderToolsPopup);
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
            InkReplayPanelStatusText.Text = Properties.MainWindowStrings.Main_InkReplayPlaying;
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
                                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

                                    stylusPoints.Add(stylusPoint);
                                    s = new Stroke(stylusPoints.Clone())
                                    {
                                        DrawingAttributes = stroke.DrawingAttributes
                                    };
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
                                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

                                    stylusPoints.Add(stylusPoint);
                                    s = new Stroke(stylusPoints.Clone())
                                    {
                                        DrawingAttributes = stroke.DrawingAttributes
                                    };
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
                    inkCanvas.IsHitTestVisible = true;
                    inkCanvas.IsManipulationEnabled = true;

                    if (inkCanvas.EditingMode == InkCanvasEditingMode.None)
                    {
                        inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    }

                    ResetTouchStates();
                });
            }).Start();
        }

        /// <summary>
        /// 是否停止墨迹重播
        /// </summary>
        private bool isStopInkReplay;
        /// <summary>
        /// 是否暂停墨迹重播
        /// </summary>
        private bool isPauseInkReplay;
        /// <summary>
        /// 是否重新开始墨迹重播
        /// </summary>
        private bool isRestartInkReplay;
        /// <summary>
        /// 墨迹重播速度
        /// </summary>
        private double inkReplaySpeed = 1;

        /// <summary>
        /// 墨迹重播画布鼠标按下事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
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
                inkCanvas.IsHitTestVisible = true;
                inkCanvas.IsManipulationEnabled = true;

                if (inkCanvas.EditingMode == InkCanvasEditingMode.None)
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                }

                ResetTouchStates();
            }
        }

        /// <summary>
        /// 墨迹重播播放/暂停按钮鼠标按下事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        private void InkReplayPlayPauseBorder_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            InkReplayPlayPauseBorder.Background = new SolidColorBrush(Color.FromArgb(34, 9, 9, 11));
        }

        /// <summary>
        /// 墨迹重播播放/暂停按钮鼠标释放事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        private void InkReplayPlayPauseBorder_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            InkReplayPlayPauseBorder.Background = new SolidColorBrush(Colors.Transparent);
            isPauseInkReplay = !isPauseInkReplay;
            InkReplayPanelStatusText.Text = isPauseInkReplay ? Properties.MainWindowStrings.Main_InkReplay_Paused : Properties.MainWindowStrings.Main_InkReplayPlaying;
            InkReplayPlayButtonImage.Visibility = isPauseInkReplay ? Visibility.Visible : Visibility.Collapsed;
            InkReplayPauseButtonImage.Visibility = !isPauseInkReplay ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 墨迹重播停止按钮鼠标按下事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        private void InkReplayStopButtonBorder_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            InkReplayStopButtonBorder.Background = new SolidColorBrush(Color.FromArgb(34, 9, 9, 11));
        }

        /// <summary>
        /// 墨迹重播停止按钮鼠标释放事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
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

        /// <summary>
        /// 墨迹重播重新开始按钮鼠标按下事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        private void InkReplayReplayButtonBorder_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            InkReplayReplayButtonBorder.Background = new SolidColorBrush(Color.FromArgb(34, 9, 9, 11));
        }

        /// <summary>
        /// 墨迹重播重新开始按钮鼠标释放事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        private void InkReplayReplayButtonBorder_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            InkReplayReplayButtonBorder.Background = new SolidColorBrush(Colors.Transparent);
            isRestartInkReplay = true;
            isPauseInkReplay = false;
            InkReplayPanelStatusText.Text = Properties.MainWindowStrings.Main_InkReplayPlaying;
            InkReplayPlayButtonImage.Visibility = Visibility.Collapsed;
            InkReplayPauseButtonImage.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 墨迹重播速度按钮鼠标按下事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        private void InkReplaySpeedButtonBorder_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            InkReplaySpeedButtonBorder.Background = new SolidColorBrush(Color.FromArgb(34, 9, 9, 11));
        }

        /// <summary>
        /// 墨迹重播速度按钮鼠标释放事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        private void InkReplaySpeedButtonBorder_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            InkReplaySpeedButtonBorder.Background = new SolidColorBrush(Colors.Transparent);
            inkReplaySpeed = inkReplaySpeed == 0.5 ? 1 :
                inkReplaySpeed == 1 ? 2 :
                inkReplaySpeed == 2 ? 4 :
                inkReplaySpeed == 4 ? 8 : 0.5;
            InkReplaySpeedTextBlock.Text = inkReplaySpeed + "x";
        }

        /// <summary>
        /// 工具图标点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        internal void SymbolIconTools_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (BorderTools.IsOpen || BoardBorderToolsPopup.IsOpen)
            {
                AnimationsHelper.HidePopupWithSlideAndFade(BorderTools);
                AnimationsHelper.HidePopupWithSlideAndFade(BoardBorderToolsPopup);
            }
            else
            {
                HideSubPanels();
                if (currentMode == 0)
                {
                    AnimationsHelper.ShowPopupWithSlideAndFade(BorderTools);
                    _popupManager?.BringToFront(BorderTools);
                }
                else
                {
                    AnimationsHelper.ShowPopupWithSlideAndFade(BoardBorderToolsPopup);
                    _popupManager?.BringToFront(BoardBorderToolsPopup);
                }
            }
        }

        /// <summary>
        /// 浮动工具栏边距动画是否正在运行
        /// </summary>
        private bool isViewboxFloatingBarMarginAnimationRunning;
        private bool isFloatingBarHeadOnRight;

        private double GetFloatingBarScaleX()
        {
            var scale = ViewboxFloatingBarScaleTransform?.ScaleX ?? 1;
            return scale > 0 && !double.IsNaN(scale) && !double.IsInfinity(scale) ? scale : 1;
        }

        private double GetElementWidthForFloatingBar(FrameworkElement element, double fallbackWidth)
        {
            if (element == null) return fallbackWidth;

            var width = element.ActualWidth;
            if (width <= 0 || double.IsNaN(width)) width = element.DesiredSize.Width;
            if (width <= 0 || double.IsNaN(width)) width = element.RenderSize.Width;
            if (width <= 0 || double.IsNaN(width)) width = element.Width;

            return width > 0 && !double.IsNaN(width) && !double.IsInfinity(width) ? width : fallbackWidth;
        }

        private double GetElementHeightForFloatingBar(FrameworkElement element, double fallbackHeight)
        {
            if (element == null) return fallbackHeight;

            var height = element.ActualHeight;
            if (height <= 0 || double.IsNaN(height)) height = element.DesiredSize.Height;
            if (height <= 0 || double.IsNaN(height)) height = element.RenderSize.Height;
            if (height <= 0 || double.IsNaN(height)) height = element.Height;

            return height > 0 && !double.IsNaN(height) && !double.IsInfinity(height) ? height : fallbackHeight;
        }

        private double GetFloatingBarScaledWidth()
        {
            var baseWidth = GetElementWidthForFloatingBar(ViewboxFloatingBar, 200);
            return baseWidth * GetFloatingBarScaleX();
        }

        private double GetFloatingBarHeadScaledWidth()
        {
            var dragElement = FindDragHandleInRoot();
            return GetElementWidthForFloatingBar(dragElement, 50) * GetFloatingBarScaleX();
        }

        private double GetSelectionBGLeft()
        {
            var (_, _, contentPanel) = GetFirstContentBorderElements();
            if (contentPanel == null) return 0;
            foreach (var border in FloatingBarRootPanel.Children.OfType<Border>())
            {
                if (border.Tag as string == ToolbarRegistry.ContentBorderTag && border.Child is Grid grid)
                {
                    foreach (var gridChild in grid.Children.OfType<System.Windows.Controls.Canvas>())
                    {
                        if (gridChild.Tag as string == ToolbarRegistry.SelectionCanvasTag)
                        {
                            foreach (var canvasChild in gridChild.Children.OfType<Border>())
                            {
                                if (canvasChild.Tag as string == ToolbarRegistry.SelectionBGTag)
                                {
                                    var left = System.Windows.Controls.Canvas.GetLeft(canvasChild);
                                    return double.IsNaN(left) ? 0 : left;
                                }
                            }
                        }
                    }
                }
            }
            return 0;
        }

        private StackPanel GetFirstContentPanel()
        {
            if (FloatingBarRootPanel == null) return null;
            foreach (var border in FloatingBarRootPanel.Children.OfType<Border>())
            {
                if (border.Tag as string == ToolbarRegistry.ContentBorderTag && border.Child is Grid grid)
                {
                    foreach (var gridChild in grid.Children.OfType<StackPanel>())
                    {
                        if (gridChild.Tag as string == ToolbarRegistry.ContentPanelTag)
                            return gridChild;
                    }
                }
            }
            return null;
        }

        private FrameworkElement FindDragHandleInRoot()
        {
            if (FloatingBarRootPanel == null) return null;
            if (BorderFloatingBarMoveControls != null &&
                FloatingBarRootPanel.Children.Contains(BorderFloatingBarMoveControls))
                return BorderFloatingBarMoveControls;
            foreach (var child in FloatingBarRootPanel.Children.OfType<FrameworkElement>())
            {
                if (IsDragHandleElement(child))
                    return child;
            }
            return null;
        }

        private double GetFloatingBarScreenWidth(bool useWorkingArea)
        {
            double dpiScaleX = 1;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
            }

            var screen = GetFloatingBarTargetScreen();
            return (useWorkingArea ? screen.WorkingArea.Width : screen.Bounds.Width) / dpiScaleX;
        }

        private void SetFloatingBarHeadPlacement(bool headOnRight)
        {
            if (FloatingBarRootPanel == null) return;
            if (isFloatingBarHeadOnRight == headOnRight) return;

            var rootChildren = FloatingBarRootPanel.Children;
            var rootList = rootChildren.OfType<FrameworkElement>().ToList();

            var dragElement = FindDragHandleInRoot();
            var otherElements = rootList.Where(c => c != dragElement).ToList();

            rootChildren.Clear();

            if (headOnRight)
            {
                foreach (var elem in otherElements.AsEnumerable().Reverse())
                {
                    rootChildren.Add(elem);
                }
                if (dragElement != null)
                {
                    dragElement.Margin = new Thickness(3, 0, 0, 0);
                    rootChildren.Add(dragElement);
                }

                ReverseAllContentPanels();
            }
            else
            {
                if (dragElement != null)
                {
                    dragElement.Margin = new Thickness(0);
                    rootChildren.Add(dragElement);
                }
                foreach (var elem in otherElements.AsEnumerable().Reverse())
                {
                    rootChildren.Add(elem);
                }

                RestoreAllContentPanels();
            }

            isFloatingBarHeadOnRight = headOnRight;

            // 翻转工具栏后更新高光和指示条位置
            SetFloatingBarHighlightPosition(_currentToolMode);
        }

        private bool IsDragHandleElement(FrameworkElement element)
        {
            if (element is Border border)
            {
                if (border.Name == "BorderFloatingBarMoveControls") return true;
                var child = border.Child;
                if (child is Image) return true;
                if (child is StackPanel panel && panel.Children.Count > 0 && panel.Children[0] is Image)
                    return true;
            }
            return false;
        }

        private IEnumerable<StackPanel> GetAllContentPanels()
        {
            if (FloatingBarRootPanel == null) yield break;
            foreach (var child in FloatingBarRootPanel.Children.OfType<Border>())
            {
                if (child.Tag as string == ToolbarRegistry.ContentBorderTag && child.Child is Grid grid)
                {
                    foreach (var gridChild in grid.Children.OfType<StackPanel>())
                    {
                        if (gridChild.Tag as string == ToolbarRegistry.ContentPanelTag)
                            yield return gridChild;
                    }
                }
            }
        }

        private Dictionary<StackPanel, List<FrameworkElement>> _normalContentOrders;

        private void ReverseAllContentPanels()
        {
            _normalContentOrders = new Dictionary<StackPanel, List<FrameworkElement>>();
            foreach (var panel in GetAllContentPanels())
            {
                _normalContentOrders[panel] = panel.Children.OfType<FrameworkElement>().ToList();
                var reversed = panel.Children.OfType<FrameworkElement>().Reverse().ToList();
                panel.Children.Clear();
                foreach (var child in reversed)
                    panel.Children.Add(child);
            }
        }

        private void RestoreAllContentPanels()
        {
            if (_normalContentOrders == null) return;
            foreach (var kvp in _normalContentOrders)
            {
                var panel = kvp.Key;
                var normalOrder = kvp.Value;
                var current = panel.Children.OfType<FrameworkElement>().ToList();
                if (current.SequenceEqual(normalOrder)) continue;
                panel.Children.Clear();
                foreach (var child in normalOrder)
                {
                    if (child.Parent != null && child.Parent != panel)
                        continue;
                    if (!panel.Children.Contains(child))
                        panel.Children.Add(child);
                }
            }
            _normalContentOrders = null;
        }

        private bool IsFloatingBarContentVisible()
        {
            return !ToolbarRegistry.IsContentCollapsedByUser;
        }

        private void SetFloatingBarContentVisibility(bool visible)
        {
            ToolbarRegistry.IsContentCollapsedByUser = !visible;
            ToolbarRegistry.UpdateVisibilityByMode(
                FloatingBarRootPanel,
                IsAnnotating,
                IsInPptPresentationMode);
        }

        private double ClampFloatingBarLeft(double left, double floatingBarWidth, double screenWidth)
        {
            var maxLeft = Math.Max(0, screenWidth - floatingBarWidth);
            return Math.Max(0, Math.Min(left, maxLeft));
        }

        private void PlaceFloatingBarAfterHeadToggle(double headLeft, bool isExpanding)
        {
            var screenWidth = GetFloatingBarScreenWidth(Settings.Advanced.IsEnableAvoidFullScreenHelper);

            if (!isExpanding)
            {
                SetFloatingBarHeadPlacement(false);
                var collapsedWidth = GetFloatingBarHeadScaledWidth();
                pos.X = ClampFloatingBarLeft(headLeft, collapsedWidth, screenWidth);
                ViewboxFloatingBar.Margin = new Thickness(pos.X, ViewboxFloatingBar.Margin.Top, -2000, -200);
                SaveFloatingBarPositionPoint();
                return;
            }

            var floatingBarWidth = GetFloatingBarScaledWidth();
            var headWidth = GetFloatingBarHeadScaledWidth();
            var shouldPlaceToolsOnLeft = headLeft + floatingBarWidth > screenWidth;
            var wasHeadOnRight = isFloatingBarHeadOnRight;

            SetFloatingBarHeadPlacement(shouldPlaceToolsOnLeft);

            floatingBarWidth = GetFloatingBarScaledWidth();
            headWidth = GetFloatingBarHeadScaledWidth();

            var nextLeft = shouldPlaceToolsOnLeft
                ? headLeft - Math.Max(0, floatingBarWidth - headWidth)
                : headLeft;

            pos.X = ClampFloatingBarLeft(nextLeft, floatingBarWidth, screenWidth);
            ViewboxFloatingBar.Margin = new Thickness(pos.X, ViewboxFloatingBar.Margin.Top, -2000, -200);

            if (shouldPlaceToolsOnLeft != wasHeadOnRight)
            {
                var actualHeadLeft = ViewboxFloatingBar.Margin.Left + (isFloatingBarHeadOnRight ? Math.Max(0, floatingBarWidth - headWidth) : 0);
                var correction = headLeft - actualHeadLeft;
                if (Math.Abs(correction) > 0.5)
                {
                    pos.X += correction;
                    pos.X = ClampFloatingBarLeft(pos.X, GetFloatingBarScaledWidth(), screenWidth);
                    ViewboxFloatingBar.Margin = new Thickness(pos.X, ViewboxFloatingBar.Margin.Top, -2000, -200);
                }
            }
            SaveFloatingBarPositionPoint();
        }

        private double NormalizeFloatingBarLeftForScreen(double requestedLeft, double floatingBarWidth,
            double screenWidth)
        {
            var headWidth = GetFloatingBarHeadScaledWidth();
            var nextLeft = requestedLeft;
            var shouldPlaceToolsOnLeft = isFloatingBarHeadOnRight;
            var wasHeadOnRight = isFloatingBarHeadOnRight;

            if (!isFloatingBarHeadOnRight && requestedLeft + floatingBarWidth > screenWidth)
            {
                shouldPlaceToolsOnLeft = true;
                nextLeft = requestedLeft - Math.Max(0, floatingBarWidth - headWidth);
            }
            else if (isFloatingBarHeadOnRight && requestedLeft + floatingBarWidth <= screenWidth)
            {
                shouldPlaceToolsOnLeft = requestedLeft > screenWidth / 2;
            }

            SetFloatingBarHeadPlacement(shouldPlaceToolsOnLeft);

            if (shouldPlaceToolsOnLeft != wasHeadOnRight)
            {
                floatingBarWidth = GetFloatingBarScaledWidth();
            }

            return ClampFloatingBarLeft(nextLeft, floatingBarWidth, screenWidth);
        }

        private double GetCurrentFloatingBarHeadLeft()
        {
            var floatingBarWidth = GetFloatingBarScaledWidth();
            var headWidth = GetFloatingBarHeadScaledWidth();
            var headOffset = isFloatingBarHeadOnRight
                ? Math.Max(0, floatingBarWidth - headWidth)
                : 0;
            return ViewboxFloatingBar.Margin.Left + headOffset;
        }

        private void SaveFloatingBarPositionPoint()
        {
            var currentPoint = new Point(ViewboxFloatingBar.Margin.Left, ViewboxFloatingBar.Margin.Top);
            if (IsInPptPresentationMode)
                pointPPT = currentPoint;
            else
                pointDesktop = currentPoint;
        }

        /// <summary>
        /// 浮动工具栏边距动画处理
        /// </summary>
        /// <param name="MarginFromEdge">边缘边距</param>
        /// <param name="PosXCaculatedWithTaskbarHeight">是否考虑任务栏高度计算位置</param>
        /// <param name="skipAnimation">是否跳过动画直接定位（用于启动时快速恢复位置）</param>
        public async void ViewboxFloatingBarMarginAnimation(int MarginFromEdge,
            bool PosXCaculatedWithTaskbarHeight = false, bool skipAnimation = false)
        {
            if (currentMode == 1)
            {
                return;
            }

            if (MarginFromEdge == 60) MarginFromEdge = 55;

            await Dispatcher.InvokeAsync(() =>
            {
                if (skipAnimation)
                {
                    ViewboxFloatingBarMarginAnimationCore(MarginFromEdge, PosXCaculatedWithTaskbarHeight, false);
                    return;
                }

                ViewboxFloatingBarMarginAnimationCore(MarginFromEdge, PosXCaculatedWithTaskbarHeight, true);
            });

            await Task.Delay(skipAnimation ? 0 : 200);

            await Dispatcher.InvokeAsync(() =>
            {
                ViewboxFloatingBar.Margin = new Thickness(pos.X, pos.Y, -2000, -200);
                ViewboxFloatingBar.BeginAnimation(MarginProperty, null);
                isViewboxFloatingBarMarginAnimationRunning = false;
                if (!Topmost) ViewboxFloatingBar.Visibility = Visibility.Hidden;
            });
        }

        private void ViewboxFloatingBarMarginAnimationCore(int MarginFromEdge,
            bool PosXCaculatedWithTaskbarHeight = false, bool animate = false)
        {
            if (!Topmost)
                MarginFromEdge = -60;
            else
            {
                ViewboxFloatingBar.Visibility = Visibility.Visible;
                ViewboxFloatingBar.UpdateLayout();
            }
            isViewboxFloatingBarMarginAnimationRunning = true;

            double dpiScaleX = 1, dpiScaleY = 1;
            var source = PresentationSource.FromVisual(this);
            if (source != null)
            {
                dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
            }

            var screen = GetFloatingBarTargetScreen();
            double screenWidth, screenHeight;
            double toolbarHeight;
            if (Settings.Advanced.IsEnableAvoidFullScreenHelper && PosXCaculatedWithTaskbarHeight)
            {
                screenWidth = screen.WorkingArea.Width / dpiScaleX;
                screenHeight = screen.WorkingArea.Height / dpiScaleY;
                toolbarHeight = 0;
            }
            else
            {
                screenWidth = screen.Bounds.Width / dpiScaleX;
                screenHeight = screen.Bounds.Height / dpiScaleY;
                toolbarHeight = ForegroundWindowInfo.GetTaskbarHeight(screen, dpiScaleY);
            }

            double baseWidth = ViewboxFloatingBar.ActualWidth;

            if (baseWidth <= 0)
            {
                baseWidth = ViewboxFloatingBar.DesiredSize.Width;
            }

            if (baseWidth <= 0)
            {
                baseWidth = ViewboxFloatingBar.RenderSize.Width;
            }

            if (baseWidth <= 0)
            {
                baseWidth = 200;
                LogHelper.WriteLogToFile($"浮动栏宽度无法获取，使用估算值: {baseWidth}");
            }

            double floatingBarWidth = baseWidth * ViewboxFloatingBarScaleTransform.ScaleX;

            double baseHeight = ViewboxFloatingBar.ActualHeight;
            if (baseHeight <= 0)
            {
                baseHeight = ViewboxFloatingBar.DesiredSize.Height;
            }
            if (baseHeight <= 0)
            {
                baseHeight = ViewboxFloatingBar.RenderSize.Height;
            }
            if (baseHeight <= 0)
            {
                baseHeight = 58;
            }
            double floatingBarHeight = baseHeight * ViewboxFloatingBarScaleTransform.ScaleY;


            if (QuickColorPalette != null &&
                (QuickColorPalette.QuickColorPalettePanel.Visibility == Visibility.Visible ||
                 QuickColorPalette.QuickColorPaletteSingleRowPanel.Visibility == Visibility.Visible))
            {
                if (Settings.Appearance.QuickColorPaletteDisplayMode == 0)
                {
                    floatingBarWidth = Math.Max(floatingBarWidth, 120 * ViewboxFloatingBarScaleTransform.ScaleX);
                }
                else
                {
                    floatingBarWidth = Math.Max(floatingBarWidth, 68 * ViewboxFloatingBarScaleTransform.ScaleX);
                }
            }

            pos.X = (screenWidth - floatingBarWidth) / 2;

            if (!PosXCaculatedWithTaskbarHeight)
            {
                if (toolbarHeight == 0)
                {
                    pos.Y = screenHeight - MarginFromEdge * ViewboxFloatingBarScaleTransform.ScaleY;
                }
                else
                {
                    pos.Y = screenHeight - MarginFromEdge * ViewboxFloatingBarScaleTransform.ScaleY - toolbarHeight;
                }

                baseWidth = GetElementWidthForFloatingBar(ViewboxFloatingBar, 200);
                if (baseWidth <= 0)
                {
                    pos.Y = screenHeight - floatingBarHeight -
                           3 * ViewboxFloatingBarScaleTransform.ScaleY;
                }
                floatingBarWidth = baseWidth * ViewboxFloatingBarScaleTransform.ScaleX;

                baseHeight = GetElementHeightForFloatingBar(ViewboxFloatingBar, 58);
                if (baseHeight <= 0)
                {
                    baseHeight = 58;
                }
            }

            if (MarginFromEdge != -60)
            {
                if (IsInPptPresentationMode)
                // 如果快捷调色盘显示，确保有足够空间
                if (QuickColorPalette?.Visibility == Visibility.Visible)
                {
                    // 根据显示模式调整宽度
                    if (Settings.Appearance.QuickColorPaletteDisplayMode == 0)
                    {
                        // 单行显示模式，自适应宽度，但需要足够空间显示6个颜色
                        floatingBarWidth = Math.Max(floatingBarWidth, 200 * ViewboxFloatingBarScaleTransform.ScaleX);
                    }
                    else
                    {
                        // 双行显示模式，宽度较大
                        floatingBarWidth = Math.Max(floatingBarWidth, 108 * ViewboxFloatingBarScaleTransform.ScaleX);
                    }
                }
                pos.X = (screenWidth - floatingBarWidth) / 2;

                if (MarginFromEdge < 0)
                {
                    pos.Y = screenHeight - MarginFromEdge * ViewboxFloatingBarScaleTransform.ScaleY;
                }
                else if (IsInPptPresentationMode)
                {
                    pos.Y = screenHeight - floatingBarHeight +
                           2 * ViewboxFloatingBarScaleTransform.ScaleY;
                }
                else if (toolbarHeight == 0)
                {
                    pos.Y = screenHeight - floatingBarHeight -
                           3 * ViewboxFloatingBarScaleTransform.ScaleY;
                }
                else
                {
                    pos.Y = screenHeight - floatingBarHeight -
                           toolbarHeight - ViewboxFloatingBarScaleTransform.ScaleY * 3;
                }

                if (IsInPptPresentationMode)
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

                pos.X = NormalizeFloatingBarLeftForScreen(pos.X, floatingBarWidth, screenWidth);
                if (IsInPptPresentationMode)
                    pointPPT = pos;
                else
                    pointDesktop = pos;
            }

            if (animate)
            {
                var marginAnimation = new ThicknessAnimation
                {
                    Duration = TimeSpan.FromSeconds(0.35),
                    From = ViewboxFloatingBar.Margin,
                    To = new Thickness(pos.X, pos.Y, 0, -20),
                    EasingFunction = new CircleEase()
                };
                ViewboxFloatingBar.BeginAnimation(MarginProperty, marginAnimation);
            }
            else
            {
                ViewboxFloatingBar.Margin = new Thickness(pos.X, pos.Y, 0, -20);
            }

            if (!animate) isViewboxFloatingBarMarginAnimationRunning = false;
            if (!Topmost) ViewboxFloatingBar.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// 桌面模式下的浮动工具栏边距动画处理
        /// </summary>
        public async void PureViewboxFloatingBarMarginAnimationInDesktopMode()
        {
            // 在白板模式下不执行浮动栏动画
            if (currentMode == 1)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                ViewboxFloatingBar.Visibility = Visibility.Visible;
                ViewboxFloatingBar.UpdateLayout();
                isViewboxFloatingBarMarginAnimationRunning = true;

                double dpiScaleX = 1, dpiScaleY = 1;
                var source = PresentationSource.FromVisual(this);
                if (source != null)
                {
                    dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                    dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
                }

                var screen = GetFloatingBarTargetScreen();
                double screenWidth, screenHeight;
                double toolbarHeight;
                if (Settings.Advanced.IsEnableAvoidFullScreenHelper)
                {
                    screenWidth = screen.WorkingArea.Width / dpiScaleX;
                    screenHeight = screen.WorkingArea.Height / dpiScaleY;
                    toolbarHeight = 0;
                }
                else
                {
                    screenWidth = screen.Bounds.Width / dpiScaleX;
                    screenHeight = screen.Bounds.Height / dpiScaleY;
                    toolbarHeight = ForegroundWindowInfo.GetTaskbarHeight(screen, dpiScaleY);
                }

                double baseWidth = GetElementWidthForFloatingBar(ViewboxFloatingBar, 200);
                if (baseWidth <= 0)
                {
                    baseWidth = 200;
                    LogHelper.WriteLogToFile($"浮动栏宽度无法获取，使用估算值: {baseWidth}");
                }
                double floatingBarWidth = baseWidth * ViewboxFloatingBarScaleTransform.ScaleX;

                double baseHeight = GetElementHeightForFloatingBar(ViewboxFloatingBar, 58);
                if (baseHeight <= 0)
                {
                    baseHeight = 58;
                }
                double floatingBarHeight = baseHeight * ViewboxFloatingBarScaleTransform.ScaleY;


                // 如果快捷调色盘显示，确保有足够空间
                if (QuickColorPalette?.Visibility == Visibility.Visible)
                {
                    if (Settings.Appearance.QuickColorPaletteDisplayMode == 0)
                    {
                        floatingBarWidth = Math.Max(floatingBarWidth, 140 * ViewboxFloatingBarScaleTransform.ScaleX);
                    }
                    else
                    {
                        floatingBarWidth = Math.Max(floatingBarWidth, 86 * ViewboxFloatingBarScaleTransform.ScaleX);
                    }
                }

                pos.X = (screenWidth - floatingBarWidth) / 2;

                if (toolbarHeight == 0)
                {
                    pos.Y = screenHeight - floatingBarHeight -
                           3 * ViewboxFloatingBarScaleTransform.ScaleY;
                }
                else
                {
                    pos.Y = screenHeight - floatingBarHeight -
                           toolbarHeight - ViewboxFloatingBarScaleTransform.ScaleY * 3;
                }

                if (pointDesktop.X != -1 || pointDesktop.Y != -1) pointDesktop = pos;

                pos.X = NormalizeFloatingBarLeftForScreen(pos.X, floatingBarWidth, screenWidth);
                pointDesktop = pos;

                var marginAnimation = new ThicknessAnimation
                {
                    Duration = TimeSpan.FromSeconds(0.35),
                    From = ViewboxFloatingBar.Margin,
                    To = new Thickness(pos.X, pos.Y, 0, -20),
                    EasingFunction = new CircleEase()
                };
                ViewboxFloatingBar.BeginAnimation(MarginProperty, marginAnimation);
            });

            await Task.Delay(349);

            await Dispatcher.InvokeAsync(() =>
            {
                ViewboxFloatingBar.Margin = new Thickness(pos.X, pos.Y, -2000, -200);
            });
        }

        /// <summary>
        /// PPT模式下的浮动工具栏边距动画处理
        /// </summary>
        /// <param name="isRetry">是否为重试操作</param>
        public async void PureViewboxFloatingBarMarginAnimationInPPTMode(bool isRetry = false)
        {
            // 新增：在白板模式下不执行浮动栏动画
            if (currentMode == 1)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                ViewboxFloatingBar.Visibility = Visibility.Visible;
                ViewboxFloatingBar.UpdateLayout();
                isViewboxFloatingBarMarginAnimationRunning = true;

                double dpiScaleX = 1, dpiScaleY = 1;
                var source = PresentationSource.FromVisual(this);
                if (source != null)
                {
                    dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                    dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
                }

                var screen = GetFloatingBarTargetScreen();
                double screenWidth = screen.Bounds.Width / dpiScaleX, screenHeight = screen.Bounds.Height / dpiScaleY;
                // 仅计算Windows任务栏高度，不考虑其他程序对工作区的影响
                var toolbarHeight = ForegroundWindowInfo.GetTaskbarHeight(screen, dpiScaleY);

                // 计算浮动栏位置，考虑快捷调色盘的显示状态
                // 使用更可靠的方法获取浮动栏宽度
                double baseWidth = GetElementWidthForFloatingBar(ViewboxFloatingBar, 200);
                if (baseWidth <= 0)
                {
                    baseWidth = 200;
                    LogHelper.WriteLogToFile($"浮动栏宽度无法获取，使用估算值: {baseWidth}");
                }
                double floatingBarWidth = baseWidth * ViewboxFloatingBarScaleTransform.ScaleX;

                double baseHeight = GetElementHeightForFloatingBar(ViewboxFloatingBar, 58);
                if (baseHeight <= 0) baseHeight = 58;
                double floatingBarHeight = baseHeight * ViewboxFloatingBarScaleTransform.ScaleY;


                // 如果快捷调色盘显示，确保有足够空间
                if (QuickColorPalette?.Visibility == Visibility.Visible)
                {
                    if (Settings.Appearance.QuickColorPaletteDisplayMode == 0)
                    {
                        floatingBarWidth = Math.Max(floatingBarWidth, 140 * ViewboxFloatingBarScaleTransform.ScaleX);
                    }
                    else
                    {
                        floatingBarWidth = Math.Max(floatingBarWidth, 86 * ViewboxFloatingBarScaleTransform.ScaleX);
                    }
                }

                pos.X = (screenWidth - floatingBarWidth) / 2;

                pos.Y = screenHeight - floatingBarHeight +
                       2 * ViewboxFloatingBarScaleTransform.ScaleY;

                if (pointPPT.X != -1 || pointPPT.Y != -1)
                {
                    pointPPT = pos;
                }

                pos.X = NormalizeFloatingBarLeftForScreen(pos.X, floatingBarWidth, screenWidth);
                pointPPT = pos;

                var marginAnimation = new ThicknessAnimation
                {
                    Duration = TimeSpan.FromSeconds(0.35),
                    From = ViewboxFloatingBar.Margin,
                    To = new Thickness(pos.X, pos.Y, 0, -20),
                    EasingFunction = new CircleEase()
                };
                ViewboxFloatingBar.BeginAnimation(MarginProperty, marginAnimation);
            });

            await Task.Delay(349);

            await Dispatcher.InvokeAsync(() =>
            {
                ViewboxFloatingBar.Margin = new Thickness(pos.X, pos.Y, -2000, -200);
            });

            if (Settings.ModeSettings.IsPPTOnlyMode && !isRetry)
            {
                await Task.Delay(2000); // 等待动画完成后再检查

                bool isFloatingBarVisible = false;
                await Dispatcher.InvokeAsync(() =>
                {
                    // 检查浮动栏是否真的显示了
                    isFloatingBarVisible = ViewboxFloatingBar.Visibility == Visibility.Visible &&
                                          ViewboxFloatingBar.Margin.Left >= 0 &&
                                          ViewboxFloatingBar.Margin.Top >= 0;
                });

                if (!isFloatingBarVisible)
                {
                    PureViewboxFloatingBarMarginAnimationInPPTMode(true);
                }
            }
        }

        private Screen GetFloatingBarTargetScreen()
        {
            try
            {
                if (Settings.Advanced.EnableMultiScreenSupport &&
                    Settings.Advanced.FollowMouseForScreenSelection &&
                    ScreenDetectionHelper.HasMultipleScreens())
                {
                    var mouseScreen = Screen.FromPoint(System.Windows.Forms.Control.MousePosition);
                    if (mouseScreen != null)
                    {
                        return mouseScreen;
                    }
                }

                var windowHandle = new WindowInteropHelper(this).Handle;
                return Screen.FromHandle(windowHandle);
            }
            catch
            {
                return Screen.PrimaryScreen;
            }
        }

        private Screen GetCurrentFloatingBarScreen()
        {
            try
            {
                if (ViewboxFloatingBar == null || !IsLoaded)
                {
                    return null;
                }

                var center = ViewboxFloatingBar.PointToScreen(new Point(
                    Math.Max(0, ViewboxFloatingBar.ActualWidth / 2),
                    Math.Max(0, ViewboxFloatingBar.ActualHeight / 2)));
                return Screen.FromPoint(new System.Drawing.Point((int)center.X, (int)center.Y));
            }
            catch
            {
                return null;
            }
        }

        internal void RefreshFloatingBarScreenFollowState()
        {
            try
            {
                var enableFollow = Settings.Advanced.EnableMultiScreenSupport &&
                                   Settings.Advanced.FollowMouseForScreenSelection &&
                                   ScreenDetectionHelper.HasMultipleScreens();

                if (!enableFollow)
                {
                    _floatingBarScreenFollowTimer?.Stop();
                    _lastFloatingBarScreenDeviceName = null;
                    return;
                }

                if (_floatingBarScreenFollowTimer == null)
                {
                    _floatingBarScreenFollowTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(350)
                    };
                    _floatingBarScreenFollowTimer.Tick += FloatingBarScreenFollowTimer_Tick;
                }

                _lastFloatingBarScreenDeviceName = GetCurrentFloatingBarScreen()?.DeviceName;
                _lastCanvasScreenDeviceName = _lastFloatingBarScreenDeviceName;

                if (!_floatingBarScreenFollowTimer.IsEnabled)
                {
                    _floatingBarScreenFollowTimer.Start();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"刷新浮动栏多屏跟随状态失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void FloatingBarScreenFollowTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (!Settings.Advanced.EnableMultiScreenSupport ||
                    !Settings.Advanced.FollowMouseForScreenSelection ||
                    !ScreenDetectionHelper.HasMultipleScreens())
                {
                    _floatingBarScreenFollowTimer?.Stop();
                    _lastFloatingBarScreenDeviceName = null;
                    return;
                }

                if (currentMode == 1 || isDragDropInEffect || ViewboxFloatingBar.Visibility != Visibility.Visible)
                {
                    return;
                }

                var mouseScreen = Screen.FromPoint(System.Windows.Forms.Control.MousePosition);
                var currentFloatingBarScreen = GetCurrentFloatingBarScreen();

                if (mouseScreen == null || currentFloatingBarScreen == null)
                {
                    return;
                }

                if (mouseScreen.DeviceName == currentFloatingBarScreen.DeviceName)
                {
                    _lastFloatingBarScreenDeviceName = currentFloatingBarScreen.DeviceName;
                    return;
                }

                if (mouseScreen.DeviceName == _lastFloatingBarScreenDeviceName)
                {
                    return;
                }

                _lastFloatingBarScreenDeviceName = mouseScreen.DeviceName;
                RebuildCanvasOnTargetScreen(mouseScreen);

                if (IsInPptPresentationMode)
                {
                    PureViewboxFloatingBarMarginAnimationInPPTMode();
                }
                else
                {
                    PureViewboxFloatingBarMarginAnimationInDesktopMode();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"浮动栏跨屏跟随失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void RebuildCanvasOnTargetScreen(Screen targetScreen)
        {
            try
            {
                if (targetScreen == null || _isRebuildingCanvasForScreen)
                {
                    return;
                }

                if (_lastCanvasScreenDeviceName == targetScreen.DeviceName)
                {
                    return;
                }

                _isRebuildingCanvasForScreen = true;

                double dpiScaleX = 1, dpiScaleY = 1;
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                    dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
                }

                // 先移动主窗口到目标屏，确保画布承载区域切换到新屏幕。
                MainWindow.MoveWindow(
                    new WindowInteropHelper(this).Handle,
                    targetScreen.Bounds.X,
                    targetScreen.Bounds.Y,
                    targetScreen.Bounds.Width,
                    targetScreen.Bounds.Height,
                    true);

                // 重新铺设画布尺寸，强制触发布局刷新。
                inkCanvas.Width = targetScreen.Bounds.Width / dpiScaleX;
                inkCanvas.Height = targetScreen.Bounds.Height / dpiScaleY;
                inkCanvas.InvalidateMeasure();
                inkCanvas.InvalidateArrange();
                inkCanvas.UpdateLayout();

                if (GridInkCanvasSelectionCover != null)
                {
                    GridInkCanvasSelectionCover.Width = inkCanvas.Width;
                    GridInkCanvasSelectionCover.Height = inkCanvas.Height;
                    GridInkCanvasSelectionCover.InvalidateMeasure();
                    GridInkCanvasSelectionCover.InvalidateArrange();
                }

                _lastCanvasScreenDeviceName = targetScreen.DeviceName;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"在新屏重建画布失败: {ex.Message}", LogHelper.LogType.Warning);
            }
            finally
            {
                _isRebuildingCanvasForScreen = false;
            }
        }

        /// <summary>
        /// 光标图标点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        internal async void CursorIcon_Click(object sender, MouseButtonEventArgs e)
        {
            if (lastBorderMouseDownObject is Panel panel)
                panel.Background = new SolidColorBrush(Colors.Transparent);

            // 禁用高级橡皮擦系统
            DisableEraserOverlay();
            SetCurrentToolMode(InkCanvasEditingMode.None);

            UpdateCurrentToolMode("cursor");

            SetFloatingBarHighlightPosition("cursor");

            // 切换前自动截图保存墨迹
            if (inkCanvas.Strokes.Count > 0 &&
                inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber)
            {
                if (IsInPptPresentationMode)
                {
                    var currentSlide = _pptManager?.GetCurrentSlideNumber() ?? 0;
                    var presentationName = _pptManager?.GetPresentationName() ?? "";
                    CaptureAndEnqueueScreenshotSave(true, $"{presentationName}/{currentSlide}_{DateTime.Now:HH-mm-ss}");
                }
                else CaptureAndEnqueueScreenshotSave(true);
            }

            if (!IsInPptPresentationMode)
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
            SetTransparentHitThrough();

            GridBackgroundCoverHolder.Visibility = Visibility.Collapsed;

            // 点击鼠标按钮退出批注模式时的全屏还原
            RestoreFullScreenOnExitAnnotationMode();

            inkCanvas.Select(new StrokeCollection());
            GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

            if (currentMode != 0)
            {
                SaveStrokes();
                RestoreStrokes(true);
            }

            if (ThemeManager.Current.ApplicationTheme == ApplicationTheme.Dark)
            { /* Old UI removed */ }
            else
            { /* Old UI removed */ }

            { /* Old UI removed */ }
            { /* Old UI removed */ }
            UpdateToolbarComponentVisibility();


            UpdateToolbarComponentVisibility();

            // 注意：快捷调色盘的可见性现在完全由工具栏规则集管理，不需要手动设置

            if (!isFloatingBarFolded)
            {
                HideSubPanels("cursor", true);
                await Task.Delay(50);

                if (IsInPptPresentationMode)
                    ViewboxFloatingBarMarginAnimation(60);
                else
                    ViewboxFloatingBarMarginAnimation(100, true);
            }
        }

        /// <summary>
        /// 画笔图标点击事件处理，用于切换到批注模式或显示画笔调色盘
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        internal void PenIcon_Click(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("切换到画笔")) return;

            if (lastBorderMouseDownObject is Panel panel)
                panel.Background = new SolidColorBrush(Colors.Transparent);

            // 如果当前有选中的图片元素，先取消选中
            if (currentSelectedElement != null)
            {
                UnselectElement(currentSelectedElement);
                currentSelectedElement = null;
            }

            // 禁用高级橡皮擦系统
            DisableEraserOverlay();

            // 停止橡皮擦自动切换计时器（如果正在运行）
            StopEraserAutoSwitchBackTimer();

            bool isRealtimePenState = inkCanvas.EditingMode == InkCanvasEditingMode.None
                                      && ShouldUseRealtimeVelocityBrushTip()
                                      && string.Equals(GetCurrentSelectedMode(), "pen", StringComparison.OrdinalIgnoreCase);
            bool wasInInkMode = inkCanvas.EditingMode == InkCanvasEditingMode.Ink
                                || isRealtimePenState
                                || (Pen_Icon.Background != null
                                    && IsAnnotating
                                    && string.Equals(GetCurrentSelectedMode(), "pen", StringComparison.OrdinalIgnoreCase));
            bool wasHighlighter = drawingAttributes.IsHighlighter;

            if (drawingShapeMode != 0 && !isLongPressSelected)
            {
                return;
            }

            if (Pen_Icon.Background == null || !IsAnnotating)
            {
                if (isLongPressSelected)
                {
                    drawingShapeMode = 0;
                    isLongPressSelected = false;
                }

                // 使用集中化的工具模式切换方法
                SetCurrentToolMode(InkCanvasEditingMode.Ink);

                // 更新模式缓存
                UpdateCurrentToolMode("pen");

                GridTransparencyFakeBackground.Opacity = 1;
                GridTransparencyFakeBackground.Background = new SolidColorBrush(StringToColor("#01FFFFFF"));
                SetTransparentNotHitThrough();

                inkCanvas.IsHitTestVisible = true;
                inkCanvas.Visibility = Visibility.Visible;

                GridBackgroundCoverHolder.Visibility = Visibility.Visible;
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

                /*if (forceEraser && currentMode == 0)
                    BtnColorRed_Click(sender, null);*/

                if (GridBackgroundCover.Visibility == Visibility.Collapsed)
                {
                    if (ThemeManager.Current.ApplicationTheme == ApplicationTheme.Dark)
            { /* Old UI removed */ }
                    else
            { /* Old UI removed */ }
            { /* Old UI removed */ }
                }
                else
                {
            { /* Old UI removed */ }
            { /* Old UI removed */ }
                }

            { /* Old UI removed */ }

                // 进入批注模式时的全屏处理（仅当未应用过全屏处理时）
                if (Settings.Advanced.IsEnableAvoidFullScreenHelper && !isFullScreenApplied)
                {
                    // 设置为画板模式，允许全屏操作
                    AvoidFullScreenHelper.SetBoardMode(true);
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        MainWindow.MoveWindow(new WindowInteropHelper(this).Handle, 0, 0,
                            System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width,
                            System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height, true);
                    }), DispatcherPriority.ApplicationIdle);

                    isFullScreenApplied = true; // 标记已应用全屏处理
                }

                UpdateToolbarComponentVisibility();
                // 使用集中化的工具模式切换方法
                SetCurrentToolMode(InkCanvasEditingMode.Ink);

            UpdateCurrentToolMode("pen");

            // 注意：快捷调色盘的可见性和显示模式现在完全由工具栏系统管理
            // 不需要手动设置，UpdateToolbarComponentVisibility 会处理好

            SetFloatingBarHighlightPosition("pen");

                forceEraser = false;
                forcePointEraser = false;
                drawingShapeMode = 0;

                // 保持之前的笔类型状态，而不是强制重置
                if (!wasHighlighter && penType != 2)
                {
                    penType = 0;
                    drawingAttributes.IsHighlighter = false;
                    drawingAttributes.StylusTip = StylusTip.Ellipse;
                    Settings.Canvas.EnableInkFade = false;
                    if (_inkFadeManager != null)
                        _inkFadeManager.IsEnabled = false;
                }
                else if (penType == 1)
                {
                    drawingAttributes.IsHighlighter = !Settings.Canvas.HighlighterOverlapEnabled;
                    drawingAttributes.StylusTip = StylusTip.Rectangle;
                    drawingAttributes.Width = Settings.Canvas.HighlighterWidth / 2;
                    drawingAttributes.Height = Settings.Canvas.HighlighterWidth;
                    Settings.Canvas.EnableInkFade = false;
                    if (_inkFadeManager != null)
                        _inkFadeManager.IsEnabled = false;
                }
                // 如果之前是激光笔模式，则保持激光笔属性
                else if (penType == 2)
                {
                    drawingAttributes.IsHighlighter = false;
                    drawingAttributes.StylusTip = StylusTip.Ellipse;
                    drawingAttributes.Width = Settings.Canvas.LaserPenWidth;
                    drawingAttributes.Height = Settings.Canvas.LaserPenWidth;
                    Settings.Canvas.EnableInkFade = true;
                    if (_inkFadeManager != null)
                    {
                        _inkFadeManager.IsEnabled = true;
                        _inkFadeManager.UpdateFadeTime(Settings.Canvas.InkFadeTime);
                        _inkFadeManager.UpdateFadeSpeedMultiplier(Settings.Canvas.InkFadeSpeedMultiplier);
                    }
                }

                ColorSwitchCheck();
                HideSubPanels("pen", true);
            }
            else
            {
                if (wasInInkMode)
                {
                    if (forceEraser)
                    {
                        // 从橡皮擦模式切换过来，保持之前的笔类型状态
                        forceEraser = false;
                        forcePointEraser = false;
                        drawingShapeMode = 0;

                        // 保持之前的笔类型状态，而不是强制重置
                        if (!wasHighlighter && penType != 2)
                        {
                            penType = 0;
                            drawingAttributes.IsHighlighter = false;
                            drawingAttributes.StylusTip = StylusTip.Ellipse;
                            Settings.Canvas.EnableInkFade = false;
                            if (_inkFadeManager != null)
                                _inkFadeManager.IsEnabled = false;
                        }
                        else if (penType == 1)
                        {
                            drawingAttributes.IsHighlighter = !Settings.Canvas.HighlighterOverlapEnabled;
                            drawingAttributes.StylusTip = StylusTip.Rectangle;
                            drawingAttributes.Width = Settings.Canvas.HighlighterWidth / 2;
                            drawingAttributes.Height = Settings.Canvas.HighlighterWidth;
                            Settings.Canvas.EnableInkFade = false;
                            if (_inkFadeManager != null)
                                _inkFadeManager.IsEnabled = false;
                        }
                        // 如果之前是激光笔模式，则保持激光笔属性
                        else if (penType == 2)
                        {
                            drawingAttributes.IsHighlighter = false;
                            drawingAttributes.StylusTip = StylusTip.Ellipse;
                            drawingAttributes.Width = Settings.Canvas.LaserPenWidth;
                            drawingAttributes.Height = Settings.Canvas.LaserPenWidth;
                            Settings.Canvas.EnableInkFade = true;
                            if (_inkFadeManager != null)
                            {
                                _inkFadeManager.IsEnabled = true;
                                _inkFadeManager.UpdateFadeTime(Settings.Canvas.InkFadeTime);
                                _inkFadeManager.UpdateFadeSpeedMultiplier(Settings.Canvas.InkFadeSpeedMultiplier);
                            }
                        }

                        // 在非白板模式下，从线擦切换到批注时不直接弹出子面板
                        if (currentMode != 1)
                        {
                            HideSubPanels("pen", true);
                            return;
                        }
                    }

                    if (PenPalette.IsOpen || BoardPenPalette.IsOpen)
                    {
                        AnimationsHelper.HidePopupWithSlideAndFade(PenPalette);
                        AnimationsHelper.HidePopupWithSlideAndFade(BoardPenPalette);
                    }
                    else
                    {
                        HideSubPanels();
                        if (currentMode == 0)
                        {
                            AnimationsHelper.ShowPopupWithSlideAndFade(PenPalette);
                            _popupManager?.BringToFront(PenPalette);
                        }
                        else
                        {
                            AnimationsHelper.ShowPopupWithSlideAndFade(BoardPenPalette);
                            _popupManager?.BringToFront(BoardPenPalette);
                        }
                    }
                }
                else
                {
                    // 切换到批注模式时，确保保存当前图片信息
                    if (currentMode != 0)
                    {
                        SaveStrokes();
                    }
                    // 使用集中化的工具模式切换方法
                    SetCurrentToolMode(InkCanvasEditingMode.Ink);

                    // 更新模式缓存
                    UpdateCurrentToolMode("pen");

                    forceEraser = false;
                    forcePointEraser = false;
                    drawingShapeMode = 0;

                    // 保持之前的笔类型状态，而不是强制重置
                    if (!wasHighlighter && penType != 2)
                    {
                        penType = 0;
                        drawingAttributes.IsHighlighter = false;
                        drawingAttributes.StylusTip = StylusTip.Ellipse;
                        Settings.Canvas.EnableInkFade = false;
                        if (_inkFadeManager != null)
                            _inkFadeManager.IsEnabled = false;
                    }
                    else if (penType == 1)
                    {
                        drawingAttributes.IsHighlighter = !Settings.Canvas.HighlighterOverlapEnabled;
                        drawingAttributes.StylusTip = StylusTip.Rectangle;
                        drawingAttributes.Width = Settings.Canvas.HighlighterWidth / 2;
                        drawingAttributes.Height = Settings.Canvas.HighlighterWidth;
                        Settings.Canvas.EnableInkFade = false;
                        if (_inkFadeManager != null)
                            _inkFadeManager.IsEnabled = false;
                    }
                    // 如果之前是激光笔模式，则保持激光笔属性
                    else if (penType == 2)
                    {
                        drawingAttributes.IsHighlighter = false;
                        drawingAttributes.StylusTip = StylusTip.Ellipse;
                        drawingAttributes.Width = Settings.Canvas.LaserPenWidth;
                        drawingAttributes.Height = Settings.Canvas.LaserPenWidth;
                        Settings.Canvas.EnableInkFade = true;
                        if (_inkFadeManager != null)
                        {
                            _inkFadeManager.IsEnabled = true;
                            _inkFadeManager.UpdateFadeTime(Settings.Canvas.InkFadeTime);
                            _inkFadeManager.UpdateFadeSpeedMultiplier(Settings.Canvas.InkFadeSpeedMultiplier);
                        }
                    }

                    ColorSwitchCheck();
                    HideSubPanels("pen", true);
                }
            }


            forceEraser = false;
            forcePointEraser = false;
            drawingShapeMode = 0;
            EnsureRealtimeStylusPipelineBinding();
        }

        /// <summary>
        /// 颜色主题切换鼠标释放事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        private void ColorThemeSwitch_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isUselightThemeColor = !isUselightThemeColor;
            if (currentMode == 0) isDesktopUselightThemeColor = isUselightThemeColor;
            CheckColorTheme();
        }

        /// <summary>
        /// 橡皮擦图标点击事件处理，用于切换到橡皮擦模式或显示橡皮擦尺寸面板
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        internal void EraserIcon_Click(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("切换到橡皮擦")) return;

            bool isAlreadyEraser = inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint;
            forceEraser = false;
            forcePointEraser = true;
            drawingShapeMode = 0;

            // 切换到橡皮擦模式时，确保保存当前图片信息
            if (!isAlreadyEraser && currentMode != 0)
            {
                SaveStrokes();
            }

            if (!isAlreadyEraser)
            {
                ResetTouchStates();
            }

            // 启用新的高级橡皮擦系统
            EnableEraserOverlay();

            // 使用新的高级橡皮擦系统
            // 使用集中化的工具模式切换方法
            SetCurrentToolMode(InkCanvasEditingMode.EraseByPoint);

            // 更新模式缓存
            UpdateCurrentToolMode("eraser");

            ApplyAdvancedEraserShape(); // 使用新的橡皮擦形状应用方法
            SetCursorBasedOnEditingMode(inkCanvas);
            HideSubPanels("eraser"); // 高亮橡皮按钮
            Trace.WriteLine($"Eraser: Eraser button clicked, current size: {eraserWidth}, circle: {isEraserCircleShape}");

            // 如果启用了橡皮擦自动切换功能，停止之前的计时器（如果正在运行）
            if (Settings.Canvas.EnableEraserAutoSwitchBack)
            {
                StopEraserAutoSwitchBackTimer();
            }

            if (isAlreadyEraser)
            {
                if (EraserSizePanel.IsOpen == false && BoardEraserSizePanel?.IsOpen != true)
                {
                    if (currentMode == 0)
                    {
                        AnimationsHelper.ShowPopupWithSlideAndFade(EraserSizePanel);
                        _popupManager?.BringToFront(EraserSizePanel);
                    }
                    else
                    {
                        AnimationsHelper.ShowPopupWithSlideAndFade(BoardEraserSizePanel);
                        _popupManager?.BringToFront(BoardEraserSizePanel);
                    }
                }
                else
                {
                    AnimationsHelper.HidePopupWithSlideAndFade(EraserSizePanel);
                    if (BoardEraserSizePanel != null)
                        AnimationsHelper.HidePopupWithSlideAndFade(BoardEraserSizePanel);
                }
            }
        }

        /// <summary>
        /// 白板模式下的橡皮擦图标点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        private void BoardEraserIcon_Click(object sender, RoutedEventArgs e)
        {
            if (TryBlockFrozenPageMutation("切换到橡皮擦")) return;

            bool isAlreadyEraser = inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint;
            forceEraser = false;
            forcePointEraser = true;
            drawingShapeMode = 0;

            // 启用新的高级橡皮擦系统
            EnableEraserOverlay();

            // 使用新的高级橡皮擦系统
            // 使用集中化的工具模式切换方法
            SetCurrentToolMode(InkCanvasEditingMode.EraseByPoint);

            // 更新模式缓存
            UpdateCurrentToolMode("eraser");

            ApplyAdvancedEraserShape(); // 使用新的橡皮擦形状应用方法
            SetCursorBasedOnEditingMode(inkCanvas);
            HideSubPanels("eraser"); // 高亮橡皮按钮

            // 如果启用了橡皮擦自动切换功能，停止之前的计时器（如果正在运行）
            if (Settings.Canvas.EnableEraserAutoSwitchBack)
            {
                StopEraserAutoSwitchBackTimer();
            }

            if (isAlreadyEraser)
            {
                if (BoardEraserSizePanel?.IsOpen != true && EraserSizePanel.IsOpen == false)
                {
                    if (currentMode == 0)
                    {
                        AnimationsHelper.ShowPopupWithSlideAndFade(EraserSizePanel);
                        _popupManager?.BringToFront(EraserSizePanel);
                    }
                    else
                    {
                        AnimationsHelper.ShowPopupWithSlideAndFade(BoardEraserSizePanel);
                        _popupManager?.BringToFront(BoardEraserSizePanel);
                    }
                }
                else
                {
                    if (BoardEraserSizePanel != null)
                        AnimationsHelper.HidePopupWithSlideAndFade(BoardEraserSizePanel);
                    AnimationsHelper.HidePopupWithSlideAndFade(EraserSizePanel);
                }
            }
        }

        /// <summary>
        /// 墨迹擦除图标点击事件处理，用于切换到按笔画擦除模式
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        internal void EraserIconByStrokes_Click(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("切换到线擦")) return;

            // 禁用高级橡皮擦系统
            DisableEraserOverlay();

            forceEraser = true;
            forcePointEraser = false;

            inkCanvas.EraserShape = new EllipseStylusShape(5, 5);
            // 使用集中化的工具模式切换方法
            SetCurrentToolMode(InkCanvasEditingMode.EraseByStroke);

            // 更新模式缓存
            UpdateCurrentToolMode("eraserByStrokes");

            drawingShapeMode = 0;

            // 这样从线擦切换回批注时，可以恢复之前的荧光笔状态
            // penType 和 drawingAttributes 的状态将在 PenIcon_Click 中根据 wasHighlighter 来恢复

            inkCanvas_EditingModeChanged(inkCanvas, null);
            CancelSingleFingerDragMode();

            HideSubPanels("eraserByStrokes");

        }
        
        /// <summary>
        /// 白板模式下的墨迹擦除图标点击事件处理，用于切换到按笔画擦除模式
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        internal void BoardEraserIconByStrokes_Click(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("切换到线擦")) return;

            // 禁用高级橡皮擦系统
            DisableEraserOverlay();

            forceEraser = true;
            forcePointEraser = false;

            inkCanvas.EraserShape = new EllipseStylusShape(5, 5);
            // 使用集中化的工具模式切换方法
            SetCurrentToolMode(InkCanvasEditingMode.EraseByStroke);

            // 更新模式缓存
            UpdateCurrentToolMode("eraserByStrokes");

            drawingShapeMode = 0;

            // 这样从线擦切换回批注时，可以恢复之前的荧光笔状态
            // penType 和 drawingAttributes 的状态将在 PenIcon_Click 中根据 wasHighlighter 来恢复

            inkCanvas_EditingModeChanged(inkCanvas, null);
            CancelSingleFingerDragMode();

            HideSubPanels("eraserByStrokes");
        }

        /// <summary>
        /// 光标删除图标点击事件处理，用于删除选中内容并切换到光标模式
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        internal void CursorWithDelIcon_Click(object sender, MouseButtonEventArgs e)
        {
            SymbolIconDelete_MouseUp(sender, null);
            CursorIcon_Click(null, null);
        }

        /// <summary>
        /// 将当前绘笔颜色设置为白色并安排在短时间后自动恢复到之前的笔刷。
        /// </summary>
        private void QuickColorWhite_Click(object sender, RoutedEventArgs e)
        {
            SetQuickColor(Colors.White);
            ScheduleBrushAutoRestore();
        }

        /// <summary>
        /// 将快速颜色设置为橙色，并安排稍后自动恢复到先前的画笔颜色。
        /// </summary>
        private void QuickColorOrange_Click(object sender, RoutedEventArgs e)
        {
            SetQuickColor(Color.FromRgb(251, 150, 80)); // 橙色
            ScheduleBrushAutoRestore();
        }

        /// <summary>
        /// 将画笔颜色切换为黄色并安排自动恢复为先前的画笔设置。
        /// </summary>
        private void QuickColorYellow_Click(object sender, RoutedEventArgs e)
        {
            SetQuickColor(Colors.Yellow);
            ScheduleBrushAutoRestore();
        }

        /// <summary>
        /// 将快速颜色设置为黑色并安排在稍后自动恢复为先前的画笔颜色。
        /// </summary>
        private void QuickColorBlack_Click(object sender, RoutedEventArgs e)
        {
            SetQuickColor(Colors.Black);
            ScheduleBrushAutoRestore();
        }

        /// <summary>
        /// 将当前画笔颜色设置为蓝色并安排在一段时间后自动恢复到之前的画笔颜色。
        /// </summary>
        private void QuickColorBlue_Click(object sender, RoutedEventArgs e)
        {
            SetQuickColor(Color.FromRgb(37, 99, 235)); // 蓝色
            ScheduleBrushAutoRestore();
        }

        /// <summary>
        /// 将快速颜色切换为红色，并安排稍后自动恢复为先前的画笔颜色。
        /// </summary>
        private void QuickColorRed_Click(object sender, RoutedEventArgs e)
        {
            SetQuickColor(Colors.Red);
            ScheduleBrushAutoRestore();
        }

        /// <summary>
        /// 将快速颜色切换为绿色并安排在一段时间后自动恢复先前画笔颜色。
        /// </summary>
        private void QuickColorGreen_Click(object sender, RoutedEventArgs e)
        {
            SetQuickColor(Color.FromRgb(22, 163, 74));
            ScheduleBrushAutoRestore();
        }

        /// <summary>
        /// 将当前画笔颜色切换为紫色快捷色并安排自动恢复先前画笔设置。
        /// </summary>
        private void QuickColorPurple_Click(object sender, RoutedEventArgs e)
        {
            SetQuickColor(Color.FromRgb(147, 51, 234));
            ScheduleBrushAutoRestore();
        }

        internal void ApplyQuickColorByName(string colorName)
        {
            var color = colorName switch
            {
                "Black" => Colors.Black,
                "White" => Colors.White,
                "Red" => Colors.Red,
                "Orange" => Color.FromRgb(251, 150, 80),
                "Yellow" => Colors.Yellow,
                "Green" => Color.FromRgb(22, 163, 74),
                "Blue" => Color.FromRgb(37, 99, 235),
                "Purple" => Color.FromRgb(147, 51, 234),
                _ => Colors.Black
            };
            SetQuickColor(color);
            ScheduleBrushAutoRestore();
        }

        /// <summary>
        /// 设置并应用快速颜色到当前画笔与相关状态，包括必要时切换到批注模式、更新荧光笔属性与颜色索引、记录桌面/白板的最近颜色，以及刷新调色盘指示器和颜色显示。
        /// </summary>
        /// <param name="color">要应用的颜色。</param>
        /// <remarks>
        /// 此方法会：
        /// - 在非批注模式时切换到绘制（Ink）模式；
        /// - 将指定颜色应用到绘图属性和 InkCanvas 的默认绘图属性；
        /// - 在荧光笔模式下更新荧光笔的内部颜色索引与绘图属性（宽度、笔尖形状、IsHighlighter 等）；
        /// - 根据当前模式（桌面或白板）记录最近使用的颜色索引；
        /// - 更新快速调色盘的选中指示器并刷新颜色显示状态。
        /// </remarks>
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
                else if (color == Colors.Yellow || IsColorSimilar(color, Color.FromRgb(234, 179, 8)) ||
                         IsColorSimilar(color, Color.FromRgb(250, 204, 21)) ||
                         IsColorSimilar(color, Color.FromRgb(253, 224, 71)))
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
                else if (color == Colors.Red || IsColorSimilar(color, Color.FromRgb(220, 38, 38)) ||
                         IsColorSimilar(color, Color.FromRgb(239, 68, 68)))
                {
                    highlighterColor = 102; // 红色荧光笔
                }
                else if (color == Colors.Green || IsColorSimilar(color, Color.FromRgb(22, 163, 74)))
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
                drawingAttributes.IsHighlighter = !Settings.Canvas.HighlighterOverlapEnabled;

                inkCanvas.DefaultDrawingAttributes.Width = Settings.Canvas.HighlighterWidth / 2;
                inkCanvas.DefaultDrawingAttributes.Height = Settings.Canvas.HighlighterWidth;
                inkCanvas.DefaultDrawingAttributes.StylusTip = StylusTip.Rectangle;
                inkCanvas.DefaultDrawingAttributes.IsHighlighter = !Settings.Canvas.HighlighterOverlapEnabled;

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

        /// <summary>
        /// 更新快速调色盘的选中指示器，根据当前选中的颜色显示对应的勾选图标
        /// </summary>
        /// <param name="selectedColor">当前选中的颜色</param>
        private void UpdateQuickColorPaletteIndicator(Color selectedColor)
        {
            var qcp = QuickColorPalette;
            if (qcp == null)
            {
                return;
            }

            int tolerance = (penType == 1) ? 25 : 15;
            qcp.ClearAllChecked();
            qcp.SetCheckedByColor(selectedColor, tolerance);
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

        /// <summary>
        /// 选择工具图标鼠标释放事件处理，用于切换到选择模式或选择所有墨迹
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        private void SelectIcon_MouseUp(object sender, RoutedEventArgs e)
        {
            if (TryBlockFrozenPageMutation("切换到选择工具")) return;

            // 禁用高级橡皮擦系统
            DisableEraserOverlay();

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
                // 使用集中化的工具模式切换方法
                SetCurrentToolMode(InkCanvasEditingMode.Select);
            }
        }

        /// <summary>
        /// 从图形绘制模式切换到画笔模式的提示处理
        /// </summary>
        private void DrawShapePromptToPen()
        {
            if (isLongPressSelected)
            {
                // 如果是长按选中的状态，只隐藏面板，不切换到笔模式
                HideSubPanels("shape");
            }
            else
            {
                if (IsAnnotating)
                    HideSubPanels("pen");
                else
                    HideSubPanels("cursor");
            }
        }

        /// <summary>
        /// 关闭工具面板鼠标释放事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        private void CloseBordertools_MouseUp(object sender, MouseButtonEventArgs e)
        {
            HideSubPanels();
        }

        private void CloseBordertools_Click(object sender, RoutedEventArgs e)
        {
            HideSubPanels();
        }

        #region Left Side Panel

        /// <summary>
        /// 手指拖动模式切换按钮点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        public void ToggleFingerDragMode(object sender, RoutedEventArgs e)
        {
            if (isSingleFingerDragMode)
            {
                isSingleFingerDragMode = false;
            { /* Old UI removed */ }
            }
            else
            {
                isSingleFingerDragMode = true;
            { /* Old UI removed */ }
            }
        }

        /// <summary>
        /// 撤销按钮点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
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

        /// <summary>
        /// 重做按钮点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
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

        /// <summary>
        /// 按钮启用状态变更事件处理，用于更新按钮内容的透明度
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">依赖属性变更事件参数</param>
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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        #endregion Left Side Panel

        #region Right Side Panel

        public static bool CloseIsFromButton;

        /// <summary>
        /// 退出按钮点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        public void ExitApplication(object sender, RoutedEventArgs e)
        {
            _forceCloseFromExitOrRestartButton = true;
            App.IsAppExitByUser = true;
            Close();
        }

        /// <summary>
        /// 重启按钮点击事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        public void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            if (Settings.Advanced.IsSecondConfirmWhenShutdownApp)
            {
                if (MessageBox.Show(Properties.MainWindowStrings.Main_CloseConfirm_Level1, "InkCanvasForClass",
                        MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.Cancel) return;
                if (MessageBox.Show(Properties.MainWindowStrings.Main_CloseConfirm_Level2, "InkCanvasForClass",
                        MessageBoxButton.OKCancel, MessageBoxImage.Error) == MessageBoxResult.Cancel) return;
                if (MessageBox.Show(Properties.MainWindowStrings.Main_CloseConfirm_Level3, "InkCanvasForClass",
                        MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.Cancel) return;
            }

            Process.Start(System.Windows.Forms.Application.ExecutablePath, "-m");
            _forceCloseFromExitOrRestartButton = true;
            App.IsAppExitByUser = true;
            CloseIsFromButton = true;
            Close();
        }

        /// <summary>
        /// 切换并打开设置面板；在需要时先进行安全密码校验，然后显示设置面板并启动打开动画，同时根据设置暂时调整无焦点模式与遮罩交互状态。
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">路由事件参数</param>
        internal async void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsWindow != null)
            {
                if (_settingsWindow.WindowState == System.Windows.WindowState.Minimized)
                    _settingsWindow.WindowState = System.Windows.WindowState.Normal;
                _settingsWindow.Activate();
                _settingsWindow.Focus();
                return;
            }

            try
            {
                if (Ink_Canvas.Helpers.SecurityManager.IsPasswordRequiredForEnterSettings(Settings))
                {
                    bool ok = await Ink_Canvas.Helpers.SecurityManager.PromptAndVerifyPasswordOrTotpAsync(Settings, this, Properties.MainWindowStrings.Main_EnterSettings, Properties.MainWindowStrings.Main_EnterSettings_Message);
                    if (!ok) return;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"安全密码校验失败: {ex}", LogHelper.LogType.Error);
                return;
            }

            HideSubPanels();
            _settingsWindow = new Windows.SettingsViews.SettingsWindow();
            _settingsWindow.Owner = this;
            _settingsWindow.Topmost = this.Topmost;
            _settingsWindow.Closed += (s, args) => _settingsWindow = null;
            _settingsWindow.ShowDialog();
        }
private bool forceEraser;


        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (TryBlockFrozenPageMutation("清空冻结页面内容")) return;
            forceEraser = false;
            //BorderClearInDelete.Visibility = Visibility.Collapsed;

            if (currentMode == 0)
            {
                // 先回到画笔再清屏，避免 TimeMachine 的相关 bug 影响
                if (Pen_Icon.Background == null && IsAnnotating)
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

            if (Settings.Canvas.ClearCanvasAndClearTimeMachine) timeMachine.ClearStrokeHistory();

            CancelSingleFingerDragMode();

        }

        private bool lastIsInMultiTouchMode;

        private void CancelSingleFingerDragMode()
        {
            if (ToggleSwitchDrawShapeBorderAutoHide.IsOn) CollapseBorderDrawShape();

            GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

            if (isSingleFingerDragMode) ToggleFingerDragMode(null, null);
            isLongPressSelected = false;
        }

        /// <summary>
        /// 重置所有触摸相关状态，
        /// </summary>
        private void ResetTouchStates()
        {
            try
            {
                // 清空触摸点计数器
                dec.Clear();

                if (isPalmEraserActive)
                    isPalmEraserActive = false;

                // 确保触摸事件能正常响应
                inkCanvas.IsHitTestVisible = true;
                inkCanvas.IsManipulationEnabled = true;

                // 释放所有触摸捕获
                inkCanvas.ReleaseAllTouchCaptures();

                // 恢复UI元素的触摸响应
                ViewboxFloatingBar.IsHitTestVisible = true;
                BlackboardUIGridForInkReplay.IsHitTestVisible = true;


            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"重置触摸状态失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }


        internal int currentMode;

        // 退出批注模式时的全屏还原处理
        private void RestoreFullScreenOnExitAnnotationMode()
        {
            if (Settings.Advanced.IsEnableAvoidFullScreenHelper &&
                isFullScreenApplied &&
                currentMode == 0 && // 不在白板模式
                !IsInPptPresentationMode) // 不在PPT放映模式
            {
                // 恢复为非画板模式，重新启用全屏限制
                AvoidFullScreenHelper.SetBoardMode(false);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 退出批注模式，恢复到工作区域大小
                    var workingArea = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
                    MainWindow.MoveWindow(new WindowInteropHelper(this).Handle,
                        workingArea.Left, workingArea.Top,
                        workingArea.Width, workingArea.Height, true);
                }), DispatcherPriority.ApplicationIdle);

                isFullScreenApplied = false; // 标记全屏处理已还原
            }
        }

        /// <summary>
        /// 在屏幕模式、白板与黑板模式之间切换并同步相关的 UI 状态与资源处理。
        /// </summary>
        /// <remarks>
        /// 切换过程中会保存/清理/恢复画笔轨迹，显示或隐藏白板/黑板面板、手势面板与 PPT 控件，调整主题与悬浮工具栏可见性，处理全屏/工作区尺寸恢复或进入全屏，以及在进入白板时检查剪贴板并显示粘贴提示。该方法还会触发隐藏/显示墨迹画布的逻辑（通过调用 BtnHideInkCanvas_Click）。
        /// </remarks>
        private void SwitchBackground(object sender, RoutedEventArgs e)
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

                    // 在PPT模式下隐藏手势面板和手势按钮
                    AnimationsHelper.HideWithSlideAndFade(TwoFingerGestureBorder);
                    AnimationsHelper.HideWithSlideAndFade(BoardTwoFingerGestureBorder);
                    UpdateToolbarComponentVisibility();

                    SaveStrokes(true);
                    ClearStrokes(true);
                    RestoreStrokes(true);


                    if (ThemeManager.Current.ApplicationTheme == ApplicationTheme.Dark)
                    {
            { /* Old UI removed */ }
            { /* Old UI removed */ }
                    }
                    else
                    {
            { /* Old UI removed */ }
                        if (isPresentationHaveBlackSpace)
                        {
            { /* Old UI removed */ }
                            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                        }
                        else
                        {
            { /* Old UI removed */ }
                            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                        }
                    }

            { /* Old UI removed */ }

                    CheckClipboardImageAndShowPasteNotificationWhenEnteringBoard();
                }

                Topmost = true;
                BtnHideInkCanvas_Click(null, e);
            }
            else
            {
                switch (++currentMode % 2)
                {
                    case 0: //屏幕模式
                        VideoPresenter_OnExitWhiteboardMode();
                        currentMode = 0;
                        GridBackgroundCover.Visibility = Visibility.Collapsed;
                        AnimationsHelper.HideWithSlideAndFade(BlackboardLeftSide);
                        AnimationsHelper.HideWithSlideAndFade(BlackboardCenterSide);
                        AnimationsHelper.HideWithSlideAndFade(BlackboardRightSide);

                        // 在PPT模式下隐藏手势面板和手势按钮
                        AnimationsHelper.HideWithSlideAndFade(TwoFingerGestureBorder);
                        AnimationsHelper.HideWithSlideAndFade(BoardTwoFingerGestureBorder);
                        UpdateToolbarComponentVisibility();

                        SaveStrokes();
                        ClearStrokes(true);
                        RestoreStrokes(true);

                        // 退出白板模式时取消全屏（仅在非PPT模式下）
                        if (Settings.Advanced.IsEnableAvoidFullScreenHelper &&
                            !IsInPptPresentationMode) // 不在PPT放映模式
                        {
                            // 恢复为非画板模式，重新启用全屏限制
                            AvoidFullScreenHelper.SetBoardMode(false);

                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                // 退出白板模式，恢复到工作区域大小
                                var workingArea = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
                                MainWindow.MoveWindow(new WindowInteropHelper(this).Handle,
                                    workingArea.Left, workingArea.Top,
                                    workingArea.Width, workingArea.Height, true);
                            }), DispatcherPriority.ApplicationIdle);

                            isFullScreenApplied = false; // 标记全屏处理已还原
                        }

                        // 在屏幕模式下恢复基础浮动栏的显示
                        ViewboxFloatingBar.Visibility = Visibility.Visible;

                        // 退出白板时自动收纳功能 - 等待浮动栏完全展开后再收纳
                        // 当处于PPT放映模式时，不自动收纳
                        if (Settings.Automation.IsAutoFoldWhenExitWhiteboard && !isFloatingBarFolded &&
                            !IsInPptPresentationMode)
                        {
                            // 使用异步延迟，等待浮动栏展开动画完成后再收纳
                            Task.Run(async () =>
                            {
                                await Task.Delay(700);
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    FoldFloatingBar_MouseUp(new object(), null);
                                });
                            });
                        }

                        if (ThemeManager.Current.ApplicationTheme == ApplicationTheme.Dark)
                        {
            { /* Old UI removed */ }
            { /* Old UI removed */ }
                            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                        }
                        else
                        {
            { /* Old UI removed */ }
                            if (isPresentationHaveBlackSpace)
                            {
            { /* Old UI removed */ }
                                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                            }
                            else
                            {
            { /* Old UI removed */ }
                                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                            }
                        }

            { /* Old UI removed */ }
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

                        // 进入白板模式时全屏（仅在非PPT模式下）
                        if (Settings.Advanced.IsEnableAvoidFullScreenHelper &&
                            !IsInPptPresentationMode) // 不在PPT放映模式
                        {
                            // 设置为画板模式，允许全屏操作
                            AvoidFullScreenHelper.SetBoardMode(true);
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                MainWindow.MoveWindow(new WindowInteropHelper(this).Handle, 0, 0,
                                    System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width,
                                    System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height, true);
                            }), DispatcherPriority.ApplicationIdle);

                            isFullScreenApplied = true; // 标记已应用全屏处理
                        }

                        ViewboxFloatingBar.Visibility = Visibility.Collapsed;

            { /* Old UI removed */ }
                        if (ThemeManager.Current.ApplicationTheme == ApplicationTheme.Dark)
                        {
            { /* Old UI removed */ }
                            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                        }
                        else
                        {
            { /* Old UI removed */ }
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

            { /* Old UI removed */ }

                        if (Settings.Advanced.EnableUIAccessTopMost)
                        {
                            Topmost = true;
                        }
                        else
                        {
                            Topmost = false;
                        }

                        CheckClipboardImageAndShowPasteNotificationWhenEnteringBoard();
                        break;
                }
            }

            SyncPdfPageSidebarWithCanvas();
        }

        public int BoundsWidth = 5;
        private bool _isToolbarOnRightSide = true;

        private void BtnHideInkCanvas_Click(object sender, RoutedEventArgs e)
        {
            if (GridTransparencyFakeBackground.Background == Brushes.Transparent)
            {
                // 进入批注模式
                GridTransparencyFakeBackground.Opacity = 1;
                GridTransparencyFakeBackground.Background = new SolidColorBrush(StringToColor("#01FFFFFF"));
                SetTransparentNotHitThrough();
                inkCanvas.IsHitTestVisible = true;
                inkCanvas.Visibility = Visibility.Visible;

                GridBackgroundCoverHolder.Visibility = Visibility.Visible;

                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

                if (GridBackgroundCover.Visibility == Visibility.Collapsed)
                {
                    if (ThemeManager.Current.ApplicationTheme == ApplicationTheme.Dark)
            { /* Old UI removed */ }
                    else
            { /* Old UI removed */ }
            { /* Old UI removed */ }
                }
                else
                {
            { /* Old UI removed */ }
            { /* Old UI removed */ }
                }

            { /* Old UI removed */ }

                // 进入批注模式时的全屏处理（仅当未应用过全屏处理时）
                if (Settings.Advanced.IsEnableAvoidFullScreenHelper && !isFullScreenApplied)
                {
                    // 设置为画板模式，允许全屏操作
                    AvoidFullScreenHelper.SetBoardMode(true);
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        MainWindow.MoveWindow(new WindowInteropHelper(this).Handle, 0, 0,
                            System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width,
                            System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height, true);
                    }), DispatcherPriority.ApplicationIdle);

                    isFullScreenApplied = true; // 标记已应用全屏处理
                }
            }
            else
            {
                // Auto-clear Strokes 要等待截图完成再清理笔记
                if (!IsInPptPresentationMode)
                {
                    if (isLoaded && Settings.Automation.IsAutoClearWhenExitingWritingMode)
                        if (inkCanvas.Strokes.Count > 0)
                        {
                            if (Settings.Automation.IsAutoSaveScreenshotAtClear && inkCanvas.Strokes.Count >
                                Settings.Automation.MinimumAutomationStrokeNumber)
                                CaptureAndEnqueueScreenshotSave(true);

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
                            if (Settings.Automation.IsAutoSaveScreenshotAtClear && inkCanvas.Strokes.Count >
                                Settings.Automation.MinimumAutomationStrokeNumber)
                                CaptureAndEnqueueScreenshotSave(true);

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
                SetTransparentHitThrough();

                GridBackgroundCoverHolder.Visibility = Visibility.Collapsed;

                // 退出批注模式时的全屏还原
                RestoreFullScreenOnExitAnnotationMode();

                if (currentMode != 0)
                {
                    SaveStrokes();
                    RestoreStrokes(true);
                }

                if (ThemeManager.Current.ApplicationTheme == ApplicationTheme.Dark)
            { /* Old UI removed */ }
                else
            { /* Old UI removed */ }

            { /* Old UI removed */ }
            { /* Old UI removed */ }
            }

            if (GridTransparencyFakeBackground.Background == Brushes.Transparent)
            {
                UpdateToolbarComponentVisibility();
                HideSubPanels("cursor");

                if (currentMode == 0)
                {
                    ViewboxFloatingBar.Visibility = Visibility.Visible;
                }
            }
            else
            {
                UpdateToolbarComponentVisibility();

                if (currentMode == 0)
                {
                    ViewboxFloatingBar.Visibility = Visibility.Visible;
                }
            }
        }

        private void BtnSwitchSide_Click(object sender, RoutedEventArgs e)
        {
            if (_isToolbarOnRightSide)
            {
            { /* Old UI removed */ }
            { /* Old UI removed */ }
            }
            else
            {
            { /* Old UI removed */ }
            { /* Old UI removed */ }
            }
        }

        private void StackPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (((StackPanel)sender).Visibility == Visibility.Visible)
            { /* Old UI removed */ }
            else
            { /* Old UI removed */ }
        }

        #endregion

        private void InsertImageOptions_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation(FloatingBarStrings.Board_InsertImage)) return;
            // Check if the image options panel is currently visible
            bool isImagePanelVisible = BoardImageOptionsPanel.IsOpen;

            // Toggle the image options panel
            if (isImagePanelVisible)
            {
                // Panel was visible, so hide it with animation
                AnimationsHelper.HidePopupWithSlideAndFade(BoardImageOptionsPanel);
            }
            else
            {
                // Panel was hidden, so hide other panels and show this one
                HideSubPanels();
                AnimationsHelper.ShowPopupWithSlideAndFade(BoardImageOptionsPanel);
                _popupManager?.BringToFront(BoardImageOptionsPanel);
            }
        }

        private void CloseImageOptionsPanel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            AnimationsHelper.HidePopupWithSlideAndFade(BoardImageOptionsPanel);
        }

        private async void ImageOptionScreenshot_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("插入截图")) return;
            // Hide the options panel
            AnimationsHelper.HidePopupWithSlideAndFade(BoardImageOptionsPanel);

            // Wait a bit for the panel to hide
            await Task.Delay(100);

            // Capture screenshot and insert to canvas
            await CaptureScreenshotAndInsert();
        }

        private async void ImageOptionSelectFile_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation(FloatingBarStrings.Board_InsertImage)) return;
            // Hide the options panel
            AnimationsHelper.HideWithSlideAndFade(BoardImageOptionsPanel);

            // Open file dialog to select image
            var dialog = new OpenFileDialog
            {
                Filter = "图片与 PDF|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.pdf|图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif|PDF|*.pdf"
            };
            if (dialog.ShowDialog() == true)
            {
                string filePath = dialog.FileName;
                FrameworkElement element = await CreateAndCompressImageAsync(filePath);
                if (element != null)
                {
                    string timestamp = "img_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
                    element.Name = timestamp;

                    // 初始化TransformGroup
                    var transformGroup = new TransformGroup();
                    transformGroup.Children.Add(new ScaleTransform(1, 1));
                    transformGroup.Children.Add(new TranslateTransform(0, 0));
                    transformGroup.Children.Add(new RotateTransform(0));
                    element.RenderTransform = transformGroup;

                    CenterAndScaleElement(element);

                    // 设置图片属性，避免被InkCanvas选择系统处理
                    element.IsHitTestVisible = true;
                    element.Focusable = false;

                    // 初始化InkCanvas选择设置
                    if (inkCanvas != null)
                    {
                        // 清除当前选择，避免显示控制点
                        inkCanvas.Select(new StrokeCollection());
                        // 同时通过图片的IsHitTestVisible和Focusable属性来避免InkCanvas选择系统的干扰
                        inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    }

                    inkCanvas.Children.Add(element);

                    // 绑定事件处理器
                    element.MouseLeftButtonDown += Element_MouseLeftButtonDown;
                    element.MouseLeftButtonUp += Element_MouseLeftButtonUp;
                    element.MouseMove += Element_MouseMove;
                    element.MouseWheel += Element_MouseWheel;

                    // 触摸事件
                    element.TouchDown += Element_TouchDown;
                    element.TouchUp += Element_TouchUp;
                    element.IsManipulationEnabled = true;
                    element.ManipulationDelta += Element_ManipulationDelta;
                    element.ManipulationCompleted += Element_ManipulationCompleted;

                    // 设置光标
                    element.Cursor = Cursors.Hand;

                    timeMachine.CommitElementInsertHistory(element);

                    // 插入图片后切换到选择模式并刷新浮动栏高光显示
                    SetCurrentToolMode(InkCanvasEditingMode.Select);
                    UpdateCurrentToolMode("select");
                    HideSubPanels("select");
                    if (element is PdfEmbeddedView)
                        _pdfSidebarNextPositionUseHostTransform = true;
                    SyncPdfPageSidebarWithCanvas();
                }
            }
        }

        // 插入图片方法
        private async void InsertImage_MouseUp_New(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation("插入图片")) return;
            var dialog = new OpenFileDialog
            {
                Filter = "图片与 PDF|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.pdf|图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif|PDF|*.pdf"
            };
            if (dialog.ShowDialog() == true)
            {
                string filePath = dialog.FileName;
                FrameworkElement element = await CreateAndCompressImageAsync(filePath);
                if (element != null)
                {
                    string timestamp = "img_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
                    element.Name = timestamp;

                    // 初始化TransformGroup
                    var transformGroup = new TransformGroup();
                    transformGroup.Children.Add(new ScaleTransform(1, 1));
                    transformGroup.Children.Add(new TranslateTransform(0, 0));
                    transformGroup.Children.Add(new RotateTransform(0));
                    element.RenderTransform = transformGroup;

                    CenterAndScaleElement(element);

                    // 设置图片属性，避免被InkCanvas选择系统处理
                    element.IsHitTestVisible = true;
                    element.Focusable = false;

                    // 初始化InkCanvas选择设置
                    if (inkCanvas != null)
                    {
                        // 清除当前选择，避免显示控制点
                        inkCanvas.Select(new StrokeCollection());
                        // 设置编辑模式为非选择模式
                        inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    }

                    inkCanvas.Children.Add(element);

                    // 绑定事件处理器
                    element.MouseLeftButtonDown += Element_MouseLeftButtonDown;
                    element.MouseLeftButtonUp += Element_MouseLeftButtonUp;
                    element.MouseMove += Element_MouseMove;
                    element.MouseWheel += Element_MouseWheel;

                    // 触摸事件
                    element.TouchDown += Element_TouchDown;
                    element.TouchUp += Element_TouchUp;
                    element.IsManipulationEnabled = true;
                    element.ManipulationDelta += Element_ManipulationDelta;
                    element.ManipulationCompleted += Element_ManipulationCompleted;

                    // 设置光标
                    element.Cursor = Cursors.Hand;

                    timeMachine.CommitElementInsertHistory(element);

                    // 插入图片后切换到选择模式并刷新浮动栏高光显示
                    SetCurrentToolMode(InkCanvasEditingMode.Select);
                    UpdateCurrentToolMode("select");
                    HideSubPanels("select");
                    if (element is PdfEmbeddedView)
                        _pdfSidebarNextPositionUseHostTransform = true;
                    SyncPdfPageSidebarWithCanvas();
                }
            }
        }

        // Keep the old method for backward compatibility
        private async void InsertImage_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TryBlockFrozenPageMutation(FloatingBarStrings.Board_InsertImage)) return;
            var dialog = new OpenFileDialog
            {
                Filter = "图片与 PDF|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.pdf|图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif|PDF|*.pdf"
            };
            if (dialog.ShowDialog() == true)
            {
                string filePath = dialog.FileName;
                FrameworkElement element = await CreateAndCompressImageAsync(filePath);
                if (element != null)
                {
                    string timestamp = "img_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
                    element.Name = timestamp;

                    // 初始化TransformGroup
                    var transformGroup = new TransformGroup();
                    transformGroup.Children.Add(new ScaleTransform(1, 1));
                    transformGroup.Children.Add(new TranslateTransform(0, 0));
                    transformGroup.Children.Add(new RotateTransform(0));
                    element.RenderTransform = transformGroup;

                    CenterAndScaleElement(element);

                    // 设置图片属性，避免被InkCanvas选择系统处理
                    element.IsHitTestVisible = true;
                    element.Focusable = false;

                    // 初始化InkCanvas选择设置
                    if (inkCanvas != null)
                    {
                        // 清除当前选择，避免显示控制点
                        inkCanvas.Select(new StrokeCollection());
                        // 设置编辑模式为非选择模式
                        inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    }

                    inkCanvas.Children.Add(element);

                    // 绑定事件处理器
                    element.MouseLeftButtonDown += Element_MouseLeftButtonDown;
                    element.MouseLeftButtonUp += Element_MouseLeftButtonUp;
                    element.MouseMove += Element_MouseMove;
                    element.MouseWheel += Element_MouseWheel;

                    // 触摸事件
                    element.TouchDown += Element_TouchDown;
                    element.TouchUp += Element_TouchUp;
                    element.IsManipulationEnabled = true;
                    element.ManipulationDelta += Element_ManipulationDelta;
                    element.ManipulationCompleted += Element_ManipulationCompleted;

                    // 设置光标
                    element.Cursor = Cursors.Hand;

                    timeMachine.CommitElementInsertHistory(element);

                    // 插入图片后切换到选择模式并刷新浮动栏高光显示
                    SetCurrentToolMode(InkCanvasEditingMode.Select);
                    UpdateCurrentToolMode("select");
                    HideSubPanels("select");
                    if (element is PdfEmbeddedView)
                        _pdfSidebarNextPositionUseHostTransform = true;
                    SyncPdfPageSidebarWithCanvas();
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
                var floatingBarPanel = GetFirstContentPanel();
                if (floatingBarPanel == null) return 0;

                double currentPosition = 0;

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
                return fe.ActualWidth > 0 ? fe.ActualWidth : 28;
            }
            return 28; // 默认宽度
        }

        /// <summary>
        /// 设置浮动栏高光显示位置
        /// </summary>
        /// <param name="mode">模式名称</param>
        private ToolbarImageButton _lastHighlightButton;
        private int _indicatorAnimationGeneration;
        private Storyboard _activeIndicatorStoryboard;
        private string _pendingHighlightMode;
        private int _highlightPositionVersion;
        private int _highlightLayoutRetryCount;

        private void SetFloatingBarHighlightPosition(string mode)
        {
            mode = NormalizeToolModeForFreeze(mode);

            ApplyFloatingBarIconHighlightImmediate(mode);

            _pendingHighlightMode = mode;
            _highlightLayoutRetryCount = 0;
            int version = ++_highlightPositionVersion;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_highlightPositionVersion != version) return;
                AnimateFloatingBarHighlightTo(mode);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void ApplyFloatingBarIconHighlightImmediate(string mode)
        {
            try
            {
                Color highlightBarColor;
                bool isDarkTheme = Settings.Appearance.Theme == 1 ||
                                   (Settings.Appearance.Theme == 2 && !ThemeHelper.IsSystemThemeLight());

                if (isDarkTheme)
                    highlightBarColor = Color.FromRgb(102, 204, 255);
                else
                    highlightBarColor = Color.FromRgb(37, 99, 235);

                if (isFloatingBarFolded || (BorderFloatingBarMoveControls != null && BorderFloatingBarMoveControls.Visibility == Visibility.Collapsed))
                {
                    return;
                }

                var foregroundBrush = new SolidColorBrush(FloatBarForegroundColor);

                void ResetIcon(ToolbarImageButton button, string iconType)
                {
                    if (button == null) return;
                    button.Icon.Brush = foregroundBrush;
                    button.Icon.Geometry = Geometry.Parse(GetCorrectIcon(iconType, false));
                }

                ResetIcon(Cursor_Icon, "cursor");
                ResetIcon(Pen_Icon, "pen");
                ResetIcon(Eraser_Icon, "eraserCircle");
                ResetIcon(EraserByStrokes_Icon, "eraserStroke");
                ResetIcon(SymbolIconSelect, "lassoSelect");

                string targetIconType = null;
                ToolbarImageButton targetButton = null;

                switch (mode)
                {
                    case "cursor":
                        targetButton = Cursor_Icon;
                        targetIconType = "cursor";
                        break;
                    case "pen":
                    case "color":
                        targetButton = Pen_Icon;
                        targetIconType = "pen";
                        break;
                    case "eraser":
                        targetButton = Eraser_Icon;
                        targetIconType = "eraserCircle";
                        break;
                    case "eraserByStrokes":
                        targetButton = EraserByStrokes_Icon;
                        targetIconType = "eraserStroke";
                        break;
                    case "select":
                        targetButton = SymbolIconSelect;
                        targetIconType = "lassoSelect";
                        break;
                    case "shape":
                        targetButton = ShapeDrawFloatingBarBtn;
                        break;
                }

                if (targetButton != null && targetIconType != null)
                {
                    targetButton.Icon.Brush = new SolidColorBrush(highlightBarColor);
                    targetButton.Icon.Geometry = Geometry.Parse(GetCorrectIcon(targetIconType, true));
                }
            }
            catch (Exception ex)
            {
                ExceptionHandler.HandleException(ex, "更新浮动栏图标高亮状态失败", LogHelper.LogType.Warning);
            }
        }

        private void AnimateFloatingBarHighlightTo(string mode)
        {
            try
            {
                var selectionBG = SelectionBGFloatingBar;
                var indicatorBar = IndicatorBarFloatingBar;
                var container = GridFloatingBarContainer;

                if (selectionBG == null || indicatorBar == null || container == null) return;

                ToolbarImageButton targetButton = null;

                switch (mode)
                {
                    case "cursor":
                        targetButton = Cursor_Icon;
                        break;
                    case "pen":
                    case "color":
                        targetButton = Pen_Icon;
                        break;
                    case "eraser":
                        targetButton = Eraser_Icon;
                        break;
                    case "eraserByStrokes":
                        targetButton = EraserByStrokes_Icon;
                        break;
                    case "select":
                        targetButton = SymbolIconSelect;
                        break;
                    case "shape":
                        targetButton = ShapeDrawFloatingBarBtn;
                        break;
                }

                if (targetButton == null || !IsElementVisibleInTree(targetButton))
                {
                    // 如果目标按钮不可见则隐藏高光
                    HideAllSelectionHighlights();
                    return;
                }

                Point nextButtonOrigin;
                try
                {
                    nextButtonOrigin = targetButton.TransformToAncestor(container).Transform(new Point(0, 0));
                }
                catch (InvalidOperationException)
                {
                    DeferFloatingBarHighlightIfLayoutPending(mode);
                    return;
                }

                double nextWidth = targetButton.ActualWidth > 0 ? targetButton.ActualWidth : 44;
                double nextPos = nextButtonOrigin.X;
                double nextTop = nextButtonOrigin.Y;

                if (nextWidth <= 0)
                {
                    DeferFloatingBarHighlightIfLayoutPending(mode);
                    return;
                }

                Color highlightBackgroundColor;
                Color highlightBarColor;
                bool isDarkTheme = Settings.Appearance.Theme == 1 ||
                                   (Settings.Appearance.Theme == 2 && !ThemeHelper.IsSystemThemeLight());

                if (isDarkTheme)
                {
                    highlightBackgroundColor = Color.FromArgb(48, 102, 204, 255);
                    highlightBarColor = Color.FromRgb(102, 204, 255);
                }
                else
                {
                    highlightBackgroundColor = Color.FromArgb(48, 59, 130, 246);
                    highlightBarColor = Color.FromRgb(37, 99, 235);
                }

                selectionBG.Background = new SolidColorBrush(highlightBackgroundColor);
                indicatorBar.Background = new SolidColorBrush(highlightBarColor);

                double indicatorBarWidth = 16;
                double nextBarLeft = nextPos + Math.Max(0, (nextWidth - indicatorBarWidth) / 2);
                double nextBarTop = nextTop + 2 + 43 + 2;

                bool isFirstShow = _lastHighlightButton == null;

                if (isFirstShow)
                {
                    selectionBG.Width = nextWidth;
                    selectionBG.Height = 43;
                    System.Windows.Controls.Canvas.SetLeft(selectionBG, nextPos);
                    System.Windows.Controls.Canvas.SetTop(selectionBG, nextTop + 2);

                    _indicatorAnimationGeneration++;
                    indicatorBar.RenderTransform = null;
                    indicatorBar.Visibility = Visibility.Visible;
                    indicatorBar.Width = indicatorBarWidth;
                    indicatorBar.Opacity = 1.0;
                    System.Windows.Controls.Canvas.SetLeft(indicatorBar, nextBarLeft);
                    System.Windows.Controls.Canvas.SetTop(indicatorBar, nextBarTop);

                    selectionBG.Visibility = Visibility.Visible;
                    _lastHighlightButton = targetButton;
                    return;
                }

                double prevBarLeft;
                if (_lastHighlightButton != null && IsElementVisibleInTree(_lastHighlightButton))
                {
                    try
                    {
                        var prevOrigin = _lastHighlightButton.TransformToAncestor(container).Transform(new Point(0, 0));
                        double prevWidth = _lastHighlightButton.ActualWidth > 0 ? _lastHighlightButton.ActualWidth : 44;
                        prevBarLeft = prevOrigin.X + Math.Max(0, (prevWidth - indicatorBarWidth) / 2);
                    }
                    catch (InvalidOperationException)
                    {
                        prevBarLeft = System.Windows.Controls.Canvas.GetLeft(indicatorBar);
                        if (double.IsNaN(prevBarLeft)) prevBarLeft = nextBarLeft;
                    }
                }
                else
                {
                    prevBarLeft = System.Windows.Controls.Canvas.GetLeft(indicatorBar);
                    if (double.IsNaN(prevBarLeft)) prevBarLeft = nextBarLeft;
                }

                _lastHighlightButton = targetButton;

                selectionBG.Width = nextWidth;
                selectionBG.Height = 43;
                System.Windows.Controls.Canvas.SetLeft(selectionBG, nextPos);
                System.Windows.Controls.Canvas.SetTop(selectionBG, nextTop + 2);
                selectionBG.Visibility = Visibility.Visible;

                indicatorBar.Visibility = Visibility.Visible;

                double distance = Math.Abs(nextBarLeft - prevBarLeft);

                if (distance < 0.5)
                {
                    _indicatorAnimationGeneration++;
                    indicatorBar.RenderTransform = null;
                    indicatorBar.Width = indicatorBarWidth;
                    System.Windows.Controls.Canvas.SetLeft(indicatorBar, nextBarLeft);
                    System.Windows.Controls.Canvas.SetTop(indicatorBar, nextBarTop);
                    return;
                }

                if (_activeIndicatorStoryboard != null)
                {
                    var oldStoryboard = _activeIndicatorStoryboard;
                    _activeIndicatorStoryboard = null;
                    try { oldStoryboard.Stop(indicatorBar); } catch { }
                    indicatorBar.RenderTransform = null;
                    indicatorBar.Opacity = 1.0;
                }

                _indicatorAnimationGeneration++;
                indicatorBar.RenderTransform = null;

                double from = prevBarLeft - nextBarLeft;
                double to = 0;
                double dimension = indicatorBarWidth;
                double stretchScale = distance / dimension + 1.0;

                System.Windows.Controls.Canvas.SetLeft(indicatorBar, nextBarLeft);
                System.Windows.Controls.Canvas.SetTop(indicatorBar, nextBarTop);
                indicatorBar.Width = indicatorBarWidth;

                indicatorBar.RenderTransform = new TransformGroup
                {
                    Children =
                    {
                        new ScaleTransform(),
                        new TranslateTransform()
                    }
                };

                var storyboard = new Storyboard { FillBehavior = FillBehavior.Stop };

                var posAnim = new DoubleAnimationUsingKeyFrames
                {
                    KeyFrames =
                    {
                        new DiscreteDoubleKeyFrame(from < to ? from : (from + (dimension * (1.0 - 1))), KeyTime.FromPercent(0.0)),
                        new DiscreteDoubleKeyFrame(from < to ? (to + (dimension * (1.0 - 1))) : to, KeyTime.FromPercent(0.333)),
                    },
                    Duration = TimeSpan.FromMilliseconds(600)
                };

                var scaleAnim = new DoubleAnimationUsingKeyFrames
                {
                    KeyFrames =
                    {
                        new DiscreteDoubleKeyFrame(1.0, KeyTime.FromPercent(0.0)),
                        new SplineDoubleKeyFrame(stretchScale, KeyTime.FromPercent(0.333), new KeySpline(new Point(0.9, 0.1), new Point(1.0, 0.2))),
                        new SplineDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0), new KeySpline(new Point(0.1, 0.9), new Point(0.2, 1.0)))
                    },
                    Duration = TimeSpan.FromMilliseconds(600)
                };

                var centerAnim = new DoubleAnimationUsingKeyFrames
                {
                    KeyFrames =
                    {
                        new DiscreteDoubleKeyFrame(from < to ? 0.0 : dimension, KeyTime.FromPercent(0.0)),
                        new DiscreteDoubleKeyFrame(from < to ? dimension : 0.0, KeyTime.FromPercent(1.0))
                    },
                    Duration = TimeSpan.FromMilliseconds(200)
                };

                Storyboard.SetTarget(posAnim, indicatorBar);
                Storyboard.SetTargetProperty(posAnim, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.X)"));
                Storyboard.SetTarget(scaleAnim, indicatorBar);
                Storyboard.SetTargetProperty(scaleAnim, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
                Storyboard.SetTarget(centerAnim, indicatorBar);
                Storyboard.SetTargetProperty(centerAnim, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.CenterX)"));

                storyboard.Children.Add(posAnim);
                storyboard.Children.Add(scaleAnim);
                storyboard.Children.Add(centerAnim);

                int currentGeneration = _indicatorAnimationGeneration;
                _activeIndicatorStoryboard = storyboard;
                storyboard.Completed += (s, e) =>
                {
                    if (currentGeneration != _indicatorAnimationGeneration) return;
                    _activeIndicatorStoryboard = null;
                    indicatorBar.RenderTransform = null;
                };

                storyboard.Begin(indicatorBar, true);
                storyboard.Pause(indicatorBar);
                storyboard.SeekAlignedToLastTick(indicatorBar, TimeSpan.Zero, TimeSeekOrigin.BeginTime);
                Dispatcher.BeginInvoke(() =>
                {
                    storyboard.Resume(indicatorBar);
                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"设置高光位置失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 通用子面板位置更新方法：根据触发按钮的位置，动态调整子面板的水平位置，
        /// 使面板水平中心对齐按钮中心。不改变面板大小，不改变上下边距。
        /// </summary>
        /// <param name="button">触发按钮元素</param>
        /// <param name="panel">需要定位的子面板</param>
        /// <param name="defaultPanelWidth">面板默认宽度（当无法从Margin计算时使用）</param>
        private void UpdateSubPanelPosition(FrameworkElement button, FrameworkElement panel, double defaultPanelWidth)
        {
            try
            {
                if (button == null || panel == null) return;

                if (panel is Popup popup)
                {
                    if (popup.PlacementTarget == null)
                    {
                        popup.PlacementTarget = button;
                    }

                    if (popup.IsOpen)
                    {
                        _popupManager?.UpdatePosition(popup);
                    }

                    return;
                }

                if (!(panel.Parent is FrameworkElement panelContainer)) return;

                var ancestor = StackPanelFloatingBarRoot;
                if (ancestor == null) return;

                var buttonTransform = button.TransformToAncestor(ancestor);
                var buttonOrigin = buttonTransform.Transform(new Point(0, 0));
                double buttonCenterX = buttonOrigin.X + button.ActualWidth / 2.0;

                var containerTransform = panelContainer.TransformToAncestor(ancestor);
                var containerOrigin = containerTransform.Transform(new Point(0, 0));
                double containerX = containerOrigin.X;

                // 计算当前面板宽度（保持不变）：panelWidth = -Margin.Left - Margin.Right
                double currentLeft = panel.Margin.Left;
                double currentRight = panel.Margin.Right;
                double panelWidth = -currentLeft - currentRight;
                if (panelWidth <= 0) panelWidth = defaultPanelWidth;

                // 计算新的左边距，使面板水平中心对齐按钮：
                //   panel_center = containerX + newLeft + panelWidth/2 = buttonCenterX
                //   => newLeft = buttonCenterX - containerX - panelWidth/2
                double newLeft = buttonCenterX - containerX - panelWidth / 2.0;

                // 保持面板宽度不变：-newLeft - newRight = panelWidth
                //   => newRight = -panelWidth - newLeft
                double newRight = -panelWidth - newLeft;

                // 清除可能残留的 Margin 动画（HoldEnd 会阻止本地值生效）
                panel.BeginAnimation(FrameworkElement.MarginProperty, null);

                // 更新边距，仅调整Left/Right，保持Top/Bottom不变
                var margin = panel.Margin;
                panel.Margin = new Thickness(newLeft, margin.Top, newRight, margin.Bottom);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新子面板位置失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 更新批注子面板（PenPalette）的弹出位置，使其水平中心对齐笔按钮。
        /// </summary>
        private void UpdatePenPalettePosition()
        {
            UpdateSubPanelPosition(Pen_Icon, PenPalette, 193);
        }

        /// <summary>
        /// 更新工具面板（BorderTools）的弹出位置，使其水平中心对齐工具按钮。
        /// </summary>
        private void UpdateBorderToolsPosition()
        {
            UpdateSubPanelPosition(ToolsFloatingBarBtn, BorderTools, 119);
        }

        /// <summary>
        /// 更新橡皮擦尺寸面板（EraserSizePanel）的弹出位置，使其水平中心对齐橡皮擦按钮。
        /// </summary>
        private void UpdateEraserSizePanelPosition()
        {
            UpdateSubPanelPosition(Eraser_Icon, EraserSizePanel, 120);
        }

        /// <summary>
        /// 更新形状绘制面板（BorderDrawShape）的弹出位置，使其水平中心对齐形状按钮。
        /// </summary>
        private void UpdateBorderDrawShapePosition()
        {
        }

        /// <summary>
        /// 更新手势面板（TwoFingerGestureBorder）的弹出位置，使其水平中心对齐手势按钮。
        /// </summary>
        private void UpdateTwoFingerGestureBorderPosition()
        {
            UpdateSubPanelPosition(Gesture_Icon, TwoFingerGestureBorder, 119);
        }

        /// <summary>
        /// 隐藏浮动栏高光显示
        /// </summary>
        private void HideFloatingBarHighlight()
        {
            HideAllSelectionHighlights();
            _lastHighlightButton = null;
        }

        private void DeferFloatingBarHighlightIfLayoutPending(string mode)
        {
            if (string.IsNullOrEmpty(mode) || _highlightLayoutRetryCount >= 3)
            {
                HideAllSelectionHighlights();
                return;
            }

            _highlightLayoutRetryCount++;
            int version = ++_highlightPositionVersion;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_highlightPositionVersion != version) return;
                AnimateFloatingBarHighlightTo(mode);
            }), DispatcherPriority.ContextIdle);
        }

        private void HideAllSelectionHighlights()
        {
            if (SelectionBGFloatingBar != null)
            {
                SelectionBGFloatingBar.Visibility = Visibility.Hidden;
            }
            if (IndicatorBarFloatingBar != null)
            {
                IndicatorBarFloatingBar.BeginAnimation(System.Windows.Controls.Canvas.LeftProperty, null);
                IndicatorBarFloatingBar.RenderTransform = null;
                IndicatorBarFloatingBar.Visibility = Visibility.Hidden;
            }
            _lastHighlightButton = null;
        }

        private (Border selectionBG, Border indicatorBar, StackPanel contentPanel) FindSelectionElementsForMode(string mode)
        {
            ToolbarImageButton targetButton = null;
            switch (mode)
            {
                case "cursor": targetButton = Cursor_Icon; break;
                case "pen":
                case "color": targetButton = Pen_Icon; break;
                case "eraser": targetButton = Eraser_Icon; break;
                case "eraserByStrokes": targetButton = EraserByStrokes_Icon; break;
                case "select": targetButton = SymbolIconSelect; break;
                case "shape": targetButton = ShapeDrawFloatingBarBtn; break;
            }
            return FindSelectionElementsForButton(targetButton);
        }

        private (Border selectionBG, Border indicatorBar, StackPanel contentPanel) FindSelectionElementsForButton(ToolbarImageButton button)
        {
            if (button == null || FloatingBarRootPanel == null) return (null, null, null);

            foreach (var border in FloatingBarRootPanel.Children.OfType<Border>())
            {
                if (border.Tag as string != ToolbarRegistry.ContentBorderTag || !(border.Child is Grid grid)) continue;

                StackPanel contentPanel = null;
                System.Windows.Controls.Canvas selectionCanvas = null;

                foreach (var gridChild in grid.Children.OfType<FrameworkElement>())
                {
                    if (gridChild is StackPanel sp && sp.Tag as string == ToolbarRegistry.ContentPanelTag)
                        contentPanel = sp;
                    else if (gridChild is System.Windows.Controls.Canvas canvas && canvas.Tag as string == ToolbarRegistry.SelectionCanvasTag)
                        selectionCanvas = canvas;
                }

                if (contentPanel == null) continue;

                bool containsButton = ContainsButton(contentPanel, button);
                if (containsButton)
                {
                    Border selectionBG = null;
                    Border indicatorBar = null;
                    if (selectionCanvas != null)
                    {
                        foreach (var canvasChild in selectionCanvas.Children.OfType<Border>())
                        {
                            if (canvasChild.Tag as string == ToolbarRegistry.SelectionBGTag)
                                selectionBG = canvasChild;
                            else if (canvasChild.Tag as string == ToolbarRegistry.IndicatorBarTag)
                                indicatorBar = canvasChild;
                        }
                    }
                    return (selectionBG, indicatorBar, contentPanel);
                }
            }

            var firstResult = GetFirstContentBorderElements();
            return firstResult;
        }

        private static bool ContainsButton(Panel panel, ToolbarImageButton button)
        {
            foreach (var child in panel.Children)
            {
                if (child == button) return true;
                if (child is Panel innerPanel && ContainsButton(innerPanel, button)) return true;
                if (child is ContentControl cc && cc.Content == button) return true;
                if (child is Decorator decorator && decorator.Child == button) return true;
            }
            return false;
        }

        private StackPanel FindContentPanelForButton(ToolbarImageButton button)
        {
            if (button == null || FloatingBarRootPanel == null) return null;

            foreach (var border in FloatingBarRootPanel.Children.OfType<Border>())
            {
                if (border.Tag as string != ToolbarRegistry.ContentBorderTag || !(border.Child is Grid grid)) continue;

                foreach (var gridChild in grid.Children.OfType<StackPanel>())
                {
                    if (gridChild.Tag as string == ToolbarRegistry.ContentPanelTag && ContainsButton(gridChild, button))
                        return gridChild;
                }
            }
            return null;
        }

        private (Border, Border, StackPanel) GetFirstContentBorderElements()
        {
            if (FloatingBarRootPanel == null) return (null, null, null);

            foreach (var border in FloatingBarRootPanel.Children.OfType<Border>())
            {
                if (border.Tag as string != ToolbarRegistry.ContentBorderTag || !(border.Child is Grid grid)) continue;

                Border selectionBG = null;
                Border indicatorBar = null;
                StackPanel contentPanel = null;

                foreach (var gridChild in grid.Children.OfType<FrameworkElement>())
                {
                    if (gridChild is StackPanel sp && sp.Tag as string == ToolbarRegistry.ContentPanelTag)
                        contentPanel = sp;
                    else if (gridChild is System.Windows.Controls.Canvas canvas && canvas.Tag as string == ToolbarRegistry.SelectionCanvasTag)
                    {
                        foreach (var canvasChild in canvas.Children.OfType<Border>())
                        {
                            if (canvasChild.Tag as string == ToolbarRegistry.SelectionBGTag)
                                selectionBG = canvasChild;
                            else if (canvasChild.Tag as string == ToolbarRegistry.IndicatorBarTag)
                                indicatorBar = canvasChild;
                        }
                    }
                }

                if (contentPanel != null)
                    return (selectionBG, indicatorBar, contentPanel);
            }
            return (null, null, null);
        }

        private static bool IsDescendantOf(DependencyObject child, DependencyObject parent)
        {
            if (child == null || parent == null) return false;
            var current = LogicalTreeHelper.GetParent(child);
            while (current != null)
            {
                if (current == parent) return true;
                current = LogicalTreeHelper.GetParent(current);
            }
            current = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (current != null)
            {
                if (current == parent) return true;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private bool IsElementVisibleInTree(FrameworkElement element)
        {
            if (element == null || element.Visibility != Visibility.Visible) return false;
            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is FrameworkElement fe && fe.Visibility != Visibility.Visible) return false;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return true;
        }

        /// <summary>
        /// 获取当前选中的模式
        /// </summary>
        /// <returns>当前选中的模式名称</returns>
        public string GetCurrentSelectedMode()
        {
            try
            {
                // 优先使用缓存的模式，避免在浮动栏刷新时返回过时的模式信息
                if (!string.IsNullOrEmpty(_currentToolMode))
                {
                    return _currentToolMode;
                }

                // 如果缓存为空，则从inkCanvas状态推断模式
                if (inkCanvas.EditingMode == InkCanvasEditingMode.Select)
                {
                    return "select";
                }

                if (inkCanvas.EditingMode == InkCanvasEditingMode.Ink)
                {
                    // 检查是否是荧光笔模式
                    if (drawingAttributes != null && drawingAttributes.IsHighlighter)
                    {
                        return "color";
                    }

                    return "pen";
                }

                if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
                {
                    // 检查是面积擦还是线擦
                    if (Eraser_Icon != null && Eraser_Icon.Visibility == Visibility.Visible)
                    {
                        return "eraser";
                    }

                    if (EraserByStrokes_Icon != null && EraserByStrokes_Icon.Visibility == Visibility.Visible)
                    {
                        return "eraserByStrokes";
                    }
                }
                else if (inkCanvas.EditingMode == InkCanvasEditingMode.None)
                {
                    return "cursor";
                }
                else if (drawingShapeMode != 0)
                {
                    return "shape";
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取当前选中模式失败: {ex.Message}", LogHelper.LogType.Error);
            }

            return "cursor"; // 默认返回鼠标模式
        }

        /// <summary>
        /// 更新当前工具模式缓存
        /// </summary>
        /// <param name="mode">模式名称</param>
        private void UpdateCurrentToolMode(string mode)
        {
            _currentToolMode = NormalizeToolModeForFreeze(mode);
        }

        #endregion

        /// <summary>
        /// 强制禁用所有双指手势功能（当多指书写模式启用时）
        /// </summary>
        private void ForceDisableTwoFingerGestures()
        {
            // 强制关闭所有双指手势设置
            Settings.Gesture.IsEnableTwoFingerTranslate = false;
            Settings.Gesture.IsEnableTwoFingerZoom = false;
            Settings.Gesture.IsEnableTwoFingerRotation = false;

            // 更新UI开关状态
            if (ToggleSwitchEnableTwoFingerTranslate != null)
                ToggleSwitchEnableTwoFingerTranslate.IsOn = false;
            if (ToggleSwitchEnableTwoFingerZoom != null)
                ToggleSwitchEnableTwoFingerZoom.IsOn = false;
            if (ToggleSwitchEnableTwoFingerRotation != null)
                ToggleSwitchEnableTwoFingerRotation.IsOn = false;

            // 更新设置窗口中的开关状态
            if (BoardToggleSwitchEnableTwoFingerTranslate != null)
                BoardToggleSwitchEnableTwoFingerTranslate.IsOn = false;
            if (BoardToggleSwitchEnableTwoFingerZoom != null)
                BoardToggleSwitchEnableTwoFingerZoom.IsOn = false;
            if (BoardToggleSwitchEnableTwoFingerRotation != null)
                BoardToggleSwitchEnableTwoFingerRotation.IsOn = false;
        }

    }
}
