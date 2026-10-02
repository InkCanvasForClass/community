using Ink_Canvas.Helpers;
using WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType;
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
            Topmost = true;
            AnimationsHelper.ShowWithSlideFromBottomAndFade(this, 0.25);
            // 先应用主题再应用背景：WPF-UI 的主题切换会连带移除窗口背景，顺序反了会丢背景
            ApplyTheme();
            WindowBackdropHelper.Apply(this);
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

                Wpf.Ui.Appearance.ApplicationTheme target;
                switch (settings.Appearance.Theme)
                {
                    case 0: target = Wpf.Ui.Appearance.ApplicationTheme.Light; break;
                    case 1: target = Wpf.Ui.Appearance.ApplicationTheme.Dark; break;
                    default:
                        target = IsSystemThemeLight()
                            ? Wpf.Ui.Appearance.ApplicationTheme.Light
                            : Wpf.Ui.Appearance.ApplicationTheme.Dark; break;
                }
                ThemeHelper.ApplyApplicationTheme(target);
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
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[UI] 隐私说明窗口读取系统主题注册表项失败: {ex.Message}", LogHelper.LogType.Info);
            }
            return true;
        }
    }
}
