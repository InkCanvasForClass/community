using Ink_Canvas.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;
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
        private readonly ServiceCollection _serviceCollection = new ServiceCollection();
        private ServiceProvider _serviceProvider;
        private readonly string _pluginsDirectory;
        private readonly string _pluginPackagesDirectory;
        private readonly string _pluginConfigsDirectory;
        private readonly List<PluginInfo> _plugins = new List<PluginInfo>();
        private readonly Dictionary<string, PluginLoadContext> _assemblyContexts = new Dictionary<string, PluginLoadContext>();
        private readonly HashSet<string> _disabledPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly string _disabledPluginsFile;
        private readonly string _pluginLogsDirectory;

        // 子服务
        private readonly PluginErrorRecoveryService _errorRecovery;
        private readonly PluginDependencyResolver _dependencyResolver = new PluginDependencyResolver();
        private readonly PluginConfigIo _configIo;
        private PluginSecurityCheck _securityCheck;
        private PluginLogger _logger;
        private PluginIpcService _ipc;

        public static readonly string ManifestFileName = "manifest.json";
        public static readonly string PluginPackageExtension = ".icpx";

        /// <summary>
        /// 已禁用的插件 ID 列表。
        /// </summary>
        public IReadOnlyCollection<string> DisabledPlugins => _disabledPlugins;

        public IReadOnlyList<PluginInfo> Plugins
        {
            get { return _plugins.AsReadOnly(); }
        }

        public event EventHandler<PluginInfo> PluginLoaded;
        public event EventHandler<PluginInfo> PluginUnloaded;
        public event EventHandler<string> LogMessage;

        private PluginManager()
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            _pluginsDirectory = Path.Combine(basePath, "Plugins");
            _pluginPackagesDirectory = Path.Combine(basePath, "PluginPackages");
            _pluginConfigsDirectory = Path.Combine(basePath, "PluginConfigs");
            _disabledPluginsFile = Path.Combine(basePath, "Configs", "disabled_plugins.json");
            _pluginLogsDirectory = Path.Combine(basePath, "PluginLogs");

            EnsureDirectoryExists(_pluginsDirectory);
            EnsureDirectoryExists(_pluginPackagesDirectory);
            EnsureDirectoryExists(_pluginConfigsDirectory);
            EnsureDirectoryExists(_pluginLogsDirectory);
            LoadDisabledPlugins();

            _errorRecovery = new PluginErrorRecoveryService(basePath);
            _configIo = new PluginConfigIo();
            _logger = new PluginLogger(_pluginLogsDirectory, "host");
        }

        /// <summary>
        /// 待外部在市场服务初始化后注入。
        /// </summary>
        public void InitializeAdvancedServices(PluginMarketService market)
        {
            _securityCheck = new PluginSecurityCheck(market);
        }

        /// <summary>
        /// 启动 IPC 总线。可由 MainWindow 在适当时机调用一次。
        /// </summary>
        public void StartIpc()
        {
            if (_ipc == null)
            {
                _ipc = new PluginIpcService();
            }
            _ipc.Start();
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        #region 禁用插件管理

        private void LoadDisabledPlugins()
        {
            try
            {
                if (!File.Exists(_disabledPluginsFile)) return;
                var json = File.ReadAllText(_disabledPluginsFile);
                var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                if (list != null)
                {
                    _disabledPlugins.Clear();
                    foreach (var id in list) _disabledPlugins.Add(id);
                }
            }
            catch { }
        }

        private void SaveDisabledPlugins()
        {
            try
            {
                var dir = Path.GetDirectoryName(_disabledPluginsFile);
                if (dir != null) EnsureDirectoryExists(dir);
                File.WriteAllText(_disabledPluginsFile,
                    System.Text.Json.JsonSerializer.Serialize(_disabledPlugins.ToList(),
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        /// <summary>
        /// 禁用插件（下次启动生效）。
        /// </summary>
        public void DisablePlugin(string pluginId)
        {
            _disabledPlugins.Add(pluginId);
            SaveDisabledPlugins();
        }

        /// <summary>
        /// 启用已禁用的插件（下次启动生效）。
        /// </summary>
        public void EnablePlugin(string pluginId)
        {
            _disabledPlugins.Remove(pluginId);
            SaveDisabledPlugins();
        }

        /// <summary>
        /// 检查插件是否被禁用。
        /// </summary>
        public bool IsPluginDisabled(string pluginId)
        {
            return _disabledPlugins.Contains(pluginId);
        }

        #endregion

        #region 插件独立日志

        /// <summary>
        /// 写入插件独立日志。
        /// </summary>
        public void LogPlugin(string pluginId, string level, string message)
        {
            try
            {
                var logFile = Path.Combine(_pluginLogsDirectory, pluginId + ".log");
                var line = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] {2}{3}",
                    DateTime.Now, level, message, Environment.NewLine);
                File.AppendAllText(logFile, line);
            }
            catch { }
        }

        /// <summary>
        /// 获取插件日志文件路径。
        /// </summary>
        public string GetPluginLogPath(string pluginId)
        {
            return Path.Combine(_pluginLogsDirectory, pluginId + ".log");
        }

        #endregion

        public async Task LoadAllAsync()
        {
            try
            {
                // 0. 清理标记为卸载的插件目录
                CleanupUninstalledPlugins();

                // 1. 处理待安装的 .icpx 插件包
                ProcessPluginPackages();

                // 2. 扫描插件目录，加载清单
                DiscoverPlugins();

                // 3. 解析依赖顺序
                var loadOrder = ResolveLoadOrder();

                // 4. 按顺序加载插件
                foreach (var pluginId in loadOrder)
                {
                    var info = _plugins.FirstOrDefault(p => p.Id == pluginId);
                    if (info == null || info.LoadStatus != PluginLoadStatus.NotLoaded) continue;

                    // 跳过已禁用的插件
                    if (IsPluginDisabled(pluginId))
                    {
                        info.LoadStatus = PluginLoadStatus.Disabled;
                        Log(string.Format("Plugin {0} is disabled, skipping", info.Name));
                        continue;
                    }

                    try
                    {
                        LoadPlugin(info);
                    }
                    catch (Exception ex)
                    {
                        info.LoadStatus = PluginLoadStatus.Error;
                        info.Exception = ex;
                        LogError(string.Format("Failed to load plugin {0}", info.Name), ex);
                    }
                }

                _plugins.Sort((a, b) => a.Order.CompareTo(b.Order));
                BuildServiceProvider();
                Log(string.Format("Plugin loading complete. Loaded {0} plugins", _plugins.Count(p => p.LoadStatus == PluginLoadStatus.Loaded)));
            }
            catch (Exception ex)
            {
                LogError("Failed to load plugins", ex);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 安装 PluginPackages 中待安装的插件包并立即加载。可在运行时调用。
        /// </summary>
        public void InstallPendingPackages()
        {
            ProcessPluginPackages();
            DiscoverPlugins();
            var loadOrder = ResolveLoadOrder();
            foreach (var pluginId in loadOrder)
            {
                var info = _plugins.FirstOrDefault(p => p.Id == pluginId && p.LoadStatus == PluginLoadStatus.NotLoaded);
                if (info == null) continue;
                try { LoadPlugin(info); }
                catch (Exception ex)
                {
                    info.LoadStatus = PluginLoadStatus.Error;
                    info.Exception = ex;
                    LogError(string.Format("Failed to load plugin {0}", info.Name), ex);
                }
            }
            _plugins.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        /// <summary>
        /// 清理标记为 .uninstall 的插件目录（上次卸载时 DLL 被锁定，本次启动时清理）。
        /// </summary>
        private void CleanupUninstalledPlugins()
        {
            if (!Directory.Exists(_pluginsDirectory)) return;

            foreach (var subDir in Directory.GetDirectories(_pluginsDirectory))
            {
                var marker = Path.Combine(subDir, ".uninstall");
                if (!File.Exists(marker)) continue;

                try
                {
                    // 释放门控锁后删除
                    ProcessProtectionManager.ReleaseLocksForPath(subDir);
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                    GC.WaitForPendingFinalizers();

                    Directory.Delete(subDir, true);

                    Log(string.Format("Cleaned up uninstalled plugin: {0}", Path.GetFileName(subDir)));
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"PluginManager | CleanupUninstalled: {Path.GetFileName(subDir)} - 删除失败: {ex.Message}", LogHelper.LogType.Error);
                }
            }
        }

        #region Plugin Package Installation

        /// <summary>
        /// 处理 PluginPackages 目录中的 .icpx 插件包，将其解压安装到 Plugins 目录。
        /// </summary>
        private void ProcessPluginPackages()
        {
            if (!Directory.Exists(_pluginPackagesDirectory)) return;

            foreach (var pkgPath in Directory.GetFiles(_pluginPackagesDirectory)
                .Where(x => Path.GetExtension(x).Equals(PluginPackageExtension, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    using var pkg = ZipFile.OpenRead(pkgPath);
                    var manifestEntry = pkg.GetEntry(ManifestFileName);
                    if (manifestEntry == null)
                    {
                        Log(string.Format("Package {0} missing manifest.json, skipping", Path.GetFileName(pkgPath)));
                        continue;
                    }

                    string manifestText;
                    using (var reader = new StreamReader(manifestEntry.Open()))
                    {
                        manifestText = reader.ReadToEnd();
                    }

                    var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestText);
                    if (manifest == null || string.IsNullOrEmpty(manifest.Id))
                    {
                        Log(string.Format("Package {0} has invalid manifest, skipping", Path.GetFileName(pkgPath)));
                        continue;
                    }

                    var targetPath = Path.Combine(_pluginsDirectory, manifest.Id);
                    if (Directory.Exists(targetPath))
                    {
                        // 释放门控锁后删除旧版本
                        ProcessProtectionManager.ReleaseLocksForPath(targetPath);
                        Directory.Delete(targetPath, true);
                    }
                    Directory.CreateDirectory(targetPath);
                    ZipFile.ExtractToDirectory(pkgPath, targetPath);

                    Log(string.Format("Installed plugin package: {0} v{1}", manifest.Name, manifest.Version));
                }
                catch (Exception ex)
                {
                    LogError(string.Format("Error installing package {0}", Path.GetFileName(pkgPath)), ex);
                }
                finally
                {
                    try { File.Delete(pkgPath); } catch { }
                }
            }
        }

        #endregion

        #region Plugin Discovery

        /// <summary>
        /// 扫描 Plugins 目录下的子目录，解析 manifest.json 发现插件。
        /// 同时兼容旧的 DLL 直接放置方式（无 manifest）。
        /// </summary>
        private void DiscoverPlugins()
        {
            var loadedIds = new HashSet<string>();

            // 1. 扫描带 manifest.json 的插件目录
            foreach (var subDir in Directory.GetDirectories(_pluginsDirectory))
            {
                // 跳过标记为待卸载的插件
                if (File.Exists(Path.Combine(subDir, ".uninstall")))
                    continue;

                var manifestPath = Path.Combine(subDir, ManifestFileName);
                if (!File.Exists(manifestPath)) continue;

                try
                {
                    var manifestText = File.ReadAllText(manifestPath);
                    var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestText);
                    if (manifest == null || string.IsNullOrEmpty(manifest.Id)) continue;

                    if (loadedIds.Contains(manifest.Id)) continue;
                    loadedIds.Add(manifest.Id);

                    var info = new PluginInfo
                    {
                        Id = manifest.Id,
                        Name = manifest.Name,
                        Version = manifest.Version,
                        Description = manifest.Description,
                        Author = manifest.Author,
                        Manifest = manifest,
                        PluginFolderPath = Path.GetFullPath(subDir),
                        PluginConfigFolder = Path.Combine(_pluginConfigsDirectory, manifest.Id),
                        LoadStatus = PluginLoadStatus.NotLoaded
                    };

                    EnsureDirectoryExists(info.PluginConfigFolder);
                    _plugins.Add(info);
                    Log(string.Format("Discovered plugin: {0} v{1}", manifest.Name, manifest.Version));
                }
                catch (Exception ex)
                {
                    LogError(string.Format("Error reading manifest in {0}", Path.GetFileName(subDir)), ex);
                }
            }

            // 2. 兼容旧方式：扫描 Plugins 根目录下的 DLL（无 manifest）
            foreach (var dllFile in Directory.GetFiles(_pluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    // 尝试从 DLL 中获取插件信息
                    var tempContext = new PluginLoadContext(dllFile, null);
                    var assembly = tempContext.LoadFromAssemblyPath(dllFile);
                    var pluginType = FindPluginEntrance(assembly);
                    if (pluginType == null)
                    {
                        tempContext.Unload();
                        continue;
                    }

                    var tempInstance = Activator.CreateInstance(pluginType) as IPlugin;
                    if (tempInstance == null || loadedIds.Contains(tempInstance.Id))
                    {
                        tempContext.Unload();
                        continue;
                    }

                    loadedIds.Add(tempInstance.Id);

                    var info = new PluginInfo
                    {
                        Id = tempInstance.Id,
                        Name = tempInstance.Name,
                        Version = tempInstance.Version,
                        Description = tempInstance.Description,
                        Author = tempInstance.Author,
                        Order = tempInstance.Order,
                        PluginFolderPath = Path.GetDirectoryName(dllFile),
                        PluginConfigFolder = Path.Combine(_pluginConfigsDirectory, tempInstance.Id),
                        LoadStatus = PluginLoadStatus.NotLoaded
                    };

                    EnsureDirectoryExists(info.PluginConfigFolder);
                    _plugins.Add(info);
                    Log(string.Format("Discovered legacy plugin: {0} v{1}", info.Name, info.Version));
                    tempContext.Unload();
                }
                catch (Exception ex)
                {
                    LogError(string.Format("Error scanning DLL {0}", Path.GetFileName(dllFile)), ex);
                }
            }
        }

        #endregion

        #region Dependency Resolution

        /// <summary>
        /// 解析插件加载顺序，基于依赖关系进行拓扑排序。
        /// </summary>
        private List<string> ResolveLoadOrder()
        {
            var plugins = _plugins.Where(p => p.LoadStatus == PluginLoadStatus.NotLoaded).ToList();
            var nodes = plugins.ToDictionary(p => p.Id, p => new DependencyNode(p));

            foreach (var node in nodes)
            {
                ResolveDependencyNode(nodes, node.Value, new List<DependencyNode>());
            }

            return nodes
                .Where(x => x.Value.Plugin.LoadStatus == PluginLoadStatus.NotLoaded)
                .OrderBy(x => x.Value.Depth)
                .Select(x => x.Key)
                .ToList();
        }

        private void ResolveDependencyNode(Dictionary<string, DependencyNode> allNodes, DependencyNode node, List<DependencyNode> walking)
        {
            if (node.IsDiscovered) return;

            if (walking.Contains(node))
            {
                node.Plugin.LoadStatus = PluginLoadStatus.Error;
                node.Plugin.Exception = new InvalidOperationException(
                    string.Format("Circular dependency detected: {0}", string.Join(" -> ", walking.Select(x => x.Plugin.Id))));
                return;
            }

            node.IsDiscovered = true;
            var depth = 0;

            if (node.Plugin.Manifest?.Dependencies != null)
            {
                foreach (var dep in node.Plugin.Manifest.Dependencies)
                {
                    if (!allNodes.TryGetValue(dep.Id, out var depNode) || depNode.Plugin.LoadStatus != PluginLoadStatus.NotLoaded)
                    {
                        if (dep.IsRequired)
                        {
                            node.Plugin.LoadStatus = PluginLoadStatus.Error;
                            node.Plugin.Exception = new InvalidOperationException(
                                string.Format("Plugin {0} requires missing dependency {1}", node.Plugin.Id, dep.Id));
                            return;
                        }
                        continue;
                    }

                    ResolveDependencyNode(allNodes, depNode, walking);
                    depth = Math.Max(depth, depNode.Depth);
                }
            }

            node.Depth = depth + 1;
        }

        private class DependencyNode
        {
            public PluginInfo Plugin { get; }
            public bool IsDiscovered { get; set; }
            public int Depth { get; set; }

            public DependencyNode(PluginInfo plugin)
            {
                Plugin = plugin;
            }
        }

        #endregion

        #region Plugin Loading

        private void LoadPlugin(PluginInfo info)
        {
            Log(string.Format("Loading plugin: {0}", info.Name));

            // 错误恢复：如果之前被自动禁用，先跳过加载
            if (_errorRecovery.IsAutoDisabled(info.Id))
            {
                info.LoadStatus = PluginLoadStatus.Disabled;
                var rec = _errorRecovery.GetRecord(info.Id);
                info.Exception = new InvalidOperationException(
                    rec != null
                        ? $"插件已自动禁用（最近错误：{rec.LastErrorMessage}）。请在插件列表中重置后再加载。"
                        : "插件已被自动禁用。");
                LogError(string.Format("Skipping auto-disabled plugin {0}", info.Name));
                return;
            }

            // 版本兼容检查（基于 PluginCompatibility）
            if (info.Manifest != null)
            {
                var compat = PluginCompatibility.Check(info.Manifest);
                if (!compat.IsCompatible)
                {
                    info.LoadStatus = PluginLoadStatus.Error;
                    info.Exception = new InvalidOperationException(compat.Reason);
                    LogError(string.Format("Plugin {0} incompatible: {1}", info.Name, compat.Reason));
                    TrackFailure(info, info.Exception);
                    return;
                }
            }

            string assemblyPath;
            if (info.Manifest != null && !string.IsNullOrEmpty(info.Manifest.EntranceAssembly))
            {
                // 从 manifest 指定的入口程序集加载
                assemblyPath = Path.Combine(info.PluginFolderPath, info.Manifest.EntranceAssembly);
            }
            else
            {
                // 旧方式：从插件目录查找 DLL
                assemblyPath = Directory.GetFiles(info.PluginFolderPath, "*.dll", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();
            }

            if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath))
            {
                info.LoadStatus = PluginLoadStatus.Error;
                info.Exception = new FileNotFoundException("Plugin assembly not found", assemblyPath);
                TrackFailure(info, info.Exception);
                return;
            }

            var loadContext = new PluginLoadContext(assemblyPath, info, _assemblyContexts);

            try
            {
                var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
                var pluginType = FindPluginEntrance(assembly);
                if (pluginType == null)
                {
                    info.LoadStatus = PluginLoadStatus.Error;
                    info.Exception = new InvalidOperationException("No plugin entrance class found in assembly");
                    loadContext.Unload();
                    TrackFailure(info, info.Exception);
                    return;
                }

                var pluginInstance = Activator.CreateInstance(pluginType) as IPlugin;
                if (pluginInstance == null)
                {
                    info.LoadStatus = PluginLoadStatus.Error;
                    info.Exception = new InvalidOperationException("Failed to create plugin instance");
                    loadContext.Unload();
                    TrackFailure(info, info.Exception);
                    return;
                }

                // 如果是 PluginBase 实例，注入 Manifest 和路径信息
                if (pluginInstance is PluginBase pluginBase)
                {
                    pluginBase.Manifest = info.Manifest;
                    pluginBase.PluginConfigFolder = info.PluginConfigFolder;
                    pluginBase.PluginFolder = info.PluginFolderPath;
                }

                // 用 manifest 或实例信息更新 PluginInfo
                if (info.Manifest == null)
                {
                    info.Id = pluginInstance.Id;
                    info.Name = pluginInstance.Name;
                    info.Version = pluginInstance.Version;
                    info.Description = pluginInstance.Description;
                    info.Author = pluginInstance.Author;
                    info.Order = pluginInstance.Order;
                }

                info.Instance = pluginInstance;
                info.IsLoaded = true;
                info.LoadStatus = PluginLoadStatus.Loaded;
                _assemblyContexts[info.Id] = loadContext;

                pluginInstance.Initialize(this);
                Log(string.Format("Plugin loaded: {0} v{1} by {2}", info.Name, info.Version, info.Author));
                OnPluginLoaded(info);
            }
            catch (Exception ex)
            {
                loadContext.Unload();
                info.LoadStatus = PluginLoadStatus.Error;
                info.Exception = ex;
                TrackFailure(info, ex);
                LogError(string.Format("Failed to load plugin {0}", info.Name), ex);
            }
        }

        /// <summary>
        /// 上报一次失败，并按 ClassIsland 风格触发自动禁用（参考 ClassIsland 的插件错误恢复机制）。
        /// </summary>
        private void TrackFailure(PluginInfo info, Exception ex)
        {
            if (info == null || string.IsNullOrEmpty(info.Id)) return;
            var report = _errorRecovery.ReportFailure(info.Id, info.Name, ex);
            if (report.AutoDisabled)
            {
                LogError(string.Format("Plugin {0} auto-disabled after {1} failures within {2} minutes",
                    info.Name,
                    PluginErrorRecoveryService.FailureThreshold,
                    PluginErrorRecoveryService.FailureWindowMinutes));
                info.LoadStatus = PluginLoadStatus.Disabled;
                // 自动禁用后立即写入 disabled_plugins 列表
                if (!IsPluginDisabled(info.Id))
                {
                    _disabledPlugins.Add(info.Id);
                    SaveDisabledPlugins();
                }
            }
        }

        /// <summary>
        /// 显式重置插件的错误记录并清除禁用状态，下次重新加载。
        /// </summary>
        public bool ResetPluginFailure(string pluginId)
        {
            if (string.IsNullOrEmpty(pluginId)) return false;
            _errorRecovery.Reset(pluginId);
            if (IsPluginDisabled(pluginId))
            {
                _disabledPlugins.Remove(pluginId);
                SaveDisabledPlugins();
            }
            return true;
        }

        /// <summary>
        /// 获取插件错误记录（用于 UI 展示错误详情）。
        /// </summary>
        public PluginErrorRecord GetPluginError(string pluginId)
        {
            return string.IsNullOrEmpty(pluginId) ? null : _errorRecovery.GetRecord(pluginId);
        }

        /// <summary>
        /// 当前 IPC 服务实例。
        /// </summary>
        public IPluginIpcBus Ipc => _ipc;

        /// <summary>
        /// 在程序集中查找插件入口类。优先查找带 [PluginEntrance] 特性的类，其次查找 IPlugin 实现类。
        /// </summary>
        private static Type FindPluginEntrance(Assembly assembly)
        {
            var types = assembly.GetTypes()
                .Where(t => !t.IsAbstract && t.IsClass && typeof(IPlugin).IsAssignableFrom(t))
                .ToList();

            // 优先查找带 [PluginEntrance] 特性的类
            var entrance = types.FirstOrDefault(t =>
                t.GetCustomAttributes(typeof(PluginEntranceAttribute), true).Length > 0);
            if (entrance != null) return entrance;

            // 其次查找 PluginBase 子类
            var pluginBase = types.FirstOrDefault(t => typeof(PluginBase).IsAssignableFrom(t));
            if (pluginBase != null) return pluginBase;

            // 最后查找任意 IPlugin 实现
            return types.FirstOrDefault();
        }

        #endregion

        #region Plugin Unloading

        public void UnloadPlugin(PluginInfo plugin)
        {
            try
            {
                plugin.Instance.Shutdown();
                _plugins.Remove(plugin);
                plugin.IsLoaded = false;
                plugin.LoadStatus = PluginLoadStatus.NotLoaded;

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

        #endregion

        #region IPluginHost Implementation

        public IServiceCollection Services => _serviceCollection;

        public IServiceProvider ServiceProvider => _serviceProvider;

        public void Log(string message)
        {
            _logger?.Info("PluginManager", message);
            OnLogMessage(message);
            System.Diagnostics.Debug.WriteLine(string.Format("[Plugin] {0}", message));
        }

        public void LogError(string message, Exception ex = null)
        {
            var fullMessage = ex != null ? string.Format("{0}: {1}", message, ex.Message) : message;
            _logger?.Error("PluginManager", message, ex);
            OnLogMessage(string.Format("ERROR: {0}", fullMessage));
            System.Diagnostics.Debug.WriteLine(string.Format("[Plugin ERROR] {0}", fullMessage));
            if (ex != null)
            {
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        public T GetService<T>() where T : class
        {
            // 优先从 DI 容器解析
            if (_serviceProvider != null)
            {
                var service = _serviceProvider.GetService<T>();
                if (service != null) return service;
            }
            // 回退到旧字典
            if (_services.TryGetValue(typeof(T), out var legacyService))
            {
                return legacyService as T;
            }
            return null;
        }

        public void RegisterService<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
            _serviceCollection.AddSingleton(typeof(T), service);
        }

        /// <summary>
        /// 非泛型注册服务，支持 Type 参数批量注册。
        /// </summary>
        public void RegisterService(Type serviceType, object service)
        {
            _services[serviceType] = service;
            _serviceCollection.AddSingleton(serviceType, service);
        }

        /// <summary>
        /// 构建 DI 服务提供者。在所有插件 Initialize 完成后调用。
        /// </summary>
        internal void BuildServiceProvider()
        {
            _serviceProvider?.Dispose();
            _serviceProvider = _serviceCollection.BuildServiceProvider();
        }

        public void RegisterToolbarItem(PluginToolbarItemInfo itemInfo)
        {
            if (itemInfo == null || string.IsNullOrEmpty(itemInfo.Id)) return;

            try
            {
                Controls.Toolbar.FloatingToolbar.ToolbarRegistry.RegisterPluginItem(itemInfo);
                Log(string.Format("Plugin registered toolbar item: {0}", itemInfo.Id));
            }
            catch (Exception ex)
            {
                LogError(string.Format("Failed to register toolbar item {0}", itemInfo.Id), ex);
            }
        }

        /// <summary>
        /// 注册 IPC 处理函数。
        /// </summary>
        public void RegisterIpcHandler(string method, Func<System.Text.Json.JsonElement?, object> handler)
        {
            if (_ipc == null)
            {
                _ipc = new PluginIpcService();
                _ipc.Start();
            }
            _ipc.RegisterHandler(method, handler);
        }

        /// <summary>
        /// 调用 <see cref="PluginSecurityCheck"/> 评估即将安装的插件包。
        /// </summary>
        public SecurityVerdict EvaluateTrust(string packagePath, string expectedSha256, string declaredPluginId)
        {
            if (_securityCheck == null)
            {
                return new SecurityVerdict
                {
                    PackagePath = packagePath,
                    PluginId = declaredPluginId,
                    TrustLevel = PluginTrustLevel.Unknown,
                };
            }

            return _securityCheck.EvaluatePackage(packagePath, expectedSha256, declaredPluginId);
        }

        /// <summary>
        /// 获取宿主 IPC 实例。仅在 <see cref="StartIpc"/> 之后可用。
        /// </summary>
        public IPluginIpcBus IpcService => _ipc;

        /// <summary>
        /// 按 pluginId 获取独立的 <see cref="PluginLogger"/>。
        /// </summary>
        public PluginLogger GetLogger(string pluginId)
        {
            return new PluginLogger(_pluginLogsDirectory, pluginId);
        }

        /// <summary>
        /// 暴露给 UI/插件的依赖分析入口。
        /// </summary>
        public DependencyAnalysis AnalyzeDependencies()
        {
            return _dependencyResolver.Analyze(_plugins);
        }

        /// <summary>
        /// 暴露给 UI 的配置导入导出器。
        /// </summary>
        public PluginConfigIo ConfigIo => _configIo;

        #endregion

        #region Events

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

        #endregion

        #region AssemblyLoadContext

        /// <summary>
        /// 插件程序集加载上下文，支持依赖解析和插件间依赖共享。
        /// </summary>
        private class PluginLoadContext : AssemblyLoadContext
        {
            private readonly AssemblyDependencyResolver _resolver;
            private readonly PluginInfo _info;
            private readonly Dictionary<string, PluginLoadContext> _allContexts;

            public PluginLoadContext(string pluginPath, PluginInfo info, Dictionary<string, PluginLoadContext> allContexts = null)
                : base(string.Format("PluginContext_{0}", info?.Id ?? Path.GetFileNameWithoutExtension(pluginPath)), isCollectible: true)
            {
                _resolver = new AssemblyDependencyResolver(pluginPath);
                _info = info;
                _allContexts = allContexts;
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                // 1. 尝试从依赖的插件加载上下文中查找
                if (_info?.Manifest?.Dependencies != null && _allContexts != null)
                {
                    foreach (var dep in _info.Manifest.Dependencies)
                    {
                        if (_allContexts.TryGetValue(dep.Id, out var depContext))
                        {
                            try
                            {
                                var assembly = depContext.Load(assemblyName);
                                if (assembly != null) return assembly;
                            }
                            catch { }
                        }
                    }
                }

                // 2. 尝试从默认上下文（主程序）加载，共享主程序集
                var defaultAssembly = Default.LoadFromAssemblyName(assemblyName);
                if (defaultAssembly != null) return defaultAssembly;

                // 3. 从插件目录解析依赖
                var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
                if (assemblyPath != null)
                {
                    return LoadFromAssemblyPath(assemblyPath);
                }

                return null;
            }

            protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
            {
                var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
                if (libraryPath != null)
                {
                    return LoadUnmanagedDllFromPath(libraryPath);
                }
                return IntPtr.Zero;
            }
        }

        #endregion
    }
}
