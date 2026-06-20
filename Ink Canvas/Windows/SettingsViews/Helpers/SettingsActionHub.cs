using Ink_Canvas.Helpers;
using InkCanvasPPTAgent.Contracts;
using System;
using System.Windows;

namespace Ink_Canvas.Windows.SettingsViews.Helpers
{
    public static class SettingsActionHub
    {
        private static MainWindow GetMainWindow() => Application.Current.MainWindow as MainWindow;

        #region Canvas

        public static void OnIsShowCursorChanged(bool value)
        {
            var mw = GetMainWindow();
            if (mw != null && mw.inkCanvas != null)
            {
                mw.inkCanvas.ForceCursor = value;
                mw.SetCursorBasedOnEditingMode(mw.inkCanvas);
            }
        }

        public static void OnPenCursorTypeChanged(int selectedIndex)
        {
            var mw = GetMainWindow();
            if (mw != null && mw.inkCanvas != null)
                mw.SetCursorBasedOnEditingMode(mw.inkCanvas);
        }

        public static void OnCustomPenCursorPathChanged()
        {
            var mw = GetMainWindow();
            if (mw != null && mw.inkCanvas != null)
            {
                MainWindow.ClearCustomCursorCache();
                mw.SetCursorBasedOnEditingMode(mw.inkCanvas);
            }
        }

        public static void OnEnablePressureTouchModeChanged(bool value)
        {
            if (value && SettingsManager.Settings.Canvas.DisablePressure)
            {
                SettingsManager.Settings.Canvas.DisablePressure = false;
                OnDisablePressureChanged(false);
            }
        }

        public static void OnDisablePressureChanged(bool value)
        {
            if (value && SettingsManager.Settings.Canvas.EnablePressureTouchMode)
            {
                SettingsManager.Settings.Canvas.EnablePressureTouchMode = false;
            }
            var mw = GetMainWindow();
            if (mw != null && mw.inkCanvas != null)
                mw.inkCanvas.DefaultDrawingAttributes.IgnorePressure = value;
        }

        public static void OnEraserSizeChanged(int selectedIndex)
        {
            var mw = GetMainWindow();
            if (mw != null)
            {
                if (mw.ComboBoxEraserSizeFloatingBar != null)
                    mw.ComboBoxEraserSizeFloatingBar.SelectedIndex = selectedIndex;
                if (mw.BoardComboBoxEraserSize != null)
                    mw.BoardComboBoxEraserSize.SelectedIndex = selectedIndex;
            }
        }

        public static void OnCurveSmoothingModeChanged(bool fitToCurve, bool useAdvancedBezier)
        {
            var mw = GetMainWindow();
            if (mw != null && mw.inkCanvas != null)
            {
                if (useAdvancedBezier)
                    mw.inkCanvas.DefaultDrawingAttributes.FitToCurve = false;
                else
                    mw.inkCanvas.DefaultDrawingAttributes.FitToCurve = fitToCurve;
            }
        }

        #endregion

        #region Appearance

        public static void OnThemeChanged(int themeIndex)
        {
            var mw = GetMainWindow();
            if (mw != null) mw.ApplyTheme(themeIndex);
        }

        public static void OnLanguageChanged(string language)
        {
            LocalizationHelper.TrySetCulture(language);
            var mw = GetMainWindow();
            if (mw != null)
            {
                mw._isReloadingForLanguageChange = true;
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var newWindow = new MainWindow
                        {
                            WindowState = mw.WindowState,
                            Left = mw.Left,
                            Top = mw.Top
                        };
                        newWindow.Show();
                        Application.Current.MainWindow = newWindow;
                        mw.Close();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"重建主窗口以应用语言时出错: {ex.Message}");
                        mw._isReloadingForLanguageChange = false;
                    }
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }

        public static void OnFloatingBarImgChanged()
        {
            var mw = GetMainWindow();
            if (mw != null) mw.UpdateFloatingBarIcon();
        }

