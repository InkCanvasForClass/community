// These replace diagnostic formatting/logging and unused input DTOs only.
// The classifier, source arbitration, filters, group decisions and sizes are the
// pinned project's original code, prepared in ignored artifacts by the runner.
internal static class ReferenceClock { internal static long Now; }
namespace TouchSuite.App
{
    internal static class Log { internal static void Info(string text) { } }
    internal static class Precision
    {
        internal static string Fmt(double value, int decimals = 1) => value.ToString();
        internal static string FmtFixed(double? value, int decimals = 0) => value?.ToString() ?? "";
    }
    public sealed class ContactRect { public System.Windows.Rect? DiuRect { get; init; } }
    public sealed class TouchSample
    {
        public string Source { get; init; }
        public int WidthLogical { get; init; }
        public int HeightLogical { get; init; }
        public int WidthLogMax { get; init; }
        public int HeightLogMax { get; init; }
        public System.Windows.Rect? DiuRect { get; init; }
        public IReadOnlyList<ContactRect> Contacts { get; init; }
    }
}
