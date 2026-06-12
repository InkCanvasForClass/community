using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;

namespace Ink_Canvas.Plugins
{
    public class PluginManager : IPluginHost
    {
        private static PluginManager _instance;
        public static PluginManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PluginManager();
                }
                return _instance;
            }
        }

        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private readonly string _pluginsDirectory;
        private readonly List<PluginInfo> _plugins = new List<PluginInfo>();
        private readonly Dictionary<string, AssemblyLoadContext> _assemblyContexts = new Dictionary<string, AssemblyLoadContext>();

        public IReadOnlyList<PluginInfo> Plugins
        {
            get { return _plugins.AsReadOnly(); }
        }

        public event EventHandler<PluginInfo> PluginLoaded;
        public event EventHandler<PluginInfo> PluginUnloaded;
        public event EventHandler<string> LogMessage;

        private PluginManager()
        {
            _pluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");

            if (!Directory.Exists(_pluginsDirectory))
            {
                Directory.CreateDirectory(_pluginsDirectory);
            }
        }

        public async Task LoadAllAsync()
        {
            try
            {
                if (!Directory.Exists(_pluginsDirectory))
                {
                    Log("Plugins directory does not exist, skipping plugin loading");
                    return;
                }

                Log(string.Format("Loading plugins from: {0}", _pluginsDirectory));

                var pluginFiles = new List<string>();

                try
                {
                    var topLevelFiles = Directory.GetFiles(_pluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly);
                    pluginFiles.AddRange(topLevelFiles);
                    Log(string.Format("Found {0} top-level plugin files", topLevelFiles.Length));
                }
                catch (Exception ex)
                {
                    LogError(string.Format("Error getting top-level plugin files: {0}", ex.Message));
                }

                try
                {
                    var subDirectories = Directory.GetDirectories(_pluginsDirectory);
                    foreach (var subDir in subDirectories)
                    {
                        try
                        {
                            var subDirFiles = Directory.GetFiles(subDir, "*.dll", SearchOption.TopDirectoryOnly);
                            pluginFiles.AddRange(subDirFiles);
                            Log(string.Format("Found {0} plugin files in directory: {1}", subDirFiles.Length, Path.GetFileName(subDir)));
                        }
                        catch (Exception ex)
                        {
                            LogError(string.Format("Error scanning subdirectory {0}: {1}", Path.GetFileName(subDir), ex.Message));
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError(string.Format("Error getting subdirectories: {0}", ex.Message));
                }

                Log(string.Format("Found total {0} potential plugin files", pluginFiles.Count));

                foreach (var pluginFile in pluginFiles)
                {
                    try
                    {
                        await LoadPluginAsync(pluginFile);
                    }
                    catch (Exception ex)
                    {
                        LogError(string.Format("Failed to load plugin from {0}", Path.GetFileName(pluginFile)), ex);
                    }
                }

                _plugins.Sort((a, b) => a.Order.CompareTo(b.Order));
                Log(string.Format("Plugin loading complete. Loaded {0} plugins", _plugins.Count));
            }
            catch (Exception ex)
            {
                LogError("Failed to load plugins", ex);
            }
        }

        private async Task LoadPluginAsync(string pluginFile)
        {
            var fileName = Path.GetFileName(pluginFile);
            Log(string.Format("Loading plugin: {0}", fileName));

            var alc = new PluginAssemblyLoadContext(pluginFile, isCollectible: true);

            try
            {
                var assembly = alc.LoadFromAssemblyPath(pluginFile);
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass);

                foreach (var pluginType in pluginTypes)
                {
                    var pluginInstance = Activator.CreateInstance(pluginType) as IPlugin;
                    if (pluginInstance == null) continue;

                    var pluginInfo = new PluginInfo
                    {
                        Id = pluginInstance.Id,
                        Name = pluginInstance.Name,
                        Version = pluginInstance.Version,
                        Description = pluginInstance.Description,
                        Author = pluginInstance.Author,
                        Order = pluginInstance.Order,
                        Instance = pluginInstance,
                        IsLoaded = true
                    };

                    _plugins.Add(pluginInfo);
                    _assemblyContexts[pluginInfo.Id] = alc;

                    try
                    {
                        pluginInstance.Initialize(this);
                        Log(string.Format("Plugin loaded: {0} v{1} by {2}", pluginInfo.Name, pluginInfo.Version, pluginInfo.Author));
                        OnPluginLoaded(pluginInfo);
                    }
                    catch (Exception ex)
                    {
                        LogError(string.Format("Failed to initialize plugin {0}", pluginInfo.Name), ex);
                        _plugins.Remove(pluginInfo);
                        _assemblyContexts.Remove(pluginInfo.Id);
                        alc.Unload();
                    }
                }
            }
            catch
            {
                alc.Unload();
                throw;
            }

            await Task.CompletedTask;
        }

        public void UnloadPlugin(PluginInfo plugin)
        {
            try
            {
                plugin.Instance.Shutdown();
                _plugins.Remove(plugin);
                plugin.IsLoaded = false;

                if (_assemblyContexts.TryGetValue(plugin.Id, out var alc))
                {
                    _assemblyContexts.Remove(plugin.Id);
                    alc.Unload();
                }

                Log(string.Format("Plugin unloaded: {0}", plugin.Name));
                OnPluginUnloaded(plugin);
            }
            catch (Exception ex)
            {
                LogError(string.Format("Failed to unload plugin {0}", plugin.Name), ex);
            }
        }

        public void UnloadAll()
        {
            foreach (var plugin in _plugins.ToList())
            {
                UnloadPlugin(plugin);
            }
        }

        public void Log(string message)
        {
            OnLogMessage(message);
            System.Diagnostics.Debug.WriteLine(string.Format("[Plugin] {0}", message));
        }

        public void LogError(string message, Exception ex = null)
        {
            var fullMessage = ex != null ? string.Format("{0}: {1}", message, ex.Message) : message;
            OnLogMessage(string.Format("ERROR: {0}", fullMessage));
            System.Diagnostics.Debug.WriteLine(string.Format("[Plugin ERROR] {0}", fullMessage));
            if (ex != null)
            {
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        public T GetService<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service))
            {
                return service as T;
            }
            return null;
        }

        public void RegisterService<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
        }

        protected virtual void OnPluginLoaded(PluginInfo pluginInfo)
        {
            var handler = PluginLoaded;
            if (handler != null)
            {
                handler(this, pluginInfo);
            }
        }

        protected virtual void OnPluginUnloaded(PluginInfo pluginInfo)
        {
            var handler = PluginUnloaded;
            if (handler != null)
            {
                handler(this, pluginInfo);
            }
        }

        protected virtual void OnLogMessage(string message)
        {
            var handler = LogMessage;
            if (handler != null)
            {
                handler(this, message);
            }
        }

        private class PluginAssemblyLoadContext : AssemblyLoadContext
        {
            private readonly AssemblyDependencyResolver _resolver;

            public PluginAssemblyLoadContext(string pluginPath, bool isCollectible)
                : base(string.Format("PluginContext_{0}", Path.GetFileNameWithoutExtension(pluginPath)), isCollectible)
            {
                _resolver = new AssemblyDependencyResolver(pluginPath);
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
                if (assemblyPath != null)
                {
                    return LoadFromAssemblyPath(assemblyPath);
                }
                return null;
            }
        }
    }
}
