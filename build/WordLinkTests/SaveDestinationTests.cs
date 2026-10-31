using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Ink_Canvas.Controls;
using Ink_Canvas.WordLink;

internal static partial class Program
{
    private static void SaveDestinationTests(string root)
    {
        string storage = Path.Combine(root, "save-destination");
        var source = new FrameSource { Frame = Frame() };
        source.Frame.SessionKey = new string('f', 64);
        var canvas = new InkCanvas();
        using var surface = new HwndSource(new HwndSourceParameters("Save destination fixture") { Width = 1200, Height = 1000 });
        surface.RootVisual = canvas; canvas.Measure(new Size(1200, 1000)); canvas.Arrange(new Rect(0, 0, 1200, 1000));
        var bar = new WordLectureBar();
        using var controller = new WordLectureController(canvas, bar, storage, () => true, () => false,
            _ => { }, action => action(), (_, _) => { }, source, _ => 10, automaticPolling: false);
        controller.Configure(true, true); Wait(controller.PollAsync());
        canvas.Strokes.Add(Stroke(150, 150)); Wait(controller.PollAsync());
        // Switch identity during the 150ms save coalescing window.
        source.Frame.DocumentKey = new string('e', 64); Wait(controller.PollAsync());
        string key = source.Frame.DocumentKey, revision = source.Frame.Revision;
        string path = Path.Combine(storage, key, revision + ".json");
        long deadline = Environment.TickCount64 + 3000;
        while (!File.Exists(path) && Environment.TickCount64 < deadline) Pump(10);
        Check(File.Exists(path), "Save As while saving never persisted the new document destination");
        var store = new WordAnnotationStore(storage);
        Check(Wait(store.LoadAsync(key, revision)).Pages[1].Count == 1, "Save As persisted stale ink");
        canvas.Strokes.Add(Stroke(200, 200)); Wait(controller.PollAsync());
        source.Frame.Pages[0].End++;
        Wait(controller.PollAsync());
        Check(bar.Status == "LayoutChanged", "Fixture did not enter the layout recovery path");
        bar.Continue(); Wait(controller.PollAsync());
        deadline = Environment.TickCount64 + 3000;
        while (Wait(store.LoadAsync(key, revision)).Pages.Count != 0 && Environment.TickCount64 < deadline) Pump(20);
        Pump(300);
        Check(Wait(store.LoadAsync(key, revision)).Pages.Count == 0, "Superseded background save overwrote the fresh layout session");
        var archives = Directory.GetFiles(Path.Combine(storage, key), "*.json").Where(p => p != path).ToArray();
        Check(archives.Length == 1 && Wait(store.LoadAsync(key, Path.GetFileNameWithoutExtension(archives[0]))).Pages[1].Count == 2,
            "Layout recovery failed to preserve the previous annotation snapshot");
    }
}
