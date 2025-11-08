using Ink_Canvas.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Newtonsoft.Json;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace Ink_Canvas
{
    /// <summary>
    /// Interaction logic for RollCallHistoryWindow.xaml
    /// </summary>
    public partial class RollCallHistoryWindow : Window
    {
        public RollCallHistoryWindow()
        {
            InitializeComponent();
            AnimationsHelper.ShowWithSlideFromBottomAndFade(this, 0.25);
            ApplyTheme();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadHistory();
        }

        private void LoadHistory()
        {
            try
            {
                string configsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs");
                string historyJsonPath = System.IO.Path.Combine(configsFolder, "RollCallHistory.json");

                if (!File.Exists(historyJsonPath))
                {
                    TextBoxHistory.Text = "暂无历史记录";
                    return;
                }

                string jsonContent = File.ReadAllText(historyJsonPath);
                var historyData = JsonConvert.DeserializeObject<RollCallHistoryData>(jsonContent);

                if (historyData == null || historyData.History == null || historyData.History.Count == 0)
                {
                    TextBoxHistory.Text = "暂无历史记录";
                    return;
                }

                // 按时间倒序显示（最新的在上方）
                // 由于历史记录是按时间顺序添加的，所以直接反转即可
                var reversedHistory = historyData.History.ToList();
                reversedHistory.Reverse();

                // 显示历史记录，每行一个
                TextBoxHistory.Text = string.Join(Environment.NewLine, reversedHistory);

                // 显示统计信息
                int totalCount = historyData.History.Count;
                string lastUpdate = historyData.LastUpdate.ToString("yyyy-MM-dd HH:mm:ss");
                string header = $"共 {totalCount} 条记录，最后更新：{lastUpdate}\n\n";
                TextBoxHistory.Text = header + TextBoxHistory.Text;
            }
            catch (Exception ex)
            {
                TextBoxHistory.Text = $"加载历史记录失败: {ex.Message}";
                LogHelper.WriteLogToFile($"加载点名历史记录失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
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
                LogHelper.WriteLogToFile($"应用历史记录窗口主题出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ApplyTheme(Settings settings)
        {
            try
            {
                if (settings.Appearance.Theme == 0) // 浅色主题
                {
                    iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, iNKORE.UI.WPF.Modern.ElementTheme.Light);
                    ApplyThemeResources("Light");
                }
                else if (settings.Appearance.Theme == 1) // 深色主题
                {
                    iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, iNKORE.UI.WPF.Modern.ElementTheme.Dark);
                    ApplyThemeResources("Dark");
                }
                else // 跟随系统主题
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
                LogHelper.WriteLogToFile($"应用历史记录窗口主题出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ApplyThemeResources(string theme)
        {
            try
            {
                var resources = this.Resources;
                
                if (theme == "Light")
                {
                    // 应用浅色主题资源
                    resources["RollCallHistoryWindowBackground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    resources["RollCallHistoryWindowForeground"] = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    resources["RollCallHistoryWindowButtonBackground"] = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                    resources["RollCallHistoryWindowButtonForeground"] = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    resources["RollCallHistoryWindowBorderBrush"] = new SolidColorBrush(Color.FromRgb(228, 228, 231));
                }
                else
                {
                    // 应用深色主题资源
                    resources["RollCallHistoryWindowBackground"] = new SolidColorBrush(Color.FromRgb(31, 31, 31)); // #1f1f1f
                    resources["RollCallHistoryWindowForeground"] = new SolidColorBrush(Colors.White);
                    resources["RollCallHistoryWindowButtonBackground"] = new SolidColorBrush(Color.FromRgb(42, 42, 42)); // #2a2a2a
                    resources["RollCallHistoryWindowButtonForeground"] = new SolidColorBrush(Colors.White);
                    resources["RollCallHistoryWindowBorderBrush"] = new SolidColorBrush(Color.FromRgb(224, 224, 224)); // #E0E0E0
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用历史记录窗口主题资源出错: {ex.Message}", LogHelper.LogType.Error);
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
                // 如果无法读取注册表，默认使用浅色主题
                light = true;
            }
            return light;
        }
    }
}

