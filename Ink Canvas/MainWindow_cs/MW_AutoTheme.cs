using Microsoft.Win32;
using iNKORE.UI.WPF.Modern;
using System;
using System.Windows;
using System.Windows.Media;
using Application = System.Windows.Application;

namespace Ink_Canvas {
    public partial class MainWindow : Window {
        private Color FloatBarForegroundColor = Color.FromRgb(102, 102, 102);

        private void SetTheme(string theme) {
            if (theme == "Light") {
                var rd1 = new ResourceDictionary()
                    { Source = new Uri("Resources/Styles/Light.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(rd1);

                var rd2 = new ResourceDictionary()
                    { Source = new Uri("Resources/DrawShapeImageDictionary.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(rd2);

                var rd3 = new ResourceDictionary()
                    { Source = new Uri("Resources/SeewoImageDictionary.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(rd3);

                var rd4 = new ResourceDictionary()
                    { Source = new Uri("Resources/IconImageDictionary.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(rd4);

                ThemeManager.SetRequestedTheme(window, ElementTheme.Light);

                FloatBarForegroundColor = (Color)Application.Current.FindResource("FloatBarForegroundColor");
            }
            else if (theme == "Dark") {
                var rd1 = new ResourceDictionary() { Source = new Uri("Resources/Styles/Dark.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(rd1);

                var rd2 = new ResourceDictionary()
                    { Source = new Uri("Resources/DrawShapeImageDictionary.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(rd2);

                var rd3 = new ResourceDictionary()
                    { Source = new Uri("Resources/SeewoImageDictionary.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(rd3);

                var rd4 = new ResourceDictionary()
                    { Source = new Uri("Resources/IconImageDictionary.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(rd4);

                ThemeManager.SetRequestedTheme(window, ElementTheme.Dark);

                FloatBarForegroundColor = (Color)Application.Current.FindResource("FloatBarForegroundColor");
            }
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) {
            switch (Settings.Appearance.Theme) {
                case 0:
                    SetTheme("Light");
                    break;
                case 1:
                    SetTheme("Dark");
                    break;
                case 2:
                    if (IsSystemThemeLight()) SetTheme("Light");
                    else SetTheme("Dark");
                    break;
            }
        }

        private bool IsSystemThemeLight() {
            var light = false;
            try {
                var registryKey = Registry.CurrentUser;
                var themeKey =
                    registryKey.OpenSubKey("software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
                var keyValue = 0;
                if (themeKey != null) keyValue = (int)themeKey.GetValue("SystemUsesLightTheme");
                if (keyValue == 1) light = true;
            }
            catch { }

            return light;
        }
    }
}