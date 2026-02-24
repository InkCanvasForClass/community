using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WebDav;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// WebDav上传工具类
    /// </summary>
    public static class WebDavUploader
    {
        /// <summary>
        /// 上传文件到WebDav服务器
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否上传成功</returns>
        public static async Task<bool> UploadFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 检查文件是否存在
                if (!File.Exists(filePath))
                {
                    return false;
                }

                // 获取WebDav设置
                var webDavUrl = MainWindow.Settings?.Dlass?.WebDavUrl;
                var username = MainWindow.Settings?.Dlass?.WebDavUsername;
                var password = MainWindow.Settings?.Dlass?.WebDavPassword;
                var rootDirectory = MainWindow.Settings?.Dlass?.WebDavRootDirectory;

                // 验证设置
                if (string.IsNullOrEmpty(webDavUrl))
                {
                    return false;
                }

                // 构建完整的目标路径
                var fileName = Path.GetFileName(filePath);
                var targetPath = Path.Combine(rootDirectory ?? string.Empty, fileName).Replace("\\", "/");
                if (targetPath.StartsWith("/"))
                {
                    targetPath = targetPath.Substring(1);
                }

                // 创建WebDav客户端
                var clientParams = new WebDavClientParams
                {
                    BaseAddress = new Uri(webDavUrl),
                    Credentials = new NetworkCredential(username ?? string.Empty, password ?? string.Empty)
                };

                using (var client = new WebDavClient(clientParams))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // 先直接尝试上传文件
                    using (var fileStream = File.OpenRead(filePath))
                    {
                        // 检查取消令牌
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var result = await client.PutFile(targetPath, fileStream);
                        if (result.IsSuccessful)
                        {
                            return true;
                        }
                        else
                        {
                            // 上传失败，尝试创建目录
                            var directoryPath = Path.GetDirectoryName(targetPath);
                            if (!string.IsNullOrEmpty(directoryPath))
                            {
                                await EnsureDirectoryExistsAsync(client, directoryPath, cancellationToken);
                                
                                // 再次尝试上传文件
                                cancellationToken.ThrowIfCancellationRequested();
                                using (var retryStream = File.OpenRead(filePath))
                                {
                                    var retryResult = await client.PutFile(targetPath, retryStream);
                                    return retryResult.IsSuccessful;
                                }
                            }
                            else
                            {
                                // 没有目录路径，直接返回失败
                                return false;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 确保WebDav目录存在
        /// </summary>
        /// <param name="client">WebDav客户端</param>
        /// <param name="directoryPath">目录路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        private static async Task EnsureDirectoryExistsAsync(IWebDavClient client, string directoryPath, CancellationToken cancellationToken)
        {
            try
            {
                // 分割路径并逐级创建目录
                var pathParts = directoryPath.Split('/');
                var currentPath = string.Empty;

                foreach (var part in pathParts)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (string.IsNullOrEmpty(part))
                        continue;

                    currentPath = Path.Combine(currentPath, part).Replace("\\", "/");

                    // 检查取消令牌
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 尝试创建目录
                    await client.Mkcol(currentPath);
                }
            }
            catch (Exception)
            {
                // 静默处理目录创建错误
            }
        }

        /// <summary>
        /// 检查WebDAV是否已启用
        /// </summary>
        /// <returns>是否启用</returns>
        public static bool IsWebDavEnabled()
        {
            // 检查WebDav设置是否有效
            var webDavUrl = MainWindow.Settings?.Dlass?.WebDavUrl;
            if (string.IsNullOrEmpty(webDavUrl))
            {
                return false;
            }

            // 尝试解析URL
            try
            {
                new Uri(webDavUrl);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
