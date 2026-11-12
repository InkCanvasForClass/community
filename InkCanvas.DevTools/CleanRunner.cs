using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace InkCanvas.DevTools
{
    /// <summary>
    /// 一个待清理的构建产物目录（bin 或 obj）。
    /// </summary>
    public sealed class CleanTarget
    {
        /// <summary>目录绝对路径。</summary>
        public string FullPath { get; }

        /// <summary>相对仓库根目录的路径，用于日志展示。</summary>
        public string RelativePath { get; }

        /// <summary>扫描时统计的目录大小（字节）。</summary>
        public long SizeBytes { get; }

        public CleanTarget(string fullPath, string relativePath, long sizeBytes)
        {
            FullPath = fullPath;
            RelativePath = relativePath;
            SizeBytes = sizeBytes;
        }
    }

    /// <summary>
    /// 扫描结果：待清理目录列表，以及被主动跳过的目录。
    /// </summary>
    public sealed class CleanScanResult
    {
        /// <summary>待删除的 bin / obj 目录。</summary>
        public IReadOnlyList<CleanTarget> Targets { get; }

        /// <summary>
        /// 被跳过的目录，例如本工具自身正在使用的输出目录（运行中的程序会锁住文件）。
        /// </summary>
        public IReadOnlyList<string> SkippedPaths { get; }

        /// <summary>待清理目录的总大小（字节）。</summary>
        public long TotalBytes => Targets.Sum(t => t.SizeBytes);

        public CleanScanResult(IReadOnlyList<CleanTarget> targets, IReadOnlyList<string> skippedPaths)
        {
            Targets = targets;
            SkippedPaths = skippedPaths;
        }
    }

    /// <summary>
    /// 清理结果汇总。
    /// </summary>
    public sealed class CleanSummary
    {
        /// <summary>成功删除的目录数量。</summary>
        public int DeletedCount { get; }

        /// <summary>删除失败的数量。</summary>
        public int FailedCount => Failures.Count;

        /// <summary>成功删除的目录扫描时统计的总大小（字节）。</summary>
        public long DeletedBytes { get; }

        /// <summary>失败项，格式为“相对路径：错误信息”。</summary>
        public IReadOnlyList<string> Failures { get; }

        /// <summary>整个删除过程耗时。</summary>
        public TimeSpan Elapsed { get; }

        public CleanSummary(int deletedCount, long deletedBytes, IReadOnlyList<string> failures, TimeSpan elapsed)
        {
            DeletedCount = deletedCount;
            DeletedBytes = deletedBytes;
            Failures = failures;
            Elapsed = elapsed;
        }
    }

    /// <summary>
    /// 负责扫描并删除仓库内所有子项目的 bin / obj 构建产物目录。
    /// </summary>
    public sealed class CleanRunner
    {
        /// <summary>扫描时直接跳过的目录名（版本库、IDE 缓存、依赖缓存等）。</summary>
        private static readonly HashSet<string> SkippedDirectoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git",
            ".svn",
            ".hg",
            ".vs",
            ".idea",
            ".vscode",
            "node_modules",
            "packages",
            "TestResults"
        };

        private static readonly string[] BuildOutputDirectoryNames = { "bin", "obj" };

        /// <summary>仓库根目录（社区版目录，即 Ink Canvas.sln 所在目录）。</summary>
        public string RepositoryRoot { get; }

        /// <summary>本工具自身的项目目录，其 bin / obj 在运行时被占用，清理时需要跳过。</summary>
        private readonly string _selfProjectDirectory;

        public CleanRunner(string repositoryRoot)
        {
            RepositoryRoot = repositoryRoot ?? throw new ArgumentNullException(nameof(repositoryRoot));
            _selfProjectDirectory = FindSelfProjectDirectory();
        }

        /// <summary>
        /// 扫描仓库根目录下所有名为 bin / obj 的目录。不会跟随符号链接（junction），
        /// 也不会进入已经命中的产物目录，因此每个产物目录只会被统计一次。
        /// </summary>
        public CleanScanResult Scan()
        {
            var targets = new List<CleanTarget>();
            var skipped = new List<string>();

            if (!Directory.Exists(RepositoryRoot))
                return new CleanScanResult(targets, skipped);

            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint
            };

            var pending = new Stack<string>();
            pending.Push(RepositoryRoot);

            while (pending.Count > 0)
            {
                string current = pending.Pop();

                string[] children;
                try
                {
                    children = Directory.GetDirectories(current, "*", options);
                }
                catch (Exception)
                {
                    // 无权限或扫描期间被删除的目录直接跳过。
                    continue;
                }

                foreach (string child in children)
                {
                    string name = Path.GetFileName(child);

                    if (IsBuildOutputDirectory(name))
                    {
                        // 本工具自身可能正在运行，其 bin / obj 中的文件被进程占用，删除必然失败。
                        if (IsInsideSelfProject(child))
                            skipped.Add(GetRelativePath(child));
                        else
                            targets.Add(new CleanTarget(child, GetRelativePath(child), GetDirectorySize(child)));

                        // 命中 bin / obj 后不再深入，避免重复统计其内部目录。
                        continue;
                    }

                    if (SkippedDirectoryNames.Contains(name))
                        continue;

                    pending.Push(child);
                }
            }

            targets.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));
            skipped.Sort(StringComparer.OrdinalIgnoreCase);
            return new CleanScanResult(targets, skipped);
        }

        /// <summary>
        /// 删除扫描得到的目录。单个目录删除失败不会中断其余目录的清理。
        /// </summary>
        /// <param name="targets">要删除的目录。</param>
        /// <param name="output">逐条输出进度；可为 null。</param>
        public CleanSummary Delete(IReadOnlyList<CleanTarget> targets, IProgress<string> output = null)
        {
            if (targets == null)
                throw new ArgumentNullException(nameof(targets));

            var failures = new List<string>();
            int deletedCount = 0;
            long deletedBytes = 0;
            var stopwatch = Stopwatch.StartNew();

            foreach (CleanTarget target in targets)
            {
                output?.Report($"[清理] {target.RelativePath}（{FormatBytes(target.SizeBytes)}）\n");

                try
                {
                    DeleteDirectory(target.FullPath);
                    deletedCount++;
                    deletedBytes += target.SizeBytes;
                }
                catch (Exception ex)
                {
                    string failure = $"{target.RelativePath}：{ex.Message}";
                    failures.Add(failure);
                    output?.Report($"[失败] {failure}\n");
                }
            }

            stopwatch.Stop();
            return new CleanSummary(deletedCount, deletedBytes, failures, stopwatch.Elapsed);
        }

        /// <summary>
        /// 把字节数格式化为便于阅读的字符串。
        /// </summary>
        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";

            double value = bytes;
            string[] units = { "KB", "MB", "GB", "TB" };
            int unit = 0;
            value /= 1024;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return $"{value:0.##} {units[unit]}";
        }

        /// <summary>
        /// 删除单个目录；遇到只读文件导致失败时，清除只读属性后重试一次。
        /// </summary>
        private static void DeleteDirectory(string path)
        {
            if (!Directory.Exists(path))
                return;

            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            // 首次删除可能因只读属性失败，清理属性后重试。
            if (!Directory.Exists(path))
                return;

            ClearReadOnlyAttributes(path);
            Directory.Delete(path, recursive: true);
        }

        /// <summary>
        /// 递归清除只读属性（尽力而为），避免因只读文件导致整个目录删不掉。
        /// </summary>
        private static void ClearReadOnlyAttributes(string path)
        {
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            try
            {
                foreach (string file in Directory.EnumerateFiles(path, "*", options))
                    TryClearReadOnly(file);
            }
            catch (Exception)
            {
            }

            try
            {
                foreach (string directory in Directory.EnumerateDirectories(path, "*", options))
                    TryClearReadOnly(directory);
            }
            catch (Exception)
            {
            }

            TryClearReadOnly(path);
        }

        private static void TryClearReadOnly(string path)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 统计目录总大小；无法访问的文件按 0 计算。
        /// </summary>
        private static long GetDirectorySize(string path)
        {
            long total = 0;
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            try
            {
                foreach (string file in Directory.EnumerateFiles(path, "*", options))
                {
                    try
                    {
                        total += new FileInfo(file).Length;
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception)
            {
            }

            return total;
        }

        private static bool IsBuildOutputDirectory(string name)
        {
            foreach (string outputName in BuildOutputDirectoryNames)
            {
                if (name.Equals(outputName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private bool IsInsideSelfProject(string path)
        {
            if (string.IsNullOrEmpty(_selfProjectDirectory))
                return false;

            string prefix = _selfProjectDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? _selfProjectDirectory
                : _selfProjectDirectory + Path.DirectorySeparatorChar;

            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private string GetRelativePath(string fullPath)
        {
            return Path.GetRelativePath(RepositoryRoot, fullPath);
        }

        /// <summary>
        /// 从程序目录向上查找本工具的项目目录（含 InkCanvas.DevTools.csproj 的目录）。
        /// </summary>
        private static string FindSelfProjectDirectory()
        {
            string directory = AppContext.BaseDirectory;

            while (!string.IsNullOrEmpty(directory))
            {
                if (File.Exists(Path.Combine(directory, "InkCanvas.DevTools.csproj")))
                    return Path.TrimEndingDirectorySeparator(directory);

                directory = Directory.GetParent(directory)?.FullName;
            }

            return null;
        }
    }
}
