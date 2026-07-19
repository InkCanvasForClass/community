using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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

        private void RefreshThemes()
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
                    ThemeStrings.Theme_FloatingBarThemesTitle);
            }
        }

        private void ButtonReloadThemes_Click(object sender, RoutedEventArgs e)
        {
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
