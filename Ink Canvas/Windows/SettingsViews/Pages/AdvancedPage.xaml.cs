using Ink_Canvas.Properties;
using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ContentDialog = iNKORE.UI.WPF.Modern.Controls.ContentDialog;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class AdvancedPage : Page
    {
        private bool _isLoaded = false;
        private bool _isRefreshingConfigProfileList = false;
        private string _lastAppliedProfileName;

        public AdvancedPage()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
            Unloaded += Page_Unloaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
            RefreshConfigProfileList();
            UpdateAllSliderTexts();
        }

        private void UpdateAllSliderTexts()
        {
            UpdateSliderText(TouchMultiplierSlider, TouchMultiplierText, "{0:F2}");
            UpdateSliderText(NibModeBoundsWidthSlider, NibModeBoundsWidthText, "{0:0}");
            UpdateSliderText(FingerModeBoundsWidthSlider, FingerModeBoundsWidthText, "{0:0}");
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

        private MainWindow GetMainWindow() => Application.Current.MainWindow as MainWindow;

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings;
            if (settings?.Advanced == null) return;

            ToggleSwitchIsSpecialScreen.IsOn = settings.Advanced.IsSpecialScreen;
            ToggleSwitchDisableHardwareAcceleration.IsOn = !settings.Canvas.UseHardwareAcceleration;
            TouchMultiplierSlider.Value = settings.Advanced.TouchMultiplier;
            ToggleSwitchEraserBindTouchMultiplier.IsOn = settings.Advanced.EraserBindTouchMultiplier;
            NibModeBoundsWidthSlider.Value = settings.Advanced.NibModeBoundsWidth;
            FingerModeBoundsWidthSlider.Value = settings.Advanced.FingerModeBoundsWidth;
            ToggleSwitchIsQuadIR.IsOn = settings.Advanced.IsQuadIR;
            ToggleSwitchIsLogEnabled.IsOn = settings.Advanced.IsLogEnabled;
            ToggleSwitchIsSaveLogByDate.IsOn = settings.Advanced.IsSaveLogByDate;
            ToggleSwitchIsSecondConfimeWhenShutdownApp.IsOn = settings.Advanced.IsSecondConfirmWhenShutdownApp;
            ToggleSwitchIsAutoBackupBeforeUpdate.IsOn = settings.Advanced.IsAutoBackupBeforeUpdate;
            ToggleSwitchIsAutoBackupEnabled.IsOn = settings.Advanced.IsAutoBackupEnabled;

            foreach (ComboBoxItem item in ComboBoxAutoBackupInterval.Items)
            {
                if (item.Tag != null && int.TryParse(item.Tag.ToString(), out int interval) && interval == settings.Advanced.AutoBackupIntervalDays)
                {
                    ComboBoxAutoBackupInterval.SelectedItem = item;
                    break;
                }
            }

            CardTouchMultiplier.IsExpanded = settings.Advanced.IsSpecialScreen;
        }

        #region Special Screen & Touch Multiplier

        private void ToggleSwitchIsSpecialScreen_OnToggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Advanced.IsSpecialScreen = ToggleSwitchIsSpecialScreen.IsOn;
            CardTouchMultiplier.IsExpanded = ToggleSwitchIsSpecialScreen.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchDisableHardwareAcceleration_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Canvas.UseHardwareAcceleration = !ToggleSwitchDisableHardwareAcceleration.IsOn;
            var mw = GetMainWindow();
            if (mw != null) mw.UpdateInkSmoothingConfig();
            SettingsManager.SaveSettingsToFile();
        }

        private void TouchMultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(TouchMultiplierSlider, TouchMultiplierText, "{0:F2}");
            if (!_isLoaded) return;
            var val = Math.Round(TouchMultiplierSlider.Value, 2);
            TouchMultiplierSlider.Value = val;
            SettingsManager.Settings.Advanced.TouchMultiplier = val;
            SettingsManager.SaveSettingsToFile();
        }

        private void BorderCalculateMultiplier_TouchDown(object sender, TouchEventArgs e)
        {
            var args = e.GetTouchPoint(null).Bounds;
            double value;
            if (!SettingsManager.Settings.Advanced.IsQuadIR) value = args.Width;
            else value = Math.Sqrt(args.Width * args.Height);

            TextBlockShowCalculatedMultiplier.Text = (5 / (value * 1.1)).ToString();
        }

        #endregion

        #region Eraser & Bounds Width

        private void ToggleSwitchEraserBindTouchMultiplier_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Advanced.EraserBindTouchMultiplier = ToggleSwitchEraserBindTouchMultiplier.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void NibModeBoundsWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(NibModeBoundsWidthSlider, NibModeBoundsWidthText, "{0:0}");
            if (!_isLoaded) return;
            SettingsManager.Settings.Advanced.NibModeBoundsWidth = (int)e.NewValue;
            var mw = GetMainWindow();
            if (mw != null)
            {
                if (SettingsManager.Settings.Startup.IsEnableNibMode)
                    mw.BoundsWidth = SettingsManager.Settings.Advanced.NibModeBoundsWidth;
                else
                    mw.BoundsWidth = SettingsManager.Settings.Advanced.FingerModeBoundsWidth;
            }
            SettingsManager.SaveSettingsToFile();
        }

        private void FingerModeBoundsWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(FingerModeBoundsWidthSlider, FingerModeBoundsWidthText, "{0:0}");
            if (!_isLoaded) return;
            SettingsManager.Settings.Advanced.FingerModeBoundsWidth = (int)e.NewValue;
            var mw = GetMainWindow();
            if (mw != null)
            {
                if (SettingsManager.Settings.Startup.IsEnableNibMode)
                    mw.BoundsWidth = SettingsManager.Settings.Advanced.NibModeBoundsWidth;
                else
                    mw.BoundsWidth = SettingsManager.Settings.Advanced.FingerModeBoundsWidth;
            }
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchIsQuadIR_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Advanced.IsQuadIR = ToggleSwitchIsQuadIR.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        #endregion

        #region Logging & Exit

        private void ToggleSwitchIsLogEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Advanced.IsLogEnabled = ToggleSwitchIsLogEnabled.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchIsSaveLogByDate_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Advanced.IsSaveLogByDate = ToggleSwitchIsSaveLogByDate.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchIsSecondConfimeWhenShutdownApp_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Advanced.IsSecondConfirmWhenShutdownApp = ToggleSwitchIsSecondConfimeWhenShutdownApp.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        #endregion

        #region Backup

        private void ToggleSwitchIsAutoBackupBeforeUpdate_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Advanced.IsAutoBackupBeforeUpdate = ToggleSwitchIsAutoBackupBeforeUpdate.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchIsAutoBackupEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Advanced.IsAutoBackupEnabled = ToggleSwitchIsAutoBackupEnabled.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ComboBoxAutoBackupInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (ComboBoxAutoBackupInterval.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
            {
                if (int.TryParse(selectedItem.Tag.ToString(), out int interval))
                {
                    SettingsManager.Settings.Advanced.AutoBackupIntervalDays = interval;
                    SettingsManager.SaveSettingsToFile();
                }
            }
        }

        private void BtnManualBackup_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                string backupDir = Path.Combine(App.RootPath, "Backups");
                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                    LogHelper.WriteLogToFile($"创建备份目录: {backupDir}");
                }

                string backupFileName = $"Settings_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string backupPath = Path.Combine(backupDir, backupFileName);

                string settingsJson = Newtonsoft.Json.JsonConvert.SerializeObject(SettingsManager.Settings, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(backupPath, settingsJson);

                LogHelper.WriteLogToFile($"成功创建设置备份: {backupPath}");
                MessageBox.Show(string.Format(StorageStrings.Backup_SuccessMsg, backupPath), StorageStrings.Backup_SuccessTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"创建设置备份时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show(string.Format(StorageStrings.Backup_CreateFailedMsg, ex.Message), StorageStrings.Backup_FailedTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                string backupDir = Path.Combine(App.RootPath, "Backups");
                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                    LogHelper.WriteLogToFile($"创建备份目录: {backupDir}");
                    MessageBox.Show(StorageStrings.Restore_NoBackupFound, StorageStrings.Restore_FailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.InitialDirectory = backupDir;
                dlg.Filter = $"{StorageStrings.Restore_FilterLabel}|Settings_Backup_*.json|{StorageStrings.Restore_AllJsonFilter}|*.json";
                dlg.Title = StorageStrings.Restore_SelectFileTitle;

                if (dlg.ShowDialog() == true)
                {
                    string backupJson = File.ReadAllText(dlg.FileName);
                    Settings backupSettings = Newtonsoft.Json.JsonConvert.DeserializeObject<Settings>(backupJson);

                    if (backupSettings != null)
                    {
                        if (MessageBox.Show(StorageStrings.Restore_ConfirmMsg, StorageStrings.Restore_ConfirmTitle,
                                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            string currentSettingsJson = Newtonsoft.Json.JsonConvert.SerializeObject(SettingsManager.Settings, Newtonsoft.Json.Formatting.Indented);
                            string tempBackupPath = Path.Combine(backupDir, $"Settings_Before_Restore_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                            File.WriteAllText(tempBackupPath, currentSettingsJson);

                            SettingsManager.Settings = backupSettings;
                            SettingsManager.SaveSettingsToFile();

                            var mw = GetMainWindow();
                            if (mw != null) mw.ReloadSettingsFromFile();

                            LogHelper.WriteLogToFile($"成功从备份还原设置: {dlg.FileName}");
                            MessageBox.Show(StorageStrings.Restore_SuccessMsg, StorageStrings.Restore_SuccessTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    else
                    {
                        MessageBox.Show(StorageStrings.Restore_ParseFailed, StorageStrings.Restore_FailedTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"还原设置备份时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show(string.Format(StorageStrings.Restore_FailedMsg, ex.Message), StorageStrings.Restore_FailedTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Config Profiles

        private void RefreshConfigProfileList()
        {
            try
            {
                if (ComboBoxConfigProfile == null) return;
                _isRefreshingConfigProfileList = true;
                try
                {
                    var names = ConfigProfileManager.ListProfileNames();
                    ComboBoxConfigProfile.ItemsSource = names;
                    if (names.Count == 0)
                    {
                        ComboBoxConfigProfile.SelectedItem = null;
                    }
                    else if (_lastAppliedProfileName != null && names.Contains(_lastAppliedProfileName))
                    {
                        ComboBoxConfigProfile.SelectedItem = _lastAppliedProfileName;
                    }
                    else
                    {
                        var selected = ComboBoxConfigProfile.SelectedItem as string;
                        if (selected != null && names.Contains(selected))
                            ComboBoxConfigProfile.SelectedItem = selected;
                        else
                            ComboBoxConfigProfile.SelectedIndex = 0;
                    }
                    if (BtnDeleteConfigProfile != null)
                        BtnDeleteConfigProfile.IsEnabled = ComboBoxConfigProfile.SelectedItem != null;
                }
                finally
                {
                    _isRefreshingConfigProfileList = false;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"刷新配置方案列表失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ComboBoxConfigProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BtnDeleteConfigProfile != null)
                BtnDeleteConfigProfile.IsEnabled = ComboBoxConfigProfile?.SelectedItem != null;
            if (!_isLoaded || _isRefreshingConfigProfileList) return;
            var name = ComboBoxConfigProfile?.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) return;
            try
            {
                if (ConfigProfileManager.ApplyProfile(name))
                {
                    _lastAppliedProfileName = name;
                    var mw = GetMainWindow();
                    if (mw != null)
                    {
                        mw.ReloadSettingsFromFile();
                        mw.ShowNotification(string.Format(ConfigStrings.SwitchedToProfile, name));
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换配置方案失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private async void BtnSaveAsConfigProfile_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var input = new System.Windows.Controls.TextBox
            {
                MinWidth = 260,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var label = new System.Windows.Controls.TextBlock
            {
                Text = ConfigStrings.ProfileNameLabel,
                Margin = new Thickness(0, 0, 0, 8)
            };
            var content = new iNKORE.UI.WPF.Controls.SimpleStackPanel { Spacing = 6 };
            content.Children.Add(label);
            content.Children.Add(input);
            var dialog = new ContentDialog
            {
                Title = ConfigStrings.SaveAsProfileTitle,
                Content = content,
                PrimaryButtonText = FloatingBarStrings.Tools_Save,
                SecondaryButtonText = CommonStrings.Common_Cancel,
                Owner = Window.GetWindow(this) ?? GetMainWindow()
            };
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;
            var name = input.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(ConfigStrings.SaveAs_EnterName, ConfigStrings.SaveAsProfileTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(SettingsManager.Settings, Newtonsoft.Json.Formatting.Indented);
                if (ConfigProfileManager.SaveAsProfile(name, json))
                {
                    _lastAppliedProfileName = name;
                    RefreshConfigProfileList();
                    var mw = GetMainWindow();
                    if (mw != null) mw.ShowNotification(string.Format(ConfigStrings.SavedAsProfile, name));
                }
                else
                    MessageBox.Show(ConfigStrings.SaveAs_Failed, ConfigStrings.SaveAsProfileTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"另存为方案失败: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show(string.Format(ConfigStrings.SaveAs_FailedMsg, ex.Message), ConfigStrings.SaveAsProfileTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDeleteConfigProfile_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var name = ComboBoxConfigProfile?.SelectedItem as string;
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(ConfigStrings.Delete_SelectFirst, ConfigStrings.SaveAsProfileTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                if (MessageBox.Show(string.Format(ConfigStrings.Delete_ConfirmMsg, name), ConfigStrings.Delete_ConfirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
                if (ConfigProfileManager.DeleteProfile(name))
                {
                    RefreshConfigProfileList();
                    var nextName = ComboBoxConfigProfile?.SelectedItem as string;
                    var mw = GetMainWindow();
                    if (!string.IsNullOrEmpty(nextName) && ConfigProfileManager.ApplyProfile(nextName))
                    {
                        _lastAppliedProfileName = nextName;
                        if (mw != null)
                        {
                            mw.ReloadSettingsFromFile();
                            mw.ShowNotification(string.Format(ConfigStrings.DeletedAndSwitched, name, nextName));
                        }
                    }
                    else
                    {
                        if (mw != null) mw.ShowNotification(string.Format(ConfigStrings.DeletedProfile, name));
                    }
                }
                else
                    MessageBox.Show(ConfigStrings.Delete_Failed, ConfigStrings.SaveAsProfileTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"删除配置文件失败: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show(string.Format(ConfigStrings.Delete_FailedMsg, ex.Message), ConfigStrings.SaveAsProfileTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
