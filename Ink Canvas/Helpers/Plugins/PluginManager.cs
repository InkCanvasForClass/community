using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Ink_Canvas.Windows;
using Newtonsoft.Json;
using Timer = System.Timers.Timer;

namespace Ink_Canvas.Helpers.Plugins
{
    /// <summary>
    /// 插件管理器，负责插件的加载、卸载和管理
    /// </summary>
    public class PluginManager
    {
        private static readonly string PluginsDirectory = Path.Combine(App.RootPath, "Plugins");
        private static readonly string PluginConfigFile = Path.Combine(App.RootPath, "PluginConfig.json");
        private static readonly string PluginConfigBackupFile = Path.Combine(App.RootPath, "PluginConfig.json.bak");
        
        private static PluginManager _instance;
        private static SemaphoreSlim _configLock = new SemaphoreSlim(1, 1);
        
        /// <summary>
        /// 插件管理器单例
        /// </summary>
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
        
        /// <summary>
        /// 已加载的插件集合
        /// </summary>
        public ObservableCollection<IPlugin> Plugins { get; } = new ObservableCollection<IPlugin>();
        
        /// <summary>
        /// 插件配置信息
        /// </summary>
        public Dictionary<string, bool> PluginStates { get; private set; } = new Dictionary<string, bool>();

        /// <summary>
        /// 配置是否已更改但未保存
        /// </summary>
        private bool _configDirty;
        
        /// <summary>
        /// 配置自动保存计时器
        /// </summary>
        private Timer _autoSaveTimer;
        
        /// <summary>
        /// 加载的程序集缓存
        /// </summary>
        private Dictionary<string, Assembly> _loadedAssemblies = new Dictionary<string, Assembly>();
        
        /// <summary>
        /// 插件文件哈希缓存，用于热重载检测
        /// </summary>
        private Dictionary<string, string> _pluginHashes = new Dictionary<string, string>();
        
        private PluginManager()
        {
            // 确保插件目录存在
            if (!Directory.Exists(PluginsDirectory))
            {
                Directory.CreateDirectory(PluginsDirectory);
            }
            
            // 加载插件配置
            LoadConfig();
            
            // 初始化自动保存计时器（3秒）
            _autoSaveTimer = new Timer(3000);
            _autoSaveTimer.Elapsed += (s, e) => 
            {
                if (_configDirty)
                {
                    SaveConfigAsync().ConfigureAwait(false);
                }
            };
            _autoSaveTimer.AutoReset = false;
            
            // 注册插件状态变更事件处理
            AppDomain.CurrentDomain.ProcessExit += (s, e) => 
            {
                // 应用退出时强制保存配置
                if (_configDirty)
                {
                    SaveConfig();
                }
            };
        }
        
