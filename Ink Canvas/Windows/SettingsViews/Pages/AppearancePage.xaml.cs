using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ContentDialog = iNKORE.UI.WPF.Modern.Controls.ContentDialog;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class AppearancePage : Page
    {
        private bool _isLoaded = false;
        private bool _suppressChickenSoupSourceSelectionChanged = false;
        private bool _isApplyingLanguageFromSettings = false;

        public AppearancePage()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
            Unloaded += Page_Unloaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
            UpdateAllSliderTexts();
        }

        private void UpdateAllSliderTexts()
        {
            UpdateSliderText(ViewboxFloatingBarScaleTransformValueSlider, ViewboxFloatingBarScaleSliderText, "{0:F2}x");
            UpdateSliderText(ViewboxFloatingBarOpacityValueSlider, ViewboxFloatingBarOpacityText, "{0:F2}");
            UpdateSliderText(ViewboxFloatingBarOpacityInPPTValueSlider, ViewboxFloatingBarOpacityInPPTText, "{0:F2}");
            UpdateSliderText(ViewboxBlackBoardScaleTransformValueSlider, ViewboxBlackBoardScaleText, "{0:F2}");
            UpdateSliderText(QuickPanelBottomOffsetSlider, QuickPanelBottomOffsetText, "{0:F0}");
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

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings;
            if (settings?.Appearance == null) return;

            ComboBoxTheme.SelectedIndex = settings.Appearance.Theme;
            SelectComboBoxItemByTag(ComboBoxWindowBackdrop, settings.Appearance.WindowBackdrop);

            _isApplyingLanguageFromSettings = true;
            try
            {
                var lang = settings.Appearance.Language ?? string.Empty;
                int langIndex = string.IsNullOrWhiteSpace(lang) ? 0 :
                    string.Equals(lang, "zh-CN", StringComparison.OrdinalIgnoreCase) ? 1 :
                    string.Equals(lang, "en-US", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
                ComboBoxLanguage.SelectedIndex = langIndex;
            }
            finally
            {
                _isApplyingLanguageFromSettings = false;
            }

            CardEnableSplashScreen.IsOn = settings.Appearance.EnableSplashScreen;
            ComboBoxSplashScreenStyle.SelectedIndex = settings.Appearance.SplashScreenStyle;
            UpdateCustomSplashImageVisibility();

            if (!string.IsNullOrEmpty(settings.Appearance.CustomSplashImagePath) && 
                System.IO.File.Exists(settings.Appearance.CustomSplashImagePath))
            {
                TextBlockCustomSplashPath.Text = System.IO.Path.GetFileName(settings.Appearance.CustomSplashImagePath);
                TextBlockCustomSplashPath.ToolTip = settings.Appearance.CustomSplashImagePath;
            }
            else
            {
                TextBlockCustomSplashPath.Text = "未选择自定义图片";
                TextBlockCustomSplashPath.ToolTip = null;
            }

            UpdateTextAlignButtonAppearance(settings.Appearance.CustomSplashTextPosition);

            if (settings.Appearance.FloatingBarImg >= ComboBoxFloatingBarImg.Items.Count)
                settings.Appearance.FloatingBarImg = 0;
            ComboBoxFloatingBarImg.SelectedIndex = settings.Appearance.FloatingBarImg;

            if (settings.Appearance.ViewboxFloatingBarScaleTransformValue != 0)
                ViewboxFloatingBarScaleTransformValueSlider.Value = settings.Appearance.ViewboxFloatingBarScaleTransformValue;

            ViewboxBlackBoardScaleTransformValueSlider.Value = settings.Appearance.ViewboxBlackBoardScaleTransformValue;

            ViewboxFloatingBarOpacityValueSlider.Value = settings.Appearance.ViewboxFloatingBarOpacityValue;
            ViewboxFloatingBarOpacityInPPTValueSlider.Value = settings.Appearance.ViewboxFloatingBarOpacityInPPTValue;

            CardEnableDisPlayNibModeToggle.IsOn = settings.Appearance.IsEnableDisPlayNibModeToggler;
            CardEnableTimeDisplayInWhiteboardMode.IsOn = settings.Appearance.EnableTimeDisplayInWhiteboardMode;
            CardUse24HourTimeFormat.IsOn = settings.Appearance.Use24HourTimeFormat;
            CardEnableChickenSoupInWhiteboardMode.IsOn = settings.Appearance.EnableChickenSoupInWhiteboardMode;

            _suppressChickenSoupSourceSelectionChanged = true;
            try
            {
                ComboBoxChickenSoupSource.SelectedIndex = settings.Appearance.ChickenSoupSource;
            }
            finally
            {
                Dispatcher.BeginInvoke(
                    (Action)(() => { _suppressChickenSoupSourceSelectionChanged = false; }),
                    DispatcherPriority.ContextIdle);
            }

            CardEnableQuickPanel.IsOn = settings.Appearance.IsShowQuickPanel;
            QuickPanelBottomOffsetSlider.Value = settings.Appearance.QuickPanelBottomOffset;
            ComboBoxUnFoldBtnImg.SelectedIndex = settings.Appearance.UnFoldButtonImageType;

            CardUseLegacyFloatingBarUI.IsOn = settings.Appearance.UseLegacyFloatingBarUI;

            CardEnableTrayIcon.IsOn = settings.Appearance.EnableTrayIcon;

            ComboBoxTrayLeftClickAction.SelectedIndex = (int)settings.Appearance.TrayLeftClickAction;
            ComboBoxTrayRightClickAction.SelectedIndex = (int)settings.Appearance.TrayRightClickAction;

            if (BtnHitokotoCustomize != null)
                BtnHitokotoCustomize.Visibility = settings.Appearance.ChickenSoupSource == 3 ? Visibility.Visible : Visibility.Collapsed;
        }

        private MainWindow GetMainWindow() => Application.Current.MainWindow as MainWindow;

        private static void SelectComboBoxItemByTag(ComboBox comboBox, string tag)
        {
            if (comboBox == null) return;

            var selectedItem = comboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                ?? comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();

            comboBox.SelectedItem = selectedItem;
        }

        private static string GetSelectedComboBoxTag(ComboBox comboBox, string fallback)
        {
            return (comboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;
        }

        #region Theme & Language

        private void ComboBoxTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                SettingsManager.Settings.Appearance.Theme = ComboBoxTheme.SelectedIndex;
                SettingsManager.SaveSettingsToFile();
                var mw = GetMainWindow();
                if (mw != null) mw.ApplyTheme(ComboBoxTheme.SelectedIndex);
            }
            catch (Exception ex) { Debug.WriteLine($"切换主题时出错: {ex.Message}"); }
        }

        private void ComboBoxLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || _isApplyingLanguageFromSettings) return;
            try
            {
                var index = ComboBoxLanguage.SelectedIndex;
                string language = index switch
                {
                    1 => "zh-CN",
                    2 => "en-US",
                    _ => string.Empty
                };
                SettingsManager.Settings.Appearance.Language = language;
                SettingsManager.SaveSettingsToFile();
                LocalizationHelper.TrySetCulture(language);
                var mw = GetMainWindow();
                if (mw != null)
                {
                    mw._isReloadingForLanguageChange = true;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            var newWindow = new MainWindow
                            {
                                WindowState = mw.WindowState,
                                Left = mw.Left,
                                Top = mw.Top
                            };
                            newWindow.Show();
                            mw.Close();
                        }
                        catch (Exception ex2)
                        {
                            Debug.WriteLine($"重建主窗口以应用语言时出错: {ex2.Message}");
                            mw._isReloadingForLanguageChange = false;
                        }
                    }), DispatcherPriority.ApplicationIdle);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"切换界面语言时出错: {ex.Message}"); }
        }

        private void ComboBoxWindowBackdrop_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                var backdrop = GetSelectedComboBoxTag(ComboBoxWindowBackdrop, "None");
                SettingsManager.Settings.Appearance.WindowBackdrop = backdrop;
                SettingsManager.SaveSettingsToFile();

                if (Window.GetWindow(this) is SettingsWindow settingsWindow)
                {
                    settingsWindow.ApplyWindowBackdrop(backdrop);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"切换窗口背景样式时出错: {ex.Message}"); }
        }

        #endregion

        #region Splash Screen

        private void ToggleSwitchEnableSplashScreen_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.EnableSplashScreen = CardEnableSplashScreen.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ComboBoxSplashScreenStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.SplashScreenStyle = ComboBoxSplashScreenStyle.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
            UpdateCustomSplashImageVisibility();
        }

        private void UpdateCustomSplashImageVisibility()
        {
            bool isCustomSelected = ComboBoxSplashScreenStyle.SelectedIndex == 7;
            CardCustomSplashImage.Visibility = isCustomSelected ? Visibility.Visible : Visibility.Collapsed;
            CardCustomSplashTextPosition.Visibility = isCustomSelected ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BorderTextAlign_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_isLoaded) return;

            if (sender is Border border && border.Tag != null)
            {
                int selectedIndex = int.Parse(border.Tag.ToString());
                SettingsManager.Settings.Appearance.CustomSplashTextPosition = selectedIndex;
                SettingsManager.SaveSettingsToFile();
                UpdateTextAlignButtonAppearance(selectedIndex);
            }
        }

        private void UpdateTextAlignButtonAppearance(int selectedIndex)
        {
            AnimateIndicatorToPosition(selectedIndex);
        }

        private void AnimateIndicatorToPosition(int position)
        {
            // 外容器 110px，内边距 1px，每个按钮 36px
            // 左: X=0, 中: X=36, 右: X=72
            double targetX = position * 36;

            var animation = new DoubleAnimation
            {
                To = targetX,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            IndicatorTranslateTransform.BeginAnimation(TranslateTransform.XProperty, animation);

            // 跟随系统主题颜色
            var isDarkTheme = SettingsManager.Settings.Appearance.Theme == 1;

            if (isDarkTheme)
            {
                SelectionIndicator.Background = new SolidColorBrush(Color.FromArgb(40, 0, 120, 215));
                SelectionIndicator.BorderBrush = new SolidColorBrush(Color.FromArgb(150, 0, 120, 215));
            }
            else
            {
                SelectionIndicator.Background = new SolidColorBrush(Color.FromArgb(25, 0, 120, 215));
                SelectionIndicator.BorderBrush = new SolidColorBrush(Color.FromArgb(120, 0, 120, 215));
            }
        }

        private void ButtonBrowseCustomSplash_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|All Files|*.*",
                    Title = "选择自定义启动图片"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    string selectedPath = openFileDialog.FileName;
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        SettingsManager.Settings.Appearance.CustomSplashImagePath = selectedPath;
                        SettingsManager.SaveSettingsToFile();
                        TextBlockCustomSplashPath.Text = System.IO.Path.GetFileName(selectedPath);
                        TextBlockCustomSplashPath.ToolTip = selectedPath;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"选择自定义启动图片时出错: {ex.Message}");
            }
        }

        private void ButtonClearCustomSplash_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Settings.Appearance.CustomSplashImagePath = string.Empty;
            SettingsManager.SaveSettingsToFile();
            TextBlockCustomSplashPath.Text = "未选择自定义图片";
            TextBlockCustomSplashPath.ToolTip = null;
        }

        #endregion

        #region Floating Bar Appearance

        private void ComboBoxFloatingBarImg_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.FloatingBarImg = ComboBoxFloatingBarImg.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
            var mw = GetMainWindow();
            if (mw != null) mw.UpdateFloatingBarIcon();
        }

        private void ButtonAddCustomIcon_Click(object sender, RoutedEventArgs e)
        {
            var mw = GetMainWindow();
            if (mw == null) return;
            AddCustomIconWindow dialog = new AddCustomIconWindow(mw);
            dialog.Owner = mw;
            dialog.ShowDialog();
            if (dialog.IsSuccess)
            {
                ComboBoxFloatingBarImg.SelectedIndex = ComboBoxFloatingBarImg.Items.Count - 1;
            }
        }

        private void ButtonManageCustomIcons_Click(object sender, RoutedEventArgs e)
        {
            var mw = GetMainWindow();
            if (mw == null) return;
            CustomIconWindow dialog = new CustomIconWindow(mw);
            dialog.Owner = mw;
            dialog.ShowDialog();
        }

        private void ViewboxFloatingBarScaleTransformValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(ViewboxFloatingBarScaleTransformValueSlider, ViewboxFloatingBarScaleSliderText, "{0:F2}x");
            if (!_isLoaded) return;
            var slider = ViewboxFloatingBarScaleTransformValueSlider;
            var val = Math.Round(slider.Value, 2);
            // 仅当四舍五入纠正了显示值时才回写；那次 set 会重入 ValueChanged 完成保存。
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

            var mw = GetMainWindow();
            if (mw != null)
            {
                mw.ViewboxFloatingBarScaleTransform.ScaleX = actualScale;
                mw.ViewboxFloatingBarScaleTransform.ScaleY = actualScale;
                if (mw.IsInPptPresentationMode)
                    mw.ViewboxFloatingBarMarginAnimation(60);
                else
                    mw.ViewboxFloatingBarMarginAnimation(100, true);
            }
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
            var mw = GetMainWindow();
            if (mw != null) mw.ViewboxFloatingBar.Opacity = val;
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
            var mw = GetMainWindow();
            if (mw != null && mw.currentMode == 2)
            {
                mw.ViewboxFloatingBar.Opacity = val;
            }
        }

        #endregion

        #region Display Options

        private void ToggleSwitchEnableDisPlayNibModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.IsEnableDisPlayNibModeToggler = CardEnableDisPlayNibModeToggle.IsOn;
            SettingsManager.SaveSettingsToFile();
            var mw = GetMainWindow();
            if (mw != null)
            {
                var vis = CardEnableDisPlayNibModeToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
                mw.NibModeSimpleStackPanel.Visibility = vis;
                mw.BoardNibModeSimpleStackPanel.Visibility = vis;
            }
        }

        private void ViewboxBlackBoardScaleTransformValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSliderText(ViewboxBlackBoardScaleTransformValueSlider, ViewboxBlackBoardScaleText, "{0:F2}");
            if (!_isLoaded) return;
            var slider = ViewboxBlackBoardScaleTransformValueSlider;
            var val = Math.Round(slider.Value, 2);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.ViewboxBlackBoardScaleTransformValue = val;
            SettingsManager.SaveSettingsToFile();
            var mw = GetMainWindow();
            if (mw != null)
            {
                mw.ViewboxBlackboardCenterSideScaleTransform.ScaleX = val;
                mw.ViewboxBlackboardCenterSideScaleTransform.ScaleY = val;
            }
        }

        private void ToggleSwitchEnableTimeDisplayInWhiteboardMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.EnableTimeDisplayInWhiteboardMode = CardEnableTimeDisplayInWhiteboardMode.IsOn;
            SettingsManager.SaveSettingsToFile();
            var mw = GetMainWindow();
            if (mw != null && mw.currentMode == 1)
            {
                var vis = CardEnableTimeDisplayInWhiteboardMode.IsOn ? Visibility.Visible : Visibility.Collapsed;
                mw.WaterMarkTime.Visibility = vis;
                mw.WaterMarkDate.Visibility = vis;
            }
        }

        private void ToggleSwitchEnableChickenSoupInWhiteboardMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.EnableChickenSoupInWhiteboardMode = CardEnableChickenSoupInWhiteboardMode.IsOn;
            SettingsManager.SaveSettingsToFile();
            var mw = GetMainWindow();
            if (mw != null && mw.currentMode == 1 && CardEnableTimeDisplayInWhiteboardMode.IsOn)
            {
                mw.BlackBoardWaterMark.Visibility = CardEnableChickenSoupInWhiteboardMode.IsOn ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ToggleSwitchUse24HourTimeFormat_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.Use24HourTimeFormat = CardUse24HourTimeFormat.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private async void ComboBoxChickenSoupSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressChickenSoupSourceSelectionChanged || !_isLoaded) return;
            int idx = ComboBoxChickenSoupSource.SelectedIndex;
            if (idx < 0) return;
            if (SettingsManager.Settings.Appearance.ChickenSoupSource == idx) return;
            SettingsManager.Settings.Appearance.ChickenSoupSource = idx;
            if (BtnHitokotoCustomize != null)
                BtnHitokotoCustomize.Visibility = idx == 3 ? Visibility.Visible : Visibility.Collapsed;
            SettingsManager.SaveSettingsToFile();
            var mw = GetMainWindow();
            if (mw != null) await mw.UpdateChickenSoupTextAsync();
        }

        private async void BtnHitokotoCustomize_Click(object sender, RoutedEventArgs e)
        {
            var categories = new System.Collections.Generic.Dictionary<string, string>
            {
                { "a", "动画" }, { "b", "漫画" }, { "c", "游戏" }, { "d", "文学" },
                { "e", "原创" }, { "f", "来自网络" }, { "g", "其他" }, { "h", "影视" },
                { "i", "诗词" }, { "j", "网易云" }, { "k", "哲学" }, { "l", "抖机灵" }
            };

            var contentPanel = new StackPanel { Margin = new Thickness(20), Orientation = Orientation.Vertical };
            var selectAllCheckBox = new CheckBox { Content = "全选", FontSize = 14, Margin = new Thickness(0, 0, 0, 8) };
            var categoryCheckBoxes = new System.Collections.Generic.Dictionary<string, CheckBox>();
            var savedHitokoto = SettingsManager.Settings.Appearance.HitokotoCategories;
            bool implicitAllCategories = savedHitokoto == null || savedHitokoto.Count == 0;

            foreach (var category in categories)
            {
                var checkBox = new CheckBox
                {
                    Content = category.Value,
                    Tag = category.Key,
                    FontSize = 13,
                    IsChecked = implicitAllCategories || savedHitokoto.Contains(category.Key),
                    Margin = new Thickness(0, 0, 0, 8)
                };
                categoryCheckBoxes[category.Key] = checkBox;
                contentPanel.Children.Add(checkBox);
            }

            bool isUpdatingSelectAll = false;
            selectAllCheckBox.IsChecked = implicitAllCategories || savedHitokoto.Count == categories.Count;
            selectAllCheckBox.Checked += (s, args) => { if (isUpdatingSelectAll) return; isUpdatingSelectAll = true; foreach (var cb in categoryCheckBoxes.Values) cb.IsChecked = true; isUpdatingSelectAll = false; };
            selectAllCheckBox.Unchecked += (s, args) => { if (isUpdatingSelectAll) return; isUpdatingSelectAll = true; foreach (var cb in categoryCheckBoxes.Values) cb.IsChecked = false; isUpdatingSelectAll = false; };
            foreach (var cb in categoryCheckBoxes.Values)
            {
                cb.Checked += (s, args) => { if (isUpdatingSelectAll) return; isUpdatingSelectAll = true; selectAllCheckBox.IsChecked = categoryCheckBoxes.Values.All(c => c.IsChecked == true); isUpdatingSelectAll = false; };
                cb.Unchecked += (s, args) => { if (isUpdatingSelectAll) return; isUpdatingSelectAll = true; selectAllCheckBox.IsChecked = false; isUpdatingSelectAll = false; };
            }

            var mainPanel = new StackPanel();
            mainPanel.Children.Add(selectAllCheckBox);
            mainPanel.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });
            mainPanel.Children.Add(contentPanel);

            var mw = GetMainWindow();
            var contentDialog = new ContentDialog
            {
                Title = "自定义一言分类",
                Content = new ScrollViewer { Content = mainPanel, MaxHeight = 400, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
                PrimaryButtonText = "确定",
                SecondaryButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                Owner = mw
            };

            var dialogResult = await contentDialog.ShowAsync();
            if (dialogResult == ContentDialogResult.Primary)
            {
                SettingsManager.Settings.Appearance.HitokotoCategories = categoryCheckBoxes.Where(kvp => kvp.Value.IsChecked == true).Select(kvp => kvp.Key).ToList();
                if (SettingsManager.Settings.Appearance.HitokotoCategories.Count == 0)
                    SettingsManager.Settings.Appearance.HitokotoCategories = categories.Keys.ToList();
                SettingsManager.SaveSettingsToFile();
                if (SettingsManager.Settings.Appearance.ChickenSoupSource == 3 && SettingsManager.Settings.Appearance.EnableChickenSoupInWhiteboardMode)
                {
                    if (mw != null) await mw.UpdateChickenSoupTextAsync();
                }
            }
        }

        private void ToggleSwitchEnableQuickPanel_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.IsShowQuickPanel = CardEnableQuickPanel.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void QuickPanelBottomOffsetSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(QuickPanelBottomOffsetSlider, QuickPanelBottomOffsetText, "{0:F0}");
            if (!_isLoaded) return;
            var val = Math.Round(QuickPanelBottomOffsetSlider.Value);
            if (QuickPanelBottomOffsetSlider.Value != val)
            {
                QuickPanelBottomOffsetSlider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.QuickPanelBottomOffset = val;
            SettingsManager.SaveSettingsToFile();
            var mw = GetMainWindow();
            if (mw != null) mw.ApplyQuickPanelBottomOffset(val);
        }

        private void ComboBoxUnFoldBtnImg_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.UnFoldButtonImageType = ComboBoxUnFoldBtnImg.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
            var mw = GetMainWindow();
            if (mw != null)
            {
                if (ComboBoxUnFoldBtnImg.SelectedIndex == 0)
                {
                    mw.RightUnFoldBtnImgChevron.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/new-icons/unfold-chevron.png"));
                    mw.RightUnFoldBtnImgChevron.Width = 14; mw.RightUnFoldBtnImgChevron.Height = 14;
                    mw.RightUnFoldBtnImgChevron.RenderTransform = new RotateTransform(180);
                    mw.LeftUnFoldBtnImgChevron.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/new-icons/unfold-chevron.png"));
                    mw.LeftUnFoldBtnImgChevron.Width = 14; mw.LeftUnFoldBtnImgChevron.Height = 14;
                    mw.LeftUnFoldBtnImgChevron.RenderTransform = null;
                }
                else if (ComboBoxUnFoldBtnImg.SelectedIndex == 1)
                {
                    mw.RightUnFoldBtnImgChevron.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/new-icons/pen-white.png"));
                    mw.RightUnFoldBtnImgChevron.Width = 18; mw.RightUnFoldBtnImgChevron.Height = 18;
                    mw.RightUnFoldBtnImgChevron.RenderTransform = null;
                    mw.LeftUnFoldBtnImgChevron.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/new-icons/pen-white.png"));
                    mw.LeftUnFoldBtnImgChevron.Width = 18; mw.LeftUnFoldBtnImgChevron.Height = 18;
                    mw.LeftUnFoldBtnImgChevron.RenderTransform = null;
                }
            }
        }

        #endregion

        #region Floating Bar Buttons

        private void ToggleSwitchUseLegacyFloatingBarUI_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.UseLegacyFloatingBarUI = CardUseLegacyFloatingBarUI.IsOn;
            SettingsManager.SaveSettingsToFile();
            var mw = GetMainWindow();
            if (mw != null) mw.UpdateFloatingBarIcons();
        }

        private void CardFloatingBarButtons_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = Application.Current.Windows.OfType<SettingsViews.SettingsWindow>().FirstOrDefault();
            if (settingsWindow != null)
                settingsWindow.NavigateToPage("ToolbarPage");
        }

        #endregion

        #region Tray Icon

        private void ToggleSwitchEnableTrayIcon_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.EnableTrayIcon = CardEnableTrayIcon.IsOn;
            SettingsManager.SaveSettingsToFile();
            try
            {
                var _taskbar = Application.Current.Resources["TaskbarTrayIcon"];
                if (_taskbar is FrameworkElement fe)
                    fe.Visibility = CardEnableTrayIcon.IsOn ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ExceptionHandler.HandleException(ex, "更新托盘图标可见性失败", LogHelper.LogType.Warning);
            }
        }

        private void ComboBoxTrayLeftClickAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.TrayLeftClickAction = (TrayClickAction)ComboBoxTrayLeftClickAction.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
        }

        private void ComboBoxTrayRightClickAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.TrayRightClickAction = (TrayClickAction)ComboBoxTrayRightClickAction.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
        }

        #endregion
    }
}
