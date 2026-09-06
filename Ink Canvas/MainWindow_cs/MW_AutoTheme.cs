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
                // 主题切换后浮动栏背景变化，重新评估批注图标描边是否需要
                UpdatePenIconColor();
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

                void SetSelectedFloatingBarButtonBrush(ToolbarImageButton btn)
                {
                    if (btn != null && !ToolbarRegistry.GetUseRedStyle(btn)) btn.Icon.Brush = new SolidColorBrush(selectedColor);
                }

                switch (_currentToolMode)
                {
                    case "cursor":
                        SetSelectedFloatingBarButtonBrush(Cursor_Icon);
                        break;
                    case "pen":
                    case "color":
                        SetSelectedFloatingBarButtonBrush(Pen_Icon);
                        break;
                    case "eraser":
                        SetSelectedFloatingBarButtonBrush(Eraser_Icon);
                        break;
                    case "eraserByStrokes":
                        SetSelectedFloatingBarButtonBrush(EraserByStrokes_Icon);
                        break;
                    case "select":
                        SetSelectedFloatingBarButtonBrush(SymbolIconSelect);
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

            void SetFloatingBarButtonBrush(ToolbarImageButton btn)
            {
                if (btn != null && !ToolbarRegistry.GetUseRedStyle(btn)) btn.Icon.Brush = brush;
            }

            SetFloatingBarButtonBrush(Cursor_Icon);
            SetFloatingBarButtonBrush(Pen_Icon);
            SetFloatingBarButtonBrush(EraserByStrokes_Icon);
            SetFloatingBarButtonBrush(Eraser_Icon);
            SetFloatingBarButtonBrush(SymbolIconSelect);
            SetFloatingBarButtonBrush(ShapeDrawFloatingBarBtn);
            SetFloatingBarButtonBrush(SymbolIconUndo);
            SetFloatingBarButtonBrush(SymbolIconRedo);
            SetFloatingBarButtonBrush(CursorWithDelFloatingBarBtn);
            SetFloatingBarButtonBrush(WhiteboardFloatingBarBtn);
            SetFloatingBarButtonBrush(ToolsFloatingBarBtn);
            SetFloatingBarButtonBrush(Fold_Icon);
            SetFloatingBarButtonBrush(Freeze_Icon);
            SetFloatingBarButtonBrush(Gesture_Icon);
            SetFloatingBarButtonBrush(Exit_Icon);
        }

        // 跟随系统模式下最近一次应用的系统深浅色；null 表示尚未建立基线
        private bool? _lastFollowedSystemThemeLight;
        private DispatcherTimer _systemThemeRetryTimer;
        private int _systemThemeRetryCount;
        private const int SystemThemeRetryLimit = 10;
        private static readonly TimeSpan SystemThemeRetryInterval = TimeSpan.FromMilliseconds(300);

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            // 只关心外观类偏好变化；键盘/鼠标/电源等类别与主题无关
            if (e.Category != UserPreferenceCategory.General) return;
            // SystemEvents 的事件不保证在 UI 线程触发，统一调度回 UI 线程
            Dispatcher.BeginInvoke(new Action(CheckSystemThemeSwitch));
        }

        /// <summary>
        /// 系统深浅色切换检测：AppsUseLightTheme 注册表的写入可能晚于系统广播，
        /// 首次读到旧值时延迟重试，确认真正翻转后再走完整主题管线。
        /// </summary>
        private void CheckSystemThemeSwitch()
        {
            // 仅「跟随系统」需要响应；固定主题重复应用反而会打断黑板/PPT 模式的深色覆盖
            if (Settings.Appearance.Theme != 2) return;

            bool systemLight = ThemeHelper.IsSystemThemeLight();

            if (_lastFollowedSystemThemeLight == null)
            {
                // 首次只建立基线，不重复应用当前主题
                _lastFollowedSystemThemeLight = systemLight;
                return;
            }

            if (systemLight == _lastFollowedSystemThemeLight)
            {
                // 广播先于注册表写入的情况：延迟重读，确认值确实没变再放弃
                StartSystemThemeRetry();
                return;
            }

            StopSystemThemeRetry();
            _lastFollowedSystemThemeLight = systemLight;
            ApplyFollowedSystemTheme(systemLight);
        }

        private void StartSystemThemeRetry()
        {
            if (_systemThemeRetryCount >= SystemThemeRetryLimit) return;

            _systemThemeRetryCount++;
            if (_systemThemeRetryTimer == null)
            {
                _systemThemeRetryTimer = new DispatcherTimer { Interval = SystemThemeRetryInterval };
                _systemThemeRetryTimer.Tick += (s, args) =>
                {
                    _systemThemeRetryTimer.Stop();
                    CheckSystemThemeSwitch();
                };
            }
            _systemThemeRetryTimer.Start();
        }

        private void StopSystemThemeRetry()
        {
            _systemThemeRetryTimer?.Stop();
            _systemThemeRetryCount = 0;
        }

        private void ApplyFollowedSystemTheme(bool systemLight)
        {
            // 应用级主题（App 级 ThemeResources / Default 主题控件与 Popup）必须与
            // 窗口级 RequestedTheme 同步翻转，否则切换后会出现深浅混搭
            ThemeManager.Current.ApplicationTheme = systemLight ? ApplicationTheme.Light : ApplicationTheme.Dark;
            SetTheme(systemLight ? ThemeLight : ThemeDark, autoSwitchIcon: true);
            ViewboxFloatingBar.Opacity = 1.0;
            RefreshNotificationColors();
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
            try
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is Ink_Canvas.Windows.SettingsViews.SettingsWindow)
                    {
                        var comboBox = FindChildByName<System.Windows.Controls.ComboBox>(window, "ComboBoxFloatingBarImg");
                        if (comboBox != null)
                        {
                            comboBox.SelectedIndex = Settings.Appearance.FloatingBarImg;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static T FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T fe && fe.Name == name)
                    return fe;
                var result = FindChildByName<T>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void RefreshStrokeSelectionIcons()
        {
            try
            {
                if (BorderStrokeSelectionControl != null)
                {
                    BorderStrokeSelectionControl.InvalidateVisual();
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
                if (isLoaded)
                {
                    CheckEnableTwoFingerGestureBtnColorPrompt();
                }
            }
            catch (Exception)
            {
            }
        }

        private void RefreshOtherWindowsTheme()
        {
            try
            {
                if (isLoaded)
                {
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window == this || window == null) continue;

                        ThemeManager.SetRequestedTheme(window, IsCurrentThemeDark() ? ElementTheme.Dark : ElementTheme.Light);
                        window.InvalidateVisual();
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
