using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class FloatingBarThemePage : Page
    {
        private FloatingBarThemeService ThemeService =>
            (Application.Current.MainWindow as MainWindow)?.FloatingBarThemeService;

        public FloatingBarThemePage()
        {
            InitializeComponent();
            Loaded += (_, __) => RefreshThemes();
        }

        // 对外公开以便在导航或外部操作后强制刷新
        public void RefreshThemes()
        {
            var service = ThemeService;
            if (service == null) return;
            service.LoadThemes();
            ThemeItemsControl.ItemsSource = service.Themes;
        }

        private void ButtonApplyTheme_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string themeId) return;
            var service = ThemeService;
            if (service == null || !service.ApplyTheme(themeId))
            {
                iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(
                    ThemeStrings.Theme_FloatingBarThemesApplyFailed,
                    ThemeStrings.Theme_FloatingBarThemeMarketTitle);
            }
            else
            {
                var result = MessageBox.Show(
                    ThemeStrings.GetString("Theme_RestartRequired"),
                    ThemeStrings.GetString("Theme_RestartPromptTitle"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    AppRestartHelper.RestartWithCurrentPrivileges();
                    return;
                }
            }
            RefreshThemes();
        }

        private void ButtonDeleteTheme_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string themeId) return;
            var service = ThemeService;
            if (service == null) return;
            var result = service.DeleteTheme(themeId);
            if (!result)
            {
                iNKORE.UI.WPF.Modern.Controls.MessageBox.Show("删除失败", ThemeStrings.Theme_FloatingBarThemeMarketTitle);
            }
            RefreshThemes();
        }

        private void ButtonOpenThemeFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = Path.Combine(App.RootPath, "FloatingBarThemes");
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
    }
}
