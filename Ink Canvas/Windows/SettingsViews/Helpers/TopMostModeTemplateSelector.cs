using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Helpers
{
    public class TopMostModeSelectionItem
    {
    }

    public class TopMostModeButtonItem
    {
        public string ButtonHeader { get; set; }
        public string ButtonContent { get; set; }
        public bool RestartAsAdmin { get; set; }
    }

    public class TopMostModeTemplateSelector : DataTemplateSelector
    {
        public DataTemplate SelectionTemplate { get; set; }
        public DataTemplate ButtonTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is TopMostModeSelectionItem) return SelectionTemplate;
            if (item is TopMostModeButtonItem) return ButtonTemplate;
            return null;
        }
    }
}