        public static void OnBlackBoardScaleChanged(double value)
        {
            var mw = GetMainWindow();
            if (mw != null)
            {
                mw.ViewboxBlackboardCenterSideScaleTransform.ScaleX = value;
                mw.ViewboxBlackboardCenterSideScaleTransform.ScaleY = value;
            }
        }

        public static void OnBlackBoardLeftScaleChanged(double value)
        {
            var mw = GetMainWindow();
            if (mw != null)
            {
                mw.ViewboxBlackboardLeftSideScaleTransform.ScaleX = value;
                mw.ViewboxBlackboardLeftSideScaleTransform.ScaleY = value;
            }
        }

        public static void OnBlackBoardRightScaleChanged(double value)
        {
            var mw = GetMainWindow();
            if (mw != null)
            {
                mw.ViewboxBlackboardRightSideScaleTransform.ScaleX = value;
                mw.ViewboxBlackboardRightSideScaleTransform.ScaleY = value;
            }
        }

        public static void OnBoardToolbarLeftOpacityChanged(double value)
        {
            var mw = GetMainWindow();
            if (mw != null) mw.ViewboxBlackboardLeftSide.Opacity = value;
        }

        public static void OnBoardToolbarCenterOpacityChanged(double value)
        {
            var mw = GetMainWindow();
            if (mw != null) mw.ViewboxBlackboardCenterSide.Opacity = value;
        }

        public static void OnBoardToolbarRightOpacityChanged(double value)
        {
            var mw = GetMainWindow();
            if (mw != null) mw.ViewboxBlackboardRightSide.Opacity = value;
        }

        public static void OnFloatingBarMenuOpacityChanged(double value)
        {
            var mw = GetMainWindow();
            if (mw != null) mw.ApplyFloatingBarMenuOpacity();
        }

        public static void OnFloatingBarMenuOpacityInPPTChanged(double value)
        {
            var mw = GetMainWindow();
            if (mw != null && mw.currentMode == 2)
                mw.ApplyFloatingBarMenuOpacity();
        }

        public static void OnBoardMenuOpacityChanged(double value)
        {
            var mw = GetMainWindow();
            if (mw != null) mw.ApplyBoardMenuOpacity();
        }

        public static void OnTimeDisplayInWhiteboardChanged(bool isOn)
        {
            var mw = GetMainWindow();
            if (mw != null && mw.currentMode == 1)
            {
                var vis = isOn ? Visibility.Visible : Visibility.Collapsed;
                mw.WaterMarkTime.Visibility = vis;
                mw.WaterMarkDate.Visibility = vis;
            }
        }

