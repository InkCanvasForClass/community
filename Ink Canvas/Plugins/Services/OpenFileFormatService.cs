using System;
using System.Collections.Generic;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="IOpenFileFormatService"/> 的宿主实现：注册表与派发逻辑落在 <see cref="PluginManager"/>，
    /// 本类仅做转发。注册时由 <see cref="PluginManager"/> 通过 <c>_currentLoadingPlugin</c>
    /// 识别调用方插件 ID，因此须在插件 Initialize 阶段调用。
    /// </summary>
    internal sealed class OpenFileFormatService : IOpenFileFormatService
    {
        private readonly PluginManager _manager;

        public OpenFileFormatService(PluginManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public bool RegisterFormat(string groupName, IEnumerable<string> extensions, Func<string, bool> handler)
            => _manager.RegisterOpenFormat(groupName, extensions, handler);
    }
}
