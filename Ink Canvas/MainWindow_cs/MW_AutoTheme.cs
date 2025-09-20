using iNKORE.UI.WPF.Modern;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Ink_Canvas.Helpers;
using Application = System.Windows.Application;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        private Color FloatBarForegroundColor;

        private void SetTheme(string theme)
        {
            // 清理现有的主题资源
            var resourcesToRemove = new List<ResourceDictionary>();
            foreach (var dict in Application.Current.Resources.MergedDictionaries)
            {
                if (dict.Source != null && 
                    (dict.Source.ToString().Contains("Light.xaml") || 
                     dict.Source.ToString().Contains("Dark.xaml")))
                {
                    resourcesToRemove.Add(dict);
                }
            }
            
            foreach (var dict in resourcesToRemove)
            {
                Application.Current.Resources.MergedDictionaries.Remove(dict);
            }

            // 先添加其他资源
            var rd2 = new ResourceDictionary
            { Source = new Uri("Resources/DrawShapeImageDictionary.xaml", UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(rd2);

            var rd3 = new ResourceDictionary
            { Source = new Uri("Resources/SeewoImageDictionary.xaml", UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(rd3);

            var rd4 = new ResourceDictionary
            { Source = new Uri("Resources/IconImageDictionary.xaml", UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(rd4);

            if (theme == "Light")
            {
                var rd1 = new ResourceDictionary
                { Source = new Uri("Resources/Styles/Light.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(rd1);

                ThemeManager.SetRequestedTheme(window, ElementTheme.Light);

                InitializeFloatBarForegroundColor();
                
                // 强制刷新UI
                window.InvalidateVisual();
            }
            else if (theme == "Dark")
            {
                var rd1 = new ResourceDictionary { Source = new Uri("Resources/Styles/Dark.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(rd1);

                ThemeManager.SetRequestedTheme(window, ElementTheme.Dark);

                InitializeFloatBarForegroundColor();
                
                // 强制刷新UI
                window.InvalidateVisual();
            }
        }

        /// <summary>
        /// 初始化FloatBarForegroundColor，从当前主题资源中加载颜色
        /// </summary>
        private void InitializeFloatBarForegroundColor()
        {
            try
            {
                FloatBarForegroundColor = (Color)Application.Current.FindResource("FloatBarForegroundColor");
            }
            catch (Exception ex)
            {
                // 如果无法从资源中加载，使用默认颜色
                FloatBarForegroundColor = Color.FromRgb(0, 0, 0); 
                LogHelper.WriteLogToFile($"初始化FloatBarForegroundColor时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            switch (Settings.Appearance.Theme)
            {
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

        private bool IsSystemThemeLight()
        {
            var light = false;
            try
            {
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