using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Ink_Canvas.Helpers;

internal static class Program
{
    private static int _checks;
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); _checks++; }
    private static BitmapSource Bitmap(int width = 12, int height = 8) =>
        BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, new byte[width * height * 4], width * 4);
    private static void Until(Func<bool> done)
    {
        var timeout = Stopwatch.StartNew();
        while (!done() && timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); Dispatcher.PushFrame(frame);
        }
        Check(done(), "Asynchronous operation did not finish");
    }
    [STAThread]
    private static int Main()
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        try
        {
            int renders = 0;
            using var cache = new PresentationPreviewCache(100, (_, _) =>
            { renders++; return Task.FromResult(Bitmap(512, 256)); });
            Check(renders == 0 && cache.Items.Count == 100, "Creating a catalogue eagerly rendered pages");
            cache.Retain(1);
            Check(renders == 1 && cache.Items[0].Thumbnail.IsFrozen, "Thumbnail was not published frozen");
            Check(cache.Items[0].Thumbnail.PixelWidth == 256 && cache.Items[0].Thumbnail.PixelHeight == 128,
                "Large preview exceeded its memory bound");
            cache.Retain(1); cache.Release(1);
            Check(renders == 1, "Two visible controls rendered the same page twice");
            for (int page = 2; page <= 40; page++) { cache.Retain(page); cache.Release(page); }
            Check(cache.ResidentCount == 16 && cache.Items[0].Thumbnail != null,
                "LRU evicted a visible page or grew beyond its bound");
            cache.Release(1);
            using var other = new PresentationPreviewCache(100, (_, _) => Task.FromResult(Bitmap()));
            Check(cache.Owns(cache.Items[0]) && !other.Owns(cache.Items[0]), "Previous-document events matched a new cache");
            cache.Retain(0); cache.Release(101);

            int active = 0, peak = 0, completed = 0;
            using var serial = new PresentationPreviewCache(5, async (_, token) =>
            {
                active++; peak = Math.Max(active, peak);
                try { await Task.Delay(30, token); completed++; return Bitmap(); }
                finally { active--; }
            });
            serial.Retain(1); serial.Retain(2); serial.Retain(3);
            serial.Release(2);
            Until(() => completed == 2);
            Check(peak == 1 && serial.Items[1].Thumbnail == null, "Recycled thumbnails rendered or work ran concurrently");
            var late = new TaskCompletionSource<BitmapSource>();
            using var stale = new PresentationPreviewCache(1, (_, _) => late.Task);
            stale.Retain(1); stale.Release(1); stale.Retain(1);
            late.SetResult(Bitmap());
            Until(() => stale.Items[0].Thumbnail != null);
            Check(stale.ResidentCount == 1, "A cancelled request removed its replacement");
            var disposedResult = new TaskCompletionSource<BitmapSource>();
            var disposed = new PresentationPreviewCache(1, (_, _) => disposedResult.Task);
            disposed.Retain(1); disposed.Dispose(); disposedResult.SetResult(Bitmap());
            Until(() => disposedResult.Task.IsCompleted);
            Check(disposed.Items[0].Thumbnail == null, "Disposed catalogue published a stale result");

            var surface = new Grid(); var toolbar = new Grid(); surface.Children.Add(toolbar);
            surface.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.Zero));
            toolbar.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(.7, TimeSpan.Zero));
            ToolbarOpacityPresentation.Apply(toolbar, surface, .4);
            Check(surface.Opacity == .4 && toolbar.Opacity == 1, "Animation overrode opacity or parent and child multiplied it");
            ToolbarOpacityPresentation.Apply(toolbar, surface, double.NaN);
            Check(surface.Opacity == 1, "Invalid opacity propagated into WPF");
            ToolbarOpacityPresentation.Apply(toolbar, surface, 0);
            Check(surface.Opacity == .1, "Toolbar became impossible to see");
            Console.WriteLine($"PASS: {_checks} presentation preview/opacity checks"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
