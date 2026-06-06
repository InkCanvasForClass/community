using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class BackupPage : Page
    {
        private bool _isLoaded = false;

        public BackupPage()
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

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings;
            if (settings?.Advanced == null) return;

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
        }

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

                            SettingsActionHub.OnReloadSettingsFromFile();

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
    }
}
