using iNKORE.UI.WPF.Modern;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
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

            if (theme == "Light")
            {
                var rd1 = new ResourceDictionary
                { Source = new Uri("Resources/Styles/Light.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(rd1);
                
                // 在主题资源之后添加其他资源
                var rd2 = new ResourceDictionary
                { Source = new Uri("Resources/DrawShapeImageDictionary.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(rd2);

                var rd3 = new ResourceDictionary
                { Source = new Uri("Resources/SeewoImageDictionary.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(rd3);

                var rd4 = new ResourceDictionary
                { Source = new Uri("Resources/IconImageDictionary.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(rd4);

                ThemeManager.SetRequestedTheme(window, ElementTheme.Light);

                InitializeFloatBarForegroundColor();
                
                // 强制刷新UI
                window.InvalidateVisual();
            }
            else if (theme == "Dark")
            {
                var rd1 = new ResourceDictionary { Source = new Uri("Resources/Styles/Dark.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(rd1);
                
                // 在主题资源之后添加其他资源
                var rd2 = new ResourceDictionary
                { Source = new Uri("Resources/DrawShapeImageDictionary.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(rd2);

                var rd3 = new ResourceDictionary
                { Source = new Uri("Resources/SeewoImageDictionary.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(rd3);

                var rd4 = new ResourceDictionary
                { Source = new Uri("Resources/IconImageDictionary.xaml", UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(rd4);

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
                
                // 强制刷新浮动工具栏按钮颜色
                RefreshFloatingBarButtonColors();
            }
            catch (Exception)
            {
                // 如果无法从资源中加载，使用默认颜色
                FloatBarForegroundColor = Color.FromRgb(0, 0, 0); 
            }
        }
        
        /// <summary>
        /// 刷新浮动工具栏按钮颜色
        /// </summary>
        private void RefreshFloatingBarButtonColors()
        {
            try
            {
                // 选中状态的颜色（蓝底）
                var selectedColor = Color.FromRgb(30, 58, 138);
                
                // 根据当前模式设置按钮颜色
                switch (_currentToolMode)
                {
                    case "cursor":
                        CursorIconGeometry.Brush = new SolidColorBrush(selectedColor);
                        PenIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        StrokeEraserIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        CircleEraserIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        LassoSelectIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        break;
                    case "pen":
                    case "color":
                        CursorIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        PenIconGeometry.Brush = new SolidColorBrush(selectedColor);
                        StrokeEraserIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        CircleEraserIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        LassoSelectIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        break;
                    case "eraser":
                        CursorIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        PenIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        StrokeEraserIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        CircleEraserIconGeometry.Brush = new SolidColorBrush(selectedColor);
                        LassoSelectIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        break;
                    case "eraserByStrokes":
                        CursorIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        PenIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        StrokeEraserIconGeometry.Brush = new SolidColorBrush(selectedColor);
                        CircleEraserIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        LassoSelectIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        break;
                    case "select":
                        CursorIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        PenIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        StrokeEraserIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        CircleEraserIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        LassoSelectIconGeometry.Brush = new SolidColorBrush(selectedColor);
                        break;
                    default:
                        // 默认情况，所有按钮都使用主题颜色
                        CursorIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        PenIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        StrokeEraserIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        CircleEraserIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        LassoSelectIconGeometry.Brush = new SolidColorBrush(FloatBarForegroundColor);
                        break;
                }
            }
            catch (Exception)
            {
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