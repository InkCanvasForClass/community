using System;
using System.Windows;

namespace Ink_Canvas.Windows
{
    /// <summary>
    /// 首次启动体验（OOBE）窗口，用于引导用户选择遥测与隐私设置。
    /// </summary>
    public partial class OobeWindow : Window
    {
        private readonly Settings _settings;
        private int _currentStep = 0;
        private const int MaxStepIndex = 11;

        /// <summary>
        /// 初始化 OobeWindow，并使用指定的 Settings 填充初始状态与界面。
        /// </summary>
        /// <param name="settings">用于读取和写入用户首选项的 Settings 实例。</param>
        /// <exception cref="System.ArgumentNullException">当 <paramref name="settings"/> 为 null 时抛出。</exception>
        public OobeWindow(Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _settings = settings;
            InitializeComponent();

            // 初始时设置为透明，等待加载完成后淡入
            Opacity = 0;

            InitializeFromSettings();
            UpdateStepUI();
        }

        /// <summary>
        /// 从当前 Settings 对象将相关首选项映射并回显到各个 OOBE 界面控件中，以反映用户已保存的配置状态。
        /// </summary>
        /// <remarks>
        /// 对各个配置分组（外观、启动、托盘、PPT、画板、手势、墨迹纠正、快捷键、崩溃处理、自动化、随机点名、高级选项、截图等）分别进行读取并更新对应控件的选中/选项状态；在初始化每个分组时会捕获并忽略异常，避免单个分组的错误影响窗口启动流程。
        /// </remarks>
        private void InitializeFromSettings()
        {
            // 根据当前设置回显遥测选项
            switch (_settings.Startup.TelemetryUploadLevel)
            {
                case TelemetryUploadLevel.Basic:
                    RadioTelemetryBasic.IsChecked = true;
                    break;
                case TelemetryUploadLevel.Extended:
                    RadioTelemetryExtended.IsChecked = true;
                    break;
                case TelemetryUploadLevel.None:
                default:
                    RadioTelemetryNone.IsChecked = true;
                    break;
            }

            // 主题与外观设置
            try
            {
                if (_settings.Appearance != null)
                {
                    switch (_settings.Appearance.Theme)
                    {
                        case 0: // 浅色
                            RadioThemeLight.IsChecked = true;
                            break;
                        case 1: // 深色
                            RadioThemeDark.IsChecked = true;
                            break;
                        case 2: // 跟随系统
                        default:
                            RadioThemeFollowSystem.IsChecked = true;
                            break;
                    }

                    CheckBoxEnableSplashScreen.IsChecked = _settings.Appearance.EnableSplashScreen;
                }
            }
            catch
            {
                // 忽略外观初始化异常，避免影响启动
            }

            // 启动行为设置
            try
            {
                if (_settings.Startup != null)
                {
                    CheckBoxFoldAtStartup.IsChecked = _settings.Startup.IsFoldAtStartup;
                    CheckBoxAutoUpdate.IsChecked = _settings.Startup.IsAutoUpdate;
                }
            }
            catch
            {
                // 忽略启动行为初始化异常
            }

            // 托盘与快速面板
            try
            {
                if (_settings.Appearance != null)
                {
                    CheckBoxEnableTrayIcon.IsChecked = _settings.Appearance.EnableTrayIcon;
                    CheckBoxShowQuickPanel.IsChecked = _settings.Appearance.IsShowQuickPanel;
                }
            }
            catch
            {
                // 忽略托盘/快速面板初始化异常
            }

            // PPT 联动
            try
            {
                if (_settings.PowerPointSettings != null)
                {
                    CheckBoxPptSupport.IsChecked = _settings.PowerPointSettings.PowerPointSupport;
                    CheckBoxPptAutoSaveStrokes.IsChecked = _settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint;
                    CheckBoxPptAutoSaveScreenshots.IsChecked = _settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint;
                    CheckBoxPptTimeCapsule.IsChecked = _settings.PowerPointSettings.EnablePPTTimeCapsule;
                }
            }
            catch
            {
                // 忽略 PPT 联动初始化异常
            }

            // 画板和墨迹
            try
            {
                if (_settings.Canvas != null)
                {
                    CheckBoxShowCursor.IsChecked = _settings.Canvas.IsShowCursor;
                    CheckBoxDisablePressure.IsChecked = _settings.Canvas.DisablePressure;
                    CheckBoxHideStrokeWhenSelecting.IsChecked = _settings.Canvas.HideStrokeWhenSelecting;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            // 手势操作
            try
            {
                if (_settings.Gesture != null)
                {
                    CheckBoxTwoFingerZoom.IsChecked = _settings.Gesture.IsEnableTwoFingerZoom;
                    CheckBoxTwoFingerTranslate.IsChecked = _settings.Gesture.IsEnableTwoFingerTranslate;
                    CheckBoxAutoSwitchTwoFingerGesture.IsChecked = _settings.Gesture.AutoSwitchTwoFingerGesture;
                    CheckBoxEnablePalmEraser.IsChecked = _settings.Canvas != null && _settings.Canvas.EnablePalmEraser;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            // 墨迹纠正
            try
            {
                if (_settings.InkToShape != null)
                {
                    CheckBoxInkToShapeEnabled.IsChecked = _settings.InkToShape.IsInkToShapeEnabled;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            // 快捷键（外观）
            try
            {
                if (_settings.Appearance != null)
                {
                    CheckBoxEnableHotkeysInMouseMode.IsChecked = _settings.Appearance.EnableHotkeysInMouseMode;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            // 崩溃处理
            try
            {
                RadioCrashSilentRestart.IsChecked = _settings.Startup.CrashAction == 0;
                RadioCrashNoAction.IsChecked = _settings.Startup.CrashAction != 0;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            // 自动化行为
            try
            {
                if (_settings.Automation != null)
                {
                    CheckBoxAutoFoldInPPTSlideShow.IsChecked = _settings.Automation.IsAutoFoldInPPTSlideShow;
                    CheckBoxEnableAutoSaveStrokes.IsChecked = _settings.Automation.IsEnableAutoSaveStrokes;
                    if (_settings.Automation.FloatingWindowInterceptor != null)
                    {
                        CheckBoxFloatingWindowInterceptorEnabled.IsChecked = _settings.Automation.FloatingWindowInterceptor.IsEnabled;
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            // 随机点名
            try
            {
                if (_settings.RandSettings != null)
                {
                    CheckBoxShowRandomAndSingleDraw.IsChecked = _settings.RandSettings.ShowRandomAndSingleDraw;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            // 高级选项
            try
            {
                if (_settings.Advanced != null)
                {
                    CheckBoxIsLogEnabled.IsChecked = _settings.Advanced.IsLogEnabled;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            // 截图（自动化中的截图相关）
            try
            {
                if (_settings.Automation != null)
                {
                    CheckBoxAutoSaveStrokesAtClear.IsChecked = _settings.Automation.IsAutoSaveStrokesAtClear;
                    CheckBoxSaveScreenshotsInDateFolders.IsChecked = _settings.Automation.IsSaveScreenshotsInDateFolders;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        /// <summary>
        /// 将当前界面上用户的选择写回到 Settings 对象并标记已接受遥测隐私说明。
        /// </summary>
        /// <remarks>
        /// 更新的设置包括：启动时的遥测级别、主题与启动外观、启动行为、托盘/快速面板、PowerPoint 联动、画板与墨迹选项、手势设置、墨迹纠正、鼠标模式快捷键、崩溃处理策略、自动化相关设置、随机点名设置以及高级日志选项；操作中对各子配置块采用防护性写回（局部异常被忽略）。方法结束时会将 HasAcceptedTelemetryPrivacy 置为 true。
        /// </remarks>
        private void ApplySelection()
        {
            // 将当前遥测选项写回到设置
            if (RadioTelemetryBasic.IsChecked == true)
            {
                _settings.Startup.TelemetryUploadLevel = TelemetryUploadLevel.Basic;
            }
            else if (RadioTelemetryExtended.IsChecked == true)
            {
                _settings.Startup.TelemetryUploadLevel = TelemetryUploadLevel.Extended;
            }
            else
            {
                _settings.Startup.TelemetryUploadLevel = TelemetryUploadLevel.None;
            }

            // 写回主题与外观设置
            try
            {
                if (_settings.Appearance != null)
                {
                    if (RadioThemeLight.IsChecked == true)
                    {
                        _settings.Appearance.Theme = 0;
                    }
                    else if (RadioThemeDark.IsChecked == true)
                    {
                        _settings.Appearance.Theme = 1;
                    }
                    else
                    {
                        // 默认视为跟随系统
                        _settings.Appearance.Theme = 2;
                    }

                    _settings.Appearance.EnableSplashScreen = CheckBoxEnableSplashScreen.IsChecked == true;
                }
            }
            catch
            {
                // 忽略外观写回异常
            }

            // 写回启动行为设置
            try
            {
                if (_settings.Startup != null)
                {
                    _settings.Startup.IsFoldAtStartup = CheckBoxFoldAtStartup.IsChecked == true;
                    _settings.Startup.IsAutoUpdate = CheckBoxAutoUpdate.IsChecked == true;
                }
            }
            catch
            {
                // 忽略启动行为写回异常
            }

            // 写回托盘与快速面板设置
            try
            {
                if (_settings.Appearance != null)
                {
                    _settings.Appearance.EnableTrayIcon = CheckBoxEnableTrayIcon.IsChecked == true;
                    _settings.Appearance.IsShowQuickPanel = CheckBoxShowQuickPanel.IsChecked == true;
                }
            }
            catch
            {
                // 忽略托盘/快速面板写回异常
            }

            // 写回 PPT 联动设置
            try
            {
                if (_settings.PowerPointSettings != null)
                {
                    _settings.PowerPointSettings.PowerPointSupport = CheckBoxPptSupport.IsChecked == true;
                    _settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint = CheckBoxPptAutoSaveStrokes.IsChecked == true;
                    _settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint = CheckBoxPptAutoSaveScreenshots.IsChecked == true;
                    _settings.PowerPointSettings.EnablePPTTimeCapsule = CheckBoxPptTimeCapsule.IsChecked == true;
                }
            }
            catch
            {
                // 忽略 PPT 联动写回异常
            }

            // 写回画板和墨迹
            try
            {
                if (_settings.Canvas != null)
                {
                    _settings.Canvas.IsShowCursor = CheckBoxShowCursor.IsChecked == true;
                    _settings.Canvas.DisablePressure = CheckBoxDisablePressure.IsChecked == true;
                    _settings.Canvas.HideStrokeWhenSelecting = CheckBoxHideStrokeWhenSelecting.IsChecked == true;
                    _settings.Canvas.EnablePalmEraser = CheckBoxEnablePalmEraser.IsChecked == true;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            // 写回手势操作
            try
            {
                if (_settings.Gesture != null)
                {
                    _settings.Gesture.IsEnableTwoFingerZoom = CheckBoxTwoFingerZoom.IsChecked == true;
                    _settings.Gesture.IsEnableTwoFingerTranslate = CheckBoxTwoFingerTranslate.IsChecked == true;
                    _settings.Gesture.AutoSwitchTwoFingerGesture = CheckBoxAutoSwitchTwoFingerGesture.IsChecked == true;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            // 写回墨迹纠正
            try
            {
                if (_settings.InkToShape != null)
                {
                    _settings.InkToShape.IsInkToShapeEnabled = CheckBoxInkToShapeEnabled.IsChecked == true;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            // 写回快捷键（外观）
            try
            {
                if (_settings.Appearance != null)
                {
                    _settings.Appearance.EnableHotkeysInMouseMode = CheckBoxEnableHotkeysInMouseMode.IsChecked == true;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            // 写回崩溃处理（0=静默重启，1=无操作）
            try
            {
                _settings.Startup.CrashAction = RadioCrashNoAction.IsChecked == true ? 1 : 0;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            // 写回自动化行为
            try
            {
                if (_settings.Automation != null)
                {
                    _settings.Automation.IsAutoFoldInPPTSlideShow = CheckBoxAutoFoldInPPTSlideShow.IsChecked == true;
                    _settings.Automation.IsEnableAutoSaveStrokes = CheckBoxEnableAutoSaveStrokes.IsChecked == true;
                    _settings.Automation.IsAutoSaveStrokesAtClear = CheckBoxAutoSaveStrokesAtClear.IsChecked == true;
                    _settings.Automation.IsSaveScreenshotsInDateFolders = CheckBoxSaveScreenshotsInDateFolders.IsChecked == true;
                    if (_settings.Automation.FloatingWindowInterceptor != null)
                    {
                        _settings.Automation.FloatingWindowInterceptor.IsEnabled = CheckBoxFloatingWindowInterceptorEnabled.IsChecked == true;
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            // 写回随机点名
            try
            {
                if (_settings.RandSettings != null)
                {
                    _settings.RandSettings.ShowRandomAndSingleDraw = CheckBoxShowRandomAndSingleDraw.IsChecked == true;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            // 写回高级选项
            try
            {
                if (_settings.Advanced != null)
                {
                    _settings.Advanced.IsLogEnabled = CheckBoxIsLogEnabled.IsChecked == true;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            // 标记用户已经阅读并确认过隐私说明
            _settings.Startup.HasAcceptedTelemetryPrivacy = true;
        }

        /// <summary>
        /// 处理“确认/下一步”按钮的点击：在未到最后一步时前进到下一步，若已是最后一步则应用当前选择并关闭窗口。
        /// </summary>
        /// <param name="sender">触发事件的源对象。</param>
        /// <param name="e">路由事件参数。</param>
        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            // 如果还没到最后一步，则进入下一步
            if (_currentStep < MaxStepIndex)
            {
                _currentStep++;
                UpdateStepUI();
                return;
            }

            // 最后一步：应用选择并关闭窗口
            ApplySelection();
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 导航到上一步骤；若已处于第一步（索引为 0）则不做任何操作。
        /// </summary>
        /// <param name="sender">触发此事件的源对象。</param>
        /// <param name="e">事件的路由参数。</param>
        private void BtnPreviousStep_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep <= 0) return;
            _currentStep--;
            UpdateStepUI();
        }

        /// <summary>
        /// 在窗口加载时对窗口不透明度执行淡入动画以显示窗口。
        /// </summary>
        /// <remarks>
        /// 使用约 220 毫秒的缓出三次方缓动实现淡入；如果动画失败，方法会立即将窗口不透明度设为 1 作为回退。 
        /// </remarks>
        private void OobeWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var animation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                };

                BeginAnimation(OpacityProperty, animation);
            }
            catch
            {
                // 动画失败时直接显示
                Opacity = 1;
            }
        }



        /// <summary>
        /// 根据当前步骤索引更新向导界面：显示对应步骤面板，播放切换动画，并刷新步骤指示、标题、子标题和按钮文本/可见性。
        /// </summary>
        /// <remarks>
        /// 若更新过程中出现异常会被捕获并忽略以避免中断主流程。
        /// </remarks>
        private void UpdateStepUI()
        {
            try
            {
                StepTelemetryPanel.Visibility = _currentStep == 0 ? Visibility.Visible : Visibility.Collapsed;
                StepCanvasPanel.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
                StepGesturesPanel.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
                StepInkRecognitionPanel.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
                StepAppearancePanel.Visibility = _currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;
                StepShortcutsPanel.Visibility = _currentStep == 5 ? Visibility.Visible : Visibility.Collapsed;
                StepCrashActionPanel.Visibility = _currentStep == 6 ? Visibility.Visible : Visibility.Collapsed;
                StepPptPanel.Visibility = _currentStep == 7 ? Visibility.Visible : Visibility.Collapsed;
                StepAutomationPanel.Visibility = _currentStep == 8 ? Visibility.Visible : Visibility.Collapsed;
                StepLuckyRandomPanel.Visibility = _currentStep == 9 ? Visibility.Visible : Visibility.Collapsed;
                StepAdvancedPanel.Visibility = _currentStep == 10 ? Visibility.Visible : Visibility.Collapsed;
                StepSnapshotPanel.Visibility = _currentStep == 11 ? Visibility.Visible : Visibility.Collapsed;

                FrameworkElement activePanel = null;
                if (_currentStep == 0) activePanel = StepTelemetryPanel;
                else if (_currentStep == 1) activePanel = StepCanvasPanel;
                else if (_currentStep == 2) activePanel = StepGesturesPanel;
                else if (_currentStep == 3) activePanel = StepInkRecognitionPanel;
                else if (_currentStep == 4) activePanel = StepAppearancePanel;
                else if (_currentStep == 5) activePanel = StepShortcutsPanel;
                else if (_currentStep == 6) activePanel = StepCrashActionPanel;
                else if (_currentStep == 7) activePanel = StepPptPanel;
                else if (_currentStep == 8) activePanel = StepAutomationPanel;
                else if (_currentStep == 9) activePanel = StepLuckyRandomPanel;
                else if (_currentStep == 10) activePanel = StepAdvancedPanel;
                else if (_currentStep == 11) activePanel = StepSnapshotPanel;

                if (activePanel != null)
                {
                    activePanel.Opacity = 0;

                    var transform = activePanel.RenderTransform as System.Windows.Media.TranslateTransform;
                    if (transform == null)
                    {
                        transform = new System.Windows.Media.TranslateTransform(0, 12);
                        activePanel.RenderTransform = transform;
                    }
                    else
                    {
                        transform.Y = 12;
                    }

                    var fade = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new System.Windows.Media.Animation.CubicEase
                        {
                            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                        }
                    };

                    var slide = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = 12,
                        To = 0,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new System.Windows.Media.Animation.CubicEase
                        {
                            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                        }
                    };

                    activePanel.BeginAnimation(OpacityProperty, fade);
                    transform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slide);
                }

                StepIndicatorText.Text = $"步骤 {_currentStep + 1} / 12";

                BtnPreviousStep.Visibility = _currentStep > 0 ? Visibility.Visible : Visibility.Collapsed;

                switch (_currentStep)
                {
                    case 0:
                        StepTitleText.Text = "启动时行为";
                        StepSubtitleText.Text = "遥测、自动更新与启动行为，对应 设置 → 启动时行为、高级 → 遥测。";
                        break;
                    case 1:
                        StepTitleText.Text = "画板和墨迹";
                        StepSubtitleText.Text = "画笔光标、压感、墨迹显示，对应 设置 → 画板和墨迹。";
                        break;
                    case 2:
                        StepTitleText.Text = "手势操作";
                        StepSubtitleText.Text = "双指缩放/平移、手掌擦等，对应 设置 → 手势操作。";
                        break;
                    case 3:
                        StepTitleText.Text = "墨迹纠正";
                        StepSubtitleText.Text = "手绘图形识别为标准形状，对应 设置 → 墨迹纠正。";
                        break;
                    case 4:
                        StepTitleText.Text = "个性化设置";
                        StepSubtitleText.Text = "主题、启动动画、托盘与快速工具栏，对应 设置 → 个性化设置。";
                        break;
                    case 5:
                        StepTitleText.Text = "快捷键设置";
                        StepSubtitleText.Text = "鼠标模式下全局快捷键，对应 设置 → 快捷键设置。";
                        break;
                    case 6:
                        StepTitleText.Text = "崩溃处理";
                        StepSubtitleText.Text = "未处理异常时的行为，对应 设置 → 崩溃处理。";
                        break;
                    case 7:
                        StepTitleText.Text = "PowerPoint 支持";
                        StepSubtitleText.Text = "放映联动与墨迹保存等，对应 设置 → PowerPoint 支持。";
                        break;
                    case 8:
                        StepTitleText.Text = "自动化行为";
                        StepSubtitleText.Text = "自动收纳、墨迹自动保存等，对应 设置 → 自动化行为。";
                        break;
                    case 9:
                        StepTitleText.Text = "随机点名";
                        StepSubtitleText.Text = "点名窗口选项，对应 设置 → 随机点名。";
                        break;
                    case 10:
                        StepTitleText.Text = "高级选项";
                        StepSubtitleText.Text = "日志、特殊屏幕等，对应 设置 → 高级选项。";
                        break;
                    case 11:
                        StepTitleText.Text = "截图和屏幕捕捉";
                        StepSubtitleText.Text = "清屏截图、按日期保存等，对应 设置 → 截图和屏幕捕捉。";
                        break;
                }

                BtnConfirm.Content = _currentStep == MaxStepIndex ? "保存并开始使用" : "下一步";
            }
            catch
            {
                // 忽略 UI 更新异常，避免影响主流程
            }
        }
    }
}
