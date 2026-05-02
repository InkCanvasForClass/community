using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
            UpdateSliderText(ViewboxFloatingBarScaleTransformValueSlider, ViewboxFloatingBarScaleSliderText, "{0:F2}x)");
            UpdateSliderText(ViewboxFloatingBarOpacityValueSlider, ViewboxFloatingBarOpacityText, "{0:F2}");
            UpdateSliderText(ViewboxFloatingBarOpacityInPPTValueSlider, ViewboxFloatingBarOpacityInPPTText, "{0:F2}");
            UpdateSliderText(ViewboxBlackBoardScaleTransformValueSlider, ViewboxBlackBoardScaleText, "{0:F2}");
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
            ComboBoxUnFoldBtnImg.SelectedIndex = settings.Appearance.UnFoldButtonImageType;

            CardUseLegacyFloatingBarUI.IsOn = settings.Appearance.UseLegacyFloatingBarUI;
            CheckBoxShowShapeButton.IsChecked = settings.Appearance.IsShowShapeButton;
            CheckBoxShowUndoButton.IsChecked = settings.Appearance.IsShowUndoButton;
            CheckBoxShowRedoButton.IsChecked = settings.Appearance.IsShowRedoButton;
            CheckBoxShowClearButton.IsChecked = settings.Appearance.IsShowClearButton;
            CheckBoxShowWhiteboardButton.IsChecked = settings.Appearance.IsShowWhiteboardButton;
            CheckBoxShowHideButton.IsChecked = settings.Appearance.IsShowHideButton;
            CheckBoxShowLassoSelectButton.IsChecked = settings.Appearance.IsShowLassoSelectButton;
            CheckBoxShowClearAndMouseButton.IsChecked = settings.Appearance.IsShowClearAndMouseButton;
            CheckBoxShowQuickColorPalette.IsChecked = settings.Appearance.IsShowQuickColorPalette;
            ComboBoxQuickColorPaletteDisplayMode.SelectedIndex = settings.Appearance.QuickColorPaletteDisplayMode;
            ComboBoxEraserDisplayOption.SelectedIndex = settings.Appearance.EraserDisplayOption;

            CardEnableTrayIcon.IsOn = settings.Appearance.EnableTrayIcon;

            ComboBoxTrayLeftClickAction.SelectedIndex = (int)settings.Appearance.TrayLeftClickAction;
            ComboBoxTrayRightClickAction.SelectedIndex = (int)settings.Appearance.TrayRightClickAction;

            if (BtnHitokotoCustomize != null)
                BtnHitokotoCustomize.Visibility = settings.Appearance.ChickenSoupSource == 3 ? Visibility.Visible : Visibility.Collapsed;
        }

        private MainWindow GetMainWindow() => Application.Current.MainWindow as MainWindow;

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
            UpdateSliderText(ViewboxFloatingBarScaleTransformValueSlider, ViewboxFloatingBarScaleSliderText, "{0:F2}x)");
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

            // 计算并显示实际缩放倍率（基础 1.5 × 用户设置）
            double clampedVal = (val > 0.5 && val < 1.25) ? val : val <= 0.5 ? 0.5 : val >= 1.25 ? 1.25 : 1.0;
            double actualScale = 1.5 * clampedVal;
            ViewboxFloatingBarActualScaleText.Text = $"{actualScale:F2}x";

            var mw = GetMainWindow();
            if (mw != null)
            {
                // 应用实际缩放值（基础 1.5 × 用户设置）
                mw.ViewboxFloatingBarScaleTransform.ScaleX = actualScale;
                mw.ViewboxFloatingBarScaleTransform.ScaleY = actualScale;
                if (mw.BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
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

        private void CheckBoxShowShapeButton_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.IsShowShapeButton = CheckBoxShowShapeButton.IsChecked ?? false;
            SettingsManager.SaveSettingsToFile();
            var mw = GetMainWindow();
            if (mw != null) mw.UpdateFloatingBarButtonsVisibility();
        }

        private void CheckBoxShowButton_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (sender == CheckBoxShowUndoButton)
                SettingsManager.Settings.Appearance.IsShowUndoButton = CheckBoxShowUndoButton.IsChecked ?? false;
            else if (sender == CheckBoxShowRedoButton)
                SettingsManager.Settings.Appearance.IsShowRedoButton = CheckBoxShowRedoButton.IsChecked ?? false;
            else if (sender == CheckBoxShowClearButton)
                SettingsManager.Settings.Appearance.IsShowClearButton = CheckBoxShowClearButton.IsChecked ?? false;
            else if (sender == CheckBoxShowWhiteboardButton)
                SettingsManager.Settings.Appearance.IsShowWhiteboardButton = CheckBoxShowWhiteboardButton.IsChecked ?? false;
            else if (sender == CheckBoxShowHideButton)
                SettingsManager.Settings.Appearance.IsShowHideButton = CheckBoxShowHideButton.IsChecked ?? false;
            else if (sender == CheckBoxShowLassoSelectButton)
                SettingsManager.Settings.Appearance.IsShowLassoSelectButton = CheckBoxShowLassoSelectButton.IsChecked ?? false;
            else if (sender == CheckBoxShowClearAndMouseButton)
                SettingsManager.Settings.Appearance.IsShowClearAndMouseButton = CheckBoxShowClearAndMouseButton.IsChecked ?? false;
            else if (sender == CheckBoxShowQuickColorPalette)
                SettingsManager.Settings.Appearance.IsShowQuickColorPalette = CheckBoxShowQuickColorPalette.IsChecked ?? false;
            SettingsManager.SaveSettingsToFile();
            var mw = GetMainWindow();
            if (mw != null) mw.UpdateFloatingBarButtonsVisibility();
        }

        private void ComboBoxQuickColorPaletteDisplayMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.QuickColorPaletteDisplayMode = ComboBoxQuickColorPaletteDisplayMode.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
            var mw = GetMainWindow();
            if (mw != null) mw.UpdateFloatingBarButtonsVisibility();
        }

        private void ComboBoxEraserDisplayOption_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.EraserDisplayOption = ComboBoxEraserDisplayOption.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
            var mw = GetMainWindow();
            if (mw != null) mw.UpdateFloatingBarButtonsVisibility();
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
            catch { }
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
