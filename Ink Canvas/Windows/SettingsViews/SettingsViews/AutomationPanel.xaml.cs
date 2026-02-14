using System;
using System.Windows;
using System.Windows.Controls;
using Ink_Canvas.Windows.SettingsViews;
using System.Windows.Media;

namespace Ink_Canvas.Windows.SettingsViews
{
    /// <summary>
    /// AutomationPanel.xaml 的交互逻辑
    /// </summary>
    public partial class AutomationPanel : UserControl
    {
        private bool _isLoaded = false;

        public AutomationPanel()
        {
            InitializeComponent();
            Loaded += AutomationPanel_Loaded;
        }

        private void AutomationPanel_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            // 添加触摸支持
            EnableTouchSupport();
            // 应用主题
            ApplyTheme();
            _isLoaded = true;
        }

        /// <summary>
        /// 加载设置
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                if (MainWindow.Settings == null || MainWindow.Settings.Automation == null) return;

                _isLoaded = false;

                var automation = MainWindow.Settings.Automation;

                // 自动收纳相关
                SetToggleSwitchState("ToggleSwitchAutoFoldInEasiNote", automation.IsAutoFoldInEasiNote);
                SetToggleSwitchState("ToggleSwitchAutoFoldInEasiCamera", automation.IsAutoFoldInEasiCamera);
                SetToggleSwitchState("ToggleSwitchAutoFoldInHiteTouchPro", automation.IsAutoFoldInHiteTouchPro);
                SetToggleSwitchState("ToggleSwitchAutoFoldInEasiNote3", automation.IsAutoFoldInEasiNote3);
                SetToggleSwitchState("ToggleSwitchAutoFoldInEasiNote3C", automation.IsAutoFoldInEasiNote3C);
                SetToggleSwitchState("ToggleSwitchAutoFoldInEasiNote5C", automation.IsAutoFoldInEasiNote5C);
                SetToggleSwitchState("ToggleSwitchAutoFoldInSeewoPincoTeacher", automation.IsAutoFoldInSeewoPincoTeacher);
                SetToggleSwitchState("ToggleSwitchAutoFoldInHiteCamera", automation.IsAutoFoldInHiteCamera);
                SetToggleSwitchState("ToggleSwitchAutoFoldInHiteLightBoard", automation.IsAutoFoldInHiteLightBoard);
                SetToggleSwitchState("ToggleSwitchAutoFoldInWxBoardMain", automation.IsAutoFoldInWxBoardMain);
                SetToggleSwitchState("ToggleSwitchAutoFoldInMSWhiteboard", automation.IsAutoFoldInMSWhiteboard);
                SetToggleSwitchState("ToggleSwitchAutoFoldInAdmoxWhiteboard", automation.IsAutoFoldInAdmoxWhiteboard);
                SetToggleSwitchState("ToggleSwitchAutoFoldInAdmoxBooth", automation.IsAutoFoldInAdmoxBooth);
                SetToggleSwitchState("ToggleSwitchAutoFoldInQPoint", automation.IsAutoFoldInQPoint);
                SetToggleSwitchState("ToggleSwitchAutoFoldInYiYunVisualPresenter", automation.IsAutoFoldInYiYunVisualPresenter);
                SetToggleSwitchState("ToggleSwitchAutoFoldInMaxHubWhiteboard", automation.IsAutoFoldInMaxHubWhiteboard);
                SetToggleSwitchState("ToggleSwitchAutoFoldInPPTSlideShow", automation.IsAutoFoldInPPTSlideShow);
                SetToggleSwitchState("ToggleSwitchAutoFoldInEasiNoteIgnoreDesktopAnno", automation.IsAutoFoldInEasiNoteIgnoreDesktopAnno);
                SetToggleSwitchState("ToggleSwitchAutoFoldInOldZyBoard", automation.IsAutoFoldInOldZyBoard);
                SetToggleSwitchState("ToggleSwitchKeepFoldAfterSoftwareExit", automation.KeepFoldAfterSoftwareExit);

