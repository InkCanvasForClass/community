using Ink_Canvas.Helpers;
using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas
{
    public partial class PrivacyAgreementWindow : Window
    {
        public bool UserAccepted { get; private set; } = false;
        private bool wasSettingsPanelVisible = false;

        public PrivacyAgreementWindow()
        {
            InitializeComponent();
            this.Topmost = true;
            AnimationsHelper.ShowWithSlideFromBottomAndFade(this, 0.25);
            ApplyTheme();
            HideSettingsPanel();
        }

        private void HideSettingsPanel()
        {
            try
            {
                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    var borderSettings = mainWindow.FindName("BorderSettings") as Border;
                    var borderSettingsMask = mainWindow.FindName("BorderSettingsMask") as Border;

                    if (borderSettings != null)
                    {
                        wasSettingsPanelVisible = borderSettings.Visibility == Visibility.Visible;
                        if (wasSettingsPanelVisible)
                        {
                            borderSettings.Visibility = Visibility.Hidden;
                        }
                    }

                    if (borderSettingsMask != null)
                    {
                        if (wasSettingsPanelVisible)
                        {
                            borderSettingsMask.Visibility = Visibility.Hidden;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"隐藏设置面板失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Topmost = true;
                
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    this.Activate();
                    this.Focus();
                    this.Topmost = true;
                    SetForegroundWindow(new WindowInteropHelper(this).Handle);
                }), DispatcherPriority.Loaded);

                string privacyText = null;
                
                try
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    var resourceName = "Ink_Canvas.privacy.txt";
                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            using (StreamReader reader = new StreamReader(stream, System.Text.Encoding.UTF8))
                            {
                                privacyText = reader.ReadToEnd();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"读取隐私说明失败: {ex.Message}", LogHelper.LogType.Warning);
                }

                if (string.IsNullOrWhiteSpace(privacyText))
                {
                    privacyText = "未找到隐私说明文件（privacy.txt）。";
                }

                TextBoxPrivacyContent.Text = privacyText;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"读取隐私说明失败: {ex.Message}", LogHelper.LogType.Warning);
                TextBoxPrivacyContent.Text = "读取隐私说明文件时出错。";
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (wasSettingsPanelVisible)
            {
                try
                {
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        var borderSettings = mainWindow.FindName("BorderSettings") as Border;
                        var borderSettingsMask = mainWindow.FindName("BorderSettingsMask") as Border;

                        if (borderSettings != null)
                        {
                            borderSettings.Visibility = Visibility.Visible;
                        }

                        if (borderSettingsMask != null)
                        {
                            borderSettingsMask.Visibility = Visibility.Visible;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"恢复设置面板失败: {ex.Message}", LogHelper.LogType.Warning);
                }
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            UserAccepted = false;
            DialogResult = false;
            Close();
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            UserAccepted = true;
            DialogResult = true;
            Close();
        }

        private void ApplyTheme()
        {
            try
            {
                if (MainWindow.Settings != null)
                {
                    ApplyTheme(MainWindow.Settings);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用隐私说明窗口主题出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ApplyTheme(Settings settings)
        {
            try
            {
                if (settings.Appearance.Theme == 0)
                {
                    iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, iNKORE.UI.WPF.Modern.ElementTheme.Light);
                    ApplyThemeResources("Light");
                }
                else if (settings.Appearance.Theme == 1)
                {
                    iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, iNKORE.UI.WPF.Modern.ElementTheme.Dark);
                    ApplyThemeResources("Dark");
                }
                else
                {
                    bool isSystemLight = IsSystemThemeLight();
                    if (isSystemLight)
                    {
                        iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, iNKORE.UI.WPF.Modern.ElementTheme.Light);
                        ApplyThemeResources("Light");
                    }
                    else
                    {
                        iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, iNKORE.UI.WPF.Modern.ElementTheme.Dark);
                        ApplyThemeResources("Dark");
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用隐私说明窗口主题出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ApplyThemeResources(string theme)
        {
            try
            {
                var resources = this.Resources;

                if (theme == "Light")
                {
                    resources["PrivacyAgreementWindowBackground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    resources["PrivacyAgreementWindowForeground"] = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    resources["PrivacyAgreementWindowButtonBackground"] = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                    resources["PrivacyAgreementWindowButtonForeground"] = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    resources["PrivacyAgreementWindowBorderBrush"] = new SolidColorBrush(Color.FromRgb(228, 228, 231));
                    resources["PrivacyAgreementWindowButtonAcceptBackground"] = new SolidColorBrush(Color.FromRgb(53, 132, 228));
                    resources["PrivacyAgreementWindowButtonAcceptForeground"] = new SolidColorBrush(Colors.White);
                }
                else
                {
                    resources["PrivacyAgreementWindowBackground"] = new SolidColorBrush(Color.FromRgb(31, 31, 31));
                    resources["PrivacyAgreementWindowForeground"] = new SolidColorBrush(Colors.White);
                    resources["PrivacyAgreementWindowButtonBackground"] = new SolidColorBrush(Color.FromRgb(42, 42, 42));
                    resources["PrivacyAgreementWindowButtonForeground"] = new SolidColorBrush(Colors.White);
                    resources["PrivacyAgreementWindowBorderBrush"] = new SolidColorBrush(Color.FromRgb(224, 224, 224));
                    resources["PrivacyAgreementWindowButtonAcceptBackground"] = new SolidColorBrush(Color.FromRgb(53, 132, 228));
                    resources["PrivacyAgreementWindowButtonAcceptForeground"] = new SolidColorBrush(Colors.White);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用隐私说明窗口主题资源出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private bool IsSystemThemeLight()
        {
            var light = false;
            try
            {
                var registryKey = Microsoft.Win32.Registry.CurrentUser;
                var themeKey = registryKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (themeKey != null)
                {
                    var value = themeKey.GetValue("AppsUseLightTheme");
                    if (value != null)
                    {
                        light = (int)value == 1;
                    }
                    themeKey.Close();
                }
            }
            catch
            {
                light = true;
            }
            return light;
        }
    }
}

