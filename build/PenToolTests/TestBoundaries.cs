namespace Ink_Canvas
{
    // An unshown window with a real InkCanvas and parent panel. No production UI is opened.
    public sealed class MainWindow : System.Windows.Window
    {
        public readonly System.Windows.Controls.InkCanvas inkCanvas = new();
        public readonly System.Windows.Controls.Grid Surface = new();
        public MainWindow() { Surface.Children.Add(inkCanvas); Content = Surface; }
    }
}
namespace Ink_Canvas.Helpers
{
    internal static class LogHelper
    {
        internal enum LogType { Info, Error }
        internal static void WriteLogToFile(string text, LogType type) { if (type == LogType.Error) System.Console.Error.WriteLine(text); }
    }
}
