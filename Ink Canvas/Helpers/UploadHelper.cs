using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        /// <returns>是否上传成功</returns>
        Task<bool> UploadAsync(string filePath);
    }

    /// <summary>
    /// Dlass上传提供者
    /// </summary>
    public class DlassUploadProvider : IUploadProvider
    {
        /// <summary>
        /// 提供者名称
        /// </summary>
        public string Name => "Dlass";

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled => MainWindow.Settings?.Dlass?.IsAutoUploadNotes ?? false;

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否上传成功</returns>
        public async Task<bool> UploadAsync(string filePath)
        {
            return await DlassNoteUploader.UploadNoteFileAsync(filePath);
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
        /// <returns>是否至少有一个提供者上传成功</returns>
        public static async Task<bool> UploadFileAsync(string filePath)
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

            foreach (var provider in providersSnapshot)
            {
                try
                {
                    if (provider.IsEnabled)
                    {
                        bool success = await provider.UploadAsync(filePath);
                        if (success)
                        {
                            anySuccess = true;
                        }
                    }
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
