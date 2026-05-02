using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class RandomDrawPage : Page
    {
        private bool _isLoaded = false;

        public RandomDrawPage()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
            Unloaded += Page_Unloaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
        }

        private MainWindow GetMainWindow() => Application.Current.MainWindow as MainWindow;

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings;
            if (settings?.RandSettings == null) return;

            ToggleSwitchDisplayRandWindowNamesInputBtn.IsOn = settings.RandSettings.DisplayRandWindowNamesInputBtn;
            RandWindowOnceCloseLatencySlider.Value = settings.RandSettings.RandWindowOnceCloseLatency;
            RandWindowOnceMaxStudentsSlider.Value = settings.RandSettings.RandWindowOnceMaxStudents;
            ToggleSwitchShowRandomAndSingleDraw.IsOn = settings.RandSettings.ShowRandomAndSingleDraw;
            ToggleSwitchEnableQuickDraw.IsOn = settings.RandSettings.EnableQuickDraw;
            ToggleSwitchExternalCaller.IsOn = settings.RandSettings.DirectCallCiRand;
            ComboBoxExternalCallerType.SelectedIndex = settings.RandSettings.ExternalCallerType;

            ToggleSwitchUseNewRollCallUI.IsOn = settings.RandSettings.UseNewRollCallUI;
            ToggleSwitchEnableMLAvoidance.IsOn = settings.RandSettings.EnableMLAvoidance;
            MLAvoidanceHistorySlider.Value = settings.RandSettings.MLAvoidanceHistoryCount;
            MLAvoidanceWeightSlider.Value = settings.RandSettings.MLAvoidanceWeight;

            ToggleSwitchUseLegacyTimerUI.IsOn = settings.RandSettings.UseLegacyTimerUI;
            ToggleSwitchUseNewStyleUI.IsOn = settings.RandSettings.UseNewStyleUI;
            ToggleSwitchEnableOvertimeCountUp.IsOn = settings.RandSettings.EnableOvertimeCountUp;

            bool canEnableRedText = settings.RandSettings.EnableOvertimeCountUp && settings.RandSettings.EnableOvertimeRedText;
            ToggleSwitchEnableOvertimeRedText.IsOn = canEnableRedText;

            TimerVolumeSlider.Value = settings.RandSettings.TimerVolume;
            ToggleSwitchEnableProgressiveReminder.IsOn = settings.RandSettings.EnableProgressiveReminder;
            ProgressiveReminderVolumeSlider.Value = settings.RandSettings.ProgressiveReminderVolume;

            UpdatePickNameBackgroundsInComboBox();
            if (settings.RandSettings.SelectedBackgroundIndex >= ComboBoxPickNameBackground.Items.Count)
            {
                settings.RandSettings.SelectedBackgroundIndex = 0;
            }
            ComboBoxPickNameBackground.SelectedIndex = settings.RandSettings.SelectedBackgroundIndex;
        }

        #region Basic Settings

        private void ToggleSwitchDisplayRandWindowNamesInputBtn_OnToggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.RandSettings.DisplayRandWindowNamesInputBtn = ToggleSwitchDisplayRandWindowNamesInputBtn.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void RandWindowOnceCloseLatencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            var val = Math.Round(RandWindowOnceCloseLatencySlider.Value, 2);
            RandWindowOnceCloseLatencySlider.Value = val;
            SettingsManager.Settings.RandSettings.RandWindowOnceCloseLatency = val;
            SettingsManager.SaveSettingsToFile();
        }

        private void RandWindowOnceMaxStudentsSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.RandSettings.RandWindowOnceMaxStudents = (int)RandWindowOnceMaxStudentsSlider.Value;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchShowRandomAndSingleDraw_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            bool isToggled = ToggleSwitchShowRandomAndSingleDraw.IsOn;
            SettingsManager.Settings.RandSettings.ShowRandomAndSingleDraw = isToggled;

            var mw = GetMainWindow();
            if (mw != null)
            {
                mw.BoardRandomDrawToolBtn.Visibility = isToggled ? Visibility.Visible : Visibility.Collapsed;
                mw.BoardSingleDrawToolBtn.Visibility = isToggled ? Visibility.Visible : Visibility.Collapsed;
            }

            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnableQuickDraw_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.RandSettings.EnableQuickDraw = ToggleSwitchEnableQuickDraw.IsOn;
            SettingsManager.SaveSettingsToFile();

            var mw = GetMainWindow();
            if (mw != null) mw.ShowQuickDrawFloatingButton();
        }

        private void ToggleSwitchExternalCaller_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.RandSettings.DirectCallCiRand = ToggleSwitchExternalCaller.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ComboBoxExternalCallerType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.RandSettings.ExternalCallerType = ComboBoxExternalCallerType.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
        }

        #endregion

        #region Background

        private void UpdatePickNameBackgroundsInComboBox()
        {
            if (ComboBoxPickNameBackground == null) return;

            while (ComboBoxPickNameBackground.Items.Count > 1)
            {
                ComboBoxPickNameBackground.Items.RemoveAt(ComboBoxPickNameBackground.Items.Count - 1);
            }

            foreach (var background in SettingsManager.Settings.RandSettings.CustomPickNameBackgrounds)
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = background.Name;
                item.FontFamily = new FontFamily("Microsoft YaHei UI");
                ComboBoxPickNameBackground.Items.Add(item);
            }
        }

        private void ComboBoxPickNameBackground_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.RandSettings.SelectedBackgroundIndex = ComboBoxPickNameBackground.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
        }

        private void ButtonAddCustomBackground_Click(object sender, RoutedEventArgs e)
        {
            var mw = GetMainWindow();
            if (mw == null) return;

            AddPickNameBackgroundWindow dialog = new AddPickNameBackgroundWindow(mw);
            dialog.Owner = mw;
            dialog.ShowDialog();

            if (dialog.IsSuccess)
            {
                ComboBoxPickNameBackground.SelectedIndex = ComboBoxPickNameBackground.Items.Count - 1;
            }
        }

        private void ButtonManageBackgrounds_Click(object sender, RoutedEventArgs e)
        {
            var mw = GetMainWindow();
            if (mw == null) return;

            ManagePickNameBackgroundsWindow dialog = new ManagePickNameBackgroundsWindow(mw);
            dialog.Owner = mw;
            dialog.ShowDialog();
        }

        #endregion

        #region New Roll Call UI

        private void ToggleSwitchUseNewRollCallUI_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.RandSettings.UseNewRollCallUI = ToggleSwitchUseNewRollCallUI.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnableMLAvoidance_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.RandSettings.EnableMLAvoidance = ToggleSwitchEnableMLAvoidance.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void MLAvoidanceHistorySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.RandSettings.MLAvoidanceHistoryCount = (int)MLAvoidanceHistorySlider.Value;
            SettingsManager.SaveSettingsToFile();
        }

        private void MLAvoidanceWeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            var slider = MLAvoidanceWeightSlider;
            var val = Math.Round(slider.Value, 2);
            // 仅当四舍五入纠正了显示值时才回写；那次 set 会重入 ValueChanged 完成保存。
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.RandSettings.MLAvoidanceWeight = val;
            SettingsManager.SaveSettingsToFile();
        }

        #endregion

        #region Timer

        private void ToggleSwitchUseLegacyTimerUI_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.RandSettings.UseLegacyTimerUI = ToggleSwitchUseLegacyTimerUI.IsOn;
            if (ToggleSwitchUseLegacyTimerUI.IsOn)
            {
                ToggleSwitchUseNewStyleUI.IsOn = false;
                SettingsManager.Settings.RandSettings.UseNewStyleUI = false;
            }
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchUseNewStyleUI_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.RandSettings.UseNewStyleUI = ToggleSwitchUseNewStyleUI.IsOn;
            if (ToggleSwitchUseNewStyleUI.IsOn)
            {
                ToggleSwitchUseLegacyTimerUI.IsOn = false;
                SettingsManager.Settings.RandSettings.UseLegacyTimerUI = false;
            }
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnableOvertimeCountUp_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.RandSettings.EnableOvertimeCountUp = ToggleSwitchEnableOvertimeCountUp.IsOn;

            if (!ToggleSwitchEnableOvertimeCountUp.IsOn)
            {
                ToggleSwitchEnableOvertimeRedText.IsOn = false;
                SettingsManager.Settings.RandSettings.EnableOvertimeRedText = false;
            }

            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnableOvertimeRedText_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            if (ToggleSwitchEnableOvertimeRedText.IsOn && !ToggleSwitchEnableOvertimeCountUp.IsOn)
            {
                ToggleSwitchEnableOvertimeCountUp.IsOn = true;
                SettingsManager.Settings.RandSettings.EnableOvertimeCountUp = true;
            }

            SettingsManager.Settings.RandSettings.EnableOvertimeRedText = ToggleSwitchEnableOvertimeRedText.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void TimerVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            var slider = TimerVolumeSlider;
            var val = Math.Round(slider.Value, 2);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.RandSettings.TimerVolume = val;
            SettingsManager.SaveSettingsToFile();
        }

        private void ButtonSelectCustomTimerSound_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择计时器提醒铃声",
                Filter = "音频文件 (*.wav)|*.wav|所有文件 (*.*)|*.*",
                DefaultExt = "wav"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                SettingsManager.Settings.RandSettings.CustomTimerSoundPath = openFileDialog.FileName;
                SettingsManager.SaveSettingsToFile();
                MessageBox.Show("自定义铃声设置成功！", "设置成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ButtonResetTimerSound_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Settings.RandSettings.CustomTimerSoundPath = "";
            SettingsManager.SaveSettingsToFile();
            MessageBox.Show("已重置为默认铃声！", "重置成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ToggleSwitchEnableProgressiveReminder_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.RandSettings.EnableProgressiveReminder = ToggleSwitchEnableProgressiveReminder.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ProgressiveReminderVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            var slider = ProgressiveReminderVolumeSlider;
            var val = Math.Round(slider.Value, 2);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.RandSettings.ProgressiveReminderVolume = val;
            SettingsManager.SaveSettingsToFile();
        }

        private void ButtonSelectCustomProgressiveReminderSound_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择渐进提醒音频文件",
                Filter = "音频文件 (*.wav)|*.wav|所有文件 (*.*)|*.*",
                DefaultExt = "wav"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                SettingsManager.Settings.RandSettings.ProgressiveReminderSoundPath = openFileDialog.FileName;
                SettingsManager.SaveSettingsToFile();
            }
        }

        private void ButtonResetProgressiveReminderSound_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Settings.RandSettings.ProgressiveReminderSoundPath = "";
            SettingsManager.SaveSettingsToFile();
        }

        #endregion
    }
}