        public static void OnChickenSoupInWhiteboardChanged(bool isOn, bool isTimeDisplayOn)
        {
            var mw = GetMainWindow();
            if (mw != null && mw.currentMode == 1 && isTimeDisplayOn)
            {
                mw.BlackBoardWaterMark.Visibility = isOn ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public static void OnChickenSoupSourceChanged()
        {
            var mw = GetMainWindow();
            if (mw != null) mw.UpdateChickenSoupTextAsync().ConfigureAwait(false);
        }

        public static void OnChickenSoupPositionChanged()
        {
            var mw = GetMainWindow();
            if (mw != null) mw.ApplyChickenSoupPosition();
        }

        public static void OnQuickPanelBottomOffsetChanged(double value)
        {
            var mw = GetMainWindow();
            if (mw != null) mw.ApplyQuickPanelBottomOffset(value);
        }

        public static void OnQuickPanelOpacityChanged(double value)
        {
            var mw = GetMainWindow();
            if (mw != null) mw.ApplyQuickPanelOpacity(value);
        }

        public static void OnAutoCollapseQuickPanelChanged()
        {
            var mw = GetMainWindow();
            if (mw != null) mw.UpdateAutoCollapseQuickPanelTimer();
        }

        public static void OnUnFoldButtonImageTypeChanged(int selectedIndex)
        {
            var mw = GetMainWindow();
            if (mw == null) return;

            mw.LeftSidePanel?.ApplySettings();
            mw.RightSidePanel?.ApplySettings();

            if (selectedIndex == 0)
            {
                mw.RightUnFoldBtnImgChevron.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Resources/new-icons/unfold-chevron.png"));
                mw.RightUnFoldBtnImgChevron.Width = 14; mw.RightUnFoldBtnImgChevron.Height = 14;
                mw.RightUnFoldBtnImgChevron.RenderTransform = new System.Windows.Media.RotateTransform(180);
                mw.LeftUnFoldBtnImgChevron.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Resources/new-icons/unfold-chevron.png"));
                mw.LeftUnFoldBtnImgChevron.Width = 14; mw.LeftUnFoldBtnImgChevron.Height = 14;
                mw.LeftUnFoldBtnImgChevron.RenderTransform = null;
            }
            else if (selectedIndex == 1)
            {
                mw.LeftUnFoldBtnImgChevron.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Resources/new-icons/pen-white.png"));
                mw.LeftUnFoldBtnImgChevron.Width = 18; mw.LeftUnFoldBtnImgChevron.Height = 18;
                mw.LeftUnFoldBtnImgChevron.RenderTransformOrigin = new Point(0.5, 0.5);
                mw.LeftUnFoldBtnImgChevron.RenderTransform = new System.Windows.Media.ScaleTransform(-1, 1);
                mw.RightUnFoldBtnImgChevron.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Resources/new-icons/pen-white.png"));
                mw.RightUnFoldBtnImgChevron.Width = 18; mw.RightUnFoldBtnImgChevron.Height = 18;
                mw.RightUnFoldBtnImgChevron.RenderTransform = null;
            }
        }

        public static void OnUseLegacyFloatingBarUIChanged()
        {
            var mw = GetMainWindow();
            if (mw != null) mw.UpdateFloatingBarIcons();
        }

        public static void OnFloatingBarScaleChanged(double actualScale)
        {
            var mw = GetMainWindow();
            if (mw != null)
            {
                mw._userHasDraggedFloatingBar = false;
                mw.pointDesktop = new Point(-1, -1);
                mw.pointPPT = new Point(-1, -1);
                // 紧凑模式叠加缩放因子
                double effectiveScale = actualScale;
                if (SettingsManager.Settings.Appearance.CompactFloatingBar)
                    effectiveScale = actualScale * MainWindow.CompactFloatingBarScaleFactor;
                mw.ViewboxFloatingBarScaleTransform.ScaleX = effectiveScale;
                mw.ViewboxFloatingBarScaleTransform.ScaleY = effectiveScale;
                if (mw.IsInPPTPresentationMode)
                    mw.ViewboxFloatingBarMarginAnimation(60);
                else
                    mw.ViewboxFloatingBarMarginAnimation(100, true);
            }
        }

        public static void OnFloatingBarOpacityChanged(double value)
        {
            var mw = GetMainWindow();
            if (mw != null) mw.ViewboxFloatingBar.Opacity = value;
        }

        public static void OnFloatingBarOpacityInPPTChanged(double value)
        {
            var mw = GetMainWindow();
            if (mw != null && mw.currentMode == 2)
                mw.ViewboxFloatingBar.Opacity = value;
        }

        public static void OnToolbarPositionChanged(ToolbarPosition position)
        {
            var mw = GetMainWindow();
            if (mw != null) mw.UpdateToolbarPosition();
        }

        public static void OnReverseToolbarContentChanged(bool isOn)
        {
            var mw = GetMainWindow();
            if (mw != null) mw.UpdateToolbarPosition();
        }

        public static void OnCompactFloatingBarChanged(bool isOn)
        {
            var mw = GetMainWindow();
            if (mw != null) mw.ApplyCompactFloatingBarMode(isOn);
        }

        public static void OnHideFloatingBarBorderChanged(bool isOn)
        {
            var mw = GetMainWindow();
            if (mw != null) mw.ApplyHideFloatingBarBorder(isOn);
        }

        #endregion

        #region Advanced

        public static void OnHardwareAccelerationChanged()
        {
            var mw = GetMainWindow();
            if (mw != null) mw.UpdateInkSmoothingConfig();
        }

        public static void OnNibModeBoundsWidthChanged()
        {
            var mw = GetMainWindow();
            if (mw != null)
            {
                if (SettingsManager.Settings.Startup.IsEnableNibMode)
                    mw.BoundsWidth = SettingsManager.Settings.Advanced.NibModeBoundsWidth;
                else
                    mw.BoundsWidth = SettingsManager.Settings.Advanced.FingerModeBoundsWidth;
            }
        }

        public static void OnFingerModeBoundsWidthChanged()
        {
            OnNibModeBoundsWidthChanged();
        }

        #endregion

        #region Automation

        public static void OnAutoFoldChanged()
        {
            var mw = GetMainWindow();
            if (mw != null) mw.StartOrStoptimerCheckAutoFold();
        }

        public static void OnAutoKillChanged()
        {
            var mw = GetMainWindow();
            if (mw == null) return;
            var auto = SettingsManager.Settings.Automation;
            bool anyKill = auto.IsAutoKillEasiNote || auto.IsAutoKillPPTService ||
                auto.IsAutoKillHiteAnnotation || auto.IsAutoKillInkCanvas ||
                auto.IsAutoKillICA || auto.IsAutoKillIDT || auto.IsAutoKillVComYouJiao ||
                auto.IsAutoKillSeewoLauncher2DesktopAnnotation;
            mw.UpdateAutoKillProcessTimer(anyKill);
        }

        public static void OnAutoSaveStrokesChanged()
        {
            var mw = GetMainWindow();
            if (mw != null) mw.UpdateAutoSaveStrokesTimer();
        }

        public static void OnFloatingWindowInterceptorRuleChanged(string ruleKey, bool isOn)
        {
            var mw = GetMainWindow();
            if (mw != null)
            {
                if (Enum.TryParse<FloatingWindowInterceptor.InterceptType>(ruleKey, out var interceptType))
                    mw.SetInterceptRule(interceptType, isOn);
            }
        }

        public static void OnFloatingWindowInterceptorEnabledCheck(bool anyOn)
        {
            var mw = GetMainWindow();
            if (mw == null) return;
            SettingsManager.Settings.Automation.FloatingWindowInterceptor.IsEnabled = anyOn;
            if (mw._floatingWindowInterceptorManager != null)
            {
                if (anyOn)
                    mw._floatingWindowInterceptorManager.Start();
                else
                    mw._floatingWindowInterceptorManager.Stop();
            }
            SettingsManager.SaveSettingsToFile();
        }

        #endregion

        #region Startup

        public static void OnNibModeChanged(bool isOn)
        {
            var mw = GetMainWindow();
            if (mw != null)
            {
                if (isOn)
                    mw.BoundsWidth = SettingsManager.Settings.Advanced.NibModeBoundsWidth;
                else
                    mw.BoundsWidth = SettingsManager.Settings.Advanced.FingerModeBoundsWidth;
            }
        }

        #endregion

        #region PowerPoint

        public static void OnPPTSupportChanged(bool isOn)
        {
            var mw = GetMainWindow();
            if (mw == null) return;
            var ppt = SettingsManager.Settings.PowerPointSettings;
            if (!isOn && ppt.IsSupportWPS)
            {
                ppt.IsSupportWPS = false;
                if (mw.PPTManager != null) mw.PPTManager.IsSupportWPS = false;
            }
            if (isOn)
            {
                if (mw.PPTManager == null) mw.InitializePPTManagers();
                mw.StartPPTMonitoring();
            }
            else
            {
                mw.StopPPTMonitoring();
            }
        }

        public static void OnPPTEnhancementChanged(bool isOn)
        {
            var mw = GetMainWindow();
            if (mw == null) return;
            var ppt = SettingsManager.Settings.PowerPointSettings;
            if (isOn)
            {
                ppt.IsSupportWPS = false;
                if (mw.PPTManager != null) mw.PPTManager.IsSupportWPS = false;
                mw.StartPowerPointProcessMonitoring();
            }
            else
            {
                mw.StopPowerPointProcessMonitoring();
            }
        }

        public static void OnSkipAnimationsWhenGoNextChanged(bool isOn)
        {
            var mw = GetMainWindow();
            if (mw?.PPTManager != null)
                mw.PPTManager.SkipAnimationsWhenNavigating = isOn;
        }

        public static void OnPPTLinkModeChanged()
        {
            var mw = GetMainWindow();
            if (mw == null) return;
            var ppt = SettingsManager.Settings.PowerPointSettings;
            try
            {
                mw.StopPPTMonitoring();
                if (ppt.PPTLinkMode != PPTLinkMode.Com && ppt.EnablePowerPointEnhancement)
                {
                    ppt.EnablePowerPointEnhancement = false;
                    mw.StopPowerPointProcessMonitoring();
                    SettingsManager.SaveSettingsToFile();
                }
                if (ppt.PPTLinkMode != PPTLinkMode.Com && ppt.IsSupportWPS)
                {
                    ppt.IsSupportWPS = false;
                    SettingsManager.SaveSettingsToFile();
                }

                // 切换到 Agent 模式时，自动注册 VSTO 插件
                if (ppt.PPTLinkMode == PPTLinkMode.Agent)
                {
                    if (!VstoRegistrationHelper.EnsureRegistered())
                    {
                        LogHelper.WriteLogToFile("VSTO 插件注册失败，Agent 模式可能无法正常工作", LogHelper.LogType.Warning);
                    }
                }

                mw.InitializePPTManagers();
                if (ppt.PowerPointSupport) mw.StartPPTMonitoring();
                LogHelper.WriteLogToFile($"已切换 PPT 联动架构为 {ppt.PPTLinkMode}", LogHelper.LogType.Event);
            }
            catch (Exception ex) { LogHelper.WriteLogToFile($"切换 PPT 联动架构失败: {ex}", LogHelper.LogType.Error); }
        }

        public static void OnSupportWPSChanged()
        {
            var mw = GetMainWindow();
            if (mw == null) return;
            var ppt = SettingsManager.Settings.PowerPointSettings;
            if (ppt.IsSupportWPS)
            {
                if (!ppt.PowerPointSupport)
                {
                    ppt.PowerPointSupport = true;
                    if (mw.PPTManager == null) mw.InitializePPTManagers();
                    mw.StartPPTMonitoring();
                }
                if (ppt.EnablePowerPointEnhancement)
                {
                    ppt.EnablePowerPointEnhancement = false;
                    mw.StopPowerPointProcessMonitoring();
                }
            }
            if (mw.PPTManager != null)
            {
                mw.PPTManager.IsSupportWPS = ppt.IsSupportWPS;
                mw.PPTManager.SkipAnimationsWhenNavigating = ppt.SkipAnimationsWhenGoNext;
            }
        }

        public static void OnShowPPTButtonChanged(bool isOn)
        {
            var mw = GetMainWindow();
            if (mw?.PPTUIManager != null)
            {
                mw.PPTUIManager.ShowPPTButton = isOn;
                mw.PPTUIManager.UpdateNavigationPanelsVisibility();
            }
            mw?.UpdatePPTBtnPreview();
        }

        public static void OnShowPPTSidebarByDefaultChanged()
        {
            var mw = GetMainWindow();
            if (mw != null && mw.IsInPPTPresentationMode)
                mw.UpdatePPTQuickPanelVisibility();
        }

        public static void OnPPTButtonPositionChanged()
        {
            var mw = GetMainWindow();
            mw?.UpdatePPTBtnSlidersStatus();
            mw?.UpdatePPTUIManagerSettings();
            mw?.UpdatePPTBtnPreview();
        }

        public static void OnPPTButtonOpacityChanged(string buttonKey, double value)
        {
            var mw = GetMainWindow();
            if (mw?.PPTUIManager != null)
            {
                switch (buttonKey)
                {
                    case "LS": mw.PPTUIManager.PPTLSButtonOpacity = value; break;
                    case "RS": mw.PPTUIManager.PPTRSButtonOpacity = value; break;
                    case "LB": mw.PPTUIManager.PPTLBButtonOpacity = value; break;
                    case "RB": mw.PPTUIManager.PPTRBButtonOpacity = value; break;
                }
                mw.PPTUIManager.UpdateNavigationButtonStyles();
            }
            mw?.UpdatePPTBtnPreview();
        }

        public static void OnPPTButtonsDisplayOptionChanged()
        {
            var mw = GetMainWindow();
            if (mw?.PPTUIManager != null && mw.IsInPPTPresentationMode)
            {
                mw.PPTUIManager.PPTButtonsDisplayOption = SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption;
                mw.PPTUIManager.UpdateNavigationPanelsVisibility();
            }
            mw?.UpdatePPTBtnPreview();
        }

        public static void OnPPTSButtonsOptionChanged()
        {
            var mw = GetMainWindow();
            if (mw?.PPTUIManager != null && mw.IsInPPTPresentationMode)
            {
                mw.PPTUIManager.PPTSButtonsOption = SettingsManager.Settings.PowerPointSettings.PPTSButtonsOption;
                mw.PPTUIManager.UpdateNavigationButtonStyles();
            }
            mw?.UpdatePPTBtnPreview();
        }

        public static void OnPPTSButtonsOptionWithOpacityChanged()
        {
            var mw = GetMainWindow();
            var ppt = SettingsManager.Settings.PowerPointSettings;
            if (mw?.PPTUIManager != null && mw.IsInPPTPresentationMode)
            {
                mw.PPTUIManager.PPTSButtonsOption = ppt.PPTSButtonsOption;
                mw.PPTUIManager.PPTLSButtonOpacity = ppt.PPTLSButtonOpacity;
                mw.PPTUIManager.PPTRSButtonOpacity = ppt.PPTRSButtonOpacity;
                mw.PPTUIManager.UpdateNavigationButtonStyles();
            }
            mw?.UpdatePPTBtnPreview();
        }

        public static void OnPPTBButtonsOptionChanged()
        {
            var mw = GetMainWindow();
            if (mw?.PPTUIManager != null && mw.IsInPPTPresentationMode)
            {
                mw.PPTUIManager.PPTBButtonsOption = SettingsManager.Settings.PowerPointSettings.PPTBButtonsOption;
                mw.PPTUIManager.UpdateNavigationButtonStyles();
            }
            mw?.UpdatePPTBtnPreview();
        }

        public static void OnPPTBButtonsOptionWithOpacityChanged()
        {
            var mw = GetMainWindow();
            var ppt = SettingsManager.Settings.PowerPointSettings;
            if (mw?.PPTUIManager != null && mw.IsInPPTPresentationMode)
            {
                mw.PPTUIManager.PPTBButtonsOption = ppt.PPTBButtonsOption;
                mw.PPTUIManager.PPTLBButtonOpacity = ppt.PPTLBButtonOpacity;
                mw.PPTUIManager.PPTRBButtonOpacity = ppt.PPTRBButtonOpacity;
                mw.PPTUIManager.UpdateNavigationButtonStyles();
            }
            mw?.UpdatePPTBtnPreview();
        }

        public static void OnPPTTimeCapsuleChanged()
        {
            var mw = GetMainWindow();
            if (mw != null && mw.IsInPPTPresentationMode)
            {
                mw.UpdatePPTTimeCapsuleVisibility();
                mw.UpdatePPTQuickPanelVisibility();
            }
        }

        public static void OnPPTTimeCapsulePositionChanged()
        {
            var mw = GetMainWindow();
            if (mw != null && mw.IsInPPTPresentationMode)
                mw.UpdatePPTTimeCapsulePosition();
        }

        public static void OnPPTTimeCapsuleOpacityChanged()
        {
            var mw = GetMainWindow();
            if (mw != null && mw.IsInPPTPresentationMode)
                mw.UpdatePPTTimeCapsuleOpacity();
        }

        public static void OnPPTTimeCapsuleScaleChanged()
        {
            var mw = GetMainWindow();
            if (mw != null && mw.IsInPPTPresentationMode)
                mw.UpdatePPTTimeCapsuleScale();
        }

        public static void OnResetPPTTimeCapsulePosition()
        {
            var mw = GetMainWindow();
            mw?.ResetPPTTimeCapsuleOffset();
        }

        public static void OnPPTNavBarScaleChanged(double scale)
        {
            var mw = GetMainWindow();
            if (mw?.PPTUIManager != null)
            {
                mw.PPTUIManager.PPTNavBarScale = scale;
                mw.PPTUIManager.UpdateNavigationButtonStyles();
            }
            mw?.UpdatePPTBtnPreview();
        }

        #endregion

        #region RandomDraw

        public static void OnShowRandomAndSingleDrawChanged(bool isOn)
        {
            var mw = GetMainWindow();
            if (mw != null)
            {
                if (mw.BoardRandomDrawToolBtn != null)
                    mw.BoardRandomDrawToolBtn.Visibility = isOn ? Visibility.Visible : Visibility.Collapsed;
                if (mw.BoardSingleDrawToolBtn != null)
                    mw.BoardSingleDrawToolBtn.Visibility = isOn ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public static void OnEnableQuickDrawChanged()
        {
            var mw = GetMainWindow();
            if (mw != null) mw.ShowQuickDrawFloatingButton();
        }

        #endregion

        #region InkRecognition

        public static void OnInkToShapeEnabledChanged(bool isOn)
        {
            var mw = GetMainWindow();
            if (mw != null)
            {
                if (mw.FloatingBarToggleSwitchEnableInkToShape != null)
                    mw.FloatingBarToggleSwitchEnableInkToShape.IsOn = isOn;
                if (mw.BoardToggleSwitchEnableInkToShape != null)
                    mw.BoardToggleSwitchEnableInkToShape.IsOn = isOn;
            }
        }

        #endregion

        #region Update

        public static void OnSmartUpdateChanged()
        {
            if (!SettingsManager.Settings.Startup.IsAutoUpdate) return;
            var mw = GetMainWindow();
            if (mw != null)
            {
                mw.ResetUpdateCheckRetry();
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() => mw.AutoUpdate());
            }
        }

        public static void OnUpdateChannelChanged()
        {
            if (!SettingsManager.Settings.Startup.IsAutoUpdate) return;
            var mw = GetMainWindow();
            if (mw != null)
            {
                mw.ResetUpdateCheckRetry();
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() => mw.AutoUpdate());
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | Error during channel switch update check: {ex.Message}", LogHelper.LogType.Error);
                    }
                });
            }
        }

        public static void OnStartSilentUpdateTimer()
        {
            var mw = GetMainWindow();
            if (mw != null) mw.StartSilentUpdateTimer();
        }

        public static void OnReloadSettingsFromFile()
        {
            var mw = GetMainWindow();
            if (mw != null) mw.ReloadSettingsFromFile();
        }

        #endregion

        #region Home

        public static void OnRestartApplication(object sender, RoutedEventArgs e)
        {
            var mw = GetMainWindow();
            mw?.BtnRestart_Click(sender, e);
        }

        public static void OnResetToSuggestion(object sender, RoutedEventArgs e)
        {
            var mw = GetMainWindow();
            mw?.BtnResetToSuggestion_Click(sender, e);
        }

        public static void OnExitApplication(object sender, RoutedEventArgs e)
        {
            var mw = GetMainWindow();
            mw?.ExitApplication(sender, e);
        }

        #endregion
    }
}
