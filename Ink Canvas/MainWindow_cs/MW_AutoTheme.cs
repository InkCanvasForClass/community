using Ink_Canvas.Controls;
using Ink_Canvas.Controls.Toolbar.FloatingToolbar;
using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
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

                if (FloatingBarRootPanel == null) return;
                foreach (var border in FloatingBarRootPanel.Children.OfType<Border>())
                {
                    if (border.Tag as string == ToolbarRegistry.ContentBorderTag && border.Child is Grid grid)
                    {
                        foreach (var gridChild in grid.Children.OfType<System.Windows.Controls.Canvas>())
                        {
                            if (gridChild.Tag as string == ToolbarRegistry.SelectionCanvasTag)
                            {
                                foreach (var canvasChild in gridChild.Children.OfType<Border>())
                                {
                                    if (canvasChild.Tag as string == ToolbarRegistry.SelectionBGTag && canvasChild.Visibility == Visibility.Visible)
                                    {
                                        canvasChild.Background = new SolidColorBrush(highlightBackgroundColor);
                                    }
                                    else if (canvasChild.Tag as string == ToolbarRegistry.IndicatorBarTag && canvasChild.Visibility == Visibility.Visible)
                                    {
                                        canvasChild.Background = new SolidColorBrush(highlightBarColor);
                                    }
                                }
                            }
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
                void SetToolbarGeometry(ToolbarImageButton btn, string geometry)
                {
                    if (btn != null) btn.Icon.Geometry = Geometry.Parse(geometry);
                }
                void SetMenuGeometry(ToolMenuButton btn, string geometry)
                {
                    if (btn != null) btn.Icon.Geometry = Geometry.Parse(geometry);
                }

                SetToolbarGeometry(SymbolIconDelete, XamlGraphicsIconGeometries.DeleteIcon);
                SetToolbarGeometry(ShapeDrawFloatingBarBtn, XamlGraphicsIconGeometries.ShapesIcon);
                SetToolbarGeometry(SymbolIconUndo, XamlGraphicsIconGeometries.UndoIcon);
                SetToolbarGeometry(SymbolIconRedo, XamlGraphicsIconGeometries.RedoIcon);
                SetToolbarGeometry(CursorWithDelFloatingBarBtn, XamlGraphicsIconGeometries.CursorWithDelFloatingBarBtnIcon);
                SetToolbarGeometry(WhiteboardFloatingBarBtn, XamlGraphicsIconGeometries.WhiteboardFloatingBarBtnIcon);
                SetToolbarGeometry(ToolsFloatingBarBtn, XamlGraphicsIconGeometries.ToolsFloatingBarBtnIcon);
                SetToolbarGeometry(Fold_Icon, XamlGraphicsIconGeometries.FoldIcon);
                SetToolbarGeometry(Gesture_Icon, XamlGraphicsIconGeometries.DisabledGestureIcon);
                SetToolbarGeometry(Exit_Icon, XamlGraphicsIconGeometries.ExitPresentationIconGeometry);
                UpdateInkFreezeButtonState();

                SetMenuGeometry(TimerToolBtn, XamlGraphicsIconGeometries.TimerIconGeometry);
                SetMenuGeometry(RandomDrawToolBtn, XamlGraphicsIconGeometries.RandomDrawIconGeometry);
                SetMenuGeometry(SingleDrawToolBtn, XamlGraphicsIconGeometries.SingleDrawIconGeometry);
                SetMenuGeometry(SaveToolBtn, XamlGraphicsIconGeometries.SaveIconGeometry);
                SetMenuGeometry(OpenToolBtn, XamlGraphicsIconGeometries.OpenIconGeometry);
                SetMenuGeometry(ReplayToolBtn, XamlGraphicsIconGeometries.ReplayIconGeometry);
                SetMenuGeometry(ScreenshotToolBtn, XamlGraphicsIconGeometries.ScreenshotIconGeometry);
                SetMenuGeometry(ShapeDrawToolBtn, XamlGraphicsIconGeometries.ShapesIcon);
                SetMenuGeometry(RedoToolBtn, XamlGraphicsIconGeometries.RedoIcon);
                SetMenuGeometry(ManualToolBtn, XamlGraphicsIconGeometries.ManualIconGeometry);
                SetMenuGeometry(SettingsToolBtn, XamlGraphicsIconGeometries.SettingsIconGeometry);

                SetMenuGeometry(BoardTimerToolBtn, XamlGraphicsIconGeometries.TimerIconGeometry);
                SetMenuGeometry(BoardRandomDrawToolBtn, XamlGraphicsIconGeometries.RandomDrawIconGeometry);
                SetMenuGeometry(BoardSingleDrawToolBtn, XamlGraphicsIconGeometries.SingleDrawIconGeometry);
                SetMenuGeometry(BoardSaveToolBtn, XamlGraphicsIconGeometries.SaveIconGeometry);
                SetMenuGeometry(BoardOpenToolBtn, XamlGraphicsIconGeometries.OpenIconGeometry);
                SetMenuGeometry(BoardReplayToolBtn, XamlGraphicsIconGeometries.ReplayIconGeometry);
                SetMenuGeometry(BoardScreenshotToolBtn, XamlGraphicsIconGeometries.ScreenshotIconGeometry);
                SetMenuGeometry(BoardShapeDrawToolBtn, XamlGraphicsIconGeometries.ShapesIcon);
                SetMenuGeometry(BoardRedoToolBtn, XamlGraphicsIconGeometries.RedoIcon);
                SetMenuGeometry(BoardManualToolBtn, XamlGraphicsIconGeometries.ManualIconGeometry);
                SetMenuGeometry(BoardSettingsToolBtn, XamlGraphicsIconGeometries.SettingsIconGeometry);

                bool isDarkTheme = IsCurrentThemeDark();
                Color selectedColor = isDarkTheme ? Color.FromRgb(102, 204, 255) : Color.FromRgb(30, 58, 138);

                SetAllFloatingBarButtonsToColor(FloatBarForegroundColor);

                switch (_currentToolMode)
                {
                    case "cursor":
                        if (Cursor_Icon != null) Cursor_Icon.Icon.Brush = new SolidColorBrush(selectedColor);
                        break;
                    case "pen":
                    case "color":
                        if (Pen_Icon != null) Pen_Icon.Icon.Brush = new SolidColorBrush(selectedColor);
                        break;
                    case "eraser":
                        if (Eraser_Icon != null) Eraser_Icon.Icon.Brush = new SolidColorBrush(selectedColor);
                        break;
                    case "eraserByStrokes":
                        if (EraserByStrokes_Icon != null) EraserByStrokes_Icon.Icon.Brush = new SolidColorBrush(selectedColor);
                        break;
                    case "select":
                        if (SymbolIconSelect != null) SymbolIconSelect.Icon.Brush = new SolidColorBrush(selectedColor);
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
            if (Cursor_Icon != null) Cursor_Icon.Icon.Brush = brush;
            if (Pen_Icon != null) Pen_Icon.Icon.Brush = brush;
            if (EraserByStrokes_Icon != null) EraserByStrokes_Icon.Icon.Brush = brush;
            if (Eraser_Icon != null) Eraser_Icon.Icon.Brush = brush;
            if (SymbolIconSelect != null) SymbolIconSelect.Icon.Brush = brush;
            if (ShapeDrawFloatingBarBtn != null) ShapeDrawFloatingBarBtn.Icon.Brush = brush;
            if (SymbolIconUndo != null) SymbolIconUndo.Icon.Brush = brush;
            if (SymbolIconRedo != null) SymbolIconRedo.Icon.Brush = brush;
            if (CursorWithDelFloatingBarBtn != null && !ToolbarRegistry.GetUseRedStyle(CursorWithDelFloatingBarBtn)) CursorWithDelFloatingBarBtn.Icon.Brush = brush;
            if (WhiteboardFloatingBarBtn != null) WhiteboardFloatingBarBtn.Icon.Brush = brush;
            if (ToolsFloatingBarBtn != null) ToolsFloatingBarBtn.Icon.Brush = brush;
            if (Fold_Icon != null) Fold_Icon.Icon.Brush = brush;
            if (Freeze_Icon != null) Freeze_Icon.Icon.Brush = brush;
            if (Gesture_Icon != null) Gesture_Icon.Icon.Brush = brush;
            if (Exit_Icon != null) Exit_Icon.Icon.Brush = brush;
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

                DynamicNotification?.RefreshTheme(IsCurrentThemeDark());
            }
            catch (Exception)
            {
            }
        }
    }
}
