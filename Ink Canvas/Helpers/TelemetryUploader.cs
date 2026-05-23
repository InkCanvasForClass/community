using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Ink_Canvas.Helpers
{
    internal static class TelemetryUploader
    {
        private const string UploadUrl = "https://dev-api.dy.ci/api/telemetry/client/upload/";

        public static async Task UploadTelemetryIfNeededAsync()
        {
            try
            {
                var settings = MainWindow.Settings;
                if (settings == null || settings.Startup == null)
                {
                    return;
                }

                var level = settings.Startup.TelemetryUploadLevel;
                if (level == TelemetryUploadLevel.None)
                {
                    return;
                }

                if (!settings.Startup.HasAcceptedTelemetryPrivacy)
                {
                    LogHelper.WriteLogToFile("TelemetryUploader | 未同意隐私说明，取消遥测上传", LogHelper.LogType.Warning);
                    return;
                }

                string token = GetTelemetryToken();
                if (string.IsNullOrWhiteSpace(token))
                {
                    LogHelper.WriteLogToFile("TelemetryUploader | 未配置遥测 Token，取消遥测上传", LogHelper.LogType.Warning);
                    return;
                }

                string deviceId = DeviceIdentifier.GetDeviceId();
                if (string.IsNullOrWhiteSpace(deviceId) || deviceId.Length < 5)
                {
                    LogHelper.WriteLogToFile("TelemetryUploader | 设备ID无效，取消遥测上传", LogHelper.LogType.Warning);
                    return;
                }

                object crashFile = TryGetLatestFile(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Crashes"),
                    "Crash_*.txt",
                    "崩溃日志");

                object runtimeLogFile = null;
                if (level == TelemetryUploadLevel.Extended)
                {
                    runtimeLogFile = TryGetLatestFile(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs"),
                        "Log_*.txt",
                        "运行日志",
                        true);
                }

                string appVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                string systemVersion = NormalizeWindowsVersion(DeviceIdentifier.GetSystemVersion());
                var usageStats = DeviceIdentifier.GetTelemetryStats();
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var traceContent = new
                {
                    telemetry_level = level.ToString(),
                    id = deviceId,
                    update_channel = settings.Startup.UpdateChannel.ToString(),
                    os = systemVersion,
                    machine_name = Environment.MachineName,
                    is_64_bit_operating_system = Environment.Is64BitOperatingSystem,
                    is_64_bit_process = Environment.Is64BitProcess,
                    processor_count = Environment.ProcessorCount,
                    clr_version = Environment.Version.ToString(),
                    process_uptime_seconds = (DateTime.Now - process.StartTime).TotalSeconds,
                    working_set_mb = process.WorkingSet64 / 1024 / 1024,
                    usage_stats = usageStats,
                    usage_frequency = DeviceIdentifier.GetUsageFrequency().ToString(),
                    update_priority = DeviceIdentifier.GetUpdatePriority().ToString()
                };

                await UploadAsync(token, "trace", appVersion, traceContent).ConfigureAwait(false);

                if (crashFile != null || runtimeLogFile != null)
                {
                    var logContent = new
                    {
                        has_crash_log = crashFile != null,
                        has_runtime_log = runtimeLogFile != null,
                        crash_file = crashFile,
                        runtime_log_file = runtimeLogFile
                    };

                    await UploadAsync(token, "log", appVersion, logContent).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"TelemetryUploader | 遥测上传失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private static async Task UploadAsync(string token, string dataType, string appVersion, object content)
        {
            var payload = new
            {
                token,
                data_type = dataType,
                service_name = "ICC-CE",
#if DEBUG
                environment = "dev",
#else
                environment = "prod",
#endif
                version = appVersion,
                content
            };

            using (var client = new HttpClient())
            using (var requestContent = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"))
            using (var response = await client.PostAsync(UploadUrl, requestContent).ConfigureAwait(false))
            {
                if (response.IsSuccessStatusCode)
                {
                    LogHelper.WriteLogToFile($"TelemetryUploader | {dataType} 数据已上报", LogHelper.LogType.Event);
                }
                else
                {
                    LogHelper.WriteLogToFile($"TelemetryUploader | {dataType} 上传失败: HTTP {(int)response.StatusCode}", LogHelper.LogType.Warning);
                }
            }
        }

        private static string NormalizeWindowsVersion(string systemVersion)
        {
            if (!string.IsNullOrWhiteSpace(systemVersion))
            {
                if (systemVersion.IndexOf("Windows11", StringComparison.OrdinalIgnoreCase) >= 0 || systemVersion.IndexOf("Windows 11", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "Windows 11";
                }

                if (systemVersion.IndexOf("Windows10", StringComparison.OrdinalIgnoreCase) >= 0 || systemVersion.IndexOf("Windows 10", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "Windows 10";
                }

                if (systemVersion.IndexOf("Windows8", StringComparison.OrdinalIgnoreCase) >= 0 || systemVersion.IndexOf("Windows 8", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "Windows 8";
                }

                if (systemVersion.IndexOf("Windows7", StringComparison.OrdinalIgnoreCase) >= 0 || systemVersion.IndexOf("Windows 7", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "Windows 7";
                }
            }

            var version = Environment.OSVersion.Version;
            if (version.Major >= 10 && version.Build >= 22000) return "Windows 11";
            if (version.Major >= 10) return "Windows 10";
            if (version.Major == 6 && version.Minor == 3) return "Windows 8.1";
            if (version.Major == 6 && version.Minor == 2) return "Windows 8";
            if (version.Major == 6 && version.Minor == 1) return "Windows 7";
            return "Windows " + version.Major;
        }

        private static string GetTelemetryToken()
        {
            try
            {
                var envToken = Environment.GetEnvironmentVariable("DLASS_TELEMETRY_TOKEN");
                if (!string.IsNullOrWhiteSpace(envToken))
                {
                    return envToken.Trim();
                }

                envToken = Environment.GetEnvironmentVariable("ICC_CE_TELEMETRY_TOKEN");
                if (!string.IsNullOrWhiteSpace(envToken))
                {
                    return envToken.Trim();
                }

                var announcementToken = NotificationSettings.DefaultAnnouncementSoftwareToken;
                if (!string.IsNullOrWhiteSpace(announcementToken))
                {
                    return announcementToken.Trim();
                }

                try
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    var resourceName = "Ink_Canvas.telemetry_token.txt";
                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                            {
                                string token = reader.ReadToEnd().Trim();
                                if (!string.IsNullOrWhiteSpace(token))
                                {
                                    return token;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"从程序集资源读取遥测 Token 失败: {ex.Message}", LogHelper.LogType.Warning);
                }

                string assemblyLocation = Assembly.GetExecutingAssembly().Location;
                string currentDir = Path.GetDirectoryName(assemblyLocation);

                for (int i = 0; i < 5; i++)
                {
                    string tokenFilePath = Path.Combine(currentDir, "telemetry_token.txt");
                    if (File.Exists(tokenFilePath))
                    {
                        string token = File.ReadAllText(tokenFilePath, Encoding.UTF8).Trim();
                        if (!string.IsNullOrWhiteSpace(token))
                        {
                            return token;
                        }
                    }

                    DirectoryInfo parentDir = Directory.GetParent(currentDir);
                    if (parentDir == null)
                    {
                        break;
                    }
                    currentDir = parentDir.FullName;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static object TryGetLatestFile(string directory, string pattern, string fileType, bool importantOnly = false)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    return null;
                }

                var latest = new DirectoryInfo(directory)
                    .GetFiles(pattern)
                    .OrderByDescending(file => file.LastWriteTime)
                    .FirstOrDefault();

                if (latest == null)
                {
                    return null;
                }

                string content = File.ReadAllText(latest.FullName);
                if (importantOnly)
                {
                    content = PickImportantLogContent(content);
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        return null;
                    }
                }

                return new
                {
                    file_type = fileType,
                    file_name = latest.Name,
                    last_write_time = latest.LastWriteTime.ToString("o"),
                    important_only = importantOnly,
                    content
                };
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"TelemetryUploader | 收集{fileType}失败: {ex.Message}",
                    LogHelper.LogType.Warning);
                return null;
            }
        }

        private static string PickImportantLogContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            var importantLines = content
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Where(IsImportantLogLine)
                .TakeLast(200);

            return string.Join(Environment.NewLine, importantLines);
        }

        private static bool IsImportantLogLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            return line.IndexOf("[Error]", StringComparison.OrdinalIgnoreCase) >= 0
                   || line.IndexOf("[Warning]", StringComparison.OrdinalIgnoreCase) >= 0
                   || line.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0
                   || line.IndexOf("异常", StringComparison.OrdinalIgnoreCase) >= 0
                   || line.IndexOf("失败", StringComparison.OrdinalIgnoreCase) >= 0
                   || line.IndexOf("崩溃", StringComparison.OrdinalIgnoreCase) >= 0
                   || line.IndexOf("终止", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
