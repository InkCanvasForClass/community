using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ink_Canvas.Helpers
{
    internal static class ProcessProtectionManager
    {
        private static readonly object _lock = new object();
        private static readonly Dictionary<string, FileStream> _lockedFiles = new Dictionary<string, FileStream>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, SafeFileHandle> _lockedDirs = new Dictionary<string, SafeFileHandle>(StringComparer.OrdinalIgnoreCase);
        private static bool _enabled;
        private static int _writeGate;
        private static readonly string[] _excludedSubDirectories = new[]
        {
            "Configs",
            "Saves",
            "Backups",
            "Logs",
            "AutoUpdate"
        };

        public static bool Enabled
        {
            get { lock (_lock) return _enabled; }
        }

        /// <summary>
        /// 从应用设置读取 EnableProcessProtection 并相应地启用或禁用进程保护。
        /// </summary>
        public static void ApplyFromSettings()
        {
            try
            {
                var settings = MainWindow.Settings;
                var enabled = settings?.Security != null && settings.Security.EnableProcessProtection;
                SetEnabled(enabled);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ProcessProtectionManager.ApplyFromSettings 失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        /// <summary>
        /// 切换进程保护的启用状态；在状态发生变化时触发相应的启用或禁用操作并保证线程安全。
        /// </summary>
        /// <param name="enabled">为 `true` 时启用进程保护，为 `false` 时禁用进程保护。</param>
        public static void SetEnabled(bool enabled)
        {
            lock (_lock)
            {
                if (_enabled == enabled) return;
                _enabled = enabled;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (enabled) Enable();
                    else Disable();
                }
                catch (Exception ex)
                {
                    try
                    {
                        LogHelper.WriteLogToFile($"ProcessProtectionManager.SetEnabled 后台执行失败: {ex.Message}", LogHelper.LogType.Warning);
                    }
                    catch { }
                }
            });
        }

        /// <summary>
        /// 在受进程保护的上下文中执行对指定目标的写入操作；在执行时会在必要情况下临时释放针对目标路径及其父目录的锁，执行完成后恢复这些锁。
        /// </summary>
        /// <param name="targetPath">目标文件或目录的路径，用于确定需要临时释放和随后恢复的锁。</param>
        /// <param name="action">执行写入的操作委托，不能为空。</param>
        /// <remarks>
        /// 如果 ProcessProtectionManager.Enabled 为 false，会直接执行 <paramref name="action"/>；
        /// 若在有限时间内无法获取写入门闩，会记录警告并降级为直接执行 <paramref name="action"/>。方法在内部处理异常，不会抛出异常给调用者。
        /// </remarks>
        public static void WithWriteAccess(string targetPath, Action action)
        {
            if (action == null) return;

            if (!Enabled)
            {
                action();
                return;
            }

            const int gateTimeoutMs = 10_000;
            if (!TryEnterWriteGate(gateTimeoutMs))
            {
                try
                {
                    LogHelper.WriteLogToFile($"ProcessProtectionManager.WithWriteAccess: 获取写入门闩超时({gateTimeoutMs}ms)，将降级释放目标路径锁后执行写入。目标: {targetPath}",
                        LogHelper.LogType.Warning);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProcessProtectionManager] 写日志失败: {ex.Message}");
                }

                var normPath = NormalizePath(targetPath);
                var dirsChain = GetDirChainToRoot(normPath);
                Dictionary<string, SafeFileHandle> fallbackDirs = null;
                Dictionary<string, FileStream> fallbackFiles = null;

                try
                {
                    lock (_lock)
                    {
                        fallbackDirs = new Dictionary<string, SafeFileHandle>(StringComparer.OrdinalIgnoreCase);
                        fallbackFiles = new Dictionary<string, FileStream>(StringComparer.OrdinalIgnoreCase);

                        foreach (var dir in dirsChain)
                        {
                            if (_lockedDirs.TryGetValue(dir, out var handle))
                            {
                                _lockedDirs.Remove(dir);
                                fallbackDirs[dir] = handle;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(normPath) && File.Exists(normPath) && _lockedFiles.TryGetValue(normPath, out var fs))
                        {
                            _lockedFiles.Remove(normPath);
                            fallbackFiles[normPath] = fs;
                        }
                    }

                    if (fallbackFiles != null)
                    {
                        foreach (var kv in fallbackFiles)
                        {
                            try { kv.Value.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                        }
                    }
                    if (fallbackDirs != null)
                    {
                        foreach (var kv in fallbackDirs)
                        {
                            try { kv.Value.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                        }
                    }

                    action();
                }
                finally
                {
                    try
                    {
                        if (Enabled)
                        {
                            Enable(rescanRoot: false, rescanDirs: dirsChain);
                        }
                    }
                    catch { }
                }
                return;
            }

            var normalized = NormalizePath(targetPath);
            var dirsToToggle = GetDirChainToRoot(normalized);

            Dictionary<string, SafeFileHandle> releasedDirs = null;
            Dictionary<string, FileStream> releasedFiles = null;

            try
            {
                lock (_lock)
                {
                    releasedDirs = new Dictionary<string, SafeFileHandle>(StringComparer.OrdinalIgnoreCase);
                    releasedFiles = new Dictionary<string, FileStream>(StringComparer.OrdinalIgnoreCase);

                    foreach (var dir in dirsToToggle)
                    {
                        if (_lockedDirs.TryGetValue(dir, out var handle))
                        {
                            _lockedDirs.Remove(dir);
                            releasedDirs[dir] = handle;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(normalized) && File.Exists(normalized) && _lockedFiles.TryGetValue(normalized, out var fs))
                    {
                        _lockedFiles.Remove(normalized);
                        releasedFiles[normalized] = fs;
                    }
                }

                if (releasedFiles != null)
                {
                    foreach (var kv in releasedFiles)
                    {
                        try { kv.Value.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                    }
                }
                if (releasedDirs != null)
                {
                    foreach (var kv in releasedDirs)
                    {
                        try { kv.Value.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                    }
                }

                action();
            }
            finally
            {
                try
                {
                    if (Enabled)
                    {
                        Enable(rescanRoot: false, rescanDirs: dirsToToggle);
                    }
                }
                catch
                {
                }

                Interlocked.Exchange(ref _writeGate, 0);
            }
        }

        /// <summary>
        /// 尝试在指定的毫秒数内获取写入门控（write gate）。
        /// </summary>
        /// <param name="timeoutMs">等待超时时间（毫秒）。小于或等于 0 时视为 1 毫秒。</param>
        /// <returns>`true` 如果在指定时间内成功获取到写入门控，`false` 否则。</returns>
        private static bool TryEnterWriteGate(int timeoutMs)
        {
            if (timeoutMs <= 0) timeoutMs = 1;

            var start = Environment.TickCount;
            while (Interlocked.CompareExchange(ref _writeGate, 1, 0) != 0)
            {
                var elapsed = unchecked(Environment.TickCount - start);
                if (elapsed >= timeoutMs) return false;

                if (elapsed < 2000) Thread.Sleep(10);
                else Thread.Sleep(50);
            }
            return true;
        }

        /// <summary>
        /// 启用进程保护并对应用根路径进行完整重扫描以锁定需要保护的目录和文件。
        /// </summary>
        private static void Enable()
        {
            Enable(rescanRoot: true, rescanDirs: null);
        }

        /// <summary>
        /// 在应用根目录或提供的路径集合上建立目录句柄和文件读取锁以启用进程保护。
        /// </summary>
        /// <param name="rescanRoot">为 true 时对 App.RootPath 进行递归扫描并锁定其下的目录与文件；为 false 时仅处理 <paramref name="rescanDirs"/> 指定的路径（若为 null 则不处理）。</param>
        /// <param name="rescanDirs">当 <paramref name="rescanRoot"/> 为 false 时，按项对存在的目录建立目录锁，对存在的文件建立文件锁；可为 null。</param>
        private static void Enable(bool rescanRoot, IEnumerable<string> rescanDirs)
        {
            try
            {
                var root = App.RootPath;
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return;
                root = NormalizePath(root);

                if (rescanRoot)
                {
                    LockDirectoryRecursive(root);
                }
                else if (rescanDirs != null)
                {
                    foreach (var d in rescanDirs)
                    {
                        if (Directory.Exists(d))
                        {
                            LockDirectory(d);
                        }
                    }
                }

                if (rescanRoot)
                {
                    LockFilesRecursive(root);
                }
                else if (rescanDirs != null)
                {
                    foreach (var d in rescanDirs)
                    {
                        if (Directory.Exists(d))
                        {
                            LockFilesRecursive(d);
                        }
                        else if (File.Exists(d))
                        {
                            LockFile(d);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 释放并清除当前进程持有的所有文件和目录锁定句柄与流资源。
        /// </summary>
        /// <remarks>
        /// 在内部同步锁定下逐一 Dispose 已记录的 FileStream 和 SafeFileHandle，并清空对应的缓存字典；
        /// 释放过程中发生的异常会被忽略（吞掉）。
        /// </remarks>
        private static void Disable()
        {
            lock (_lock)
            {
                foreach (var kv in _lockedFiles)
                {
                    try { kv.Value.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                }
                _lockedFiles.Clear();

                foreach (var kv in _lockedDirs)
                {
                    try { kv.Value.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                }
                _lockedDirs.Clear();
            }
        }

        /// <summary>
        /// 递归地尝试为指定目录及其所有子目录建立并保持目录句柄锁定，跳过配置的排除目录。
        /// </summary>
        /// <param name="root">起始目录的路径；从此路径开始遍历并对符合条件的子目录尝试建立锁定。</param>
        /// <remarks>遇到的异常会被捕获并忽略，不会向调用方抛出。</remarks>
        private static void LockDirectoryRecursive(string root)
        {
            try
            {
                if (!IsExcludedPath(root))
                {
                    LockDirectory(root);
                }
                foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
                {
                    if (!IsExcludedPath(dir))
                    {
                        LockDirectory(dir);
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 递归扫描指定根目录下的所有文件，并为具有特定扩展名的文件建立读取锁定以防止被进程修改或替换。
        /// </summary>
        /// <param name="root">要开始扫描的根目录路径。</param>
        /// <remarks>
        /// 仅处理扩展名为 `.exe`, `.dll`, `.config`, `.manifest`, `.dat`, `.enc` 的文件；会跳过被 IsExcludedPath 判定为排除的路径。遇到任何 I/O 或访问错误时会静默忽略，不会抛出异常。</remarks>
        private static void LockFilesRecursive(string root)
        {
            try
            {
                foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    if (!IsExcludedPath(file))
                    {
                        var ext = Path.GetExtension(file);
                        if (string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ext, ".dll", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ext, ".config", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ext, ".manifest", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ext, ".dat", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ext, ".enc", StringComparison.OrdinalIgnoreCase))
                        {
                            LockFile(file);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 以只读方式打开并保留指定文件的句柄，将其加入内部锁定缓存以减少该文件被外部修改或删除的可能性。
        /// </summary>
        /// <param name="filePath">要锁定的文件的路径（会被规范化为完整路径）。</param>
        private static void LockFile(string filePath)
        {
            filePath = NormalizePath(filePath);
            lock (_lock)
            {
                if (_lockedFiles.ContainsKey(filePath)) return;
                try
                {
                    var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    _lockedFiles[filePath] = fs;
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// 尝试为指定目录获取一个用于保持目录打开的句柄并将其保存为内部锁定记录；若目录已被记录则不作任何操作，发生错误时静默忽略。
        /// </summary>
        /// <param name="dirPath">要锁定的目录路径；调用时会对路径进行规范化（转换为完整路径并移除多余分隔符）。</param>
        private static void LockDirectory(string dirPath)
        {
            dirPath = NormalizePath(dirPath);
            lock (_lock)
            {
                if (_lockedDirs.ContainsKey(dirPath)) return;
                try
                {
                    var handle = CreateDirectoryHandle(dirPath);
                    if (handle != null && !handle.IsInvalid)
                    {
                        _lockedDirs[dirPath] = handle;
                    }
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// 将路径标准化为不含末尾路径分隔符的绝对路径。
        /// </summary>
        /// <param name="p">要规范化的路径；如果为 null、空或仅空白，则返回原值。</param>
        /// <returns>规范化后的路径：在解析成功时返回去除末尾分隔符的绝对路径；在解析失败时返回原始输入。</returns>
        private static string NormalizePath(string p)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(p)) return p;
                return Path.GetFullPath(p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
            catch
            {
                return p;
            }
        }

        /// <summary>
        /// 构建从指定路径向上直到应用根目录（App.RootPath）的目录链，并按从下到上的顺序返回已规范化的目录路径。
        /// </summary>
        /// <param name="path">起始路径，既可为文件路径也可为目录路径；若为文件则使用其所在目录作为起点。</param>
        /// <returns>包含起始目录及其各级父目录直到并包含应用根目录的列表；当根路径无效或未能匹配到根目录时返回空列表。</returns>
        private static List<string> GetDirChainToRoot(string path)
        {
            var list = new List<string>();
            try
            {
                var root = NormalizePath(App.RootPath);
                if (string.IsNullOrWhiteSpace(root)) return list;

                string dir = Directory.Exists(path) ? NormalizePath(path) : NormalizePath(Path.GetDirectoryName(path));
                while (!string.IsNullOrWhiteSpace(dir))
                {
                    if (!dir.StartsWith(root, StringComparison.OrdinalIgnoreCase)) break;
                    list.Add(dir);
                    if (string.Equals(dir, root, StringComparison.OrdinalIgnoreCase)) break;
                    dir = NormalizePath(Path.GetDirectoryName(dir));
                }
            }
            catch
            {
            }
            return list;
        }

        /// <summary>
        /// 检查给定路径是否位于应用根目录下的受排除子目录之一。
        /// </summary>
        /// <param name="path">要检查的文件或目录路径。</param>
        /// <returns>`true` 如果路径位于任何配置为排除的子目录下，`false` 否则。</returns>
        private static bool IsExcludedPath(string path)
        {
            try
            {
                var root = NormalizePath(App.RootPath);
                if (string.IsNullOrWhiteSpace(root)) return false;
                path = NormalizePath(path);
                foreach (var name in _excludedSubDirectories)
                {
                    var prefix = Path.Combine(root, name);
                    if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        /// <summary>
        /// 为指定目录打开一个文件句柄，便于对该目录进行锁定或访问其元数据。
        /// </summary>
        /// <param name="dirPath">目标目录的完整路径。</param>
        /// <returns>表示已打开目录的 <see cref="SafeFileHandle"/>；若无法打开则返回无效的句柄，调用方应检查句柄有效性。</returns>
        private static SafeFileHandle CreateDirectoryHandle(string dirPath)
        {
            const uint GENERIC_READ = 0x80000000;
            const uint FILE_SHARE_READ = 0x00000001;
            const uint OPEN_EXISTING = 3;
            const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

            return CreateFile(
                dirPath,
                GENERIC_READ,
                FILE_SHARE_READ,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS,
                IntPtr.Zero);
        }

        /// <summary>
        /// 使用原生 CreateFile API 打开或创建一个文件/目录并返回底层句柄的包装函数声明。
        /// </summary>
        /// <param name="lpFileName">要打开或创建的文件或目录的完整路径（UTF-16 编码）。</param>
        /// <param name="dwDesiredAccess">请求的访问权限位掩码（例如读取或写入访问）。</param>
        /// <param name="dwShareMode">共享模式位掩码，指定其他进程可以如何共享此文件句柄。</param>
        /// <param name="lpSecurityAttributes">指向安全属性结构的指针，或为 <see cref="IntPtr.Zero"/> 表示默认安全性。</param>
        /// <param name="dwCreationDisposition">指定如何处理已存在或不存在的文件（例如打开、创建或截断）。</param>
        /// <param name="dwFlagsAndAttributes">文件属性和标志位，用于控制文件或目录的特殊行为（例如备份语义）。</param>
        /// <param name="hTemplateFile">用于创建新文件时的模板句柄，通常为 <see cref="IntPtr.Zero"/>。</param>
        /// <returns>表示文件或目录句柄的 <see cref="Microsoft.Win32.SafeHandles.SafeFileHandle"/>；调用失败时返回无效的句柄（可通过检查句柄或调用 <see cref="System.Runtime.InteropServices.Marshal.GetLastWin32Error"/> 获取错误码）。</returns>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);
    }
}
