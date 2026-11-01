using System.Windows;
using System.Windows.Ink;
namespace Ink_Canvas.Helpers
{
    internal static class LogHelper
    {
        internal enum LogType { Info, Warning, Error }
        internal static void WriteLogToFile(string text, LogType type)
        {
            System.Diagnostics.Debug.WriteLine(text);
            if (System.Environment.GetEnvironmentVariable("ICC_WORD_TEST_TRACE") == "1")
                System.Console.WriteLine(text);
        }
    }
    internal static class ProcessProtectionManager { internal static void WithWriteAccess(string path, System.Action action) => action(); }
    // Laser rendering itself is covered by PenToolTests; this boundary supplies its
    // stable transient-stroke identity to the document projection tests.
    internal static class InkFadeManager
    {
        internal static readonly System.Guid LaserId = new("A69B0C9D-9A3A-4F91-8BE3-8E2DBB9AD4F7");
        internal static bool IsLaserStroke(Stroke stroke) => stroke.ContainsPropertyData(LaserId);
    }
}
namespace Ink_Canvas.Controls
{
    internal sealed class WordLectureBar : FrameworkElement
    {
        internal event System.Action<int> Navigate;
        internal event System.Action Resume;
        internal event System.Action<int> Zoom;
        internal bool ShowNavigation { get; set; }
        internal string Status;
        internal int Page;
        internal void SetState(string status, string document, int page, int total, bool resume, int zoom = 0) { Status = status; Page = page; Visibility = Visibility.Visible; }
        internal void Go(int delta) => Navigate?.Invoke(delta);
        internal void ChangeZoom(int delta) => Zoom?.Invoke(delta);
        internal void Continue() => Resume?.Invoke();
    }
}
