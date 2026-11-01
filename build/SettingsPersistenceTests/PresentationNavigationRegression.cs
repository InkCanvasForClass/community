using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Ink_Canvas.Controls;

internal static class PresentationNavigationRegression
{
    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    internal static void Run(Action<bool, string> check)
    {
        var bar = new PPTNavBar { Visibility = Visibility.Collapsed };
        var window = new Window { Content = bar, Width = 300, Height = 650, Left = -20000, ShowActivated = false };
        try
        {
            window.Show(); bar.SetBarOpacity(.35);
            typeof(PPTNavBar).Assembly.GetType("Ink_Canvas.Helpers.AnimationsHelper")
                .GetMethod("ShowWithFadeIn", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { bar, .03 }); Pump(100);
            check(Math.Abs(bar.Opacity - .35) < .001, "Navigation fade held opacity at 1 instead of the setting");
            bar.SetBarOpacity(.55);
            check(bar.Opacity == .55, "Opacity change was hidden beneath an animation");
            bar.IsPreviewExpanded = true;
            check(bar.Opacity == 1, "Expanded catalogue became hard to read");
            bar.IsPreviewExpanded = false;
            check(bar.Opacity == .55, "Closing the catalogue lost the configured opacity");
            int requested = 0, released = 0;
            bar.PreviewThumbnailRequested += (_, _) => requested++;
            bar.PreviewThumbnailReleased += (_, _) => released++;
            bar.PreviewItems = Enumerable.Range(1, 2000).Select(page => new PPTNavBar.PreviewItem { SlideNumber = page }).ToArray();
            bar.CurrentSlide = 900; bar.TotalSlides = 2000; bar.IsPreviewExpanded = true;
            window.UpdateLayout(); Pump(100);
            check(requested > 0 && requested < 30, "Catalogue did not virtualize a large document");
            var list = (ListBox)bar.FindName("PreviewList");
            list.ScrollIntoView(bar.PreviewItems[1500]); window.UpdateLayout(); Pump(100);
            check(requested < 60 && released > 0, $"Catalogue recycling retained offscreen thumbnails (requested {requested}, released {released})");
            check(VirtualizingPanel.GetVirtualizationMode(list) == VirtualizationMode.Recycling,
                "Catalogue discarded its recycling policy");
            bar.IsPreviewExpanded = false; bar.PreviewItems = null; window.UpdateLayout(); Pump(100);
            check(released == requested, "Closing the catalogue retained visible thumbnail leases");
        }
        finally { window.Close(); }
    }
}
