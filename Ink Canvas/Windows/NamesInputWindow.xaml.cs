using Ink_Canvas.Helpers;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace Ink_Canvas
{
    /// <summary>
    /// Interaction logic for NamesInputWindow.xaml
    /// </summary>
    public partial class NamesInputWindow : Window
    {
        public NamesInputWindow()
        {
            InitializeComponent();
            AnimationsHelper.ShowWithSlideFromBottomAndFade(this, 0.25);
            ApplyTheme();
        }

        string originText = "";

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (File.Exists(App.RootPath + "Names.txt"))
            {
                TextBoxNames.Text = File.ReadAllText(App.RootPath + "Names.txt");
                originText = TextBoxNames.Text;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (originText != TextBoxNames.Text)
            {
                var result = MessageBox.Show("是否保存？", "名单导入", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    File.WriteAllText(App.RootPath + "Names.txt", TextBoxNames.Text);
                }
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
                LogHelper.WriteLogToFile($"应用名单导入窗口主题出错: {ex.Message}", LogHelper.LogType.Error);
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
                LogHelper.WriteLogToFile($"应用名单导入窗口主题出错: {ex.Message}", LogHelper.LogType.Error);
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
                    resources["NamesInputWindowBackground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    resources["NamesInputWindowForeground"] = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    resources["NamesInputWindowButtonBackground"] = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                    resources["NamesInputWindowButtonForeground"] = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    resources["NamesInputWindowBorderBrush"] = new SolidColorBrush(Color.FromRgb(228, 228, 231));
                }
                else
                {
                    // 应用深色主题资源 - 与新计时器窗口统一
                    resources["NamesInputWindowBackground"] = new SolidColorBrush(Color.FromRgb(31, 31, 31)); // #1f1f1f
                    resources["NamesInputWindowForeground"] = new SolidColorBrush(Colors.White);
                    resources["NamesInputWindowButtonBackground"] = new SolidColorBrush(Color.FromRgb(42, 42, 42)); // #2a2a2a
                    resources["NamesInputWindowButtonForeground"] = new SolidColorBrush(Colors.White);
                    resources["NamesInputWindowBorderBrush"] = new SolidColorBrush(Color.FromRgb(224, 224, 224)); // #E0E0E0
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用名单导入窗口主题资源出错: {ex.Message}", LogHelper.LogType.Error);
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
