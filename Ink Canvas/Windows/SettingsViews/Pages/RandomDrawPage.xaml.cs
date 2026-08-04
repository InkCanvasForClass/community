using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;
// ManageNameRostersWindow lives in Ink_Canvas namespace

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
            UpdateAllSliderTexts();
            SliderTouchHelper.AddTouchSupportToAllSliders(this);
        }

        private void UpdateAllSliderTexts()
        {
            UpdateSliderText(RandWindowOnceCloseLatencySlider, RandWindowOnceCloseLatencyText, "{0:F1}");
            UpdateSliderText(RandWindowOnceMaxStudentsSlider, RandWindowOnceMaxStudentsText, "{0:0}");
            UpdateSliderText(MLAvoidanceHistorySlider, MLAvoidanceHistoryText, "{0:0}");
            UpdateSliderText(MLAvoidanceWeightSlider, MLAvoidanceWeightText, "{0:F1}");
            UpdateSliderText(TimerVolumeSlider, TimerVolumeText, "{0:F1}");
            UpdateSliderText(ProgressiveReminderVolumeSlider, ProgressiveReminderVolumeText, "{0:F1}");
        }

        private void UpdateSliderText(Slider slider, TextBlock textBlock, string format)
        {
            if (slider == null || textBlock == null) return;
            textBlock.Text = string.Format(format, slider.Value);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings;
            if (settings?.RandSettings == null) return;

            ToggleSwitchDisplayRandWindowNamesInputBtn.IsOn = settings.RandSettings.DisplayRandWindowNamesInputBtn;
            RandWindowOnceCloseLatencySlider.Value = settings.RandSettings.RandWindowOnceCloseLatency;
            RandWindowOnceMaxStudentsSlider.Value = settings.RandSettings.RandWindowOnceMaxStudents;
            ToggleSwitchShowRandomAndSingleDraw.IsOn = settings.RandSettings.ShowRandomAndSingleDraw;
            ToggleSwitchEnableQuickDraw.IsOn = settings.RandSettings.EnableQuickDraw;
            ToggleSwitchQuickDrawExternalCaller.IsOn = settings.RandSettings.QuickDrawExternalCaller;
            ToggleSwitchExternalCaller.IsOn = settings.RandSettings.DirectCallCiRand;
            ComboBoxExternalCallerType.SelectedIndex = settings.RandSettings.ExternalCallerType;

            ToggleSwitchUseNewRollCallUI.IsOn = settings.RandSettings.UseNewRollCallUI;
            ToggleSwitchDisplayRandWindowNamesInputBtn.Visibility = settings.RandSettings.UseNewRollCallUI ? Visibility.Collapsed : Visibility.Visible;
            ToggleSwitchEnableMLAvoidance.IsOn = settings.RandSettings.EnableMLAvoidance;
            MLAvoidanceHistorySlider.Value = settings.RandSettings.MLAvoidanceHistoryCount;
            MLAvoidanceWeightSlider.Value = settings.RandSettings.MLAvoidanceWeight;

            if (settings.RandSettings.UseLegacyTimerUI)
                ComboBoxTimerUIStyle.SelectedIndex = 0;
            else
                ComboBoxTimerUIStyle.SelectedIndex = 1;
            ToggleSwitchEnableOvertimeCountUp.IsOn = settings.RandSettings.EnableOvertimeCountUp;

            bool canEnableRedText = settings.RandSettings.EnableOvertimeCountUp && settings.RandSettings.EnableOvertimeRedText;
            ToggleSwitchEnableOvertimeRedText.IsOn = canEnableRedText;

            TimerVolumeSlider.Value = settings.RandSettings.TimerVolume;
            ToggleSwitchEnableProgressiveReminder.IsOn = settings.RandSettings.EnableProgressiveReminder;
            ProgressiveReminderVolumeSlider.Value = settings.RandSettings.ProgressiveReminderVolume;

            UpdateNameRostersInComboBox();
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
            UpdateSliderText(RandWindowOnceCloseLatencySlider, RandWindowOnceCloseLatencyText, "{0:F1}");
            if (!_isLoaded) return;
            var val = Math.Round(RandWindowOnceCloseLatencySlider.Value, 2);
            RandWindowOnceCloseLatencySlider.Value = val;
            SettingsManager.Settings.RandSettings.RandWindowOnceCloseLatency = val;
            SettingsManager.SaveSettingsToFile();
        }

        private void RandWindowOnceMaxStudentsSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(RandWindowOnceMaxStudentsSlider, RandWindowOnceMaxStudentsText, "{0:0}");
            if (!_isLoaded) return;
            SettingsManager.Settings.RandSettings.RandWindowOnceMaxStudents = (int)RandWindowOnceMaxStudentsSlider.Value;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchShowRandomAndSingleDraw_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            bool isToggled = ToggleSwitchShowRandomAndSingleDraw.IsOn;
            SettingsManager.Settings.RandSettings.ShowRandomAndSingleDraw = isToggled;

            SettingsActionHub.OnShowRandomAndSingleDrawChanged(isToggled);

            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnableQuickDraw_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.RandSettings.EnableQuickDraw = ToggleSwitchEnableQuickDraw.IsOn;
            SettingsManager.SaveSettingsToFile();

            SettingsActionHub.OnEnableQuickDrawChanged();
        }

        private void ToggleSwitchQuickDrawExternalCaller_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.RandSettings.QuickDrawExternalCaller = ToggleSwitchQuickDrawExternalCaller.IsOn;
            SettingsManager.SaveSettingsToFile();
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

        #region Name Roster Schemes

        private void UpdateNameRostersInComboBox()
        {
            if (ComboBoxNameRoster == null) return;

            bool wasLoaded = _isLoaded;
            _isLoaded = false;
            try
            {
                ComboBoxNameRoster.Items.Clear();

                var noneItem = new ComboBoxItem
                {
                    Content = RandomStrings.Random_Roster_None,
                    Tag = "",
                    FontFamily = new FontFamily("Microsoft YaHei UI")
                };
                ComboBoxNameRoster.Items.Add(noneItem);

                string selectedGuid = SettingsManager.Settings?.RandSettings?.SelectedNameRosterGuid ?? "";
                int selectedIndex = 0;
                var rosters = SettingsManager.Settings?.RandSettings?.NameRosters;
                if (rosters != null)
                {
                    for (int i = 0; i < rosters.Count; i++)
                    {
                        var roster = rosters[i];
                        var item = new ComboBoxItem
                        {
                            Content = roster.Name,
                            Tag = roster.Guid ?? "",
                            FontFamily = new FontFamily("Microsoft YaHei UI")
                        };
                        ComboBoxNameRoster.Items.Add(item);
                        if (!string.IsNullOrEmpty(selectedGuid) &&
                            string.Equals(roster.Guid, selectedGuid, StringComparison.OrdinalIgnoreCase))
                        {
                            selectedIndex = i + 1;
                        }
                    }
                }

                ComboBoxNameRoster.SelectedIndex = selectedIndex;
            }
            finally
            {
                _isLoaded = wasLoaded;
            }
        }

        private void ComboBoxNameRoster_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (!(ComboBoxNameRoster.SelectedItem is ComboBoxItem selectedItem)) return;

            string guid = selectedItem.Tag as string ?? "";
            if (string.IsNullOrEmpty(guid))
            {
                // 选“未选择方案”：只清空选中状态，不改 Names.txt
                if (SettingsManager.Settings?.RandSettings != null)
                {
                    SettingsManager.Settings.RandSettings.SelectedNameRosterGuid = "";
                    SettingsManager.SaveSettingsToFile();
                }
                return;
            }

            NameRosterManager.SelectAndApply(guid);
        }

        private async void ButtonAddNameRoster_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this) ?? Application.Current.MainWindow;
            string guid = await ManageNameRostersWindow.AddNewRosterDialogAsync(owner);
            if (string.IsNullOrEmpty(guid)) return;

            // 新建后自动设为当前方案
            NameRosterManager.SelectAndApply(guid);
            UpdateNameRostersInComboBox();
        }

        private async void ButtonManageNameRosters_Click(object sender, RoutedEventArgs e)
        {
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw == null) return;

            // 增设/重命名在控件内部用 ContentDialog 完成（同一时刻只能开一个，
            // 已打开时会就地切换内容）；此处只负责展示管理列表。
            // 覆盖默认 ContentDialogMaxWidth(548)：内容 UserControl 固定 640×360，
            // 列表在控件内部纵向滚动，避免横向裁切操作按钮。
            var content = new ManageNameRostersWindow();
            var dialog = new iNKORE.UI.WPF.Modern.Controls.ContentDialog
            {
                Title = RandomStrings.Random_Roster_ManageWindowTitle,
                Content = content,
                CloseButtonText = NotificationStrings.AnimationOff,
                Owner = Window.GetWindow(this) ?? mw,
                DefaultButton = iNKORE.UI.WPF.Modern.Controls.ContentDialogButton.Close,
                Resources =
                {
                    ["ContentDialogMaxWidth"] = 720d,
                    ["ContentDialogMaxHeight"] = 560d
                }
            };

            await dialog.ShowAsync();
            UpdateNameRostersInComboBox();
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

        private async void ButtonAddCustomBackground_Click(object sender, RoutedEventArgs e)
        {
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw == null) return;

            var content = new AddPickNameBackgroundWindow(mw);
            var dialog = new iNKORE.UI.WPF.Modern.Controls.ContentDialog
            {
                Title = Properties.RandomStrings.Random_AddBg_WindowTitle,
                Content = content,
                PrimaryButtonText = FloatingBarStrings.Tools_Save,
                CloseButtonText = Properties.RandomStrings.Random_Cancel,
                Owner = Window.GetWindow(this) ?? mw,
                DefaultButton = iNKORE.UI.WPF.Modern.Controls.ContentDialogButton.Primary
            };

            content.OnInputChanged += () =>
            {
                dialog.IsPrimaryButtonEnabled = content.CanSave();
            };
            dialog.IsPrimaryButtonEnabled = content.CanSave();

            dialog.PrimaryButtonClick += (s, args) =>
            {
                var deferral = args.GetDeferral();
                if (content.Save())
                {
                    ComboBoxPickNameBackground.SelectedIndex = ComboBoxPickNameBackground.Items.Count - 1;
                    dialog.Hide();
                }
                deferral.Complete();
            };

            await dialog.ShowAsync();
        }

        private async void ButtonManageBackgrounds_Click(object sender, RoutedEventArgs e)
        {
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw == null) return;

            var content = new ManagePickNameBackgroundsWindow(mw);
            var dialog = new iNKORE.UI.WPF.Modern.Controls.ContentDialog
            {
                Title = Properties.RandomStrings.Random_ManageBg_WindowTitle,
                Content = content,
                CloseButtonText = Properties.NotificationStrings.AnimationOff,
                Owner = Window.GetWindow(this) ?? mw,
                DefaultButton = iNKORE.UI.WPF.Modern.Controls.ContentDialogButton.Close
            };
            await dialog.ShowAsync();
        }

        #endregion

        #region New Roll Call UI

        private void ToggleSwitchUseNewRollCallUI_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.RandSettings.UseNewRollCallUI = ToggleSwitchUseNewRollCallUI.IsOn;
            ToggleSwitchDisplayRandWindowNamesInputBtn.Visibility = ToggleSwitchUseNewRollCallUI.IsOn ? Visibility.Collapsed : Visibility.Visible;
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
            UpdateSliderText(MLAvoidanceHistorySlider, MLAvoidanceHistoryText, "{0:0}");
            if (!_isLoaded) return;
            SettingsManager.Settings.RandSettings.MLAvoidanceHistoryCount = (int)MLAvoidanceHistorySlider.Value;
            SettingsManager.SaveSettingsToFile();
        }

        private void MLAvoidanceWeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(MLAvoidanceWeightSlider, MLAvoidanceWeightText, "{0:F1}");
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

        private void ComboBoxTimerUIStyle_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            var selectedItem = ComboBoxTimerUIStyle.SelectedItem as System.Windows.Controls.ComboBoxItem;
            var tag = selectedItem?.Tag?.ToString() ?? "Default";
            SettingsManager.Settings.RandSettings.UseLegacyTimerUI = tag == "Legacy";
            SettingsManager.Settings.RandSettings.UseNewStyleUI = tag == "NewStyle";
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
            UpdateSliderText(TimerVolumeSlider, TimerVolumeText, "{0:F1}");
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
                Title = RandomStrings.Random_SelectTimerAlarm,
                Filter = RandomStrings.Random_AudioFilter,
                DefaultExt = "wav"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                SettingsManager.Settings.RandSettings.CustomTimerSoundPath = openFileDialog.FileName;
                SettingsManager.SaveSettingsToFile();
                MessageBox.Show(RandomStrings.Random_CustomAlarmSuccess, RandomStrings.Random_AlarmSetupSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ButtonResetTimerSound_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Settings.RandSettings.CustomTimerSoundPath = "";
            SettingsManager.SaveSettingsToFile();
            MessageBox.Show(RandomStrings.Random_ResetAlarmSuccess, RandomStrings.Random_ResetSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ToggleSwitchEnableProgressiveReminder_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.RandSettings.EnableProgressiveReminder = ToggleSwitchEnableProgressiveReminder.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ProgressiveReminderVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(ProgressiveReminderVolumeSlider, ProgressiveReminderVolumeText, "{0:F1}");
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
                Title = RandomStrings.Random_SelectProgressiveAlarm,
                Filter = RandomStrings.Random_AudioFilter,
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
