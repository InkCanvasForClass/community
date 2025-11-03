using iNKORE.UI.WPF.Modern;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Application = System.Windows.Application;
using ui = iNKORE.UI.WPF.Modern.Controls;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        private Color FloatBarForegroundColor;

        private void SetTheme(string theme, bool autoSwitchIcon = false)
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

                // 刷新快速面板图标
                RefreshQuickPanelIcons();

                // 刷新墨迹选中栏图标
                RefreshStrokeSelectionIcons();

                // 刷新图片选中栏图标
                RefreshImageSelectionIcons();

                // 刷新手势按钮图标
                RefreshGestureButtonIcon();

                RefreshFloatingBarHighlightColors();

                if (autoSwitchIcon)
                {
                    AutoSwitchFloatingBarIconForTheme("Light");
                }

                // 强制刷新UI
                window.InvalidateVisual();

                // 通知其他窗口刷新主题
                RefreshOtherWindowsTheme();
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

                // 刷新快速面板图标
                RefreshQuickPanelIcons();

                // 刷新墨迹选中栏图标
                RefreshStrokeSelectionIcons();

                // 刷新图片选中栏图标
                RefreshImageSelectionIcons();

                // 刷新手势按钮图标
                RefreshGestureButtonIcon();

                RefreshFloatingBarHighlightColors();

                if (autoSwitchIcon)
                {
                    AutoSwitchFloatingBarIconForTheme("Dark");
                }

                // 强制刷新UI
                window.InvalidateVisual();

                // 通知其他窗口刷新主题
                RefreshOtherWindowsTheme();
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
        /// 刷新快速面板图标
        /// </summary>
        private void RefreshQuickPanelIcons()
        {
            try
            {
                if (LeftUnFoldButtonQuickPanel != null)
                {
                    LeftUnFoldButtonQuickPanel.InvalidateVisual();
                }
                if (RightUnFoldButtonQuickPanel != null)
                {
                    RightUnFoldButtonQuickPanel.InvalidateVisual();
                }
                if (LeftSidePanel != null)
                {
                    LeftSidePanel.InvalidateVisual();
                }
                if (RightSidePanel != null)
                {
                    RightSidePanel.InvalidateVisual();
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 刷新浮动栏高光条颜色
        /// </summary>
        private void RefreshFloatingBarHighlightColors()
        {
            try
            {
                if (FloatingbarSelectionBG != null && FloatingbarSelectionBG.Visibility == Visibility.Visible)
                {
                    // 根据主题设置高光颜色
                    Color highlightBackgroundColor;
                    Color highlightBarColor;
                    bool isDarkTheme = Settings.Appearance.Theme == 1 ||
                                       (Settings.Appearance.Theme == 2 && !IsSystemThemeLight());

                    if (isDarkTheme)
                    {
                        highlightBackgroundColor = Color.FromArgb(21, 102, 204, 255);
                        highlightBarColor = Color.FromRgb(102, 204, 255);
                    }
                    else
                    {
                        highlightBackgroundColor = Color.FromArgb(21, 59, 130, 246);
                        highlightBarColor = Color.FromRgb(37, 99, 235);
                    }

                    // 设置高光背景颜色
                    FloatingbarSelectionBG.Background = new SolidColorBrush(highlightBackgroundColor);
                    if (FloatingbarSelectionBG.Child is System.Windows.Controls.Canvas canvas && canvas.Children.Count > 0)
                    {
                        var firstChild = canvas.Children[0];
                        if (firstChild is Border innerBorder)
                        {
                            innerBorder.Background = new SolidColorBrush(highlightBarColor);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 刷新浮动工具栏按钮颜色
        /// </summary>
        private void RefreshFloatingBarButtonColors()
        {
            try
            {
                // 根据主题选择高光颜色
                Color selectedColor;
                bool isDarkTheme = Settings.Appearance.Theme == 1 ||
                                   (Settings.Appearance.Theme == 2 && !IsSystemThemeLight());

                if (isDarkTheme)
                {
                    selectedColor = Color.FromRgb(102, 204, 255);
                }
                else
                {
                    selectedColor = Color.FromRgb(30, 58, 138);
                }

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

        /// <summary>
        /// 根据主题自动切换浮动栏图标
        /// </summary>
        private void AutoSwitchFloatingBarIconForTheme(string theme)
        {
            try
            {
                if (theme == "Light")
                {
                    Settings.Appearance.FloatingBarImg = 0;
                }
                else if (theme == "Dark")
                {
                    Settings.Appearance.FloatingBarImg = 3;
                }

                UpdateFloatingBarIcon();
                UpdateFloatingBarIconComboBox();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 更新设置界面中的浮动栏图标选择下拉框显示
        /// </summary>
        private void UpdateFloatingBarIconComboBox()
        {
            try
            {
                if (ComboBoxFloatingBarImg != null)
                {
                    ComboBoxFloatingBarImg.SelectedIndex = Settings.Appearance.FloatingBarImg;
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 刷新墨迹选中栏图标
        /// </summary>
        private void RefreshStrokeSelectionIcons()
        {
            try
            {
                if (BorderStrokeSelectionControl != null)
                {
                    // 强制刷新墨迹选中栏的视觉状态
                    BorderStrokeSelectionControl.InvalidateVisual();

                    // 刷新墨迹选中栏内的所有图标
                    var viewbox = BorderStrokeSelectionControl.Child as Viewbox;
                    if (viewbox?.Child is ui.SimpleStackPanel stackPanel)
                    {
                        RefreshStrokeSelectionIconsRecursive(stackPanel);
                    }
                }
            }
            catch (Exception)
            {
                // 忽略异常，确保主题切换不会因为图标刷新失败而中断
            }
        }

        /// <summary>
        /// 递归刷新墨迹选中栏内的图标
        /// </summary>
        private void RefreshStrokeSelectionIconsRecursive(System.Windows.Controls.Panel panel)
        {
            try
            {
                foreach (var child in panel.Children)
                {
                    if (child is Image image)
                    {
                        // 强制刷新图像
                        image.InvalidateVisual();
                    }
                    else if (child is System.Windows.Controls.Panel childPanel)
                    {
                        // 递归处理子面板
                        RefreshStrokeSelectionIconsRecursive(childPanel);
                    }
                    else if (child is Border border && border.Child is System.Windows.Controls.Panel borderPanel)
                    {
                        // 处理Border内的面板
                        RefreshStrokeSelectionIconsRecursive(borderPanel);
                    }
                }
            }
            catch (Exception)
            {
                // 忽略异常
            }
        }

        /// <summary>
        /// 刷新图片选中栏图标
        /// </summary>
        private void RefreshImageSelectionIcons()
        {
            try
            {
                if (BorderImageSelectionControl != null)
                {
                    // 强制刷新图片选中栏的视觉状态
                    BorderImageSelectionControl.InvalidateVisual();

                    // 刷新图片选中栏内的所有图标
                    var viewbox = BorderImageSelectionControl.Child as Viewbox;
                    if (viewbox?.Child is ui.SimpleStackPanel stackPanel)
                    {
                        RefreshImageSelectionIconsRecursive(stackPanel);
                    }
                }
            }
            catch (Exception)
            {
                // 忽略异常，确保主题切换不会因为图标刷新失败而中断
            }
        }

        /// <summary>
        /// 递归刷新图片选中栏内的图标
        /// </summary>
        private void RefreshImageSelectionIconsRecursive(System.Windows.Controls.Panel panel)
        {
            try
            {
                foreach (var child in panel.Children)
                {
                    if (child is Image image)
                    {
                        // 强制刷新图像
                        image.InvalidateVisual();
                    }
                    else if (child is System.Windows.Controls.Panel childPanel)
                    {
                        // 递归处理子面板
                        RefreshImageSelectionIconsRecursive(childPanel);
                    }
                    else if (child is Border border && border.Child is System.Windows.Controls.Panel borderPanel)
                    {
                        // 处理Border内的面板
                        RefreshImageSelectionIconsRecursive(borderPanel);
                    }
                    else if (child is Grid grid)
                    {
                        // 处理Grid内的子元素
                        foreach (var gridChild in grid.Children)
                        {
                            if (gridChild is Image gridImage)
                            {
                                gridImage.InvalidateVisual();
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 忽略异常
            }
        }

        /// <summary>
        /// 刷新手势按钮图标
        /// </summary>
        private void RefreshGestureButtonIcon()
        {
            try
            {
                // 调用手势按钮颜色和图标更新方法，该方法会根据当前主题和手势状态设置正确的图标
                CheckEnableTwoFingerGestureBtnColorPrompt();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 刷新其他窗口的主题
        /// </summary>
        private void RefreshOtherWindowsTheme()
        {
            try
            {
                // 刷新所有打开的窗口
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is CountdownTimerWindow timerWindow)
                    {
                        timerWindow.RefreshTheme();
                    }
                    else if (window is RandWindow randWindow)
                    {
                        randWindow.RefreshTheme();
                    }
                    else if (window is OperatingGuideWindow operatingGuideWindow)
                    {
                        operatingGuideWindow.RefreshTheme();
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}