using Ink_Canvas.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// Dlass笔记自动上传辅助类
    /// </summary>
    public class DlassNoteUploader
    {
        private const string APP_ID = "app_WkjocWqsrVY7T6zQV2CfiA";
        private const string APP_SECRET = "o7dx5b5ASGUMcM72PCpmRQYAhSijqaOVHoGyBK0IxbA";
        private const int BATCH_SIZE = 10; // 批量上传大小

        /// <summary>
        /// 上传队列（线程安全）
        /// </summary>
        private static readonly ConcurrentQueue<string> _uploadQueue = new ConcurrentQueue<string>();

        /// <summary>
        /// 队列处理锁，防止并发处理
        /// </summary>
        private static readonly SemaphoreSlim _queueProcessingLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 上传笔记响应模型
        /// </summary>
        public class UploadNoteResponse
        {
            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; }

            [JsonProperty("note_id")]
            public int? NoteId { get; set; }

            [JsonProperty("filename")]
            public string Filename { get; set; }

            [JsonProperty("file_path")]
            public string FilePath { get; set; }

            [JsonProperty("file_url")]
            public string FileUrl { get; set; }
        }

        /// <summary>
        /// 白板信息模型（用于查找白板）
        /// </summary>
        private class WhiteboardInfo
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("board_id")]
            public string BoardId { get; set; }

            [JsonProperty("secret_key")]
            public string SecretKey { get; set; }

            [JsonProperty("class_name")]
            public string ClassName { get; set; }

            [JsonProperty("class_id")]
            public int ClassId { get; set; }
        }

        /// <summary>
        /// 认证响应模型
        /// </summary>
        private class AuthWithTokenResponse
        {
            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("whiteboards")]
            public List<WhiteboardInfo> Whiteboards { get; set; }
        }

        /// <summary>
        /// 异步上传笔记文件到Dlass（支持PNG和ICSTK格式）
        /// </summary>
        /// <param name="filePath">文件路径（支持PNG和ICSTK）</param>
        /// <returns>是否成功加入队列（不等待实际上传完成）</returns>
        public static async Task<bool> UploadNoteFileAsync(string filePath)
        {
            try
            {
                // 检查是否启用自动上传
                if (MainWindow.Settings?.Dlass?.IsAutoUploadNotes != true)
                {
                    return false;
                }

                // 基本验证
                if (!File.Exists(filePath))
                {
                    LogHelper.WriteLogToFile($"上传失败：文件不存在 - {filePath}", LogHelper.LogType.Error);
                    return false;
                }

                var fileExtension = Path.GetExtension(filePath).ToLower();
                if (fileExtension != ".png" && fileExtension != ".icstk")
                {
                    return false;
                }

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 10 * 1024 * 1024)
                {
                    LogHelper.WriteLogToFile($"上传失败：文件过大（{fileInfo.Length / 1024 / 1024}MB），超过10MB限制", LogHelper.LogType.Error);
                    return false;
                }

                // 获取上传延迟时间（分钟）
                var delayMinutes = MainWindow.Settings?.Dlass?.AutoUploadDelayMinutes ?? 0;

                // 如果设置了延迟时间，在后台任务中等待后再加入队列
                if (delayMinutes > 0)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromMinutes(delayMinutes));
                        EnqueueFile(filePath);
                    });
                }
                else
                {
                    EnqueueFile(filePath);
                }

                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加入上传队列时出错: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 将文件加入上传队列
        /// </summary>
        private static void EnqueueFile(string filePath)
        {
            _uploadQueue.Enqueue(filePath);

            // 如果队列达到批量大小，触发批量上传
            if (_uploadQueue.Count >= BATCH_SIZE)
            {
                _ = ProcessUploadQueueAsync();
            }
        }

        /// <summary>
        /// 处理上传队列，批量上传文件
        /// </summary>
        private static async Task ProcessUploadQueueAsync()
        {
            // 使用信号量防止并发处理
            if (!await _queueProcessingLock.WaitAsync(0))
            {
                return; // 已有处理任务在运行
            }

            try
            {
                var filesToUpload = new List<string>();

                // 从队列中取出最多BATCH_SIZE个文件
                while (filesToUpload.Count < BATCH_SIZE && _uploadQueue.TryDequeue(out string filePath))
                {
                    // 再次检查文件是否存在（可能在队列中时被删除）
                    if (File.Exists(filePath))
                    {
                        filesToUpload.Add(filePath);
                    }
                }

                if (filesToUpload.Count == 0)
                {
                    return;
                }

                // 获取共享的白板信息（同一批次的所有文件共享认证信息）
                WhiteboardInfo sharedWhiteboard = null;
                string apiBaseUrl = null;
                string userToken = null;
                
                try
                {
                    var selectedClassName = MainWindow.Settings?.Dlass?.SelectedClassName;
                    if (string.IsNullOrEmpty(selectedClassName))
                    {
                        LogHelper.WriteLogToFile("上传失败：未选择班级", LogHelper.LogType.Error);
                        return;
                    }

                    userToken = MainWindow.Settings?.Dlass?.UserToken;
                    if (string.IsNullOrEmpty(userToken))
                    {
                        LogHelper.WriteLogToFile("上传失败：未设置用户Token", LogHelper.LogType.Error);
                        return;
                    }

                    apiBaseUrl = MainWindow.Settings?.Dlass?.ApiBaseUrl ?? "https://dlass.tech";

                    // 获取白板信息（只获取一次，所有文件共享）
                    using (var apiClient = new DlassApiClient(APP_ID, APP_SECRET, apiBaseUrl, userToken))
                    {
                        var authData = new
                        {
                            app_id = APP_ID,
                            app_secret = APP_SECRET,
                            user_token = userToken
                        };

                        var authResult = await apiClient.PostAsync<AuthWithTokenResponse>("/api/whiteboard/framework/auth-with-token", authData, requireAuth: false);

                        if (authResult == null || !authResult.Success || authResult.Whiteboards == null)
                        {
                            LogHelper.WriteLogToFile("上传失败：无法获取白板信息", LogHelper.LogType.Error);
                            return;
                        }

                        sharedWhiteboard = authResult.Whiteboards
                            .FirstOrDefault(w => !string.IsNullOrEmpty(w.ClassName) && w.ClassName == selectedClassName);

                        if (sharedWhiteboard == null || string.IsNullOrEmpty(sharedWhiteboard.BoardId) || string.IsNullOrEmpty(sharedWhiteboard.SecretKey))
                        {
                            LogHelper.WriteLogToFile($"上传失败：未找到班级'{selectedClassName}'对应的白板", LogHelper.LogType.Error);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"批量上传获取白板信息时出错: {ex.Message}", LogHelper.LogType.Error);
                    return;
                }

                // 并发上传所有文件（共享白板信息）
                var uploadTasks = filesToUpload.Select(filePath => UploadFileInternalAsync(filePath, sharedWhiteboard, apiBaseUrl, userToken));
                await Task.WhenAll(uploadTasks);

                // 如果队列中还有文件，继续处理
                if (_uploadQueue.Count >= BATCH_SIZE)
                {
                    _ = ProcessUploadQueueAsync();
                }
            }
            finally
            {
                _queueProcessingLock.Release();
            }
        }

        /// <summary>
        /// 内部上传方法，执行实际上传操作
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="whiteboard">白板信息（如果为null则重新获取）</param>
        /// <param name="apiBaseUrl">API基础URL（如果为null则从设置获取）</param>
        /// <param name="userToken">用户Token（如果为null则从设置获取）</param>
        private static async Task<bool> UploadFileInternalAsync(string filePath, WhiteboardInfo whiteboard = null, string apiBaseUrl = null, string userToken = null)
        {
            try
            {
                // 再次检查文件是否存在（可能在队列等待时被删除）
                if (!File.Exists(filePath))
                {
                    return false;
                }

                // 检查文件扩展名
                var fileExtension = Path.GetExtension(filePath).ToLower();
                if (fileExtension != ".png" && fileExtension != ".icstk")
                {
                    return false;
                }

                // 检查文件大小（最大10MB）
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 10 * 1024 * 1024)
                {
                    LogHelper.WriteLogToFile($"上传失败：文件过大（{fileInfo.Length / 1024 / 1024}MB），超过10MB限制", LogHelper.LogType.Error);
                    return false;
                }

                // 如果白板信息未提供，则重新获取
                if (whiteboard == null)
                {
                    var selectedClassName = MainWindow.Settings?.Dlass?.SelectedClassName;
                    if (string.IsNullOrEmpty(selectedClassName))
                    {
                        LogHelper.WriteLogToFile("上传失败：未选择班级", LogHelper.LogType.Error);
                        return false;
                    }

                    userToken = userToken ?? MainWindow.Settings?.Dlass?.UserToken;
                    if (string.IsNullOrEmpty(userToken))
                    {
                        LogHelper.WriteLogToFile("上传失败：未设置用户Token", LogHelper.LogType.Error);
                        return false;
                    }

                    apiBaseUrl = apiBaseUrl ?? MainWindow.Settings?.Dlass?.ApiBaseUrl ?? "https://dlass.tech";

                    // 创建API客户端并获取白板信息
                    using (var apiClient = new DlassApiClient(APP_ID, APP_SECRET, apiBaseUrl, userToken))
                    {
                        var authData = new
                        {
                            app_id = APP_ID,
                            app_secret = APP_SECRET,
                            user_token = userToken
                        };

                        var authResult = await apiClient.PostAsync<AuthWithTokenResponse>("/api/whiteboard/framework/auth-with-token", authData, requireAuth: false);

                        if (authResult == null || !authResult.Success || authResult.Whiteboards == null)
                        {
                            LogHelper.WriteLogToFile("上传失败：无法获取白板信息", LogHelper.LogType.Error);
                            return false;
                        }

                        // 查找匹配班级的白板
                        whiteboard = authResult.Whiteboards
                            .FirstOrDefault(w => !string.IsNullOrEmpty(w.ClassName) && w.ClassName == selectedClassName);

                        if (whiteboard == null || string.IsNullOrEmpty(whiteboard.BoardId) || string.IsNullOrEmpty(whiteboard.SecretKey))
                        {
                            LogHelper.WriteLogToFile($"上传失败：未找到班级'{selectedClassName}'对应的白板", LogHelper.LogType.Error);
                            return false;
                        }
                    }
                }

                // 获取API基础URL和用户Token（如果未提供）
                apiBaseUrl = apiBaseUrl ?? MainWindow.Settings?.Dlass?.ApiBaseUrl ?? "https://dlass.tech";
                userToken = userToken ?? MainWindow.Settings?.Dlass?.UserToken;

                // 准备上传参数
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var title = fileName;
                var fileType = fileExtension == ".icstk" ? "墨迹文件" : "笔记";
                var description = $"自动上传的{fileType} - {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                var tags = fileExtension == ".icstk" ? "自动上传,墨迹,icstk" : "自动上传,笔记,png";

                // 创建API客户端并上传文件
                using (var apiClient = new DlassApiClient(APP_ID, APP_SECRET, apiBaseUrl, userToken))
                {
                    var uploadResult = await apiClient.UploadNoteAsync<UploadNoteResponse>(
                        "/api/whiteboard/upload_note",
                        filePath,
                        whiteboard.BoardId,
                        whiteboard.SecretKey,
                        title,
                        description,
                        tags);

                    if (uploadResult != null && uploadResult.Success)
                    {
                        LogHelper.WriteLogToFile($"笔记上传成功：{fileName} -> {uploadResult.FileUrl}", LogHelper.LogType.Event);
                        return true;
                    }
                    else
                    {
                        LogHelper.WriteLogToFile($"上传失败：服务器响应失败 - {uploadResult?.Message ?? "未知错误"}", LogHelper.LogType.Error);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"上传笔记时出错: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }
    }
}

