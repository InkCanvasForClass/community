using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// PPT UI管理器 - 统一管理PPT相关的UI更新和样式设置
    /// </summary>
    public class PPTUIManager
    {
        #region Properties
        public bool ShowPPTButton { get; set; } = true;
        public int PPTButtonsDisplayOption { get; set; } = 2222;
        public int PPTSButtonsOption { get; set; } = 221;
        public int PPTBButtonsOption { get; set; } = 121;
        public int PPTLSButtonPosition { get; set; } = 0;
        public int PPTRSButtonPosition { get; set; } = 0;
        public int PPTLBButtonPosition { get; set; } = 0;
        public int PPTRBButtonPosition { get; set; } = 0;
        public bool EnablePPTButtonPageClickable { get; set; } = true;
        public bool EnablePPTButtonLongPressPageTurn { get; set; } = true;
        public double PPTLSButtonOpacity { get; set; } = 0.5;
        public double PPTRSButtonOpacity { get; set; } = 0.5;
        public double PPTLBButtonOpacity { get; set; } = 0.5;
        public double PPTRBButtonOpacity { get; set; } = 0.5;
        #endregion

        #region Private Fields
        private readonly MainWindow _mainWindow;
        private readonly Dispatcher _dispatcher;
        #endregion

        #region Constructor
        public PPTUIManager(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _dispatcher = _mainWindow.Dispatcher;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 更新PPT连接状态UI
        /// </summary>
        public void UpdateConnectionStatus(bool isConnected)
        {
            _dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (isConnected)
                    {
                        _mainWindow.StackPanelPPTControls.Visibility = Visibility.Visible;
                        _mainWindow.BtnPPTSlideShow.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        _mainWindow.StackPanelPPTControls.Visibility = Visibility.Collapsed;
                        _mainWindow.BtnPPTSlideShow.Visibility = Visibility.Collapsed;
                        _mainWindow.BtnPPTSlideShowEnd.Visibility = Visibility.Collapsed;
                        HideAllNavigationPanels();
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"更新PPT连接状态UI失败: {ex}", LogHelper.LogType.Error);
                }
            });
        }

        /// <summary>
        /// 更新幻灯片放映状态UI
        /// </summary>
        public void UpdateSlideShowStatus(bool isInSlideShow, int currentSlide = 0, int totalSlides = 0)
        {
            _dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (isInSlideShow)
                    {
                        _mainWindow.BtnPPTSlideShow.Visibility = Visibility.Collapsed;
                        _mainWindow.BtnPPTSlideShowEnd.Visibility = Visibility.Visible;

                        // 只有在页数有效时才更新页码显示
                        if (currentSlide > 0 && totalSlides > 0)
                        {
                            _mainWindow.PPTBtnPageNow.Text = currentSlide.ToString();
                            _mainWindow.PPTBtnPageTotal.Text = $"/ {totalSlides}";
                        }
                        else
                        {
                            // 页数无效时清空页码显示
                            _mainWindow.PPTBtnPageNow.Text = "?";
                            _mainWindow.PPTBtnPageTotal.Text = "/ ?";
                        }

                        UpdateNavigationPanelsVisibility();
                        UpdateNavigationButtonStyles();
                        if (MainWindow.Settings.Advanced.IsEnableAvoidFullScreenHelper)
                        {
                            // 设置为画板模式，允许全屏操作
                            AvoidFullScreenHelper.SetBoardMode(true);
                            _dispatcher.BeginInvoke(new Action(() =>
                            {
                                MainWindow.MoveWindow(new WindowInteropHelper(_mainWindow).Handle, 0, 0,
                                    System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width,
                                    System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height, true);
                            }), DispatcherPriority.ApplicationIdle);

                            _mainWindow.isFullScreenApplied = true; // 标记已应用全屏处理
                        }
                    }
                    else
                    {
                        _mainWindow.BtnPPTSlideShow.Visibility = Visibility.Visible;
                        _mainWindow.BtnPPTSlideShowEnd.Visibility = Visibility.Collapsed;
                        HideAllNavigationPanels();
                        if (MainWindow.Settings.Advanced.IsEnableAvoidFullScreenHelper)
                        {
                            // 恢复为非画板模式，重新启用全屏限制
                            AvoidFullScreenHelper.SetBoardMode(false);

                            _dispatcher.BeginInvoke(new Action(() =>
                            {
                                // 退出PPT放映模式，恢复到工作区域大小
                                var workingArea = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
                                MainWindow.MoveWindow(new WindowInteropHelper(_mainWindow).Handle,
                                    workingArea.X, workingArea.Y,
                                    workingArea.Width, workingArea.Height, true);
                            }), DispatcherPriority.ApplicationIdle);

                            _mainWindow.isFullScreenApplied = false; // 标记全屏处理已还原
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"更新幻灯片放映状态UI失败: {ex}", LogHelper.LogType.Error);
                }
            });
        }

        /// <summary>
        /// 更新当前页码显示
        /// </summary>
        public void UpdateCurrentSlideNumber(int currentSlide, int totalSlides)
        {
            _dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // 只有在页数有效时才更新页码显示
                    if (currentSlide > 0 && totalSlides > 0)
                    {
                        _mainWindow.PPTBtnPageNow.Text = currentSlide.ToString();
                        _mainWindow.PPTBtnPageTotal.Text = $"/ {totalSlides}";
                    }
                    else
                    {
                        // 页数无效时清空页码显示
                        _mainWindow.PPTBtnPageNow.Text = "?";
                        _mainWindow.PPTBtnPageTotal.Text = "/ ?";
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"更新页码显示失败: {ex}", LogHelper.LogType.Error);
                }
            });
        }

        /// <summary>
        /// 处理PPT放映状态变化
        /// </summary>
        public void OnSlideShowStateChanged(bool isInSlideShow)
        {
            _dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (!isInSlideShow)
                    {
                        // 如果不在放映模式，隐藏所有导航面板
                        HideAllNavigationPanels();
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"处理PPT放映状态变化失败: {ex}", LogHelper.LogType.Error);
                }
            });
        }

        /// <summary>
        /// 更新导航面板显示状态
        /// </summary>
        public void UpdateNavigationPanelsVisibility()
        {
            _dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // 检查是否应该显示PPT按钮
                    // 不仅要检查按钮设置，还要确保确实在PPT放映模式下且页数有效
                    bool isInSlideShow = _mainWindow.PPTManager?.IsInSlideShow == true;
                    int slidesCount = _mainWindow.PPTManager?.SlidesCount ?? 0;
                    bool hasValidPageCount = slidesCount > 0;

                    bool shouldShowButtons = ShowPPTButton &&
                                          _mainWindow.BtnPPTSlideShowEnd.Visibility == Visibility.Visible &&
                                          isInSlideShow &&
                                          hasValidPageCount &&
                                          !MainWindow.Settings.Automation.IsAutoFoldInPPTSlideShow;

                    if (!shouldShowButtons)
                    {
                        HideAllNavigationPanels();
                        return;
                    }

                    // 设置侧边按钮位置
                    _mainWindow.LeftSidePanelForPPTNavigation.Margin = new Thickness(0, 0, 0, PPTLSButtonPosition * 2);
                    _mainWindow.RightSidePanelForPPTNavigation.Margin = new Thickness(0, 0, 0, PPTRSButtonPosition * 2);

                    // 设置底部按钮水平位置
                    _mainWindow.LeftBottomPanelForPPTNavigation.Margin = new Thickness(6 + PPTLBButtonPosition, 0, 0, 6);
                    _mainWindow.RightBottomPanelForPPTNavigation.Margin = new Thickness(0, 0, 6 + PPTRBButtonPosition, 6);

                    // 根据显示选项设置面板可见性
                    var displayOption = PPTButtonsDisplayOption.ToString();
                    if (displayOption.Length >= 4)
                    {
                        var options = displayOption.ToCharArray();

                        // 左下角面板
                        if (options[0] == '2')
                            AnimationsHelper.ShowWithFadeIn(_mainWindow.LeftBottomPanelForPPTNavigation);
                        else
                            _mainWindow.LeftBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;

                        // 右下角面板
                        if (options[1] == '2')
                            AnimationsHelper.ShowWithFadeIn(_mainWindow.RightBottomPanelForPPTNavigation);
                        else
                            _mainWindow.RightBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;

                        // 左侧面板
                        if (options[2] == '2')
                            AnimationsHelper.ShowWithFadeIn(_mainWindow.LeftSidePanelForPPTNavigation);
                        else
                            _mainWindow.LeftSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;

                        // 右侧面板
                        if (options[3] == '2')
                            AnimationsHelper.ShowWithFadeIn(_mainWindow.RightSidePanelForPPTNavigation);
                        else
                            _mainWindow.RightSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"更新导航面板显示状态失败: {ex}", LogHelper.LogType.Error);
                }
            });
        }

        /// <summary>
        /// 更新导航按钮样式
        /// </summary>
        public void UpdateNavigationButtonStyles()
        {
            _dispatcher.InvokeAsync(() =>
            {
                try
                {
                    UpdateSideButtonStyles();
                    UpdateBottomButtonStyles();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"更新导航按钮样式失败: {ex}", LogHelper.LogType.Error);
                }
            });
        }

        /// <summary>
        /// 隐藏所有导航面板
        /// </summary>
        public void HideAllNavigationPanels()
        {
            _dispatcher.InvokeAsync(() =>
            {
                try
                {
                    _mainWindow.LeftBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                    _mainWindow.RightBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                    _mainWindow.LeftSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                    _mainWindow.RightSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"隐藏导航面板失败: {ex}", LogHelper.LogType.Error);
                }
            });
        }

        /// <summary>
        /// 显示/隐藏侧边栏退出按钮
        /// </summary>
        public void UpdateSidebarExitButtons(bool show)
        {
            _dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var visibility = show ? Visibility.Visible : Visibility.Collapsed;

                    if (_mainWindow.BtnExitPptFromSidebarLeft != null)
                        _mainWindow.BtnExitPptFromSidebarLeft.Visibility = visibility;

                    if (_mainWindow.BtnExitPptFromSidebarRight != null)
                        _mainWindow.BtnExitPptFromSidebarRight.Visibility = visibility;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"更新侧边栏退出按钮失败: {ex}", LogHelper.LogType.Error);
                }
            });
        }

        /// <summary>
        /// 设置浮动栏透明度
        /// </summary>
        public void SetFloatingBarOpacity(double opacity)
        {
            _dispatcher.InvokeAsync(() =>
            {
                try
                {
                    _mainWindow.ViewboxFloatingBar.Opacity = opacity;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"设置浮动栏透明度失败: {ex}", LogHelper.LogType.Error);
                }
            });
        }

        /// <summary>
        /// 设置主面板边距
        /// </summary>
        public void SetMainPanelMargin(Thickness margin)
        {
            _dispatcher.InvokeAsync(() =>
            {
                try
                {
                    _mainWindow.ViewBoxStackPanelMain.Margin = margin;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"设置主面板边距失败: {ex}", LogHelper.LogType.Error);
                }
            });
        }
        #endregion

        #region Private Methods
        private void UpdateSideButtonStyles()
        {
            try
            {
                var sideOption = PPTSButtonsOption.ToString();
                if (sideOption.Length < 3) return;

                var options = sideOption.ToCharArray();

                // 页码按钮显示
                var pageButtonVisibility = options[0] == '2' ? Visibility.Visible : Visibility.Collapsed;
                _mainWindow.PPTLSPageButton.Visibility = pageButtonVisibility;
                _mainWindow.PPTRSPageButton.Visibility = pageButtonVisibility;

                // 透明度设置 - 直接使用用户设置的透明度值
                _mainWindow.PPTBtnLSBorder.Opacity = PPTLSButtonOpacity;
                _mainWindow.PPTBtnRSBorder.Opacity = PPTRSButtonOpacity;

                // 颜色主题
                bool isDarkTheme = options[2] == '2';
                ApplyButtonTheme(_mainWindow.PPTBtnLSBorder, _mainWindow.PPTBtnRSBorder, isDarkTheme, true);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新侧边按钮样式失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void UpdateBottomButtonStyles()
        {
            try
            {
                var bottomOption = PPTBButtonsOption.ToString();
                if (bottomOption.Length < 3) return;

                var options = bottomOption.ToCharArray();

                // 页码按钮显示
                var pageButtonVisibility = options[0] == '2' ? Visibility.Visible : Visibility.Collapsed;
                _mainWindow.PPTLBPageButton.Visibility = pageButtonVisibility;
                _mainWindow.PPTRBPageButton.Visibility = pageButtonVisibility;

                // 透明度设置 - 直接使用用户设置的透明度值
                _mainWindow.PPTBtnLBBorder.Opacity = PPTLBButtonOpacity;
                _mainWindow.PPTBtnRBBorder.Opacity = PPTRBButtonOpacity;

                // 颜色主题
                bool isDarkTheme = options[2] == '2';
                ApplyButtonTheme(_mainWindow.PPTBtnLBBorder, _mainWindow.PPTBtnRBBorder, isDarkTheme, false);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新底部按钮样式失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void ApplyButtonTheme(Border leftBorder, Border rightBorder, bool isDarkTheme, bool isSideButton)
        {
            try
            {
                Color backgroundColor, borderColor, foregroundColor, feedbackColor;

                if (isDarkTheme)
                {
                    backgroundColor = Color.FromRgb(39, 39, 42);
                    borderColor = Color.FromRgb(82, 82, 91);
                    foregroundColor = Colors.White;
                    feedbackColor = Colors.White;
                }
                else
                {
                    backgroundColor = Color.FromRgb(244, 244, 245);
                    borderColor = Color.FromRgb(161, 161, 170);
                    foregroundColor = Color.FromRgb(39, 39, 42);
                    feedbackColor = Color.FromRgb(24, 24, 27);
                }

                // 应用背景和边框颜色
                var backgroundBrush = new SolidColorBrush(backgroundColor);
                var borderBrush = new SolidColorBrush(borderColor);

                leftBorder.Background = backgroundBrush;
                leftBorder.BorderBrush = borderBrush;
                rightBorder.Background = backgroundBrush;
                rightBorder.BorderBrush = borderBrush;

                // 应用图标和文字颜色
                var foregroundBrush = new SolidColorBrush(foregroundColor);
                var feedbackBrush = new SolidColorBrush(feedbackColor);

                if (isSideButton)
                {
                    ApplySideButtonColors(foregroundBrush, feedbackBrush);
                }
                else
                {
                    ApplyBottomButtonColors(foregroundBrush, feedbackBrush);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用按钮主题失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void ApplySideButtonColors(SolidColorBrush foregroundBrush, SolidColorBrush feedbackBrush)
        {
            // 图标颜色
            _mainWindow.PPTLSPreviousButtonGeometry.Brush = foregroundBrush;
            _mainWindow.PPTRSPreviousButtonGeometry.Brush = foregroundBrush;
            _mainWindow.PPTLSNextButtonGeometry.Brush = foregroundBrush;
            _mainWindow.PPTRSNextButtonGeometry.Brush = foregroundBrush;

            // 反馈背景颜色
            _mainWindow.PPTLSPreviousButtonFeedbackBorder.Background = feedbackBrush;
            _mainWindow.PPTRSPreviousButtonFeedbackBorder.Background = feedbackBrush;
            _mainWindow.PPTLSPageButtonFeedbackBorder.Background = feedbackBrush;
            _mainWindow.PPTRSPageButtonFeedbackBorder.Background = feedbackBrush;
            _mainWindow.PPTLSNextButtonFeedbackBorder.Background = feedbackBrush;
            _mainWindow.PPTRSNextButtonFeedbackBorder.Background = feedbackBrush;

            // 文字颜色
            TextBlock.SetForeground(_mainWindow.PPTLSPageButton, foregroundBrush);
            TextBlock.SetForeground(_mainWindow.PPTRSPageButton, foregroundBrush);
        }

        private void ApplyBottomButtonColors(SolidColorBrush foregroundBrush, SolidColorBrush feedbackBrush)
        {
            // 图标颜色
            _mainWindow.PPTLBPreviousButtonGeometry.Brush = foregroundBrush;
            _mainWindow.PPTRBPreviousButtonGeometry.Brush = foregroundBrush;
            _mainWindow.PPTLBNextButtonGeometry.Brush = foregroundBrush;
            _mainWindow.PPTRBNextButtonGeometry.Brush = foregroundBrush;

            // 反馈背景颜色
            _mainWindow.PPTLBPreviousButtonFeedbackBorder.Background = feedbackBrush;
            _mainWindow.PPTRBPreviousButtonFeedbackBorder.Background = feedbackBrush;
            _mainWindow.PPTLBPageButtonFeedbackBorder.Background = feedbackBrush;
            _mainWindow.PPTRBPageButtonFeedbackBorder.Background = feedbackBrush;
            _mainWindow.PPTLBNextButtonFeedbackBorder.Background = feedbackBrush;
            _mainWindow.PPTRBNextButtonFeedbackBorder.Background = feedbackBrush;

            // 文字颜色
            TextBlock.SetForeground(_mainWindow.PPTLBPageButton, foregroundBrush);
            TextBlock.SetForeground(_mainWindow.PPTRBPageButton, foregroundBrush);
        }
        #endregion
    }
}
