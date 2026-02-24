using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 上传提供者接口
    /// </summary>
    public interface IUploadProvider
    {
        /// <summary>
        /// 提供者名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 是否启用
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否上传成功</returns>
        Task<bool> UploadAsync(string filePath, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Dlass上传提供者
    /// </summary>
    public class DlassUploadProvider : IUploadProvider
    {
        public static readonly DlassUploadQueue Queue = new DlassUploadQueue();

        /// <summary>
        /// 提供者名称
        /// </summary>
        public string Name => "Dlass";

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled => MainWindow.Settings?.Upload?.EnabledProviders?.Contains(Name) ?? false;

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否上传成功</returns>
        public async Task<bool> UploadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return await Queue.UploadFileAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// WebDav上传提供者
    /// </summary>
    public class WebDavUploadProvider : IUploadProvider
    {
        public static readonly WebDavUploadQueue Queue = new WebDavUploadQueue();

        /// <summary>
        /// 提供者名称
        /// </summary>
        public string Name => "WebDav";

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled => MainWindow.Settings?.Upload?.EnabledProviders?.Contains(Name) ?? false;

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否上传成功</returns>
        public async Task<bool> UploadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return await Queue.UploadFileAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
    }



    /// <summary>
    /// 上传帮助类
    /// </summary>
    public static class UploadHelper
    {
        private static readonly List<IUploadProvider> _providers = new List<IUploadProvider>();
        private static bool _initialized;
        private static readonly object s_sync = new object();

        /// <summary>
        /// 初始化上传帮助类
        /// </summary>
        public static void Initialize()
        {
            lock (s_sync)
            {
                if (_initialized)
                    return;

                // 注册默认上传提供者
                RegisterProviderInternal(new DlassUploadProvider());
                RegisterProviderInternal(new WebDavUploadProvider());

                // 注册上传队列
                UploadQueueHelper.RegisterQueue(DlassUploadProvider.Queue);
                UploadQueueHelper.RegisterQueue(WebDavUploadProvider.Queue);

                // 初始化所有上传队列
                UploadQueueHelper.InitializeAllQueues();

                _initialized = true;
            }
        }

        /// <summary>
        /// 注册上传提供者
        /// </summary>
        /// <param name="provider">上传提供者</param>
        public static void RegisterProvider(IUploadProvider provider)
        {
            if (provider == null)
                return;

            lock (s_sync)
            {
                RegisterProviderInternal(provider);
            }
        }

        private static void RegisterProviderInternal(IUploadProvider provider)
        {
            if (provider != null)
            {
                bool providerExists = _providers.Any(p => p.GetType() == provider.GetType());
                if (!providerExists)
                {
                    _providers.Add(provider);
                }
            }
        }

        /// <summary>
        /// 上传文件到所有启用的提供者
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否至少有一个提供者上传成功</returns>
        public static async Task<bool> UploadFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (!_initialized)
            {
                Initialize();
            }

            List<IUploadProvider> providersSnapshot;
            lock (s_sync)
            {
                providersSnapshot = new List<IUploadProvider>(_providers);
            }

            bool anySuccess = false;

            // 获取上传延迟时间
            int delayMinutes = MainWindow.Settings?.Upload?.UploadDelayMinutes ?? 0;

            // 应用上传延迟
            if (delayMinutes > 0)
            {
                LogHelper.WriteLogToFile($"上传延迟 {delayMinutes} 分钟", LogHelper.LogType.Event);
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(TimeSpan.FromMinutes(delayMinutes), cancellationToken).ConfigureAwait(false);
            }

            // 上传前验证文件是否存在且可访问
            if (!File.Exists(filePath))
            {
                LogHelper.WriteLogToFile($"上传失败：文件不存在 - {filePath}", LogHelper.LogType.Error);
                return false;
            }

            try
            {
                // 检查文件是否可访问
                using (var fileStream = File.OpenRead(filePath))
                {
                    // 文件可访问
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"上传失败：文件不可访问 - {filePath}, 原因: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }

            foreach (var provider in providersSnapshot)
            {
                try
                {
                    if (provider.IsEnabled)
                    {
                        bool success = await provider.UploadAsync(filePath, cancellationToken).ConfigureAwait(false);
                        if (success)
                        {
                            anySuccess = true;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    LogHelper.WriteLogToFile($"上传被取消: {provider.Name}", LogHelper.LogType.Event);
                    throw;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"使用 {provider.Name} 上传失败: {ex}", LogHelper.LogType.Error);
                }
            }

            return anySuccess;
        }

        /// <summary>
        /// 获取所有上传提供者
        /// </summary>
        /// <returns>上传提供者列表</returns>
        public static List<IUploadProvider> GetProviders()
        {
            if (!_initialized)
            {
                Initialize();
            }

            lock (s_sync)
            {
                return new List<IUploadProvider>(_providers);
            }
        }

        /// <summary>
        /// 获取所有启用的上传提供者
        /// </summary>
        /// <returns>启用的上传提供者列表</returns>
        public static List<IUploadProvider> GetEnabledProviders()
        {
            if (!_initialized)
            {
                Initialize();
            }

            lock (s_sync)
            {
                return _providers.FindAll(p => p.IsEnabled);
            }
        }
    }
}
