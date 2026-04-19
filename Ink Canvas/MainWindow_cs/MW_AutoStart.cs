using Ink_Canvas.Windows.SettingsViews.Helpers;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        public static bool StartAutomaticallyCreate(string exeName) => AutoStartHelper.StartAutomaticallyCreate(exeName);

        public static bool StartAutomaticallyDel(string exeName) => AutoStartHelper.StartAutomaticallyDel(exeName);
    }
}
