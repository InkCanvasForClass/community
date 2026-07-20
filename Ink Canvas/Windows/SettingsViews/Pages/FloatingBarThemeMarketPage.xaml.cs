using Ink_Canvas.Helpers;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class FloatingBarThemeMarketPage : Page
    {
        private readonly FloatingBarThemeMarketService _market = new FloatingBarThemeMarketService();

        public FloatingBarThemeMarketPage()
        {
            InitializeComponent();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e) => await RefreshAsync();

        private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            LoadingBar.Visibility = Visibility.Visible;
            try
            {
                if (await _market.RefreshAsync()) ThemeList.ItemsSource = _market.Entries;
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"FloatingBarThemeMarketPage | Refresh failed: {ex}");
            }
            finally
            {
                LoadingBar.Visibility = Visibility.Collapsed;
            }
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not ThemeMarketEntry entry) return;
            button.IsEnabled = false;
            try
            {
                var installed = await _market.InstallAsync(entry);
                if (installed)
                {
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    mainWindow?.FloatingBarThemeService?.LoadThemes();
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"FloatingBarThemeMarketPage | Install failed: {ex}");
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var path = Path.Combine(App.RootPath, "FloatingBarThemes");
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
    }
}
