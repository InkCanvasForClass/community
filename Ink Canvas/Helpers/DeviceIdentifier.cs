using Newtonsoft.Json;
using OSVersionExtension;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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
        private static readonly string UsageStatsBackupPath = Path.Combine(App.RootPath, "Saves", "usage_stats_backup.enc");

        private static readonly Lazy<string> deviceIdLazy = new Lazy<string>(() =>
        {
            try
            {
                return GetOrCreateDeviceId();
            }
            catch
            {
                return GenerateFallbackDeviceId();
            }
        });
        // 兼容旧代码对 DeviceId 字段的引用，提供同名只读属性（延迟获取）
        private static string DeviceId => deviceIdLazy.Value;
        private static readonly object fileLock = new object();
        private static UsageStats usageStatsCache;
        private static DateTime usageStatsCacheTime;
        private static readonly TimeSpan UsageStatsCacheDuration = TimeSpan.FromMinutes(2);

        // 延迟初始化，避免在类型加载/静态构造期间触发平台相关异常

        /// <summary>
        /// 获取或创建设备ID
        /// </summary>
        /// <returns>25字符的唯一设备标识符</returns>
        public static string GetDeviceId()
        {
            return deviceIdLazy.Value;
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
                    // 计算当前设备的硬件指纹
                    string currentHardwareFingerprint = GenerateHardwareFingerprint();

                    // 1. 尝试从主文件读取设备ID及其硬件指纹
                    var storedInfo = LoadDeviceIdFromFile(DeviceIdFilePath);
                    if (storedInfo != null && !string.IsNullOrEmpty(storedInfo.DeviceId) && IsValidDeviceId(storedInfo.DeviceId))
                    {
                        if (!string.IsNullOrEmpty(storedInfo.HardwareFingerprint))
                        {
                            if (storedInfo.HardwareFingerprint == currentHardwareFingerprint)
                            {
                                LogHelper.WriteLogToFile($"DeviceIdentifier | 从主文件读取设备ID且硬件信息一致: {storedInfo.DeviceId}");
                                return storedInfo.DeviceId;
                            }

                            LogHelper.WriteLogToFile("DeviceIdentifier | 检测到当前硬件信息与保存的设备ID不一致，将重新生成设备ID");
                        }
                        else
                        {
                            LogHelper.WriteLogToFile("DeviceIdentifier | 检测到旧格式设备ID文件（无硬件信息），将基于当前硬件重新生成设备ID并升级文件格式");
                        }
                    }

                    // 2. 基于当前硬件指纹生成新的设备ID
                    string newDeviceId = GenerateDeviceIdFromFingerprint(currentHardwareFingerprint);
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 生成新设备ID: {newDeviceId}");

                    // 3. 保存到主文件（包含硬件指纹）
                    var newInfo = new DeviceIdInfo
                    {
                        DeviceId = newDeviceId,
                        HardwareFingerprint = currentHardwareFingerprint
                    };
                    SaveDeviceIdToFile(DeviceIdFilePath, newInfo);

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
                // 基于当前硬件指纹生成设备ID
                string hardwareFingerprint = GenerateHardwareFingerprint();
                return GenerateDeviceIdFromFingerprint(hardwareFingerprint);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 生成设备ID时出错: {ex.Message}", LogHelper.LogType.Error);
                return GenerateFallbackDeviceId();
            }
        }

        /// <summary>
        /// 生成当前设备的硬件指纹字符串（用于生成和校验设备ID）
        /// </summary>
        private static string GenerateHardwareFingerprint()
        {
            var hardwareInfo = new StringBuilder();
            AppendFingerprintPart(hardwareInfo, "CPU",
                GetWmiProperty("SELECT ProcessorId FROM Win32_Processor", "ProcessorId"),
                GetRegistryValue(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString"),
                GetRegistryValue(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "Identifier"));

            AppendFingerprintPart(hardwareInfo, "BOARD",
                GetWmiProperty("SELECT SerialNumber FROM Win32_BaseBoard", "SerialNumber"),
                GetRegistryValue(@"HARDWARE\DESCRIPTION\System\BIOS", "BaseBoardSerialNumber"),
                GetRegistryValue(@"HARDWARE\DESCRIPTION\System\BIOS", "BaseBoardProduct"));

            AppendFingerprintPart(hardwareInfo, "BIOS",
                GetWmiProperty("SELECT SerialNumber FROM Win32_BIOS", "SerialNumber"),
                GetRegistryValue(@"HARDWARE\DESCRIPTION\System\BIOS", "BIOSVersion"),
                GetRegistryValue(@"HARDWARE\DESCRIPTION\System\BIOS", "BIOSVendor"));

            AppendFingerprintPart(hardwareInfo, "DISK",
                GetWmiProperty("SELECT SerialNumber FROM Win32_DiskDrive WHERE MediaType='Fixed hard disk media'", "SerialNumber"),
                GetSystemDriveVolumeSerial(),
                GetRegistryValue(@"SOFTWARE\Microsoft\Cryptography", "MachineGuid"));

            if (hardwareInfo.Length < 10)
            {
                hardwareInfo.Append(Environment.MachineName);
                hardwareInfo.Append(Environment.UserName);
                hardwareInfo.Append(Environment.OSVersion);
            }

            return hardwareInfo.ToString();
        }

        private static void AppendFingerprintPart(StringBuilder hardwareInfo, string key, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    hardwareInfo.Append(key).Append(':').Append(candidate.Trim()).Append(';');
                    return;
                }
            }
        }

        private static string GetWmiProperty(string query, string propertyName)
        {
            // 仅在 Windows 桌面环境尝试使用 WMI
            try
            {
                if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                    return null;

                // 尝试以非抛出方式解析类型，避免在不支持的宿主中触发构造函数抛出
                var searcherType = Type.GetType("System.Management.ManagementObjectSearcher, System.Management", throwOnError: false);
                if (searcherType == null)
                {
                    return null;
                }

                object searcher = null;
                try
                {
                    searcher = Activator.CreateInstance(searcherType, query);
                }
                catch (PlatformNotSupportedException)
                {
                    // 在某些运行时（例如非 Windows 或受限环境）System.Management 不受支持，返回 null 作为回退
                    return null;
                }
                var getMethod = searcherType.GetMethod("Get");
                var resultCollection = getMethod?.Invoke(searcher, null);
                if (resultCollection == null)
                {
                    return null;
                }

                var enumerator = resultCollection.GetType().GetMethod("GetEnumerator")?.Invoke(resultCollection, null);
                var moveNextMethod = enumerator?.GetType().GetMethod("MoveNext");
                var currentProperty = enumerator?.GetType().GetProperty("Current");
                if (enumerator == null || moveNextMethod == null || currentProperty == null)
                {
                    return null;
                }

                if (!(bool)moveNextMethod.Invoke(enumerator, null))
                {
                    return null;
                }

                var currentObject = currentProperty.GetValue(enumerator);
                var indexer = currentObject?.GetType().GetProperty("Item", new[] { typeof(string) });
                var result = indexer?.GetValue(currentObject, new object[] { propertyName })?.ToString();

                searcher?.GetType().GetMethod("Dispose")?.Invoke(searcher, null);
                return result;
            }
            catch (PlatformNotSupportedException)
            {
                // 在某些运行时（例如非 Windows 或受限环境）System.Management 不受支持，返回 null 作为回退
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string GetRegistryValue(string subKey, string valueName)
        {
            try
            {
                return Microsoft.Win32.Registry.GetValue($@"HKEY_LOCAL_MACHINE\{subKey}", valueName, null)?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static string GetSystemDriveVolumeSerial()
        {
            try
            {
                var rootPath = Path.GetPathRoot(Environment.SystemDirectory);
                if (string.IsNullOrWhiteSpace(rootPath))
                {
                    return null;
                }

                if (GetVolumeInformation(rootPath, null, 0, out uint serialNumber, out _, out _, null, 0))
                {
                    return serialNumber.ToString("X8");
                }
            }
            catch
            {
            }

            return null;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetVolumeInformation(
            string rootPathName,
            StringBuilder volumeNameBuffer,
            uint volumeNameSize,
            out uint volumeSerialNumber,
            out uint maximumComponentLength,
            out uint fileSystemFlags,
            StringBuilder fileSystemNameBuffer,
            uint nFileSystemNameSize);

        /// <summary>
        /// 基于硬件指纹生成25字符的设备ID
        /// </summary>
        private static string GenerateDeviceIdFromFingerprint(string hardwareFingerprint)
        {
            // 生成哈希
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(hardwareFingerprint ?? string.Empty));
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
        private static DeviceIdInfo LoadDeviceIdFromFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string content = File.ReadAllText(filePath).Trim();

                    try
                    {
                        var info = JsonConvert.DeserializeObject<DeviceIdInfo>(content);
                        if (info != null && !string.IsNullOrEmpty(info.DeviceId) && IsValidDeviceId(info.DeviceId))
                        {
                            return info;
                        }
                    }
                    catch
                    {
                    }

                    if (IsValidDeviceId(content))
                    {
                        return new DeviceIdInfo
                        {
                            DeviceId = content,
                            HardwareFingerprint = null
                        };
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
        /// <remarks>
        /// 将设备标识信息以格式化的 JSON 写入指定文件，并确保目标目录存在；在失败时记录错误但不抛出异常。
        /// </remarks>
        /// <param name="filePath">目标文件的完整路径，用于保存设备标识信息。</param>
        /// <param name="info">要保存的设备标识信息对象（包含 DeviceId 和 可选的硬件指纹）。</param>
        private static void SaveDeviceIdToFile(string filePath, DeviceIdInfo info)
        {
            try
            {
                // 确保目录存在
                var directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    ProcessProtectionManager.WithWriteAccess(directory, () => Directory.CreateDirectory(directory));
                }

                string json = JsonConvert.SerializeObject(info, Formatting.Indented);
                ProcessProtectionManager.WithWriteAccess(filePath, () => File.WriteAllText(filePath, json));

                LogHelper.WriteLogToFile($"DeviceIdentifier | 设备ID已保存到: {filePath}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 保存设备ID到文件失败 ({filePath}): {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private class DeviceIdInfo
        {
            [JsonProperty("deviceId")]
            public string DeviceId { get; set; }

            [JsonProperty("hardwareFingerprint")]
            public string HardwareFingerprint { get; set; }
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

            [JsonProperty("systemVersion")]
            public string SystemVersion { get; set; }

            [JsonProperty("totalUsageSeconds")]
            public long TotalUsageSeconds { get; set; }

            [JsonProperty("averageSessionSeconds")]
            public double AverageSessionSeconds { get; set; }



            [JsonProperty("lastUpdateCheck")]
            public DateTime LastUpdateCheck { get; set; }

            [JsonProperty("updatePriority")]
            public UpdatePriority UpdatePriority { get; set; }

            [JsonProperty("usageFrequency")]
            public UsageFrequency UsageFrequency { get; set; }

            [JsonProperty("lastModified")]
            public DateTime LastModified { get; set; }

            [JsonProperty("updateChannel")]
            public Ink_Canvas.UpdateChannel UpdateChannel { get; set; } = Ink_Canvas.UpdateChannel.Release;

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

                    // 重置本周数据
                    WeeklyLaunchCount = 0;
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

                    try
                    {
                        var osName = OSVersion.GetOperatingSystem();
                        var osVersion = OSVersion.GetOSVersion();
                        string versionText = osVersion != null
                            ? $"{osName} {osVersion.Version}"
                            : osName.ToString();
                        stats.SystemVersion = versionText;
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 刷新系统版本信息失败: {ex.Message}", LogHelper.LogType.Warning);
                    }

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

                    // 计算本次会话时长（秒级精度）
                    long sessionSeconds = 0;
                    if (stats.LastLaunchTime != DateTime.MinValue)
                    {
                        var sessionDuration = DateTime.Now - stats.LastLaunchTime;
                        sessionSeconds = (long)sessionDuration.TotalSeconds;

                        // 更新秒级精度数据
                        stats.TotalUsageSeconds += sessionSeconds;

                        // 记录每周使用时长（秒级精度）
                        stats.RecordWeeklyUsage(sessionSeconds);

                        // 更新平均会话时长（秒级精度）
                        if (stats.LaunchCount > 0)
                        {
                            stats.AverageSessionSeconds = (double)stats.TotalUsageSeconds / stats.LaunchCount;
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

        public static string GetSystemVersion()
        {
            try
            {
                var stats = GetUsageStatsCached();
                return stats.SystemVersion;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 获取系统版本失败: {ex.Message}", LogHelper.LogType.Error);
                return null;
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



                // 如果本周数据不足，参考上周数据
                var weeklyLaunches = currentWeekLaunches > 0 ? currentWeekLaunches : stats.LastWeekLaunchCount;
                var weeklySeconds = currentWeekSeconds > 0 ? currentWeekSeconds : stats.LastWeekUsageSeconds;



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
            var totalSeconds = stats.TotalUsageSeconds;
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
                var stats = GetUsageStatsCached();
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
                var stats = GetUsageStatsCached();
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

        public static object GetTelemetryStats()
        {
            try
            {
                var stats = GetUsageStatsCached(true);
                stats.CheckAndResetWeeklyStats();
                return new
                {
                    last_launch_time = stats.LastLaunchTime,
                    launch_count = stats.LaunchCount,
                    total_usage_seconds = stats.TotalUsageSeconds,
                    average_session_seconds = stats.AverageSessionSeconds,
                    last_update_check = stats.LastUpdateCheck,
                    weekly_launch_count = stats.WeeklyLaunchCount,
                    weekly_usage_seconds = stats.WeeklyUsageSeconds,
                    week_start_date = stats.WeekStartDate,
                    last_week_launch_count = stats.LastWeekLaunchCount,
                    last_week_usage_seconds = stats.LastWeekUsageSeconds,
                    last_modified = stats.LastModified
                };
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 获取遥测统计失败: {ex.Message}", LogHelper.LogType.Error);
                return null;
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
                    return stats;
                }

                // 2. 主文件无法读取，尝试从备份文件恢复
                LogHelper.WriteLogToFile("DeviceIdentifier | 主文件无法读取，尝试从备份文件恢复");
                if (RestoreUsageStatsFromBackup())
                {
                    // 恢复成功后重新尝试加载主文件
                    stats = LoadUsageStatsFromFile(UsageStatsFilePath);
                    if (stats != null)
                    {
                        LogHelper.WriteLogToFile("DeviceIdentifier | 从备份文件成功恢复主文件");
                        return stats;
                    }
                }

                // 3. 如果备份恢复也失败，尝试直接加载备份文件
                stats = LoadUsageStatsFromFile(UsageStatsBackupPath);
                if (stats != null)
                {
                    LogHelper.WriteLogToFile("DeviceIdentifier | 直接使用备份文件数据");
                    return stats;
                }



                // 如果所有文件都不存在或损坏，返回新的统计对象
                var newStats = new UsageStats
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

        private static UsageStats GetUsageStatsCached(bool forceRefresh = false)
        {
            lock (fileLock)
            {
                if (!forceRefresh
                    && usageStatsCache != null
                    && (DateTime.Now - usageStatsCacheTime) < UsageStatsCacheDuration)
                {
                    return usageStatsCache;
                }

                usageStatsCache = LoadUsageStats();
                usageStatsCacheTime = DateTime.Now;
                return usageStatsCache;
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

            usageStatsCache = stats;
            usageStatsCacheTime = DateTime.Now;
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

                        // 验证校验
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
                    ProcessProtectionManager.WithWriteAccess(directory, () => Directory.CreateDirectory(directory));
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

                    ProcessProtectionManager.WithWriteAccess(filePath, () => File.WriteAllBytes(filePath, finalData));

                    LogHelper.WriteLogToFile($"DeviceIdentifier | 加密使用统计已保存到: {filePath}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 保存使用统计到文件失败 ({filePath}): {ex.Message}", LogHelper.LogType.Error);
            }
        }

        public static void UpdateUsageChannel(Ink_Canvas.UpdateChannel channel)
        {
            try
            {
                lock (fileLock)
                {
                    var stats = LoadUsageStats();
                    if (stats == null)
                    {
                        stats = new UsageStats
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

                    stats.UpdateChannel = channel;
                    SaveUsageStats(stats);

                    LogHelper.WriteLogToFile($"DeviceIdentifier | 更新使用统计中的通道信息: {channel}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 更新通道信息到使用统计失败: {ex.Message}", LogHelper.LogType.Error);
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
        /// 获取上次更新检查时间
        /// </summary>
        public static DateTime GetLastUpdateCheck()
        {
            try
            {
                var stats = LoadUsageStats();
                return stats.LastUpdateCheck;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 获取上次更新检查时间失败: {ex.Message}", LogHelper.LogType.Error);
                return DateTime.MinValue;
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
        public static bool ShouldPushUpdate(string updateVersion, DateTime releaseTime, bool isAutoUpdate = true, DateTime? currentVersionReleaseTime = null, string localVersion = null)
        {
            try
            {
                // 判断更新类型（基于版本号）
                var updateType = DetermineUpdateType(updateVersion);

                // 如果不是自动更新（即版本修复），则应用不同的策略
                if (!isAutoUpdate)
                {
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

                // 当无法获取版本发布时间时，判断版本号差异
                if (!currentVersionReleaseTime.HasValue && !string.IsNullOrEmpty(localVersion))
                {
                    int versionDiff = CalculateVersionGenerationDifference(localVersion, updateVersion);
                    LogHelper.WriteLogToFile($"DeviceIdentifier | 无法获取版本发布时间，使用版本号差异判断 - 本地版本: {localVersion}, 远程版本: {updateVersion}, 代数差异: {versionDiff}");

                    if (versionDiff <= 0)
                    {
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 版本号代数差异({versionDiff})<=0，可能是相同版本或降级，暂不更新");
                        return false;
                    }

                    // 当代数差异较大（>=3）时直接放行，避免被分级策略卡住
                    if (versionDiff >= 3)
                    {
                        LogHelper.WriteLogToFile($"DeviceIdentifier | 版本号代数差异({versionDiff})>=3，跳过分级策略直接推送");
                        return true;
                    }

                    LogHelper.WriteLogToFile($"DeviceIdentifier | 版本号代数差异({versionDiff})>=1，进入分级策略判断");
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
        /// 计算版本号代数差异
        /// </summary>
        /// <param name="localVersion">本地版本号</param>
        /// <param name="remoteVersion">远程版本号</param>
        /// <returns>版本号代数差异，如果无法计算则返回0</returns>
        private static int CalculateVersionGenerationDifference(string localVersion, string remoteVersion)
        {
            try
            {
                if (string.IsNullOrEmpty(localVersion) || string.IsNullOrEmpty(remoteVersion))
                    return 0;

                // 移除可能的前缀（如 "v"）
                var cleanLocal = localVersion.TrimStart('v', 'V');
                var cleanRemote = remoteVersion.TrimStart('v', 'V');

                // 解析版本号 (格式: X.X.X.X)
                var localParts = cleanLocal.Split('.');
                var remoteParts = cleanRemote.Split('.');

                if (localParts.Length < 4 || remoteParts.Length < 4)
                    return 0;

                // 解析四个版本号部分
                if (int.TryParse(localParts[0], out int localMajor) &&
                    int.TryParse(localParts[1], out int localMinor) &&
                    int.TryParse(localParts[2], out int localBuild) &&
                    int.TryParse(localParts[3], out int localRevision) &&
                    int.TryParse(remoteParts[0], out int remoteMajor) &&
                    int.TryParse(remoteParts[1], out int remoteMinor) &&
                    int.TryParse(remoteParts[2], out int remoteBuild) &&
                    int.TryParse(remoteParts[3], out int remoteRevision))
                {
                    var localSemver = new Version(localMajor, localMinor, localBuild, localRevision);
                    var remoteSemver = new Version(remoteMajor, remoteMinor, remoteBuild, remoteRevision);
                    int direction = remoteSemver.CompareTo(localSemver);
                    if (direction == 0) return 0;
                    int sign = direction > 0 ? 1 : -1;

                    int majorDiff = Math.Abs(remoteMajor - localMajor);
                    if (majorDiff != 0) return sign * (majorDiff * 1000);

                    int minorDiff = Math.Abs(remoteMinor - localMinor);
                    if (minorDiff != 0) return sign * (minorDiff * 100);

                    int buildDiff = Math.Abs(remoteBuild - localBuild);
                    if (buildDiff != 0) return sign * (buildDiff * 10);

                    return sign * Math.Abs(remoteRevision - localRevision);
                }

                return 0;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"DeviceIdentifier | 计算版本号代数差异失败: {ex.Message}", LogHelper.LogType.Warning);
                return 0;
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
