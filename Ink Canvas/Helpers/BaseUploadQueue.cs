using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 上传队列项数据（用于序列化）
    /// </summary>
    public class UploadQueueItemData
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
    public class UploadQueueItem
    {
        public string FilePath { get; set; }
        public int RetryCount { get; set; }
    }

    /// <summary>
    /// 通用上传队列基类
    /// </summary>
    public abstract class BaseUploadQueue : IDisposable
    {
        protected const int BATCH_SIZE = 10; // 批量上传大小
        protected const int MAX_RETRY_COUNT = 3; // 最大重试次数

        /// <summary>
        /// 上传队列
        /// </summary>
        protected readonly ConcurrentQueue<UploadQueueItem> _uploadQueue = new ConcurrentQueue<UploadQueueItem>();

        /// <summary>
        /// 队列处理锁，防止并发处理
        /// </summary>
        protected readonly SemaphoreSlim _queueProcessingLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 队列保存锁，防止并发保存
        /// </summary>
        protected readonly SemaphoreSlim _queueSaveLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 是否已初始化队列
        /// </summary>
        protected bool _isQueueInitialized = false;

        /// <summary>
        /// 是否已释放资源
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// 队列文件名
        /// </summary>
        protected abstract string QueueFileName { get; }

        /// <summary>
        /// 允许的文件扩展名
        /// </summary>
        protected virtual HashSet<string> AllowedExtensions => new HashSet<string> { ".png", ".icstk", ".xml", ".zip" };

        /// <summary>
        /// 获取队列文件路径
        /// </summary>
        protected string GetQueueFilePath()
        {
            var configsDir = Path.Combine(App.RootPath, "Configs");
            if (!Directory.Exists(configsDir))
            {
                Directory.CreateDirectory(configsDir);
            }
            return Path.Combine(configsDir, QueueFileName);
        }

        /// <summary>
        /// 获取最大文件大小
        /// </summary>
        /// <param name="extension">文件扩展名</param>
        /// <returns>最大文件大小（字节）</returns>
        protected virtual long GetMaxFileSize(string extension)
        {
            return extension == ".zip" ? 50 * 1024 * 1024 : 10 * 1024 * 1024;
        }

        /// <summary>
        /// 初始化上传队列
        /// </summary>
        public void InitializeQueue()
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
                    if (!IsValidFile(item.FilePath))
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
                    LogHelper.WriteLogToFile($"[{GetType().Name}] 已恢复上传队列：{restoredCount}个文件，跳过{skippedCount}个无效文件", LogHelper.LogType.Event);
                    // 如果恢复了队列，触发处理
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessUploadQueueAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"[{GetType().Name}] 恢复上传队列后处理时出错: {ex}", LogHelper.LogType.Error);
                        }
                    });
                }
                else if (skippedCount > 0)
                {
                    LogHelper.WriteLogToFile($"[{GetType().Name}] 队列恢复完成：跳过{skippedCount}个无效文件", LogHelper.LogType.Event);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[{GetType().Name}] 恢复上传队列时出错: {ex.Message}", LogHelper.LogType.Error);
                _isQueueInitialized = true; // 即使出错也标记为已初始化，避免重复尝试
            }
        }

        /// <summary>
        /// 保存队列到文件
        /// </summary>
        protected async Task SaveQueueToFileAsync(CancellationToken cancellationToken = default)
        {
            if (!await _queueSaveLock.WaitAsync(1000, cancellationToken)) // 最多等待1秒
            {
                return; // 如果无法获取锁，跳过保存（避免阻塞）
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var queueData = new List<UploadQueueItemData>();

                // 将队列转换为可序列化的格式
                foreach (var item in _uploadQueue)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
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

                // 使用进程保护的写入门控，避免安全面板中"进程文件保护"占用导致无法写入
                var tempFilePath = queueFilePath + ".tmp";
                ProcessProtectionManager.WithWriteAccess(queueFilePath, () =>
                {
                    File.WriteAllText(tempFilePath, jsonContent);
                    if (File.Exists(queueFilePath))
                        File.Delete(queueFilePath);
                    File.Move(tempFilePath, queueFilePath);
                });
            }
            catch (OperationCanceledException)
            {
                // 取消操作，静默处理
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[{GetType().Name}] 保存上传队列时出错: {ex.Message}", LogHelper.LogType.Error);
            }
            finally
            {
                _queueSaveLock.Release();
            }
        }

        /// <summary>
        /// 清空队列文件
        /// </summary>
        protected void ClearQueueFile()
        {
            try
            {
                var queueFilePath = GetQueueFilePath();
                ProcessProtectionManager.WithWriteAccess(queueFilePath, () =>
                {
                    if (File.Exists(queueFilePath))
                        File.WriteAllText(queueFilePath, "[]");
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[{GetType().Name}] 清空队列文件时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 将文件加入上传队列
        /// </summary>
        protected void EnqueueFile(string filePath, int retryCount = 0, CancellationToken cancellationToken = default)
        {
            _uploadQueue.Enqueue(new UploadQueueItem
            {
                FilePath = filePath,
                RetryCount = retryCount
            });

            // 异步保存队列到文件
            _ = Task.Run(async () =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await SaveQueueToFileAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // 取消操作，静默处理
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"[{GetType().Name}] 保存上传队列时出错（后台任务）: {ex}", LogHelper.LogType.Error);
                }
            }, cancellationToken);

            // 触发队列处理
            _ = ProcessUploadQueueAsync(cancellationToken);
        }

        /// <summary>
        /// 处理上传队列，批量上传文件
        /// </summary>
        protected async Task ProcessUploadQueueAsync(CancellationToken cancellationToken = default)
        {
            // 使用信号量防止并发处理
            if (!await _queueProcessingLock.WaitAsync(0, cancellationToken))
            {
                return; // 已有处理任务在运行
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var filesToUpload = new List<UploadQueueItem>();

                // 从队列中取出最多BATCH_SIZE个文件
                int count = 0;
                while (count < BATCH_SIZE && _uploadQueue.TryDequeue(out UploadQueueItem item))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 再次检查文件是否存在
                    if (File.Exists(item.FilePath) && IsValidFile(item.FilePath))
                    {
                        filesToUpload.Add(item);
                        count++;
                    }
                }

                if (filesToUpload.Count == 0)
                {
                    return;
                }

                // 检查是否启用
                if (!IsUploadEnabled())
                {
                    LogHelper.WriteLogToFile($"[{GetType().Name}] 上传失败：上传未启用", LogHelper.LogType.Error);
                    // 将文件重新加入队列
                    foreach (var item in filesToUpload)
                    {
                        EnqueueFile(item.FilePath, item.RetryCount, cancellationToken);
                    }
                    return;
                }

                // 并发上传所有文件，并处理失败重试
                var uploadTasks = filesToUpload.Select(async item =>
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var success = await UploadFileInternalAsync(item.FilePath, cancellationToken);
                        if (!success)
                        {
                            // 检查是否是可重试的错误
                            if (IsRetryableError(item.FilePath))
                            {
                                // 检查重试次数
                                if (item.RetryCount < MAX_RETRY_COUNT)
                                {
                                    LogHelper.WriteLogToFile($"[{GetType().Name}] 上传失败，将重试 ({item.RetryCount + 1}/{MAX_RETRY_COUNT}): {Path.GetFileName(item.FilePath)}", LogHelper.LogType.Event);
                                    EnqueueFile(item.FilePath, item.RetryCount + 1, cancellationToken);
                                }
                                else
                                {
                                    LogHelper.WriteLogToFile($"[{GetType().Name}] 上传失败，已达到最大重试次数: {Path.GetFileName(item.FilePath)}", LogHelper.LogType.Error);
                                }
                            }
                        }
                        return success;
                    }
                    catch (OperationCanceledException)
                    {
                        // 取消操作，将文件重新加入队列
                        EnqueueFile(item.FilePath, item.RetryCount, cancellationToken);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // 检查是否是可重试的错误（超时、网络错误等）
                        var errorMessage = ex.Message.ToLower();
                        bool isRetryable = errorMessage.Contains("超时") ||
                                          errorMessage.Contains("timeout") ||
                                          errorMessage.Contains("网络错误") ||
                                          errorMessage.Contains("network") ||
                                          errorMessage.Contains("408") || // 请求超时
                                          errorMessage.Contains("423") || // 资源锁定
                                          errorMessage.Contains("429") || // 请求过多
                                          errorMessage.Contains("500") || // 服务器错误
                                          errorMessage.Contains("502") || // 网关错误
                                          errorMessage.Contains("503") || // 服务不可用
                                          errorMessage.Contains("504"); // 网关超时

                        if (isRetryable && IsRetryableError(item.FilePath))
                        {
                            // 检查重试次数
                            if (item.RetryCount < MAX_RETRY_COUNT)
                            {
                                LogHelper.WriteLogToFile($"[{GetType().Name}] 上传失败({ex.Message})，将重试 ({item.RetryCount + 1}/{MAX_RETRY_COUNT}): {Path.GetFileName(item.FilePath)}", LogHelper.LogType.Error);
                                EnqueueFile(item.FilePath, item.RetryCount + 1, cancellationToken);
                            }
                            else
                            {
                                LogHelper.WriteLogToFile($"[{GetType().Name}] 上传失败，已达到最大重试次数: {Path.GetFileName(item.FilePath)}", LogHelper.LogType.Error);
                            }
                        }
                        else
                        {
                            LogHelper.WriteLogToFile($"[{GetType().Name}] 上传失败（不可重试）: {Path.GetFileName(item.FilePath)} - {ex.Message}", LogHelper.LogType.Error);
                        }
                        return false;
                    }
                });
                await Task.WhenAll(uploadTasks).ConfigureAwait(false);

                // 上传完成后保存队列状态
                await SaveQueueToFileAsync(cancellationToken).ConfigureAwait(false);

                // 检查队列中是否还有文件，如果有就继续处理
                if (_uploadQueue.Count > 0)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessUploadQueueAsync(cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"[{GetType().Name}] 继续处理上传队列时出错: {ex}", LogHelper.LogType.Error);
                        }
                    }, cancellationToken);
                }
            }
            finally
            {
                _queueProcessingLock.Release();
            }
        }

        /// <summary>
        /// 验证文件是否有效
        /// </summary>
        protected virtual bool IsValidFile(string filePath)
        {
            try
            {
                var fileExtension = Path.GetExtension(filePath).ToLower();
                if (!AllowedExtensions.Contains(fileExtension))
                {
                    return false;
                }

                var fileInfo = new FileInfo(filePath);
                long maxSize = GetMaxFileSize(fileExtension);
                if (fileInfo.Length > maxSize)
                {
                    LogHelper.WriteLogToFile($"[{GetType().Name}] 上传失败：文件过大（{fileInfo.Length / 1024 / 1024:F2}MB），超过{maxSize / 1024 / 1024}MB限制", LogHelper.LogType.Error);
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 判断错误是否可重试
        /// </summary>
        protected bool IsRetryableError(string filePath)
        {
            // 检查文件是否存在
            if (!File.Exists(filePath))
            {
                return false; // 文件不存在，不可重试
            }

            // 检查文件是否有效
            if (!IsValidFile(filePath))
            {
                return false; // 文件无效，不可重试
            }

            // 检查是否启用
            if (!IsUploadEnabled())
            {
                return false; // 上传未启用，不可重试
            }

            // 其他错误（超时、网络错误等）可以重试
            return true;
        }

        /// <summary>
        /// 检查上传是否启用
        /// </summary>
        protected abstract bool IsUploadEnabled();

        /// <summary>
        /// 内部上传方法，执行实际上传操作
        /// </summary>
        protected abstract Task<bool> UploadFileInternalAsync(string filePath, CancellationToken cancellationToken);

        /// <summary>
        /// 异步上传文件
        /// </summary>
        public async Task<bool> UploadFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 检查是否启用
                if (!IsUploadEnabled())
                {
                    return false;
                }

                // 基本验证
                if (!File.Exists(filePath))
                {
                    LogHelper.WriteLogToFile($"[{GetType().Name}] 上传失败：文件不存在 - {filePath}", LogHelper.LogType.Error);
                    return false;
                }

                if (!IsValidFile(filePath))
                {
                    return false;
                }

                // 确保队列已初始化
                if (!_isQueueInitialized)
                {
                    InitializeQueue();
                }

                // 加入队列
                EnqueueFile(filePath, 0, cancellationToken);

                return true;
            }
            catch (OperationCanceledException)
            {
                LogHelper.WriteLogToFile($"[{GetType().Name}] 上传被取消: {Path.GetFileName(filePath)}", LogHelper.LogType.Event);
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[{GetType().Name}] 加入上传队列时出错: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否手动释放</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _queueProcessingLock?.Dispose();
                    _queueSaveLock?.Dispose();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~BaseUploadQueue()
        {
            Dispose(false);
        }
    }
}