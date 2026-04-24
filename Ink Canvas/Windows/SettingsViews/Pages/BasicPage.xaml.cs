using iNKORE.UI.WPF.Modern.Common.IconKeys;
using iNKORE.UI.WPF.Modern.Controls;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using SWC = System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public class SubPageNavItem
    {
        public string Header { get; set; }
        public string Description { get; set; }
        public string PageTag { get; set; }
        public string IconGlyph { get; set; }
        public FontFamily IconFontFamily { get; set; }
    }

    public partial class BasicPage : SWC.Page
    {
        private readonly ObservableCollection<SubPageNavItem> _subPageItems = new();

        public BasicPage()
        {
            InitializeComponent();
            SubPageItems.ItemsSource = _subPageItems;
            Loaded += BasicPage_Loaded;
        }

        private void BasicPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSubPages();
        }

        private void LoadSubPages()
        {
            _subPageItems.Clear();

            var settingsWindow = Window.GetWindow(this) as SettingsWindow;
            if (settingsWindow == null) return;

            var navView = settingsWindow.GetNavigationView();
            if (navView == null) return;

            foreach (var item in navView.MenuItems)
            {
                if (item is NavigationViewItem navItem)
                {
                    string tag = navItem.Tag as string;
                    if (tag == "BasicPage" && navItem.MenuItems.Count > 0)
                    {
                        foreach (var child in navItem.MenuItems)
                        {
                            if (child is NavigationViewItem childItem)
                            {
                                string childTag = childItem.Tag as string;
                                if (!string.IsNullOrEmpty(childTag))
                                {
                                    (string glyph, FontFamily fontFamily) = ExtractIconInfo(childItem);
                                    string description = SWC.ToolTipService.GetToolTip(childItem) as string
                                        ?? $"点击跳转到{childItem.Content}";

                                    _subPageItems.Add(new SubPageNavItem
                                    {
                                        Header = childItem.Content?.ToString() ?? "",
                                        Description = description,
                                        PageTag = childTag,
                                        IconGlyph = glyph,
                                        IconFontFamily = fontFamily
                                    });
                                }
                            }
                        }
                        break;
                    }
                }
            }
        }

        private (string glyph, FontFamily fontFamily) ExtractIconInfo(NavigationViewItem navItem)
        {
            if (navItem.Icon is FontIcon fontIcon)
            {
                string glyph = fontIcon.Glyph ?? "\uE713";
                FontFamily fontFamily = fontIcon.FontFamily ?? new FontFamily("Segoe Fluent Icons");

                if (fontIcon.Icon != null)
                {
                    string iconStr = fontIcon.Icon.ToString();
                    if (iconStr.Contains("_20_"))
                    {
                        string key24 = iconStr.Replace("_20_", "_24_");
                        var field = typeof(FluentSystemIcons).GetField(key24, BindingFlags.Public | BindingFlags.Static);
                        if (field != null)
                        {
                            var value = field.GetValue(null);
                            if (value is FontIconData data24)
                            {
                                glyph = data24.Glyph ?? glyph;
                                fontFamily = data24.FontFamily ?? fontFamily;
                            }
                        }
                    }
                }

                return (glyph, fontFamily);
            }

            if (navItem.Icon is SymbolIcon symbolIcon)
                return (char.ConvertFromUtf32((int)symbolIcon.Symbol), new FontFamily("Segoe Fluent Icons"));

            return ("\uE713", new FontFamily("Segoe Fluent Icons"));
        }

        private void SubPageCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string pageTag)
            {
                var settingsWindow = Window.GetWindow(this) as SettingsWindow;
                settingsWindow?.NavigateToPage(pageTag);
            }
        }
    }
}
