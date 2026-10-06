using System;
using System.Collections.Generic;

namespace Ink_Canvas.Plugins
{
    internal sealed class PluginSettingsNavigationEntry
    {
        internal PluginSettingsNavigationEntry(PluginInfo plugin, string pageTag)
        {
            Plugin = plugin;
            PageTag = pageTag;
        }

        internal PluginInfo Plugin { get; }

        internal string PageTag { get; }
    }

    internal static class PluginSettingsNavigationRegistry
    {
        internal static IReadOnlyList<PluginSettingsNavigationEntry> Discover(
            IEnumerable<PluginInfo> plugins,
            Action<PluginInfo, Exception> onError = null)
        {
            if (plugins == null) throw new ArgumentNullException(nameof(plugins));
            var result = new List<PluginSettingsNavigationEntry>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var plugin in plugins)
            {
                if (plugin?.Instance == null || string.IsNullOrWhiteSpace(plugin.Id) || !ids.Add(plugin.Id))
                    continue;
                try
                {
                    if (plugin.Instance.GetSettingsView() != null)
                        result.Add(new PluginSettingsNavigationEntry(plugin, $"PluginSettings_{plugin.Id}"));
                }
                catch (Exception ex)
                {
                    onError?.Invoke(plugin, ex);
                }
            }
            return result;
        }
    }
}
