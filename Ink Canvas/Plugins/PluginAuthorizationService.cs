using Ink_Canvas.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 管理插件加载外部程序集的用户授权。授权绑定插件 ID、程序集路径和 SHA-256。
    /// </summary>
    internal sealed class PluginAuthorizationService
    {
        private readonly string _filePath;
        private readonly Dictionary<string, string> _authorizations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public PluginAuthorizationService(string basePath)
        {
            _filePath = Path.Combine(basePath, "Configs", "plugin_authorizations.json");
            Load();
        }

        public bool IsAuthorized(PluginInfo plugin, string assemblyPath)
        {
            var key = CreateKey(plugin, assemblyPath);
            if (key == null || !File.Exists(assemblyPath)) return false;
            var hash = ComputeHash(assemblyPath);
            return _authorizations.TryGetValue(key, out var authorizedHash)
                && string.Equals(authorizedHash, hash, StringComparison.OrdinalIgnoreCase);
        }

        public bool RequestAuthorization(PluginInfo plugin, string assemblyPath)
        {
            return Request(plugin, assemblyPath, PluginStrings.Plugin_ExternalDllAuthorizationMessage);
        }

        public bool RequestExternalAuthorization(PluginInfo plugin, string assemblyPath)
        {
            return Request(plugin, assemblyPath, PluginStrings.Plugin_ExternalDllAuthorizationMessage);
        }

        private bool Request(PluginInfo plugin, string assemblyPath, string messageTemplate)
        {
            if (IsAuthorized(plugin, assemblyPath)) return true;

            var message = string.Format(messageTemplate,
                plugin.Name, plugin.Author, Path.GetFileName(assemblyPath));
            var result = iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(
                message,
                PluginStrings.Plugin_ExternalDllAuthorizationTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return false;

            _authorizations[CreateKey(plugin, assemblyPath)] = ComputeHash(assemblyPath);
            Save();
            return true;
        }

        private static string CreateKey(PluginInfo plugin, string assemblyPath)
        {
            if (plugin == null || string.IsNullOrWhiteSpace(plugin.Id) || string.IsNullOrEmpty(assemblyPath)) return null;
            return plugin.Id + "|" + Path.GetFullPath(assemblyPath);
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return;
                var values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_filePath));
                if (values == null) return;
                foreach (var value in values) _authorizations[value.Key] = value.Value;
            }
            catch { }
        }

        private void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(_filePath, JsonSerializer.Serialize(_authorizations,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private static string ComputeHash(string path)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(sha256.ComputeHash(stream));
        }
    }
}
