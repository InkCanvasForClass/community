using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows
{
    public enum StrokeFileOpenTarget { Screen, Whiteboard, Booth }

    public partial class StrokeFileOpenWindow : Window
    {
        public StrokeFileOpenTarget? SelectedTarget { get; private set; }

        public StrokeFileOpenWindow(string filePath)
        {
            InitializeComponent();
            FileNameText.Text = Path.GetFileName(filePath);
            FileNameText.ToolTip = filePath;
            MaxHeight = SystemParameters.WorkArea.Height;
        }

        private void Destination_Click(object sender, RoutedEventArgs e)
        {
            SelectedTarget = (StrokeFileOpenTarget)((Button)sender).Tag;
            DialogResult = true;
        }
    }
}
