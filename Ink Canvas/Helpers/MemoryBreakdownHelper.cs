using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// Builds a detailed memory snapshot of the current process for diagnostics
    /// (relates to issue #546). The snapshot is plain text — it lists:
    ///   1) Process-level working set / private usage / page-file usage
    ///   2) .NET GC heap stats (per-generation size, fragmentation, pinned objects)
    ///   3) WPF UI counts (Windows, SettingsWindow page cache, Visual tree size)
    ///   4) In-app caches that are known to grow (PerformanceMonitorHelper samples,
    ///      Automation / plugin manager registry if accessible via reflection)
    ///   5) Loaded assemblies count
    /// The caller can persist the report to Logs/MemoryBreakdown_*.txt and/or
    /// emit a short summary through <see cref="LogHelper.WriteLogToFile"/>.
    /// </summary>
    public static class MemoryBreakdownHelper
    {
        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS counters, uint cb);

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_MEMORY_COUNTERS
        {
            public uint cb;
            public uint PageFaultCount;
            public IntPtr PeakWorkingSetSize;
            public IntPtr WorkingSetSize;
            public IntPtr QuotaPeakPagedPoolUsage;
            public IntPtr QuotaPagedPoolUsage;
            public IntPtr QuotaPeakNonPagedPoolUsage;
            public IntPtr QuotaNonPagedPoolUsage;
            public IntPtr PagefileUsage;
            public IntPtr PeakPagefileUsage;
            public IntPtr PrivateUsage;
        }

        private const string FolderName = "Logs";

        /// <summary>
        /// Build the report. Does NOT mutate process state (no GC, no file IO).
        /// </summary>
        public static string BuildReport()
        {
            var sb = new StringBuilder(8192);
            var now = DateTime.Now;

            sb.AppendLine("=== InkCanvas Memory Breakdown ===");
            sb.AppendLine($"Time          : {now:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"OS            : {Environment.OSVersion}");
            sb.AppendLine($"64-bit OS     : {Environment.Is64BitOperatingSystem}");
            sb.AppendLine($"Processor Cnt : {Environment.ProcessorCount}");
            sb.AppendLine($"CLR Version   : {Environment.Version}");
            sb.AppendLine($"Working Set   : {(Environment.WorkingSet / (1024.0 * 1024.0)):F1} MB (Environment)");

            double privateUsageMb = 0;
            double gcHeapMb = 0;

            AppendProcessSection(sb, ref privateUsageMb);
            AppendGcSection(sb, ref gcHeapMb);
            AppendWpfSection(sb);
            AppendAppCacheSection(sb, privateUsageMb, gcHeapMb);
            AppendAssembliesSection(sb);

            return sb.ToString();
        }

        /// <summary>
        /// Write the report to Logs/MemoryBreakdown_yyyyMMdd_HHmmss.txt and return the path.
        /// Also logs a one-line summary through LogHelper.
        /// </summary>
        public static string DumpToFile()
        {
            try
            {
                var report = BuildReport();
                var logsDir = Path.Combine(App.RootPath, FolderName);
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                }

                var fileName = $"MemoryBreakdown_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var fullPath = Path.Combine(logsDir, fileName);
                File.WriteAllText(fullPath, report, Encoding.UTF8);

                LogHelper.WriteLogToFile(
                    $"[MemoryBreakdown] 报告已写入 {fullPath} ({new FileInfo(fullPath).Length / 1024} KB)",
                    LogHelper.LogType.Info);
                return fullPath;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[MemoryBreakdown] 写入失败: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        /// <summary>
        /// Force a full GC pass. Captures Working Set / Private Usage / GC Heap / Finalization Pending
        /// before & after, writes a multi-line summary to the log file, and returns the summary text.
        /// </summary>        /// <summary>
        /// Force a full GC pass. Captures Working Set / Private Usage / GC Heap / Finalization Pending
        /// before & after, writes a multi-line summary to the log file, and returns the summary text.
        /// </summary>
        public static string ForceFullGc()
        {
            // 采样前
            var before = SampleMemorySnapshot();

            try
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[MemoryBreakdown] ForceFullGc 异常: {ex.Message}", LogHelper.LogType.Warning);
            }

            var after = SampleMemorySnapshot();
            var summary = FormatDiff(before, after);

            // 把这次 GC 单独写入一份带时间戳的对照报告,便于追踪
            try
            {
                var logsDir = Path.Combine(App.RootPath, FolderName);
                if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);
                var fileName = $"GcDiff_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var fullPath = Path.Combine(logsDir, fileName);
                File.WriteAllText(fullPath, summary, Encoding.UTF8);
                LogHelper.WriteLogToFile(
                    $"[MemoryBreakdown] GC 对照报告: {fullPath}",
                    LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[MemoryBreakdown] 写入 GcDiff 失败: {ex.Message}", LogHelper.LogType.Warning);
            }

            return summary;
        }

        private struct MemorySnapshot
        {
            public long WorkingSetMb;
            public long PrivateUsageMb;
            public long ManagedHeapMb;
            public int PinnedObjects;
            public int FinalizationPending;
        }

        private static MemorySnapshot SampleMemorySnapshot()
        {
            var snap = new MemorySnapshot
            {
                WorkingSetMb = Environment.WorkingSet / (1024L * 1024L),
                ManagedHeapMb = GC.GetTotalMemory(forceFullCollection: false) / (1024L * 1024L),
            };

            try
            {
                using var p = Process.GetCurrentProcess();
                p.Refresh();
                var counters = new PROCESS_MEMORY_COUNTERS { cb = (uint)Marshal.SizeOf<PROCESS_MEMORY_COUNTERS>() };
                if (GetProcessMemoryInfo(p.Handle, out counters, counters.cb))
                {
                    snap.PrivateUsageMb = counters.PrivateUsage.ToInt64() / (1024L * 1024L);
                }
            }
            catch { }

            try
            {
                var info = GC.GetGCMemoryInfo();
                snap.PinnedObjects = (int)info.PinnedObjectsCount;
                snap.FinalizationPending = (int)info.FinalizationPendingCount;
            }
            catch { }

            return snap;
        }

        private static string FormatDiff(MemorySnapshot b, MemorySnapshot a)
        {
            long dWS = a.WorkingSetMb - b.WorkingSetMb;
            long dPriv = a.PrivateUsageMb - b.PrivateUsageMb;
            long dHeap = a.ManagedHeapMb - b.ManagedHeapMb;
            int dPinned = a.PinnedObjects - b.PinnedObjects;
            int dFin = a.FinalizationPending - b.FinalizationPending;

            var sb = new StringBuilder(512);
            sb.AppendLine("=== MemoryBreakdown GC Diff ===");
            sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine();
            sb.AppendLine($"  {"Metric",-24} {"Before",12} {"After",12} {"Delta",12}");
            sb.AppendLine($"  {new string('-', 60)}");
            sb.AppendLine($"  {"Working Set (MB)",-24} {b.WorkingSetMb,12} {a.WorkingSetMb,12} {dWS,+12}");
            sb.AppendLine($"  {"Private Usage (MB)",-24} {b.PrivateUsageMb,12} {a.PrivateUsageMb,12} {dPriv,+12}");
            sb.AppendLine($"  {"Managed Heap (MB)",-24} {b.ManagedHeapMb,12} {a.ManagedHeapMb,12} {dHeap,+12}");
            sb.AppendLine($"  {"Pinned Objects",-24} {b.PinnedObjects,12} {a.PinnedObjects,12} {dPinned,+12}");
            sb.AppendLine($"  {"Finalization Pending",-24} {b.FinalizationPending,12} {a.FinalizationPending,12} {dFin,+12}");
            sb.AppendLine();
            sb.AppendLine(InterpretDiff(dWS, dPriv, dHeap));
            return sb.ToString();
        }

        private static string InterpretDiff(long dWS, long dPriv, long dHeap)
        {
            if (dPriv <= 0 && dHeap <= 0)
            {
                return "  ✓ GC 释放了内存:私有占用下降,GC 堆压缩成功。";
            }
            if (dPriv > 0 && dHeap < 0)
            {
                return "  ⚠️ GC 堆释放但私有占用增加:通常为 compressing:true 触发的页重新分配,实际 Working Set 会随后回落。";
            }
            if (dPriv > 0 && dHeap > 0)
            {
                return "  ⚠️ GC 后内存反而上升:可能有钉住对象(Pinned)/终结器队列残留/分页抖动。";
            }
            if (dPriv < 0 && dHeap > 0)
            {
                return "  ℹ️ 私有占用下降但托管堆略增:非托管缓冲区被回收,GC 效果正常。";
            }
            return "  ? 未变化,可能 GC 已是稳态或对象全部长生命周期。";
        }

        #region Section builders

        private static void AppendProcessSection(StringBuilder sb, ref double privateUsageMb)
        {
            sb.AppendLine();
            sb.AppendLine("--- 1. Process Memory (Win32 PROCESS_MEMORY_COUNTERS) ---");
            try
            {
                using var p = Process.GetCurrentProcess();
                p.Refresh();

                var counters = new PROCESS_MEMORY_COUNTERS { cb = (uint)Marshal.SizeOf<PROCESS_MEMORY_COUNTERS>() };
                if (GetProcessMemoryInfo(p.Handle, out counters, counters.cb))
                {
                    sb.AppendLine($"  Working Set         : {ToMb(counters.WorkingSetSize):F1} MB");
                    sb.AppendLine($"  Peak Working Set    : {ToMb(counters.PeakWorkingSetSize):F1} MB");
                    sb.AppendLine($"  Private Usage       : {ToMb(counters.PrivateUsage):F1} MB  (≈ 任务管理器“提交内存”)");
                    sb.AppendLine($"  Peak Private Usage  : {ToMb(counters.PeakPagefileUsage):F1} MB");
                    sb.AppendLine($"  Pagefile Usage      : {ToMb(counters.PagefileUsage):F1} MB");
                    sb.AppendLine($"  Page Fault Count    : {counters.PageFaultCount}");
                    sb.AppendLine($"  Paged Pool (current): {ToMb(counters.QuotaPagedPoolUsage):F1} MB");
                    sb.AppendLine($"  Non-Paged Pool      : {ToMb(counters.QuotaNonPagedPoolUsage):F1} MB");
                    privateUsageMb = ToMb(counters.PrivateUsage);
                }
                else
                {
                    sb.AppendLine("  GetProcessMemoryInfo 调用失败,回退 Process API:");
                    sb.AppendLine($"  PrivateMemorySize64 : {p.PrivateMemorySize64 / (1024.0 * 1024.0):F1} MB");
                    sb.AppendLine($"  WorkingSet64        : {p.WorkingSet64 / (1024.0 * 1024.0):F1} MB");
                    privateUsageMb = p.PrivateMemorySize64 / (1024.0 * 1024.0);
                }

                sb.AppendLine($"  Handle Count        : {p.HandleCount}");
                sb.AppendLine($"  Thread Count        : {p.Threads.Count}");
                sb.AppendLine($"  Virtual Memory      : {p.VirtualMemorySize64 / (1024.0 * 1024.0):F1} MB");
                sb.AppendLine($"  Paged Memory        : {p.PagedMemorySize64 / (1024.0 * 1024.0):F1} MB");
                sb.AppendLine($"  Nonpaged System Mem : {p.NonpagedSystemMemorySize64 / (1024.0 * 1024.0):F1} MB");
                sb.AppendLine($"  Uptime              : {(DateTime.Now - p.StartTime)}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  (获取进程信息失败: {ex.Message})");
            }
        }

        private static void AppendGcSection(StringBuilder sb, ref double gcHeapMb)
        {
            sb.AppendLine();
            sb.AppendLine("--- 2. .NET GC Heap ---");
            try
            {
                var info = GC.GetGCMemoryInfo();
                sb.AppendLine($"  Total Available     : {ToMb(info.TotalAvailableMemoryBytes):F1} MB");
                sb.AppendLine($"  Total Committed     : {ToMb(info.TotalCommittedBytes):F1} MB");
                sb.AppendLine($"  Heap Size           : {ToMb(info.HeapSizeBytes):F1} MB");
                sb.AppendLine($"  Fragmented Bytes    : {ToMb(info.FragmentedBytes):F1} MB");
                sb.AppendLine($"  Pinned Objects      : {info.PinnedObjectsCount}");
                sb.AppendLine($"  Finalization Pending: {info.FinalizationPendingCount}");

                for (int gen = 0; gen <= GC.MaxGeneration; gen++)
                {
                    GCGenerationInfo genInfo = default;
                    if (info.GenerationInfo != null && gen < info.GenerationInfo.Length)
                    {
                        genInfo = info.GenerationInfo[gen];
                    }
                    sb.AppendLine($"  Gen {gen,-2}             : SizeAfter={ToMb(genInfo.SizeAfterBytes):F2} MB, " +
                                  $"Fragmented={ToMb(genInfo.FragmentationAfterBytes):F2} MB");
                }

                sb.AppendLine($"  Total Allocated     : {ToMb(GC.GetTotalMemory(forceFullCollection: false)):F1} MB");
                sb.AppendLine($"  Total Allocated(FC) : {ToMb(GC.GetTotalMemory(forceFullCollection: true)) :F1} MB");
                gcHeapMb = ToMb(info.HeapSizeBytes);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  (获取 GC 信息失败: {ex.Message})");
            }
        }

        private static void AppendWpfSection(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("--- 3. WPF Visual Tree ---");
            try
            {
                int windowCount = 0;
                int frameCount = 0;
                int settingsPageCache = 0;
                long totalDescendants = 0;
                long peakDescendants = 0;
                string peakWindow = null;
                var visualTypeCounts = new Dictionary<string, int>(StringComparer.Ordinal);

                foreach (Window w in Application.Current?.Windows ?? new WindowCollection())
                {
                    windowCount++;
                    long descendants = 0;
                    int frames = 0;
                    CountVisuals(w, ref descendants, ref frames, 0);
                    CountVisualTypes(w, visualTypeCounts);
                    totalDescendants += descendants;

                    if (descendants > peakDescendants)
                    {
                        peakDescendants = descendants;
                        peakWindow = w.GetType().FullName;
                    }
                    frameCount += frames;

                    sb.AppendLine($"  Window: {w.GetType().FullName} " +
                                  $"(Title='{Truncate(w.Title, 40)}', Visuals={descendants}, Frames={frames})");

                    if (w is Ink_Canvas.Windows.SettingsViews.SettingsWindow sw)
                    {
                        settingsPageCache = ReadSettingsPageCacheCount(sw);
                        sb.AppendLine($"    └─ SettingsWindow._pages 缓存: {settingsPageCache} 页");
                    }
                }
                sb.AppendLine($"  Windows Total       : {windowCount}");
                sb.AppendLine($"  Visuals Total       : {totalDescendants:N0}");
                sb.AppendLine($"  Peak Window         : {peakWindow} ({peakDescendants:N0} visuals)");
                sb.AppendLine("  Selected Visual Types:");
                foreach (var typeName in new[] { "Image", "MediaElement", "InkCanvas", "D3DImage", "WriteableBitmap", "Popup", "Adorner" })
                {
                    int count = visualTypeCounts.TryGetValue(typeName, out var value) ? value : 0;
                    sb.AppendLine($"    {typeName,-18}: {count}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  (遍历 WPF 失败: {ex.Message})");
            }
        }

        private static void AppendAppCacheSection(StringBuilder sb, double privateUsageMb, double gcHeapMb)
        {
            sb.AppendLine();
            sb.AppendLine("--- 4. In-app caches (via reflection, best-effort) ---");
            try
            {
                // 非托管内存估算: Private Usage - GC Heap ≈ 非托管/原生缓冲区
                if (privateUsageMb > 0 && gcHeapMb > 0)
                {
                    double unmanagedMb = privateUsageMb - gcHeapMb;
                    if (unmanagedMb < 0) unmanagedMb = 0;
                    double ratio = gcHeapMb > 0 ? (unmanagedMb / privateUsageMb) * 100 : 0;
                    sb.AppendLine($"  Native (unmanaged) 估算                : {unmanagedMb:F1} MB  ({ratio:F0}% of Private Usage)");
                    sb.AppendLine($"    (Private Usage {privateUsageMb:F1} MB − GC Heap {gcHeapMb:F1} MB)");
                    if (ratio > 70)
                    {
                        sb.AppendLine($"    ⚠️ 非托管占比 >70%, 关注 DirectX 渲染缓冲 / WIC 位图 / PowerPoint COM Interop");
                    }
                }

                // PerformanceMonitorHelper 内部样本
                long perfSamples = ReadStaticField<long>(typeof(PerformanceMonitorHelper), "_cpuSamples")
                                   + ReadStaticField<long>(typeof(PerformanceMonitorHelper), "_memorySamples");
                sb.AppendLine($"  PerformanceMonitorHelper 累计样本       : {perfSamples}");

                // PerformanceHistory.json 条目
                try
                {
                    var perfHistory = PerformanceMonitorHelper.LoadHistory();
                    sb.AppendLine($"  PerformanceHistory.json 条目            : {perfHistory.Count}");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  PerformanceHistory.json: 读取失败 ({ex.Message})");
                }

                // GC 总占用
                sb.AppendLine($"  GC Total Memory                          : {GC.GetTotalMemory(false) / (1024.0 * 1024.0):F2} MB");
                sb.AppendLine();

                // MainWindow 相关缓存(白板页 / PageListView / InkSmoothingManager)
                var mainWindow = Application.Current?.MainWindow as Ink_Canvas.MainWindow;
                if (mainWindow == null)
                {
                    sb.AppendLine("  MainWindow 未就绪,跳过应用内反射。");
                }
                else
                {
                    AppendMainWindowCacheSection(sb, mainWindow);
                    AppendPptStreamSection(sb, mainWindow);
                }

                // TimeMachine(单例,通过 Ink_Canvas.MainWindow.TimeMachine 公共属性访问)
                string tmField = null;
                long tmCount = ReadTimeMachineCount(mainWindow, out tmField);
                sb.AppendLine($"  TimeMachine 历史                         : {tmCount} 条{(string.IsNullOrEmpty(tmField) ? "" : $"  (字段: {tmField})")}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  (缓存统计失败: {ex.Message})");
            }
        }

        private static void AppendMainWindowCacheSection(StringBuilder sb, Ink_Canvas.MainWindow mw)
        {
            try
            {
                // whiteboardPages (List<Canvas>)
                int whiteboardPages = ReadInstanceCollectionCount(mw, "whiteboardPages");
                int currentPageIndex = ReadInstanceField<int>(mw, "currentPageIndex");
                sb.AppendLine($"  MainWindow.whiteboardPages              : {whiteboardPages} 页 (currentPageIndex={currentPageIndex})");

                // blackBoardSidePageListViewObservableCollection (PageListViewItem)
                int pageListCount = ReadInstanceCollectionCount(mw, "blackBoardSidePageListViewObservableCollection");
                sb.AppendLine($"  MainWindow.PageListView Observable      : {pageListCount} 项");

                // InkSmoothingManager 性能监控器(若暴露了 InkSmoothingPerformanceMonitor.SampleCount)
                long smSample = ReadInkSmoothingSampleCount(mw);
                sb.AppendLine($"  InkSmoothingManager SampleCount         : {smSample}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  MainWindow 反射失败: {ex.Message}");
            }
        }

        private static int ReadInstanceCollectionCount(object instance, string fieldName)
        {
            try
            {
                var fi = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi?.GetValue(instance) is System.Collections.ICollection c)
                {
                    return c.Count;
                }
            }
            catch { }
            return 0;
        }

        private static T ReadInstanceField<T>(object instance, string fieldName)
        {
            try
            {
                var fi = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi != null)
                {
                    var v = fi.GetValue(instance);
                    if (v is T t) return t;
                    try { return (T)System.Convert.ChangeType(v, typeof(T)); }
                    catch { return default; }
                }
            }
            catch { }
            return default;
        }

        private static long ReadTimeMachineCount(Ink_Canvas.MainWindow mw, out string bestFieldName)
        {
            bestFieldName = null;
            try
            {
                if (mw == null) return 0;

                // 1. MainWindow 内 partial 字段 `timeMachine` (MW_TimeMachine.cs)
                var fi = mw.GetType().GetField("timeMachine", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                object tmInstance = fi?.GetValue(mw);

                // 2. 公开属性 `TimeMachine`
                if (tmInstance == null)
                {
                    var prop = mw.GetType().GetProperty("TimeMachine", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    tmInstance = prop?.GetValue(mw);
                }

                // 3. 公开静态属性 TimeMachine.Instance
                if (tmInstance == null)
                {
                    var tmType = Type.GetType("Ink_Canvas.Helpers.TimeMachine, Ink_Canvas");
                    var instProp = tmType?.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    tmInstance = instProp?.GetValue(null);
                }

                if (tmInstance == null) return 0;

                // 4. 枚举所有字段,选 List/Dictionary 计数最大的(最像历史记录)
                long best = 0;
                foreach (var f in tmInstance.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    try
                    {
                        var v = f.GetValue(tmInstance);
                        if (v is System.Collections.ICollection c && c.Count > best)
                        {
                            if (v is System.Collections.IList
                                || v is System.Collections.IDictionary
                                || v is System.Collections.Generic.ICollection<object>)
                            {
                                best = c.Count;
                                bestFieldName = f.Name;
                            }
                        }
                    }
                    catch { }
                }
                return best;
            }
            catch
            {
                return 0;
            }
        }

        private static long ReadInkSmoothingSampleCount(Ink_Canvas.MainWindow mw)
        {
            try
            {
                var prop = mw.GetType().GetProperty("InkSmoothingManagerInstance", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var manager = prop?.GetValue(mw);
                if (manager == null) return -1;

                // InkSmoothingManager.PerformanceMonitor.SampleCount 或 _samples
                var monitorProp = manager.GetType().GetProperty("PerformanceMonitor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var monitor = monitorProp?.GetValue(manager);
                if (monitor != null)
                {
                    var sampleProp = monitor.GetType().GetProperty("SampleCount");
                    if (sampleProp != null && sampleProp.PropertyType == typeof(int))
                    {
                        return (int)sampleProp.GetValue(monitor);
                    }
                }

                // 退路:在 manager 上找 SampleCount 字段
                foreach (var fi in manager.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (fi.Name.IndexOf("sample", StringComparison.OrdinalIgnoreCase) >= 0
                        && fi.FieldType == typeof(int))
                    {
                        return (int)fi.GetValue(manager);
                    }
                }
            }
            catch { }
            return 0;
        }

        private static void AppendAssembliesSection(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("--- 5. Loaded Assemblies ---");
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                long totalBytes = 0;
                var sb2 = new StringBuilder();
                foreach (var asm in assemblies)
                {
                    long bytes = 0;
                    try
                    {
                        if (File.Exists(asm.Location))
                        {
                            var fi = new FileInfo(asm.Location);
                            bytes = fi.Length;
                            totalBytes += bytes;
                        }
                    }
                    catch { }

                    sb2.AppendLine($"  {asm.GetName().Name,-50} v{asm.GetName().Version?.ToString() ?? "?"}  ({bytes / 1024} KB)");
                }
                sb.AppendLine($"  Count               : {assemblies.Length}");
                sb.AppendLine($"  On-disk total       : {totalBytes / 1024 / 1024} MB");
                sb.Append(sb2);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  (程序集列表失败: {ex.Message})");
            }
        }

        #endregion

        #region Helpers

        private static double ToMb(long bytes) => bytes / (1024.0 * 1024.0);
        private static double ToMb(IntPtr ptr) => ptr.ToInt64() / (1024.0 * 1024.0);

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s.Substring(0, max) + "…");

        private static void AppendPptStreamSection(StringBuilder sb, Ink_Canvas.MainWindow mw)
        {
            try
            {
                var field = mw.GetType().GetField("_memoryStreams", BindingFlags.Instance | BindingFlags.NonPublic);
                if (!(field?.GetValue(mw) is System.Collections.IDictionary streams))
                {
                    sb.AppendLine("  PPT MemoryStreams                       : unavailable");
                    return;
                }

                long totalBytes = 0;
                foreach (System.Collections.DictionaryEntry entry in streams)
                {
                    if (entry.Value is MemoryStream stream)
                    {
                        totalBytes += stream.Length;
                    }
                }
                sb.AppendLine($"  PPT MemoryStreams                       : {streams.Count} 条, {ToMb(totalBytes):F1} MB");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  PPT MemoryStreams: 读取失败 ({ex.Message})");
            }
        }

        private static void CountVisualTypes(DependencyObject root, Dictionary<string, int> counts)
        {
            if (root == null) return;

            string typeName = root.GetType().Name;
            if (counts.ContainsKey(typeName))
            {
                counts[typeName]++;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                CountVisualTypes(VisualTreeHelper.GetChild(root, i), counts);
            }
        }

        private static void CountVisuals(DependencyObject root, ref long descendants, ref int frames, int depth)
        {
            if (root == null) return;
            descendants++;
            if (root is System.Windows.Controls.Frame) frames++;

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                CountVisuals(child, ref descendants, ref frames, depth + 1);
            }
        }

        private static int ReadSettingsPageCacheCount(Ink_Canvas.Windows.SettingsViews.SettingsWindow sw)
        {
            try
            {
                var field = sw.GetType().GetField("_pages", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field?.GetValue(sw) is System.Collections.IDictionary dict)
                {
                    return dict.Count;
                }
            }
            catch { }
            return 0;
        }

        private static long ReadStaticField<T>(Type type, string fieldName)
        {
            try
            {
                var fi = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
                if (fi?.GetValue(null) is ICollection<T> coll) return coll.Count;
                if (fi?.GetValue(null) is System.Collections.ICollection legacy) return legacy.Count;
            }
            catch { }
            return 0;
        }

        #endregion
    }
}