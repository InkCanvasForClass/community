using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Application = System.Windows.Application;
using ui = iNKORE.UI.WPF.Controls;

namespace Ink_Canvas
{
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        private const string ThemeLight = "Light";
        private const string ThemeDark = "Dark";
        private const string LightThemePath = "Resources/Styles/Light.xaml";
        private const string DarkThemePath = "Resources/Styles/Dark.xaml";
        private const string DrawShapeImagePath = "Resources/DrawShapeImageDictionary.xaml";
        private const string SeewoImagePath = "Resources/SeewoImageDictionary.xaml";
        private const string IconImagePath = "Resources/IconImageDictionary.xaml";

        private Color FloatBarForegroundColor;

        private void SetTheme(string theme, bool autoSwitchIcon = false)
        {
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

            var isLightTheme = theme == ThemeLight;
            var themePath = isLightTheme ? LightThemePath : DarkThemePath;
            var elementTheme = isLightTheme ? ElementTheme.Light : ElementTheme.Dark;

            var rd1 = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(rd1);

            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                Dispatcher.Invoke(() =>
                {
                    LoadImageResourceDictionary(DrawShapeImagePath);
                    LoadImageResourceDictionary(SeewoImagePath);
                    LoadImageResourceDictionary(IconImagePath);
                });
            });

            ThemeManager.SetRequestedTheme(this, elementTheme);

            InitializeFloatBarForegroundColor();
            RefreshQuickPanelIcons();
            RefreshStrokeSelectionIcons();
            RefreshImageSelectionIcons();
            RefreshGestureButtonIcon();
            RefreshFloatingBarHighlightColors();

            if (autoSwitchIcon)
            {
                AutoSwitchFloatingBarIconForTheme(theme);
            }

            InvalidateVisual();
            RefreshOtherWindowsTheme();
        }

        void LoadImageResourceDictionary(string path)
        {
            var rd = new ResourceDictionary { Source = new Uri(path, UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(rd);
        }

        private void InitializeFloatBarForegroundColor()
        {
            try
            {
                FloatBarForegroundColor = (Color)Application.Current.FindResource("FloatBarForegroundColor");
                RefreshFloatingBarButtonColors();
            }
            catch (Exception)
            {
                FloatBarForegroundColor = Color.FromRgb(0, 0, 0);
            }
        }

        private void RefreshQuickPanelIcons()
        {
            try
            {
                LeftUnFoldButtonQuickPanel?.InvalidateVisual();
                RightUnFoldButtonQuickPanel?.InvalidateVisual();
                LeftSidePanel?.InvalidateVisual();
                RightSidePanel?.InvalidateVisual();
            }
            catch (Exception)
            {
            }
        }

        private void RefreshFloatingBarHighlightColors()
        {
            try
            {
                if (FloatingbarSelectionBG != null && FloatingbarSelectionBG.Visibility == Visibility.Visible)
                {
                    bool isDarkTheme = IsCurrentThemeDark();

                    Color highlightBackgroundColor;
                    Color highlightBarColor;

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

        private bool IsCurrentThemeDark()
        {
            return Settings.Appearance.Theme == 1 ||
                   (Settings.Appearance.Theme == 2 && !ThemeHelper.IsSystemThemeLight());
        }

        private void RefreshFloatingBarButtonColors()
        {
            try
            {
                SymbolIconDelete.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.DeleteIcon);
                ShapeDrawFloatingBarBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.ShapesIcon);
                SymbolIconUndo.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.UndoIcon);
                SymbolIconRedo.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.RedoIcon);
                CursorWithDelFloatingBarBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.CursorWithDelFloatingBarBtnIcon);
                WhiteboardFloatingBarBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.WhiteboardFloatingBarBtnIcon);
                ToolsFloatingBarBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.ToolsFloatingBarBtnIcon);
                Fold_Icon.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.FoldIcon);
                UpdateInkFreezeButtonState();

                TimerToolBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.TimerIconGeometry);
                RandomDrawToolBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.RandomDrawIconGeometry);
                SingleDrawToolBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.SingleDrawIconGeometry);
                SaveToolBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.SaveIconGeometry);
                OpenToolBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.OpenIconGeometry);
                ReplayToolBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.ReplayIconGeometry);
                ScreenshotToolBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.ScreenshotIconGeometry);
                ManualToolBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.ManualIconGeometry);
                SettingsToolBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.SettingsIconGeometry);

                BoardTimerToolBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.TimerIconGeometry);
                BoardRandomDrawToolBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.RandomDrawIconGeometry);
                BoardSingleDrawToolBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.SingleDrawIconGeometry);
                BoardSaveToolBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.SaveIconGeometry);
                BoardOpenToolBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.OpenIconGeometry);
                BoardReplayToolBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.ReplayIconGeometry);
                BoardScreenshotToolBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.ScreenshotIconGeometry);
                BoardManualToolBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.ManualIconGeometry);
                BoardSettingsToolBtn.Icon.Geometry = Geometry.Parse(XamlGraphicsIconGeometries.SettingsIconGeometry);

                bool isDarkTheme = IsCurrentThemeDark();
                Color selectedColor = isDarkTheme ? Color.FromRgb(102, 204, 255) : Color.FromRgb(30, 58, 138);

                SetAllFloatingBarButtonsToColor(FloatBarForegroundColor);

                switch (_currentToolMode)
                {
                    case "cursor":
                        Cursor_Icon.Icon.Brush = new SolidColorBrush(selectedColor);
                        break;
                    case "pen":
                    case "color":
                        Pen_Icon.Icon.Brush = new SolidColorBrush(selectedColor);
                        break;
                    case "eraser":
                        Eraser_Icon.Icon.Brush = new SolidColorBrush(selectedColor);
                        break;
                    case "eraserByStrokes":
                        EraserByStrokes_Icon.Icon.Brush = new SolidColorBrush(selectedColor);
                        break;
                    case "select":
                        SymbolIconSelect.Icon.Brush = new SolidColorBrush(selectedColor);
                        break;
                }
            }
            catch (Exception)
            {
            }
        }

        void SetAllFloatingBarButtonsToColor(Color color)
        {
            var brush = new SolidColorBrush(color);
            Cursor_Icon.Icon.Brush = brush;
            Pen_Icon.Icon.Brush = brush;
            EraserByStrokes_Icon.Icon.Brush = brush;
            Eraser_Icon.Icon.Brush = brush;
            SymbolIconSelect.Icon.Brush = brush;
            ShapeDrawFloatingBarBtn.Icon.Brush = brush;
            SymbolIconUndo.Icon.Brush = brush;
            SymbolIconRedo.Icon.Brush = brush;
            CursorWithDelFloatingBarBtn.Icon.Brush = brush;
            WhiteboardFloatingBarBtn.Icon.Brush = brush;
            ToolsFloatingBarBtn.Icon.Brush = brush;
            Fold_Icon.Icon.Brush = brush;
            if (Freeze_Icon != null) Freeze_Icon.Icon.Brush = brush;
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            switch (Settings.Appearance.Theme)
            {
                case 0:
                    SetTheme(ThemeLight);
                    break;
                case 1:
                    SetTheme(ThemeDark);
                    break;
                case 2:
                    // 与 IsCurrentThemeDark / GetEffectiveTheme / 浮动栏一致，统一读 AppsUseLightTheme，
                    // 否则 SystemUsesLightTheme 与 AppsUseLightTheme 可独立取值时主题会混搭
                    SetTheme(ThemeHelper.IsSystemThemeLight() ? ThemeLight : ThemeDark);
                    break;
            }
        }

        private void AutoSwitchFloatingBarIconForTheme(string theme)
        {
            try
            {
                Settings.Appearance.FloatingBarImg = theme == ThemeLight ? 0 : 3;
                UpdateFloatingBarIcon();
                UpdateFloatingBarIconComboBox();
            }
            catch (Exception)
            {
            }
        }

        private void UpdateFloatingBarIconComboBox()
        {
        }

        private void RefreshStrokeSelectionIcons()
        {
            try
            {
                if (BorderStrokeSelectionControl != null)
                {
                    BorderStrokeSelectionControl.InvalidateVisual();
                    var viewbox = BorderStrokeSelectionControl.Child as Viewbox;
                    if (viewbox?.Child is ui.SimpleStackPanel stackPanel)
                    {
                        RefreshIconsRecursive(stackPanel);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void RefreshImageSelectionIcons()
        {
            try
            {
                if (BorderImageSelectionControl != null)
                {
                    BorderImageSelectionControl.InvalidateVisual();
                    var viewbox = BorderImageSelectionControl.Child as Viewbox;
                    if (viewbox?.Child is ui.SimpleStackPanel stackPanel)
                    {
                        RefreshIconsRecursive(stackPanel);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void RefreshIconsRecursive(System.Windows.Controls.Panel panel)
        {
            try
            {
                foreach (var child in panel.Children)
                {
                    if (child is Image image)
                    {
                        image.InvalidateVisual();
                    }
                    else if (child is System.Windows.Controls.Panel childPanel)
                    {
                        RefreshIconsRecursive(childPanel);
                    }
                    else if (child is Border border && border.Child is System.Windows.Controls.Panel borderPanel)
                    {
                        RefreshIconsRecursive(borderPanel);
                    }
                    else if (child is Grid grid)
                    {
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
            }
        }

        private void RefreshGestureButtonIcon()
        {
            try
            {
                CheckEnableTwoFingerGestureBtnColorPrompt();
            }
            catch (Exception)
            {
            }
        }

        private void RefreshOtherWindowsTheme()
        {
            try
            {
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
                    else if (window is QuickDrawWindow quickDrawWindow)
                    {
                        quickDrawWindow.RefreshTheme();
                    }
                    else if (window is OperatingGuideWindow operatingGuideWindow)
                    {
                        operatingGuideWindow.RefreshTheme();
                    }
                    else if (window is Windows.SettingsViews.SettingsWindow settingsWindow)
                    {
                        settingsWindow.RefreshTheme();
                    }
                }

                TimerControl?.RefreshTheme();
                MinimizedTimerControl?.RefreshTheme();
            }
            catch (Exception)
            {
            }
        }
    }
}
