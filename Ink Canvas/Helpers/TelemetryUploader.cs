using Sentry;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ink_Canvas.Helpers
{
    internal static class TelemetryUploader
    {
        private static readonly Regex EmailRegex = new Regex(
            @"(?i)\b[a-z0-9._%+\-]+@[a-z0-9.\-]+\.[a-z]{2,}\b",
            RegexOptions.Compiled);

        private static readonly Regex PhoneRegex = new Regex(
            @"\b1[3-9]\d{9}\b",
            RegexOptions.Compiled);

        private static readonly Regex IPv4Regex = new Regex(
            @"\b(?:\d{1,3}\.){3}\d{1,3}\b",
            RegexOptions.Compiled);

        private static readonly Regex WindowsPathRegex = new Regex(
            @"\b[A-Za-z]:\\[^\s<>|]+\b",
            RegexOptions.Compiled);

        private static readonly Regex UncPathRegex = new Regex(
            @"\\\\[^\s]+",
            RegexOptions.Compiled);

        private static readonly Regex KeyValueSecretRegex = new Regex(
            @"(?i)(\b(?:access[_-]?token|refresh[_-]?token|token|password|passwd|pwd|secret|authorization)\b\s*[:=]\s*)([^\s,;]+)",
            RegexOptions.Compiled);

        private static readonly Regex JsonSecretRegex = new Regex(
            "(?i)(\"(?:access_token|refresh_token|token|password|passwd|pwd|secret|authorization)\"\\s*:\\s*\")([^\"]*)(\")",
            RegexOptions.Compiled);

        private static readonly Regex UrlSecretRegex = new Regex(
            @"(?i)([?&](?:access_token|token|password|pwd|secret)=)[^&\s]+",
            RegexOptions.Compiled);

        public static Task UploadTelemetryIfNeededAsync()
        {
            return Task.Run(() =>
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

                    string deviceId = DeviceIdentifier.GetDeviceId();
                    if (string.IsNullOrWhiteSpace(deviceId) || deviceId.Length < 5)
                    {
                        LogHelper.WriteLogToFile("TelemetryUploader | 设备ID无效，取消遥测上传", LogHelper.LogType.Warning);
                        return;
                    }

                    // Basic 和 Extended 均上传崩溃日志（脱敏）
                    object crashFile = TryGetLatestSanitizedFile(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Crashes"),
                        "Crash_*.txt",
                        "崩溃日志");

                    // Extended 额外上传运行日志（脱敏）
                    object runtimeLogFile = null;
                    if (level == TelemetryUploadLevel.Extended)
                    {
                        runtimeLogFile = TryGetLatestSanitizedFile(
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs"),
                            "Log_*.txt",
                            "运行日志");
                    }

                    var telemetryData = new
                    {
                        telemetry_level = level.ToString(),
                        device_id = deviceId,
                        update_channel = settings.Startup.UpdateChannel.ToString(),
                        app_version = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                        os_version = Environment.OSVersion.VersionString,
                        has_crash_log = crashFile != null,
                        has_runtime_log = runtimeLogFile != null
                    };

                    // 通过 Sentry 上报一个包含遥测信息的事件
                    string userName = Environment.UserName;
                    SentrySdk.ConfigureScope(scope =>
                    {
                        scope.User = new SentryUser
                        {
                            Id = deviceId,
                            Username = userName,
                            Email = $"{userName}",
                            IpAddress = "{{auto}}"
                        };
                    });

                    var evt = new SentryEvent
                    {
                        Message = "ICC CE Telemetry",
                        Level = SentryLevel.Info
                    };

                    evt.User = new SentryUser
                    {
                        Id = deviceId,
                        Username = userName,
                        Email = $"{userName}",
                        IpAddress = "{{auto}}"
                    };

                    evt.SetTag("telemetry_level", level.ToString());
                    evt.SetTag("device_id", deviceId);
                    evt.SetTag("update_channel", settings.Startup.UpdateChannel.ToString());
                    evt.SetTag("app_version", Assembly.GetExecutingAssembly().GetName().Version.ToString());
                    evt.SetTag("os_version", Environment.OSVersion.VersionString);
                    evt.SetExtra("telemetry_data", telemetryData);

                    if (crashFile != null)
                    {
                        evt.SetExtra("crash_file", crashFile);
                    }

                    if (runtimeLogFile != null)
                    {
                        evt.SetExtra("runtime_log_file", runtimeLogFile);
                    }

                    SentrySdk.CaptureEvent(evt);
                    LogHelper.WriteLogToFile("TelemetryUploader | 遥测数据已通过 Sentry 上报", LogHelper.LogType.Event);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"TelemetryUploader | 遥测上传失败: {ex.Message}", LogHelper.LogType.Warning);
                }
            });
        }

        private static object TryGetLatestSanitizedFile(string directory, string pattern, string fileType)
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
                string sanitizedContent = SanitizeLogContent(content);

                return new
                {
                    file_type = fileType,
                    file_name = latest.Name,
                    last_write_time = latest.LastWriteTime.ToString("o"),
                    content = sanitizedContent
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

        private static string SanitizeLogContent(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return content;
            }

            string sanitized = content;
            sanitized = EmailRegex.Replace(sanitized, "[REDACTED_EMAIL]");
            sanitized = PhoneRegex.Replace(sanitized, "[REDACTED_PHONE]");
            sanitized = IPv4Regex.Replace(sanitized, "[REDACTED_IP]");
            sanitized = WindowsPathRegex.Replace(sanitized, "[REDACTED_PATH]");
            sanitized = UncPathRegex.Replace(sanitized, "[REDACTED_PATH]");
            sanitized = UrlSecretRegex.Replace(sanitized, "$1[REDACTED]");
            sanitized = KeyValueSecretRegex.Replace(sanitized, "$1[REDACTED]");
            sanitized = JsonSecretRegex.Replace(sanitized, "$1[REDACTED]$3");
            return sanitized;
        }
    }
}
