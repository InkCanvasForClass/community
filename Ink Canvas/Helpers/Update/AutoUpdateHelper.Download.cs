using Ink_Canvas.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Helpers
{
    internal partial class AutoUpdateHelper
    {
        // 区块任务结构体（移到类体内）
        private class BlockTask
        {
            public int Index;
            public long Start;
            public long End;
            public int RetryCount;
        }

        // 使用指定线路组下载新版
        public static async Task<bool> DownloadSetupFile(string version, UpdateLineGroup group)
        {
            return await DownloadSetupFileWithFallback(version, new List<UpdateLineGroup> { group });
        }

        // 获取智教联盟真实下载地址
        private static async Task<string> GetZhijiaoRealDownloadUrl(string url)
        {
            try
            {
                using (var handler = new HttpClientHandler { AllowAutoRedirect = false })
                using (var client = new HttpClient(handler))
                {
                    client.Timeout = RequestTimeout;
                    var resp = await client.GetAsync(url);
                    // 优先取Location头
                    if (resp.StatusCode == HttpStatusCode.Found || resp.StatusCode == HttpStatusCode.Redirect || resp.StatusCode == HttpStatusCode.MovedPermanently)
                    {
                        if (resp.Headers.Location != null)
                        {
                            var realUrl = resp.Headers.Location.ToString();
                            if (realUrl.Contains(" ")) realUrl = realUrl.Replace(" ", "%20");
                            return realUrl;
                        }
                    }
                    // 有些服务器直接返回真实地址在内容里
                    var content = await resp.Content.ReadAsStringAsync();
                    if (Uri.IsWellFormedUriString(content.Trim(), UriKind.Absolute))
                    {
                        var realUrl = content.Trim();
                        if (realUrl.Contains(" ")) realUrl = realUrl.Replace(" ", "%20");
                        return realUrl;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 获取智教联盟真实下载地址失败: {ex.Message}", LogHelper.LogType.Error);
            }
            return null;
        }

        // 使用多线路组下载新版（支持自动切换）
        public static async Task<bool> DownloadSetupFileWithFallback(string version, List<UpdateLineGroup> groups, Action<double, string> progressCallback = null)
        {
            SetDownloadFailure(DownloadFailureReason.None);
            var session = BeginDownloadSession();
            try
            {
                version = NormalizeVersionForUpdate(version);
                statusFilePath = GetUpdateDownloadStatusFilePath(version);

                // 状态文件说"已下载"但 zip 不存在（用户/杀软清理过 Updates 目录），
                // 不能短路返回成功——必须重新下载，否则 InstallNewVersionApp 找不到文件。
                if (File.Exists(statusFilePath) && File.ReadAllText(statusFilePath).Trim().ToLower() == "true")
                {
                    var zipPath = GetLocalUpdateZipFilePath(version);
                    if (File.Exists(zipPath))
                    {
                        LogHelper.WriteLogToFile("AutoUpdate | 安装包已下载");
                        progressCallback?.Invoke(100, "已下载完成");
                        return true;
                    }
                    // zip 不存在则回退下载，并清理状态文件以避免下次再误判。
                    try { File.Delete(statusFilePath); } catch (Exception ex) { LogService.LogException(ex); }
                    LogHelper.WriteLogToFile("AutoUpdate | 状态文件存在但 zip 缺失，重新下载", LogHelper.LogType.Warning);
                }

                // 确保更新目录存在
                if (!Directory.Exists(updatesFolderPath))
                {
                    Directory.CreateDirectory(updatesFolderPath);
                    LogHelper.WriteLogToFile($"AutoUpdate | 创建更新目录: {updatesFolderPath}");
                }

                string zipFilePath = GetLocalUpdateZipFilePath(version);
                LogHelper.WriteLogToFile($"AutoUpdate | 目标文件路径: {zipFilePath}");

                SaveDownloadStatus(false);

                // 优先尝试"inkeys"线路组
                var inkeysGroup = groups.FirstOrDefault(g => g.GroupName == "inkeys");
                if (inkeysGroup != null)
                {
                    groups.Remove(inkeysGroup);
                    groups.Insert(0, inkeysGroup);
                    LogHelper.WriteLogToFile("AutoUpdate | 下载时优先尝试inkeys线路组");
                }

                // 依次尝试每个线路组
                CancellationToken groupLoopToken = session.Token;
                foreach (var group in groups)
                {
                    if (groupLoopToken.IsCancellationRequested)
                    {
                        LogHelper.WriteLogToFile("AutoUpdate | 用户已取消，停止尝试后续线路组");
                        break;
                    }

                    string url = group.DownloadUrlFormat != null && group.DownloadUrlFormat.Contains("{0}")
                        ? string.Format(group.DownloadUrlFormat, version)
                        : group.DownloadUrlFormat;
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 线路组 {group.GroupName} 缺少下载地址，跳过", LogHelper.LogType.Warning);
                        continue;
                    }
                    if (group.GroupName != StartupStrings.SmartUpdate)
                    {
                        url = AppendX64SuffixBeforeZipExtension(url);
                    }
                    // 智教联盟需要先获取真实下载地址
                    if (group.GroupName == "智教联盟")
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 获取智教联盟真实下载地址: {url}");
                        var realUrl = await GetZhijiaoRealDownloadUrl(url);
                        if (string.IsNullOrEmpty(realUrl))
                        {
                            LogHelper.WriteLogToFile("AutoUpdate | 智教联盟真实下载地址获取失败，跳过", LogHelper.LogType.Warning);
                            progressCallback?.Invoke(0, "智教联盟真实下载地址获取失败，跳过");
                            continue;
                        }
                        url = realUrl;
                        LogHelper.WriteLogToFile($"AutoUpdate | 智教联盟真实下载地址: {url}");
                    }
                    // inkeys线路组直接使用下载地址，无需特殊处理
                    else if (group.GroupName == "inkeys")
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 使用inkeys线路组下载地址: {url}");
                    }
                    LogHelper.WriteLogToFile($"AutoUpdate | 尝试从线路组 {group.GroupName} 下载: {url}");

                    bool downloadSuccess = await DownloadFile(url, zipFilePath, progressCallback, session.Token);

                    if (groupLoopToken.IsCancellationRequested)
                    {
                        LogHelper.WriteLogToFile("AutoUpdate | 用户已取消，停止尝试后续线路组");
                        break;
                    }

                    if (downloadSuccess)
                    {
                        SaveDownloadStatus(true);
                        LogHelper.WriteLogToFile($"AutoUpdate | 从线路组 {group.GroupName} 下载成功");
                        progressCallback?.Invoke(100, "下载完成");
                        return true;
                    }

                    LogHelper.WriteLogToFile($"AutoUpdate | 线路组 {group.GroupName} 下载失败，尝试下一个线路组", LogHelper.LogType.Warning);
                }

                LogHelper.WriteLogToFile("AutoUpdate | 所有线路组下载均失败", LogHelper.LogType.Error);
                progressCallback?.Invoke(0, "所有线路组下载均失败");
                if (LastDownloadFailure == DownloadFailureReason.None)
                    SetDownloadFailure(DownloadFailureReason.NetworkOrAllLinesFailed);
                return false;
            }
            catch (OperationCanceledException)
            {
                LogHelper.WriteLogToFile("AutoUpdate | 下载已被用户取消", LogHelper.LogType.Warning);
                SaveDownloadStatus(false);
                progressCallback?.Invoke(0, "下载已取消");
                SetDownloadFailure(DownloadFailureReason.Cancelled);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 下载更新时出错: {ex.Message}", LogHelper.LogType.Error);
                if (ex.InnerException != null)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 内部异常: {ex.InnerException.Message}", LogHelper.LogType.Error);
                }

                SaveDownloadStatus(false);
                progressCallback?.Invoke(0, $"下载异常: {ex.Message}");
                if (LastDownloadFailure == DownloadFailureReason.None)
                    SetDownloadFailure(DownloadFailureReason.NetworkOrAllLinesFailed);
                return false;
            }
            finally
            {
                EndDownloadSession(session);
            }
        }

        // 下载文件的具体实现
        public static async Task<bool> DownloadFile(string fileUrl, string destinationPath, Action<double, string> progressCallback = null, CancellationToken externalToken = default)
        {
            LogHelper.WriteLogToFile($"AutoUpdate | 正在尝试多线程下载: {fileUrl}");
            int maxRetry = 3;
            // 降低并发数，减少网络压力
            int[] threadOptions = { 32, 16, 8, 4, 1 };

            if (externalToken == default)
            {
                lock (_activeDownloadLock)
                {
                    // 兼容无显式令牌的调用：取任一进行中的会话令牌；无会话则不可取消
                    externalToken = _activeDownloadSessions.FirstOrDefault()?.Token ?? CancellationToken.None;
                }
            }

            // 检查服务器是否支持Range分块下载
            bool supportRange = false;
            long totalSize = -1;
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    var req = new HttpRequestMessage(HttpMethod.Head, fileUrl);
                    req.Headers.Range = new RangeHeaderValue(0, 0);
                    var resp = await client.SendAsync(req);
                    if (resp.StatusCode == HttpStatusCode.PartialContent)
                    {
                        supportRange = true;
                        if (resp.Content.Headers.ContentRange != null && resp.Content.Headers.ContentRange.Length.HasValue)
                        {
                            totalSize = resp.Content.Headers.ContentRange.Length.Value;
                        }
                        else if (resp.Content.Headers.ContentLength.HasValue)
                        {
                            totalSize = resp.Content.Headers.ContentLength.Value;
                        }
                    }
                    else if (resp.StatusCode == HttpStatusCode.OK)
                    {
                        supportRange = false;
                        if (resp.Content.Headers.ContentLength.HasValue)
                        {
                            totalSize = resp.Content.Headers.ContentLength.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 检查Range支持时异常: {ex.Message}", LogHelper.LogType.Warning);
            }

            if (!supportRange)
            {
                LogHelper.WriteLogToFile("AutoUpdate | 服务器不支持分块下载，自动降级为单线程下载");
                progressCallback?.Invoke(0, "服务器不支持分块下载，自动降级为单线程下载");
                return await DownloadSingleThread(fileUrl, destinationPath, totalSize, progressCallback, externalToken);
            }

            foreach (int threadCount in threadOptions)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 尝试使用 {threadCount} 线程下载");
                progressCallback?.Invoke(0, $"尝试使用 {threadCount} 线程下载");

                if (totalSize <= 0)
                {
                    totalSize = await GetContentLength(fileUrl);
                }
                if (totalSize <= 0)
                {
                    progressCallback?.Invoke(0, "无法获取文件大小，取消下载");
                    SetDownloadFailure(DownloadFailureReason.NetworkOrAllLinesFailed);
                    return false;
                }

                // 根据文件大小动态调整分块大小，避免分块过小
                int minBlockSize = 32 * 1024; // 最小32KB
                int blockSize = Math.Max(minBlockSize, (int)Math.Ceiling((double)totalSize / threadCount));
                int blockCount = (int)Math.Ceiling((double)totalSize / blockSize);

                LogHelper.WriteLogToFile($"AutoUpdate | 文件大小: {totalSize}, 分块数: {blockCount}, 分块大小: {blockSize}");

                var blockQueue = new ConcurrentQueue<BlockTask>();
                var finishedBlocks = new ConcurrentDictionary<int, bool>();
                long[] blockDownloaded = new long[blockCount];

                for (int i = 0; i < blockCount; i++)
                {
                    long start = i * blockSize;
                    long end = Math.Min(start + blockSize - 1, totalSize - 1);
                    blockQueue.Enqueue(new BlockTask { Index = i, Start = start, End = end, RetryCount = 0 });
                }

                CancellationTokenSource cts = new CancellationTokenSource();
                var tasks = new List<Task>();

                for (int t = 0; t < threadCount; t++)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        while (blockQueue.TryDequeue(out var block))
                        {
                            bool success = false;
                            string tempPath = destinationPath + $".part{block.Index}";

                            for (int retry = block.RetryCount; retry < maxRetry && !success; retry++)
                            {
                                try
                                {
                                    using (var client = new HttpClient())
                                    {
                                        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                                        var req = new HttpRequestMessage(HttpMethod.Get, fileUrl);
                                        req.Headers.Range = new RangeHeaderValue(block.Start, block.End);

                                        // 增加连接超时设置
                                        client.Timeout = TimeSpan.FromSeconds(30);

                                        var downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, externalToken);
                                        var lastReadTime = DateTime.UtcNow;
                                        bool dataReceived = false;

                                        using (var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, downloadCts.Token))
                                        {
                                            LogHelper.WriteLogToFile($"AutoUpdate | 分块{block.Index} 响应状态: {resp.StatusCode}");
                                            resp.EnsureSuccessStatusCode();
                                            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                                            {
                                                var stream = await resp.Content.ReadAsStreamAsync();
                                                byte[] buffer = new byte[8192];
                                                int read;
                                                long blockDownloadedBytes = 0;

                                                while (true)
                                                {
                                                    var readTask = stream.ReadAsync(buffer, 0, buffer.Length, downloadCts.Token);
                                                    var timeoutTask = Task.Delay(20000, downloadCts.Token); // 增加到20秒超时
                                                    var completed = await Task.WhenAny(readTask, timeoutTask);
                                                    if (completed == timeoutTask)
                                                    {
                                                        LogHelper.WriteLogToFile($"AutoUpdate | 分块{block.Index} 20秒无数据，线程超时重试", LogHelper.LogType.Warning);
                                                        progressCallback?.Invoke(0, $"分块{block.Index} 20秒无数据，线程超时重试");
                                                        downloadCts.Cancel();
                                                        break;
                                                    }
                                                    read = await readTask;
                                                    if (read <= 0) break;
                                                    await fs.WriteAsync(buffer, 0, read, downloadCts.Token);
                                                    blockDownloadedBytes += read;
                                                    blockDownloaded[block.Index] = blockDownloadedBytes;
                                                    lastReadTime = DateTime.UtcNow;
                                                    dataReceived = true;

                                                    // 合并所有块进度
                                                    long totalDownloaded = blockDownloaded.Sum();
                                                    double percent = (double)totalDownloaded / totalSize * 100;
                                                    progressCallback?.Invoke(percent, $"多线程下载中({threadCount}线程): {percent:F1}%");
                                                }
                                            }
                                        }

                                        if (!dataReceived)
                                        {
                                            throw new IOException("分块下载超时无数据");
                                        }

                                        // 验证分块大小是否正确
                                        var fileInfo = new FileInfo(tempPath);
                                        long expectedSize = block.End - block.Start + 1;
                                        if (fileInfo.Length != expectedSize)
                                        {
                                            LogHelper.WriteLogToFile($"AutoUpdate | 分块{block.Index}大小不匹配，期望:{expectedSize}，实际:{fileInfo.Length}", LogHelper.LogType.Warning);
                                            throw new IOException($"分块{block.Index}大小不匹配");
                                        }
                                    }
                                    success = true;
                                    LogHelper.WriteLogToFile($"AutoUpdate | 分块{block.Index}下载成功");
                                }
                                catch (Exception ex) when (ex is HttpRequestException || ex is IOException || ex is TaskCanceledException || ex is OperationCanceledException)
                                {
                                    // 用户主动取消：不再重试
                                    if (externalToken.IsCancellationRequested)
                                    {
                                        LogHelper.WriteLogToFile($"AutoUpdate | 分块{block.Index}下载已被用户取消", LogHelper.LogType.Warning);
                                        if (File.Exists(tempPath))
                                        {
                                            try { File.Delete(tempPath); } catch (Exception ex2) { LogService.LogException(ex2); }
                                        }
                                        cts.Cancel();
                                        return;
                                    }

                                    LogHelper.WriteLogToFile($"AutoUpdate | 分块{block.Index}下载失败，第{retry + 1}次: {ex.Message}", LogHelper.LogType.Warning);
                                    progressCallback?.Invoke(0, $"分块{block.Index}下载失败，第{retry + 1}次: {ex.Message}");

                                    // 清理可能损坏的分块文件
                                    if (File.Exists(tempPath))
                                    {
                                        try { File.Delete(tempPath); } catch (Exception innerEx) { System.Diagnostics.Debug.WriteLine(innerEx); }
                                    }

                                    // 增加重试间隔，避免频繁重试
                                    try { await Task.Delay(2000 * (retry + 1), externalToken); }
                                    catch (OperationCanceledException) { cts.Cancel(); return; }
                                }
                            }
                            if (success)
                            {
                                finishedBlocks[block.Index] = true;
                            }
                            else if (block.RetryCount + 1 < maxRetry)
                            {
                                // 失败但未超最大重试，重新入队
                                block.RetryCount++;
                                blockQueue.Enqueue(block);
                            }
                            else
                            {
                                // 超过最大重试，取消所有任务
                                LogHelper.WriteLogToFile($"AutoUpdate | 分块{block.Index}超过最大重试次数，取消下载", LogHelper.LogType.Error);
                                cts.Cancel();
                                break;
                            }
                        }
                    }));
                }

                await Task.WhenAll(tasks);

                if (cts.IsCancellationRequested || finishedBlocks.Count != blockCount)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | {threadCount}线程下载失败，完成分块数: {finishedBlocks.Count}/{blockCount}", LogHelper.LogType.Warning);
                    progressCallback?.Invoke(0, $"{threadCount}线程下载失败，完成分块数: {finishedBlocks.Count}/{blockCount}");

                    // 清理分块文件
                    for (int i = 0; i < blockCount; i++)
                    {
                        string tempPath = destinationPath + $".part{i}";
                        if (File.Exists(tempPath)) File.Delete(tempPath);
                    }

                    if (threadCount == threadOptions.Last())
                    {
                        // 已经是最后一次尝试，降级为单线程
                        LogHelper.WriteLogToFile("AutoUpdate | 所有多线程尝试失败，降级为单线程下载");
                        progressCallback?.Invoke(0, "所有多线程尝试失败，降级为单线程下载");
                        return await DownloadSingleThread(fileUrl, destinationPath, totalSize, progressCallback, externalToken);
                    }

                    LogHelper.WriteLogToFile($"AutoUpdate | {threadCount}线程下载失败，尝试降级为{threadOptions[Array.IndexOf(threadOptions, threadCount) + 1]}线程");
                    progressCallback?.Invoke(0, $"{threadCount}线程下载失败，尝试降级为{threadOptions[Array.IndexOf(threadOptions, threadCount) + 1]}线程");
                    continue;
                }

                // 合并所有块（带文件锁重试，避免杀软扫描或残留句柄导致占用失败）
                Exception mergeError = null;
                for (int mergeAttempt = 0; mergeAttempt < 3; mergeAttempt++)
                {
                    try
                    {
                        // 合并前清理可能残留的目标文件（上一轮失败可能留下被占用的半成品）
                        if (mergeAttempt > 0)
                        {
                            try { await Task.Delay(1000, externalToken); }
                            catch (OperationCanceledException) { return false; }
                            TryDeleteFileWithRetry(destinationPath);
                        }

                        using (var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            for (int i = 0; i < blockCount; i++)
                            {
                                string tempPath = destinationPath + $".part{i}";
                                if (!File.Exists(tempPath))
                                {
                                    throw new FileNotFoundException($"分块文件不存在: {tempPath}");
                                }

                                using (var input = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    await input.CopyToAsync(output);
                                }
                                TryDeleteFileWithRetry(tempPath);
                            }
                        }

                        progressCallback?.Invoke(100, $"多线程下载完成({threadCount}线程)");
                        LogHelper.WriteLogToFile($"AutoUpdate | 多线程下载完成({threadCount}线程)");

                        // 文件大小校验
                        FileInfo fileInfo = new FileInfo(destinationPath);
                        if (fileInfo.Length != totalSize)
                        {
                            LogHelper.WriteLogToFile($"AutoUpdate | 文件大小校验失败，本地：{fileInfo.Length}，服务器：{totalSize}", LogHelper.LogType.Error);
                            TryDeleteFileWithRetry(destinationPath);
                            progressCallback?.Invoke(0, UpdateStrings.Msg_UpdateIntegrityFailed);
                            SetDownloadFailure(DownloadFailureReason.IntegrityCheckFailed);
                            return false;
                        }

                        // ZIP文件完整性校验
                        if (destinationPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                ZipFile.OpenRead(destinationPath).Dispose();
                            }
                            catch (Exception zipEx)
                            {
                                LogHelper.WriteLogToFile($"AutoUpdate | ZIP文件解压测试失败，文件可能已损坏: {zipEx.Message}", LogHelper.LogType.Error);
                                TryDeleteFileWithRetry(destinationPath);
                                progressCallback?.Invoke(0, UpdateStrings.Msg_UpdateIntegrityFailed);
                                SetDownloadFailure(DownloadFailureReason.IntegrityCheckFailed);
                                return false;
                            }
                        }
                        return true;
                    }
                    catch (Exception ex)
                    {
                        mergeError = ex;
                        LogHelper.WriteLogToFile($"AutoUpdate | 合并分块文件时出错(第{mergeAttempt + 1}次): {ex.Message}", LogHelper.LogType.Warning);
                        TryDeleteFileWithRetry(destinationPath);
                        if (externalToken.IsCancellationRequested)
                        {
                            progressCallback?.Invoke(0, "下载已取消");
                            SetDownloadFailure(DownloadFailureReason.Cancelled);
                            return false;
                        }
                        // 仅对文件占用类异常重试；其它异常重试一次后放弃
                        bool isFileLock = ex is IOException && ex.Message != null &&
                            (ex.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase) ||
                             ex.Message.Contains("正由另一进程使用", StringComparison.OrdinalIgnoreCase));
                        if (!isFileLock) break;
                    }
                }
                LogHelper.WriteLogToFile($"AutoUpdate | 合并分块文件最终失败: {mergeError?.Message}", LogHelper.LogType.Error);
                bool finalFileLock = mergeError is IOException && mergeError.Message != null &&
                    (mergeError.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase) ||
                     mergeError.Message.Contains("正由另一进程使用", StringComparison.OrdinalIgnoreCase));
                progressCallback?.Invoke(0, finalFileLock
                        ? UpdateStrings.Msg_UpdateFileInUse
                        : string.Format(UpdateStrings.Msg_UpdateMergeFailed, mergeError?.Message ?? ""));
                SetDownloadFailure(finalFileLock ? DownloadFailureReason.FileInUse : DownloadFailureReason.MergeFailed);
                return false;
            }
            return false;
        }

        /// <summary>
        /// 删除文件并对文件占用异常做有限重试，避免杀软扫描或残留句柄导致删除失败。
        /// </summary>
        private static void TryDeleteFileWithRetry(string path, int maxRetry = 3)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            for (int i = 0; i < maxRetry; i++)
            {
                try { File.Delete(path); return; }
                catch (IOException) { try { Thread.Sleep(500); } catch (Exception ex) { LogService.LogException(ex); } }
                catch { return; }
            }
        }

        // 单线程下载方法
        private static async Task<bool> DownloadSingleThread(string fileUrl, string destinationPath, long totalSize, Action<double, string> progressCallback = null, CancellationToken externalToken = default)
        {
            try
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 开始单线程下载: {fileUrl}");
                progressCallback?.Invoke(0, "开始单线程下载");

                CancellationToken token = externalToken;
                if (token == default)
                {
                    lock (_activeDownloadLock)
                    {
                        token = _activeDownloadSessions.FirstOrDefault()?.Token ?? CancellationToken.None;
                    }
                }

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    client.Timeout = TimeSpan.FromMinutes(10); // 单线程下载设置更长的超时时间

                    using (var resp = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, token))
                    {
                        resp.EnsureSuccessStatusCode();
                        using (var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            var stream = await resp.Content.ReadAsStreamAsync();
                            byte[] buffer = new byte[8192];
                            int read;
                            long downloaded = 0;
                            var lastProgressUpdate = DateTime.UtcNow;

                            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                            {
                                await fs.WriteAsync(buffer, 0, read, token);
                                downloaded += read;

                                // 限制进度更新频率，避免UI卡顿
                                if (DateTime.UtcNow - lastProgressUpdate > TimeSpan.FromMilliseconds(500))
                                {
                                    if (totalSize > 0)
                                    {
                                        double percent = (double)downloaded / totalSize * 100;
                                        progressCallback?.Invoke(percent, $"单线程下载中: {percent:F1}%");
                                    }
                                    lastProgressUpdate = DateTime.UtcNow;
                                }
                            }
                        }
                    }
                }

                progressCallback?.Invoke(100, "单线程下载完成");
                LogHelper.WriteLogToFile("AutoUpdate | 单线程下载完成");
                return true;
            }
            catch (OperationCanceledException)
            {
                LogHelper.WriteLogToFile("AutoUpdate | 单线程下载已被取消", LogHelper.LogType.Warning);
                progressCallback?.Invoke(0, "下载已取消");
                try { if (File.Exists(destinationPath)) File.Delete(destinationPath); } catch (Exception ex) { LogService.LogException(ex); }
                SetDownloadFailure(DownloadFailureReason.Cancelled);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 单线程下载失败: {ex.Message}", LogHelper.LogType.Error);
                bool isFileLock = ex is IOException && ex.Message != null &&
                    (ex.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase) ||
                     ex.Message.Contains("正由另一进程使用", StringComparison.OrdinalIgnoreCase));
                progressCallback?.Invoke(0, isFileLock
                    ? UpdateStrings.Msg_UpdateFileInUse
                    : $"单线程下载失败: {ex.Message}");
                SetDownloadFailure(isFileLock ? DownloadFailureReason.FileInUse : DownloadFailureReason.NetworkOrAllLinesFailed);
                return false;
            }
        }

        // 获取文件总大小
        private static async Task<long> GetContentLength(string fileUrl)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var req = new HttpRequestMessage(HttpMethod.Head, fileUrl);
                    var resp = await client.SendAsync(req);
                    if (resp.IsSuccessStatusCode && resp.Content.Headers.ContentLength.HasValue)
                        return resp.Content.Headers.ContentLength.Value;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            return -1;
        }

        /// <summary>
        /// 将下载完成状态写入预定义的状态文件以供后续检查。
        /// </summary>
        /// <param name="isSuccess">指示下载是否成功；将以字符串形式写入状态文件（"True" 或 "False"）。</param>
        /// <remarks>
        /// 如果状态文件路径为空则不执行任何操作；方法内部捕获异常并记录日志，不会向调用方抛出异常。
        /// </remarks>
        private static void SaveDownloadStatus(bool isSuccess)
        {
            try
            {
                if (statusFilePath == null) return;

                string directory = Path.GetDirectoryName(statusFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(statusFilePath, isSuccess.ToString());
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 保存下载状态时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
    }
}
