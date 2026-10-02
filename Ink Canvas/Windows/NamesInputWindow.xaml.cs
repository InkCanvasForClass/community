using Ink_Canvas.Helpers;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using MessageBox = System.Windows.MessageBox;

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
            WindowBackdropHelper.RegisterForThemeSync(this);
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
                var result = MessageBox.Show(Properties.RandomStrings.Random_NamesInput_SaveConfirm, Properties.RandomStrings.Random_NamesInput_Title, MessageBoxButton.YesNo);
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
            ThemeHelper.ApplyTheme(this, settings, _ =>
            {
                // 颜色全部走 WPF-UI 原生资源键（DynamicResource），主题字典替换后自动刷新，
                // 这里无需再按深浅色手动覆盖任何画刷。
            });
        }

    }
}
