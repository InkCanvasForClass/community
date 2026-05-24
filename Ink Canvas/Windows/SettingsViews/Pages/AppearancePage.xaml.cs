using Ink_Canvas.Properties;
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
        public static event Action<double> OnBottomOffsetChanged;

        private bool _isLoaded = false;
        private bool _suppressChickenSoupSourceSelectionChanged = false;
        private bool _isApplyingLanguageFromSettings = false;

        public AppearancePage()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
            Unloaded += Page_Unloaded;
        }

        public static void NotifyBottomOffsetChanged(double val)
        {
            OnBottomOffsetChanged?.Invoke(val);
        }

        private void HandleBottomOffsetChanged(double val)
        {
            if (QuickPanelBottomOffsetSlider != null)
            {
                QuickPanelBottomOffsetSlider.Value = val;
                UpdateSliderText(QuickPanelBottomOffsetSlider, QuickPanelBottomOffsetText, "{0:F0}");
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
            UpdateAllSliderTexts();
            OnBottomOffsetChanged += HandleBottomOffsetChanged;
        }

        private void UpdateAllSliderTexts()
        {
            UpdateSliderText(ViewboxBlackBoardScaleTransformValueSlider, ViewboxBlackBoardScaleText, "{0:F2}");
            UpdateSliderText(QuickPanelBottomOffsetSlider, QuickPanelBottomOffsetText, "{0:F0}");
        }

        private void UpdateSliderText(Slider slider, TextBlock textBlock, string format)
        {
            if (slider == null || textBlock == null) return;
            textBlock.Text = string.Format(format, slider.Value);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            OnBottomOffsetChanged -= HandleBottomOffsetChanged;
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
                    string.Equals(lang, "en-US", StringComparison.OrdinalIgnoreCase) ? 2 :
                    string.Equals(lang, "zh-ME", StringComparison.OrdinalIgnoreCase) ? 3 : 0;
                ComboBoxLanguage.SelectedIndex = langIndex;
            }
            finally
            {
                _isApplyingLanguageFromSettings = false;
            }

            if (settings.Appearance.FloatingBarImg >= ComboBoxFloatingBarImg.Items.Count)
                settings.Appearance.FloatingBarImg = 0;
            ComboBoxFloatingBarImg.SelectedIndex = settings.Appearance.FloatingBarImg;

            ViewboxBlackBoardScaleTransformValueSlider.Value = settings.Appearance.ViewboxBlackBoardScaleTransformValue;

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
                    3 => "zh-ME",
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
                            Application.Current.MainWindow = newWindow;
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

        #endregion

        #region Display Options

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
                { "a", ThemeStrings.Theme_HitokotoCategory_Animation }, { "b", ThemeStrings.Theme_HitokotoCategory_Manga }, { "c", ThemeStrings.Theme_HitokotoCategory_Game }, { "d", ThemeStrings.Theme_HitokotoCategory_Literature },
                { "e", ThemeStrings.Theme_HitokotoCategory_Original }, { "f", ThemeStrings.Theme_HitokotoCategory_FromWeb }, { "g", NotificationStrings.Type_Other }, { "h", ThemeStrings.Theme_HitokotoCategory_Movie },
                { "i", ThemeStrings.Theme_HitokotoCategory_Poetry }, { "j", ThemeStrings.Theme_HitokotoCategory_NeteaseCloud }, { "k", ThemeStrings.Theme_HitokotoCategory_Philosophy }, { "l", ThemeStrings.Theme_HitokotoCategory_Humor }
            };

            var contentPanel = new StackPanel { Margin = new Thickness(20), Orientation = Orientation.Vertical };
            var selectAllCheckBox = new CheckBox { Content = ThemeStrings.Theme_Hitokoto_SelectAll, FontSize = 14, Margin = new Thickness(0, 0, 0, 8) };
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
                Title = ThemeStrings.Theme_Hitokoto_CustomizeTitle,
                Content = new ScrollViewer { Content = mainPanel, MaxHeight = 400, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
                PrimaryButtonText = CommonStrings.Common_OK,
                SecondaryButtonText = CommonStrings.Common_Cancel,
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
    }
}
