using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using InkCanvas.PluginSdk;

namespace Ink_Canvas.Helpers.Plugins.New
{
    /// <summary>
    /// 插件管理器
    /// </summary>
    public class NuGetPluginManager
    {
        private static readonly string PluginsDirectory = Path.Combine(App.RootPath, "Plugins");
        private static readonly string PluginConfigFile = Path.Combine(App.RootPath, "Configs", "PluginConfig.json");
        private static readonly string PluginConfigBackupFile = Path.Combine(App.RootPath, "Configs", "PluginConfig.json.bak");

        private static NuGetPluginManager _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// 插件管理器单例
        /// </summary>
        public static NuGetPluginManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new NuGetPluginManager();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 已加载的插件集合
        /// </summary>
        public ObservableCollection<PluginInfo> Plugins { get; } = new ObservableCollection<PluginInfo>();

        /// <summary>
        /// 插件配置信息
        /// </summary>
        public Dictionary<string, PluginConfiguration> PluginConfigurations { get; private set; } = new Dictionary<string, PluginConfiguration>();

        /// <summary>
        /// 插件上下文
        /// </summary>
        public IPluginContext PluginContext { get; private set; }

        /// <summary>
        /// 加载的程序集缓存
        /// </summary>
        private Dictionary<string, Assembly> _loadedAssemblies = new Dictionary<string, Assembly>();

        /// <summary>
        /// 插件实例缓存
        /// </summary>
        private Dictionary<string, object> _pluginInstances = new Dictionary<string, object>();

        private NuGetPluginManager()
        {
            // 确保插件目录存在
            if (!Directory.Exists(PluginsDirectory))
            {
                Directory.CreateDirectory(PluginsDirectory);
            }
        }

