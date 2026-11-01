using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Ink_Canvas.Controls;
using Ink_Canvas.WordLink;
using InkCanvasWordLink;
using InkCanvasWordLinkHost;

internal static partial class Program
{
    private static void RestartRecoveryTests(string root)
    {
        string storage = Path.Combine(root, "restart-recovery");
        var canvas = new InkCanvas();
        using var surface = new HwndSource(new HwndSourceParameters("Word restart fixture") { Width = 1200, Height = 1000 });
        surface.RootVisual = canvas; canvas.Measure(new Size(1200, 1000)); canvas.Arrange(new Rect(0, 0, 1200, 1000));
        var frame = Frame();
        WordLectureController Open(WordLectureBar bar, WordFrame value) => new(canvas, bar, storage, () => true, () => false,
            _ => { }, change => change(), (_, _) => { }, new FrameSource { Frame = value }, _ => 10, automaticPolling: false);
        using (var first = Open(new WordLectureBar(), frame))
        {
            first.Configure(true, true); Wait(first.PollAsync());
            var p = canvas.PointFromScreen(new Point(150, 150)); canvas.Strokes.Add(Stroke(p.X, p.Y));
            Check(first.FlushForRestart(), "Restart handoff could not flush the final stroke");
            Check(Wait(new WordAnnotationStore(storage).LoadAsync(frame.DocumentKey, frame.Revision)).Pages[1].Count == 1,
                "A new instance could read annotations before the old instance saved them");
            // Exit before the queued local commit and 150ms background save.
        }
        using (var second = Open(new WordLectureBar(), Frame()))
        {
            second.Configure(true, true); Wait(second.PollAsync());
            Check(canvas.Strokes.Count == 1, "An immediate restart lost the final unscheduled stroke");
        }
        var bar = new WordLectureBar(); var changed = Frame(); changed.Revision = new string('e', 64);
        using var third = Open(bar, changed);
        third.Configure(true, true); Wait(third.PollAsync());
        var store = new WordAnnotationStore(storage);
        Check(canvas.Strokes.Count == 0 && Wait(store.HistoryAsync(frame.DocumentKey, changed.Revision)).Any(v => v.Revision == frame.Revision),
            "Switching revisions lost the original annotation file");
        var original = File.ReadAllBytes(Path.Combine(storage, frame.DocumentKey, frame.Revision + ".json"));
        Check(Wait(store.LoadAsync(frame.DocumentKey, frame.Revision)).Pages[1].Count == 1,
            "The preserved annotation version could not be read");
        Check(original.SequenceEqual(File.ReadAllBytes(Path.Combine(storage, frame.DocumentKey, frame.Revision + ".json"))),
            "History recovery overwrote the original annotation version");
        Check(WordProcessPolicy.IsSupported("wps") && WordProcessPolicy.IsSupported("WINWORD")
            && !WordProcessPolicy.IsSupported("wpp") && !WordProcessPolicy.IsSupported("et") && !WordProcessPolicy.IsSupported("wpscloudsvr"),
            "WPS process policy admitted a presentation, spreadsheet or service");
        var named = new OfficeObject(new NamedOfficeFixture(), true);
        Check(named.IsRange && named.Get<float>("Size") == 12.5f && named.Get<string>("FontName") == "Fixture",
            "Named WPS dispatch failed range detection or numeric/font member conversion");
    }
    public sealed class NamedOfficeFixture
    {
        public int Start => 0;
        public int StoryType => 1;
        public double Size => 12.5;
        public string Name => "Fixture";
    }
}
