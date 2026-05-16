using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FontIcon = iNKORE.UI.WPF.Modern.Controls.FontIcon;
using NavigationView = iNKORE.UI.WPF.Modern.Controls.NavigationView;
using NavigationViewItem = iNKORE.UI.WPF.Modern.Controls.NavigationViewItem;
using NavigationViewSelectionChangedEventArgs = iNKORE.UI.WPF.Modern.Controls.NavigationViewSelectionChangedEventArgs;

namespace Ink_Canvas.Windows
{
    /// <summary>
    /// 首次启动体验(OOBE)窗口。使用 iNKORE.UI.WPF.Modern 的 NavigationView 作为左侧导航,
    /// 引导用户依次完成欢迎页、8 个配置步骤与完成摘要页。
    /// </summary>
    public partial class OobeWindow : Window
    {
        private readonly Settings _settings;

        // 视图状态: -1 = 欢迎; 0..7 = 步骤; 8 = 完成
        private const int WelcomeIndex = -1;
        private const int FinishIndex = 8;
        private const int StepCount = 8;
        private const int MaxStepIndex = StepCount - 1;

        private int _currentStep = WelcomeIndex;
        private bool _suppressNavSelection;

        private FrameworkElement[] _stepPanels;
        private NavigationViewItem[] _navItems;

        private static readonly TimeSpan SlideDuration = TimeSpan.FromMilliseconds(280);
        private static readonly IEasingFunction SlideEase = new CubicEase { EasingMode = EasingMode.EaseOut };

        public OobeWindow(Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _settings = settings;
            InitializeComponent();
            WindowBackdropHelper.Apply(this, _settings);

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

            _navItems = new[]
            {
                NavItemTelemetry,
                NavItemCanvas,
                NavItemGestures,
                NavItemAppearance,
                NavItemPpt,
                NavItemAutomation,
                NavItemLuckyRandom,
                NavItemAdvanced,
            };

            InitializeFromSettings();
            UpdateView(animateDirection: 0, instant: true);
            SyncNavSelection();
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
                    int crashAction = _settings.Startup.CrashAction;
                    if (crashAction < 0 || crashAction > 2) crashAction = 0;
                    ComboBoxCrashAction.SelectedIndex = crashAction;
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
                    SelectComboBoxItemByTag(ComboBoxWindowBackdrop, _settings.Appearance.WindowBackdrop);
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
                    int crashAction = ComboBoxCrashAction.SelectedIndex;
                    if (crashAction < 0 || crashAction > 2) crashAction = 0;
                    _settings.Startup.CrashAction = crashAction;
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
                    _settings.Appearance.WindowBackdrop = GetSelectedComboBoxTag(ComboBoxWindowBackdrop, "None");
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

        private static void SelectComboBoxItemByTag(ComboBox comboBox, string tag)
        {
            if (comboBox == null) return;

            var selectedItem = comboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                ?? comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();

            comboBox.SelectedItem = selectedItem;
        }

        private static string GetSelectedComboBoxTag(ComboBox comboBox, string fallback)
        {
            return (comboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;
        }

        private void ComboBoxWindowBackdrop_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxWindowBackdrop == null) return;
            WindowBackdropHelper.Apply(this, GetSelectedComboBoxTag(ComboBoxWindowBackdrop, "None"));
        }

        #endregion

        #region Navigation

        private void BtnStartWelcome_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(0, direction: 1);
        }

        private void BtnUsePreset_Click(object sender, RoutedEventArgs e)
        {
            var presetWindow = new OobePresetWindow { Owner = this };
            bool? result = presetWindow.ShowDialog();
            if (result != true) return;

            switch (presetWindow.SelectedPreset)
            {
                case OobePresetWindow.PresetKind.Standard:
                    OobePresetWindow.ApplyStandard(_settings);
                    break;
                case OobePresetWindow.PresetKind.Lite:
                    OobePresetWindow.ApplyLite(_settings);
                    break;
                default:
                    return;
            }

            DialogResult = true;
            Close();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep == FinishIndex)
            {
                ApplySelection();
                DialogResult = true;
                Close();
                return;
            }

