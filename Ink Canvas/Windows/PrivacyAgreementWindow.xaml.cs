using Ink_Canvas.Helpers;
using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Ink_Canvas
{
    public partial class PrivacyAgreementWindow : Window
    {
        public bool UserAccepted { get; private set; } = false;

        public PrivacyAgreementWindow()
        {
            InitializeComponent();
            WindowBackdropHelper.Apply(this);
            Topmost = true;
            AnimationsHelper.ShowWithSlideFromBottomAndFade(this, 0.25);
            ApplyTheme();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Topmost = true;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Activate();
                    Focus();
                    Topmost = true;
                    PInvoke.SetForegroundWindow(new HWND(new WindowInteropHelper(this).Handle));
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
                    privacyText = Properties.MainWindowStrings.Main_Privacy_FileNotFound;
                }

                TextBoxPrivacyContent.Text = privacyText;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"读取隐私说明失败: {ex.Message}", LogHelper.LogType.Warning);
                TextBoxPrivacyContent.Text = Properties.MainWindowStrings.Main_Privacy_ReadError;
            }
        }

        //[DllImport("user32.dll")]
        //private static extern bool SetForegroundWindow(IntPtr hWnd);

        private void Window_Closing(object sender, CancelEventArgs e) { }

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
                var settings = MainWindow.Settings;
                if (settings == null) return;

                iNKORE.UI.WPF.Modern.ElementTheme target;
                switch (settings.Appearance.Theme)
                {
                    case 0: target = iNKORE.UI.WPF.Modern.ElementTheme.Light; break;
                    case 1: target = iNKORE.UI.WPF.Modern.ElementTheme.Dark; break;
                    default:
                        target = IsSystemThemeLight()
                            ? iNKORE.UI.WPF.Modern.ElementTheme.Light
                            : iNKORE.UI.WPF.Modern.ElementTheme.Dark; break;
                }
                iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, target);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用隐私说明窗口主题出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private static bool IsSystemThemeLight()
        {
            try
            {
                var registryKey = Microsoft.Win32.Registry.CurrentUser;
                using (var themeKey = registryKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var value = themeKey?.GetValue("AppsUseLightTheme");
                    if (value is int i) return i == 1;
                }
            }
            catch { }
            return true;
        }
    }
}
