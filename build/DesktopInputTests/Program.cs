using System.IO;
using System.Windows;
using Ink_Canvas.Windows.SettingsViews.Helpers;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--desktop-click-fixture")) return DesktopInputRegression.RunFixture();
        using var output = new StreamWriter(Path.Combine(AppContext.BaseDirectory, "desktop-input-result.txt"), false) { AutoFlush = true };
        Console.SetOut(output); Console.SetError(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Ink_Canvas.App.RootPath = Path.Combine(Path.GetTempPath(), "ICC_Input_" + Guid.NewGuid().ToString("N")) + Path.DirectorySeparatorChar;
        SettingsManager.Settings = new Ink_Canvas.Settings();
        int checks = 0;
        try {
            DesktopInputRegression.Run((ok, message) => {
                if (!ok) throw new InvalidOperationException(message);
                checks++;
            });
            Console.WriteLine($"PASS: {checks} cross-process desktop input checks"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { app.Shutdown(); }
    }
}
