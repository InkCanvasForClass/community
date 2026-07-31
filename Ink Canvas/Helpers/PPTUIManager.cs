using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
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
        public double PPTNavBarScale { get; set; } = 1.0;
        public double PPTLSButtonScale { get; set; } = 1.0;
        public double PPTRSButtonScale { get; set; } = 1.0;
        public double PPTLBButtonScale { get; set; } = 1.0;
        public double PPTRBButtonScale { get; set; } = 1.0;
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
                        _mainWindow.ArePPTControlsVisible = true;
                        // Old UI removed:                         _mainWindow.BtnPPTSlideShow.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        _mainWindow.ArePPTControlsVisible = false;
                        // Old UI removed:                         _mainWindow.BtnPPTSlideShow.Visibility = Visibility.Collapsed;
                        _mainWindow.IsInPPTPresentationMode = false;
                        _mainWindow.UpdateToolbarComponentVisibility();
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
                        bool wasInSlideShow = _mainWindow.IsInPPTPresentationMode;

                        // Old UI removed:                         _mainWindow.BtnPPTSlideShow.Visibility = Visibility.Collapsed;
                        _mainWindow.IsInPPTPresentationMode = true;
                        _mainWindow.UpdateToolbarComponentVisibility();

                        // 同步页码到所有翻页条 + 兼容旧绑定的隐藏 placeholder
                        SetPageNumberOnAllBars(currentSlide, totalSlides);

                        UpdateNavigationPanelsVisibility();
                        UpdateNavigationButtonStyles();
                        _mainWindow.UpdatePPTTimeCapsuleVisibility();
                        _mainWindow.UpdatePPTQuickPanelVisibility();
                        if (!wasInSlideShow)
                        {
                            _dispatcher.BeginInvoke(new Action(async () =>
                            {
                                await Task.Delay(1000);
                                _mainWindow.ShowPPTModePromptNotification();
                            }), DispatcherPriority.ContextIdle);
                        }
                        if (MainWindow.Settings.Advanced.IsEnableAvoidFullScreenHelper)
                        {
                            // 设置为画板模式，允许全屏操作
                            AvoidFullScreenHelper.SetBoardMode(true);
                            _dispatcher.BeginInvoke(new Action(() =>
                            {
                                MainWindow.MoveWindow(new WindowInteropHelper(_mainWindow).Handle, 0, 0,
                                    System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width,
                                    System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height, true);

                                // MoveWindow 触发的 WM_WINDOWPOSCHANGING + 重绘会打断面板的 ShowWithFadeIn 动画，
                                // 在窗口尺寸最终确定后重新评估一次翻页面板的可见性。
                                UpdateNavigationPanelsVisibility();
                                UpdateNavigationButtonStyles();
                            }), DispatcherPriority.ApplicationIdle);

                            _mainWindow.isFullScreenApplied = true; // 标记已应用全屏处理
                        }
                    }
                    else
                    {
                        // Old UI removed:                         _mainWindow.BtnPPTSlideShow.Visibility = Visibility.Visible;
                        _mainWindow.IsInPPTPresentationMode = false;
                        _mainWindow.UpdateToolbarComponentVisibility();
                        HideAllNavigationPanels();
                        _mainWindow.UpdatePPTTimeCapsuleVisibility();
                        _mainWindow.UpdatePPTQuickPanelVisibility();
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
                    SetPageNumberOnAllBars(currentSlide, totalSlides);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"更新页码显示失败: {ex}", LogHelper.LogType.Error);
                }
            });
        }

        private void SetPageNumberOnAllBars(int currentSlide, int totalSlides)
        {
            var bars = new[]
            {
                _mainWindow.LeftBottomPanelForPPTNavigation,
                _mainWindow.RightBottomPanelForPPTNavigation,
                _mainWindow.LeftSidePanelForPPTNavigation,
                _mainWindow.RightSidePanelForPPTNavigation,
            };
            foreach (var bar in bars)
            {
                if (bar == null) continue;
                bar.CurrentSlide = currentSlide;
                bar.TotalSlides = totalSlides;
            }
            // 兼容旧绑定（其它界面通过 ElementName 引用 PPTBtnPageNow / PPTBtnPageTotal）
            if (currentSlide > 0 && totalSlides > 0)
            {
                _mainWindow.PPTBtnPageNow.Text = currentSlide.ToString();
                _mainWindow.PPTBtnPageTotal.Text = $"/ {totalSlides}";
            }
            else
            {
                _mainWindow.PPTBtnPageNow.Text = "?";
                _mainWindow.PPTBtnPageTotal.Text = "/ ?";
            }
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
                    // 放映来源有两种：真实 PowerPoint，或插件注册的外部演示源（PDF 等）。
                    // 外部演示源没有 PPTManager 会话，页数由它自己声明，不能用 PPTManager 判断。
                    bool isExternal = _mainWindow.IsExternalPresentationActive;
                    bool isInSlideShow = isExternal || _mainWindow.PPTManager?.IsInSlideShow == true;
                    int slidesCount = isExternal
                        ? _mainWindow.ExternalPresentationPageCount
                        : (_mainWindow.PPTManager?.SlidesCount ?? 0);
                    bool hasValidPageCount = slidesCount > 0;

                    bool shouldShowButtons = ShowPPTButton &&
                                          _mainWindow.IsInPPTPresentationMode &&
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
                    ApplyNavBarScale();
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

                    if (_mainWindow.BtnExitPPTFromSidebarLeft != null)
                        _mainWindow.BtnExitPPTFromSidebarLeft.Visibility = visibility;

                    if (_mainWindow.BtnExitPPTFromSidebarRight != null)
                        _mainWindow.BtnExitPPTFromSidebarRight.Visibility = visibility;
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
                    // Old UI removed:                     _mainWindow.ViewBoxStackPanelMain.Margin = margin;
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
                var ppt = MainWindow.Settings.PowerPointSettings;

                // 左侧
                var leftPageButtonVisibility = ppt.PPTLSShowPageNumber ? Visibility.Visible : Visibility.Collapsed;
                _mainWindow.LeftSidePanelForPPTNavigation.SetPageButtonVisibility(leftPageButtonVisibility);
                _mainWindow.LeftSidePanelForPPTNavigation.SetBarOpacity(ppt.PPTLSButtonOpacity);
                _mainWindow.LeftSidePanelForPPTNavigation.ApplyTheme(ppt.PPTLSBlackBackground);

                // 右侧
                var rightPageButtonVisibility = ppt.PPTRSShowPageNumber ? Visibility.Visible : Visibility.Collapsed;
                _mainWindow.RightSidePanelForPPTNavigation.SetPageButtonVisibility(rightPageButtonVisibility);
                _mainWindow.RightSidePanelForPPTNavigation.SetBarOpacity(ppt.PPTRSButtonOpacity);
                _mainWindow.RightSidePanelForPPTNavigation.ApplyTheme(ppt.PPTRSBlackBackground);
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
                var ppt = MainWindow.Settings.PowerPointSettings;

                // 左下
                var leftBottomPageButtonVisibility = ppt.PPTLBShowPageNumber ? Visibility.Visible : Visibility.Collapsed;
                _mainWindow.LeftBottomPanelForPPTNavigation.SetPageButtonVisibility(leftBottomPageButtonVisibility);
                _mainWindow.LeftBottomPanelForPPTNavigation.SetBarOpacity(ppt.PPTLBButtonOpacity);
                _mainWindow.LeftBottomPanelForPPTNavigation.ApplyTheme(ppt.PPTLBBlackBackground);

                // 右下
                var rightBottomPageButtonVisibility = ppt.PPTRBShowPageNumber ? Visibility.Visible : Visibility.Collapsed;
                _mainWindow.RightBottomPanelForPPTNavigation.SetPageButtonVisibility(rightBottomPageButtonVisibility);
                _mainWindow.RightBottomPanelForPPTNavigation.SetBarOpacity(ppt.PPTRBButtonOpacity);
                _mainWindow.RightBottomPanelForPPTNavigation.ApplyTheme(ppt.PPTRBBlackBackground);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新底部按钮样式失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void ApplyNavBarScale()
        {
            try
            {
                _mainWindow.LeftBottomPanelForPPTNavigation.SetBarScale(PPTLBButtonScale);
                _mainWindow.RightBottomPanelForPPTNavigation.SetBarScale(PPTRBButtonScale);
                _mainWindow.LeftSidePanelForPPTNavigation.SetBarScale(PPTLSButtonScale);
                _mainWindow.RightSidePanelForPPTNavigation.SetBarScale(PPTRSButtonScale);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用翻页按钮缩放失败: {ex}", LogHelper.LogType.Error);
            }
        }
        #endregion
    }
}
