using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class ToolbarAppearancePage : Page
    {
        private bool _isLoaded = false;

        public ToolbarAppearancePage()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
            Unloaded += Page_Unloaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var mw = Application.Current.MainWindow as MainWindow;
            mw?.UpdateCustomIconsInComboBox();
            LoadSettings();
            _isLoaded = true;
            UpdateAllSliderTexts();
            SliderTouchHelper.AddTouchSupportToAllSliders(this);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings;
            if (settings?.Appearance == null) return;

            if (settings.Appearance.FloatingBarImg >= ComboBoxFloatingBarImg.Items.Count)
                settings.Appearance.FloatingBarImg = 0;
            ComboBoxFloatingBarImg.SelectedIndex = settings.Appearance.FloatingBarImg;

            if (settings.Appearance.ViewboxFloatingBarScaleTransformValue != 0)
                ViewboxFloatingBarScaleTransformValueSlider.Value = settings.Appearance.ViewboxFloatingBarScaleTransformValue;

            ViewboxFloatingBarOpacityValueSlider.Value = settings.Appearance.ViewboxFloatingBarOpacityValue;
            ViewboxFloatingBarOpacityInPPTValueSlider.Value = settings.Appearance.ViewboxFloatingBarOpacityInPPTValue;
            FloatingBarMenuOpacitySlider.Value = settings.Appearance.FloatingBarMenuOpacity;
            FloatingBarMenuOpacityInPPTSlider.Value = settings.Appearance.FloatingBarMenuOpacityInPPT;

            // 加载更小批注栏（Issue #285）设置
            if (ToggleSwitchEnableIdleMiniBar != null)
                ToggleSwitchEnableIdleMiniBar.IsOn = settings.Appearance.EnableIdleMiniBar;
            if (IdleMiniBarOpacitySlider != null)
                IdleMiniBarOpacitySlider.Value = settings.Appearance.IdleMiniBarOpacity;
            if (IdleMiniBarAutoRestoreSlider != null)
                IdleMiniBarAutoRestoreSlider.Value = settings.Appearance.IdleMiniBarAutoRestoreSeconds;

            // 加载液态玻璃浮动栏设置
            if (ToggleSwitchEnableLiquidGlassBar != null)
                ToggleSwitchEnableLiquidGlassBar.IsOn = settings.Appearance.EnableLiquidGlassBar;
            if (LiquidGlassBarOpacitySlider != null)
                LiquidGlassBarOpacitySlider.Value = settings.Appearance.LiquidGlassBarOpacity;

            // 加载工具栏位置
            string positionTag = settings.Appearance.ToolbarPosition.ToString();
            foreach (ComboBoxItem item in ToolbarPositionComboBox.Items)
            {
                if ((string)item.Tag == positionTag)
                {
                    ToolbarPositionComboBox.SelectedItem = item;
                    break;
                }
            }

            // 加载翻转内容设置
            if (CardReverseToolbarContent != null)
            {
                CardReverseToolbarContent.IsOn = settings.Appearance.ReverseToolbarContent;
            }

            // 加载自动翻转设置
            if (ToggleSwitchAutoFlipWhenSpaceInsufficient != null)
            {
                ToggleSwitchAutoFlipWhenSpaceInsufficient.IsOn = settings.Appearance.AutoFlipWhenSpaceInsufficient;
            }

            // 加载自动翻转后翻转组件内容设置
            if (ToggleSwitchFlipContentOnAutoFlip != null)
            {
                ToggleSwitchFlipContentOnAutoFlip.IsOn = settings.Appearance.FlipContentOnAutoFlip;
            }

            // 加载禁止工具栏动画设置
            if (CardDisableToolbarAnimation != null)
            {
                CardDisableToolbarAnimation.IsOn = settings.Appearance.DisableToolbarAnimation;
            }

            // 加载旧版浮动栏 UI 设置
            if (CardUseLegacyFloatingBarUI != null)
                CardUseLegacyFloatingBarUI.IsOn = settings.Appearance.UseLegacyFloatingBarUI;

            // 加载在浮动栏图标上显示笔色设置
            if (CardShowPenColorOnFloatingBarIcon != null)
                CardShowPenColorOnFloatingBarIcon.IsOn = settings.Appearance.ShowPenColorOnFloatingBarIcon;

            // 加载紧凑浮动栏模式设置
            if (ToggleSwitchCompactFloatingBar != null)
                ToggleSwitchCompactFloatingBar.IsOn = settings.Appearance.CompactFloatingBar;

            // 加载隐藏浮动栏边框设置
            if (ToggleSwitchHideFloatingBarBorder != null)
                ToggleSwitchHideFloatingBarBorder.IsOn = settings.Appearance.HideFloatingBarBorder;

            // 加载浮动栏边框颜色设置
            int mode = settings.Appearance.FloatingBarBorderColorMode;
            if (mode < 0 || mode > 2) mode = 0;
            ComboBoxFloatingBarBorderColorMode.SelectedIndex = mode;
            UpdateFloatingBarBorderColorSwatch();
        }

        private void UpdateAllSliderTexts()
        {
            UpdateSliderText(ViewboxFloatingBarScaleTransformValueSlider, ViewboxFloatingBarScaleSliderText, "{0:F2}x");
            UpdateSliderText(ViewboxFloatingBarOpacityValueSlider, ViewboxFloatingBarOpacityText, "{0:F2}");
            UpdateSliderText(ViewboxFloatingBarOpacityInPPTValueSlider, ViewboxFloatingBarOpacityInPPTText, "{0:F2}");
            UpdateSliderText(FloatingBarMenuOpacitySlider, FloatingBarMenuOpacityText, "{0:F2}");
            UpdateSliderText(FloatingBarMenuOpacityInPPTSlider, FloatingBarMenuOpacityInPPTText, "{0:F2}");
            UpdateSliderText(IdleMiniBarOpacitySlider, IdleMiniBarOpacityText, "{0:F2}");
            UpdateSliderText(IdleMiniBarAutoRestoreSlider, IdleMiniBarAutoRestoreText, "{0:F0}s");
            UpdateSliderText(LiquidGlassBarOpacitySlider, LiquidGlassBarOpacityText, "{0:F2}");
            UpdateFloatingBarActualScaleText();
        }

        private void UpdateFloatingBarActualScaleText()
        {
            if (ViewboxFloatingBarScaleTransformValueSlider == null || ViewboxFloatingBarActualScaleText == null) return;
            double val = ViewboxFloatingBarScaleTransformValueSlider.Value;
            double clampedVal = (val > 0.5 && val < 1.25) ? val : val <= 0.5 ? 0.5 : val >= 1.25 ? 1.25 : 1.0;
            double actualScale = clampedVal;
            ViewboxFloatingBarActualScaleText.Text = $"{actualScale:F2}x";
        }

        private void UpdateSliderText(Slider slider, TextBlock textBlock, string format)
        {
            if (slider == null || textBlock == null) return;
            textBlock.Text = string.Format(format, slider.Value);
        }

        private void ViewboxFloatingBarScaleTransformValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(ViewboxFloatingBarScaleTransformValueSlider, ViewboxFloatingBarScaleSliderText, "{0:F2}x");
            if (!_isLoaded) return;
            var slider = ViewboxFloatingBarScaleTransformValueSlider;
            var val = Math.Round(slider.Value, 2);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.ViewboxFloatingBarScaleTransformValue = val;
            SettingsManager.SaveSettingsToFile();

            double clampedVal = (val > 0.5 && val < 1.25) ? val : val <= 0.5 ? 0.5 : val >= 1.25 ? 1.25 : 1.0;
            double actualScale = clampedVal;
            UpdateFloatingBarActualScaleText();

            SettingsActionHub.OnFloatingBarScaleChanged(actualScale);
        }

        private void ViewboxFloatingBarOpacityValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(ViewboxFloatingBarOpacityValueSlider, ViewboxFloatingBarOpacityText, "{0:F2}");
            if (!_isLoaded) return;
            var slider = ViewboxFloatingBarOpacityValueSlider;
            var val = Math.Round(slider.Value, 2);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.ViewboxFloatingBarOpacityValue = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnFloatingBarOpacityChanged(val);
        }

        private void ViewboxFloatingBarOpacityInPPTValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(ViewboxFloatingBarOpacityInPPTValueSlider, ViewboxFloatingBarOpacityInPPTText, "{0:F2}");
            if (!_isLoaded) return;
            var slider = ViewboxFloatingBarOpacityInPPTValueSlider;
            var val = Math.Round(slider.Value, 2);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.ViewboxFloatingBarOpacityInPPTValue = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnFloatingBarOpacityInPPTChanged(val);
        }

        private void ToolbarPositionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (ToolbarPositionComboBox.SelectedItem is not ComboBoxItem selectedItem) return;

            if (Enum.TryParse<ToolbarPosition>((string)selectedItem.Tag, out var position))
            {
                SettingsManager.Settings.Appearance.ToolbarPosition = position;
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnToolbarPositionChanged(position);
            }
        }

        private void ReverseToolbarContentToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (CardReverseToolbarContent == null) return;

            SettingsManager.Settings.Appearance.ReverseToolbarContent = CardReverseToolbarContent.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnReverseToolbarContentChanged(CardReverseToolbarContent.IsOn);
        }

        private void AutoFlipWhenSpaceInsufficientToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (ToggleSwitchAutoFlipWhenSpaceInsufficient == null) return;

            SettingsManager.Settings.Appearance.AutoFlipWhenSpaceInsufficient = ToggleSwitchAutoFlipWhenSpaceInsufficient.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void FlipContentOnAutoFlipToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (ToggleSwitchFlipContentOnAutoFlip == null) return;

            SettingsManager.Settings.Appearance.FlipContentOnAutoFlip = ToggleSwitchFlipContentOnAutoFlip.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void DisableToolbarAnimationToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (CardDisableToolbarAnimation == null) return;

            SettingsManager.Settings.Appearance.DisableToolbarAnimation = CardDisableToolbarAnimation.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchUseLegacyFloatingBarUI_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.UseLegacyFloatingBarUI = CardUseLegacyFloatingBarUI.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnUseLegacyFloatingBarUIChanged();
        }

        private void ToggleSwitchShowPenColorOnFloatingBarIcon_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.ShowPenColorOnFloatingBarIcon = CardShowPenColorOnFloatingBarIcon.IsOn;
            SettingsManager.SaveSettingsToFile();
            // Refresh the pen icon color on the floating bar
            if (Application.Current.MainWindow is MainWindow mainWindow)
                mainWindow.UpdatePenIconColor();
        }

        private void ToggleSwitchCompactFloatingBar_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.CompactFloatingBar = ToggleSwitchCompactFloatingBar.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnCompactFloatingBarChanged(ToggleSwitchCompactFloatingBar.IsOn);
        }

        private void ToggleSwitchHideFloatingBarBorder_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.HideFloatingBarBorder = ToggleSwitchHideFloatingBarBorder.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnHideFloatingBarBorderChanged(ToggleSwitchHideFloatingBarBorder.IsOn);
        }

        /// <summary>
        /// 边框颜色模式：0=默认（主题色），1=跟随背景颜色，2=自定义。
        /// </summary>
        private const int BorderColorMode_Default = 0;
        private const int BorderColorMode_FollowBackground = 1;
        private const int BorderColorMode_Custom = 2;

        /// <summary>
        /// 根据当前模式刷新色块按钮显示：仅自定义模式显示色块，色块颜色为已保存的自定义色或回退到主题默认色。
        /// </summary>
        private void UpdateFloatingBarBorderColorSwatch()
        {
            if (ButtonFloatingBarBorderColor == null) return;
            int mode = ComboBoxFloatingBarBorderColorMode?.SelectedIndex ?? BorderColorMode_Default;
            bool isCustom = mode == BorderColorMode_Custom;
            ButtonFloatingBarBorderColor.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            if (!isCustom) return;

            var color = TryGetFloatingBarBorderColor(out var c) ? c : GetThemeDefaultBorderColor();
            ButtonFloatingBarBorderColor.Background = new SolidColorBrush(color);
        }

        /// <summary>
        /// 解析设置中保存的自定义边框颜色（hex 字符串），失败返回 false。
        /// </summary>
        private static bool TryGetFloatingBarBorderColor(out Color color)
        {
            var saved = SettingsManager.Settings?.Appearance?.FloatingBarBorderColor;
            if (string.IsNullOrWhiteSpace(saved))
            {
                color = Colors.Transparent;
                return false;
            }
            try
            {
                var text = saved.Trim();
                if (text.StartsWith("#")) text = text.Substring(1);
                if (text.Length == 6)
                    text = "FF" + text;
                color = (Color)ColorConverter.ConvertFromString("#" + text);
                return true;
            }
            catch
            {
                color = Colors.Transparent;
                return false;
            }
        }

        /// <summary>
        /// 从应用资源中读取当前主题的浮动栏默认边框色。
        /// </summary>
        private static Color GetThemeDefaultBorderColor()
        {
            if (Application.Current.TryFindResource("FloatBarBorderBrush") is SolidColorBrush brush)
                return brush.Color;
            return Colors.Black;
        }

        private void ComboBoxFloatingBarBorderColorMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 始终刷新色块显示，避免初始化阶段被 _isLoaded 拦截时控件状态仍正确
            UpdateFloatingBarBorderColorSwatch();
            if (!_isLoaded) return;
            if (ComboBoxFloatingBarBorderColorMode.SelectedItem is not ComboBoxItem selectedItem) return;

            int mode = ComboBoxFloatingBarBorderColorMode.SelectedIndex;
            SettingsManager.Settings.Appearance.FloatingBarBorderColorMode = mode;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnFloatingBarBorderColorChanged();
        }

        private void ButtonFloatingBarBorderColor_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var current = TryGetFloatingBarBorderColor(out var c) ? c : GetThemeDefaultBorderColor();
            using (var dialog = new System.Windows.Forms.ColorDialog
            {
                FullOpen = true,
                Color = System.Drawing.Color.FromArgb(current.A, current.R, current.G, current.B)
            })
            {
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                var picked = dialog.Color;
                var hex = $"#{picked.A:X2}{picked.R:X2}{picked.G:X2}{picked.B:X2}";
                SettingsManager.Settings.Appearance.FloatingBarBorderColor = hex;
                SettingsManager.SaveSettingsToFile();
                UpdateFloatingBarBorderColorSwatch();
                SettingsActionHub.OnFloatingBarBorderColorChanged();
            }
        }

        private void FloatingBarMenuOpacitySlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(FloatingBarMenuOpacitySlider, FloatingBarMenuOpacityText, "{0:F2}");
            if (!_isLoaded) return;
            var slider = FloatingBarMenuOpacitySlider;
            var val = Math.Round(slider.Value, 2);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.FloatingBarMenuOpacity = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnFloatingBarMenuOpacityChanged(val);
        }

        private void FloatingBarMenuOpacityInPPTSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(FloatingBarMenuOpacityInPPTSlider, FloatingBarMenuOpacityInPPTText, "{0:F2}");
            if (!_isLoaded) return;
            var slider = FloatingBarMenuOpacityInPPTSlider;
            var val = Math.Round(slider.Value, 2);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.FloatingBarMenuOpacityInPPT = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnFloatingBarMenuOpacityInPPTChanged(val);
        }

        // —— 更小批注栏（Issue #285）——

        private void ToggleSwitchEnableIdleMiniBar_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (ToggleSwitchEnableIdleMiniBar == null) return;
            SettingsManager.Settings.Appearance.EnableIdleMiniBar = ToggleSwitchEnableIdleMiniBar.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnEnableIdleMiniBarChanged(ToggleSwitchEnableIdleMiniBar.IsOn);
        }

        private void IdleMiniBarOpacitySlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(IdleMiniBarOpacitySlider, IdleMiniBarOpacityText, "{0:F2}");
            if (!_isLoaded) return;
            var slider = IdleMiniBarOpacitySlider;
            var val = Math.Round(slider.Value, 2);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.IdleMiniBarOpacity = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnIdleMiniBarOpacityChanged(val);
        }

        private void IdleMiniBarAutoRestoreSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(IdleMiniBarAutoRestoreSlider, IdleMiniBarAutoRestoreText, "{0:F0}s");
            if (!_isLoaded) return;
            var slider = IdleMiniBarAutoRestoreSlider;
            var val = Math.Round(slider.Value, 0);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.IdleMiniBarAutoRestoreSeconds = val;
            SettingsManager.SaveSettingsToFile();
        }

        // —— 液态玻璃浮动栏 ——

        private void ToggleSwitchEnableLiquidGlassBar_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (ToggleSwitchEnableLiquidGlassBar == null) return;
            SettingsManager.Settings.Appearance.EnableLiquidGlassBar = ToggleSwitchEnableLiquidGlassBar.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnEnableLiquidGlassBarChanged(ToggleSwitchEnableLiquidGlassBar.IsOn);
        }

        private void LiquidGlassBarOpacitySlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(LiquidGlassBarOpacitySlider, LiquidGlassBarOpacityText, "{0:F2}");
            if (!_isLoaded) return;
            var slider = LiquidGlassBarOpacitySlider;
            var val = Math.Round(slider.Value, 2);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.LiquidGlassBarOpacity = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnLiquidGlassBarOpacityChanged(val);
        }

        #region Floating Bar Icon

        private void ComboBoxFloatingBarImg_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.FloatingBarImg = ComboBoxFloatingBarImg.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnFloatingBarImgChanged();
        }

        private async void ButtonAddCustomIcon_Click(object sender, RoutedEventArgs e)
        {
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw == null) return;

            var content = new AddCustomIconWindow(mw);
            var dialog = new iNKORE.UI.WPF.Modern.Controls.ContentDialog
            {
                Title = Properties.RandomStrings.Random_AddIcon_WindowTitle,
                Content = content,
                PrimaryButtonText = FloatingBarStrings.Tools_Save,
                CloseButtonText = Properties.RandomStrings.Random_Cancel,
                Owner = Window.GetWindow(this) ?? mw,
                DefaultButton = iNKORE.UI.WPF.Modern.Controls.ContentDialogButton.Primary
            };

            content.OnInputChanged += () =>
            {
                dialog.IsPrimaryButtonEnabled = content.CanSave();
            };
            dialog.IsPrimaryButtonEnabled = content.CanSave();

            dialog.PrimaryButtonClick += (s, args) =>
            {
                var deferral = args.GetDeferral();
                if (content.Save())
                {
                    ComboBoxFloatingBarImg.SelectedIndex = ComboBoxFloatingBarImg.Items.Count - 1;
                    dialog.Hide();
                }
                deferral.Complete();
            };

            await dialog.ShowAsync();
        }

        private async void ButtonManageCustomIcons_Click(object sender, RoutedEventArgs e)
        {
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw == null) return;

            var content = new CustomIconWindow(mw);
            var dialog = new iNKORE.UI.WPF.Modern.Controls.ContentDialog
            {
                Title = Properties.ThemeStrings.Theme_CustomFloatingIconLabel,
                Content = content,
                CloseButtonText = Properties.NotificationStrings.AnimationOff,
                Owner = Window.GetWindow(this) ?? mw,
                DefaultButton = iNKORE.UI.WPF.Modern.Controls.ContentDialogButton.Close
            };
            await dialog.ShowAsync();
        }

        #endregion
    }
}
