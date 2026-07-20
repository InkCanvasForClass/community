using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 插件错误恢复服务，当某插件多次加载失败或连续抛异常时。
    /// 自动将其标记为"自动禁用"并写入恢复令牌文件。用户可在插件列表上手动重置。
    /// </summary>
    public class PluginErrorRecoveryService
    {
        private readonly string _recoveryFile;
        private readonly Dictionary<string, PluginErrorRecord> _records = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        /// <summary>
        /// 在最近 <see cref="FailureWindowMinutes"/> 分钟内连续发生 <see cref="FailureThreshold"/> 次失败，自动禁用插件。
        /// </summary>
        public const int FailureThreshold = 3;
        public const int FailureWindowMinutes = 30;

        /// <summary>
        /// 当插件被自动禁用后，用户仍能在 UI 上看到错误信息，需手动重置才能再次加载。
        /// </summary>
        public PluginErrorRecoveryService(string basePath)
        {
            _recoveryFile = Path.Combine(basePath, "Configs", "plugin_error_recovery.json");
            Load();
        }

        public IReadOnlyDictionary<string, PluginErrorRecord> Records
        {
            get { lock (_lock) return new Dictionary<string, PluginErrorRecord>(_records); }
        }

        /// <summary>
        /// 报告一次加载失败。如果触达阈值，自动禁用插件。
        /// </summary>
        public PluginErrorReport ReportFailure(string pluginId, string pluginName, Exception ex)
        {
            if (string.IsNullOrEmpty(pluginId)) return PluginErrorReport.NoneResult();

            PluginErrorRecord rec;
            bool autoDisabledNow = false;

            lock (_lock)
            {
                if (!_records.TryGetValue(pluginId, out rec))
                {
                    rec = new PluginErrorRecord
                    {
                        PluginId = pluginId,
                        PluginName = pluginName ?? pluginId,
                        FirstFailureAt = DateTime.UtcNow,
                        FailureTimestamps = new List<DateTime>()
                    };
                    _records[pluginId] = rec;
                }

                rec.PluginName = pluginName ?? rec.PluginName;
                rec.LastFailureAt = DateTime.UtcNow;
                rec.LastErrorMessage = ex?.Message ?? "";
                rec.LastStackTrace = ex?.StackTrace ?? "";
                rec.FailureTimestamps.Add(DateTime.UtcNow);

                // 清理窗口外的历史时间戳
                var cutoff = DateTime.UtcNow.AddMinutes(-FailureWindowMinutes);
                rec.FailureTimestamps = rec.FailureTimestamps.Where(t => t >= cutoff).ToList();

                // 触发自动禁用
                if (rec.FailureTimestamps.Count >= FailureThreshold && !rec.AutoDisabled)
                {
                    rec.AutoDisabled = true;
                    rec.AutoDisabledAt = DateTime.UtcNow;
                    autoDisabledNow = true;
                }

                Save();
            }

            return autoDisabledNow
                ? PluginErrorReport.CreateAutoDisabled(rec)
                : PluginErrorReport.CreateWarned(rec);
        }

        /// <summary>
        /// 重置插件的错误记录并清除自动禁用标记，下一次启动会重新尝试加载。
        /// </summary>
        public bool Reset(string pluginId)
        {
            lock (_lock)
            {
                if (!_records.Remove(pluginId)) return false;
                Save();
                return true;
            }
        }

        /// <summary>
        /// 查询插件是否已被自动禁用。
        /// </summary>
        public bool IsAutoDisabled(string pluginId)
        {
            lock (_lock)
            {
                return _records.TryGetValue(pluginId, out var rec) && rec.AutoDisabled;
            }
        }

        /// <summary>
        /// 获取某个插件的错误摘要，用于 UI 展示。
        /// </summary>
        public PluginErrorRecord GetRecord(string pluginId)
        {
            lock (_lock)
            {
                return _records.TryGetValue(pluginId, out var rec) ? rec.Clone() : null;
            }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_recoveryFile)) return;
                var json = File.ReadAllText(_recoveryFile);
                var list = JsonSerializer.Deserialize<List<PluginErrorRecord>>(json);
                if (list == null) return;
                lock (_lock)
                {
                    _records.Clear();
                    foreach (var r in list)
                    {
                        if (!string.IsNullOrEmpty(r.PluginId))
                            _records[r.PluginId] = r;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PluginErrorRecovery | 加载失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_recoveryFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var list = _records.Values.ToList();
                var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_recoveryFile, json);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PluginErrorRecovery | 保存失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }
    }

    /// <summary>
    /// 单个插件的错误记录。
    /// </summary>
    public class PluginErrorRecord
    {
        public string PluginId { get; set; } = "";
        public string PluginName { get; set; } = "";
        public DateTime FirstFailureAt { get; set; }
        public DateTime LastFailureAt { get; set; }
        public List<DateTime> FailureTimestamps { get; set; } = new();
        public string LastErrorMessage { get; set; } = "";
        public string LastStackTrace { get; set; } = "";
        public bool AutoDisabled { get; set; }
        public DateTime? AutoDisabledAt { get; set; }

        public PluginErrorRecord Clone() => new()
        {
            PluginId = PluginId,
            PluginName = PluginName,
            FirstFailureAt = FirstFailureAt,
            LastFailureAt = LastFailureAt,
            FailureTimestamps = FailureTimestamps.ToList(),
            LastErrorMessage = LastErrorMessage,
            LastStackTrace = LastStackTrace,
            AutoDisabled = AutoDisabled,
            AutoDisabledAt = AutoDisabledAt
        };
    }

    /// <summary>
    /// 错误报告——记录 <see cref="PluginErrorRecoveryService.ReportFailure"/> 的处置结果。
    /// </summary>
    public class PluginErrorReport
    {
        public bool Warned { get; set; }
        public bool AutoDisabled { get; set; }
        public PluginErrorRecord Record { get; set; }

        public static PluginErrorReport NoneResult() => new();
        public static PluginErrorReport CreateWarned(PluginErrorRecord r) => new() { Warned = true, Record = r };
        public static PluginErrorReport CreateAutoDisabled(PluginErrorRecord r) => new() { Warned = true, AutoDisabled = true, Record = r };
    }
}
