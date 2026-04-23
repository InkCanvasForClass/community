using iNKORE.UI.WPF.Modern.Common.IconKeys;
using iNKORE.UI.WPF.Modern.Controls;
using System.Collections.ObjectModel;
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
                        string glyph = ExtractIconGlyph(navItem);
                        string description = System.Windows.Controls.ToolTipService.GetToolTip(navItem) as string
                            ?? $"点击跳转到{navItem.Content}";

                        _navItems.Add(new QuickNavItem
                        {
                            Header = navItem.Content?.ToString() ?? "",
                            Description = description,
                            PageTag = tag,
                            IconGlyph = glyph
                        });
                    }
                }
            }
        }

        private string ExtractIconGlyph(NavigationViewItem navItem)
        {
            if (navItem.Icon is FontIcon fontIcon)
            {
                return fontIcon.Glyph ?? "\uE713";
            }

            if (navItem.Icon is SymbolIcon symbolIcon)
            {
                return char.ConvertFromUtf32((int)symbolIcon.Symbol);
            }

            return "\uE713";
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