        /// <summary>
        /// 初始化插件系统
        /// </summary>
        public void Initialize()
        {
            try
            {
                LogHelper.WriteLogToFile("开始初始化插件系统");
                
                // 加载配置
                LoadConfig();
                LogHelper.WriteLogToFile($"已从配置文件加载 {PluginStates.Count} 个插件状态记录");
                
                // 加载内置插件
                LogHelper.WriteLogToFile("正在加载内置插件...");
                LoadBuiltInPlugins();
                
                // 加载外部插件
                LogHelper.WriteLogToFile("正在加载外部插件...");
                LoadExternalPlugins();
                
                // 启用已配置为启用的插件
                LogHelper.WriteLogToFile("正在应用配置的插件状态...");
                EnableConfiguredPlugins();
                
                // 设置定期检查热重载
                StartHotReloadWatcher();
                
                // 保存初始化后的配置（可能有新插件）
                SaveConfig();
                
                LogHelper.WriteLogToFile($"插件系统初始化完成，共加载 {Plugins.Count} 个插件");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化插件系统时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
        
        /// <summary>
        /// 加载内置插件
        /// </summary>
        private void LoadBuiltInPlugins()
        {
            try
            {
                // 获取当前程序集
                Assembly currentAssembly = Assembly.GetExecutingAssembly();
                
                // 查找实现了IPlugin接口的所有类型
                var pluginTypes = currentAssembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass);
                
                foreach (var pluginType in pluginTypes)
                {
                    try
                    {
                        // 创建插件实例
                        IPlugin plugin = (IPlugin)Activator.CreateInstance(pluginType);
                        
                        // 只处理内置插件
                        if (plugin.IsBuiltIn)
                        {
                            plugin.Initialize();
                            Plugins.Add(plugin);
                            LogHelper.WriteLogToFile($"已加载内置插件: {plugin.Name} v{plugin.Version}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"加载内置插件 {pluginType.Name} 时出错: {ex.Message}", LogHelper.LogType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载内置插件时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
        
        /// <summary>
        /// 加载外部插件
        /// </summary>
        private void LoadExternalPlugins()
        {
            try
            {
                // 检查插件目录是否存在
                if (!Directory.Exists(PluginsDirectory))
                {
                    Directory.CreateDirectory(PluginsDirectory);
                    return;
                }
                
                // 获取所有插件文件（支持 .iccpp 和 .dll 格式）
                var pluginFiles = Directory.GetFiles(PluginsDirectory, "*.iccpp", SearchOption.TopDirectoryOnly)
                    .Concat(Directory.GetFiles(PluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly))
                    .ToArray();
                
                LogHelper.WriteLogToFile($"发现 {pluginFiles.Length} 个外部插件文件");
                
                foreach (var pluginFile in pluginFiles)
                {
                    LoadExternalPlugin(pluginFile);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载外部插件时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
        
        /// <summary>
        /// 加载单个外部插件
        /// </summary>
        /// <param name="pluginPath">插件文件路径</param>
        /// <returns>加载的插件实例，加载失败则返回null</returns>
        public IPlugin LoadExternalPlugin(string pluginPath)
        {
            try
            {
                // 计算文件哈希
                string fileHash = CalculateFileHash(pluginPath);
                _pluginHashes[pluginPath] = fileHash;
                
                // 检查文件扩展名
                string extension = Path.GetExtension(pluginPath).ToLowerInvariant();
                if (extension == ".iccpp")
                {
                    // 创建 ICCPP 插件适配器
                    return CreateICCPPPluginAdapter(pluginPath);
                }
                
                // 加载插件程序集
                Assembly pluginAssembly = LoadPluginAssembly(pluginPath);
                if (pluginAssembly == null) return null;
                
                // 查找实现了IPlugin接口的类型
                var pluginTypes = pluginAssembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass);
                
                foreach (var pluginType in pluginTypes)
                {
                    try
                    {
                        // 创建插件实例
                        IPlugin plugin = (IPlugin)Activator.CreateInstance(pluginType);
                        
                        // 设置插件路径
                        if (plugin is PluginBase pluginBase)
                        {
                            pluginBase.PluginPath = pluginPath;
                        }
                        
                        plugin.Initialize();
                        Plugins.Add(plugin);
                        
                        LogHelper.WriteLogToFile($"已加载外部插件: {plugin.Name} v{plugin.Version} 来自 {Path.GetFileName(pluginPath)}");
                        
                        return plugin;
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"实例化插件类型 {pluginType.Name} 时出错: {ex.Message}", LogHelper.LogType.Error);
                    }
                }
                
                LogHelper.WriteLogToFile($"在程序集 {Path.GetFileName(pluginPath)} 中未找到有效的插件类型", LogHelper.LogType.Warning);
                return null;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载外部插件 {Path.GetFileName(pluginPath)} 时出错: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }
        
        /// <summary>
        /// 创建 ICCPP 插件适配器
        /// </summary>
        /// <param name="pluginPath">插件文件路径</param>
        /// <returns>适配的插件实例</returns>
        private IPlugin CreateICCPPPluginAdapter(string pluginPath)
        {
            try
            {
                // 读取插件文件内容
                byte[] pluginData = File.ReadAllBytes(pluginPath);
                
                // 创建适配器插件实例
                var pluginAdapter = new ICCPPPluginAdapter(pluginPath, pluginData);
                
                // 添加到插件列表
                Plugins.Add(pluginAdapter);
                
                LogHelper.WriteLogToFile($"已创建 ICCPP 插件适配器: {pluginAdapter.Name} 来自 {Path.GetFileName(pluginPath)}");
                
                return pluginAdapter;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"创建 ICCPP 插件适配器时出错: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }
        
        /// <summary>
        /// 加载插件程序集
        /// </summary>
        /// <param name="pluginPath">插件文件路径</param>
        /// <returns>加载的程序集</returns>
        private Assembly LoadPluginAssembly(string pluginPath)
        {
            try
            {
                // 检查是否已加载该程序集
                if (_loadedAssemblies.TryGetValue(pluginPath, out var loadedAssembly))
                {
                    return loadedAssembly;
                }
                
                // 直接加载程序集
                Assembly pluginAssembly = Assembly.LoadFrom(pluginPath);
                _loadedAssemblies[pluginPath] = pluginAssembly;
                
                return pluginAssembly;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载插件程序集 {Path.GetFileName(pluginPath)} 时出错: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }
        
        /// <summary>
        /// 启用已配置为启用的插件
        /// </summary>
        private void EnableConfiguredPlugins()
        {
            int enabledCount = 0;
            int disabledCount = 0;
            int errorCount = 0;
            
            foreach (var plugin in Plugins)
            {
                try
                {
                    string pluginTypeName = plugin.GetType().FullName;
                    
                    // 检查配置中的插件状态
                    if (PluginStates.TryGetValue(pluginTypeName, out bool enabled))
                    {
                        // 获取当前实际状态
                        bool currentState = plugin is PluginBase pluginBase && pluginBase.IsEnabled;
                        
                        // 如果配置状态与当前状态不一致，则应用配置状态
                        if (currentState != enabled)
                        {
                            // 注册插件状态变更事件
                            if (plugin is PluginBase pb)
                            {
                                pb.EnabledStateChanged += Plugin_EnabledStateChanged;
                            }
                            
                            if (enabled)
                            {
                                plugin.Enable();
                                enabledCount++;
                                LogHelper.WriteLogToFile($"根据配置启用插件: {plugin.Name}");
                            }
                            else
                            {
                                plugin.Disable();
                                disabledCount++;
                                LogHelper.WriteLogToFile($"根据配置禁用插件: {plugin.Name}");
                            }
                        }
                        else
                        {
                            // 状态一致，只注册事件
                            if (plugin is PluginBase pb)
                            {
                                pb.EnabledStateChanged += Plugin_EnabledStateChanged;
                            }
                        }
                    }
                    else
                    {
                        // 插件不在配置中，添加默认状态（禁用）
                        PluginStates[pluginTypeName] = false;
                        _configDirty = true;
                        
                        // 注册插件状态变更事件
                        if (plugin is PluginBase pb)
                        {
                            pb.EnabledStateChanged += Plugin_EnabledStateChanged;
                        }
                        
                        // 如果当前是启用状态，则禁用
                        if (plugin is PluginBase pluginBase && pluginBase.IsEnabled)
                        {
                            plugin.Disable();
                            disabledCount++;
                            LogHelper.WriteLogToFile($"插件不在配置中，默认禁用: {plugin.Name}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    LogHelper.WriteLogToFile($"应用插件 {plugin.Name} 配置时出错: {ex.Message}", LogHelper.LogType.Error);
                }
            }
            
            // 如果有配置变更，启动自动保存
            if (_configDirty)
            {
                TriggerAutoSave();
            }
            
            LogHelper.WriteLogToFile($"已应用插件配置: 启用 {enabledCount} 个，禁用 {disabledCount} 个，错误 {errorCount} 个");
        }
        
        /// <summary>
        /// 插件状态变更事件处理
        /// </summary>
        private void Plugin_EnabledStateChanged(object sender, bool isEnabled)
        {
            try
            {
                if (sender is IPlugin plugin)
                {
                    string pluginTypeName = plugin.GetType().FullName;
                    
                    // 更新配置状态
                    if (!PluginStates.ContainsKey(pluginTypeName) || PluginStates[pluginTypeName] != isEnabled)
                    {
                        PluginStates[pluginTypeName] = isEnabled;
                        _configDirty = true;
                        
                        LogHelper.WriteLogToFile($"插件状态变更: {plugin.Name} = {(isEnabled ? "启用" : "禁用")}");
                        
                        // 立即同步保存配置（不再使用延迟自动保存）
                        SaveConfig();
                        LogHelper.WriteLogToFile($"插件 {plugin.Name} 状态已立即保存到配置文件");
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理插件状态变更事件时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
        
        /// <summary>
        /// 触发自动保存计时器
        /// </summary>
        private void TriggerAutoSave()
        {
            // 重置并启动计时器
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }
        
        /// <summary>
        /// 启动热重载监视器
        /// </summary>
        private void StartHotReloadWatcher()
        {
            // 创建定时检查任务
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        // 每5秒检查一次
                        await Task.Delay(5000);
                        
                        // 获取所有外部插件
                        var externalPlugins = Plugins.OfType<PluginBase>()
                            .Where(p => !p.IsBuiltIn && !string.IsNullOrEmpty(p.PluginPath))
                            .ToList();
                        
                        foreach (var plugin in externalPlugins)
                        {
                            // 检查插件文件是否存在
                            if (!File.Exists(plugin.PluginPath))
                            {
                                continue;
                            }
                            
                            // 计算当前文件哈希
                            string currentHash = CalculateFileHash(plugin.PluginPath);
                            
                            // 比较哈希值是否变化
                            if (_pluginHashes.TryGetValue(plugin.PluginPath, out string oldHash) && 
                                !string.IsNullOrEmpty(oldHash) && 
                                oldHash != currentHash)
                            {
                                // 文件已变化，执行热重载
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    ReloadPlugin(plugin);
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"热重载检查出错: {ex.Message}", LogHelper.LogType.Error);
                    }
                }
            });
        }
        
        /// <summary>
        /// 重新加载插件
        /// </summary>
        /// <param name="plugin">要重新加载的插件</param>
        private void ReloadPlugin(PluginBase plugin)
        {
            try
            {
                string pluginPath = plugin.PluginPath;
                if (string.IsNullOrEmpty(pluginPath) || !File.Exists(pluginPath))
                {
                    LogHelper.WriteLogToFile($"无法重新加载插件 {plugin.Name}: 插件文件不存在", LogHelper.LogType.Error);
                    return;
                }
                
                LogHelper.WriteLogToFile($"开始热重载插件: {plugin.Name} ({Path.GetFileName(pluginPath)})");
                
                // 保存插件的当前状态
                bool wasEnabled = plugin.IsEnabled;
                string pluginTypeName = plugin.GetType().FullName;
                
                // 卸载插件
                UnloadPlugin(plugin);
                
                // 从加载缓存中移除
                if (_loadedAssemblies.ContainsKey(pluginPath))
                {
                    _loadedAssemblies.Remove(pluginPath);
                }
                
                // 计算新的文件哈希
                string newHash = CalculateFileHash(pluginPath);
                _pluginHashes[pluginPath] = newHash;
                
                // 重新加载插件
                IPlugin newPlugin = LoadExternalPlugin(pluginPath);
                
                if (newPlugin != null)
                {
                    // 恢复插件状态
                    if (wasEnabled)
                    {
                        newPlugin.Enable();
                    }
                    
                    // 更新配置（如果类型名称变化）
                    string newPluginTypeName = newPlugin.GetType().FullName;
                    if (pluginTypeName != newPluginTypeName && PluginStates.ContainsKey(pluginTypeName))
                    {
                        bool state = PluginStates[pluginTypeName];
                        PluginStates.Remove(pluginTypeName);
                        PluginStates[newPluginTypeName] = state;
                        _configDirty = true;
                        SaveConfig();
                    }
                    
                    LogHelper.WriteLogToFile($"插件 {newPlugin.Name} v{newPlugin.Version} 热重载成功");
                    
                    // 通知UI刷新
                    NotifyUIRefresh();
                }
                else
                {
                    LogHelper.WriteLogToFile($"插件 {plugin.Name} 热重载失败", LogHelper.LogType.Error);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"重新加载插件 {plugin.Name} 时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
        
        /// <summary>
        /// 卸载插件
        /// </summary>
        /// <param name="plugin">要卸载的插件</param>
        /// <param name="removeFromConfig">是否从配置中移除</param>
        public void UnloadPlugin(IPlugin plugin, bool removeFromConfig = false)
        {
            try
            {
                // 保存插件名称，以便在卸载后使用
                string pluginName = plugin.Name;
                
                // 如果插件已启用，先禁用它
                if (plugin is PluginBase pluginBase && pluginBase.IsEnabled)
                {
                    plugin.Disable();
                }
                
                // 执行插件清理
                plugin.Cleanup();
                
                // 从插件集合中移除
                Plugins.Remove(plugin);
                
                // 从配置中移除（如果需要）
                if (removeFromConfig && plugin.GetType() != null)
                {
                    string pluginTypeName = plugin.GetType().FullName;
                    if (PluginStates.ContainsKey(pluginTypeName))
                    {
                        PluginStates.Remove(pluginTypeName);
                        SaveConfig();
                    }
                }
                
                LogHelper.WriteLogToFile($"已卸载插件: {pluginName}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"卸载插件时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
        
        /// <summary>
        /// 删除插件
        /// </summary>
        /// <param name="plugin">要删除的插件</param>
        /// <returns>删除是否成功</returns>
        public bool DeletePlugin(IPlugin plugin)
        {
            try
            {
                // 只能删除外部插件
                if (plugin.IsBuiltIn)
                {
                    return false;
                }
                
                // 保存插件名称，以便在删除后使用
                string pluginName = plugin.Name;
                
                // 获取插件路径
                string pluginPath = null;
                if (plugin is PluginBase pluginBase)
                {
                    pluginPath = pluginBase.PluginPath;
                }
                
                if (string.IsNullOrEmpty(pluginPath) || !File.Exists(pluginPath))
                {
                    return false;
                }
                
                // 卸载插件（并从配置中移除状态）
                UnloadPlugin(plugin, true);
                
                // 删除插件文件
                File.Delete(pluginPath);
                
                // 清理缓存
                _loadedAssemblies.Remove(pluginPath);
                _pluginHashes.Remove(pluginPath);
                
                // 保存配置
                SaveConfig();
                
                LogHelper.WriteLogToFile($"已删除插件: {pluginName}");
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"删除插件时出错: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }
        
        /// <summary>
        /// 切换插件启用状态
        /// </summary>
        /// <param name="plugin">目标插件</param>
        /// <param name="enable">是否启用</param>
        public void TogglePlugin(IPlugin plugin, bool enable)
        {
            try
            {
                // 检查当前状态是否已经是目标状态
                bool currentState = plugin is PluginBase pluginBase && pluginBase.IsEnabled;
                if (currentState == enable)
                {
                    // 已经是目标状态，无需操作
                    LogHelper.WriteLogToFile($"插件 {plugin.Name} 已经是 {(enable ? "启用" : "禁用")} 状态，无需切换");
                    return;
                }
                
                // 记录插件信息，用于日志
                string pluginName = plugin.Name;
                string pluginTypeName = plugin.GetType().FullName;
                
                LogHelper.WriteLogToFile($"开始切换插件 {pluginName} 状态为: {(enable ? "启用" : "禁用")}");
                
                // 首先更新配置状态
                PluginStates[pluginTypeName] = enable;
                _configDirty = true;
                
                // 更新插件状态
                try
                {
                    // 注册事件（无需检查事件是否为null）
                    if (plugin is PluginBase pb)
                    {
                        // 先取消可能已有的订阅，避免重复订阅
                        pb.EnabledStateChanged -= Plugin_EnabledStateChanged;
                        // 重新订阅
                        pb.EnabledStateChanged += Plugin_EnabledStateChanged;
                    }
                    
                    // 更新插件状态
                    if (enable)
                    {
                        plugin.Enable();
                        LogHelper.WriteLogToFile($"插件 {pluginName} 已启用");
                    }
                    else
                    {
                        // 禁用前先记录是否为内置插件
                        bool isBuiltIn = plugin.IsBuiltIn;
                        LogHelper.WriteLogToFile($"尝试禁用{(isBuiltIn ? "内置" : "外部")}插件 {pluginName}");
                        
                        // 禁用插件
                        plugin.Disable();
                        
                        // 禁用后立即检查状态，确保禁用成功
                        bool actuallyDisabled = !(plugin is PluginBase pb2 && pb2.IsEnabled);
                        if (!actuallyDisabled)
                        {
                            LogHelper.WriteLogToFile($"警告: 插件 {pluginName} 禁用失败，再次尝试禁用", LogHelper.LogType.Warning);
                            plugin.Disable(); // 再次尝试禁用
                            
                            // 再次检查
                            actuallyDisabled = !(plugin is PluginBase pb3 && pb3.IsEnabled);
                            if (!actuallyDisabled)
                            {
                                LogHelper.WriteLogToFile($"错误: 插件 {pluginName} 禁用失败，强制设置禁用状态", LogHelper.LogType.Error);
                                // 强制设置状态
                                if (plugin is PluginBase pb4)
                                {
                                    // 使用反射强制设置禁用状态
                                    var enabledProperty = typeof(PluginBase).GetProperty("IsEnabled");
                                    if (enabledProperty != null)
                                    {
                                        enabledProperty.SetValue(pb4, false);
                                        LogHelper.WriteLogToFile($"已通过反射强制设置插件 {pluginName} 为禁用状态");
                                    }
                                }
                            }
                        }
                        
                        LogHelper.WriteLogToFile($"插件 {pluginName} 已禁用");
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"更改插件 {pluginName} 状态时出错: {ex.Message}", LogHelper.LogType.Error);
                }
                
                // 立即保存配置
                SaveConfigAsync().ConfigureAwait(false);
                
                // 插件状态切换后，始终进行重载（无论是启用还是禁用）
                if (plugin is PluginBase pluginInstance)
                {
                    // 对于内置插件，执行专门的处理
                    if (pluginInstance.IsBuiltIn)
                    {
                        LogHelper.WriteLogToFile($"处理内置插件 {pluginName} 状态变更");
                        
                        // 对于内置插件，我们需要确保状态正确应用
                        bool finalState = pluginInstance.IsEnabled;
                        bool expectedState = enable;
                        
                        if (finalState != expectedState)
                        {
                            LogHelper.WriteLogToFile($"内置插件状态不匹配: 当前={finalState}, 期望={expectedState}，尝试纠正", LogHelper.LogType.Warning);
                            
                            // 再次尝试设置状态
                            if (expectedState)
                            {
                                plugin.Enable();
                            }
                            else
                            {
                                plugin.Disable();
                                
                                // 最后一次检查，如果仍然不匹配，强制设置
                                if (pluginInstance.IsEnabled != expectedState)
                                {
                                    var enabledProperty = typeof(PluginBase).GetProperty("IsEnabled");
                                    if (enabledProperty != null)
                                    {
                                        enabledProperty.SetValue(pluginInstance, expectedState);
                                        LogHelper.WriteLogToFile($"已通过反射强制设置内置插件 {pluginName} 状态为 {(expectedState ? "启用" : "禁用")}");
                                    }
                                }
                            }
                        }
                        
                        // 通知UI刷新
                        NotifyUIRefresh();
                    }
                    else
                    {
                        // 外部插件，执行热重载
                        try
                        {
                            if (!string.IsNullOrEmpty(pluginInstance.PluginPath) && File.Exists(pluginInstance.PluginPath))
                            {
                                LogHelper.WriteLogToFile($"开始重载外部插件 {pluginName}");
                                
                                // 使用调度器确保在UI线程执行热重载
                                if (Application.Current != null && Application.Current.Dispatcher != null)
                                {
                                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        ReloadPlugin(pluginInstance);
                                        LogHelper.WriteLogToFile($"插件 {pluginName} 已重载以应用{(enable ? "启用" : "禁用")}状态");
                                    }));
                                }
                                else
                                {
                                    // 当前不在UI线程，直接重载
                                    ReloadPlugin(pluginInstance);
                                    LogHelper.WriteLogToFile($"插件 {pluginName} 已重载以应用{(enable ? "启用" : "禁用")}状态");
                                }
                            }
                            else
                            {
                                LogHelper.WriteLogToFile($"外部插件 {pluginName} 文件不存在，无法重载", LogHelper.LogType.Warning);
                                NotifyUIRefresh();
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"重载插件 {pluginName} 时出错: {ex.Message}", LogHelper.LogType.Error);
                            // 出错时也要刷新UI
                            NotifyUIRefresh();
                        }
                    }
                }
                else
                {
                    // 通知UI刷新
                    NotifyUIRefresh();
                }
                
                LogHelper.WriteLogToFile($"插件 {pluginName} 状态切换完成");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换插件状态时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
        
        /// <summary>
        /// 应用插件实时状态
        /// </summary>
        /// <param name="plugin">目标插件</param>
        /// <param name="enable">是否启用</param>
        private void ApplyPluginRealTimeState(IPlugin plugin, bool enable)
        {
            try
            {
                // 确保当前实例状态正确
                bool currentState = plugin is PluginBase pluginBase && pluginBase.IsEnabled;
                if (currentState != enable)
                {
                    if (enable)
                    {
                        plugin.Enable();
                        LogHelper.WriteLogToFile($"实时应用: 已启用插件 {plugin.Name}");
                    }
                    else
                    {
                        plugin.Disable();
                        LogHelper.WriteLogToFile($"实时应用: 已禁用插件 {plugin.Name}");
                    }
                    
                    // 同步状态到插件自身的配置
                    if (plugin is PluginBase pluginSettings)
                    {
                        try
                        {
                            // 保存插件设置（与启用状态无关）
                            pluginSettings.SavePluginSettings();
                            LogHelper.WriteLogToFile($"实时应用: 已保存插件 {plugin.Name} 设置");
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"实时应用: 保存插件 {plugin.Name} 设置时出错: {ex.Message}", LogHelper.LogType.Error);
                        }
                    }
                }
                
                // 对于外部插件，尝试执行热重载以确保状态立即生效
                if (plugin is PluginBase externalPlugin && !externalPlugin.IsBuiltIn)
                {
                    string pluginPath = externalPlugin.PluginPath;
                    if (!string.IsNullOrEmpty(pluginPath) && File.Exists(pluginPath))
                    {
                        // 记录插件类型名称，用于后续状态检查
                        string pluginTypeName = plugin.GetType().FullName;
                        bool targetState = enable;
                        
                        // 使用调度器确保在UI线程执行热重载
                        if (Application.Current != null && Application.Current.Dispatcher != null)
                        {
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    // 热重载前再次确认配置状态正确
                                    if (PluginStates.TryGetValue(pluginTypeName, out bool storedStateUi) && storedStateUi != targetState)
                                    {
                                        LogHelper.WriteLogToFile($"热重载前发现状态不一致，修正配置: {plugin.Name}, 配置={storedStateUi}, 目标={targetState}", LogHelper.LogType.Warning);
                                        PluginStates[pluginTypeName] = targetState;
                                        SaveConfig();
                                    }
                                    
                                    // 执行热重载
                                    ReloadPlugin(externalPlugin);
                                    LogHelper.WriteLogToFile($"插件 {plugin.Name} 已成功热重载以应用实时状态");
                                }
                                catch (Exception ex)
                                {
                                    LogHelper.WriteLogToFile($"热重载插件 {plugin.Name} 时出错: {ex.Message}", LogHelper.LogType.Error);
                                }
                            }));
                        }
                        else
                        {
                            // 当前不在UI线程，直接重载
                            // 热重载前再次确认配置状态正确
                            if (PluginStates.TryGetValue(pluginTypeName, out bool storedStateNonUi) && storedStateNonUi != targetState)
                            {
                                LogHelper.WriteLogToFile($"热重载前发现状态不一致，修正配置: {plugin.Name}, 配置={storedStateNonUi}, 目标={targetState}", LogHelper.LogType.Warning);
                                PluginStates[pluginTypeName] = targetState;
                                SaveConfig();
                            }
                            
                            ReloadPlugin(externalPlugin);
                        }
                    }
                }
                
                LogHelper.WriteLogToFile($"插件 {plugin.Name} 实时状态已应用: {(enable ? "启用" : "禁用")}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用插件实时状态时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
        
        /// <summary>
        /// 通知UI刷新
        /// </summary>
        private void NotifyUIRefresh()
        {
            try
            {
                // 通知UI刷新
                if (Application.Current != null && Application.Current.Dispatcher != null)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                        // 通知任何可能打开的插件设置窗口刷新
                        foreach (Window window in Application.Current.Windows)
                        {
                            if (window is PluginSettingsWindow pluginWindow)
                            {
                                pluginWindow.RefreshPluginList();
                                break;
                            }
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"通知UI刷新时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
        
        /// <summary>
        /// 加载插件配置
        /// </summary>
        private void LoadConfig()
        {
            const int maxRetries = 3; // 最大重试次数
            const int retryDelayMs = 300; // 重试延迟时间(毫秒)
            
            LogHelper.WriteLogToFile($"开始从配置文件加载插件状态: {PluginConfigFile}");
            
            // 确保至少有一个默认配置
            Dictionary<string, bool> defaultConfig = new Dictionary<string, bool>();
            
            // 尝试获取配置锁
            _configLock.Wait();
            
            try
            {
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        if (File.Exists(PluginConfigFile))
                        {
                            string json;
                            // 使用共享读取模式，允许其他进程同时读取但不允许写入
                            using (FileStream fs = new FileStream(PluginConfigFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                            using (StreamReader reader = new StreamReader(fs))
                            {
                                json = reader.ReadToEnd();
                            }
                            
                            var loadedStates = JsonConvert.DeserializeObject<Dictionary<string, bool>>(json);
                            
                            if (loadedStates != null && loadedStates.Count > 0)
                            {
                                PluginStates = loadedStates;
                                _configDirty = false; // 重置脏标记
                                LogHelper.WriteLogToFile($"成功从配置文件加载了 {PluginStates.Count} 个插件状态");
                            }
                            else
                            {
                                LogHelper.WriteLogToFile("配置文件解析为空，尝试使用备份", LogHelper.LogType.Warning);
                                // 尝试加载备份
                                if (File.Exists(PluginConfigBackupFile))
                                {
                                    try
                                    {
                                        string backupJson = File.ReadAllText(PluginConfigBackupFile);
                                        var backupStates = JsonConvert.DeserializeObject<Dictionary<string, bool>>(backupJson);
                                        
                                        if (backupStates != null && backupStates.Count > 0)
                                        {
                                            PluginStates = backupStates;
                                            _configDirty = true; // 从备份加载，需要重新保存主配置
                                            LogHelper.WriteLogToFile($"已从备份恢复 {PluginStates.Count} 个插件状态");
                                            return; // 成功从备份加载，提前退出
                                        }
                                    }
                                    catch (Exception backupEx)
                                    {
                                        LogHelper.WriteLogToFile($"从备份恢复配置失败: {backupEx.Message}", LogHelper.LogType.Error);
                                    }
                                }
                                
                                // 备份也失败，使用默认配置
                                PluginStates = defaultConfig;
                                _configDirty = true;
                            }
                        }
                        else
                        {
                            LogHelper.WriteLogToFile($"配置文件不存在，尝试使用备份: {PluginConfigFile}", LogHelper.LogType.Warning);
                            
                            // 尝试加载备份
                            if (File.Exists(PluginConfigBackupFile))
                            {
                                try
                                {
                                    string backupJson = File.ReadAllText(PluginConfigBackupFile);
                                    var backupStates = JsonConvert.DeserializeObject<Dictionary<string, bool>>(backupJson);
                                    
                                    if (backupStates != null && backupStates.Count > 0)
                                    {
                                        PluginStates = backupStates;
                                        _configDirty = true; // 从备份加载，需要重新保存主配置
                                        LogHelper.WriteLogToFile($"已从备份恢复 {PluginStates.Count} 个插件状态");
                                        return; // 成功从备份加载，提前退出
                                    }
                                }
                                catch (Exception backupEx)
                                {
                                    LogHelper.WriteLogToFile($"从备份恢复配置失败: {backupEx.Message}", LogHelper.LogType.Error);
                                }
                            }
                            
                            PluginStates = defaultConfig;
                            _configDirty = true;
                            LogHelper.WriteLogToFile("使用默认空配置", LogHelper.LogType.Warning);
                        }
                        
                        // 没有成功加载或使用备份，使用默认配置
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (attempt < maxRetries)
                        {
                            LogHelper.WriteLogToFile($"加载配置失败 (尝试 {attempt}/{maxRetries}): {ex.Message}，将在 {retryDelayMs}ms 后重试", LogHelper.LogType.Warning);
                            Thread.Sleep(retryDelayMs);
                        }
                        else
                        {
                            LogHelper.WriteLogToFile($"加载插件配置失败，已达最大重试次数 ({maxRetries}): {ex.Message}", LogHelper.LogType.Error);
                            
                            // 最终失败，使用默认配置
                            PluginStates = defaultConfig;
                            _configDirty = true;
                        }
                    }
                }
            }
            finally
            {
                // 释放配置锁
                _configLock.Release();
            }
        }
        
        /// <summary>
        /// 异步保存插件配置
        /// </summary>
        public async Task SaveConfigAsync()
        {
            // 如果配置没有变化，无需保存
            if (!_configDirty)
            {
                return;
            }
            
            // 尝试获取配置锁（异步）
            if (!await _configLock.WaitAsync(0))
            {
                // 已有保存操作在进行中，触发自动保存延迟
                TriggerAutoSave();
                return;
            }
            
            try
            {
                // 创建配置任务
                await Task.Run(() => SaveConfig());
            }
            finally
            {
                // 释放配置锁
                _configLock.Release();
            }
        }
        
        /// <summary>
        /// 保存插件配置
        /// </summary>
        public void SaveConfig()
        {
            // 如果配置没有变化，无需保存
            if (!_configDirty)
            {
                return;
            }
            
            const int maxRetries = 3; // 最大重试次数
            const int retryDelayMs = 500; // 重试延迟时间(毫秒)
            
            try
            {
                LogHelper.WriteLogToFile($"开始保存插件配置到: {PluginConfigFile}");
                
                // 生成JSON数据
                string json = JsonConvert.SerializeObject(PluginStates, Formatting.Indented);
                string tempFile = PluginConfigFile + ".temp"; // 临时文件路径
                
                // 确保目录存在
                string configDir = Path.GetDirectoryName(PluginConfigFile);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                    LogHelper.WriteLogToFile($"创建配置目录: {configDir}");
                }
                
                // 先备份当前配置
                try
                {
                    if (File.Exists(PluginConfigFile))
                    {
                        File.Copy(PluginConfigFile, PluginConfigBackupFile, true);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"备份配置文件失败: {ex.Message}", LogHelper.LogType.Warning);
                }
                
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        // 直接写入目标文件
                        File.WriteAllText(PluginConfigFile, json);
                        
                        // 验证写入是否成功
                        if (File.Exists(PluginConfigFile))
                        {
                            // 重置脏标记
                            _configDirty = false;
                            LogHelper.WriteLogToFile($"插件配置已成功保存到磁盘: {PluginConfigFile}, 共 {PluginStates.Count} 个插件状态");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (attempt < maxRetries)
                        {
                            LogHelper.WriteLogToFile($"保存配置失败 (尝试 {attempt}/{maxRetries}): {ex.Message}，将在 {retryDelayMs}ms 后重试", LogHelper.LogType.Warning);
                            Thread.Sleep(retryDelayMs);
                        }
                        else
                        {
                            LogHelper.WriteLogToFile($"保存插件配置失败，已达最大重试次数 ({maxRetries}): {ex.Message}", LogHelper.LogType.Error);
                            
                            // 尝试使用临时文件方式
                            try
                            {
                                // 删除可能存在的旧临时文件
                                if (File.Exists(tempFile))
                                {
                                    File.Delete(tempFile);
                                }
                                
                                // 写入临时文件
                                File.WriteAllText(tempFile, json);
                                
                                // 如果目标文件存在，先删除
                                if (File.Exists(PluginConfigFile))
                                {
                                    File.Delete(PluginConfigFile);
                                }
                                
                                // 重命名临时文件
                                File.Move(tempFile, PluginConfigFile);
                                
                                // 重置脏标记
                                _configDirty = false;
                                LogHelper.WriteLogToFile($"使用临时文件方式成功保存配置: {PluginConfigFile}");
                                return;
                            }
                            catch (Exception fallbackEx)
                            {
                                LogHelper.WriteLogToFile($"临时文件保存方式也失败: {fallbackEx.Message}", LogHelper.LogType.Error);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存插件配置时发生未处理异常: {ex.Message}", LogHelper.LogType.Error);
            }
        }
        
        /// <summary>
        /// 计算文件哈希
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件哈希值</returns>
        private string CalculateFileHash(string filePath)
        {
            try
            {
                using (var md5 = MD5.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"计算文件哈希值时出错: {ex.Message}", LogHelper.LogType.Error);
                return string.Empty;
            }
        }

        /// <summary>
        /// 从配置文件重新加载所有插件状态并应用
        /// </summary>
        public void ReloadPluginsFromConfig()
        {
            try
            {
                LogHelper.WriteLogToFile("开始从配置文件重新加载插件状态");
                
                // 保存当前配置状态，以便在加载失败时回滚
                Dictionary<string, bool> previousStates = new Dictionary<string, bool>(PluginStates);
                
                // 重新加载配置文件
                LoadConfig();
                
                // 如果配置文件加载失败，PluginStates可能为空，这时使用之前的状态
                if (PluginStates == null || PluginStates.Count == 0)
                {
                    LogHelper.WriteLogToFile("加载的配置为空，恢复到之前的状态", LogHelper.LogType.Warning);
                    PluginStates = previousStates;
                    return;
                }
                
                LogHelper.WriteLogToFile($"已加载 {PluginStates.Count} 个插件状态，开始应用...");
                
                // 对比配置，查找变更的插件
                foreach (var plugin in Plugins.ToList()) // 创建副本进行遍历，避免集合修改异常
                {
                    string pluginTypeName = plugin.GetType().FullName;
                    
                    // 检查插件在配置中是否存在
                    if (PluginStates.TryGetValue(pluginTypeName, out bool shouldBeEnabled))
                    {
                        bool currentlyEnabled = plugin is PluginBase pluginBase && pluginBase.IsEnabled;
                        
                        // 如果状态需要变更
                        if (currentlyEnabled != shouldBeEnabled)
                        {
                            LogHelper.WriteLogToFile($"应用插件 {plugin.Name} 的配置状态: {(shouldBeEnabled ? "启用" : "禁用")}");
                            
                            if (shouldBeEnabled)
                            {
                                try
                                {
                                    plugin.Enable();
                                }
                                catch (Exception ex)
                                {
                                    LogHelper.WriteLogToFile($"启用插件 {plugin.Name} 时出错: {ex.Message}", LogHelper.LogType.Error);
                                }
                            }
                            else
                            {
                                try
                                {
                                    // 记录禁用信息，特别是内置插件
                                    bool isBuiltIn = plugin.IsBuiltIn;
                                    LogHelper.WriteLogToFile($"尝试禁用{(isBuiltIn ? "内置" : "外部")}插件 {plugin.Name}");
                                    
                                    // 禁用插件
                                    plugin.Disable();
                                    
                                    // 对于内置插件，特别检查禁用状态
                                    if (isBuiltIn && plugin is PluginBase builtInPluginBase)
                                    {
                                        if (builtInPluginBase.IsEnabled)
                                        {
                                            LogHelper.WriteLogToFile($"内置插件 {plugin.Name} 禁用失败，尝试强制禁用", LogHelper.LogType.Warning);
                                            // 强制设置禁用状态
                                            var enabledProperty = typeof(PluginBase).GetProperty("IsEnabled");
                                            if (enabledProperty != null)
                                            {
                                                enabledProperty.SetValue(builtInPluginBase, false);
                                                LogHelper.WriteLogToFile($"已通过反射强制禁用内置插件 {plugin.Name}");
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogHelper.WriteLogToFile($"禁用插件 {plugin.Name} 时出错: {ex.Message}", LogHelper.LogType.Error);
                                }
                            }
                            
                            // 如果是外部插件，执行重载
                            if (!plugin.IsBuiltIn && plugin is PluginBase externalPlugin && !string.IsNullOrEmpty(externalPlugin.PluginPath))
                            {
                                try
                                {
                                    ReloadPlugin(externalPlugin);
                                }
                                catch (Exception ex)
                                {
                                    LogHelper.WriteLogToFile($"重载外部插件 {plugin.Name} 时出错: {ex.Message}", LogHelper.LogType.Error);
                                }
                            }
                        }
                    }
                    else
                    {
                        // 插件不在配置中，将其添加为禁用状态
                        PluginStates[pluginTypeName] = false;
                        LogHelper.WriteLogToFile($"插件 {plugin.Name} 不在配置中，默认设置为禁用状态");
                        
                        // 如果当前是启用状态，则禁用它
                        if (plugin is PluginBase pluginBase && pluginBase.IsEnabled)
                        {
                            try
                            {
                                bool isBuiltIn = plugin.IsBuiltIn;
                                LogHelper.WriteLogToFile($"尝试禁用未配置的{(isBuiltIn ? "内置" : "外部")}插件 {plugin.Name}");
                                
                                plugin.Disable();
                                
                                // 对于内置插件，特别检查禁用状态
                                if (isBuiltIn && pluginBase.IsEnabled)
                                {
                                    LogHelper.WriteLogToFile($"未配置的内置插件 {plugin.Name} 禁用失败，尝试强制禁用", LogHelper.LogType.Warning);
                                    // 强制设置禁用状态
                                    var enabledProperty = typeof(PluginBase).GetProperty("IsEnabled");
                                    if (enabledProperty != null)
                                    {
                                        enabledProperty.SetValue(pluginBase, false);
                                        LogHelper.WriteLogToFile($"已通过反射强制禁用未配置的内置插件 {plugin.Name}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"禁用未配置插件 {plugin.Name} 时出错: {ex.Message}", LogHelper.LogType.Error);
                            }
                        }
                    }
                }
                
                // 保存更新后的配置
                SaveConfig();
                
                // 通知UI更新
                if (Application.Current != null && Application.Current.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() => {
                        // 通知任何可能打开的插件设置窗口刷新
                        foreach (Window window in Application.Current.Windows)
                        {
                            if (window is PluginSettingsWindow pluginWindow)
                            {
                                pluginWindow.RefreshPluginList();
                            }
                        }
                    });
                }
                
                LogHelper.WriteLogToFile("插件状态已从配置文件重新加载完成");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"从配置文件重新加载插件状态时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
    }
} 