        /// <summary>
        /// 初始化插件系统
        /// </summary>
        /// <param name="context">插件上下文</param>
        public void Initialize(IPluginContext context)
        {
            try
            {
                PluginContext = context;
                LogHelper.WriteLogToFile("开始初始化插件系统");

                // 加载配置
                LoadConfigurations();
                LogHelper.WriteLogToFile($"已从配置文件加载 {PluginConfigurations.Count} 个插件配置");

                // 扫描并加载插件
                ScanAndLoadPlugins();

                LogHelper.WriteLogToFile($"插件系统初始化完成，共加载 {Plugins.Count} 个插件");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化插件系统时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 扫描并加载插件
        /// </summary>
        private void ScanAndLoadPlugins()
        {
            try
            {
                // 扫描插件目录
                var pluginDirectories = Directory.GetDirectories(PluginsDirectory);
                LogHelper.WriteLogToFile($"发现 {pluginDirectories.Length} 个插件目录");

                foreach (var pluginDir in pluginDirectories)
                {
                    LoadPluginFromDirectory(pluginDir);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"扫描插件时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 从目录加载插件
        /// </summary>
        /// <param name="pluginDirectory">插件目录</param>
        private void LoadPluginFromDirectory(string pluginDirectory)
        {
            try
            {
                var pluginInfo = new PluginInfo
                {
                    Id = Path.GetFileName(pluginDirectory),
                    Directory = pluginDirectory,
                    IsLoaded = false,
                    IsEnabled = false
                };

                // 查找插件程序集
                var dllFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.AllDirectories);
                var mainDll = dllFiles.FirstOrDefault(f => !f.Contains("\\lib\\") && !f.Contains("\\runtimes\\"));

                if (mainDll == null)
                {
                    LogHelper.WriteLogToFile($"插件目录 {pluginDirectory} 中未找到主程序集", LogHelper.LogType.Warning);
                    return;
                }

                pluginInfo.AssemblyPath = mainDll;

                // 加载程序集
                var assembly = LoadAssembly(mainDll);
                if (assembly == null)
                {
                    LogHelper.WriteLogToFile($"无法加载程序集: {mainDll}", LogHelper.LogType.Error);
                    return;
                }

                // 查找插件类型
                var pluginType = FindPluginType(assembly);
                if (pluginType == null)
                {
                    LogHelper.WriteLogToFile($"程序集 {mainDll} 中未找到有效的插件类型", LogHelper.LogType.Warning);
                    return;
                }

                // 创建插件实例
                var pluginInstance = CreatePluginInstance(pluginType);
                if (pluginInstance == null)
                {
                    LogHelper.WriteLogToFile($"无法创建插件实例: {pluginType.Name}", LogHelper.LogType.Error);
                    return;
                }

                // 初始化插件
                pluginInstance.Initialize(PluginContext);

                // 更新插件信息
                pluginInfo.Name = pluginInstance.Name;
                pluginInfo.Description = pluginInstance.Description;
                pluginInfo.Version = pluginInstance.Version.ToString();
                pluginInfo.Author = pluginInstance.Author;
                pluginInfo.IsLoaded = true;

                // 检查配置中的启用状态
                if (PluginConfigurations.TryGetValue(pluginInfo.Id, out var config))
                {
                    pluginInfo.IsEnabled = config.IsEnabled;
                    if (config.IsEnabled)
                    {
                        pluginInstance.Start();
                    }
                }

                // 添加到插件列表
                Plugins.Add(pluginInfo);
                _pluginInstances[pluginInfo.Id] = pluginInstance;

                LogHelper.WriteLogToFile($"已加载插件: {pluginInfo.Name} v{pluginInfo.Version}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载插件目录 {pluginDirectory} 时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 加载程序集
        /// </summary>
        /// <param name="assemblyPath">程序集路径</param>
        /// <returns>加载的程序集</returns>
        private Assembly LoadAssembly(string assemblyPath)
        {
            try
            {
                if (_loadedAssemblies.TryGetValue(assemblyPath, out var loadedAssembly))
                {
                    return loadedAssembly;
                }

                var assembly = Assembly.LoadFrom(assemblyPath);
                _loadedAssemblies[assemblyPath] = assembly;
                return assembly;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载程序集 {assemblyPath} 时出错: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        /// <summary>
        /// 查找插件类型
        /// </summary>
        /// <param name="assembly">程序集</param>
        /// <returns>插件类型</returns>
        private Type FindPluginType(Assembly assembly)
        {
            try
            {
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IInkCanvasPlugin).IsAssignableFrom(t) && 
                               !t.IsAbstract && 
                               !t.IsInterface &&
                               t.IsClass);

                return pluginTypes.FirstOrDefault();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"查找插件类型时出错: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        /// <summary>
        /// 创建插件实例
        /// </summary>
        /// <param name="pluginType">插件类型</param>
        /// <returns>插件实例</returns>
        private IInkCanvasPlugin CreatePluginInstance(Type pluginType)
        {
            try
            {
                return (IInkCanvasPlugin)Activator.CreateInstance(pluginType);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"创建插件实例时出错: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        /// <summary>
        /// 启用插件
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        public void EnablePlugin(string pluginId)
        {
            try
            {
                var plugin = Plugins.FirstOrDefault(p => p.Id == pluginId);
                if (plugin == null)
                {
                    LogHelper.WriteLogToFile($"未找到插件: {pluginId}", LogHelper.LogType.Warning);
                    return;
                }

                if (!plugin.IsLoaded)
                {
                    LogHelper.WriteLogToFile($"插件 {pluginId} 未加载", LogHelper.LogType.Warning);
                    return;
                }

                if (plugin.IsEnabled)
                {
                    LogHelper.WriteLogToFile($"插件 {pluginId} 已经启用", LogHelper.LogType.Info);
                    return;
                }

                if (_pluginInstances.TryGetValue(pluginId, out var instance) && instance is IInkCanvasPlugin pluginInstance)
                {
                    pluginInstance.Start();
                    plugin.IsEnabled = true;

                    // 更新配置
                    if (PluginConfigurations.TryGetValue(pluginId, out var config))
                    {
                        config.IsEnabled = true;
                    }
                    else
                    {
                        PluginConfigurations[pluginId] = new PluginConfiguration { IsEnabled = true };
                    }

                    SaveConfigurations();
                    LogHelper.WriteLogToFile($"已启用插件: {plugin.Name}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启用插件 {pluginId} 时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 禁用插件
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        public void DisablePlugin(string pluginId)
        {
            try
            {
                var plugin = Plugins.FirstOrDefault(p => p.Id == pluginId);
                if (plugin == null)
                {
                    LogHelper.WriteLogToFile($"未找到插件: {pluginId}", LogHelper.LogType.Warning);
                    return;
                }

                if (!plugin.IsEnabled)
                {
                    LogHelper.WriteLogToFile($"插件 {pluginId} 已经禁用", LogHelper.LogType.Info);
                    return;
                }

                if (_pluginInstances.TryGetValue(pluginId, out var instance) && instance is IInkCanvasPlugin pluginInstance)
                {
                    pluginInstance.Stop();
                    plugin.IsEnabled = false;

                    // 更新配置
                    if (PluginConfigurations.TryGetValue(pluginId, out var config))
                    {
                        config.IsEnabled = false;
                    }
                    else
                    {
                        PluginConfigurations[pluginId] = new PluginConfiguration { IsEnabled = false };
                    }

                    SaveConfigurations();
                    LogHelper.WriteLogToFile($"已禁用插件: {plugin.Name}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"禁用插件 {pluginId} 时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 卸载插件
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        public void UnloadPlugin(string pluginId)
        {
            try
            {
                var plugin = Plugins.FirstOrDefault(p => p.Id == pluginId);
                if (plugin == null)
                {
                    LogHelper.WriteLogToFile($"未找到插件: {pluginId}", LogHelper.LogType.Warning);
                    return;
                }

                if (_pluginInstances.TryGetValue(pluginId, out var instance) && instance is IInkCanvasPlugin pluginInstance)
                {
                    if (plugin.IsEnabled)
                    {
                        pluginInstance.Stop();
                    }
                    pluginInstance.Cleanup();
                }

                Plugins.Remove(plugin);
                _pluginInstances.Remove(pluginId);
                PluginConfigurations.Remove(pluginId);

                SaveConfigurations();
                LogHelper.WriteLogToFile($"已卸载插件: {plugin.Name}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"卸载插件 {pluginId} 时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        private void LoadConfigurations()
        {
            try
            {
                if (File.Exists(PluginConfigFile))
                {
                    var json = File.ReadAllText(PluginConfigFile);
                    var configs = JsonConvert.DeserializeObject<Dictionary<string, PluginConfiguration>>(json);
                    if (configs != null)
                    {
                        PluginConfigurations = configs;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载插件配置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        private void SaveConfigurations()
        {
            try
            {
                var json = JsonConvert.SerializeObject(PluginConfigurations, Formatting.Indented);
                File.WriteAllText(PluginConfigFile, json);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存插件配置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
    }

    /// <summary>
    /// 插件信息
    /// </summary>
    public class PluginInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string Directory { get; set; }
        public string AssemblyPath { get; set; }
        public bool IsLoaded { get; set; }
        public bool IsEnabled { get; set; }
    }

    /// <summary>
    /// 插件配置
    /// </summary>
    public class PluginConfiguration
    {
        public bool IsEnabled { get; set; }
        public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
    }
}
