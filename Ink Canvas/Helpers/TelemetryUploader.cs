using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Sentry;
using Sentry.Protocol;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 遥测上传：根据用户设置，通过 Sentry 上传 usage_stats 和 Crashes 目录的摘要信息。
    /// </summary>
    internal static class TelemetryUploader
    {
        /// <summary>
        /// 根据当前设置决定是否上传遥测数据。
        /// 在主窗口加载完成后调用一次即可。
        /// </summary>
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
                        return; // 用户未开启
                    }

                    // 获取并校验设备ID
                    string deviceId = DeviceIdentifier.GetDeviceId();
                    if (string.IsNullOrWhiteSpace(deviceId) || deviceId.Length < 5)
                    {
                        LogHelper.WriteLogToFile("TelemetryUploader | 设备ID无效，取消遥测上传", LogHelper.LogType.Warning);
                        return;
                    }

                    // 可选：Crashes 目录下的崩溃日志
                    List<object> crashFiles = null;
                    if (level == TelemetryUploadLevel.Extended)
                    {
                        try
                        {
                            string crashDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Crashes");
                            if (Directory.Exists(crashDir))
                            {
                                crashFiles = new List<object>();
                                var files = Directory.GetFiles(crashDir);

                                FileInfo latestInfo = null;
                                string latestContent = null;

                                foreach (var file in files)
                                {
                                    try
                                    {
                                        var info = new FileInfo(file);

                                        // 避免一次上传过大，单文件限制为 8192KB
                                        if (info.Length > 8192 * 1024)
                                        {
                                            continue;
                                        }

                                        string content = File.ReadAllText(file);

                                        if (latestInfo == null || info.LastWriteTime > latestInfo.LastWriteTime)
                                        {
                                            latestInfo = info;
                                            latestContent = content;
                                        }
                                    }
                                    catch (Exception exFile)
                                    {
                                        LogHelper.WriteLogToFile(
                                            $"TelemetryUploader | 读取崩溃日志失败: {exFile.Message}",
                                            LogHelper.LogType.Warning);
                                    }
                                }

                                if (latestInfo != null && latestContent != null)
                                {
                                    crashFiles.Add(new
                                    {
                                        file_name = latestInfo.Name,
                                        content = latestContent
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile(
                                $"TelemetryUploader | 收集崩溃日志失败: {ex.Message}",
                                LogHelper.LogType.Warning);
                        }
                    }

                    var telemetryData = new
                    {
                        telemetry_level = level.ToString(),
                        device_id = deviceId,
                        update_channel = settings.Startup.UpdateChannel.ToString(),
                        app_version = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                        os_version = Environment.OSVersion.VersionString,
                        crash_files = crashFiles
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

                    if (crashFiles != null)
                    {
                        evt.SetExtra("crash_files", crashFiles);
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
    }
}


