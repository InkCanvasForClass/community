using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class SecurityPage : Page
    {
        private bool _isLoaded = false;

        public SecurityPage()
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
            _isLoaded = false;
            try
            {
                var settings = SettingsManager.Settings;
                if (settings == null) return;
                if (settings.Security == null) settings.Security = new Security();

                var sec = settings.Security;
                CardPasswordEnabled.IsOn = sec.PasswordEnabled;
                CardRequirePasswordOnExit.IsOn = sec.RequirePasswordOnExit;
                CardRequirePasswordOnEnterSettings.IsOn = sec.RequirePasswordOnEnterSettings;
                CardRequirePasswordOnResetConfig.IsOn = sec.RequirePasswordOnResetConfig;
                CardRequirePasswordOnModifyOrClearNameList.IsOn = sec.RequirePasswordOnModifyOrClearNameList;
                CardTotpEnabled.IsOn = sec.TotpEnabled;
                TextBoxTotpSecret.Text = sec.TotpSecret ?? "";
                CardEnableProcessProtection.IsOn = sec.EnableProcessProtection;

                // Load U-disk settings
                CardUsbVerificationEnabled.IsOn = sec.UsbVerificationEnabled;
                TextBoxUsbAuthorizedSns.Text = sec.UsbAuthorizedSns ?? "";

                UpdatePasswordUiState();

                if (sec.UsbVerificationEnabled)
                {
                    RefreshUsbDrives();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载安全页面设置时出错: {ex.Message}");
            }
            _isLoaded = true;
        }

        private void UpdatePasswordUiState()
        {
            var sec = SettingsManager.Settings?.Security;
            var passwordEnabled = sec != null && sec.PasswordEnabled;
            var totpEnabled = sec != null && sec.TotpEnabled;
            var usbEnabled = sec != null && sec.UsbVerificationEnabled;

            if (BtnSetOrChangePassword != null) BtnSetOrChangePassword.IsEnabled = passwordEnabled;
            if (BtnGenerateTotpSecret != null) BtnGenerateTotpSecret.IsEnabled = CardTotpEnabled?.IsOn == true;
            if (TextBoxTotpSecret != null) TextBoxTotpSecret.IsEnabled = CardTotpEnabled?.IsOn == true;

            CardRequirePasswordOnExit.IsEnabled = passwordEnabled || totpEnabled;
            CardRequirePasswordOnEnterSettings.IsEnabled = passwordEnabled || totpEnabled;
            CardRequirePasswordOnResetConfig.IsEnabled = passwordEnabled || totpEnabled;
            CardRequirePasswordOnModifyOrClearNameList.IsEnabled = passwordEnabled || totpEnabled;

            // Update U-disk UI state
            if (CardUsbSnManage != null) CardUsbSnManage.IsEnabled = usbEnabled;
            if (CardUsbSnRegister != null) CardUsbSnRegister.IsEnabled = usbEnabled;
        }

        private void SetCardIsOnSilently(Ink_Canvas.Controls.LabeledSettingsCard card, bool value)
        {
            var prev = _isLoaded;
            _isLoaded = false;
            try { card.IsOn = value; }
            finally { _isLoaded = prev; }
        }

        private async void ToggleSwitchPasswordEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var settings = SettingsManager.Settings;
            if (settings == null) return;
            if (settings.Security == null) settings.Security = new Security();
            var sec = settings.Security;

            bool newState = CardPasswordEnabled.IsOn;
            var owner = Window.GetWindow(this);

            if (newState)
            {
                var havePassword = SecurityManager.HasPasswordConfigured(settings);
                if (!havePassword)
                {
                    var pwd = await SecurityManager.PromptSetNewPasswordAsync(owner);
                    if (string.IsNullOrEmpty(pwd))
                    {
                        SetCardIsOnSilently(CardPasswordEnabled, false);
                        return;
                    }
                    SecurityManager.SetPassword(settings, pwd);
                }

                sec.PasswordEnabled = true;
                SettingsManager.SaveSettingsToFile();
                UpdatePasswordUiState();
            }
            else
            {
                if (SecurityManager.HasPasswordConfigured(settings))
                {
                    bool ok = await SecurityManager.PromptAndVerifyPasswordOrTotpAsync(settings, owner,
                        SecurityStrings.Security_DisablePasswordTitle, SecurityStrings.Security_DisablePasswordMessage);
                    if (!ok)
                    {
                        SetCardIsOnSilently(CardPasswordEnabled, true);
                        return;
                    }
                }

                sec.PasswordEnabled = false;
                SecurityManager.ClearPassword(settings);
                SettingsManager.SaveSettingsToFile();
                UpdatePasswordUiState();
            }
        }

        private async void BtnSetOrChangePassword_Click(object sender, RoutedEventArgs e)
        {
            var settings = SettingsManager.Settings;
            if (settings == null) return;
            if (settings.Security == null) settings.Security = new Security();

            var owner = Window.GetWindow(this);
            var newPwd = await SecurityManager.PromptChangePasswordAsync(settings, owner);
            if (!string.IsNullOrEmpty(newPwd))
            {
                SecurityManager.SetPassword(settings, newPwd);
                settings.Security.PasswordEnabled = true;
                SettingsManager.SaveSettingsToFile();

                SetCardIsOnSilently(CardPasswordEnabled, true);
                UpdatePasswordUiState();
            }
        }

        private void ToggleSwitchRequirePasswordOnExit_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Security.RequirePasswordOnExit = CardRequirePasswordOnExit.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchRequirePasswordOnEnterSettings_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Security.RequirePasswordOnEnterSettings = CardRequirePasswordOnEnterSettings.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchRequirePasswordOnResetConfig_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Security.RequirePasswordOnResetConfig = CardRequirePasswordOnResetConfig.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchRequirePasswordOnModifyOrClearNameList_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Security.RequirePasswordOnModifyOrClearNameList = CardRequirePasswordOnModifyOrClearNameList.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnableProcessProtection_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            bool newState = CardEnableProcessProtection.IsOn;
            SettingsManager.Settings.Security.EnableProcessProtection = newState;
            SettingsManager.SaveSettingsToFile();
            ProcessProtectionManager.SetEnabled(newState);
        }

        private void ToggleSwitchTotpEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var settings = SettingsManager.Settings;
            if (settings == null) return;
            if (settings.Security == null) settings.Security = new Security();

            var sec = settings.Security;
            sec.TotpEnabled = CardTotpEnabled.IsOn;
            if (sec.TotpEnabled && string.IsNullOrWhiteSpace(sec.TotpSecret))
            {
                sec.TotpSecret = SecurityManager.GenerateTotpSecret();
                TextBoxTotpSecret.Text = sec.TotpSecret;
            }

            SettingsManager.SaveSettingsToFile();
            UpdatePasswordUiState();
        }

        private async void BtnGenerateTotpSecret_Click(object sender, RoutedEventArgs e)
        {
            var settings = SettingsManager.Settings;
            if (settings == null) return;
            if (settings.Security == null) settings.Security = new Security();

            var owner = Window.GetWindow(this);
            bool ok = await SecurityManager.PromptAndVerifyPasswordOrTotpAsync(settings, owner,
                SecurityStrings.Security_ResetTotpTitle, SecurityStrings.Security_ResetTotpMessage);
            if (!ok) return;

            settings.Security.TotpSecret = SecurityManager.GenerateTotpSecret();
            settings.Security.TotpEnabled = true;
            TextBoxTotpSecret.Text = settings.Security.TotpSecret;
            SetCardIsOnSilently(CardTotpEnabled, true);
            SettingsManager.SaveSettingsToFile();
            UpdatePasswordUiState();
        }

        private void RefreshUsbDrives()
        {
            try
            {
                if (ComboBoxUsbDrives == null) return;
                ComboBoxUsbDrives.Items.Clear();

                var drives = UsbSecurityManager.GetConnectedUsbDrives();
                if (drives.Count == 0)
                {
                    ComboBoxUsbDrives.Items.Add(new ComboBoxItem
                    {
                        Content = SecurityStrings.Security_UsbPromptNoDriveDetected,
                        IsEnabled = false
                    });
                    ComboBoxUsbDrives.SelectedIndex = 0;
                    if (BtnAuthorizeSelectedDrive != null) BtnAuthorizeSelectedDrive.IsEnabled = false;
                    return;
                }

                if (BtnAuthorizeSelectedDrive != null) BtnAuthorizeSelectedDrive.IsEnabled = true;
                foreach (var d in drives)
                {
                    string label = string.IsNullOrEmpty(d.VolumeLabel) ? SecurityStrings.Security_UsbPromptLocalDisk : d.VolumeLabel;
                    string dispSn = d.SerialNumber;
                    if (dispSn.Length > 12)
                    {
                        dispSn = dispSn.Substring(0, 12) + "...";
                    }
                    string itemText = $"{label} ({d.DriveLetter}) [SN: {dispSn}]";
                    ComboBoxUsbDrives.Items.Add(new ComboBoxItem
                    {
                        Content = itemText,
                        Tag = d
                    });
                }
                ComboBoxUsbDrives.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshUsbDrives error: {ex.Message}");
                MessageBox.Show($"Refresh USB drives failed. Info:\n{ex.Message}\n\nStack:\n{ex.StackTrace}", SecurityStrings.Security_InfoBarTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToggleSwitchUsbVerificationEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var settings = SettingsManager.Settings;
            if (settings?.Security == null) return;

            settings.Security.UsbVerificationEnabled = CardUsbVerificationEnabled.IsOn;
            SettingsManager.SaveSettingsToFile();
            UpdatePasswordUiState();

            if (settings.Security.UsbVerificationEnabled)
            {
                RefreshUsbDrives();
            }
        }

        private void TextBoxUsbAuthorizedSns_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            var settings = SettingsManager.Settings;
            if (settings?.Security == null) return;

            settings.Security.UsbAuthorizedSns = TextBoxUsbAuthorizedSns.Text;
            SettingsManager.SaveSettingsToFile();
        }

        private void BtnRefreshUsbDrives_Click(object sender, RoutedEventArgs e)
        {
            RefreshUsbDrives();
        }

        private void BtnAuthorizeSelectedDrive_Click(object sender, RoutedEventArgs e)
        {
            if (ComboBoxUsbDrives.SelectedItem is ComboBoxItem item && item.Tag is UsbDriveInfo drive)
            {
                if (string.IsNullOrEmpty(drive.SerialNumber))
                {
                    MessageBox.Show(SecurityStrings.Security_UsbPromptNoValidSn, SecurityStrings.Security_InfoBarTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var current = TextBoxUsbAuthorizedSns.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(current))
                {
                    TextBoxUsbAuthorizedSns.Text = drive.SerialNumber;
                }
                else if (current.Contains(drive.SerialNumber))
                {
                    MessageBox.Show(SecurityStrings.Security_UsbPromptAlreadyAuthorized, SecurityStrings.Security_InfoBarTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    TextBoxUsbAuthorizedSns.Text = current + "," + drive.SerialNumber;
                }
                MessageBox.Show(SecurityStrings.Security_UsbPromptAuthorizeSuccess, SecurityStrings.Security_InfoBarTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(SecurityStrings.Security_UsbPromptSelectDrive, SecurityStrings.Security_InfoBarTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
