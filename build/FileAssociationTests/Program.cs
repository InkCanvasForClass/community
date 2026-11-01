using System.IO;
using System.Reflection;
using System.Runtime.Loader;

// Run against a built host so the production IPC code and its dependencies are exercised.
string hostDirectory = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string path = Path.Combine(hostDirectory, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
string originalTmp = Environment.GetEnvironmentVariable("TMP");
string originalTemp = Environment.GetEnvironmentVariable("TEMP");
string directory = Path.Combine(Path.GetTempPath(), "ICC_FileOpenTests_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
int checks = 0;
void Check(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
    checks++;
}
try
{
    Environment.SetEnvironmentVariable("TMP", directory);
    Environment.SetEnvironmentVariable("TEMP", directory);
    Check(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) == directory, "Test temp isolation failed");
    var host = Assembly.LoadFrom(Path.Combine(hostDirectory, "InkCanvasForClass.dll"));
    host.GetType("Ink_Canvas.App", true).GetField("RootPath").SetValue(null, directory);
    var sdk = Assembly.Load("InkCanvas.PluginSdk");
    var tools = sdk.GetType("Ink_Canvas.Plugins.PluginInkTool", true);
    Check(Enum.IsDefined(tools, "Mouse") &&
        !Equals(Enum.Parse(tools, "Mouse"), Enum.Parse(tools, "Select")),
        "Deployed host SDK has no desktop mouse capability distinct from lasso selection");
    var association = host.GetType("Ink_Canvas.Helpers.FileAssociationManager", true);
    object Call(string name, params object[] parameters) => association
        .GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, parameters);

    string document = Path.Combine(directory, "含空格 document.pdf");
    File.WriteAllText(document, "IPC path fixture");
    Check((bool)Call("TrySendPluginFileToExistingInstance", document), "Open request was not queued");
    string queued = Directory.GetFiles(directory, "InkCanvasPluginFileOpen_*.tmp").Single();
    Thread.Sleep(1300);
    Check(File.Exists(queued), "Sender discarded a request while the host was busy");
    Check(File.ReadAllText(queued) == document, "Unicode document path was corrupted");
    string stale = Path.Combine(directory, "InkCanvasPluginFileOpen_stale.tmp");
    File.WriteAllText(stale, document);
    File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddMinutes(-10));
    Call("CleanupStaleIpcFiles");
    Check(File.Exists(queued), "Cold startup discarded a fresh request");
    Check(!File.Exists(stale), "Expired request survived cleanup");
    Check(Call("GetPluginFileFromArgs", new object[] { new[] { "--watchdog", "missing.pdf" } }) == null,
        "Startup flags or nonexistent files were treated as documents");
    Console.WriteLine($"PASS: {checks} production file-association checks");
}
finally
{
    Environment.SetEnvironmentVariable("TMP", originalTmp);
    Environment.SetEnvironmentVariable("TEMP", originalTemp);
}
