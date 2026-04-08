using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Weikio.PluginFramework;
using Weikio.PluginFramework.Abstractions;
using Weikio.PluginFramework.Catalogs;

namespace Ink_Canvas.Plugins
{
    public class PluginInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string FilePath { get; set; } = "";
        public bool IsLoaded { get; set; }
        public IPlugin? Instance { get; set; }
        public Exception? LoadError { get; set; }
    }

    public class PluginManager : IPluginHost
    {
        private static PluginManager? _instance;
        public static PluginManager Instance => _instance ??= new PluginManager();

        private readonly List<PluginInfo> _plugins = new();
        private readonly Dictionary<Type, object> _services = new();
        private string _pluginsDirectory;
        private FolderPluginCatalog? _catalog;

        public IReadOnlyList<PluginInfo> Plugins => _plugins.AsReadOnly();
        public event EventHandler<PluginInfo>? PluginLoaded;
        public event EventHandler<PluginInfo>? PluginUnloaded;
        public event EventHandler<string>? LogMessage;

        private PluginManager()
        {
            _pluginsDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Plugins");
        }

        public void SetPluginsDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                _pluginsDirectory = path;
            }
        }

        public async Task LoadAllAsync()
        {
            if (!Directory.Exists(_pluginsDirectory))
            {
                Directory.CreateDirectory(_pluginsDirectory);
                Log("Plugins directory created");
                return;
            }

            try
            {
                var options = new FolderPluginCatalogOptions
                {
                    SearchPatterns = new[] { "*.dll" },
                    IncludeSubfolders = false
                };

                _catalog = new FolderPluginCatalog(_pluginsDirectory, options);
                await _catalog.Initialize();

                var weikioPlugins = _catalog.GetPlugins();

                foreach (var weikioPlugin in weikioPlugins)
                {
                    try
                    {
                        var pluginType = weikioPlugin.GetType();
                        var instance = Activator.CreateInstance(pluginType) as IPlugin;

                        if (instance == null)
                        {
                            Log($"Failed to create instance of plugin from {weikioPlugin.AssemblyPath}");
                            continue;
                        }

                        var info = new PluginInfo
                        {
                            Id = instance.Id,
                            Name = instance.Name,
                            Version = instance.Version,
                            FilePath = weikioPlugin.AssemblyPath ?? "",
                            IsLoaded = true,
                            Instance = instance
                        };

                        instance.Initialize(this);
                        _plugins.Add(info);
                        PluginLoaded?.Invoke(this, info);
                        Log($"Plugin loaded: {info.Name} v{info.Version}");
                    }
                    catch (Exception ex)
                    {
                        LogError($"Failed to load plugin from {weikioPlugin.AssemblyPath}", ex);
                    }
                }

                Log($"Plugin loading complete. Loaded {_plugins.Count} plugins.");
            }
            catch (Exception ex)
            {
                LogError("Failed to initialize plugin catalog", ex);
            }
        }

        public void UnloadPlugin(PluginInfo plugin)
        {
            if (!plugin.IsLoaded || plugin.Instance == null)
                return;

            try
            {
                plugin.Instance.Shutdown();
                plugin.Instance = null;
                plugin.IsLoaded = false;

                _plugins.Remove(plugin);
                PluginUnloaded?.Invoke(this, plugin);
                Log($"Plugin unloaded: {plugin.Name}");
            }
            catch (Exception ex)
            {
                LogError($"Failed to unload plugin {plugin.Name}", ex);
            }
        }

        public void UnloadAll()
        {
            foreach (var plugin in _plugins.ToList())
            {
                UnloadPlugin(plugin);
            }
        }

        public void RegisterService<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
        }

        public T? GetService<T>() where T : class
        {
            return _services.TryGetValue(typeof(T), out var service) ? service as T : null;
        }

        public void Log(string message)
        {
            LogMessage?.Invoke(this, message);
            System.Diagnostics.Debug.WriteLine($"[PluginManager] {message}");
        }

        public void LogError(string message, Exception? ex = null)
        {
            var fullMessage = ex != null ? $"{message}: {ex.Message}\n{ex.StackTrace}" : message;
            Log($"ERROR: {fullMessage}");
        }
    }
}