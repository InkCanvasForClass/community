using Ink_Canvas.Controls.Toolbar.BoardToolbar;
using System.Collections.Generic;
using System.Windows;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class BoardToolbarComponentPickerDialog : Window
    {
        public string SelectedId { get; private set; }

        public BoardToolbarComponentPickerDialog(IReadOnlyList<IBoardToolbarItem> items)
        {
            InitializeComponent();
            ListBoxComponents.ItemsSource = items;
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            var item = ListBoxComponents.SelectedItem as IBoardToolbarItem;
            if (item != null)
                SelectedId = item.Id;
            DialogResult = true;
            Close();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
