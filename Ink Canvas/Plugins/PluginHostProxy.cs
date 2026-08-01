using Microsoft.Extensions.DependencyInjection;
using System;
using System.Text.Json;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 插件独立宿主包装。每个插件加载时获得自己的 <see cref="PluginHostProxy"/>：
    /// 除日志外的所有调用转发到共享的 <see cref="PluginManager"/>，
    /// 而 <see cref="Log"/> / <see cref="LogError"/> 路由到该插件独立的 <see cref="PluginLogger"/>，
    /// 保证插件日志统一写入 <c>PluginLogs/&lt;plugin-id&gt;/</c> 目录，互不混入宿主日志，
    /// 也禁止插件自行写文件或写入主程序日志。
    /// </summary>
    internal sealed class PluginHostProxy : IPluginHost
    {
        private readonly PluginManager _manager;
        private readonly PluginLogger _logger;
        private readonly string _pluginId;

        public PluginHostProxy(PluginManager manager, PluginLogger logger, string pluginId)
        {
            _manager = manager;
            _logger = logger;
            _pluginId = pluginId;
        }

        /// <summary>
        /// 插件普通日志：仅写入该插件自己的日志文件，不落入宿主日志与主程序日志。
        /// </summary>
        public void Log(string message)
        {
            _logger?.Info(_pluginId, message);
        }

        /// <summary>
        /// 插件错误日志：仅写入该插件自己的日志文件，不落入宿主日志与主程序日志。
        /// </summary>
        public void LogError(string message, Exception ex = null)
        {
            _logger?.Error(_pluginId, message, ex);
        }

        public IServiceCollection Services => _manager.Services;

        public IServiceProvider ServiceProvider => _manager.ServiceProvider;

        public T GetService<T>() where T : class => _manager.GetService<T>();

        public void RegisterService<T>(T service) where T : class => _manager.RegisterService(service);

        public void RegisterToolbarItem(PluginToolbarItemInfo itemInfo) => _manager.RegisterToolbarItem(itemInfo);

        public void RegisterBoardToolbarItem(PluginToolbarItemInfo itemInfo) => _manager.RegisterBoardToolbarItem(itemInfo);

        public void RegisterIpcHandler(string method, Func<JsonElement?, object> handler) => _manager.RegisterIpcHandler(method, handler);

        public IPluginIpcBus Ipc => _manager.Ipc;

        public SecurityVerdict EvaluateTrust(string packagePath, string expectedSha256, string declaredPluginId)
            => _manager.EvaluateTrust(packagePath, expectedSha256, declaredPluginId);
    }
}
