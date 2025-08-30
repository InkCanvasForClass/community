using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 设备标识符和使用频率监控类
    /// </summary>
    internal static class DeviceIdentifier
    {
        // 文件路径策略
        private static readonly string DeviceIdFilePath = Path.Combine(App.RootPath, "device_id.dat");
        private static readonly string UsageStatsFilePath = Path.Combine(App.RootPath, "usage_stats.enc");
        private static readonly string UsageStatsBackupPath = Path.Combine(App.RootPath, "saves", "usage_stats_backup.enc");

        private static readonly string DeviceId;
        private static readonly object fileLock = new object();

        static DeviceIdentifier()
        {
            // 在静态构造函数中初始化设备ID
            DeviceId = GetOrCreateDeviceId();
        }

        /// <summary>
        /// 获取或创建设备ID
        /// </summary>
        /// <returns>25字符的唯一设备标识符</returns>
        public static string GetDeviceId()
        {
            return DeviceId;
        }

        /// <summary>
        /// 获取或创建设备ID
        /// </summary>
        private static string GetOrCreateDeviceId()
        {
            lock (fileLock)
            {
                try
                {
                    // 1. 尝试从主文件读取设备ID
                    string deviceId = LoadDeviceIdFromFile(DeviceIdFilePath);
                    if (!string.IsNullOrEmpty(deviceId))
                    {
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 从主文件读取设备ID: {deviceId}");
                        return deviceId;
                    }

                    // 2. 生成新的设备ID
                    string newDeviceId = GenerateDeviceId();
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 生成新设备ID: {newDeviceId}");

                    // 3. 保存到主文件
                    SaveDeviceIdToFile(DeviceIdFilePath, newDeviceId);

                    return newDeviceId;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 获取设备ID时出错: {ex.Message}", LogHelper.LogType.Error);
                    // 返回一个基于时间戳的备用ID
                    return GenerateFallbackDeviceId();
                }
            }
        }

        /// <summary>
        /// 生成25字符的唯一设备ID
        /// </summary>
        private static string GenerateDeviceId()
        {
            try
            {
                // 收集硬件信息
                var hardwareInfo = new StringBuilder();

                // 使用反射获取硬件信息，避免直接引用System.Management
                try
                {
                    // 尝试加载System.Management程序集
                    var assembly = Assembly.Load("System.Management");
                    if (assembly != null)
                    {
                        // CPU信息
                        try
                        {
                            var searcherType = assembly.GetType("System.Management.ManagementObjectSearcher");
                            var searcher = Activator.CreateInstance(searcherType, "SELECT ProcessorId FROM Win32_Processor");
                            var getMethod = searcherType.GetMethod("Get");
                            var enumerator = getMethod.Invoke(searcher, null);

                            var moveNextMethod = enumerator.GetType().GetMethod("MoveNext");
                            var currentProperty = enumerator.GetType().GetProperty("Current");

                            if ((bool)moveNextMethod.Invoke(enumerator, null))
                            {
                                var obj = currentProperty.GetValue(enumerator);
                                var indexer = obj.GetType().GetProperty("Item", new[] { typeof(string) });
                                var processorId = indexer.GetValue(obj, new object[] { "ProcessorId" });
                                hardwareInfo.Append(processorId?.ToString() ?? "");
                            }

                            var disposeMethod = searcher.GetType().GetMethod("Dispose");
                            disposeMethod?.Invoke(searcher, null);
                        }
                        catch { }

                        // 主板序列号
                        try
                        {
                            var searcherType = assembly.GetType("System.Management.ManagementObjectSearcher");
                            var searcher = Activator.CreateInstance(searcherType, "SELECT SerialNumber FROM Win32_BaseBoard");
                            var getMethod = searcherType.GetMethod("Get");
                            var enumerator = getMethod.Invoke(searcher, null);

                            var moveNextMethod = enumerator.GetType().GetMethod("MoveNext");
                            var currentProperty = enumerator.GetType().GetProperty("Current");

                            if ((bool)moveNextMethod.Invoke(enumerator, null))
                            {
                                var obj = currentProperty.GetValue(enumerator);
                                var indexer = obj.GetType().GetProperty("Item", new[] { typeof(string) });
                                var serialNumber = indexer.GetValue(obj, new object[] { "SerialNumber" });
                                hardwareInfo.Append(serialNumber?.ToString() ?? "");
                            }

                            var disposeMethod = searcher.GetType().GetMethod("Dispose");
                            disposeMethod?.Invoke(searcher, null);
                        }
                        catch { }

                        // BIOS序列号
                        try
                        {
                            var searcherType = assembly.GetType("System.Management.ManagementObjectSearcher");
                            var searcher = Activator.CreateInstance(searcherType, "SELECT SerialNumber FROM Win32_BIOS");
                            var getMethod = searcherType.GetMethod("Get");
                            var enumerator = getMethod.Invoke(searcher, null);

                            var moveNextMethod = enumerator.GetType().GetMethod("MoveNext");
                            var currentProperty = enumerator.GetType().GetProperty("Current");

                            if ((bool)moveNextMethod.Invoke(enumerator, null))
                            {
                                var obj = currentProperty.GetValue(enumerator);
                                var indexer = obj.GetType().GetProperty("Item", new[] { typeof(string) });
                                var serialNumber = indexer.GetValue(obj, new object[] { "SerialNumber" });
                                hardwareInfo.Append(serialNumber?.ToString() ?? "");
                            }

                            var disposeMethod = searcher.GetType().GetMethod("Dispose");
                            disposeMethod?.Invoke(searcher, null);
                        }
                        catch { }

                        // 主硬盘序列号
                        try
                        {
                            var searcherType = assembly.GetType("System.Management.ManagementObjectSearcher");
                            var searcher = Activator.CreateInstance(searcherType, "SELECT SerialNumber FROM Win32_DiskDrive WHERE MediaType='Fixed hard disk media'");
                            var getMethod = searcherType.GetMethod("Get");
                            var enumerator = getMethod.Invoke(searcher, null);

                            var moveNextMethod = enumerator.GetType().GetMethod("MoveNext");
                            var currentProperty = enumerator.GetType().GetProperty("Current");

                            if ((bool)moveNextMethod.Invoke(enumerator, null))
                            {
                                var obj = currentProperty.GetValue(enumerator);
                                var indexer = obj.GetType().GetProperty("Item", new[] { typeof(string) });
                                var serialNumber = indexer.GetValue(obj, new object[] { "SerialNumber" });
                                hardwareInfo.Append(serialNumber?.ToString() ?? "");
                            }

                            var disposeMethod = searcher.GetType().GetMethod("Dispose");
                            disposeMethod?.Invoke(searcher, null);
                        }
                        catch { }
                    }
                }
                catch { }

                // 如果硬件信息不足，添加系统信息
                if (hardwareInfo.Length < 10)
                {
                    hardwareInfo.Append(Environment.MachineName);
                    hardwareInfo.Append(Environment.UserName);
                    hardwareInfo.Append(Environment.OSVersion);
                }

                // 生成哈希
                using (var sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(hardwareInfo.ToString()));
                    string hashString = BitConverter.ToString(hashBytes).Replace("-", "");

                    // 取前25个字符，确保唯一性
                    string deviceId = hashString.Substring(0, 25);

                    // 添加校验位（第25位）
                    int checksum = 0;
                    for (int i = 0; i < 24; i++)
                    {
                        checksum += Convert.ToInt32(deviceId[i]);
                    }
                    checksum %= 36; // 0-9, A-Z
                    char checksumChar = checksum < 10 ? (char)(checksum + '0') : (char)(checksum - 10 + 'A');

                    return deviceId.Substring(0, 24) + checksumChar;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 生成设备ID时出错: {ex.Message}", LogHelper.LogType.Error);
                return GenerateFallbackDeviceId();
            }
        }

        /// <summary>
        /// 生成备用设备ID（基于时间戳）
        /// </summary>
        private static string GenerateFallbackDeviceId()
        {
            try
            {
                string timestamp = DateTime.Now.Ticks.ToString("X");
                string random = Guid.NewGuid().ToString("N").Substring(0, 8);
                string combined = timestamp + random;

                using (var sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                    string hashString = BitConverter.ToString(hashBytes).Replace("-", "");
                    return hashString.Substring(0, 25);
                }
            }
            catch
            {
                // 最后的备用方案
                return "ICC" + DateTime.Now.ToString("yyyyMMddHHmmss") + "000000000";
            }
        }

        /// <summary>
        /// 验证设备ID格式
        /// </summary>
        private static bool IsValidDeviceId(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId) || deviceId.Length != 25)
                return false;

            // 验证字符集（只允许数字和大写字母）
            if (!deviceId.All(c => char.IsLetterOrDigit(c) && (char.IsDigit(c) || char.IsUpper(c))))
                return false;

            // 验证校验位
            try
            {
                int checksum = 0;
                for (int i = 0; i < 24; i++)
                {
                    checksum += Convert.ToInt32(deviceId[i]);
                }
                checksum %= 36;
                char expectedChecksum = checksum < 10 ? (char)(checksum + '0') : (char)(checksum - 10 + 'A');
                return deviceId[24] == expectedChecksum;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 从文件加载设备ID
        /// </summary>
        private static string LoadDeviceIdFromFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string content = File.ReadAllText(filePath).Trim();
                    if (IsValidDeviceId(content))
                    {
                        return content;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 从文件加载设备ID失败 ({filePath}): {ex.Message}", LogHelper.LogType.Error);
            }
            return null;
        }

        /// <summary>
        /// 保存设备ID到文件
        /// </summary>
        private static void SaveDeviceIdToFile(string filePath, string deviceId)
        {
            try
            {
                // 确保目录存在
                var directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(filePath, deviceId);

                LogHelper.WriteLogToFile($"DeviceIdentifier | 设备ID已保存到: {filePath}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 保存设备ID到文件失败 ({filePath}): {ex.Message}", LogHelper.LogType.Error);
            }
        }



        /// <summary>
        /// 使用频率统计数据结构（优化至秒级精度）
        /// </summary>
        private class UsageStats
        {
            [JsonProperty("deviceId")]
            public string DeviceId { get; set; }

            [JsonProperty("lastLaunchTime")]
            public DateTime LastLaunchTime { get; set; }

            [JsonProperty("launchCount")]
            public int LaunchCount { get; set; }

            // 新的秒级精度字段
            [JsonProperty("totalUsageSeconds")]
            public long TotalUsageSeconds { get; set; }

            [JsonProperty("averageSessionSeconds")]
            public double AverageSessionSeconds { get; set; }

            // 保留旧字段以保持向后兼容性（已弃用）
            [JsonProperty("totalUsageMinutes")]
            [Obsolete("已弃用，请使用 TotalUsageSeconds")]
            public long TotalUsageMinutes { get; set; }

            [JsonProperty("averageSessionMinutes")]
            [Obsolete("已弃用，请使用 AverageSessionSeconds")]
            public double AverageSessionMinutes { get; set; }

            [JsonProperty("lastUpdateCheck")]
            public DateTime LastUpdateCheck { get; set; }

            [JsonProperty("updatePriority")]
            public UpdatePriority UpdatePriority { get; set; }

            [JsonProperty("usageFrequency")]
            public UsageFrequency UsageFrequency { get; set; }

            [JsonProperty("lastModified")]
            public DateTime LastModified { get; set; }

            // 每周统计数据（秒级精度）
            [JsonProperty("weeklyLaunchCount")]
            public int WeeklyLaunchCount { get; set; }

            [JsonProperty("weeklyUsageSeconds")]
            public long WeeklyUsageSeconds { get; set; }

            [JsonProperty("weekStartDate")]
            public DateTime WeekStartDate { get; set; }

            [JsonProperty("lastWeekLaunchCount")]
            public int LastWeekLaunchCount { get; set; }

            [JsonProperty("lastWeekUsageSeconds")]
            public long LastWeekUsageSeconds { get; set; }

            // 保留旧字段以保持向后兼容性（已弃用）
            [JsonProperty("weeklyUsageMinutes")]
            [Obsolete("已弃用，请使用 WeeklyUsageSeconds")]
            public long WeeklyUsageMinutes { get; set; }

            [JsonProperty("lastWeekUsageMinutes")]
            [Obsolete("已弃用，请使用 LastWeekUsageSeconds")]
            public long LastWeekUsageMinutes { get; set; }

            /// <summary>
            /// 数据迁移：从分钟精度迁移到秒级精度
            /// </summary>
            public void MigrateToSecondsPrecision()
            {
                try
                {
                    // 如果新字段为空但旧字段有数据，进行迁移
                    if (TotalUsageSeconds == 0 && TotalUsageSeconds > 0)
                    {
                        TotalUsageSeconds = TotalUsageSeconds * 60;
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 迁移总使用时长: {TotalUsageSeconds}分钟 -> {TotalUsageSeconds}秒");
                    }

                    if (AverageSessionSeconds == 0 && AverageSessionSeconds > 0)
                    {
                        AverageSessionSeconds = AverageSessionSeconds * 60;
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 迁移平均会话时长: {AverageSessionSeconds}分钟 -> {AverageSessionSeconds}秒");
                    }

                    if (WeeklyUsageSeconds == 0 && WeeklyUsageSeconds > 0)
                    {
                        WeeklyUsageSeconds = WeeklyUsageSeconds * 60;
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 迁移每周使用时长: {WeeklyUsageSeconds}分钟 -> {WeeklyUsageSeconds}秒");
                    }

                    if (LastWeekUsageSeconds == 0 && LastWeekUsageSeconds > 0)
                    {
                        LastWeekUsageSeconds = LastWeekUsageSeconds * 60;
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 迁移上周使用时长: {LastWeekUsageSeconds}分钟 -> {LastWeekUsageSeconds}秒");
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 数据迁移失败: {ex.Message}", LogHelper.LogType.Error);
                }
            }

            /// <summary>
            /// 检查并重置每周统计数据（秒级精度）
            /// </summary>
            public void CheckAndResetWeeklyStats()
            {
                var now = DateTime.Now;
                var currentWeekStart = GetWeekStartDate(now);

                // 如果是新的一周，重置统计
                if (WeekStartDate == DateTime.MinValue || currentWeekStart > WeekStartDate)
                {
                    // 保存上周数据
                    LastWeekLaunchCount = WeeklyLaunchCount;
                    LastWeekUsageSeconds = WeeklyUsageSeconds;

                    // 同时更新旧字段以保持兼容性
                    LastWeekUsageSeconds = LastWeekUsageSeconds / 60;

                    // 重置本周数据
                    WeeklyLaunchCount = 0;
                    WeeklyUsageSeconds = 0;
                    WeeklyUsageSeconds = 0;
                    WeekStartDate = currentWeekStart;

                    LogHelper.WriteLogToFile($"DeviceIdentifier | 每周统计重置 - 上周启动: {LastWeekLaunchCount}次, 上周使用: {FormatDuration(LastWeekUsageSeconds)}");
                }
            }

            /// <summary>
            /// 获取指定日期所在周的开始日期（周一）
            /// </summary>
            public DateTime GetWeekStartDate(DateTime date)
            {
                var dayOfWeek = (int)date.DayOfWeek;
                var daysToSubtract = dayOfWeek == 0 ? 6 : dayOfWeek - 1; // 周日=0，需要减6天到周一
                return date.Date.AddDays(-daysToSubtract);
            }

            /// <summary>
            /// 记录本周的启动
            /// </summary>
            public void RecordWeeklyLaunch()
            {
                CheckAndResetWeeklyStats();
                WeeklyLaunchCount++;
            }

            /// <summary>
            /// 记录本周的使用时长（秒级精度）
            /// </summary>
            public void RecordWeeklyUsage(long seconds)
            {
                CheckAndResetWeeklyStats();
                WeeklyUsageSeconds += seconds;
                // 同时更新旧字段以保持兼容性
                WeeklyUsageSeconds = WeeklyUsageSeconds / 60;
            }
        }

        /// <summary>
        /// 格式化时长显示（秒级精度）
        /// </summary>
        /// <param name="totalSeconds">总秒数</param>
        /// <returns>格式化的时长字符串</returns>
        public static string FormatDuration(long totalSeconds)
        {
            if (totalSeconds < 60)
            {
                return $"{totalSeconds}秒";
            }

            if (totalSeconds < 3600)
            {
                var minutes = totalSeconds / 60;
                var seconds = totalSeconds % 60;
                return seconds > 0 ? $"{minutes}分{seconds}秒" : $"{minutes}分钟";
            }
            else
            {
                var hours = totalSeconds / 3600;
                var minutes = (totalSeconds % 3600) / 60;
                var seconds = totalSeconds % 60;

                var result = $"{hours}小时";
                if (minutes > 0) result += $"{minutes}分";
                if (seconds > 0) result += $"{seconds}秒";

                return result;
            }
        }

        /// <summary>
        /// 更新推送优先级枚举
        /// </summary>
        public enum UpdatePriority
        {
            High = 1,    // 高优先级：立即推送更新
            Medium = 2,  // 中优先级：延迟1-3天推送
            Low = 3      // 低优先级：延迟3-14天推送
        }

        /// <summary>
        /// 用户使用频率分类枚举
        /// </summary>
        public enum UsageFrequency
        {
            High = 1,    // 高频用户：综合评分≥80分（活跃度高、使用时长长、启动频繁）
            Medium = 2,  // 中频用户：综合评分40-79分（中等活跃度和使用强度）
            Low = 3      // 低频用户：综合评分<40分（活跃度低、使用时长短、启动较少）
        }

        /// <summary>
        /// 记录应用启动
        /// </summary>
        public static void RecordAppLaunch()
        {
            try
            {
                lock (fileLock)
                {
                    var stats = LoadUsageStats();
                    stats.LastLaunchTime = DateTime.Now;
                    stats.LaunchCount++;
                    stats.DeviceId = DeviceId;

                    // 记录每周启动次数
                    stats.RecordWeeklyLaunch();

                    // 计算使用频率
                    CalculateUsageFrequency(stats);

                    SaveUsageStats(stats);

                    LogHelper.WriteLogToFile($"DeviceIdentifier | 记录应用启动 - 设备ID: {DeviceId}, 总启动: {stats.LaunchCount}次, 本周启动: {stats.WeeklyLaunchCount}次");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 记录应用启动失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 记录应用退出（计算使用时长 - 秒级精度）
        /// </summary>
        public static void RecordAppExit()
        {
            try
            {
                lock (fileLock)
                {
                    var stats = LoadUsageStats();

                    // 执行数据迁移（如果需要）
                    stats.MigrateToSecondsPrecision();

                    // 计算本次会话时长（秒级精度）
                    long sessionSeconds = 0;
                    if (stats.LastLaunchTime != DateTime.MinValue)
                    {
                        var sessionDuration = DateTime.Now - stats.LastLaunchTime;
                        sessionSeconds = (long)sessionDuration.TotalSeconds;

                        // 更新秒级精度数据
                        stats.TotalUsageSeconds += sessionSeconds;

                        // 同时更新旧字段以保持兼容性
                        stats.TotalUsageSeconds = stats.TotalUsageSeconds / 60;

                        // 记录每周使用时长（秒级精度）
                        stats.RecordWeeklyUsage(sessionSeconds);

                        // 更新平均会话时长（秒级精度）
                        if (stats.LaunchCount > 0)
                        {
                            stats.AverageSessionSeconds = (double)stats.TotalUsageSeconds / stats.LaunchCount;
                            // 同时更新旧字段以保持兼容性
                            stats.AverageSessionSeconds = stats.AverageSessionSeconds / 60;
                        }
                    }

                    // 重新计算使用频率
                    CalculateUsageFrequency(stats);

                    SaveUsageStats(stats);

                    LogHelper.WriteLogToFile($"DeviceIdentifier | 记录应用退出 - 本次会话: {FormatDuration(sessionSeconds)}, " +
                                           $"总时长: {FormatDuration(stats.TotalUsageSeconds)}, " +
                                           $"本周时长: {FormatDuration(stats.WeeklyUsageSeconds)}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 记录应用退出失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 计算使用频率和更新优先级（基于真实的每周统计数据）
        /// 通过多维度评分系统确定用户类型：高频(≥80分)、中频(40-79分)、低频(<40分)
        /// </summary>
        private static void CalculateUsageFrequency(UsageStats stats)
        {
            try
            {
                // 确保每周统计数据是最新的
                stats.CheckAndResetWeeklyStats();

                // 计算最近活跃度
                var daysSinceLastUse = (DateTime.Now - stats.LastLaunchTime).TotalDays;

                // 使用真实的每周数据（秒级精度）
                var currentWeekLaunches = stats.WeeklyLaunchCount;
                var currentWeekSeconds = stats.WeeklyUsageSeconds;

                // 如果秒级数据为空但分钟数据存在，进行转换
                if (currentWeekSeconds == 0 && stats.WeeklyUsageSeconds > 0)
                {
                    currentWeekSeconds = stats.WeeklyUsageSeconds * 60;
                }

                // 如果本周数据不足，参考上周数据
                var weeklyLaunches = currentWeekLaunches > 0 ? currentWeekLaunches : stats.LastWeekLaunchCount;
                var weeklySeconds = currentWeekSeconds > 0 ? currentWeekSeconds : stats.LastWeekUsageSeconds;

                // 如果秒级数据仍为空，使用分钟数据转换
                if (weeklySeconds == 0 && stats.LastWeekUsageSeconds > 0)
                {
                    weeklySeconds = stats.LastWeekUsageSeconds * 60;
                }

                // 综合评分系统（0-100分）
                var frequencyScore = CalculateFrequencyScoreWithWeeklyData(stats, daysSinceLastUse, weeklyLaunches, weeklySeconds);

                // 根据综合评分确定频率分类和更新优先级
                if (frequencyScore >= 80)
                {
                    stats.UsageFrequency = UsageFrequency.High;      // 高频用户：立即推送更新
                    stats.UpdatePriority = UpdatePriority.High;
                }
                else if (frequencyScore >= 40)
                {
                    stats.UsageFrequency = UsageFrequency.Medium;    // 中频用户：延迟1-3天推送
                    stats.UpdatePriority = UpdatePriority.Medium;
                }
                else
                {
                    stats.UsageFrequency = UsageFrequency.Low;       // 低频用户：延迟3-14天推送
                    stats.UpdatePriority = UpdatePriority.Low;
                }

                LogHelper.WriteLogToFile($"DeviceIdentifier | 使用频率计算 - 评分: {frequencyScore}, 频率: {stats.UsageFrequency}, " +
                                       $"优先级: {stats.UpdatePriority}, 本周启动: {currentWeekLaunches}次, 本周时长: {FormatDuration(currentWeekSeconds)}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 计算使用频率失败: {ex.Message}", LogHelper.LogType.Error);
                // 默认设置为中等频率和优先级
                stats.UsageFrequency = UsageFrequency.Medium;
                stats.UpdatePriority = UpdatePriority.Medium;
            }
        }

        /// <summary>
        /// 基于每周真实数据计算综合频率评分（0-100分，秒级精度）
        /// 评分标准：≥80分=高频用户，40-79分=中频用户，<40分=低频用户
        /// </summary>
        /// <param name="stats">使用统计数据</param>
        /// <param name="daysSinceLastUse">距离最后使用的天数</param>
        /// <param name="weeklyLaunches">每周启动次数</param>
        /// <param name="weeklySeconds">每周使用时长（秒）</param>
        /// <returns>综合评分（0-100分）</returns>
        private static int CalculateFrequencyScoreWithWeeklyData(UsageStats stats, double daysSinceLastUse,
            long weeklyLaunches, long weeklySeconds)
        {
            var score = 0;

            // 最近活跃度评分（40分）- 反映用户当前的活跃程度
            if (daysSinceLastUse <= 1) score += 40;        // 1天内使用：非常活跃
            else if (daysSinceLastUse <= 3) score += 35;   // 3天内使用：很活跃
            else if (daysSinceLastUse <= 7) score += 25;   // 1周内使用：较活跃
            else if (daysSinceLastUse <= 14) score += 15;  // 2周内使用：一般活跃
            else if (daysSinceLastUse <= 30) score += 5;   // 1月内使用：不太活跃

            // 每周使用频率评分（30分）- 基于真实的每周启动次数
            if (weeklyLaunches >= 10) score += 30;         // 10次以上：高频使用
            else if (weeklyLaunches >= 5) score += 20;     // 5-9次：中高频使用
            else if (weeklyLaunches >= 3) score += 15;     // 3-4次：中频使用
            else if (weeklyLaunches >= 1) score += 10;     // 1-2次：低频使用

            // 每周使用时长评分（20分）- 基于真实的每周使用时长（秒级精度）
            if (weeklySeconds >= 36000) score += 20;         // 10小时以上：重度使用
            else if (weeklySeconds >= 18000) score += 15;    // 5-10小时：中重度使用
            else if (weeklySeconds >= 7200) score += 10;     // 2-5小时：中度使用
            else if (weeklySeconds >= 3600) score += 5;      // 1-2小时：轻度使用

            // 历史使用深度评分（10分）- 反映用户的长期使用习惯（秒级精度）
            var totalSeconds = stats.TotalUsageSeconds > 0 ? stats.TotalUsageSeconds : stats.TotalUsageSeconds * 60;
            if (totalSeconds >= 180000) score += 10;    // 50小时以上：资深用户
            else if (totalSeconds >= 72000) score += 7; // 20-50小时：中等用户
            else if (totalSeconds >= 18000) score += 4;  // 5-20小时：新手用户

            return Math.Min(100, score);
        }



        /// <summary>
        /// 获取当前更新优先级
        /// </summary>
        public static UpdatePriority GetUpdatePriority()
        {
            try
            {
                var stats = LoadUsageStats();
                return stats.UpdatePriority;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 获取更新优先级失败: {ex.Message}", LogHelper.LogType.Error);
                return UpdatePriority.Medium; // 默认中等优先级
            }
        }

        /// <summary>
        /// 获取使用频率
        /// </summary>
        public static UsageFrequency GetUsageFrequency()
        {
            try
            {
                var stats = LoadUsageStats();
                return stats.UsageFrequency;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 获取使用频率失败: {ex.Message}", LogHelper.LogType.Error);
                return UsageFrequency.Medium; // 默认中等频率
            }
        }

        /// <summary>
        /// 获取使用统计信息（秒级精度）
        /// </summary>
        public static (int launchCount, long totalSeconds, double avgSessionSeconds, UpdatePriority priority) GetUsageStats()
        {
            try
            {
                var stats = LoadUsageStats();
                return (stats.LaunchCount, stats.TotalUsageSeconds, stats.AverageSessionSeconds, stats.UpdatePriority);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 获取使用统计失败: {ex.Message}", LogHelper.LogType.Error);
                return (0, 0, 0, UpdatePriority.Medium);
            }
        }

        /// <summary>
        /// 获取使用统计信息（兼容性方法 - 分钟精度）
        /// </summary>
        [Obsolete("请使用 GetUsageStats() 获取秒级精度数据")]
        public static (int launchCount, long totalMinutes, double avgSessionMinutes, UpdatePriority priority) GetUsageStatsInMinutes()
        {
            try
            {
                var stats = LoadUsageStats();
                var totalMinutes = stats.TotalUsageSeconds / 60;
                var avgMinutes = stats.AverageSessionSeconds / 60;
                return (stats.LaunchCount, totalMinutes, avgMinutes, stats.UpdatePriority);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 获取使用统计失败: {ex.Message}", LogHelper.LogType.Error);
                return (0, 0, 0, UpdatePriority.Medium);
            }
        }

        /// <summary>
        /// 加载使用统计 
        /// </summary>
        private static UsageStats LoadUsageStats()
        {
            try
            {
                // 1. 尝试从主文件加载
                var stats = LoadUsageStatsFromFile(UsageStatsFilePath);
                if (stats != null)
                {
                    // 执行数据迁移（如果需要）
                    stats.MigrateToSecondsPrecision();
                    return stats;
                }

                // 2. 尝试从备份文件加载
                stats = LoadUsageStatsFromFile(UsageStatsBackupPath);
                if (stats != null)
                {
                    LogHelper.WriteLogToFile("DeviceIdentifier | 从备份文件恢复使用统计");
                    stats.MigrateToSecondsPrecision();
                    // 尝试恢复主文件
                    try
                    {
                        SaveUsageStatsToFile(UsageStatsFilePath, stats);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 恢复主文件失败: {ex.Message}", LogHelper.LogType.Warning);
                    }
                    return stats;
                }



                // 如果所有文件都不存在或损坏，返回新的统计对象
            var newStats = new UsageStats
            {
                DeviceId = DeviceId,
                LastLaunchTime = DateTime.Now,
                LaunchCount = 0,
                TotalUsageSeconds = 0, // 保持兼容性
                AverageSessionSeconds = 0, // 保持兼容性
                LastUpdateCheck = DateTime.MinValue,
                UpdatePriority = UpdatePriority.Medium,
                UsageFrequency = UsageFrequency.Medium
            };

                // 保存新统计到文件
                SaveUsageStatsToFile(UsageStatsFilePath, newStats);
            return newStats;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 加载使用统计失败: {ex.Message}", LogHelper.LogType.Error);
                
                // 返回默认统计对象
                return new UsageStats
                {
                    DeviceId = DeviceId,
                    LastLaunchTime = DateTime.Now,
                    LaunchCount = 0,
                    TotalUsageSeconds = 0,
                    AverageSessionSeconds = 0,
                    LastUpdateCheck = DateTime.MinValue,
                    UpdatePriority = UpdatePriority.Medium,
                    UsageFrequency = UsageFrequency.Medium
                };
            }
        }

        /// <summary>
        /// 保存使用统计
        /// </summary>
        private static void SaveUsageStats(UsageStats stats)
        {
            // 保存到主文件
            SaveUsageStatsToFile(UsageStatsFilePath, stats);
            
            // 保存到备份文件
            SaveUsageStatsToFile(UsageStatsBackupPath, stats);
        }



        /// <summary>
        /// 从文件加载使用统计（解密）
        /// </summary>
        private static UsageStats LoadUsageStatsFromFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    // 尝试解密文件
                    var stats = LoadUsageStatsFromEncryptedFile(filePath);
                    if (stats != null)
                    {
                        return stats;
                    }
                    
                    // 如果解密失败，尝试作为普通JSON文件读取（向后兼容）
                    try
                    {
                        string json = File.ReadAllText(filePath);
                        var plainStats = JsonConvert.DeserializeObject<UsageStats>(json);
                        if (plainStats != null && !string.IsNullOrEmpty(plainStats.DeviceId))
                        {
                            LogHelper.WriteLogToFile($"DeviceIdentifier | 从普通JSON文件加载使用统计: {filePath}");
                            return plainStats;
                        }
                    }
                    catch
                    {
                        // 忽略JSON解析错误
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 从文件加载使用统计失败 ({filePath}): {ex.Message}", LogHelper.LogType.Error);
            }
            return null;
        }

        /// <summary>
        /// 从加密文件加载使用统计
        /// </summary>
        private static UsageStats LoadUsageStatsFromEncryptedFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    byte[] encryptedData = File.ReadAllBytes(filePath);
                    
                    if (encryptedData.Length < 32) // SHA256校验和长度为32字节
                    {
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 加密文件格式错误: {filePath}", LogHelper.LogType.Error);
                        return null;
                    }
                    
                    // 提取校验和和加密数据
                    byte[] checksum = new byte[32];
                    byte[] data = new byte[encryptedData.Length - 32];
                    Array.Copy(encryptedData, 0, checksum, 0, 32);
                    Array.Copy(encryptedData, 32, data, 0, data.Length);
                    
                    // 使用SHA256生成解密密钥
                    using (var sha256 = SHA256.Create())
                    {
                        byte[] keyBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(DeviceId + "ICC_Usage_Stats_Salt"));
                        
                        // XOR解密
                        byte[] decryptedData = new byte[data.Length];
                        for (int i = 0; i < data.Length; i++)
                        {
                            decryptedData[i] = (byte)(data[i] ^ keyBytes[i % keyBytes.Length]);
                        }
                        
                        // 验证校验和
                        byte[] computedChecksum = sha256.ComputeHash(decryptedData);
                        if (!checksum.SequenceEqual(computedChecksum))
                        {
                            LogHelper.WriteLogToFile($"DeviceIdentifier | 加密文件校验和验证失败: {filePath}", LogHelper.LogType.Error);
                            return null;
                        }
                        
                        string json = Encoding.UTF8.GetString(decryptedData);
                        var stats = JsonConvert.DeserializeObject<UsageStats>(json);
                        if (stats != null && !string.IsNullOrEmpty(stats.DeviceId))
                        {
                            LogHelper.WriteLogToFile($"DeviceIdentifier | 从加密文件成功加载使用统计: {filePath}");
                            return stats;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 从加密文件加载使用统计失败 ({filePath}): {ex.Message}", LogHelper.LogType.Error);
            }
            return null;
        }

                /// <summary>
        /// 保存使用统计到文件（加密）
        /// </summary>
        private static void SaveUsageStatsToFile(string filePath, UsageStats stats)
        {
            try
            {
                // 确保目录存在
                var directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(stats, Formatting.Indented);
                byte[] data = Encoding.UTF8.GetBytes(json);
                
                // 使用SHA256生成加密密钥（基于设备ID）
                using (var sha256 = SHA256.Create())
                {
                    byte[] keyBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(DeviceId + "ICC_Usage_Stats_Salt"));
                    
                    // 简单的XOR加密
                    byte[] encryptedData = new byte[data.Length];
                    for (int i = 0; i < data.Length; i++)
                    {
                        encryptedData[i] = (byte)(data[i] ^ keyBytes[i % keyBytes.Length]);
                    }
                    
                    // 添加SHA256校验和
                    byte[] checksum = sha256.ComputeHash(data);
                    byte[] finalData = new byte[checksum.Length + encryptedData.Length];
                    checksum.CopyTo(finalData, 0);
                    encryptedData.CopyTo(finalData, checksum.Length);
                    
                    File.WriteAllBytes(filePath, finalData);
                    
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 加密使用统计已保存到: {filePath}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 保存使用统计到文件失败 ({filePath}): {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 记录更新检查时间
        /// </summary>
        public static void RecordUpdateCheck()
        {
            try
            {
                lock (fileLock)
                {
                    var stats = LoadUsageStats();
                    stats.LastUpdateCheck = DateTime.Now;
                    SaveUsageStats(stats);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 记录更新检查失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 验证使用统计数据的完整性
        /// </summary>
        public static bool ValidateUsageStatsIntegrity()
        {
            try
            {
                // 检查主文件
                if (File.Exists(UsageStatsFilePath))
                {
                    var mainStats = LoadUsageStatsFromFile(UsageStatsFilePath);
                    if (mainStats != null && mainStats.DeviceId == DeviceId)
                    {
                        LogHelper.WriteLogToFile("DeviceIdentifier | 主文件数据完整性验证通过");
                        return true;
                    }
                }

                // 检查备份文件
                if (File.Exists(UsageStatsBackupPath))
                {
                    var backupStats = LoadUsageStatsFromFile(UsageStatsBackupPath);
                    if (backupStats != null && backupStats.DeviceId == DeviceId)
                    {
                        LogHelper.WriteLogToFile("DeviceIdentifier | 备份文件数据完整性验证通过");
                        return true;
                    }
                }

                LogHelper.WriteLogToFile("DeviceIdentifier | 数据完整性验证失败", LogHelper.LogType.Warning);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 数据完整性验证失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 从备份文件恢复使用统计数据
        /// </summary>
        public static bool RestoreUsageStatsFromBackup()
        {
            try
            {
                if (File.Exists(UsageStatsBackupPath))
                {
                    var backupStats = LoadUsageStatsFromFile(UsageStatsBackupPath);
                    if (backupStats != null && backupStats.DeviceId == DeviceId)
                    {
                        // 保存到主文件
                        SaveUsageStatsToFile(UsageStatsFilePath, backupStats);
                        LogHelper.WriteLogToFile("DeviceIdentifier | 从备份文件成功恢复使用统计数据");
                        return true;
                    }
                }

                LogHelper.WriteLogToFile("DeviceIdentifier | 备份文件不存在或损坏，无法恢复", LogHelper.LogType.Warning);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 从备份文件恢复失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 获取使用统计文件状态信息
        /// </summary>
        public static string GetUsageStatsFileStatus()
        {
            try
            {
                var status = new List<string>();
                
                // 检查主文件
                if (File.Exists(UsageStatsFilePath))
                {
                    var fileInfo = new FileInfo(UsageStatsFilePath);
                    var mainStats = LoadUsageStatsFromFile(UsageStatsFilePath);
                    if (mainStats != null && mainStats.DeviceId == DeviceId)
                    {
                        status.Add($"主文件: 正常 ({fileInfo.Length} 字节, 修改时间: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss})");
                    }
                    else
                    {
                        status.Add($"主文件: 损坏 ({fileInfo.Length} 字节)");
                    }
                }
                else
                {
                    status.Add("主文件: 不存在");
                }

                // 检查备份文件
                if (File.Exists(UsageStatsBackupPath))
                {
                    var fileInfo = new FileInfo(UsageStatsBackupPath);
                    var backupStats = LoadUsageStatsFromFile(UsageStatsBackupPath);
                    if (backupStats != null && backupStats.DeviceId == DeviceId)
                    {
                        status.Add($"备份文件: 正常 ({fileInfo.Length} 字节, 修改时间: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss})");
                    }
                    else
                    {
                        status.Add($"备份文件: 损坏 ({fileInfo.Length} 字节)");
                    }
                }
                else
                {
                    status.Add("备份文件: 不存在");
                }

                return string.Join("\n", status);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 获取文件状态失败: {ex.Message}", LogHelper.LogType.Error);
                return "获取文件状态失败";
            }
        }


        /// <summary>
        /// 根据优先级决定是否应该推送更新（仅适用于自动更新，版本修复功能不受影响）
        /// </summary>
        /// <param name="updateVersion">更新版本号</param>
        /// <param name="releaseTime">新版本发布时间</param>
        /// <param name="isAutoUpdate">是否为自动更新检查（默认true，false表示版本修复）</param>
        /// <param name="currentVersionReleaseTime">当前版本发布时间</param>
        /// <returns>是否应该推送更新</returns>
        public static bool ShouldPushUpdate(string updateVersion, DateTime releaseTime, bool isAutoUpdate = true, DateTime? currentVersionReleaseTime = null)
        {
            try
            {
                // 判断更新类型（基于版本号）
                var updateType = DetermineUpdateType(updateVersion);

                // 如果不是自动更新（即版本修复），则应用不同的策略
                if (!isAutoUpdate)
                {
                    // 版本修复：立即允许，不受分级策略影响
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 版本修复 - 版本: {updateVersion}, 类型: {updateType}, 结果: 允许");
                    return true;
                }

                var priority = GetUpdatePriority();
                var frequency = GetUsageFrequency();
                var stats = LoadUsageStats();

                // 计算版本间的时间差
                double daysBetweenVersions;
                if (currentVersionReleaseTime.HasValue)
                {
                    // 使用当前版本发布时间与新版本发布时间的差异
                    daysBetweenVersions = (releaseTime - currentVersionReleaseTime.Value).TotalDays;
                }
                else
                {
                    // 如果没有当前版本发布时间，回退到使用新版本发布时间到现在的天数
                    daysBetweenVersions = (DateTime.Now - releaseTime).TotalDays;
                }

                // 计算最近活跃度（最后一次使用距今的天数）
                var daysSinceLastUse = (DateTime.Now - stats.LastLaunchTime).TotalDays;

                // 综合判断逻辑（仅适用于自动更新）
                var shouldPush = ShouldPushUpdateComprehensive(priority, frequency, daysBetweenVersions, daysSinceLastUse, stats, updateType);

                LogHelper.WriteLogToFile($"DeviceIdentifier | 自动更新推送判断 - 版本: {updateVersion}, 类型: {updateType}, " +
                                       $"优先级: {priority}, 频率: {frequency}, 版本间隔: {daysBetweenVersions:F1}天, " +
                                       $"最后使用: {daysSinceLastUse:F1}天前, 结果: {shouldPush}");

                return shouldPush;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 判断是否推送更新失败: {ex.Message}", LogHelper.LogType.Error);
                return true; // 出错时默认推送
            }
        }

        /// <summary>
        /// 更新类型枚举
        /// </summary>
        private enum UpdateType
        {
            Major,      // 主版本更新 (x.0.0)
            Minor,      // 次版本更新 (x.y.0)
            Patch,      // 补丁更新 (x.y.z)
            Hotfix,     // 热修复更新
            Unknown     // 未知类型
        }

        /// <summary>
        /// 根据版本号判断更新类型
        /// </summary>
        private static UpdateType DetermineUpdateType(string version)
        {
            if (string.IsNullOrEmpty(version)) return UpdateType.Unknown;

            try
            {
                // 移除可能的前缀（如 "v"）
                var cleanVersion = version.TrimStart('v', 'V');

                // 检查是否包含热修复标识
                if (cleanVersion.ToLower().Contains("hotfix") || cleanVersion.ToLower().Contains("fix"))
                {
                    return UpdateType.Hotfix;
                }

                // 解析版本号
                var parts = cleanVersion.Split('.');
                if (parts.Length >= 3)
                {
                    if (int.TryParse(parts[1], out int minor) && int.TryParse(parts[2], out int patch))
                    {
                        if (minor == 0 && patch == 0) return UpdateType.Major;
                        if (patch == 0) return UpdateType.Minor;
                        return UpdateType.Patch;
                    }
                }

                return UpdateType.Unknown;
            }
            catch
            {
                return UpdateType.Unknown;
            }
        }

        /// <summary>
        /// 综合时间和使用频率的自动更新推送判断逻辑（不影响版本修复）
        /// </summary>
        /// <param name="priority">用户更新优先级</param>
        /// <param name="frequency">用户使用频率</param>
        /// <param name="daysBetweenVersions">当前版本与新版本之间的天数差异</param>
        /// <param name="daysSinceLastUse">距离最后使用的天数</param>
        /// <param name="stats">使用统计数据</param>
        /// <param name="updateType">更新类型</param>
        /// <returns>是否应该推送更新</returns>
        private static bool ShouldPushUpdateComprehensive(UpdatePriority priority, UsageFrequency frequency,
            double daysBetweenVersions, double daysSinceLastUse, UsageStats stats, UpdateType updateType)
        {
            // 考虑用户的总体使用模式
            var isHeavyUser = stats.TotalUsageSeconds > 3000; // 超过50小时的重度用户
            var isFrequentUser = stats.LaunchCount > 100; // 启动超过100次的频繁用户

            // 根据更新类型调整推送策略
            var urgencyMultiplier = GetUpdateUrgencyMultiplier(updateType);

            // 如果用户长时间未使用（超过30天），降低推送优先级
            if (daysSinceLastUse > 30)
            {
                // 热修复和重要更新优先推送
                if (updateType == UpdateType.Hotfix)
                {
                    return daysBetweenVersions >= 1; // 热修复版本间隔1天后推送
                }

                // 但如果是重度用户，仍然要适当推送
                var baseDelay = isHeavyUser ? 7 : 14;
                return daysBetweenVersions >= (baseDelay / urgencyMultiplier);
            }

            // 如果用户最近很活跃（3天内使用过）
            if (daysSinceLastUse <= 3)
            {
                // 热修复立即推送给活跃用户
                if (updateType == UpdateType.Hotfix)
                {
                    return true;
                }

                // 结合使用频率和优先级判断
                if (frequency == UsageFrequency.High || isHeavyUser)
                {
                    return daysBetweenVersions >= Math.Max(0, 1 / urgencyMultiplier); // 高频用户优先推送
                }

                switch (priority)
                {
                    case UpdatePriority.High:
                        return daysBetweenVersions >= Math.Max(0, 1 / urgencyMultiplier);

                    case UpdatePriority.Medium:
                        return daysBetweenVersions >= Math.Max(1, 2 / urgencyMultiplier);

                    case UpdatePriority.Low:
                        return daysBetweenVersions >= Math.Max(2, 3 / urgencyMultiplier);
                }
            }

            // 中等活跃度用户（3-14天内使用过）
            if (daysSinceLastUse <= 14)
            {
                // 热修复优先推送
                if (updateType == UpdateType.Hotfix)
                {
                    return daysBetweenVersions >= 1;
                }

                // 频繁用户优先推送
                if (isFrequentUser && frequency == UsageFrequency.High)
                {
                    return daysBetweenVersions >= Math.Max(1, 2 / urgencyMultiplier);
                }

                switch (priority)
                {
                    case UpdatePriority.High:
                        return daysBetweenVersions >= Math.Max(1, 2 / urgencyMultiplier);

                    case UpdatePriority.Medium:
                        return daysBetweenVersions >= Math.Max(2, 4 / urgencyMultiplier);

                    case UpdatePriority.Low:
                        return daysBetweenVersions >= Math.Max(4, 7 / urgencyMultiplier);
                }
            }

            // 较不活跃用户（14-30天内使用过）
            // 对于低频率用户，进一步延迟推送
            var delayMultiplier = frequency == UsageFrequency.Low ? 2 : 1;

            switch (priority)
            {
                case UpdatePriority.High:
                    return daysBetweenVersions >= Math.Max(2, 3 * delayMultiplier / urgencyMultiplier);

                case UpdatePriority.Medium:
                    return daysBetweenVersions >= Math.Max(4, 7 * delayMultiplier / urgencyMultiplier);

                case UpdatePriority.Low:
                    return daysBetweenVersions >= Math.Max(7, 14 * delayMultiplier / urgencyMultiplier);

                default:
                    return daysBetweenVersions >= 7;
            }
        }

        /// <summary>
        /// 根据更新类型获取紧急程度倍数（仅用于自动更新分级）
        /// </summary>
        private static double GetUpdateUrgencyMultiplier(UpdateType updateType)
        {
            switch (updateType)
            {
                case UpdateType.Hotfix:
                    return 3.0;   // 热修复最紧急，3倍速度推送
                case UpdateType.Major:
                    return 0.5;   // 主版本更新较慢推送
                case UpdateType.Minor:
                    return 1.0;   // 次版本正常推送
                case UpdateType.Patch:
                    return 1.5;   // 补丁更新稍快推送
                case UpdateType.Unknown:
                    return 1.0;   // 未知类型正常推送
                default:
                    return 1.0;
            }
        }

        /// <summary>
        /// 检查是否应该进行版本修复（不受分级策略影响）
        /// </summary>
        /// <param name="currentVersion">当前版本</param>
        /// <param name="availableVersion">可用版本</param>
        /// <returns>是否需要版本修复</returns>
        public static bool ShouldPerformVersionFix(string currentVersion, string availableVersion)
        {
            try
            {
                // 版本修复功能不受使用频率分级策略影响，始终允许
                LogHelper.WriteLogToFile($"DeviceIdentifier | 版本修复检查 - 当前版本: {currentVersion}, 可用版本: {availableVersion}, 结果: 允许");
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 版本修复检查失败: {ex.Message}", LogHelper.LogType.Error);
                return true; // 出错时默认允许
            }
        }


        /// <summary>
        /// 获取设备信息摘要（用于调试）
        /// </summary>
        public static string GetDeviceInfoSummary()
        {
            try
            {
                var (launchCount, totalSeconds, avgSessionSeconds, priority) = GetUsageStats();
                var frequency = GetUsageFrequency();
                var stats = LoadUsageStats();
                var daysSinceLastUse = (DateTime.Now - stats.LastLaunchTime).TotalDays;

                return $"设备ID: {DeviceId}\n" +
                       $"启动次数: {launchCount}\n" +
                       $"总使用时长: {FormatDuration(totalSeconds)}\n" +
                       $"平均会话时长: {FormatDuration((long)avgSessionSeconds)}\n" +
                       $"使用频率: {frequency}\n" +
                       $"更新优先级: {priority}\n" +
                       $"最后使用: {daysSinceLastUse:F1}天前\n" +
                       $"用户类型: {GetUserTypeDescription(stats)}\n\n" +
                       $"文件状态:\n{GetUsageStatsFileStatus()}";
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 获取设备信息摘要失败: {ex.Message}", LogHelper.LogType.Error);
                return $"设备ID: {DeviceId}\n获取详细信息失败";
            }
        }

        /// <summary>
        /// 获取用户类型描述
        /// </summary>
        private static string GetUserTypeDescription(UsageStats stats)
        {
            var isHeavyUser = stats.TotalUsageSeconds > 3000;
            var isFrequentUser = stats.LaunchCount > 100;
            var daysSinceLastUse = (DateTime.Now - stats.LastLaunchTime).TotalDays;

            var descriptions = new List<string>();

            if (isHeavyUser) descriptions.Add("重度用户");
            if (isFrequentUser) descriptions.Add("频繁用户");

            if (daysSinceLastUse <= 3) descriptions.Add("高活跃");
            else if (daysSinceLastUse <= 14) descriptions.Add("中活跃");
            else if (daysSinceLastUse <= 30) descriptions.Add("低活跃");
            else descriptions.Add("非活跃");

            return descriptions.Count > 0 ? string.Join(", ", descriptions) : "普通用户";
        }



        /// <summary>
        /// 关机时保存使用时间数据
        /// </summary>
        public static void SaveUsageStatsOnShutdown()
        {
            try
            {
                LogHelper.WriteLogToFile("DeviceIdentifier | 开始关机时保存使用时间数据");

                // 1. 加载现有使用统计数据
                var stats = LoadUsageStats();
                if (stats == null)
                {
                    stats = new UsageStats { DeviceId = DeviceId };
                    LogHelper.WriteLogToFile("DeviceIdentifier | 创建新的使用统计数据");
                }

                // 2. 计算本次会话时长（防止异常值）
                TimeSpan sessionDuration = DateTime.Now - App.appStartTime;
                long sessionSeconds = Math.Max(0, (long)sessionDuration.TotalSeconds);
                
                // 防止异常大的会话时长（超过24小时）
                if (sessionSeconds > 86400)
                {
                    sessionSeconds = 86400;
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 会话时长异常，已限制为24小时: {sessionSeconds}秒", LogHelper.LogType.Warning);
                }

                // 3. 更新统计数据
                stats.TotalUsageSeconds += sessionSeconds;
                stats.LaunchCount++;
                stats.AverageSessionSeconds = stats.TotalUsageSeconds / (double)Math.Max(1, stats.LaunchCount);
                stats.LastLaunchTime = DateTime.Now;
                
                // 4. 保存数据
                SaveUsageStats(stats);
                
                LogHelper.WriteLogToFile("DeviceIdentifier | 关机保存完成");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 关机时保存使用时间数据失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }
    }
}

