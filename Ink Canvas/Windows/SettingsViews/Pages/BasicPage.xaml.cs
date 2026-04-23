using iNKORE.UI.WPF.Modern.Controls;
using System.Collections.ObjectModel;
using System.Windows;
using SWC = System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public class SubPageNavItem
    {
        public string Header { get; set; }
        public string Description { get; set; }
        public string PageTag { get; set; }
        public string IconGlyph { get; set; }
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
                                    string glyph = ExtractIconGlyph(childItem);
                                    string description = SWC.ToolTipService.GetToolTip(childItem) as string
                                        ?? $"点击跳转到{childItem.Content}";

                                    _subPageItems.Add(new SubPageNavItem
                                    {
                                        Header = childItem.Content?.ToString() ?? "",
                                        Description = description,
                                        PageTag = childTag,
                                        IconGlyph = glyph
                                    });
                                }
                            }
                        }
                        break;
                    }
                }
            }
        }

        private string ExtractIconGlyph(NavigationViewItem navItem)
        {
            if (navItem.Icon is FontIcon fontIcon)
                return fontIcon.Glyph ?? "\uE713";

            if (navItem.Icon is SymbolIcon symbolIcon)
                return char.ConvertFromUtf32((int)symbolIcon.Symbol);

            return "\uE713";
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
