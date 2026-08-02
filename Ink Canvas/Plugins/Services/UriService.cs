using System;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="IPluginUriService"/> 的宿主实现：注册表与派发逻辑落在 <see cref="PluginManager"/>，
    /// 本类仅做转发。注册时由 <see cref="PluginManager"/> 通过 <c>_currentLoadingPlugin</c>
    /// 识别调用方插件 ID，因此 <see cref="IPluginUriService.RegisterHandler"/> 须在插件 Initialize 阶段调用。
    /// </summary>
    internal sealed class UriService : IPluginUriService
    {
        private readonly PluginManager _manager;

        public UriService(PluginManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public void RegisterHandler(string subPath, Func<PluginUriRequest, bool> handler)
            => _manager.RegisterUriHandler(subPath, handler);

        public bool OpenUri(string uri)
            => _manager.OpenUri(uri);
    }
}
