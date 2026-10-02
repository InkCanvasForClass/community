using Ink_Canvas.Helpers;
using WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType;
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
            Topmost = true;
            AnimationsHelper.ShowWithSlideFromBottomAndFade(this, 0.25);
            // 先应用主题再应用背景：WPF-UI 的主题切换会连带移除窗口背景，顺序反了会丢背景
            ApplyTheme();
            WindowBackdropHelper.Apply(this);
            LogHelper.WriteLogToFile("[Crash] 崩溃详情窗口已创建", LogHelper.LogType.Info);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LogHelper.WriteLogToFile("[Crash] 崩溃详情窗口已显示", LogHelper.LogType.Info);
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
                LogHelper.WriteLogToFile("[Crash] 用户已复制崩溃详情", LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"复制崩溃详情失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            LogHelper.WriteLogToFile("[Crash] 用户关闭崩溃详情窗口", LogHelper.LogType.Info);
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
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[Crash] 崩溃窗口读取系统主题注册表项失败: {ex.Message}", LogHelper.LogType.Info);
            }
            return true;
        }
    }
}
