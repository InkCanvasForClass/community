using iNKORE.UI.WPF.Modern.Common.IconKeys;
using iNKORE.UI.WPF.Modern.Controls;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public class QuickNavItem
    {
        public string Header { get; set; }
        public string Description { get; set; }
        public string PageTag { get; set; }
        public string IconGlyph { get; set; }
        public FontFamily IconFontFamily { get; set; }
    }

    public partial class HomePage
    {
        private readonly ObservableCollection<QuickNavItem> _navItems = new();

        public HomePage()
        {
            InitializeComponent();
            QuickNavItems.ItemsSource = _navItems;
            Loaded += HomePage_Loaded;
        }

        private void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadNavigationItems();
        }

        private void LoadNavigationItems()
        {
            _navItems.Clear();

            var settingsWindow = Window.GetWindow(this) as SettingsWindow;
            if (settingsWindow == null) return;

            var navView = settingsWindow.GetNavigationView();
            if (navView == null) return;

            CollectNavItems(navView.MenuItems);
            CollectNavItems(navView.FooterMenuItems);
        }

        private void CollectNavItems(System.Collections.IEnumerable items)
        {
            foreach (var item in items)
            {
                if (item is NavigationViewItem navItem)
                {
                    string tag = navItem.Tag as string;
                    if (!string.IsNullOrEmpty(tag) && tag != "HomePage")
                    {
                        (string glyph, FontFamily fontFamily) = ExtractIconInfo(navItem);
                        string description = ToolTipService.GetToolTip(navItem) as string
                            ?? $"点击跳转到{navItem.Content}";

                        _navItems.Add(new QuickNavItem
                        {
                            Header = navItem.Content?.ToString() ?? "",
                            Description = description,
                            PageTag = tag,
                            IconGlyph = glyph,
                            IconFontFamily = fontFamily
                        });
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

        private void QuickNavCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string pageTag)
            {
                var settingsWindow = Window.GetWindow(this) as SettingsWindow;
                settingsWindow?.NavigateToPage(pageTag);
            }
        }
    }
}
