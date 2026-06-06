using System.Windows;
using System.Windows.Input;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class BoardToolbarInputDialog : Window
    {
        public string InputText { get; private set; }

        public BoardToolbarInputDialog(string prompt, string title, string defaultText)
        {
            InitializeComponent();
            Title = title;
            LabelPrompt.Text = prompt;
            TextBoxInput.Text = defaultText;
            TextBoxInput.SelectAll();
            TextBoxInput.Focus();
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            InputText = TextBoxInput.Text;
            DialogResult = true;
            Close();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TextBoxInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ButtonOK_Click(sender, e);
        }
    }
}
