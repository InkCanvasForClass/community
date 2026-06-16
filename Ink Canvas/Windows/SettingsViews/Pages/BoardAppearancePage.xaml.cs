using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ContentDialog = iNKORE.UI.WPF.Modern.Controls.ContentDialog;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class BoardAppearancePage : Page
    {
        private bool _isLoaded = false;
        private bool _suppressChickenSoupSourceSelectionChanged = false;

        public BoardAppearancePage()
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

            CardEnableTimeDisplayInWhiteboardMode.IsOn = settings.Appearance.EnableTimeDisplayInWhiteboardMode;
            CardEnableChickenSoupInWhiteboardMode.IsOn = settings.Appearance.EnableChickenSoupInWhiteboardMode;

            SelectComboBoxItemByTag(ComboBoxChickenSoupPosition, settings.Appearance.ChickenSoupPosition);

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

            if (BtnHitokotoCustomize != null)
                BtnHitokotoCustomize.Visibility = settings.Appearance.ChickenSoupSource == 3 ? Visibility.Visible : Visibility.Collapsed;

            BoardToolbarLeftOpacitySlider.Value = settings.Appearance.BoardToolbarLeftOpacity;
            BoardToolbarCenterOpacitySlider.Value = settings.Appearance.BoardToolbarCenterOpacity;
            BoardToolbarRightOpacitySlider.Value = settings.Appearance.BoardToolbarRightOpacity;
            BoardMenuOpacitySlider.Value = settings.Appearance.BoardMenuOpacity;

            ViewboxBlackBoardLeftScaleTransformValueSlider.Value = settings.Appearance.ViewboxBlackBoardLeftScaleTransformValue;
            ViewboxBlackBoardCenterScaleTransformValueSlider.Value = settings.Appearance.ViewboxBlackBoardScaleTransformValue;
            ViewboxBlackBoardRightScaleTransformValueSlider.Value = settings.Appearance.ViewboxBlackBoardRightScaleTransformValue;
        }

        private void UpdateAllSliderTexts()
        {
            UpdateSliderText(BoardToolbarLeftOpacitySlider, BoardToolbarLeftOpacityText, "{0:F2}");
            UpdateSliderText(BoardToolbarCenterOpacitySlider, BoardToolbarCenterOpacityText, "{0:F2}");
            UpdateSliderText(BoardToolbarRightOpacitySlider, BoardToolbarRightOpacityText, "{0:F2}");
            UpdateSliderText(BoardMenuOpacitySlider, BoardMenuOpacityText, "{0:F2}");
            UpdateSliderText(ViewboxBlackBoardLeftScaleTransformValueSlider, ViewboxBlackBoardLeftScaleText, "{0:F2}");
            UpdateSliderText(ViewboxBlackBoardCenterScaleTransformValueSlider, ViewboxBlackBoardCenterScaleText, "{0:F2}");
            UpdateSliderText(ViewboxBlackBoardRightScaleTransformValueSlider, ViewboxBlackBoardRightScaleText, "{0:F2}");
        }

        private void UpdateSliderText(Slider slider, TextBlock textBlock, string format)
        {
            if (slider == null || textBlock == null) return;
            textBlock.Text = string.Format(format, slider.Value);
        }

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

        #region Whiteboard Display Options

        private void ToggleSwitchEnableTimeDisplayInWhiteboardMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.EnableTimeDisplayInWhiteboardMode = CardEnableTimeDisplayInWhiteboardMode.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnTimeDisplayInWhiteboardChanged(CardEnableTimeDisplayInWhiteboardMode.IsOn);
        }

        private void ToggleSwitchEnableChickenSoupInWhiteboardMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.EnableChickenSoupInWhiteboardMode = CardEnableChickenSoupInWhiteboardMode.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnChickenSoupInWhiteboardChanged(
                CardEnableChickenSoupInWhiteboardMode.IsOn,
                CardEnableTimeDisplayInWhiteboardMode.IsOn);
        }

        private void ComboBoxChickenSoupSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressChickenSoupSourceSelectionChanged || !_isLoaded) return;
            int idx = ComboBoxChickenSoupSource.SelectedIndex;
            if (idx < 0) return;
            if (SettingsManager.Settings.Appearance.ChickenSoupSource == idx) return;
            SettingsManager.Settings.Appearance.ChickenSoupSource = idx;
            if (BtnHitokotoCustomize != null)
                BtnHitokotoCustomize.Visibility = idx == 3 ? Visibility.Visible : Visibility.Collapsed;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnChickenSoupSourceChanged();
        }

        private void ComboBoxChickenSoupPosition_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            var position = GetSelectedComboBoxTag(ComboBoxChickenSoupPosition, "TopRight");
            SettingsManager.Settings.Appearance.ChickenSoupPosition = position;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnChickenSoupPositionChanged();
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

            var mw = Application.Current.MainWindow as MainWindow;
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
                    SettingsActionHub.OnChickenSoupSourceChanged();
                }
            }
        }

        #endregion

        private void BoardToolbarLeftOpacitySlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(BoardToolbarLeftOpacitySlider, BoardToolbarLeftOpacityText, "{0:F2}");
            if (!_isLoaded) return;
            var val = Math.Round(BoardToolbarLeftOpacitySlider.Value, 2);
            if (BoardToolbarLeftOpacitySlider.Value != val)
            {
                BoardToolbarLeftOpacitySlider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.BoardToolbarLeftOpacity = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnBoardToolbarLeftOpacityChanged(val);
        }

        private void BoardToolbarCenterOpacitySlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(BoardToolbarCenterOpacitySlider, BoardToolbarCenterOpacityText, "{0:F2}");
            if (!_isLoaded) return;
            var val = Math.Round(BoardToolbarCenterOpacitySlider.Value, 2);
            if (BoardToolbarCenterOpacitySlider.Value != val)
            {
                BoardToolbarCenterOpacitySlider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.BoardToolbarCenterOpacity = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnBoardToolbarCenterOpacityChanged(val);
        }

        private void BoardToolbarRightOpacitySlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(BoardToolbarRightOpacitySlider, BoardToolbarRightOpacityText, "{0:F2}");
            if (!_isLoaded) return;
            var val = Math.Round(BoardToolbarRightOpacitySlider.Value, 2);
            if (BoardToolbarRightOpacitySlider.Value != val)
            {
                BoardToolbarRightOpacitySlider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.BoardToolbarRightOpacity = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnBoardToolbarRightOpacityChanged(val);
        }

        private void BoardMenuOpacitySlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(BoardMenuOpacitySlider, BoardMenuOpacityText, "{0:F2}");
            if (!_isLoaded) return;
            var val = Math.Round(BoardMenuOpacitySlider.Value, 2);
            if (BoardMenuOpacitySlider.Value != val)
            {
                BoardMenuOpacitySlider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.BoardMenuOpacity = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnBoardMenuOpacityChanged(val);
        }

        private void ViewboxBlackBoardLeftScaleTransformValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(ViewboxBlackBoardLeftScaleTransformValueSlider, ViewboxBlackBoardLeftScaleText, "{0:F2}");
            if (!_isLoaded) return;
            var val = Math.Round(ViewboxBlackBoardLeftScaleTransformValueSlider.Value, 2);
            if (ViewboxBlackBoardLeftScaleTransformValueSlider.Value != val)
            {
                ViewboxBlackBoardLeftScaleTransformValueSlider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.ViewboxBlackBoardLeftScaleTransformValue = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnBlackBoardLeftScaleChanged(val);
        }

        private void ViewboxBlackBoardCenterScaleTransformValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(ViewboxBlackBoardCenterScaleTransformValueSlider, ViewboxBlackBoardCenterScaleText, "{0:F2}");
            if (!_isLoaded) return;
            var val = Math.Round(ViewboxBlackBoardCenterScaleTransformValueSlider.Value, 2);
            if (ViewboxBlackBoardCenterScaleTransformValueSlider.Value != val)
            {
                ViewboxBlackBoardCenterScaleTransformValueSlider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.ViewboxBlackBoardScaleTransformValue = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnBlackBoardScaleChanged(val);
        }

        private void ViewboxBlackBoardRightScaleTransformValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(ViewboxBlackBoardRightScaleTransformValueSlider, ViewboxBlackBoardRightScaleText, "{0:F2}");
            if (!_isLoaded) return;
            var val = Math.Round(ViewboxBlackBoardRightScaleTransformValueSlider.Value, 2);
            if (ViewboxBlackBoardRightScaleTransformValueSlider.Value != val)
            {
                ViewboxBlackBoardRightScaleTransformValueSlider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.ViewboxBlackBoardRightScaleTransformValue = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnBlackBoardRightScaleChanged(val);
        }
    }
}