                // 自动查杀相关
                SetToggleSwitchState("ToggleSwitchAutoKillPptService", automation.IsAutoKillPptService);
                SetToggleSwitchState("ToggleSwitchAutoKillEasiNote", automation.IsAutoKillEasiNote);
                SetToggleSwitchState("ToggleSwitchAutoKillHiteAnnotation", automation.IsAutoKillHiteAnnotation);
                SetToggleSwitchState("ToggleSwitchAutoEnterAnnotationAfterKillHite", automation.IsAutoEnterAnnotationAfterKillHite);
                SetToggleSwitchState("ToggleSwitchAutoKillVComYouJiao", automation.IsAutoKillVComYouJiao);
                SetToggleSwitchState("ToggleSwitchAutoKillSeewoLauncher2DesktopAnnotation", automation.IsAutoKillSeewoLauncher2DesktopAnnotation);
                SetToggleSwitchState("ToggleSwitchAutoKillInkCanvas", automation.IsAutoKillInkCanvas);
                SetToggleSwitchState("ToggleSwitchAutoKillICA", automation.IsAutoKillICA);
                SetToggleSwitchState("ToggleSwitchAutoKillIDT", automation.IsAutoKillIDT);

                _isLoaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationPanel 加载设置时出错: {ex.Message}");
                _isLoaded = true;
            }
        }

        /// <summary>
        /// 设置 ToggleSwitch 状态
        /// </summary>
        private void SetToggleSwitchState(string name, bool isOn)
        {
            try
            {
                var border = FindName(name) as Border;
                if (border == null) return;

                border.Background = isOn
                    ? new SolidColorBrush(Color.FromRgb(0x35, 0x84, 0xE4))
                    : (ThemeHelper.IsDarkTheme ? ThemeHelper.GetButtonBackgroundBrush() : new SolidColorBrush(Color.FromRgb(0xE1, 0xE1, 0xE1)));

                var innerBorder = border.Child as Border;
                if (innerBorder != null)
                {
                    innerBorder.HorizontalAlignment = isOn ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                    // 保持滑块为白色（主题切换时也更稳定）
                    innerBorder.Background = new SolidColorBrush(Colors.White);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationPanel 设置 ToggleSwitch {name} 状态时出错: {ex.Message}");
            }
        }

        private static bool IsToggleSwitchOn(Border border)
        {
            try
            {
                if (border?.Background is SolidColorBrush scb)
                {
                    return scb.Color.A == 0xFF &&
                           scb.Color.R == 0x35 &&
                           scb.Color.G == 0x84 &&
                           scb.Color.B == 0xE4;
                }

                var s = border?.Background?.ToString();
                return string.Equals(s, "#FF3584E4", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// ToggleSwitch 点击事件处理
        /// </summary>
        private void ToggleSwitch_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            var border = sender as Border;
            if (border == null) return;

            bool newState = !IsToggleSwitchOn(border);
            SetToggleSwitchState(border.Name, newState);

            // 通过 MainWindowSettingsHelper 调用 MainWindow 中的方法
            string toggleSwitchName = border.Name;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled(toggleSwitchName, newState);
        }

        /// <summary>
        /// 为面板中的所有交互控件启用触摸支持
        /// </summary>
        private void EnableTouchSupport()
        {
            try
            {
                // 延迟执行，确保所有控件都已加载
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MainWindowSettingsHelper.EnableTouchSupportForControls(this);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationPanel 启用触摸支持时出错: {ex.Message}");
            }
        }

        public event EventHandler<RoutedEventArgs> IsTopBarNeedShadowEffect;
        public event EventHandler<RoutedEventArgs> IsTopBarNeedNoShadowEffect;

        private void ScrollViewerEx_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = (ScrollViewer)sender;
            if (scrollViewer.VerticalOffset >= 10)
            {
                IsTopBarNeedShadowEffect?.Invoke(this, new RoutedEventArgs());
            }
            else
            {
                IsTopBarNeedNoShadowEffect?.Invoke(this, new RoutedEventArgs());
            }
        }
        
        /// <summary>
        /// 应用主题
        /// </summary>
        public void ApplyTheme()
        {
            try
            {
                ThemeHelper.ApplyThemeToControl(this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationPanel 应用主题时出错: {ex.Message}");
            }
        }
    }
}

