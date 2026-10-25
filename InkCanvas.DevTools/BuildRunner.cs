using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InkCanvas.DevTools
{
    /// <summary>
    /// 构建模式
    /// </summary>
    public enum BuildMode
    {
        Debug,
        Release,
        Publish
    }

    /// <summary>
    /// 可选构建的项目定义。
    /// </summary>
    public sealed class ProjectDefinition
    {
        /// <summary>下拉框显示名称。</summary>
        public string DisplayName { get; }

        /// <summary>项目目录（相对仓库根目录），空字符串表示在仓库根目录构建解决方案。</summary>
        public string ProjectDirectory { get; }

        /// <summary>是否产出可执行文件（false 表示只产出 dll）。</summary>
        public bool IsExecutable { get; }

        /// <summary>Debug 产物搜索根目录（相对项目目录，不含目标框架层）。</summary>
        public string DebugOutputDirectory { get; }

        /// <summary>Release 产物搜索根目录（相对项目目录，不含目标框架层）。</summary>
        public string ReleaseOutputDirectory { get; }

        /// <summary>Publish 产物搜索根目录（相对项目目录，实际产物位于其下 publish 子目录）。</summary>
        public string PublishOutputDirectory { get; }

        /// <summary>可执行文件名。</summary>
        public string ExecutableName { get; }

        public ProjectDefinition(string displayName, string projectDirectory, bool isExecutable,
                                 string debugOutputDirectory, string releaseOutputDirectory,
                                 string publishOutputDirectory, string executableName)
        {
            DisplayName = displayName;
            ProjectDirectory = projectDirectory;
            IsExecutable = isExecutable;
            DebugOutputDirectory = debugOutputDirectory;
            ReleaseOutputDirectory = releaseOutputDirectory;
            PublishOutputDirectory = publishOutputDirectory;
            ExecutableName = executableName;
        }

        /// <summary>唯一标识，用于日志与进程名。</summary>
        public string ProcessName =>
            IsExecutable ? Path.GetFileNameWithoutExtension(ExecutableName) : null;

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// 负责调用 dotnet CLI 进行编译/发布，并启动目标程序。
    /// </summary>
    public class BuildRunner
    {
        /// <summary>仓库根目录（community），即解决方案所在目录。</summary>
        public string RepositoryRoot { get; }

        /// <summary>
        /// 可选项目列表（均使用 dotnet build 可构建的 SDK 风格项目）。
        /// 输出目录只写到配置/平台层，目标框架目录（net6.0 / net10.0 ...）在运行时自动查找，
        /// 以便同一份代码在 net6 / net10 分支都能正确定位产物。
        /// </summary>
        public static IReadOnlyList<ProjectDefinition> Projects { get; } = new[]
        {
            new ProjectDefinition(
                "InkCanvasForClass（主程序）",
                "Ink Canvas",
                true,
                Path.Combine("bin", "Debug", "AnyCPU"),
                Path.Combine("bin", "Release", "AnyCPU"),
                Path.Combine("bin", "Release", "AnyCPU"),
                "InkCanvasForClass.exe"),

            new ProjectDefinition(
                "InkCanvas.Controls（DLL）",
                "InkCanvas.Controls",
                false,
                null,
                null,
                null,
                null),

            new ProjectDefinition(
                "InkCanvas.PluginSdk（DLL）",
                "InkCanvas.PluginSdk",
                false,
                null,
                null,
                null,
                null),

            new ProjectDefinition(
                "InkCanvas.PptAgent.Contracts（DLL）",
                "InkCanvas.PPTAgent.Contracts",
                false,
                null,
                null,
                null,
                null),

            new ProjectDefinition(
                "InkCanvas.SettingsTreeView",
                "InkCanvas.SettingsTreeView",
                true,
                Path.Combine("bin", "Debug"),
                Path.Combine("bin", "Release"),
                Path.Combine("bin", "Release"),
                "InkCanvas.SettingsTreeView.exe"),

            new ProjectDefinition(
                "InkCanvas.LiquidGlassMagHost",
                "InkCanvas.LiquidGlassMagHost",
                true,
                Path.Combine("bin", "Debug"),
                Path.Combine("bin", "Release"),
                Path.Combine("bin", "Release"),
                "InkCanvas.LiquidGlassMagHost.exe")
        };

        public BuildRunner(string repositoryRoot)
        {
            RepositoryRoot = repositoryRoot ?? throw new ArgumentNullException(nameof(repositoryRoot));
        }

        /// <summary>
        /// 根据构建模式获取 dotnet 子命令（build / publish），不带任何额外参数。
        /// </summary>
        public string GetDotnetArguments(BuildMode mode)
        {
            switch (mode)
            {
                case BuildMode.Debug:
                case BuildMode.Release:
                    return "build";
                case BuildMode.Publish:
                    return "publish";
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        /// <summary>
        /// 获取项目的工作目录（dotnet 命令的执行目录）。
        /// </summary>
        public string GetWorkingDirectory(ProjectDefinition project)
        {
            return string.IsNullOrEmpty(project.ProjectDirectory)
                ? RepositoryRoot
                : Path.Combine(RepositoryRoot, project.ProjectDirectory);
        }

        /// <summary>
        /// 查找构建产物（exe）的绝对路径。仅对可执行项目有效；找不到时返回 null。
        /// 目标框架目录名按实际产物动态匹配，避免写死 net6.0 / net10.0。
        /// </summary>
        public string GetExecutablePath(ProjectDefinition project, BuildMode mode)
        {
            if (!project.IsExecutable)
                return null;

            string relativeDir;
            switch (mode)
            {
                case BuildMode.Debug:
                    relativeDir = project.DebugOutputDirectory;
                    break;
                case BuildMode.Release:
                    relativeDir = project.ReleaseOutputDirectory;
                    break;
                case BuildMode.Publish:
                    relativeDir = project.PublishOutputDirectory;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }

            string baseDir = Path.Combine(RepositoryRoot, project.ProjectDirectory, relativeDir);
            if (!Directory.Exists(baseDir))
                return null;

            var candidates = Directory
                .GetFiles(baseDir, project.ExecutableName, SearchOption.AllDirectories)
                .Where(p =>
                {
                    bool inPublish = p.IndexOf("\\publish\\", StringComparison.OrdinalIgnoreCase) >= 0;
                    return mode == BuildMode.Publish ? inPublish : !inPublish;
                })
                .OrderByDescending(File.GetLastWriteTimeUtc);

            return candidates.FirstOrDefault();
        }

        /// <summary>
        /// 异步执行 dotnet 命令，逐行回传输出文本。返回进程退出码。
        /// </summary>
        public async Task<int> RunDotnetAsync(ProjectDefinition project, BuildMode mode, IProgress<string> output)
        {
            string args = GetDotnetArguments(mode);
            string workingDirectory = GetWorkingDirectory(project);

            // 命令本身不带任何参数；通过进程环境变量 Configuration 让 MSBuild 使用对应配置。
            string configuration = mode == BuildMode.Debug ? "Debug" : "Release";

            output?.Report($"> dotnet {args}   （目录：{workingDirectory}，Configuration={configuration}）\n");

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = args,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.EnvironmentVariables["Configuration"] = configuration;

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            var tcs = new TaskCompletionSource<int>();

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null) output?.Report(e.Data + "\n");
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null) output?.Report(e.Data + "\n");
            };
            process.Exited += (s, e) => tcs.TrySetResult(process.ExitCode);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            int exitCode = await tcs.Task.ConfigureAwait(false);
            output?.Report($"\n[dotnet exited with code {exitCode}]\n");
            return exitCode;
        }

        /// <summary>
        /// 杀掉目标项目对应的正在运行进程，返回被杀掉的进程数量。
        /// </summary>
        public int KillProjectProcesses(ProjectDefinition project)
        {
            if (!project.IsExecutable)
                return 0;

            int killed = 0;
            string processName = project.ProcessName;

            try
            {
                foreach (var p in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(3000);
                        killed++;
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
            }
            catch { }

            return killed;
        }

        /// <summary>
        /// 启动构建产物 exe，返回是否成功启动。
        /// </summary>
        public bool TryLaunch(ProjectDefinition project, BuildMode mode, out string message)
        {
            string exePath = GetExecutablePath(project, mode);

            if (string.IsNullOrEmpty(exePath))
            {
                message = project.IsExecutable
                    ? $"未找到 {project.DisplayName} 的可执行文件，请先编译。"
                    : $"项目 {project.DisplayName} 只输出 DLL，没有可启动的可执行文件。";
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = Path.GetDirectoryName(exePath),
                    UseShellExecute = true
                });
                message = $"已启动：{exePath}";
                return true;
            }
            catch (Exception ex)
            {
                message = $"启动失败：{ex.Message}";
                return false;
            }
        }
    }
}
