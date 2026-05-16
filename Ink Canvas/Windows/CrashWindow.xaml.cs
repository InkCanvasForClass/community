using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Ink_Canvas
{
    public partial class CrashWindow : Window
    {
        public string CrashInfo { get; set; } = string.Empty;

        public CrashWindow()
        {
            InitializeComponent();
            WindowBackdropHelper.Apply(this);
            Topmost = true;
            AnimationsHelper.ShowWithSlideFromBottomAndFade(this, 0.25);
            ApplyTheme();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TextBoxCrashInfo.Text = string.IsNullOrWhiteSpace(CrashInfo)
                ? Strings.GetString("CrashWindowNoDetails") ?? "没有可用的崩溃详情。"
                : CrashInfo;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                Activate();
                Focus();
                Topmost = true;
                SetForegroundWindow(new WindowInteropHelper(this).Handle);
            }), DispatcherPriority.Loaded);
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private void ButtonCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TextBoxCrashInfo.Text ?? string.Empty);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"复制崩溃详情失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
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
                LogHelper.WriteLogToFile($"应用崩溃详情窗口主题出错: {ex.Message}", LogHelper.LogType.Error);
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
