using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Ink_Canvas.WordLink;

internal static partial class Program
{
    private static void WordInputRegionTests()
    {
        var boundary = new WordBoundaryToolState();
        Check(boundary.Update(false, false, false, "pen", 0)?.Mode == "cursor", "Leaving Word did not select the desktop cursor");
        Check(boundary.Update(false, false, false, "cursor", 0) == null, "Cursor boundary repeatedly switched tools");
        Check(boundary.Update(true, true, false, "cursor", 0) == null, "Toolbar hover restored a writing tool");
        Check(boundary.Update(true, false, false, "cursor", 0)?.Mode == "pen", "Returning to Word did not restore the previous pen");
        Check(boundary.Update(false, false, true, "pen", 0) == null, "A stroke was interrupted at the viewport edge");
        Check(boundary.Update(false, false, false, "eraserByStrokes", 0)?.Mode == "cursor", "Eraser did not yield outside Word");
        boundary.UserSelectedTool();
        Check(boundary.Update(true, false, false, "cursor", 0) == null, "An explicit mouse selection was overridden");
        boundary.Update(false, false, false, "pen", 9);
        Check(boundary.Update(true, false, false, "cursor", 0)?.Shape == 9, "Returning to Word lost the selected shape");
        using var source = new HwndSource(new HwndSourceParameters("Word native input region fixture")
        { Width = 800, Height = 600, WindowStyle = unchecked((int)0x80000000) });
        var region = new WordWindowInputRegion();
        IntPtr sample = RegionCreateRect(0, 0, 0, 0);
        try
        {
            var document = new Rect(100, 80, 400, 300);
            var toolbar = new Rect(200, 500, 300, 50);
            region.Update(source.Handle, new[] { document, toolbar });
            Check(region.IsApplied && RegionGetWindow(source.Handle, sample) != 0, "Word input region was not installed on its HWND");
            Check(RegionContains(sample, 150, 100), "Document no longer accepts ink");
            Check(RegionContains(sample, 250, 520), "ICCCE toolbar was cut out of the native input region");
            Check(!RegionContains(sample, 50, 100) && !RegionContains(sample, 150, 30)
                && !RegionContains(sample, 700, 450), "Desktop or Word ribbon still intercepted annotation input");
            document.Offset(100, 30);
            region.Update(source.Handle, new[] { document, toolbar });
            RegionGetWindow(source.Handle, sample);
            Check(!RegionContains(sample, 150, 100) && RegionContains(sample, 250, 150), "Input region did not follow the moved Word viewport");
            region.Update(source.Handle, new[] { toolbar });
            RegionGetWindow(source.Handle, sample);
            Check(!RegionContains(sample, 250, 150) && RegionContains(sample, 250, 520),
                "Inactive Word failed to retain only toolbar input");
            region.Update(source.Handle, Array.Empty<Rect>());
            RegionGetWindow(source.Handle, sample);
            Check(!RegionContains(sample, 250, 520), "Empty region still captured input");
            region.Restore(); region.Restore();
            Check(!region.IsApplied && RegionGetWindow(source.Handle, sample) == 0, "Leaving Word retained a clipped desktop window");
        }
        finally { region.Restore(); RegionDelete(sample); }
    }

    [DllImport("gdi32.dll", EntryPoint = "CreateRectRgn")] private static extern IntPtr RegionCreateRect(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll", EntryPoint = "DeleteObject")] private static extern bool RegionDelete(IntPtr region);
    [DllImport("gdi32.dll", EntryPoint = "PtInRegion")] private static extern bool RegionContains(IntPtr region, int x, int y);
    [DllImport("user32.dll", EntryPoint = "GetWindowRgn")] private static extern int RegionGetWindow(IntPtr window, IntPtr region);
}
