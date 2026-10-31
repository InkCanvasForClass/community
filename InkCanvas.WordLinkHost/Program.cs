using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;
using InkCanvasWordLink;

namespace InkCanvasWordLinkHost;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] != "--parent" || !int.TryParse(args[1], out int parentId)) return 2;
        Process parent;
        try { parent = Process.GetProcessById(parentId); } catch { return 2; }
        string previousStage = null;
        long stageStarted = 0;
        void TraceStage(string stage)
        {
            long now = Environment.TickCount64;
            if (previousStage != null && now - stageStarted >= 150)
                Console.Error.WriteLine($"[WordLinkHost] slow-phase={previousStage}, elapsed={now - stageStarted}ms");
            previousStage = stage; stageStarted = now;
            if (stage != null) Console.Error.WriteLine("[WordLinkHost] phase=" + stage);
        }
        using (parent)
        using (var apartment = new OfficeApartment())
        using (var filter = new OfficeMessageFilter())
        using (var watch = new Timer(_ => { try { if (parent.HasExited) Environment.Exit(0); } catch { Environment.Exit(0); } }, null, 1000, 1000))
        // Always publish the current phase to the parent's bounded stderr drain.
        // It is written to the application log only when a request actually times
        // out; no document text is sent and normal queries do not add disk logs.
        using (var observer = new WordObserver(TraceStage))
        {
            var loop = new StaRequestLoop(line =>
            {
                WordFrame frame;
                try
                {
                    var request = JsonSerializer.Deserialize<WordRequest>(line);
                    if (request?.Version == 1 && request.ReleaseDocument)
                    {
                        observer.ResetConnection();
                        frame = new WordFrame { Status = "Released" };
                    }
                    else frame = request?.Version != 1 ? new WordFrame { Status = "Unavailable" }
                        : request.Handshake ? new WordFrame { Status = "HostReady" } : observer.Read(request);
                    if (frame.Status == "Locating" && Environment.GetEnvironmentVariable("ICC_WORD_LINK_TRACE") == "1")
                        Console.Error.WriteLine("[WordLinkHost] " + observer.LastDiagnostic);
                }
                catch (COMException ex) when (OfficeMessageFilter.IsBusy(ex.HResult))
                { frame = new WordFrame { Status = "Busy" }; }
                catch (Exception ex)
                {
                    observer.ResetConnection();
                    Console.Error.WriteLine($"[WordLinkHost] {ex.GetType().Name}: 0x{ex.HResult:X8}");
                    frame = new WordFrame { Status = "Unavailable" };
                }
                TraceStage(null);
                Console.WriteLine(JsonSerializer.Serialize(frame));
                Console.Out.Flush();
            });
            loop.Run();
            return 0;
        }
    }
}
