using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class PrivacyPage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private bool _isLoaded = false;
        private bool _isChangingTelemetryInternally;
        private bool _isChangingTelemetryPrivacyInternally;

        public PrivacyPage()
        {
            InitializeComponent();
            Loaded += PrivacyPage_Loaded;
        }

        private void PrivacyPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
        }

        private void LoadSettings()
        {
            _isLoaded = false;

            try
            {
                var settings = SettingsManager.Settings;
                if (settings?.Startup != null)
                {
                    int idx = 0;
                    switch (settings.Startup.TelemetryUploadLevel)
                    {
                        case TelemetryUploadLevel.None:
                            idx = 0;
                            break;
                        case TelemetryUploadLevel.Basic:
                            idx = 1;
                            break;
                        case TelemetryUploadLevel.Extended:
                            idx = 2;
                            break;
                        default:
                            idx = 0;
                            break;
                    }
                    ComboBoxTelemetryUploadLevel.SelectedIndex = idx;
                    CheckBoxTelemetryPrivacyAccepted.IsChecked = settings.Startup.HasAcceptedTelemetryPrivacy;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载隐私页面设置时出错: {ex.Message}");
            }

            _isLoaded = true;
        }

        private void ComboBoxTelemetryUploadLevel_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (_isChangingTelemetryInternally) return;
            var oldLevel = SettingsManager.Settings.Startup.TelemetryUploadLevel;
            var item = ComboBoxTelemetryUploadLevel?.SelectedItem as ComboBoxItem;
            if (item == null) return;

            var tag = item.Tag?.ToString() ?? "0";
            var newLevel = TelemetryUploadLevel.None;
            switch (tag)
            {
                case "1":
                    newLevel = TelemetryUploadLevel.Basic;
                    break;
                case "2":
                    newLevel = TelemetryUploadLevel.Extended;
                    break;
                default:
                    newLevel = TelemetryUploadLevel.None;
                    break;
            }

            if (newLevel == TelemetryUploadLevel.None &&
                oldLevel != TelemetryUploadLevel.None &&
                SettingsManager.Settings.Startup.UpdateChannel != UpdateChannel.Release)
            {
                var result = MessageBox.Show(
                    "关闭匿名使用数据上传后，将无法继续使用预览/测试通道，系统会自动切换回正式通道（Release）。\n\n是否确认关闭？",
                    "确认关闭遥测",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    _isChangingTelemetryInternally = true;
                    try
                    {
                        int idx = 0;
                        switch (oldLevel)
                        {
                            case TelemetryUploadLevel.Basic:
                                idx = 1;
                                break;
                            case TelemetryUploadLevel.Extended:
                                idx = 2;
                                break;
                            default:
                                idx = 0;
                                break;
                        }
                        ComboBoxTelemetryUploadLevel.SelectedIndex = idx;
                    }
                    finally
                    {
                        _isChangingTelemetryInternally = false;
                    }
                    return;
                }

                SettingsManager.Settings.Startup.UpdateChannel = UpdateChannel.Release;
                DeviceIdentifier.UpdateUsageChannel(UpdateChannel.Release);
            }

            if (newLevel != TelemetryUploadLevel.None && !SettingsManager.Settings.Startup.HasAcceptedTelemetryPrivacy)
            {
                MessageBox.Show(
                    "在开启匿名使用数据上传前，请先阅读并勾选上方的隐私说明。",
                    "需要同意隐私说明",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                _isChangingTelemetryInternally = true;
                try
                {
                    SettingsManager.Settings.Startup.TelemetryUploadLevel = TelemetryUploadLevel.None;
                    if (ComboBoxTelemetryUploadLevel != null)
                    {
                        ComboBoxTelemetryUploadLevel.SelectedIndex = 0;
                    }
                }
                finally
                {
                    _isChangingTelemetryInternally = false;
                }

                return;
            }

            SettingsManager.Settings.Startup.TelemetryUploadLevel = newLevel;
            SettingsManager.SaveSettingsToFile();
        }

        private void CheckBoxTelemetryPrivacyAccepted_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (_isChangingTelemetryPrivacyInternally) return;

            bool isChecked = CheckBoxTelemetryPrivacyAccepted.IsChecked == true;

            if (isChecked)
            {
                if (!PrivacyFileExists())
                {
                    MessageBox.Show(
                        "未找到隐私说明文件（privacy / privacy.txt），暂时无法启用匿名使用数据上传。",
                        "隐私说明缺失",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    _isChangingTelemetryPrivacyInternally = true;
                    try
                    {
                        CheckBoxTelemetryPrivacyAccepted.IsChecked = false;
                    }
                    finally
                    {
                        _isChangingTelemetryPrivacyInternally = false;
                    }

                    SettingsManager.Settings.Startup.HasAcceptedTelemetryPrivacy = false;
                    SettingsManager.SaveSettingsToFile();
                    return;
                }

                var privacyWindow = new PrivacyAgreementWindow();
                bool? dialogResult = privacyWindow.ShowDialog();

                if (dialogResult == true && privacyWindow.UserAccepted)
                {
                    SettingsManager.Settings.Startup.HasAcceptedTelemetryPrivacy = true;
                    SettingsManager.SaveSettingsToFile();
                }
                else
                {
                    _isChangingTelemetryPrivacyInternally = true;
                    try
                    {
                        CheckBoxTelemetryPrivacyAccepted.IsChecked = false;
                    }
                    finally
                    {
                        _isChangingTelemetryPrivacyInternally = false;
                    }

                    SettingsManager.Settings.Startup.HasAcceptedTelemetryPrivacy = false;
                    SettingsManager.SaveSettingsToFile();
                }
            }
            else
            {
                var result = MessageBox.Show(
                    "取消同意隐私说明后，将关闭匿名使用数据上传，并切回正式通道（Release）。\n\n是否确认？",
                    "确认取消隐私同意",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    _isChangingTelemetryPrivacyInternally = true;
                    try
                    {
                        CheckBoxTelemetryPrivacyAccepted.IsChecked = true;
                    }
                    finally
                    {
                        _isChangingTelemetryPrivacyInternally = false;
                    }
                    return;
                }

                _isChangingTelemetryInternally = true;
                try
                {
                    SettingsManager.Settings.Startup.TelemetryUploadLevel = TelemetryUploadLevel.None;
                    if (ComboBoxTelemetryUploadLevel != null)
                    {
                        ComboBoxTelemetryUploadLevel.SelectedIndex = 0;
                    }
                }
                finally
                {
                    _isChangingTelemetryInternally = false;
                }

                if (SettingsManager.Settings.Startup.UpdateChannel != UpdateChannel.Release)
                {
                    SettingsManager.Settings.Startup.UpdateChannel = UpdateChannel.Release;
                    DeviceIdentifier.UpdateUsageChannel(UpdateChannel.Release);
                }

                SettingsManager.Settings.Startup.HasAcceptedTelemetryPrivacy = false;
                SettingsManager.SaveSettingsToFile();
            }
        }

        private static bool PrivacyFileExists()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "Ink_Canvas.privacy.txt";
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    return stream != null;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
