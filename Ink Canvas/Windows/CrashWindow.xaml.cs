using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;

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
                ? CrashStrings.CrashWindowNoDetails
                : CrashInfo;

            // 延迟到 Background 优先级，确保窗口 HWND 已完全创建并显示后再 Activate
            // （Loaded 时机 HWND 可能尚未创建，调用 Activate 会抛 InvalidOperationException
            //  "显示 Window 之前，无法调用 DragMove 或 Activate"）
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (!IsVisible) return;
                    Activate();
                    Focus();
                    Topmost = true;
                    var hwnd = new WindowInteropHelper(this).Handle;
                    if (hwnd != IntPtr.Zero)
                    {
                        PInvoke.SetForegroundWindow(new HWND(hwnd));
                    }
                }
                catch (Exception ex)
                {
                    // 崩溃窗口本身不应再抛异常导致二次崩溃，仅记录日志
                    LogHelper.WriteLogToFile($"CrashWindow 激活失败: {ex.Message}", LogHelper.LogType.Warning);
                }
            }), DispatcherPriority.Background);
        }

        //[DllImport("user32.dll")]
        //private static extern bool SetForegroundWindow(IntPtr hWnd);

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
