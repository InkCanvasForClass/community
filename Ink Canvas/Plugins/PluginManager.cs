using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

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
        public Assembly? Assembly { get; set; }
        public Exception? LoadError { get; set; }
    }

    public class PluginManager : IPluginHost
    {
        private static PluginManager? _instance;
        public static PluginManager Instance => _instance ??= new PluginManager();

        private readonly List<PluginInfo> _plugins = new();
        private readonly Dictionary<Type, object> _services = new();
        private string _pluginsDirectory;

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
            await Task.Run(() => LoadAll());
        }

        private void LoadAll()
        {
            if (!Directory.Exists(_pluginsDirectory))
            {
                Directory.CreateDirectory(_pluginsDirectory);
                Log("Plugins directory created");
                return;
            }

            var dllFiles = Directory.GetFiles(_pluginsDirectory, "*.dll")
                .Where(f => !f.EndsWith("InkCanvasForClass.dll") &&
                           !f.EndsWith("Weikio.PluginFramework.dll"));

            foreach (var dll in dllFiles)
            {
                LoadPlugin(dll);
            }

            Log($"Plugin loading complete. Loaded {_plugins.Count} plugins.");
        }

        private void LoadPlugin(string dllPath)
        {
            if (!File.Exists(dllPath))
            {
                Log($"Plugin file not found: {dllPath}");
                return;
            }

            try
            {
                var assembly = Assembly.LoadFrom(dllPath);
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var pluginType in pluginTypes)
                {
                    try
                    {
                        var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;

                        var info = new PluginInfo
                        {
                            Id = plugin.Id,
                            Name = plugin.Name,
                            Version = plugin.Version,
                            FilePath = dllPath,
                            IsLoaded = true,
                            Instance = plugin,
                            Assembly = assembly
                        };

                        plugin.Initialize(this);
                        _plugins.Add(info);
                        PluginLoaded?.Invoke(this, info);
                        Log($"Plugin loaded: {info.Name} v{info.Version}");
                    }
                    catch (Exception ex)
                    {
                        LogError($"Failed to create plugin instance from {dllPath}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to load plugin from {dllPath}", ex);

                var info = new PluginInfo
                {
                    FilePath = dllPath,
                    LoadError = ex
                };
                _plugins.Add(info);
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