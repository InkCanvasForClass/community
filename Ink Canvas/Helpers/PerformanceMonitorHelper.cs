using Ink_Canvas.Windows.SettingsViews.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// Monitors CPU/memory usage during app runtime and manages performance history.
    /// </summary>
    public static class PerformanceMonitorHelper
    {
        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS ppsmemCounters, uint cb);

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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out long lpIdleTime, out long lpKernelTime, out long lpUserTime);

        private static Timer _samplingTimer;
        private static readonly object _lock = new object();
        private static List<double> _cpuSamples = new List<double>();
        private static List<double> _memorySamples = new List<double>();
        private static DateTime _sessionStart;
        private static TimeSpan _lastTotalProcessorTime;
        private static DateTime _lastSampleTime;
        private static Process _currentProcess;
        private static long _lastIdleTime, _lastKernelTime, _lastUserTime;
        private static bool _isMonitoring;

        private const string HistoryFileName = "Configs/PerformanceHistory.json";
        private const int MaxHistoryCount = 30;
        private const int SamplingIntervalMs = 5000;

        /// <summary>Current session's average CPU percent (updated on each sample).</summary>
        public static double CurrentAvgCpu { get; private set; }

        /// <summary>Current session's current memory in MB.</summary>
        public static double CurrentMemoryMb { get; private set; }

        /// <summary>Current system-wide CPU usage percent.</summary>
        public static double CurrentSystemCpuPercent { get; private set; }

        /// <summary>Cached ink smoothing stats (updated by InkSmoothingManager after each smoothing).</summary>
        private static InkSmoothingDetailedStats _cachedSmoothingStats;

        /// <summary>Whether monitoring is active.</summary>
        public static bool IsMonitoring => _isMonitoring;

        /// <summary>Number of samples collected this session.</summary>
        public static int SampleCount { get; private set; }

        /// <summary>Fired when a new sample is collected. Args: (cpuPercent, memoryMb).</summary>
        public static event Action<double, double> SampleCollected;

        /// <summary>
        /// Starts monitoring if enabled in settings. Call once at app startup.
        /// </summary>
        public static void StartIfEnabled()
        {
            if (!SettingsManager.Settings.Performance.IsMonitoringEnabled)
                return;

            Start();
        }

        /// <summary>
        /// Starts the monitoring loop.
        /// </summary>
        public static void Start()
        {
            if (_isMonitoring) return;

            try
            {
                _currentProcess = Process.GetCurrentProcess();
                _lastTotalProcessorTime = _currentProcess.TotalProcessorTime;
                _lastSampleTime = DateTime.UtcNow;
                _sessionStart = DateTime.Now;

                // 初始化系统 CPU 基线
                GetSystemTimes(out _lastIdleTime, out _lastKernelTime, out _lastUserTime);

                lock (_lock)
                {
                    _cpuSamples.Clear();
                    _memorySamples.Clear();
                }
                SampleCount = 0;
                CurrentAvgCpu = 0;
                CurrentMemoryMb = 0;
                _isMonitoring = true;

                _samplingTimer = new Timer(OnSample, null, SamplingIntervalMs, SamplingIntervalMs);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PerformanceMonitorHelper.Start: {ex.Message}");
                _isMonitoring = false;
            }
        }

        /// <summary>
        /// Called by InkSmoothingManager after each smoothing operation to cache the latest stats.
        /// </summary>
        public static void UpdateSmoothingStats(InkSmoothingDetailedStats stats)
        {
            _cachedSmoothingStats = stats;
        }

        /// <summary>
        /// Stops monitoring and saves the run record. Call at app shutdown.
        /// </summary>
        public static void StopAndSave()
        {
            if (!_isMonitoring) return;

            try
            {
                _samplingTimer?.Dispose();
                _samplingTimer = null;
                _isMonitoring = false;

                // Take one final sample
                TakeSample();

                if (SampleCount > 0)
                {
                    var record = new PerformanceRunRecord
                    {
                        StartTime = _sessionStart.ToString("yyyy-MM-dd HH:mm:ss"),
                        EndTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        DurationSeconds = (DateTime.Now - _sessionStart).TotalSeconds,
                        AvgCpuPercent = Math.Round(CurrentAvgCpu, 2),
                        PeakCpuPercent = Math.Round(_cpuSamples.Count > 0 ? _cpuSamples.Max() : 0, 2),
                        AvgMemoryMb = Math.Round(_memorySamples.Count > 0 ? _memorySamples.Average() : 0, 1),
                        PeakMemoryMb = Math.Round(_memorySamples.Count > 0 ? _memorySamples.Max() : 0, 1),
                        SampleCount = SampleCount
                    };

                    // 记录墨迹平滑统计
                    if (_cachedSmoothingStats != null && _cachedSmoothingStats.SampleCount > 0)
                    {
                        record.SmoothingSampleCount = _cachedSmoothingStats.SampleCount;
                        record.SmoothingAvgTotalMs = Math.Round(_cachedSmoothingStats.AvgTotalMs, 2);
                        record.SmoothingMaxTotalMs = Math.Round(_cachedSmoothingStats.MaxTotalMs, 2);
                        record.SmoothingAvgBezierMs = Math.Round(_cachedSmoothingStats.AvgBezierMs, 2);
                        record.SmoothingAvgResampleMs = Math.Round(_cachedSmoothingStats.AvgResampleMs, 2);
                        record.SmoothingAvgInputPoints = Math.Round(_cachedSmoothingStats.AvgInputPoints, 0);
                        record.SmoothingAvgOutputPoints = Math.Round(_cachedSmoothingStats.AvgOutputPoints, 0);
                    }

                    AppendRecord(record);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PerformanceMonitorHelper.StopAndSave: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops monitoring without saving (e.g., when user disables monitoring).
        /// </summary>
        public static void StopWithoutSaving()
        {
            _samplingTimer?.Dispose();
            _samplingTimer = null;
            _isMonitoring = false;
            CurrentAvgCpu = 0;
            CurrentMemoryMb = 0;
            SampleCount = 0;
        }

        private static void OnSample(object state)
        {
            TakeSample();
        }

        private static void TakeSample()
        {
            try
            {
                if (_currentProcess == null || _currentProcess.HasExited) return;

                var now = DateTime.UtcNow;
                var currentTotalProcessorTime = _currentProcess.TotalProcessorTime;
                var cpuTimeDiff = currentTotalProcessorTime - _lastTotalProcessorTime;
                var realTimeDiff = now - _lastSampleTime;

                double cpuPercent = 0;
                if (realTimeDiff.TotalMilliseconds > 0)
                {
                    cpuPercent = (cpuTimeDiff.TotalMilliseconds / (realTimeDiff.TotalMilliseconds * Environment.ProcessorCount)) * 100.0;
                    cpuPercent = Math.Max(0, Math.Min(100, cpuPercent));
                }

                // 计算系统整体 CPU 占用
                double systemCpuPercent = 0;
                try
                {
                    if (GetSystemTimes(out long idleTime, out long kernelTime, out long userTime))
                    {
                        long idleDiff = idleTime - _lastIdleTime;
                        long kernelDiff = kernelTime - _lastKernelTime;
                        long userDiff = userTime - _lastUserTime;
                        long totalDiff = kernelDiff + userDiff;
                        if (totalDiff > 0)
                        {
                            systemCpuPercent = (1.0 - (double)idleDiff / totalDiff) * 100.0;
                            systemCpuPercent = Math.Max(0, Math.Min(100, systemCpuPercent));
                        }
                        _lastIdleTime = idleTime;
                        _lastKernelTime = kernelTime;
                        _lastUserTime = userTime;
                    }
                }
                catch { }

                // Refresh process info to get updated memory
                _currentProcess.Refresh();
                // 使用 GetProcessMemoryInfo 获取 PrivateUsage（与任务管理器一致的私有提交内存）
                double memoryMb = 0;
                try
                {
                    var counters = new PROCESS_MEMORY_COUNTERS { cb = (uint)Marshal.SizeOf<PROCESS_MEMORY_COUNTERS>() };
                    if (GetProcessMemoryInfo(_currentProcess.Handle, out counters, counters.cb))
                    {
                        memoryMb = counters.PrivateUsage.ToInt64() / (1024.0 * 1024.0);
                    }
                }
                catch { }
                if (memoryMb <= 0)
                    memoryMb = _currentProcess.PrivateMemorySize64 / (1024.0 * 1024.0);

                _lastTotalProcessorTime = currentTotalProcessorTime;
                _lastSampleTime = now;

                lock (_lock)
                {
                    _cpuSamples.Add(cpuPercent);
                    _memorySamples.Add(memoryMb);
                    SampleCount = _cpuSamples.Count;
                    CurrentAvgCpu = _cpuSamples.Average();
                    CurrentMemoryMb = memoryMb;
                    CurrentSystemCpuPercent = systemCpuPercent;
                }

                SampleCollected?.Invoke(cpuPercent, memoryMb);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PerformanceMonitorHelper.TakeSample: {ex.Message}");
            }
        }

        #region History Management

        private static string GetHistoryFilePath()
        {
            return Path.Combine(App.RootPath, HistoryFileName);
        }

        /// <summary>
        /// Loads run history from disk.
        /// </summary>
        public static List<PerformanceRunRecord> LoadHistory()
        {
            try
            {
                var path = GetHistoryFilePath();
                if (!File.Exists(path))
                    return new List<PerformanceRunRecord>();

                var json = File.ReadAllText(path);
                var records = JsonConvert.DeserializeObject<List<PerformanceRunRecord>>(json);
                return records ?? new List<PerformanceRunRecord>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PerformanceMonitorHelper.LoadHistory: {ex.Message}");
                return new List<PerformanceRunRecord>();
            }
        }

        /// <summary>
        /// Appends a run record and trims to MaxHistoryCount.
        /// </summary>
        private static void AppendRecord(PerformanceRunRecord record)
        {
            try
            {
                var history = LoadHistory();
                history.Add(record);

                // Keep only the last N records
                while (history.Count > MaxHistoryCount)
                {
                    history.RemoveAt(0);
                }

                SaveHistory(history);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PerformanceMonitorHelper.AppendRecord: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves history list to disk.
        /// </summary>
        public static void SaveHistory(List<PerformanceRunRecord> history)
        {
            try
            {
                var path = GetHistoryFilePath();
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonConvert.SerializeObject(history, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PerformanceMonitorHelper.SaveHistory: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears all history records.
        /// </summary>
        public static void ClearHistory()
        {
            SaveHistory(new List<PerformanceRunRecord>());
        }

        #endregion

        #region Device Scoring

        /// <summary>
        /// Runs device performance evaluation asynchronously.
        /// Returns (overallScore, cpuScore, memoryScore, diskScore).
        /// </summary>
        public static Task<(int overall, int cpu, int memory, int disk)> RunDeviceEvaluationAsync()
        {
            return Task.Run(() =>
            {
                int cpuScore = EvaluateCpu();
                int memoryScore = EvaluateMemory();
                int diskScore = EvaluateDisk();

                int overall = (int)Math.Round((cpuScore * 0.4 + memoryScore * 0.2 + diskScore * 0.4));
                overall = Math.Max(0, Math.Min(100, overall));

                return (overall, cpuScore, memoryScore, diskScore);
            });
        }

        private static int EvaluateCpu()
        {
            try
            {
                int coreCount = Environment.ProcessorCount;
                double baseScore = 0;

                // Try to get CPU info via WMI
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT Name, MaxClockSpeed FROM Win32_Processor");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var name = obj["Name"]?.ToString() ?? "";
                        double.TryParse(obj["MaxClockSpeed"]?.ToString(), out double maxClockSpeed);

                        // Score based on clock speed and core count
                        // Baseline: 4 cores @ 3.0 GHz = 60 points
                        double clockScore = Math.Min(40, (maxClockSpeed / 3000.0) * 30);
                        double coreScore = Math.Min(30, coreCount * 5);

                        // Bonus for modern CPU features
                        double bonus = 0;
                        if (name.Contains("i7") || name.Contains("i9") || name.Contains("Ryzen 7") || name.Contains("Ryzen 9"))
                            bonus = 15;
                        else if (name.Contains("i5") || name.Contains("Ryzen 5"))
                            bonus = 10;
                        else if (name.Contains("i3") || name.Contains("Ryzen 3"))
                            bonus = 5;

                        baseScore = clockScore + coreScore + bonus;
                        break;
                    }
                }
                catch
                {
                    // WMI not available, fall back to core count only
                    baseScore = Math.Min(60, coreCount * 10);
                }

                return Math.Max(0, Math.Min(100, (int)Math.Round(baseScore)));
            }
            catch
            {
                return 50; // Default middle score
            }
        }

        private static int EvaluateMemory()
        {
            double totalGb = 0;

            // 方法1: WMI
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    if (ulong.TryParse(obj["TotalPhysicalMemory"]?.ToString(), out ulong bytes) && bytes > 0)
                    {
                        totalGb = bytes / (1024.0 * 1024.0 * 1024.0);
                    }
                    break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EvaluateMemory WMI failed: {ex.Message}");
            }

            // 方法2: GC API (.NET 6 fallback)
            if (totalGb <= 0)
            {
                try
                {
                    var memInfo = GC.GetGCMemoryInfo();
                    if (memInfo.TotalAvailableMemoryBytes > 0)
                        totalGb = memInfo.TotalAvailableMemoryBytes / (1024.0 * 1024.0 * 1024.0);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"EvaluateMemory GC fallback failed: {ex.Message}");
                }
            }

            Debug.WriteLine($"EvaluateMemory: totalGb={totalGb:F2}");

            if (totalGb >= 32) return 95;
            if (totalGb >= 16) return 80;
            if (totalGb >= 12) return 70;
            if (totalGb >= 8) return 60;
            if (totalGb >= 4) return 40;
            if (totalGb >= 2) return 20;
            if (totalGb > 0) return 10;
            return 50; // 完全无法检测时的默认值
        }

        private static int EvaluateDisk()
        {
            try
            {
                // Get the drive where the app is running
                var appPath = App.RootPath ?? AppDomain.CurrentDomain.BaseDirectory;
                var driveInfo = new DriveInfo(Path.GetPathRoot(appPath));

                if (!driveInfo.IsReady)
                    return 50;

                long totalGb = driveInfo.TotalSize / (1024L * 1024L * 1024L);
                long freeGb = driveInfo.AvailableFreeSpace / (1024L * 1024L * 1024L);

                int score = 50; // Base

                // Score based on free space ratio
                double freeRatio = (double)freeGb / totalGb;
                if (freeRatio > 0.5) score += 15;
                else if (freeRatio > 0.2) score += 10;
                else if (freeRatio > 0.1) score += 5;

                // Score based on total size
                if (totalGb >= 500) score += 15;
                else if (totalGb >= 256) score += 10;
                else if (totalGb >= 128) score += 5;

                // Bonus for having reasonable free space
                if (freeGb >= 50) score += 10;
                else if (freeGb >= 20) score += 5;

                return Math.Max(0, Math.Min(100, score));
            }
            catch
            {
                return 50;
            }
        }

        #endregion
    }
}
