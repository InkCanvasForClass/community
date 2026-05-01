using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Ink_Canvas.Windows
{
    /// <summary>
    /// 首次启动体验(OOBE)窗口,使用与设置窗口一致的卡片化 UI 引导用户完成初始配置。
    /// 切换步骤时使用横向滑动 + 淡入,模仿 ClassIsland 的设置向导动画风格。
    /// </summary>
    public partial class OobeWindow : Window
    {
        private readonly Settings _settings;
        private int _currentStep = 0;
        private const int MaxStepIndex = 7;
        private const int StepCount = 8;

        private FrameworkElement[] _stepPanels;
        private static readonly TimeSpan SlideDuration = TimeSpan.FromMilliseconds(280);
        private static readonly IEasingFunction SlideEase = new CubicEase { EasingMode = EasingMode.EaseOut };

        public OobeWindow(Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _settings = settings;
            InitializeComponent();

            Opacity = 0;

            _stepPanels = new FrameworkElement[]
            {
                StepTelemetryPanel,
                StepCanvasPanel,
                StepGesturesPanel,
                StepAppearancePanel,
                StepPptPanel,
                StepAutomationPanel,
                StepLuckyRandomPanel,
                StepAdvancedPanel,
            };

            InitializeFromSettings();
            UpdateStepUI(animateDirection: 0, instant: true);
        }

        #region Settings IO

        private void InitializeFromSettings()
        {
            try
            {
                if (_settings.Startup != null)
                {
                    ComboBoxTelemetryUploadLevel.SelectedIndex = (int)_settings.Startup.TelemetryUploadLevel;
                    CardFoldAtStartup.IsOn = _settings.Startup.IsFoldAtStartup;
                    CardAutoUpdate.IsOn = _settings.Startup.IsAutoUpdate;
                    ComboBoxCrashAction.SelectedIndex = _settings.Startup.CrashAction == 0 ? 0 : 1;
                    CheckBoxPrivacyAccepted.IsChecked = _settings.Startup.HasAcceptedTelemetryPrivacy;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try
            {
                if (_settings.Canvas != null)
                {
                    CardShowCursor.IsOn = _settings.Canvas.IsShowCursor;
                    CardDisablePressure.IsOn = _settings.Canvas.DisablePressure;
                    CardHideStrokeWhenSelecting.IsOn = _settings.Canvas.HideStrokeWhenSelecting;
                    CardEnablePalmEraser.IsOn = _settings.Canvas.EnablePalmEraser;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try
            {
                if (_settings.Gesture != null)
                {
                    CardTwoFingerZoom.IsOn = _settings.Gesture.IsEnableTwoFingerZoom;
                    CardTwoFingerTranslate.IsOn = _settings.Gesture.IsEnableTwoFingerTranslate;
                    CardAutoSwitchTwoFingerGesture.IsOn = _settings.Gesture.AutoSwitchTwoFingerGesture;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try
            {
                if (_settings.InkToShape != null)
                {
                    CardInkToShapeEnabled.IsOn = _settings.InkToShape.IsInkToShapeEnabled;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try
            {
                if (_settings.Appearance != null)
                {
                    int themeIndex = _settings.Appearance.Theme;
                    if (themeIndex < 0 || themeIndex > 2) themeIndex = 2;
                    ComboBoxTheme.SelectedIndex = themeIndex;
                    CardEnableSplashScreen.IsOn = _settings.Appearance.EnableSplashScreen;
                    CardEnableTrayIcon.IsOn = _settings.Appearance.EnableTrayIcon;
                    CardShowQuickPanel.IsOn = _settings.Appearance.IsShowQuickPanel;
                    CardEnableHotkeysInMouseMode.IsOn = _settings.Appearance.EnableHotkeysInMouseMode;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try
            {
                if (_settings.PowerPointSettings != null)
                {
                    CardPptSupport.IsOn = _settings.PowerPointSettings.PowerPointSupport;
                    CardPptAutoSaveStrokes.IsOn = _settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint;
                    CardPptAutoSaveScreenshots.IsOn = _settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint;
                    CardPptTimeCapsule.IsOn = _settings.PowerPointSettings.EnablePPTTimeCapsule;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try
            {
                if (_settings.Automation != null)
                {
                    CardAutoFoldInPPTSlideShow.IsOn = _settings.Automation.IsAutoFoldInPPTSlideShow;
                    CardEnableAutoSaveStrokes.IsOn = _settings.Automation.IsEnableAutoSaveStrokes;
                    if (_settings.Automation.FloatingWindowInterceptor != null)
                    {
                        CardFloatingWindowInterceptor.IsOn = _settings.Automation.FloatingWindowInterceptor.IsEnabled;
                    }
                    CardAutoSaveStrokesAtClear.IsOn = _settings.Automation.IsAutoSaveStrokesAtClear;
                    CardSaveScreenshotsInDateFolders.IsOn = _settings.Automation.IsSaveScreenshotsInDateFolders;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try
            {
                if (_settings.RandSettings != null)
                {
                    CardShowRandomAndSingleDraw.IsOn = _settings.RandSettings.ShowRandomAndSingleDraw;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try
            {
                if (_settings.Advanced != null)
                {
                    CardIsLogEnabled.IsOn = _settings.Advanced.IsLogEnabled;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        private void ApplySelection()
        {
            try
            {
                if (_settings.Startup != null)
                {
                    int level = ComboBoxTelemetryUploadLevel.SelectedIndex;
                    if (level < 0) level = 0;
                    _settings.Startup.TelemetryUploadLevel = (TelemetryUploadLevel)level;
                    _settings.Startup.IsFoldAtStartup = CardFoldAtStartup.IsOn;
                    _settings.Startup.IsAutoUpdate = CardAutoUpdate.IsOn;
                    _settings.Startup.CrashAction = ComboBoxCrashAction.SelectedIndex == 1 ? 1 : 0;
                    _settings.Startup.HasAcceptedTelemetryPrivacy = CheckBoxPrivacyAccepted.IsChecked == true;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try
            {
                if (_settings.Canvas != null)
                {
                    _settings.Canvas.IsShowCursor = CardShowCursor.IsOn;
                    _settings.Canvas.DisablePressure = CardDisablePressure.IsOn;
                    _settings.Canvas.HideStrokeWhenSelecting = CardHideStrokeWhenSelecting.IsOn;
                    _settings.Canvas.EnablePalmEraser = CardEnablePalmEraser.IsOn;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try
            {
                if (_settings.Gesture != null)
                {
                    _settings.Gesture.IsEnableTwoFingerZoom = CardTwoFingerZoom.IsOn;
                    _settings.Gesture.IsEnableTwoFingerTranslate = CardTwoFingerTranslate.IsOn;
                    _settings.Gesture.AutoSwitchTwoFingerGesture = CardAutoSwitchTwoFingerGesture.IsOn;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try
            {
                if (_settings.InkToShape != null)
                {
                    _settings.InkToShape.IsInkToShapeEnabled = CardInkToShapeEnabled.IsOn;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try
            {
                if (_settings.Appearance != null)
                {
                    int themeIndex = ComboBoxTheme.SelectedIndex;
                    if (themeIndex < 0) themeIndex = 2;
                    _settings.Appearance.Theme = themeIndex;
                    _settings.Appearance.EnableSplashScreen = CardEnableSplashScreen.IsOn;
                    _settings.Appearance.EnableTrayIcon = CardEnableTrayIcon.IsOn;
                    _settings.Appearance.IsShowQuickPanel = CardShowQuickPanel.IsOn;
                    _settings.Appearance.EnableHotkeysInMouseMode = CardEnableHotkeysInMouseMode.IsOn;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try
            {
                if (_settings.PowerPointSettings != null)
                {
                    _settings.PowerPointSettings.PowerPointSupport = CardPptSupport.IsOn;
                    _settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint = CardPptAutoSaveStrokes.IsOn;
                    _settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint = CardPptAutoSaveScreenshots.IsOn;
                    _settings.PowerPointSettings.EnablePPTTimeCapsule = CardPptTimeCapsule.IsOn;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try
            {
                if (_settings.Automation != null)
                {
                    _settings.Automation.IsAutoFoldInPPTSlideShow = CardAutoFoldInPPTSlideShow.IsOn;
                    _settings.Automation.IsEnableAutoSaveStrokes = CardEnableAutoSaveStrokes.IsOn;
                    _settings.Automation.IsAutoSaveStrokesAtClear = CardAutoSaveStrokesAtClear.IsOn;
                    _settings.Automation.IsSaveScreenshotsInDateFolders = CardSaveScreenshotsInDateFolders.IsOn;
                    if (_settings.Automation.FloatingWindowInterceptor != null)
                    {
                        _settings.Automation.FloatingWindowInterceptor.IsEnabled = CardFloatingWindowInterceptor.IsOn;
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try
            {
                if (_settings.RandSettings != null)
                {
                    _settings.RandSettings.ShowRandomAndSingleDraw = CardShowRandomAndSingleDraw.IsOn;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try
            {
                if (_settings.Advanced != null)
                {
                    _settings.Advanced.IsLogEnabled = CardIsLogEnabled.IsOn;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        #endregion

        #region Navigation

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep == 0 && CheckBoxPrivacyAccepted.IsChecked != true)
            {
                MessageBox.Show(this,
                    "请先勾选\"我已阅读并同意《隐私协议》\"后再继续。",
                    "需要同意隐私协议",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (_currentStep < MaxStepIndex)
            {
                _currentStep++;
                UpdateStepUI(animateDirection: 1);
                return;
            }

            ApplySelection();
            DialogResult = true;
            Close();
        }

        private void BtnPreviousStep_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep <= 0) return;
            _currentStep--;
            UpdateStepUI(animateDirection: -1);
        }

        private void CheckBoxPrivacyAccepted_Changed(object sender, RoutedEventArgs e)
        {
            UpdateConfirmEnabled();
        }

        private bool _privacyDialogShown;

        private void HyperlinkPrivacy_Click(object sender, RoutedEventArgs e)
        {
            if (_privacyDialogShown) return;
            _privacyDialogShown = true;
            try
            {
                var dialog = new PrivacyAgreementWindow { Owner = this };
                bool? result = dialog.ShowDialog();
                if (result == true && dialog.UserAccepted)
                {
                    CheckBoxPrivacyAccepted.IsChecked = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void UpdateConfirmEnabled()
        {
            if (BtnConfirm == null || CheckBoxPrivacyAccepted == null) return;
            BtnConfirm.IsEnabled = _currentStep != 0 || CheckBoxPrivacyAccepted.IsChecked == true;
        }

        #endregion

        private void OobeWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var animation = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(260),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                BeginAnimation(OpacityProperty, animation);
            }
            catch
            {
                Opacity = 1;
            }
        }

        private void UpdateStepUI(int animateDirection, bool instant = false)
        {
            try
            {
                for (int i = 0; i < _stepPanels.Length; i++)
                {
                    _stepPanels[i].Visibility = i == _currentStep ? Visibility.Visible : Visibility.Collapsed;
                }

                StepIndicatorText.Text = $"步骤 {_currentStep + 1} / {StepCount}";
                FooterStepText.Text = $"{_currentStep + 1} / {StepCount}";
                BtnPreviousStep.Visibility = _currentStep > 0 ? Visibility.Visible : Visibility.Collapsed;

                ApplyStepMeta(_currentStep);

                BtnConfirmText.Text = _currentStep == MaxStepIndex ? "保存并开始使用" : "下一步";
                BtnConfirmIcon.Icon = _currentStep == MaxStepIndex
                    ? SegoeFluentIcons.Accept
                    : SegoeFluentIcons.ChevronRight;

                UpdateConfirmEnabled();

                if (StepScrollViewer != null) StepScrollViewer.ScrollToTop();

                AnimateProgress(instant);

                if (!instant && animateDirection != 0)
                {
                    AnimateStepSlide(animateDirection);
                }
                else
                {
                    StepHostTransform.X = 0;
                    StepHost.Opacity = 1;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void ApplyStepMeta(int step)
        {
            string title; string subtitle; FontIconData icon;
            switch (step)
            {
                case 0:
                    title = "启动与隐私";
                    subtitle = "遥测、隐私协议、自动更新与崩溃处理。";
                    icon = SegoeFluentIcons.Shield;
                    break;
                case 1:
                    title = "画板与墨迹";
                    subtitle = "光标、压感、墨迹显示与墨迹纠正。";
                    icon = SegoeFluentIcons.Edit;
                    break;
                case 2:
                    title = "手势操作";
                    subtitle = "双指缩放/平移、手掌擦等。";
                    icon = SegoeFluentIcons.TouchPointer;
                    break;
                case 3:
                    title = "个性化";
                    subtitle = "主题、启动动画、托盘、快速面板与快捷键。";
                    icon = SegoeFluentIcons.Personalize;
                    break;
                case 4:
                    title = "PowerPoint 联动";
                    subtitle = "放映联动、墨迹与截屏自动保存、时间胶囊。";
                    icon = SegoeFluentIcons.Slideshow;
                    break;
                case 5:
                    title = "自动化与截图";
                    subtitle = "自动收纳、墨迹自动保存、悬浮窗拦截与截图保存。";
                    icon = SegoeFluentIcons.Sync;
                    break;
                case 6:
                    title = "随机点名";
                    subtitle = "点名窗口选项。";
                    icon = SegoeFluentIcons.People;
                    break;
                case 7:
                    title = "高级选项";
                    subtitle = "日志等高级配置。";
                    icon = SegoeFluentIcons.Settings;
                    break;
                default:
                    title = string.Empty; subtitle = string.Empty; icon = SegoeFluentIcons.Home;
                    break;
            }

            StepTitleText.Text = title;
            StepSubtitleText.Text = subtitle;
            StepIcon.Icon = icon;
        }

        private void AnimateProgress(bool instant)
        {
            try
            {
                double total = ActualWidth > 0 ? ActualWidth : Width;
                double trackWidth = Math.Max(0, Math.Min(420, total - 56 * 2 - 320));
                double progress = (_currentStep + 1) / (double)StepCount;
                double targetWidth = trackWidth * progress;

                if (instant)
                {
                    ProgressFill.Width = targetWidth;
                    return;
                }

                var anim = new DoubleAnimation
                {
                    To = targetWidth,
                    Duration = TimeSpan.FromMilliseconds(320),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                ProgressFill.BeginAnimation(WidthProperty, anim);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void AnimateStepSlide(int direction)
        {
            // direction: 1 = 前进 (新内容从右滑入), -1 = 后退 (从左滑入)
            double from = direction > 0 ? 36 : -36;

            StepHostTransform.X = from;
            StepHost.Opacity = 0;

            var slide = new DoubleAnimation
            {
                From = from,
                To = 0,
                Duration = SlideDuration,
                EasingFunction = SlideEase
            };
            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = SlideDuration,
                EasingFunction = SlideEase
            };

            StepHostTransform.BeginAnimation(TranslateTransform.XProperty, slide);
            StepHost.BeginAnimation(OpacityProperty, fade);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            AnimateProgress(instant: true);
        }
    }
}