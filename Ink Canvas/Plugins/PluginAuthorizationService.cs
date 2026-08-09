using Ink_Canvas.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Windows;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 管理插件加载外部程序集的用户授权。授权绑定插件 ID、程序集路径和 SHA-256。
    /// <para>
    /// 为避免同一插件的多个外部依赖在 ALC 递归解析期间连续弹出多个对话框
    /// （以及并发解析时重复弹窗导致 UI 渲染错乱），本实现：
    /// 1. 串行化所有授权请求（<see cref="_authorizationGate"/>）；
    /// 2. 同一插件在一次加载会话内的首次决定（允许/拒绝）会被复用到该插件其余 DLL，
    ///    仅当用户选择「允许」时才把每个 DLL 的路径 + SHA-256 持久化；
    /// 3. 所有 MessageBox 调用强制切换到 UI 线程，避免在后台线程创建窗口。
    /// </para>
    /// </summary>
    internal sealed class PluginAuthorizationService
    {
        private readonly string _filePath;
        private readonly Dictionary<string, string> _authorizations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 串行化授权请求，确保同一时刻只有一个对话框。
        private readonly SemaphoreSlim _authorizationGate = new SemaphoreSlim(1, 1);

        // 会话级决定缓存：插件 Id -> 是否允许。一次加载会话内，同一插件的后续 DLL 复用首次决定，
        // 避免逐个 DLL 弹窗。会话由 <see cref="BeginSession"/> / <see cref="EndSession"/> 控制。
        private readonly Dictionary<string, bool> _sessionDecisions = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        // 同一插件待授权的 DLL 文件名集合，用于在单次确认对话框中向用户展示完整清单。
        private readonly Dictionary<string, HashSet<string>> _sessionPendingFiles = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private int _sessionDepth;

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
            lock (_authorizations)
            {
                return _authorizations.TryGetValue(key, out var authorizedHash)
                    && string.Equals(authorizedHash, hash, StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool RequestAuthorization(PluginInfo plugin, string assemblyPath)
        {
            return Request(plugin, assemblyPath, PluginStrings.Plugin_ExternalDllAuthorizationMessage);
        }

        public bool RequestExternalAuthorization(PluginInfo plugin, string assemblyPath)
        {
            return Request(plugin, assemblyPath, PluginStrings.Plugin_ExternalDllAuthorizationMessage);
        }

        /// <summary>
        /// 开始一次插件加载会话。会话内同一插件的多个外部 DLL 只弹一次确认框。
        /// 嵌套调用通过引用计数支持。
        /// </summary>
        public void BeginSession()
        {
            Interlocked.Increment(ref _sessionDepth);
        }

        /// <summary>
        /// 结束一次插件加载会话，清空会话级决定缓存。必须与 <see cref="BeginSession"/> 配对调用。
        /// </summary>
        public void EndSession()
        {
            if (Interlocked.Decrement(ref _sessionDepth) <= 0)
            {
                lock (_sessionDecisions)
                {
                    _sessionDecisions.Clear();
                    _sessionPendingFiles.Clear();
                }
            }
        }

        private bool Request(PluginInfo plugin, string assemblyPath, string messageTemplate)
        {
            // 已持久化授权且哈希匹配时直接放行。
            if (IsAuthorized(plugin, assemblyPath)) return true;
            if (plugin == null || string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath)) return false;

            var pluginId = plugin.Id;
            var fileName = Path.GetFileName(assemblyPath);

            // 会话内复用同一插件的决定。
            if (_sessionDepth > 0)
            {
                lock (_sessionDecisions)
                {
                    if (_sessionDecisions.TryGetValue(pluginId, out var sessionAllowed))
                    {
                        if (!sessionAllowed) return false;
                        // 用户已对该插件本次会话授权，持久化当前 DLL 的授权记录。
                        PersistAuthorization(plugin, assemblyPath);
                        return true;
                    }

                    // 收集待授权文件，稍后在单次对话框中一并向用户确认。
                    if (!_sessionPendingFiles.ContainsKey(pluginId))
                        _sessionPendingFiles[pluginId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _sessionPendingFiles[pluginId].Add(fileName);
                }
            }

            // 串行化：等待其它授权请求完成，避免并发弹窗。
            _authorizationGate.Wait();
            try
            {
                // 再次检查持久化授权，可能在等待期间被其它线程写入。
                if (IsAuthorized(plugin, assemblyPath)) return true;

                // 会话内再次检查决定（可能在等待期间被同一插件的其它 DLL 触发并写入）。
                if (_sessionDepth > 0)
                {
                    lock (_sessionDecisions)
                    {
                        if (_sessionDecisions.TryGetValue(pluginId, out var sessionAllowed))
                        {
                            if (!sessionAllowed) return false;
                            PersistAuthorization(plugin, assemblyPath);
                            return true;
                        }
                    }
                }

                // 在 UI 线程展示对话框。非 UI 线程直接调用 MessageBox.Show 会创建无 owner 窗口，
                // 与启动期工具栏重建队列交错时导致焦点、渲染上下文异常。
                bool allowed = ShowAuthorizationDialogOnUIThread(plugin, assemblyPath, messageTemplate, fileName);

                if (_sessionDepth > 0)
                {
                    lock (_sessionDecisions)
                    {
                        _sessionDecisions[pluginId] = allowed;
                    }
                }

                if (!allowed) return false;

                PersistAuthorization(plugin, assemblyPath);
                return true;
            }
            finally
            {
                _authorizationGate.Release();
            }
        }

        private bool ShowAuthorizationDialogOnUIThread(PluginInfo plugin, string assemblyPath, string messageTemplate, string fileName)
        {
            var application = Application.Current;
            if (application == null)
            {
                // 无 Application 上下文（极早期或设计期），保守拒绝。
                return false;
            }

            var dispatcher = application.Dispatcher;
            if (dispatcher == null) return false;

            if (dispatcher.CheckAccess())
            {
                return ShowAuthorizationDialog(plugin, assemblyPath, messageTemplate, fileName);
            }

            bool result = false;
            dispatcher.Invoke(() =>
            {
                result = ShowAuthorizationDialog(plugin, assemblyPath, messageTemplate, fileName);
            });
            return result;
        }

        private bool ShowAuthorizationDialog(PluginInfo plugin, string assemblyPath, string messageTemplate, string fileName)
        {
            // 会话内若有多个待确认文件，在消息中列出，让用户一次决定是否信任该插件的全部外部依赖。
            string fileList = fileName;
            if (_sessionDepth > 0)
            {
                lock (_sessionDecisions)
                {
                    if (_sessionPendingFiles.TryGetValue(plugin.Id, out var files) && files.Count > 1)
                    {
                        fileList = string.Join("\n  - ", files);
                        fileList = "  - " + fileList;
                    }
                }
            }

            var message = string.Format(messageTemplate,
                plugin.Name, plugin.Author, fileList);

            var owner = Application.Current?.MainWindow;
            MessageBoxResult result;
            if (owner != null && owner.IsLoaded)
            {
                result = iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(
                    owner,
                    message,
                    PluginStrings.Plugin_ExternalDllAuthorizationTitle,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
            }
            else
            {
                result = iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(
                    message,
                    PluginStrings.Plugin_ExternalDllAuthorizationTitle,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
            }
            return result == MessageBoxResult.Yes;
        }

        private void PersistAuthorization(PluginInfo plugin, string assemblyPath)
        {
            var key = CreateKey(plugin, assemblyPath);
            if (key == null) return;
            var hash = ComputeHash(assemblyPath);
            lock (_authorizations)
            {
                _authorizations[key] = hash;
            }
            Save();
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
