using System.Windows;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class BoardToolbarAreaPickerDialog : Window
    {
        public string SelectedArea { get; private set; }
        public string TargetGroup { get; private set; }

        public BoardToolbarAreaPickerDialog()
        {
            InitializeComponent();
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            if (RadioLeft.IsChecked == true) SelectedArea = "left";
            else if (RadioCenter.IsChecked == true) SelectedArea = "center";
            else SelectedArea = "right";

            var group = TextBoxGroup.Text?.Trim();
            TargetGroup = string.IsNullOrEmpty(group) ? null : group;

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
