using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Ink_Canvas;
using Ink_Canvas.Windows.SettingsViews.Helpers;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--interceptor-fixture")) return FloatingWindowNativeRegression.RunFixture();
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        string root = Path.Combine(Path.GetTempPath(), "ICC_SettingsRegression_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string previousRoot = Ink_Canvas.App.RootPath;
        var previousSettings = SettingsManager.Settings;
        try
        {
            Ink_Canvas.App.RootPath = root + Path.DirectorySeparatorChar;
            SettingsManager.Settings = new Settings();
            FloatingWindowNativeRegression.Run(Check);
            Check(Wait(SettingsManager.TrySaveSettingsToFileAsync()), "Initial settings could not be written");
            string path = Path.Combine(root, SettingsManager.SettingsFileName);
            Check(!ReadCompleted(path), "New install should start with incomplete setup");

            // Reproduce completing immediately after requesting a coalesced save.
            SettingsManager.SaveSettingsToFile();
            SettingsManager.Settings.Startup.HasShownOobe = true;
            SettingsManager.Settings.Canvas.HighlighterAlpha = 72;
            Check(Wait(SettingsManager.TrySaveSettingsToFileAsync()), "OOBE commit failed");
            Check(ReadCompleted(path), "OOBE commit returned before its completion flag reached disk");
            using (var saved = JsonDocument.Parse(File.ReadAllText(path)))
                Check(saved.RootElement.GetProperty("canvas").GetProperty("highlighterAlpha").GetDouble() == 72,
                    "Selected settings were lost during completion");
            Pump(250);
            Check(ReadCompleted(path), "An older delayed save reset the completed flag");
            var reloaded = Newtonsoft.Json.JsonConvert.DeserializeObject<Settings>(File.ReadAllText(path));
            Check(reloaded.Startup.HasShownOobe && reloaded.Canvas.HighlighterAlpha == 72,
                "Restart deserialization lost the completed wizard and selections");
            var cached = typeof(Ink_Canvas.App).GetProperty("CachedSettingsJson", BindingFlags.Static | BindingFlags.NonPublic);
            var cachedSettings = Newtonsoft.Json.JsonConvert.DeserializeObject<Settings>((string)cached.GetValue(null));
            Check(cachedSettings.Startup.HasShownOobe, "Startup cache retained incomplete OOBE state");

            // A genuinely unwritable destination must report failure instead of
            // letting the wizard close and pretending the settings were persisted.
            string blocked = Path.Combine(root, "blocked"); File.WriteAllText(blocked, "file, not directory");
            Ink_Canvas.App.RootPath = blocked + Path.DirectorySeparatorChar;
            Check(!Wait(SettingsManager.TrySaveSettingsToFileAsync()), "Write failure reported success");
            Console.WriteLine($"PASS: {_checks} settings persistence and native-window checks (production services and WPF dispatcher)");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            Ink_Canvas.App.RootPath = previousRoot;
            SettingsManager.Settings = previousSettings;
            app.Shutdown();
        }
    }

    private static bool ReadCompleted(string path)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        return json.RootElement.GetProperty("startup").GetProperty("hasShownOobe").GetBoolean();
    }

    private static bool Wait(Task<bool> task)
    {
        var limit = DateTime.UtcNow.AddSeconds(15);
        while (!task.IsCompleted && DateTime.UtcNow < limit) Pump(5);
        if (!task.IsCompleted) throw new TimeoutException("Settings writer did not complete");
        return task.GetAwaiter().GetResult();
    }

    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
}