            NavigateTo(_currentStep + 1, direction: 1);
        }

        private void BtnPreviousStep_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep <= WelcomeIndex) return;
            NavigateTo(_currentStep - 1, direction: -1);
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (_suppressNavSelection) return;

            int target = ResolveTargetFromNavItem(args.SelectedItem as NavigationViewItem);
            if (target == _currentStep) return;

            int direction = target > _currentStep ? 1 : -1;
            NavigateTo(target, direction);
        }

        private int ResolveTargetFromNavItem(NavigationViewItem item)
        {
            if (item == null) return _currentStep;
            if (item == NavItemWelcome) return WelcomeIndex;
            if (item == NavItemFinish) return FinishIndex;
            for (int i = 0; i < _navItems.Length; i++)
            {
                if (_navItems[i] == item) return i;
            }
            return _currentStep;
        }

        private void NavigateTo(int target, int direction)
        {
            if (target < WelcomeIndex) target = WelcomeIndex;
            if (target > FinishIndex) target = FinishIndex;
            _currentStep = target;
            UpdateView(direction);
            SyncNavSelection();
        }

        private void SyncNavSelection()
        {
            _suppressNavSelection = true;
            try
            {
                if (_currentStep == WelcomeIndex)
                    NavView.SelectedItem = NavItemWelcome;
                else if (_currentStep == FinishIndex)
                    NavView.SelectedItem = NavItemFinish;
                else
                    NavView.SelectedItem = _navItems[_currentStep];
            }
            finally
            {
                _suppressNavSelection = false;
            }
        }

        private void CheckBoxPrivacyAccepted_Changed(object sender, RoutedEventArgs e)
        {
            UpdateConfirmEnabled();
        }

        private bool _privacyDialogShown;

        private void HyperlinkPrivacy_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
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
            finally
            {
                Dispatcher.BeginInvoke(new Action(() => _privacyDialogShown = false),
                    System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void UpdateConfirmEnabled()
        {
            if (BtnConfirm == null) return;
            BtnConfirm.IsEnabled = true;
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

        private void UpdateView(int animateDirection, bool instant = false)
        {
            try
            {
                bool isWelcome = _currentStep == WelcomeIndex;
                bool isFinish = _currentStep == FinishIndex;
                bool isStep = !isWelcome && !isFinish;

                WelcomePanel.Visibility = isWelcome ? Visibility.Visible : Visibility.Collapsed;
                StepScrollViewer.Visibility = isStep ? Visibility.Visible : Visibility.Collapsed;
                FinishPanel.Visibility = isFinish ? Visibility.Visible : Visibility.Collapsed;

                if (isStep)
                {
                    for (int i = 0; i < _stepPanels.Length; i++)
                    {
                        _stepPanels[i].Visibility = i == _currentStep ? Visibility.Visible : Visibility.Collapsed;
                    }

                    StepIndicatorText.Text = $"步骤 {_currentStep + 1} / {StepCount}";
                    ApplyStepMeta(_currentStep);

                    if (StepScrollViewer != null) StepScrollViewer.ScrollToTop();
                }

                if (isFinish)
                {
                    BuildFinishSummary();
                }

                // 底部进度: 欢迎=0, 各步骤按比例, 完成=100
                double progress;
                if (isWelcome) progress = 0;
                else if (isFinish) progress = 100;
                else progress = (_currentStep + 1) / (double)(StepCount + 1) * 100.0;

                AnimateProgress(progress, instant);

                // Footer 步骤计数
                if (isWelcome) FooterStepText.Text = string.Empty;
                else if (isFinish) FooterStepText.Text = $"{StepCount} / {StepCount} · 完成";
                else FooterStepText.Text = $"{_currentStep + 1} / {StepCount}";

                // 上一步按钮: 欢迎页隐藏
                BtnPreviousStep.Visibility = isWelcome ? Visibility.Collapsed : Visibility.Visible;

                // 主按钮: 欢迎页隐藏(由欢迎页自身的"开始"按钮负责)
                BtnConfirm.Visibility = isWelcome ? Visibility.Collapsed : Visibility.Visible;
                if (isFinish)
                {
                    BtnConfirmText.Text = "保存并开始使用";
                    BtnConfirmIcon.Icon = SegoeFluentIcons.Accept;
                }
                else
                {
                    BtnConfirmText.Text = "下一步";
                    BtnConfirmIcon.Icon = SegoeFluentIcons.ChevronRight;
                }

                UpdateConfirmEnabled();

                if (!instant && animateDirection != 0)
                {
                    AnimateContentSlide(animateDirection);
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
            string title; string subtitle;
            switch (step)
            {
                case 0:
                    title = "启动与隐私";
                    subtitle = "遥测、隐私协议、自动更新与崩溃处理。";
                    break;
                case 1:
                    title = "画板与墨迹";
                    subtitle = "光标、压感、墨迹显示与墨迹纠正。";
                    break;
                case 2:
                    title = "手势操作";
                    subtitle = "双指缩放/平移、手掌擦等。";
                    break;
                case 3:
                    title = "个性化";
                    subtitle = "主题、启动动画、托盘、快速面板与快捷键。";
                    break;
                case 4:
                    title = "PowerPoint 联动";
                    subtitle = "放映联动、墨迹与截屏自动保存、时间胶囊。";
                    break;
                case 5:
                    title = "自动化与截图";
                    subtitle = "自动收纳、墨迹自动保存、悬浮窗拦截与截图保存。";
                    break;
                case 6:
                    title = "随机点名";
                    subtitle = "点名窗口选项。";
                    break;
                case 7:
                    title = "高级选项";
                    subtitle = "日志等高级配置。";
                    break;
                default:
                    title = string.Empty; subtitle = string.Empty;
                    break;
            }

            StepTitleText.Text = title;
            StepSubtitleText.Text = subtitle;
        }

        private void BuildFinishSummary()
        {
            FinishSummaryHost.Children.Clear();

            string telemetryText;
            switch (ComboBoxTelemetryUploadLevel.SelectedIndex)
            {
                case 0: telemetryText = "不上传任何匿名使用数据"; break;
                case 1: telemetryText = "基础(崩溃信息、版本与系统信息)"; break;
                default: telemetryText = "可选(功能使用频率等)"; break;
            }

            string themeText;
            string backdropText = GetSelectedComboBoxContent(ComboBoxWindowBackdrop, Strings.GetString("Theme_WindowBackdrop_None") ?? "None");
            switch (ComboBoxTheme.SelectedIndex)
            {
                case 0: themeText = "浅色"; break;
                case 1: themeText = "深色"; break;
                default: themeText = "跟随系统"; break;
            }

            AddSummaryRow(SegoeFluentIcons.Shield, "遥测级别", telemetryText);
            AddSummaryRow(SegoeFluentIcons.Sync, "自动检查更新", BoolText(CardAutoUpdate.IsOn));
            AddSummaryRow(SegoeFluentIcons.Personalize, "应用主题", themeText);
            AddSummaryRow(SegoeFluentIcons.FullScreen, Strings.GetString("Theme_WindowBackdrop") ?? "Theme_WindowBackdrop", backdropText);
            AddSummaryRow(SegoeFluentIcons.Slideshow, "PowerPoint / WPS 联动", BoolText(CardPptSupport.IsOn));
            AddSummaryRow(SegoeFluentIcons.TouchPointer, "双指缩放 / 平移",
                $"{BoolText(CardTwoFingerZoom.IsOn)} / {BoolText(CardTwoFingerTranslate.IsOn)}");
            AddSummaryRow(SegoeFluentIcons.Pin, "系统托盘图标", BoolText(CardEnableTrayIcon.IsOn));
            AddSummaryRow(SegoeFluentIcons.Document, "启用日志", BoolText(CardIsLogEnabled.IsOn));
        }

        private static string BoolText(bool value) => value ? "已启用" : "已关闭";

        private static string GetSelectedComboBoxContent(ComboBox comboBox, string fallback)
        {
            return (comboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? fallback;
        }

        private void AddSummaryRow(FontIconData icon, string label, string value)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var fontIcon = new FontIcon { Icon = icon, FontSize = 16, Opacity = 0.85 };
            Grid.SetColumn(fontIcon, 0);
            grid.Children.Add(fontIcon);

            var labelBlock = new TextBlock
            {
                Text = label,
                Opacity = 0.85,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(labelBlock, 1);
            grid.Children.Add(labelBlock);

            var valueBlock = new TextBlock
            {
                Text = value,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(valueBlock, 2);
            grid.Children.Add(valueBlock);

            FinishSummaryHost.Children.Add(grid);
        }

        private void AnimateProgress(double targetPercent, bool instant)
        {
            try
            {
                if (StepProgressBar == null) return;

                if (instant)
                {
                    StepProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, null);
                    StepProgressBar.Value = targetPercent;
                    return;
                }

                var anim = new DoubleAnimation
                {
                    To = targetPercent,
                    Duration = TimeSpan.FromMilliseconds(320),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                StepProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, anim);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void AnimateContentSlide(int direction)
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
    }
}
