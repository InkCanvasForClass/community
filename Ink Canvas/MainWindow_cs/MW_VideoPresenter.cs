using Ink_Canvas.Helpers;
using System.Windows;
using System.Windows.Input;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        private void BtnToggleVideoPresenter_Click(object sender, MouseButtonEventArgs e)
        {
            ImageBlackboard_MouseUp(null, null);
            SoftwareLauncher.LaunchEasiCamera("希沃视频展台");
        }
    }
}
