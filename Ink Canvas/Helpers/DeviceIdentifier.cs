using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Microsoft.Win32;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 设备标识符和使用频率监控类
    /// </summary>
    internal static class DeviceIdentifier
    {
        // 多重备份路径策略
        private static readonly string DeviceIdFilePath = Path.Combine(App.RootPath, "device_id.dat");
        private static readonly string UsageStatsFilePath = Path.Combine(App.RootPath, "usage_stats.json");

        // 使用频率数据的多重隐藏备份路径
        private static readonly string BackupDeviceIdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ICC", ".sys", "device.dat");
        private static readonly string BackupUsageStatsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ICC", ".sys", "usage.dat");

        // 使用频率数据的额外隐藏备份位置
        private static readonly string SecondaryUsageBackupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", ".icc", "usage_backup.tmp");
        private static readonly string TertiaryUsageBackupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ICC", ".cache", "usage_cache.dat");
        private static readonly string QuaternaryUsageBackupPath = Path.Combine(Path.GetTempPath(),
            ".icc_temp", "usage_temp.dat");

        // 数据完整性验证密钥
        private static readonly string DataIntegrityKey = "ICC_DEVICE_INTEGRITY_2024";

        private static readonly string DeviceId = null;
        private static readonly object fileLock = new object();

        static DeviceIdentifier()
        {
            // 在静态构造函数中初始化设备ID
            DeviceId = GetOrCreateDeviceId();

            // 执行数据完整性检查和自动修复
            try
            {
                PerformDataIntegrityCheck();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 初始化时数据完整性检查失败: {ex.Message}", LogHelper.LogType.Error);
            }
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
        /// 获取或创建设备ID（内部方法）- 支持多重备份恢复
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
                        // 确保备份同步
                        SaveDeviceIdToAllLocations(deviceId);
                        return deviceId;
                    }

                    // 2. 尝试从备份文件恢复
                    deviceId = LoadDeviceIdFromFile(BackupDeviceIdPath);
                    if (!string.IsNullOrEmpty(deviceId))
                    {
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 从备份文件恢复设备ID: {deviceId}");
                        SaveDeviceIdToAllLocations(deviceId);
                        return deviceId;
                    }

                    // 3. 尝试从注册表恢复
                    deviceId = LoadDeviceIdFromRegistry();
                    if (!string.IsNullOrEmpty(deviceId))
                    {
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 从注册表恢复设备ID: {deviceId}");
                        SaveDeviceIdToAllLocations(deviceId);
                        return deviceId;
                    }

                    // 4. 生成新的设备ID
                    string newDeviceId = GenerateDeviceId();
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 生成新设备ID: {newDeviceId}");

                    // 5. 保存到所有位置
                    SaveDeviceIdToAllLocations(newDeviceId);

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
                    var assembly = System.Reflection.Assembly.Load("System.Management");
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
                    hardwareInfo.Append(Environment.OSVersion.ToString());
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
        /// 从注册表加载设备ID
        /// </summary>
        private static string LoadDeviceIdFromRegistry()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\ICC\DeviceInfo"))
                {
                    if (key != null)
                    {
                        var value = key.GetValue("DeviceId") as string;
                        if (IsValidDeviceId(value))
                        {
                            return value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 从注册表加载设备ID失败: {ex.Message}", LogHelper.LogType.Error);
            }
            return null;
        }

        /// <summary>
        /// 保存设备ID到所有位置
        /// </summary>
        private static void SaveDeviceIdToAllLocations(string deviceId)
        {
            // 保存到主文件
            SaveDeviceIdToFile(DeviceIdFilePath, deviceId);

            // 保存到备份文件
            SaveDeviceIdToFile(BackupDeviceIdPath, deviceId);

            // 保存到注册表
            SaveDeviceIdToRegistry(deviceId);
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
                    // 设置隐藏属性
                    if (filePath.Contains(".sys"))
                    {
                        var dirInfo = new DirectoryInfo(directory);
                        dirInfo.Attributes |= FileAttributes.Hidden | FileAttributes.System;
                    }
                }

                File.WriteAllText(filePath, deviceId);

                // 设置文件属性为隐藏和系统文件
                if (filePath.Contains(".sys"))
                {
                    var fileInfo = new FileInfo(filePath);
                    fileInfo.Attributes |= FileAttributes.Hidden | FileAttributes.System;
                }

                LogHelper.WriteLogToFile($"DeviceIdentifier | 设备ID已保存到: {filePath}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 保存设备ID到文件失败 ({filePath}): {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 保存设备ID到注册表
        /// </summary>
        private static void SaveDeviceIdToRegistry(string deviceId)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\ICC\DeviceInfo"))
                {
                    key?.SetValue("DeviceId", deviceId);
                    key?.SetValue("LastUpdate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                }
                LogHelper.WriteLogToFile($"DeviceIdentifier | 设备ID已保存到注册表");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 保存设备ID到注册表失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 使用频率统计数据结构
        /// </summary>
        private class UsageStats
        {
            [JsonProperty("deviceId")]
            public string DeviceId { get; set; }

            [JsonProperty("lastLaunchTime")]
            public DateTime LastLaunchTime { get; set; }

            [JsonProperty("launchCount")]
            public int LaunchCount { get; set; }

            [JsonProperty("totalUsageMinutes")]
            public long TotalUsageMinutes { get; set; }

            [JsonProperty("averageSessionMinutes")]
            public double AverageSessionMinutes { get; set; }

            [JsonProperty("lastUpdateCheck")]
            public DateTime LastUpdateCheck { get; set; }

            [JsonProperty("updatePriority")]
            public UpdatePriority UpdatePriority { get; set; }

            [JsonProperty("usageFrequency")]
            public UsageFrequency UsageFrequency { get; set; }

            [JsonProperty("dataHash")]
            public string DataHash { get; set; }

            [JsonProperty("lastModified")]
            public DateTime LastModified { get; set; }

            // 每周统计数据
            [JsonProperty("weeklyLaunchCount")]
            public int WeeklyLaunchCount { get; set; }

            [JsonProperty("weeklyUsageMinutes")]
            public long WeeklyUsageMinutes { get; set; }

            [JsonProperty("weekStartDate")]
            public DateTime WeekStartDate { get; set; }

            [JsonProperty("lastWeekLaunchCount")]
            public int LastWeekLaunchCount { get; set; }

            [JsonProperty("lastWeekUsageMinutes")]
            public long LastWeekUsageMinutes { get; set; }

            /// <summary>
            /// 检查并重置每周统计数据
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
                    LastWeekUsageMinutes = WeeklyUsageMinutes;

                    // 重置本周数据
                    WeeklyLaunchCount = 0;
                    WeeklyUsageMinutes = 0;
                    WeekStartDate = currentWeekStart;

                    LogHelper.WriteLogToFile($"DeviceIdentifier | 每周统计重置 - 上周启动: {LastWeekLaunchCount}次, 上周使用: {LastWeekUsageMinutes}分钟");
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
            /// 记录本周的使用时长
            /// </summary>
            public void RecordWeeklyUsage(long minutes)
            {
                CheckAndResetWeeklyStats();
                WeeklyUsageMinutes += minutes;
            }

            /// <summary>
            /// 计算数据哈希值用于完整性验证
            /// </summary>
            public void UpdateDataHash()
            {
                var dataString = $"{DeviceId}|{LaunchCount}|{TotalUsageMinutes}|{LastLaunchTime:yyyyMMddHHmmss}|{WeeklyLaunchCount}|{WeeklyUsageMinutes}|{DataIntegrityKey}";
                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(dataString));
                    DataHash = Convert.ToBase64String(hashBytes);
                }
                LastModified = DateTime.Now;
            }

            /// <summary>
            /// 验证数据完整性
            /// </summary>
            public bool VerifyDataIntegrity()
            {
                try
                {
                    var dataString = $"{DeviceId}|{LaunchCount}|{TotalUsageMinutes}|{LastLaunchTime:yyyyMMddHHmmss}|{WeeklyLaunchCount}|{WeeklyUsageMinutes}|{DataIntegrityKey}";
                    using (var sha256 = SHA256.Create())
                    {
                        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(dataString));
                        var expectedHash = Convert.ToBase64String(hashBytes);
                        return DataHash == expectedHash;
                    }
                }
                catch
                {
                    return false;
                }
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

                    // 更新数据完整性哈希
                    stats.UpdateDataHash();

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
        /// 记录应用退出（计算使用时长）
        /// </summary>
        public static void RecordAppExit()
        {
            try
            {
                lock (fileLock)
                {
                    var stats = LoadUsageStats();

                    // 计算本次会话时长
                    long sessionMinutes = 0;
                    if (stats.LastLaunchTime != DateTime.MinValue)
                    {
                        var sessionDuration = DateTime.Now - stats.LastLaunchTime;
                        sessionMinutes = (long)sessionDuration.TotalMinutes;
                        stats.TotalUsageMinutes += sessionMinutes;

                        // 记录每周使用时长
                        stats.RecordWeeklyUsage(sessionMinutes);

                        // 更新平均会话时长
                        if (stats.LaunchCount > 0)
                        {
                            stats.AverageSessionMinutes = (double)stats.TotalUsageMinutes / stats.LaunchCount;
                        }
                    }

                    // 重新计算使用频率
                    CalculateUsageFrequency(stats);

                    // 更新数据完整性哈希
                    stats.UpdateDataHash();

                    SaveUsageStats(stats);

                    LogHelper.WriteLogToFile($"DeviceIdentifier | 记录应用退出 - 本次会话: {sessionMinutes}分钟, 总时长: {stats.TotalUsageMinutes}分钟, 本周时长: {stats.WeeklyUsageMinutes}分钟");
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

                // 使用真实的每周数据
                var currentWeekLaunches = stats.WeeklyLaunchCount;
                var currentWeekMinutes = stats.WeeklyUsageMinutes;

                // 如果本周数据不足，参考上周数据
                var weeklyLaunches = currentWeekLaunches > 0 ? currentWeekLaunches : stats.LastWeekLaunchCount;
                var weeklyMinutes = currentWeekMinutes > 0 ? currentWeekMinutes : stats.LastWeekUsageMinutes;

                // 综合评分系统（0-100分）
                var frequencyScore = CalculateFrequencyScoreWithWeeklyData(stats, daysSinceLastUse, weeklyLaunches, weeklyMinutes);

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
                                       $"优先级: {stats.UpdatePriority}, 本周启动: {currentWeekLaunches}次, 本周时长: {currentWeekMinutes}分钟");
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
        /// 基于每周真实数据计算综合频率评分（0-100分）
        /// 评分标准：≥80分=高频用户，40-79分=中频用户，<40分=低频用户
        /// </summary>
        /// <param name="stats">使用统计数据</param>
        /// <param name="daysSinceLastUse">距离最后使用的天数</param>
        /// <param name="weeklyLaunches">每周启动次数</param>
        /// <param name="weeklyMinutes">每周使用时长</param>
        /// <returns>综合评分（0-100分）</returns>
        private static int CalculateFrequencyScoreWithWeeklyData(UsageStats stats, double daysSinceLastUse,
            long weeklyLaunches, long weeklyMinutes)
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

            // 每周使用时长评分（20分）- 基于真实的每周使用时长
            if (weeklyMinutes >= 600) score += 20;         // 10小时以上：重度使用
            else if (weeklyMinutes >= 300) score += 15;    // 5-10小时：中重度使用
            else if (weeklyMinutes >= 120) score += 10;    // 2-5小时：中度使用
            else if (weeklyMinutes >= 60) score += 5;      // 1-2小时：轻度使用

            // 历史使用深度评分（10分）- 反映用户的长期使用习惯
            if (stats.TotalUsageMinutes >= 3000) score += 10;    // 50小时以上：资深用户
            else if (stats.TotalUsageMinutes >= 1200) score += 7; // 20-50小时：中等用户
            else if (stats.TotalUsageMinutes >= 300) score += 4;  // 5-20小时：新手用户

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
        /// 获取使用统计信息
        /// </summary>
        public static (int launchCount, long totalMinutes, double avgSession, UpdatePriority priority) GetUsageStats()
        {
            try
            {
                var stats = LoadUsageStats();
                return (stats.LaunchCount, stats.TotalUsageMinutes, stats.AverageSessionMinutes, stats.UpdatePriority);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 获取使用统计失败: {ex.Message}", LogHelper.LogType.Error);
                return (0, 0, 0, UpdatePriority.Medium);
            }
        }

        /// <summary>
        /// 加载使用统计 - 支持多重备份恢复和智能反篡改（强化版本）
        /// </summary>
        private static UsageStats LoadUsageStats()
        {
            try
            {
                // 智能恢复：收集所有可用的数据源，选择最可信的
                var allDataSources = CollectAllUsageDataSources();

                // 如果找到有效数据，返回最可信的
                if (allDataSources.Count > 0)
                {
                    var bestData = SelectMostTrustedData(allDataSources);
                    if (bestData != null)
                    {
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 使用最可信数据源恢复使用统计: {bestData.Source}");

                        // 确保备份同步
                        SaveUsageStatsToAllLocations(bestData.Stats);
                        return bestData.Stats;
                    }
                }

                LogHelper.WriteLogToFile($"DeviceIdentifier | 所有数据源都不可用，检查是否有部分可恢复数据", LogHelper.LogType.Warning);

                // 如果没有完全可信的数据，尝试从部分损坏的数据中恢复
                var partiallyRecoveredData = AttemptPartialDataRecovery(allDataSources);
                if (partiallyRecoveredData != null)
                {
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 从部分损坏数据中恢复使用统计");
                    SaveUsageStatsToAllLocations(partiallyRecoveredData);
                    return partiallyRecoveredData;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 加载使用统计失败: {ex.Message}", LogHelper.LogType.Error);
            }

            // 返回新的统计对象
            var newStats = new UsageStats
            {
                DeviceId = DeviceId,
                LastLaunchTime = DateTime.Now,
                LaunchCount = 0,
                TotalUsageMinutes = 0,
                AverageSessionMinutes = 0,
                LastUpdateCheck = DateTime.MinValue,
                UpdatePriority = UpdatePriority.Medium,
                UsageFrequency = UsageFrequency.Medium
            };

            // 更新数据完整性哈希
            newStats.UpdateDataHash();

            // 保存新统计到所有位置
            SaveUsageStatsToAllLocations(newStats);
            return newStats;
        }

        /// <summary>
        /// 保存使用统计 - 多重备份
        /// </summary>
        private static void SaveUsageStats(UsageStats stats)
        {
            SaveUsageStatsToAllLocations(stats);
        }

        /// <summary>
        /// 数据源信息结构
        /// </summary>
        private class DataSourceInfo
        {
            public UsageStats Stats { get; set; }
            public string Source { get; set; }
            public bool IsIntegrityValid { get; set; }
            public DateTime LastModified { get; set; }
            public int TrustScore { get; set; }
        }

        /// <summary>
        /// 从文件加载使用统计（带完整性验证，但不丢弃篡改数据）
        /// </summary>
        private static UsageStats LoadUsageStatsFromFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var stats = JsonConvert.DeserializeObject<UsageStats>(json);
                    if (stats != null && !string.IsNullOrEmpty(stats.DeviceId))
                    {
                        // 验证数据完整性
                        if (!string.IsNullOrEmpty(stats.DataHash))
                        {
                            if (stats.VerifyDataIntegrity())
                            {
                                LogHelper.WriteLogToFile($"DeviceIdentifier | 数据完整性验证通过: {filePath}");
                                return stats;
                            }
                            else
                            {
                                LogHelper.WriteLogToFile($"DeviceIdentifier | 数据完整性验证失败，可能被篡改: {filePath}", LogHelper.LogType.Warning);
                                return null; // 数据被篡改，不使用
                            }
                        }
                        else
                        {
                            // 旧版本数据，没有哈希值，更新哈希后返回
                            LogHelper.WriteLogToFile($"DeviceIdentifier | 检测到旧版本数据，正在更新完整性哈希: {filePath}");
                            stats.UpdateDataHash();
                            return stats;
                        }
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
        /// 从文件加载使用统计（包括被篡改的数据，用于恢复分析）
        /// </summary>
        private static DataSourceInfo LoadUsageStatsFromFileWithInfo(string filePath, string sourceName)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var stats = JsonConvert.DeserializeObject<UsageStats>(json);
                    if (stats != null && !string.IsNullOrEmpty(stats.DeviceId))
                    {
                        var fileInfo = new FileInfo(filePath);
                        var dataSource = new DataSourceInfo
                        {
                            Stats = stats,
                            Source = sourceName,
                            LastModified = stats.LastModified != DateTime.MinValue ? stats.LastModified : fileInfo.LastWriteTime,
                            IsIntegrityValid = false,
                            TrustScore = 0
                        };

                        // 验证数据完整性
                        if (!string.IsNullOrEmpty(stats.DataHash))
                        {
                            dataSource.IsIntegrityValid = stats.VerifyDataIntegrity();
                            dataSource.TrustScore = dataSource.IsIntegrityValid ? 100 : 20; // 完整数据100分，篡改数据20分
                        }
                        else
                        {
                            // 旧版本数据，中等信任度
                            dataSource.IsIntegrityValid = true;
                            dataSource.TrustScore = 60;
                            stats.UpdateDataHash();
                        }

                        return dataSource;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 从文件加载数据源信息失败 ({filePath}): {ex.Message}", LogHelper.LogType.Error);
            }
            return null;
        }

        /// <summary>
        /// 收集所有可用的使用统计数据源
        /// </summary>
        private static List<DataSourceInfo> CollectAllUsageDataSources()
        {
            var dataSources = new List<DataSourceInfo>();

            try
            {
                // 1. 收集文件数据源
                var fileSources = new[]
                {
                    new { Path = UsageStatsFilePath, Name = "主文件" },
                    new { Path = BackupUsageStatsPath, Name = "第一备份" },
                    new { Path = SecondaryUsageBackupPath, Name = "第二备份" },
                    new { Path = TertiaryUsageBackupPath, Name = "第三备份" },
                    new { Path = QuaternaryUsageBackupPath, Name = "第四备份" }
                };

                foreach (var source in fileSources)
                {
                    var dataSource = LoadUsageStatsFromFileWithInfo(source.Path, source.Name);
                    if (dataSource != null)
                    {
                        dataSources.Add(dataSource);
                    }
                }

                // 2. 收集注册表数据源
                var registrySource = LoadUsageStatsFromRegistryWithInfo(@"Software\ICC\DeviceInfo", "主注册表");
                if (registrySource != null)
                {
                    dataSources.Add(registrySource);
                }

                // 3. 收集备用注册表数据源
                var backupRegistryPaths = new[]
                {
                    new { Path = @"Software\Microsoft\Windows\CurrentVersion\ICC", Name = "备用注册表1" },
                    new { Path = @"Software\Classes\.icc\UsageData", Name = "备用注册表2" },
                    new { Path = @"Software\ICC\Config\Usage", Name = "备用注册表3" }
                };

                foreach (var regPath in backupRegistryPaths)
                {
                    var regSource = LoadUsageStatsFromBackupRegistryWithInfo(regPath.Path, regPath.Name);
                    if (regSource != null)
                    {
                        dataSources.Add(regSource);
                    }
                }

                LogHelper.WriteLogToFile($"DeviceIdentifier | 收集到 {dataSources.Count} 个数据源");
                return dataSources;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 收集数据源失败: {ex.Message}", LogHelper.LogType.Error);
                return dataSources;
            }
        }

        /// <summary>
        /// 选择最可信的数据源
        /// </summary>
        private static DataSourceInfo SelectMostTrustedData(List<DataSourceInfo> dataSources)
        {
            try
            {
                // 首先尝试找到完整性验证通过的数据
                var validSources = dataSources.Where(d => d.IsIntegrityValid).ToList();

                if (validSources.Count > 0)
                {
                    // 在有效数据中选择最新的
                    var bestValid = validSources.OrderByDescending(d => d.LastModified).First();
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 选择完整性验证通过的最新数据: {bestValid.Source}");
                    return bestValid;
                }

                // 如果没有完整性验证通过的数据，选择信任度最高的
                var bestByTrust = dataSources.OrderByDescending(d => d.TrustScore).ThenByDescending(d => d.LastModified).FirstOrDefault();
                if (bestByTrust != null)
                {
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 选择信任度最高的数据: {bestByTrust.Source} (信任度: {bestByTrust.TrustScore})", LogHelper.LogType.Warning);
                    return bestByTrust;
                }

                return null;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 选择最可信数据失败: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        /// <summary>
        /// 尝试从部分损坏的数据中恢复
        /// </summary>
        private static UsageStats AttemptPartialDataRecovery(List<DataSourceInfo> dataSources)
        {
            try
            {
                if (dataSources.Count == 0)
                {
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 没有可用数据源进行部分恢复");
                    return null;
                }

                // 从所有数据源中提取可信的字段
                var recoveredStats = new UsageStats
                {
                    DeviceId = DeviceId,
                    LastLaunchTime = DateTime.Now,
                    LaunchCount = 0,
                    TotalUsageMinutes = 0,
                    AverageSessionMinutes = 0,
                    LastUpdateCheck = DateTime.MinValue,
                    UpdatePriority = UpdatePriority.Medium,
                    UsageFrequency = UsageFrequency.Medium
                };

                // 使用多数投票或最大值策略恢复关键数据
                var launchCounts = dataSources.Where(d => d.Stats.LaunchCount > 0).Select(d => d.Stats.LaunchCount).ToList();
                var usageMinutes = dataSources.Where(d => d.Stats.TotalUsageMinutes > 0).Select(d => d.Stats.TotalUsageMinutes).ToList();

                if (launchCounts.Count > 0)
                {
                    recoveredStats.LaunchCount = (int)launchCounts.Average(); // 使用平均值
                }

                if (usageMinutes.Count > 0)
                {
                    recoveredStats.TotalUsageMinutes = (long)usageMinutes.Average(); // 使用平均值
                }

                // 重新计算平均会话时长
                if (recoveredStats.LaunchCount > 0)
                {
                    recoveredStats.AverageSessionMinutes = (double)recoveredStats.TotalUsageMinutes / recoveredStats.LaunchCount;
                }

                // 重新计算使用频率
                CalculateUsageFrequency(recoveredStats);

                // 更新数据完整性哈希
                recoveredStats.UpdateDataHash();

                LogHelper.WriteLogToFile($"DeviceIdentifier | 部分数据恢复完成 - 启动次数: {recoveredStats.LaunchCount}, 使用时长: {recoveredStats.TotalUsageMinutes}分钟");
                return recoveredStats;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 部分数据恢复失败: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        /// <summary>
        /// 从注册表加载使用统计（带数据源信息）
        /// </summary>
        private static DataSourceInfo LoadUsageStatsFromRegistryWithInfo(string registryPath, string sourceName)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    if (key != null)
                    {
                        var deviceId = key.GetValue("DeviceId") as string;
                        var launchCount = key.GetValue("LaunchCount");
                        var totalMinutes = key.GetValue("TotalUsageMinutes");
                        var lastLaunch = key.GetValue("LastLaunchTime") as string;
                        var priority = key.GetValue("UpdatePriority");
                        var frequency = key.GetValue("UsageFrequency");
                        var dataHash = key.GetValue("DataHash") as string;
                        var lastUpdate = key.GetValue("LastUpdate") as string;

                        // 每周统计数据
                        var weeklyLaunchCount = key.GetValue("WeeklyLaunchCount");
                        var weeklyUsageMinutes = key.GetValue("WeeklyUsageMinutes");
                        var weekStartDate = key.GetValue("WeekStartDate") as string;
                        var lastWeekLaunchCount = key.GetValue("LastWeekLaunchCount");
                        var lastWeekUsageMinutes = key.GetValue("LastWeekUsageMinutes");

                        if (!string.IsNullOrEmpty(deviceId) && launchCount != null)
                        {
                            var stats = new UsageStats
                            {
                                DeviceId = deviceId,
                                LaunchCount = Convert.ToInt32(launchCount),
                                TotalUsageMinutes = totalMinutes != null ? Convert.ToInt64(totalMinutes) : 0,
                                LastLaunchTime = DateTime.TryParse(lastLaunch, out var dt) ? dt : DateTime.Now,
                                UpdatePriority = priority != null ? (UpdatePriority)Convert.ToInt32(priority) : UpdatePriority.Medium,
                                UsageFrequency = frequency != null ? (UsageFrequency)Convert.ToInt32(frequency) : UsageFrequency.Medium,
                                DataHash = dataHash,
                                AverageSessionMinutes = 0,
                                LastUpdateCheck = DateTime.MinValue,

                                // 每周统计数据
                                WeeklyLaunchCount = weeklyLaunchCount != null ? Convert.ToInt32(weeklyLaunchCount) : 0,
                                WeeklyUsageMinutes = weeklyUsageMinutes != null ? Convert.ToInt64(weeklyUsageMinutes) : 0,
                                WeekStartDate = DateTime.TryParse(weekStartDate, out var wsd) ? wsd : DateTime.MinValue,
                                LastWeekLaunchCount = lastWeekLaunchCount != null ? Convert.ToInt32(lastWeekLaunchCount) : 0,
                                LastWeekUsageMinutes = lastWeekUsageMinutes != null ? Convert.ToInt64(lastWeekUsageMinutes) : 0
                            };

                            // 重新计算平均会话时长
                            if (stats.LaunchCount > 0)
                            {
                                stats.AverageSessionMinutes = (double)stats.TotalUsageMinutes / stats.LaunchCount;
                            }

                            var dataSource = new DataSourceInfo
                            {
                                Stats = stats,
                                Source = sourceName,
                                LastModified = DateTime.TryParse(lastUpdate, out var updateTime) ? updateTime : DateTime.Now,
                                IsIntegrityValid = false,
                                TrustScore = 80 // 注册表数据信任度较高
                            };

                            // 验证数据完整性
                            if (!string.IsNullOrEmpty(stats.DataHash))
                            {
                                dataSource.IsIntegrityValid = stats.VerifyDataIntegrity();
                                dataSource.TrustScore = dataSource.IsIntegrityValid ? 100 : 30;
                            }

                            return dataSource;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 从注册表加载数据源信息失败 ({registryPath}): {ex.Message}", LogHelper.LogType.Error);
            }
            return null;
        }

        /// <summary>
        /// 从备用注册表位置加载使用统计（带数据源信息）
        /// </summary>
        private static DataSourceInfo LoadUsageStatsFromBackupRegistryWithInfo(string registryPath, string sourceName)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    if (key != null)
                    {
                        var launchCount = key.GetValue("LC");
                        var totalMinutes = key.GetValue("TUM");
                        var lastLaunchBinary = key.GetValue("LLT");
                        var priority = key.GetValue("UP");
                        var frequency = key.GetValue("UF");
                        var dataHash = key.GetValue("DH") as string;
                        var lastUpdateBinary = key.GetValue("LU");

                        // 每周统计数据
                        var weeklyLaunchCount = key.GetValue("WLC");
                        var weeklyUsageMinutes = key.GetValue("WUM");
                        var weekStartDateBinary = key.GetValue("WSD");
                        var lastWeekLaunchCount = key.GetValue("LWLC");
                        var lastWeekUsageMinutes = key.GetValue("LWUM");

                        if (launchCount != null && totalMinutes != null)
                        {
                            var stats = new UsageStats
                            {
                                DeviceId = DeviceId,
                                LaunchCount = Convert.ToInt32(launchCount),
                                TotalUsageMinutes = Convert.ToInt64(totalMinutes),
                                LastLaunchTime = lastLaunchBinary != null ? DateTime.FromBinary(Convert.ToInt64(lastLaunchBinary)) : DateTime.Now,
                                UpdatePriority = priority != null ? (UpdatePriority)Convert.ToInt32(priority) : UpdatePriority.Medium,
                                UsageFrequency = frequency != null ? (UsageFrequency)Convert.ToInt32(frequency) : UsageFrequency.Medium,
                                DataHash = dataHash,
                                AverageSessionMinutes = 0,
                                LastUpdateCheck = DateTime.MinValue,

                                // 每周统计数据
                                WeeklyLaunchCount = weeklyLaunchCount != null ? Convert.ToInt32(weeklyLaunchCount) : 0,
                                WeeklyUsageMinutes = weeklyUsageMinutes != null ? Convert.ToInt64(weeklyUsageMinutes) : 0,
                                WeekStartDate = weekStartDateBinary != null ? DateTime.FromBinary(Convert.ToInt64(weekStartDateBinary)) : DateTime.MinValue,
                                LastWeekLaunchCount = lastWeekLaunchCount != null ? Convert.ToInt32(lastWeekLaunchCount) : 0,
                                LastWeekUsageMinutes = lastWeekUsageMinutes != null ? Convert.ToInt64(lastWeekUsageMinutes) : 0
                            };

                            // 重新计算平均会话时长
                            if (stats.LaunchCount > 0)
                            {
                                stats.AverageSessionMinutes = (double)stats.TotalUsageMinutes / stats.LaunchCount;
                            }

                            var dataSource = new DataSourceInfo
                            {
                                Stats = stats,
                                Source = sourceName,
                                LastModified = lastUpdateBinary != null ? DateTime.FromBinary(Convert.ToInt64(lastUpdateBinary)) : DateTime.Now,
                                IsIntegrityValid = false,
                                TrustScore = 75 // 备用注册表数据信任度中等
                            };

                            // 验证数据完整性
                            if (!string.IsNullOrEmpty(stats.DataHash))
                            {
                                dataSource.IsIntegrityValid = stats.VerifyDataIntegrity();
                                dataSource.TrustScore = dataSource.IsIntegrityValid ? 100 : 25;
                            }

                            return dataSource;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 从备用注册表加载数据源信息失败 ({registryPath}): {ex.Message}", LogHelper.LogType.Error);
            }
            return null;
        }

        /// <summary>
        /// 从注册表加载使用统计（保持向后兼容）
        /// </summary>
        private static UsageStats LoadUsageStatsFromRegistry()
        {
            var dataSource = LoadUsageStatsFromRegistryWithInfo(@"Software\ICC\DeviceInfo", "主注册表");
            return dataSource?.Stats;
        }

        /// <summary>
        /// 从多个注册表位置加载使用统计（强化恢复）
        /// </summary>
        private static UsageStats LoadUsageStatsFromMultipleRegistryLocations()
        {
            var registryPaths = new[]
            {
                @"Software\Microsoft\Windows\CurrentVersion\ICC",
                @"Software\Classes\.icc\UsageData",
                @"Software\ICC\Config\Usage"
            };

            foreach (var path in registryPaths)
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(path))
                    {
                        if (key != null)
                        {
                            var launchCount = key.GetValue("LC");
                            var totalMinutes = key.GetValue("TUM");
                            var lastLaunchBinary = key.GetValue("LLT");
                            var priority = key.GetValue("UP");
                            var frequency = key.GetValue("UF");
                            var dataHash = key.GetValue("DH") as string;

                            if (launchCount != null && totalMinutes != null)
                            {
                                var stats = new UsageStats
                                {
                                    DeviceId = DeviceId,
                                    LaunchCount = Convert.ToInt32(launchCount),
                                    TotalUsageMinutes = Convert.ToInt64(totalMinutes),
                                    LastLaunchTime = lastLaunchBinary != null ? DateTime.FromBinary(Convert.ToInt64(lastLaunchBinary)) : DateTime.Now,
                                    UpdatePriority = priority != null ? (UpdatePriority)Convert.ToInt32(priority) : UpdatePriority.Medium,
                                    UsageFrequency = frequency != null ? (UsageFrequency)Convert.ToInt32(frequency) : UsageFrequency.Medium,
                                    DataHash = dataHash,
                                    AverageSessionMinutes = 0,
                                    LastUpdateCheck = DateTime.MinValue
                                };

                                // 重新计算平均会话时长
                                if (stats.LaunchCount > 0)
                                {
                                    stats.AverageSessionMinutes = (double)stats.TotalUsageMinutes / stats.LaunchCount;
                                }

                                // 验证数据完整性（如果有哈希值）
                                if (!string.IsNullOrEmpty(stats.DataHash))
                                {
                                    if (stats.VerifyDataIntegrity())
                                    {
                                        LogHelper.WriteLogToFile($"DeviceIdentifier | 从注册表位置恢复数据并验证完整性通过: {path}");
                                        return stats;
                                    }
                                    else
                                    {
                                        LogHelper.WriteLogToFile($"DeviceIdentifier | 注册表位置数据完整性验证失败: {path}", LogHelper.LogType.Warning);
                                        continue; // 尝试下一个位置
                                    }
                                }
                                else
                                {
                                    // 没有哈希值的旧数据，更新哈希后返回
                                    stats.UpdateDataHash();
                                    LogHelper.WriteLogToFile($"DeviceIdentifier | 从注册表位置恢复旧版本数据: {path}");
                                    return stats;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 从注册表位置加载失败 ({path}): {ex.Message}", LogHelper.LogType.Error);
                }
            }

            return null;
        }

        /// <summary>
        /// 保存使用统计到所有位置（强化版本 - 多重隐藏备份）
        /// </summary>
        private static void SaveUsageStatsToAllLocations(UsageStats stats)
        {
            // 保存到主文件
            SaveUsageStatsToFile(UsageStatsFilePath, stats);

            // 保存到第一备份文件
            SaveUsageStatsToFile(BackupUsageStatsPath, stats);

            // 保存到多个隐藏备份位置（专门针对使用频率数据保护）
            SaveUsageStatsToFile(SecondaryUsageBackupPath, stats);
            SaveUsageStatsToFile(TertiaryUsageBackupPath, stats);
            SaveUsageStatsToFile(QuaternaryUsageBackupPath, stats);

            // 保存到注册表
            SaveUsageStatsToRegistry(stats);

            // 保存到注册表的多个位置
            SaveUsageStatsToMultipleRegistryLocations(stats);
        }

        /// <summary>
        /// 保存使用统计到文件
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
                    // 设置隐藏属性
                    if (filePath.Contains(".sys"))
                    {
                        var dirInfo = new DirectoryInfo(directory);
                        dirInfo.Attributes |= FileAttributes.Hidden | FileAttributes.System;
                    }
                }

                string json = JsonConvert.SerializeObject(stats, Formatting.Indented);
                File.WriteAllText(filePath, json);

                // 设置文件属性为隐藏和系统文件
                if (filePath.Contains(".sys"))
                {
                    var fileInfo = new FileInfo(filePath);
                    fileInfo.Attributes |= FileAttributes.Hidden | FileAttributes.System;
                }

                LogHelper.WriteLogToFile($"DeviceIdentifier | 使用统计已保存到: {filePath}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 保存使用统计到文件失败 ({filePath}): {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 保存使用统计到注册表
        /// </summary>
        private static void SaveUsageStatsToRegistry(UsageStats stats)
        {
            try
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 开始保存使用统计到主注册表位置");

                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\ICC\DeviceInfo"))
                {
                    if (key != null)
                    {
                        key.SetValue("DeviceId", stats.DeviceId);
                        key.SetValue("LaunchCount", stats.LaunchCount);
                        key.SetValue("TotalUsageMinutes", stats.TotalUsageMinutes);
                        key.SetValue("LastLaunchTime", stats.LastLaunchTime.ToString("yyyy-MM-dd HH:mm:ss"));
                        key.SetValue("UpdatePriority", (int)stats.UpdatePriority);
                        key.SetValue("UsageFrequency", (int)stats.UsageFrequency);
                        key.SetValue("DataHash", stats.DataHash ?? "");
                        key.SetValue("LastUpdate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                        // 每周统计数据
                        key.SetValue("WeeklyLaunchCount", stats.WeeklyLaunchCount);
                        key.SetValue("WeeklyUsageMinutes", stats.WeeklyUsageMinutes);
                        key.SetValue("WeekStartDate", stats.WeekStartDate.ToString("yyyy-MM-dd"));
                        key.SetValue("LastWeekLaunchCount", stats.LastWeekLaunchCount);
                        key.SetValue("LastWeekUsageMinutes", stats.LastWeekUsageMinutes);

                        LogHelper.WriteLogToFile($"DeviceIdentifier | 使用统计已保存到主注册表 - 总启动: {stats.LaunchCount}次, 本周启动: {stats.WeeklyLaunchCount}次, 总时长: {stats.TotalUsageMinutes}分钟, 本周时长: {stats.WeeklyUsageMinutes}分钟");
                    }
                    else
                    {
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 创建主注册表键失败", LogHelper.LogType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 保存使用统计到主注册表失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 保存使用统计到多个注册表位置（强化保护）
        /// </summary>
        private static void SaveUsageStatsToMultipleRegistryLocations(UsageStats stats)
        {
            var registryPaths = new[]
            {
                @"Software\Microsoft\Windows\CurrentVersion\ICC",
                @"Software\Classes\.icc\UsageData",
                @"Software\ICC\Config\Usage"
            };

            LogHelper.WriteLogToFile($"DeviceIdentifier | 开始保存使用统计到{registryPaths.Length}个备用注册表位置");
            var successCount = 0;

            foreach (var path in registryPaths)
            {
                try
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(path))
                    {
                        if (key != null)
                        {
                            // 使用编码的键名来隐藏数据
                            key.SetValue("LC", stats.LaunchCount); // LaunchCount
                            key.SetValue("TUM", stats.TotalUsageMinutes); // TotalUsageMinutes
                            key.SetValue("LLT", stats.LastLaunchTime.ToBinary()); // LastLaunchTime
                            key.SetValue("UP", (int)stats.UpdatePriority); // UpdatePriority
                            key.SetValue("UF", (int)stats.UsageFrequency); // UsageFrequency
                            key.SetValue("DH", stats.DataHash ?? ""); // DataHash
                            key.SetValue("LU", DateTime.Now.ToBinary()); // LastUpdate

                            // 每周统计数据
                            key.SetValue("WLC", stats.WeeklyLaunchCount); // WeeklyLaunchCount
                            key.SetValue("WUM", stats.WeeklyUsageMinutes); // WeeklyUsageMinutes
                            key.SetValue("WSD", stats.WeekStartDate.ToBinary()); // WeekStartDate
                            key.SetValue("LWLC", stats.LastWeekLaunchCount); // LastWeekLaunchCount
                            key.SetValue("LWUM", stats.LastWeekUsageMinutes); // LastWeekUsageMinutes

                            successCount++;
                            LogHelper.WriteLogToFile($"DeviceIdentifier | 成功保存到备用注册表位置: {path}");
                        }
                        else
                        {
                            LogHelper.WriteLogToFile($"DeviceIdentifier | 创建备用注册表键失败: {path}", LogHelper.LogType.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 保存到备用注册表位置失败 ({path}): {ex.Message}", LogHelper.LogType.Error);
                }
            }

            LogHelper.WriteLogToFile($"DeviceIdentifier | 备用注册表保存完成: {successCount}/{registryPaths.Length} 成功");
        }

        /// <summary>
        /// 记录更新检查时间（同时执行数据保护检查）
        /// </summary>
        public static void RecordUpdateCheck()
        {
            try
            {
                lock (fileLock)
                {
                    var stats = LoadUsageStats();
                    stats.LastUpdateCheck = DateTime.Now;
                    stats.UpdateDataHash();
                    SaveUsageStats(stats);

                    // 定期执行数据保护检查（每10次更新检查执行一次）
                    if (stats.LaunchCount % 10 == 0)
                    {
                        PerformUsageDataProtectionCheck();
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 记录更新检查失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 执行使用频率数据保护检查和自动修复
        /// </summary>
        public static bool PerformUsageDataProtectionCheck()
        {
            try
            {
                lock (fileLock)
                {
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 开始使用频率数据保护检查");

                    var issues = new List<string>();
                    var repaired = new List<string>();
                    var backupPaths = new[]
                    {
                        new { Path = UsageStatsFilePath, Name = "主文件" },
                        new { Path = BackupUsageStatsPath, Name = "第一备份" },
                        new { Path = SecondaryUsageBackupPath, Name = "第二备份" },
                        new { Path = TertiaryUsageBackupPath, Name = "第三备份" },
                        new { Path = QuaternaryUsageBackupPath, Name = "第四备份" }
                    };

                    // 检查所有备份文件
                    UsageStats validStats = null;
                    var missingFiles = new List<string>();

                    foreach (var backup in backupPaths)
                    {
                        if (!File.Exists(backup.Path))
                        {
                            issues.Add($"{backup.Name}丢失");
                            missingFiles.Add(backup.Path);
                        }
                        else
                        {
                            var stats = LoadUsageStatsFromFile(backup.Path);
                            if (stats != null && stats.VerifyDataIntegrity() && validStats == null)
                            {
                                validStats = stats;
                            }
                        }
                    }

                    // 如果找到有效数据，修复丢失的文件
                    if (validStats != null && missingFiles.Count > 0)
                    {
                        foreach (var missingFile in missingFiles)
                        {
                            SaveUsageStatsToFile(missingFile, validStats);
                            repaired.Add($"恢复文件: {Path.GetFileName(missingFile)}");
                        }
                    }

                    // 检查注册表备份
                    var registryPaths = new[]
                    {
                        @"Software\ICC\DeviceInfo",
                        @"Software\Microsoft\Windows\CurrentVersion\ICC",
                        @"Software\Classes\.icc\UsageData",
                        @"Software\ICC\Config\Usage"
                    };

                    var missingRegistryBackups = 0;
                    foreach (var path in registryPaths)
                    {
                        try
                        {
                            using (var key = Registry.CurrentUser.OpenSubKey(path))
                            {
                                if (key == null) missingRegistryBackups++;
                            }
                        }
                        catch
                        {
                            missingRegistryBackups++;
                        }
                    }

                    if (missingRegistryBackups > 0)
                    {
                        issues.Add($"{missingRegistryBackups}个注册表备份丢失");
                        if (validStats != null)
                        {
                            SaveUsageStatsToMultipleRegistryLocations(validStats);
                            repaired.Add("重建注册表备份");
                        }
                    }

                    // 如果没有找到任何有效数据，尝试从注册表恢复
                    if (validStats == null)
                    {
                        validStats = LoadUsageStatsFromRegistry();
                        if (validStats == null)
                        {
                            validStats = LoadUsageStatsFromMultipleRegistryLocations();
                        }

                        if (validStats != null)
                        {
                            SaveUsageStatsToAllLocations(validStats);
                            repaired.Add("从注册表完全恢复数据");
                        }
                        else
                        {
                            // 最后手段：强制重建
                            ForceRebuildUsageDataBackups();
                            repaired.Add("强制重建所有备份");
                        }
                    }

                    // 记录检查结果
                    if (issues.Count > 0)
                    {
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 使用频率数据保护检查发现问题: {string.Join(", ", issues)}", LogHelper.LogType.Warning);
                    }

                    if (repaired.Count > 0)
                    {
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 使用频率数据保护修复: {string.Join(", ", repaired)}");
                    }

                    var protectionScore = CalculateUsageDataProtectionScore();
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 使用频率数据保护检查完成 - 问题: {issues.Count}, 修复: {repaired.Count}, 保护强度: {protectionScore}/100");

                    return protectionScore >= 80;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 使用频率数据保护检查失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 根据优先级决定是否应该推送更新（综合时间和使用频率判断）
        /// </summary>
        /// <param name="updateVersion">更新版本号</param>
        /// <param name="releaseTime">发布时间</param>
        /// <returns>是否应该推送更新</returns>
        public static bool ShouldPushUpdate(string updateVersion, DateTime releaseTime)
        {
            try
            {
                var priority = GetUpdatePriority();
                var frequency = GetUsageFrequency();
                var stats = LoadUsageStats();

                // 计算发布时间到现在的天数
                var daysSinceRelease = (DateTime.Now - releaseTime).TotalDays;

                // 计算最近活跃度（最后一次使用距今的天数）
                var daysSinceLastUse = (DateTime.Now - stats.LastLaunchTime).TotalDays;

                // 判断更新类型（基于版本号）
                var updateType = DetermineUpdateType(updateVersion);

                // 综合判断逻辑
                var shouldPush = ShouldPushUpdateComprehensive(priority, frequency, daysSinceRelease, daysSinceLastUse, stats, updateType);

                LogHelper.WriteLogToFile($"DeviceIdentifier | 更新推送判断 - 版本: {updateVersion}, 类型: {updateType}, " +
                                       $"优先级: {priority}, 频率: {frequency}, 发布天数: {daysSinceRelease:F1}, " +
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
        /// 综合时间和使用频率的更新推送判断逻辑
        /// </summary>
        private static bool ShouldPushUpdateComprehensive(UpdatePriority priority, UsageFrequency frequency,
            double daysSinceRelease, double daysSinceLastUse, UsageStats stats, UpdateType updateType)
        {
            // 考虑用户的总体使用模式
            var isHeavyUser = stats.TotalUsageMinutes > 3000; // 超过50小时的重度用户
            var isFrequentUser = stats.LaunchCount > 100; // 启动超过100次的频繁用户

            // 根据更新类型调整推送策略
            var urgencyMultiplier = GetUpdateUrgencyMultiplier(updateType);

            // 如果用户长时间未使用（超过30天），降低推送优先级
            if (daysSinceLastUse > 30)
            {
                // 热修复和重要更新优先推送
                if (updateType == UpdateType.Hotfix)
                {
                    return daysSinceRelease >= 1; // 热修复1天后推送
                }

                // 但如果是重度用户，仍然要适当推送
                var baseDelay = isHeavyUser ? 7 : 14;
                return daysSinceRelease >= (baseDelay / urgencyMultiplier);
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
                    return daysSinceRelease >= Math.Max(0, 1 / urgencyMultiplier); // 高频用户优先推送
                }

                switch (priority)
                {
                    case UpdatePriority.High:
                        return daysSinceRelease >= Math.Max(0, 1 / urgencyMultiplier);

                    case UpdatePriority.Medium:
                        return daysSinceRelease >= Math.Max(1, 2 / urgencyMultiplier);

                    case UpdatePriority.Low:
                        return daysSinceRelease >= Math.Max(2, 3 / urgencyMultiplier);
                }
            }

            // 中等活跃度用户（3-14天内使用过）
            if (daysSinceLastUse <= 14)
            {
                // 热修复优先推送
                if (updateType == UpdateType.Hotfix)
                {
                    return daysSinceRelease >= 1;
                }

                // 频繁用户优先推送
                if (isFrequentUser && frequency == UsageFrequency.High)
                {
                    return daysSinceRelease >= Math.Max(1, 2 / urgencyMultiplier);
                }

                switch (priority)
                {
                    case UpdatePriority.High:
                        return daysSinceRelease >= Math.Max(1, 2 / urgencyMultiplier);

                    case UpdatePriority.Medium:
                        return daysSinceRelease >= Math.Max(2, 4 / urgencyMultiplier);

                    case UpdatePriority.Low:
                        return daysSinceRelease >= Math.Max(4, 7 / urgencyMultiplier);
                }
            }

            // 较不活跃用户（14-30天内使用过）
            // 对于低频率用户，进一步延迟推送
            var delayMultiplier = frequency == UsageFrequency.Low ? 2 : 1;

            switch (priority)
            {
                case UpdatePriority.High:
                    return daysSinceRelease >= Math.Max(2, 3 * delayMultiplier / urgencyMultiplier);

                case UpdatePriority.Medium:
                    return daysSinceRelease >= Math.Max(4, 7 * delayMultiplier / urgencyMultiplier);

                case UpdatePriority.Low:
                    return daysSinceRelease >= Math.Max(7, 14 * delayMultiplier / urgencyMultiplier);

                default:
                    return daysSinceRelease >= 7;
            }
        }

        /// <summary>
        /// 根据更新类型获取紧急程度倍数
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
        /// 获取设备信息摘要（用于调试）
        /// </summary>
        public static string GetDeviceInfoSummary()
        {
            try
            {
                var (launchCount, totalMinutes, avgSession, priority) = GetUsageStats();
                var frequency = GetUsageFrequency();
                var stats = LoadUsageStats();
                var daysSinceLastUse = (DateTime.Now - stats.LastLaunchTime).TotalDays;

                return $"设备ID: {DeviceId}\n" +
                       $"启动次数: {launchCount}\n" +
                       $"总使用时长: {totalMinutes}分钟 ({totalMinutes / 60.0:F1}小时)\n" +
                       $"平均会话时长: {avgSession:F1}分钟\n" +
                       $"使用频率: {frequency}\n" +
                       $"更新优先级: {priority}\n" +
                       $"最后使用: {daysSinceLastUse:F1}天前\n" +
                       $"用户类型: {GetUserTypeDescription(stats)}";
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
            var isHeavyUser = stats.TotalUsageMinutes > 3000;
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
        /// 模拟更新推送判断（用于测试）
        /// </summary>
        /// <param name="updateVersion">更新版本号</param>
        /// <param name="releaseTime">发布时间</param>
        /// <returns>推送判断详情</returns>
        public static string SimulateUpdatePushDecision(string updateVersion, DateTime releaseTime)
        {
            try
            {
                var priority = GetUpdatePriority();
                var frequency = GetUsageFrequency();
                var stats = LoadUsageStats();
                var daysSinceRelease = (DateTime.Now - releaseTime).TotalDays;
                var daysSinceLastUse = (DateTime.Now - stats.LastLaunchTime).TotalDays;
                var updateType = DetermineUpdateType(updateVersion);
                var urgencyMultiplier = GetUpdateUrgencyMultiplier(updateType);
                var shouldPush = ShouldPushUpdate(updateVersion, releaseTime);

                return $"更新推送判断详情:\n" +
                       $"版本号: {updateVersion}\n" +
                       $"更新类型: {updateType}\n" +
                       $"发布时间: {releaseTime:yyyy-MM-dd HH:mm}\n" +
                       $"发布天数: {daysSinceRelease:F1}天\n" +
                       $"最后使用: {daysSinceLastUse:F1}天前\n" +
                       $"使用频率: {frequency}\n" +
                       $"更新优先级: {priority}\n" +
                       $"紧急程度倍数: {urgencyMultiplier:F1}x\n" +
                       $"用户类型: {GetUserTypeDescription(stats)}\n" +
                       $"推送决定: {(shouldPush ? "是" : "否")}";
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 模拟更新推送判断失败: {ex.Message}", LogHelper.LogType.Error);
                return "模拟判断失败";
            }
        }

        /// <summary>
        /// 数据自检和修复
        /// </summary>
        public static bool PerformDataIntegrityCheck()
        {
            try
            {
                lock (fileLock)
                {
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 开始数据完整性检查");

                    var issues = new List<string>();
                    var repaired = new List<string>();

                    // 检查设备ID文件
                    if (!File.Exists(DeviceIdFilePath))
                    {
                        issues.Add("主设备ID文件丢失");
                        if (File.Exists(BackupDeviceIdPath))
                        {
                            var backupId = LoadDeviceIdFromFile(BackupDeviceIdPath);
                            if (!string.IsNullOrEmpty(backupId))
                            {
                                SaveDeviceIdToFile(DeviceIdFilePath, backupId);
                                repaired.Add("从备份恢复主设备ID文件");
                            }
                        }
                    }

                    // 检查备份设备ID文件
                    if (!File.Exists(BackupDeviceIdPath))
                    {
                        issues.Add("备份设备ID文件丢失");
                        var mainId = LoadDeviceIdFromFile(DeviceIdFilePath);
                        if (!string.IsNullOrEmpty(mainId))
                        {
                            SaveDeviceIdToFile(BackupDeviceIdPath, mainId);
                            repaired.Add("重建备份设备ID文件");
                        }
                    }

                    // 检查使用统计文件
                    if (!File.Exists(UsageStatsFilePath))
                    {
                        issues.Add("主使用统计文件丢失");
                        var backupStatsForRestore = LoadUsageStatsFromFile(BackupUsageStatsPath);
                        if (backupStatsForRestore != null)
                        {
                            SaveUsageStatsToFile(UsageStatsFilePath, backupStatsForRestore);
                            repaired.Add("从备份恢复主使用统计文件");
                        }
                    }

                    // 检查备份使用统计文件
                    if (!File.Exists(BackupUsageStatsPath))
                    {
                        issues.Add("备份使用统计文件丢失");
                        var mainStatsForBackup = LoadUsageStatsFromFile(UsageStatsFilePath);
                        if (mainStatsForBackup != null)
                        {
                            SaveUsageStatsToFile(BackupUsageStatsPath, mainStatsForBackup);
                            repaired.Add("重建备份使用统计文件");
                        }
                    }

                    // 验证数据一致性
                    var mainStats = LoadUsageStatsFromFile(UsageStatsFilePath);
                    var backupStats = LoadUsageStatsFromFile(BackupUsageStatsPath);

                    if (mainStats != null && backupStats != null)
                    {
                        if (mainStats.LaunchCount != backupStats.LaunchCount ||
                            mainStats.TotalUsageMinutes != backupStats.TotalUsageMinutes)
                        {
                            issues.Add("主备份数据不一致");
                            // 使用最新的数据
                            var newerStats = mainStats.LastModified > backupStats.LastModified ? mainStats : backupStats;
                            SaveUsageStatsToAllLocations(newerStats);
                            repaired.Add("同步主备份数据");
                        }
                    }

                    // 记录检查结果
                    if (issues.Count > 0)
                    {
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 发现问题: {string.Join(", ", issues)}", LogHelper.LogType.Warning);
                    }

                    if (repaired.Count > 0)
                    {
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 已修复: {string.Join(", ", repaired)}");
                    }

                    LogHelper.WriteLogToFile($"DeviceIdentifier | 数据完整性检查完成 - 问题: {issues.Count}, 修复: {repaired.Count}");
                    return issues.Count == 0 || repaired.Count > 0;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 数据完整性检查失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 获取使用频率数据保护状态摘要（强化版本）
        /// </summary>
        public static string GetUsageDataProtectionSummary()
        {
            try
            {
                var summary = new StringBuilder();
                summary.AppendLine("使用频率数据保护状态摘要:");

                // 检查主要文件
                summary.AppendLine($"主使用统计文件: {(File.Exists(UsageStatsFilePath) ? "✓" : "✗")}");
                summary.AppendLine($"第一备份文件: {(File.Exists(BackupUsageStatsPath) ? "✓" : "✗")}");

                // 检查多重隐藏备份
                summary.AppendLine($"第二备份文件: {(File.Exists(SecondaryUsageBackupPath) ? "✓" : "✗")}");
                summary.AppendLine($"第三备份文件: {(File.Exists(TertiaryUsageBackupPath) ? "✓" : "✗")}");
                summary.AppendLine($"第四备份文件: {(File.Exists(QuaternaryUsageBackupPath) ? "✓" : "✗")}");

                // 检查注册表备份
                var registryBackups = 0;
                var registryPaths = new[]
                {
                    @"Software\ICC\DeviceInfo",
                    @"Software\Microsoft\Windows\CurrentVersion\ICC",
                    @"Software\Classes\.icc\UsageData",
                    @"Software\ICC\Config\Usage"
                };

                foreach (var path in registryPaths)
                {
                    try
                    {
                        using (var key = Registry.CurrentUser.OpenSubKey(path))
                        {
                            if (key != null) registryBackups++;
                        }
                    }
                    catch { }
                }

                summary.AppendLine($"注册表备份位置: {registryBackups}/4 ✓");

                // 检查数据完整性和可恢复性
                var stats = LoadUsageStats();
                if (stats != null)
                {
                    summary.AppendLine($"数据完整性: {(stats.VerifyDataIntegrity() ? "✓" : "✗")}");
                    summary.AppendLine($"总启动次数: {stats.LaunchCount}");
                    summary.AppendLine($"总使用时长: {stats.TotalUsageMinutes}分钟 ({stats.TotalUsageMinutes / 60.0:F1}小时)");
                    summary.AppendLine($"本周启动次数: {stats.WeeklyLaunchCount}");
                    summary.AppendLine($"本周使用时长: {stats.WeeklyUsageMinutes}分钟 ({stats.WeeklyUsageMinutes / 60.0:F1}小时)");
                    summary.AppendLine($"上周启动次数: {stats.LastWeekLaunchCount}");
                    summary.AppendLine($"上周使用时长: {stats.LastWeekUsageMinutes}分钟 ({stats.LastWeekUsageMinutes / 60.0:F1}小时)");
                    summary.AppendLine($"本周开始日期: {(stats.WeekStartDate != DateTime.MinValue ? stats.WeekStartDate.ToString("yyyy-MM-dd") : "未设置")}");
                    summary.AppendLine($"使用频率: {stats.UsageFrequency}");
                    summary.AppendLine($"更新优先级: {stats.UpdatePriority}");
                    summary.AppendLine($"最后修改: {stats.LastModified:yyyy-MM-dd HH:mm:ss}");
                }

                // 计算保护强度评分
                var protectionScore = CalculateUsageDataProtectionScore();
                summary.AppendLine($"保护强度评分: {protectionScore}/100");

                return summary.ToString();
            }
            catch (Exception ex)
            {
                return $"获取使用频率数据保护状态失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 计算使用频率数据保护强度评分
        /// </summary>
        private static int CalculateUsageDataProtectionScore()
        {
            var score = 0;

            try
            {
                // 文件备份评分（50分）
                if (File.Exists(UsageStatsFilePath)) score += 15;
                if (File.Exists(BackupUsageStatsPath)) score += 10;
                if (File.Exists(SecondaryUsageBackupPath)) score += 8;
                if (File.Exists(TertiaryUsageBackupPath)) score += 8;
                if (File.Exists(QuaternaryUsageBackupPath)) score += 9;

                // 注册表备份评分（30分）
                var registryPaths = new[]
                {
                    @"Software\ICC\DeviceInfo",
                    @"Software\Microsoft\Windows\CurrentVersion\ICC",
                    @"Software\Classes\.icc\UsageData",
                    @"Software\ICC\Config\Usage"
                };

                foreach (var path in registryPaths)
                {
                    try
                    {
                        using (var key = Registry.CurrentUser.OpenSubKey(path))
                        {
                            if (key != null) score += 7;
                        }
                    }
                    catch { }
                }

                // 数据完整性评分（20分）
                var stats = LoadUsageStats();
                if (stats != null)
                {
                    if (!string.IsNullOrEmpty(stats.DataHash)) score += 10;
                    if (stats.VerifyDataIntegrity()) score += 10;
                }
            }
            catch { }

            return Math.Min(100, score);
        }

        /// <summary>
        /// 强制重建所有使用频率数据备份
        /// </summary>
        public static bool ForceRebuildUsageDataBackups()
        {
            try
            {
                lock (fileLock)
                {
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 开始强制重建使用频率数据备份");

                    var stats = LoadUsageStats();
                    if (stats == null)
                    {
                        // 如果无法加载任何数据，创建基础数据
                        stats = new UsageStats
                        {
                            DeviceId = DeviceId,
                            LastLaunchTime = DateTime.Now,
                            LaunchCount = 1,
                            TotalUsageMinutes = 0,
                            AverageSessionMinutes = 0,
                            LastUpdateCheck = DateTime.MinValue,
                            UpdatePriority = UpdatePriority.Medium,
                            UsageFrequency = UsageFrequency.Medium
                        };
                        stats.UpdateDataHash();
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 创建新的基础使用数据");
                    }

                    // 强制保存到所有位置
                    SaveUsageStatsToAllLocations(stats);

                    // 验证重建结果
                    var protectionScore = CalculateUsageDataProtectionScore();
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 使用频率数据备份重建完成，保护强度: {protectionScore}/100");

                    return protectionScore >= 80; // 80分以上认为重建成功
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 强制重建使用频率数据备份失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 获取数据保护状态摘要（保持向后兼容）
        /// </summary>
        public static string GetDataProtectionSummary()
        {
            return GetUsageDataProtectionSummary();
        }





        /// <summary>
        /// 强制执行一次完整的数据保存操作（包括注册表）
        /// </summary>
        public static bool ForceCompleteDataSave()
        {
            try
            {
                lock (fileLock)
                {
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 开始强制完整数据保存");

                    // 保存设备ID到所有位置
                    SaveDeviceIdToAllLocations(DeviceId);

                    // 加载并保存使用统计到所有位置
                    var stats = LoadUsageStats();
                    if (stats != null)
                    {
                        stats.UpdateDataHash();
                        SaveUsageStatsToAllLocations(stats);

                        // 验证注册表保存是否成功
                        var verificationResult = VerifyRegistryData();
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 注册表数据验证结果: {verificationResult}");

                        LogHelper.WriteLogToFile($"DeviceIdentifier | 强制完整数据保存完成");
                        return true;
                    }
                    else
                    {
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 强制完整数据保存失败: 无法加载使用统计", LogHelper.LogType.Error);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 强制完整数据保存失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 验证注册表中的数据是否存在
        /// </summary>
        public static string VerifyRegistryData()
        {
            var results = new StringBuilder();
            results.AppendLine("注册表数据验证结果:");

            try
            {
                // 验证主注册表位置
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\ICC\DeviceInfo"))
                    {
                        if (key != null)
                        {
                            var deviceId = key.GetValue("DeviceId") as string;
                            var launchCount = key.GetValue("LaunchCount");
                            var totalMinutes = key.GetValue("TotalUsageMinutes");
                            var lastUpdate = key.GetValue("LastUpdate") as string;

                            results.AppendLine($"✓ 主注册表位置存在");
                            results.AppendLine($"  设备ID: {deviceId ?? "未找到"}");
                            results.AppendLine($"  启动次数: {launchCount ?? "未找到"}");
                            results.AppendLine($"  使用时长: {totalMinutes ?? "未找到"}分钟");
                            results.AppendLine($"  最后更新: {lastUpdate ?? "未找到"}");
                        }
                        else
                        {
                            results.AppendLine("✗ 主注册表位置不存在");
                        }
                    }
                }
                catch (Exception ex)
                {
                    results.AppendLine($"✗ 主注册表位置访问失败: {ex.Message}");
                }

                // 验证备用注册表位置
                var registryPaths = new[]
                {
                    @"Software\Microsoft\Windows\CurrentVersion\ICC",
                    @"Software\Classes\.icc\UsageData",
                    @"Software\ICC\Config\Usage"
                };

                foreach (var path in registryPaths)
                {
                    try
                    {
                        using (var key = Registry.CurrentUser.OpenSubKey(path))
                        {
                            if (key != null)
                            {
                                var launchCount = key.GetValue("LC");
                                var totalMinutes = key.GetValue("TUM");
                                var lastUpdate = key.GetValue("LU");

                                results.AppendLine($"✓ 备用注册表位置存在: {path}");
                                results.AppendLine($"  启动次数: {launchCount ?? "未找到"}");
                                results.AppendLine($"  使用时长: {totalMinutes ?? "未找到"}");
                                results.AppendLine($"  最后更新: {(lastUpdate != null ? DateTime.FromBinary(Convert.ToInt64(lastUpdate)).ToString("yyyy-MM-dd HH:mm:ss") : "未找到")}");
                            }
                            else
                            {
                                results.AppendLine($"✗ 备用注册表位置不存在: {path}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        results.AppendLine($"✗ 备用注册表位置访问失败 ({path}): {ex.Message}");
                    }
                }

                return results.ToString();
            }
            catch (Exception ex)
            {
                return $"注册表数据验证失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 立即执行一次数据保存并验证注册表写入
        /// </summary>
        public static string SaveAndVerifyRegistryData()
        {
            try
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 开始保存并验证注册表数据");

                // 强制保存数据
                var saveSuccess = ForceCompleteDataSave();

                // 验证注册表数据
                var verificationResult = VerifyRegistryData();

                var result = $"保存操作: {(saveSuccess ? "成功" : "失败")}\n\n{verificationResult}";

                LogHelper.WriteLogToFile($"DeviceIdentifier | 保存并验证注册表数据完成");
                return result;
            }
            catch (Exception ex)
            {
                var errorMsg = $"保存并验证注册表数据失败: {ex.Message}";
                LogHelper.WriteLogToFile($"DeviceIdentifier | {errorMsg}", LogHelper.LogType.Error);
                return errorMsg;
            }
        }




    }
} 