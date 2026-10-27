// Only platform event construction and unrelated application services are replaced.
// The gate, coordinate forwarding, pipe framing and writers are production sources.
namespace Ink_Canvas
{
    internal static class App { internal static string RootPath; }
    internal static class MainWindow { internal static TestSettings Settings = new(); }
    internal sealed class TestSettings { public TestAdvanced Advanced = new(); }
    internal sealed class TestAdvanced { public bool IsLogEnabled = true; public bool IsSaveLogByDate; }
}
namespace Ink_Canvas.Helpers
{
    internal static class ProcessProtectionManager
    {
        internal static void WithWriteAccess(string path, System.Action action) => action();
    }
    internal static class DebugConsoleManager
    {
        internal static void WriteLine(string line, System.ConsoleColor? color) { }
    }
}
namespace Windows.Devices.Input
{
    public enum PointerDeviceType { Touch, Pen, Mouse }
    public sealed class PointerDevice { public PointerDeviceType PointerDeviceType { get; set; } }
}
namespace Windows.UI.Core
{
    public sealed class PointerEventArgs : System.EventArgs
    {
        public PointerPoint CurrentPoint { get; set; }
        public bool Handled { get; set; }
    }
    public sealed class PointerPoint
    {
        public uint PointerId { get; set; }
        public System.Windows.Point Position { get; set; }
        public Windows.Devices.Input.PointerDevice PointerDevice { get; set; }
        public PointerProperties Properties { get; set; } = new();
    }
    public sealed class PointerProperties { public ContactRect ContactRect { get; set; } }
    public struct ContactRect { public double Width, Height; }
}
namespace Windows.UI.Input.Inking { public sealed class InkStrokeInput { } }
namespace Windows.UI.Input.Inking.Core { public sealed class CoreInkIndependentInputSource { } }
