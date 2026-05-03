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
            WindowBackdropHelper.Apply(this);
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
                    var path = App.RootPath + "Names.txt";
                    ProcessProtectionManager.WithWriteAccess(path, () =>
                    {
                        File.WriteAllText(path, TextBoxNames.Text);
                    });
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
            ThemeHelper.ApplyTheme(this, settings, ApplyThemeResources);
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

    }
}
