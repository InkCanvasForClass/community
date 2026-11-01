using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using Ink_Canvas.Helpers;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }

    [STAThread]
    private static void Main()
    {
        var first = new Rect(200, 0, 600, 800);
        var second = new Rect(200, 800, 600, 800);
        var ink = new Rect(-500, -100, 2500, 2000);
        var pages = new[] { first, second };
        Rect capture = PluginPageInkBounds.ExpandHorizontally(first, pages, ink);
        Check(capture.Contains(new Point(-200, 100)) && capture.Contains(new Point(1500, 100)), "Left/right margin ink was excluded");
        Check(!capture.Contains(new Point(300, 900)), "Margin expansion stole the next page's ink");
        var stroke = new Stroke(new StylusPointCollection(new[] { new StylusPoint(-100, 80), new StylusPoint(50, 160) }));
        var collection = new StrokeCollection { stroke };
        for (int i = 0; i < 50; i++)
        {
            var move = new Matrix(1, 0, 0, 1, 0, -37);
            collection.Transform(move, false); first.Offset(0, -37); second.Offset(0, -37);
            capture = PluginPageInkBounds.ExpandHorizontally(first, new[] { first, second }, collection.GetBounds());
            Check(collection[0].GetClipResult(capture).Count == 1, "Margin ink was lost after repeated scroll/restore");
        }
        var left = new Rect(50, 0, 400, 700);
        var right = new Rect(550, 0, 400, 700);
        Rect leftCapture = PluginPageInkBounds.ExpandHorizontally(left, new[] { left, right }, ink);
        Rect rightCapture = PluginPageInkBounds.ExpandHorizontally(right, new[] { left, right }, ink);
        Check(leftCapture.Right == 500 && rightCapture.Left == 500, "Two-page margins overlap or leave a gutter hole");
        Check(!leftCapture.Contains(new Point(800, 100)) && !rightCapture.Contains(new Point(100, 100)), "Two-page margin storage duplicated another page's ink");
        Check(PluginPageInkBounds.ExpandHorizontally(left, new[] { left }, Rect.Empty) == left, "An empty canvas changed page geometry");
        Console.WriteLine($"PASS: {_checks} page-margin checks");
    }
}
