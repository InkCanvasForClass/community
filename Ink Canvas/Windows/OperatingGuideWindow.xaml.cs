using Ink_Canvas.Helpers;
using System.Windows;
using System.Windows.Input;
using iNKORE.UI.WPF.Modern;
using System;

namespace Ink_Canvas
{
    /// <summary>
    /// Interaction logic for StopwatchWindow.xaml
    /// </summary>
    public partial class OperatingGuideWindow : Window
    {
        public OperatingGuideWindow()
        {
            InitializeComponent();
            AnimationsHelper.ShowWithSlideFromBottomAndFade(this, 0.25);
        }

        private void BtnClose_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Close();
        }

        private void WindowDragMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void BtnFullscreen_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                WindowState = WindowState.Maximized;
                SymbolIconFullscreen.Symbol = iNKORE.UI.WPF.Modern.Controls.Symbol.BackToWindow;
            }
            else
            {
                WindowState = WindowState.Normal;
                SymbolIconFullscreen.Symbol = iNKORE.UI.WPF.Modern.Controls.Symbol.FullScreen;
            }
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        /// <summary>
        /// 刷新主题
        /// </summary>
        public void RefreshTheme()
        {
            try
            {
                // 根据当前主题设置窗口主题
                bool isDarkTheme = MainWindow.Settings.Appearance.Theme == 1 || 
                                   (MainWindow.Settings.Appearance.Theme == 2 && !IsSystemThemeLight());
                
                if (isDarkTheme)
                {
                    ThemeManager.SetRequestedTheme(this, ElementTheme.Dark);
                }
                else
                {
                    ThemeManager.SetRequestedTheme(this, ElementTheme.Light);
                }

                // 强制刷新UI
                InvalidateVisual();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 检查系统主题是否为浅色
        /// </summary>
        private bool IsSystemThemeLight()
        {
            var light = false;
            try
            {
                var registryKey = Microsoft.Win32.Registry.CurrentUser;
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