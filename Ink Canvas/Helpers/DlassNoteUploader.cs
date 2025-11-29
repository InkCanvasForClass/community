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
        private const int MAX_RETRY_COUNT = 3; // 最大重试次数
        private const string QUEUE_FILE_NAME = "DlassUploadQueue.json"; 

        /// <summary>
        /// 上传队列项
        /// </summary>
        private class UploadQueueItemData
        {
            [JsonProperty("file_path")]
            public string FilePath { get; set; }

            [JsonProperty("retry_count")]
            public int RetryCount { get; set; }

            [JsonProperty("added_time")]
            public DateTime AddedTime { get; set; }
        }

        /// <summary>
        /// 上传队列项
        /// </summary>
        private class UploadQueueItem
        {
            public string FilePath { get; set; }
            public int RetryCount { get; set; }
        }

        /// <summary>
        /// 上传队列
        /// </summary>
        private static readonly ConcurrentQueue<UploadQueueItem> _uploadQueue = new ConcurrentQueue<UploadQueueItem>();

        /// <summary>
        /// 队列处理锁，防止并发处理
        /// </summary>
        private static readonly SemaphoreSlim _queueProcessingLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 队列保存锁，防止并发保存
        /// </summary>
        private static readonly SemaphoreSlim _queueSaveLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 是否已初始化队列
        /// </summary>
        private static bool _isQueueInitialized = false;

        /// <summary>
        /// 获取队列文件路径
        /// </summary>
        private static string GetQueueFilePath()
        {
            var configsDir = Path.Combine(App.RootPath, "Configs");
            if (!Directory.Exists(configsDir))
            {
                Directory.CreateDirectory(configsDir);
            }
            return Path.Combine(configsDir, QUEUE_FILE_NAME);
        }

        /// <summary>
        /// 初始化上传队列
        /// </summary>
        public static void InitializeQueue()
        {
            if (_isQueueInitialized)
            {
                return;
            }

            try
            {
                var queueFilePath = GetQueueFilePath();
                if (!File.Exists(queueFilePath))
                {
                    _isQueueInitialized = true;
                    return;
                }

                var jsonContent = File.ReadAllText(queueFilePath);
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    _isQueueInitialized = true;
                    return;
                }

                var queueData = JsonConvert.DeserializeObject<List<UploadQueueItemData>>(jsonContent);
                if (queueData == null || queueData.Count == 0)
                {
                    _isQueueInitialized = true;
                    return;
                }

                int restoredCount = 0;
                int skippedCount = 0;

                foreach (var item in queueData)
                {
                    // 验证文件是否存在
                    if (!File.Exists(item.FilePath))
                    {
                        skippedCount++;
                        continue;
                    }

                    // 验证文件格式和大小
                    var fileExtension = Path.GetExtension(item.FilePath).ToLower();
                    if (fileExtension != ".png" && fileExtension != ".icstk" && fileExtension != ".zip")
                    {
                        skippedCount++;
                        continue;
                    }

                    try
                    {
                        var fileInfo = new FileInfo(item.FilePath);
                        long maxSize = fileExtension == ".zip" ? 50 * 1024 * 1024 : 10 * 1024 * 1024;
                        if (fileInfo.Length > maxSize)
                        {
                            skippedCount++;
                            continue;
                        }
                    }
                    catch
                    {
                        skippedCount++;
                        continue;
                    }

                    // 恢复队列项
                    _uploadQueue.Enqueue(new UploadQueueItem
                    {
                        FilePath = item.FilePath,
                        RetryCount = item.RetryCount
                    });
                    restoredCount++;
                }

                _isQueueInitialized = true;

                if (restoredCount > 0)
                {
                    LogHelper.WriteLogToFile($"已恢复上传队列：{restoredCount}个文件，跳过{skippedCount}个无效文件", LogHelper.LogType.Event);
                    // 如果恢复了队列，触发处理
                    _ = ProcessUploadQueueAsync();
                }
                else if (skippedCount > 0)
                {
                    LogHelper.WriteLogToFile($"队列恢复完成：跳过{skippedCount}个无效文件", LogHelper.LogType.Event);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"恢复上传队列时出错: {ex.Message}", LogHelper.LogType.Error);
                _isQueueInitialized = true; // 即使出错也标记为已初始化，避免重复尝试
            }
        }

        /// <summary>
        /// 保存队列到文件
        /// </summary>
        private static async Task SaveQueueToFileAsync()
        {
            if (!await _queueSaveLock.WaitAsync(1000)) // 最多等待1秒
            {
                return; // 如果无法获取锁，跳过保存（避免阻塞）
            }

            try
            {
                var queueData = new List<UploadQueueItemData>();
                
                // 将队列转换为可序列化的格式
                foreach (var item in _uploadQueue)
                {
                    queueData.Add(new UploadQueueItemData
                    {
                        FilePath = item.FilePath,
                        RetryCount = item.RetryCount,
                        AddedTime = DateTime.Now
                    });
                }

                var queueFilePath = GetQueueFilePath();
                
                // 如果队列为空，清空文件
                if (queueData.Count == 0)
                {
                    ClearQueueFile();
                    return;
                }

                var jsonContent = JsonConvert.SerializeObject(queueData, Formatting.Indented);
                
                // 使用临时文件写入，然后替换，确保原子性
                var tempFilePath = queueFilePath + ".tmp";
                File.WriteAllText(tempFilePath, jsonContent);
                
                // 如果原文件存在，先删除
                if (File.Exists(queueFilePath))
                {
                    File.Delete(queueFilePath);
                }
                
                // 重命名临时文件
                File.Move(tempFilePath, queueFilePath);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存上传队列时出错: {ex.Message}", LogHelper.LogType.Error);
            }
            finally
            {
                _queueSaveLock.Release();
            }
        }

        /// <summary>
        /// 清空队列文件
        /// </summary>
        private static void ClearQueueFile()
        {
            try
            {
                var queueFilePath = GetQueueFilePath();
                if (File.Exists(queueFilePath))
                {
                    File.WriteAllText(queueFilePath, "[]");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"清空队列文件时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

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
        /// 异步上传笔记文件到Dlass（支持PNG、ICSTK和ZIP格式）
        /// </summary>
        /// <param name="filePath">文件路径（支持PNG、ICSTK和ZIP）</param>
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
                if (fileExtension != ".png" && fileExtension != ".icstk" && fileExtension != ".zip")
                {
                    return false;
                }

                var fileInfo = new FileInfo(filePath);
                long maxSize = fileExtension == ".zip" ? 50 * 1024 * 1024 : 10 * 1024 * 1024;
                if (fileInfo.Length > maxSize)
                {
                    LogHelper.WriteLogToFile($"上传失败：文件过大（{fileInfo.Length / 1024 / 1024}MB），超过{maxSize / 1024 / 1024}MB限制", LogHelper.LogType.Error);
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
        private static void EnqueueFile(string filePath, int retryCount = 0)
        {
            _uploadQueue.Enqueue(new UploadQueueItem
            {
                FilePath = filePath,
                RetryCount = retryCount
            });

            // 异步保存队列到文件
            _ = Task.Run(async () => await SaveQueueToFileAsync());

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
                var filesToUpload = new List<UploadQueueItem>();

                // 从队列中取出最多BATCH_SIZE个文件
                while (filesToUpload.Count < BATCH_SIZE && _uploadQueue.TryDequeue(out UploadQueueItem item))
                {
                    // 再次检查文件是否存在
                    if (File.Exists(item.FilePath))
                    {
                        filesToUpload.Add(item);
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
                        // 将文件重新加入队列
                        foreach (var item in filesToUpload)
                        {
                            EnqueueFile(item.FilePath, item.RetryCount);
                        }
                        return;
                    }

                    userToken = MainWindow.Settings?.Dlass?.UserToken;
                    if (string.IsNullOrEmpty(userToken))
                    {
                        LogHelper.WriteLogToFile("上传失败：未设置用户Token", LogHelper.LogType.Error);
                        // 将文件重新加入队列
                        foreach (var item in filesToUpload)
                        {
                            EnqueueFile(item.FilePath, item.RetryCount);
                        }
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
                            // 将文件重新加入队列
                            foreach (var item in filesToUpload)
                            {
                                EnqueueFile(item.FilePath, item.RetryCount);
                            }
                            return;
                        }

                        sharedWhiteboard = authResult.Whiteboards
                            .FirstOrDefault(w => !string.IsNullOrEmpty(w.ClassName) && w.ClassName == selectedClassName);

                        if (sharedWhiteboard == null || string.IsNullOrEmpty(sharedWhiteboard.BoardId) || string.IsNullOrEmpty(sharedWhiteboard.SecretKey))
                        {
                            LogHelper.WriteLogToFile($"上传失败：未找到班级'{selectedClassName}'对应的白板", LogHelper.LogType.Error);
                            // 将文件重新加入队列
                            foreach (var item in filesToUpload)
                            {
                                EnqueueFile(item.FilePath, item.RetryCount);
                            }
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"批量上传获取白板信息时出错: {ex.Message}", LogHelper.LogType.Error);
                    // 将文件重新加入队列
                    foreach (var item in filesToUpload)
                    {
                        EnqueueFile(item.FilePath, item.RetryCount);
                    }
                    return;
                }

                // 并发上传所有文件（共享白板信息），并处理失败重试
                var uploadTasks = filesToUpload.Select(async item =>
                {
                    try
                    {
                        var success = await UploadFileInternalAsync(item.FilePath, sharedWhiteboard, apiBaseUrl, userToken);
                        if (!success)
                        {
                            // 检查是否是可重试的错误
                            if (IsRetryableError(item.FilePath))
                            {
                                // 检查重试次数
                                if (item.RetryCount < MAX_RETRY_COUNT)
                                {
                                    LogHelper.WriteLogToFile($"上传失败，将重试 ({item.RetryCount + 1}/{MAX_RETRY_COUNT}): {Path.GetFileName(item.FilePath)}", LogHelper.LogType.Event);
                                    EnqueueFile(item.FilePath, item.RetryCount + 1);
                                }
                                else
                                {
                                    LogHelper.WriteLogToFile($"上传失败，已达到最大重试次数: {Path.GetFileName(item.FilePath)}", LogHelper.LogType.Error);
                                }
                            }
                        }
                        return success;
                    }
                    catch (Exception ex)
                    {
                        // 检查是否是可重试的错误（超时、网络错误等）
                        var errorMessage = ex.Message.ToLower();
                        bool isRetryable = errorMessage.Contains("超时") || 
                                          errorMessage.Contains("timeout") ||
                                          errorMessage.Contains("网络错误") ||
                                          errorMessage.Contains("network");
                        
                        if (isRetryable && IsRetryableError(item.FilePath))
                        {
                            // 检查重试次数
                            if (item.RetryCount < MAX_RETRY_COUNT)
                            {
                                LogHelper.WriteLogToFile($"上传失败({ex.Message})，将重试 ({item.RetryCount + 1}/{MAX_RETRY_COUNT}): {Path.GetFileName(item.FilePath)}", LogHelper.LogType.Event);
                                EnqueueFile(item.FilePath, item.RetryCount + 1);
                            }
                            else
                            {
                                LogHelper.WriteLogToFile($"上传失败，已达到最大重试次数: {Path.GetFileName(item.FilePath)}", LogHelper.LogType.Error);
                            }
                        }
                        return false;
                    }
                });
                await Task.WhenAll(uploadTasks);

                // 上传完成后保存队列状态
                await SaveQueueToFileAsync();

                // 如果队列达到批量大小，继续处理
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
                if (fileExtension != ".png" && fileExtension != ".icstk" && fileExtension != ".zip")
                {
                    return false;
                }

                // 检查文件大小（最大10MB，ZIP文件可能更大，允许50MB）
                var fileInfo = new FileInfo(filePath);
                long maxSize = fileExtension == ".zip" ? 50 * 1024 * 1024 : 10 * 1024 * 1024;
                if (fileInfo.Length > maxSize)
                {
                    LogHelper.WriteLogToFile($"上传失败：文件过大（{fileInfo.Length / 1024 / 1024}MB），超过{maxSize / 1024 / 1024}MB限制", LogHelper.LogType.Error);
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
                string fileType;
                string tags;
                if (fileExtension == ".zip")
                {
                    fileType = "多页面墨迹压缩包";
                    tags = "自动上传,多页面,zip,压缩包";
                }
                else if (fileExtension == ".icstk")
                {
                    fileType = "墨迹文件";
                    tags = "自动上传,墨迹,icstk";
                }
                else
                {
                    fileType = "笔记";
                    tags = "自动上传,笔记,png";
                }
                var description = $"自动上传的{fileType} - {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

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
                // 记录错误信息，抛出异常以便调用方判断是否可重试
                LogHelper.WriteLogToFile($"上传笔记时出错: {ex.Message}", LogHelper.LogType.Error);
                throw;
            }
        }

        /// <summary>
        /// 判断错误是否可重试（超时、网络错误等）
        /// </summary>
        private static bool IsRetryableError(string filePath)
        {
            // 检查文件是否存在
            if (!File.Exists(filePath))
            {
                return false; // 文件不存在，不可重试
            }

            // 检查文件扩展名
            var fileExtension = Path.GetExtension(filePath).ToLower();
            if (fileExtension != ".png" && fileExtension != ".icstk" && fileExtension != ".zip")
            {
                return false; // 文件格式错误，不可重试
            }

            // 检查文件大小
            try
            {
                var fileInfo = new FileInfo(filePath);
                long maxSize = fileExtension == ".zip" ? 50 * 1024 * 1024 : 10 * 1024 * 1024;
                if (fileInfo.Length > maxSize)
                {
                    return false; // 文件过大，不可重试
                }
            }
            catch
            {
                return false; // 无法读取文件信息，不可重试
            }

            // 其他错误（超时、网络错误等）可以重试
            return true;
        }
    }
}


