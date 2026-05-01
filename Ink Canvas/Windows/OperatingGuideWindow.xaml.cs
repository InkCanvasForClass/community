using Ink_Canvas.Helpers;
using System;
using System.Windows;
using System.Windows.Input;

namespace Ink_Canvas
{
    /// <summary>
    /// Interaction logic for StopwatchWindow.xaml
    /// </summary>
    public partial class OperatingGuideWindow : Window
    {
        public OperatingGuideWindow()
        {
            InitializeComponent();
            RefreshTheme();
            AnimationsHelper.ShowWithSlideFromBottomAndFade(this, 0.25);
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        public void RefreshTheme()
        {
            try
            {
                ThemeHelper.ApplyTheme(this, MainWindow.Settings);
                InvalidateVisual();
            }
            catch (Exception)
            {
            }
        }
    }
}