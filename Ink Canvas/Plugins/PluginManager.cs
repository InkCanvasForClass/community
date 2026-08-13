using Ink_Canvas.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

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
        private readonly Dictionary<string, List<KeyValuePair<PluginToolbarSurface, string>>> _pluginToolbarItems =
            new Dictionary<string, List<KeyValuePair<PluginToolbarSurface, string>>>(StringComparer.OrdinalIgnoreCase);

        // 每个插件在宿主中留下的注册痕迹。卸载时逐一撤销，断开宿主对插件程序集的引用，
        // 否则可回收 ALC 不会真正释放，热重载失效。
        private readonly Dictionary<string, PluginRegistrationScope> _registrationScopes
            = new Dictionary<string, PluginRegistrationScope>(StringComparer.OrdinalIgnoreCase);
        // 批量加载标志：为 true 时插件注册工具栏项不会逐项排队重建，
        // 而是设置 _boardToolbarRebuildPending，由加载完成后统一重建一次。
        private bool _isLoadingBatch;
        private bool _boardToolbarRebuildPending;

        // 卸载后仍在等待 GC 完成回收的 ALC 弱引用，用于校验是否真正卸载成功。
        private readonly Dictionary<string, WeakReference> _unloadingContexts
            = new Dictionary<string, WeakReference>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _disabledPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly string _disabledPluginsFile;
        private readonly string _pluginLogsDirectory;
        private readonly PluginAuthorizationService _authorization;

        // 子服务
        private readonly PluginErrorRecoveryService _errorRecovery;
        private readonly PluginDependencyResolver _dependencyResolver = new PluginDependencyResolver();
        private readonly PluginConfigIo _configIo;
        private PluginSecurityCheck _securityCheck;
        private PluginMarketService _market;
        private PluginLogger _logger;
        private PluginIpcService _ipc;
        // 当前正在 Initialize 的插件，用于 RegisterToolbarItem / RegisterUriHandler 等回调识别调用方
        private PluginInfo _currentLoadingPlugin;

        // 插件 URI 处理器注册表：pluginId -> (subPath -> handler)
        private readonly Dictionary<string, Dictionary<string, Func<PluginUriRequest, bool>>> _uriHandlers =
            new Dictionary<string, Dictionary<string, Func<PluginUriRequest, bool>>>(StringComparer.OrdinalIgnoreCase);

        public static readonly string ManifestFileName = "manifest.json";
        public static readonly string PluginPackageExtension = ".icpx";
        /// <summary>待卸载标记文件名。目录删除失败时写入，下次启动由 CleanupUninstalledPlugins 兜底。</summary>
        private const string UninstallMarkerFileName = ".uninstall";

        /// <summary>
        /// 已禁用的插件 ID 列表。
        /// </summary>
        public IReadOnlyCollection<string> DisabledPlugins => _disabledPlugins;

        public IReadOnlyList<PluginInfo> Plugins
        {
            get { return _plugins.AsReadOnly(); }
        }

        /// <summary>
        /// 当前正在 Initialize 的插件。供宿主服务（如 <see cref="Ink_Canvas.Plugins.Services.NotificationService"/>）
        /// 在插件调用时识别来源，确保热重载时能按插件 ID 辨认通知回调归属。
        /// 不暴露 IPlugin 字段以免插件引用影响到 GC 回收。
        /// </summary>
        public string CurrentLoadingPluginId => _currentLoadingPlugin?.Id;

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

            // 让默认 ALC 能看到插件目录里的依赖程序集。WPF XAML 解析器对部分程序集请求
            // （如 "iNKORE.UI.WPF.Modern, Culture=neutral, PublicKeyToken=..."，通常不带版本号）
            // 会走默认 ALC 的 Assembly.Load，不进入插件 ALC 的 Load 重载。若插件自带依赖 DLL
            // 但默认 ALC 解析不到（宿主 Costura 只内嵌宿主自身的副本），插件设置页的 XAML
            // 解析就会抛 XamlParseException。这里按插件目录逐个探测 <插件目录>/<名称>.dll 兜底。
            _defaultResolvingHandler = OnDefaultContextResolving;
            AssemblyLoadContext.Default.Resolving += _defaultResolvingHandler;

            _errorRecovery = new PluginErrorRecoveryService(basePath);
            _configIo = new PluginConfigIo();
            _logger = new PluginLogger(_pluginLogsDirectory, "host");
            _authorization = new PluginAuthorizationService(basePath);
        }

        // net6 的 AssemblyLoadContext.Resolving 事件委托类型是 Func<AssemblyLoadContext, AssemblyName, Assembly>，
        // 不能用更高版本 .NET 才存在的嵌套委托 ResolvingEventHandler 声明。
        private readonly Func<AssemblyLoadContext, AssemblyName, Assembly> _defaultResolvingHandler;

        private Assembly OnDefaultContextResolving(AssemblyLoadContext context, AssemblyName name)
        {
            if (name == null || string.IsNullOrEmpty(name.Name)) return null;

            // 只服务于确实需要走默认 ALC 的 XAML 解析场景，且仅限已知的 UI 依赖。
            // 默认 ALC 不可回收：一旦在这里把插件目录的 DLL 加载进来，该文件就被锁到进程退出，
            // 插件热重载会永久失败。因此白名单外的请求一律交回给插件自己的 ALC 处理。
            if (!IsSharedUiDependency(name.Name)) return null;

            foreach (var plugin in _plugins)
            {
                if (plugin.LoadStatus != PluginLoadStatus.Loaded
                    || string.IsNullOrEmpty(plugin.PluginFolderPath))
                    continue;

                var path = Path.Combine(plugin.PluginFolderPath, name.Name + ".dll");
                if (!File.Exists(path)) continue;

                try
                {
                    // 从字节数组加载：不持有文件句柄，插件目录可被覆盖/删除。
                    // 代价是该程序集无法卸载，所以上面的白名单必须保持最小。
                    return context.LoadFromStream(new MemoryStream(File.ReadAllBytes(path)));
                }
                catch (Exception)
                {
                    // 同名程序集已加载（如宿主 Costura 内嵌副本）时忽略，交给其它解析路径。
                }
            }
            return null;
        }

        /// <summary>
        /// 判断某程序集是否属于「WPF XAML 解析器会经默认 ALC 请求」的共享 UI 依赖。
        /// 这类请求不进入插件 ALC 的 Load 重载，必须由默认 ALC 兜底，否则插件设置页
        /// 的 XAML 解析会抛 XamlParseException。
        /// </summary>
        private static bool IsSharedUiDependency(string simpleName)
        {
            return simpleName.StartsWith("iNKORE.UI.WPF", StringComparison.OrdinalIgnoreCase)
                || simpleName.StartsWith("InkCanvas.Controls", StringComparison.OrdinalIgnoreCase)
                || simpleName.StartsWith("InkCanvas.PluginSdk", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 待外部在市场服务初始化后注入。
        /// </summary>
        public void InitializeAdvancedServices(PluginMarketService market)
        {
            _market = market;
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
                // 包裹授权会话：同一插件的外部 DLL 只弹一次确认，避免逐个弹窗导致 UI 错乱。
                _authorization?.BeginSession();
                _isLoadingBatch = true;
                try
                {
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
                }
                finally
                {
                    _isLoadingBatch = false;
                    _authorization?.EndSession();
                }

                _plugins.Sort((a, b) => a.Order.CompareTo(b.Order));
                BuildServiceProvider();
                _market?.RefreshMergedPlugins();
                Log(string.Format("Plugin loading complete. Loaded {0} plugins", _plugins.Count(p => p.LoadStatus == PluginLoadStatus.Loaded)));

                // 批量加载期间累积的白板工具栏重建请求，在此统一排队一次。
                FlushPendingBoardToolbarRebuild();
            }
            catch (Exception ex)
            {
                LogError("Failed to load plugins", ex);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 安装 PluginPackages 中待安装的插件包并立即加载。可在运行时调用。
        /// 已加载插件若有待安装包，会先卸载再覆盖安装，实现热更新。
        /// </summary>
        /// <returns>本次成功安装（解压）的插件 ID 列表。</returns>
        public IReadOnlyList<string> InstallPendingPackages(string approvedPackagePath = null, string approvedPackageSha256 = null)
        {
            var installedIds = ProcessPluginPackages(approvedPackagePath, approvedPackageSha256);
            DiscoverPlugins();
            var loadOrder = ResolveLoadOrder();
            _authorization?.BeginSession();
            _isLoadingBatch = true;
            try
            {
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
            }
            finally
            {
                _isLoadingBatch = false;
                _authorization?.EndSession();
            }
            _plugins.Sort((a, b) => a.Order.CompareTo(b.Order));
            _market?.RefreshMergedPlugins();

            if (installedIds.Count > 0)
            {
                // 批量加载期间累积的白板工具栏重建请求，在此统一排队一次。
                FlushPendingBoardToolbarRebuild();

                try
                {
                    if (System.Windows.Application.Current?.MainWindow is Ink_Canvas.MainWindow mainWindow)
                    {
                        mainWindow.Dispatcher.InvokeAsync(() => mainWindow.RebuildPluginToolbars(),
                            System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                }
                catch (Exception ex)
                {
                    LogError("Failed to rebuild toolbar after hot-installing plugins", ex);
                }
            }

            return installedIds;
        }

        /// <summary>
        /// 返回 PluginPackages 目录中仍待安装的插件 ID（按 .icpx 文件名）。
        /// </summary>
        public HashSet<string> GetPendingPackagePluginIds()
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(_pluginPackagesDirectory)) return ids;

            foreach (var pkgPath in Directory.GetFiles(_pluginPackagesDirectory)
                .Where(x => Path.GetExtension(x).Equals(PluginPackageExtension, StringComparison.OrdinalIgnoreCase)))
            {
                var id = Path.GetFileNameWithoutExtension(pkgPath);
                if (!string.IsNullOrWhiteSpace(id))
                    ids.Add(id);
            }

            return ids;
        }

        /// <summary>
        /// 清理标记为 .uninstall 的插件目录（上次卸载时 DLL 被锁定，本次启动时清理）。
        /// </summary>
        private void CleanupUninstalledPlugins()
        {
            if (!Directory.Exists(_pluginsDirectory)) return;

            foreach (var subDir in Directory.GetDirectories(_pluginsDirectory))
            {
                var marker = Path.Combine(subDir, UninstallMarkerFileName);
                if (!File.Exists(marker)) continue;

                try
                {
                    // 释放门控锁后删除
                    ProcessProtectionManager.ReleaseLocksForPath(subDir);
                    ForceGarbageCollection();

                    Directory.Delete(subDir, true);

                    Log(string.Format("Cleaned up uninstalled plugin: {0}", Path.GetFileName(subDir)));

                    // 目录名即 pluginId，顺带清掉配置 / 日志 / 错误记录 / 禁用标记，
                    // 与运行期卸载路径保持一致。
                    PurgePluginResidue(new PluginInfo
                    {
                        Id = Path.GetFileName(subDir),
                        Name = Path.GetFileName(subDir),
                    });
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"PluginManager | CleanupUninstalled: {Path.GetFileName(subDir)} - 删除失败: {ex.Message}", LogHelper.LogType.Error);
                }
            }
        }

        private static bool IsValidPluginId(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId) || pluginId == "." || pluginId == "..") return false;
            if (pluginId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
            return pluginId.IndexOf(Path.DirectorySeparatorChar) < 0
                && pluginId.IndexOf(Path.AltDirectorySeparatorChar) < 0;
        }

        private string GetPluginPath(string pluginId)
        {
            if (!IsValidPluginId(pluginId))
                throw new ArgumentException("Invalid plugin id.", nameof(pluginId));

            var root = Path.GetFullPath(_pluginsDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var path = Path.GetFullPath(Path.Combine(root, pluginId));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Plugin path escapes the plugin directory.");
            return path;
        }

        #region Plugin Package Installation

        /// <summary>
        /// 处理 PluginPackages 目录中的 .icpx 插件包，将其解压安装到 Plugins 目录。
        /// 若目标插件已加载，会先卸载并释放 ALC，再覆盖安装，以支持热更新。
        /// </summary>
        /// <returns>成功解压安装的插件 ID 列表。</returns>
        private List<string> ProcessPluginPackages(string approvedPackagePath = null, string approvedPackageSha256 = null)
        {
            var installedIds = new List<string>();
            if (!Directory.Exists(_pluginPackagesDirectory)) return installedIds;

            foreach (var pkgPath in Directory.GetFiles(_pluginPackagesDirectory)
                .Where(x => Path.GetExtension(x).Equals(PluginPackageExtension, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    PluginManifest manifest;
                    using (var pkg = ZipFile.OpenRead(pkgPath))
                    {
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

                        manifest = JsonSerializer.Deserialize<PluginManifest>(manifestText);
                        if (manifest == null || string.IsNullOrEmpty(manifest.Id) || !IsValidPluginId(manifest.Id))
                        {
                            Log(string.Format("Package {0} has invalid manifest or plugin id, skipping", Path.GetFileName(pkgPath)));
                            continue;
                        }

                        if (_securityCheck == null)
                        {
                            Log(string.Format("Package {0} cannot be installed automatically before security services are initialized", Path.GetFileName(pkgPath)));
                            continue;
                        }

                        var verdict = _securityCheck.EvaluatePackage(pkgPath, null, manifest.Id);
                        var isExplicitlyApproved = string.Equals(pkgPath, approvedPackagePath, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(verdict.PackageSha256, approvedPackageSha256, StringComparison.OrdinalIgnoreCase);
                        if (_securityCheck.RequiresUserConfirmation(verdict) && !isExplicitlyApproved)
                        {
                            Log(string.Format("Package {0} is not trusted, skipping automatic installation: {1}",
                                Path.GetFileName(pkgPath), string.Join(" ", verdict.Reasons)));
                            continue;
                        }

                        var targetPath = GetPluginPath(manifest.Id);
                        var targetRoot = targetPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                        foreach (var entry in pkg.Entries)
                        {
                            var entryPath = Path.GetFullPath(Path.Combine(targetPath, entry.FullName));
                            if (!entryPath.StartsWith(targetRoot, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidDataException("Plugin package contains an entry outside the plugin directory.");
                        }
                    }

                    // 热更新：先卸载已加载实例并释放 ALC，否则 DLL 文件锁会阻止覆盖。
                    UnloadPluginForReplacement(manifest.Id);

                    var installTargetPath = GetPluginPath(manifest.Id);
                    if (Directory.Exists(installTargetPath))
                    {
                        ProcessProtectionManager.ReleaseLocksForPath(installTargetPath);
                        TryDeleteDirectory(installTargetPath);
                    }
                    Directory.CreateDirectory(installTargetPath);
                    ZipFile.ExtractToDirectory(pkgPath, installTargetPath);
                    File.Delete(pkgPath);

                    installedIds.Add(manifest.Id);
                    Log(string.Format("Installed plugin package: {0} v{1}", manifest.Name, manifest.Version));
                }
                catch (Exception ex)
                {
                    LogError(string.Format("Error installing package {0}", Path.GetFileName(pkgPath)), ex);
                    // 解压失败：保留 pkg 改名隔离，**不**删除。File.Delete 解压失败的包会让用户
                    // 永久丢失已下载好的 .icpx，必须重新走 GitHub 下载。
                    // 改名 .failed_install_<ts> 标记为失败态，避免下次启动再触发同样的失败循环。
                    // 30 天启动清理同 PluginMarketService.CleanupStalePartialFiles。
                    try
                    {
                        var failedPath = pkgPath + ".failed_install_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        if (File.Exists(failedPath)) File.Delete(failedPath); // 同秒重试情况
                        File.Move(pkgPath, failedPath);
                        LogError(string.Format("Package {0} preserved as failed install: {1}",
                            Path.GetFileName(pkgPath), Path.GetFileName(failedPath)), null);
                    }
                    catch (Exception moveEx)
                    {
                        LogError(string.Format("Failed to preserve failed package {0}", Path.GetFileName(pkgPath)), moveEx);
                    }
                }
            }

            return installedIds;
        }

        /// <summary>
        /// 卸载指定插件以便覆盖安装。若插件未在列表中则忽略。
        /// </summary>
        private void UnloadPluginForReplacement(string pluginId)
        {
            var existing = _plugins.FirstOrDefault(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
            if (existing == null) return;

            try
            {
                if (existing.Instance != null || existing.LoadStatus == PluginLoadStatus.Loaded
                    || _assemblyContexts.ContainsKey(existing.Id))
                {
                    UnloadPlugin(existing);
                }
                else
                {
                    _plugins.Remove(existing);
                }
            }
            catch (Exception ex)
            {
                LogError(string.Format("Failed to unload plugin {0} before replacement", pluginId), ex);
                _plugins.Remove(existing);
            }

            // ALC.Unload 是异步完成的；强制两轮 GC 尽量释放 DLL 文件锁。
            ForceGarbageCollection();
            ForceGarbageCollection();
        }

        private void TryDeleteDirectory(string path)
        {
            const int maxAttempts = 5;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (!Directory.Exists(path)) return;
                    ProcessProtectionManager.ReleaseLocksForPath(path);
                    Directory.Delete(path, true);
                    return;
                }
                catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException)
                                           && attempt < maxAttempts)
                {
                    ForceGarbageCollection();
                    System.Threading.Thread.Sleep(50 * attempt);
                }
            }

            // 最后一次失败抛出，由调用方记入 failed_install。
            Directory.Delete(path, true);
        }

        #endregion

        #region Plugin Discovery

        /// <summary>
        /// 扫描 Plugins 目录下的子目录，解析 manifest.json 发现插件。
        /// 同时兼容旧的 DLL 直接放置方式（无 manifest）。
        /// </summary>
        private void DiscoverPlugins()
        {
            var loadedIds = new HashSet<string>(
                _plugins.Select(plugin => plugin.Id),
                StringComparer.OrdinalIgnoreCase);

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
                    var assembly = tempContext.LoadWithoutLockingFile(dllFile);
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

            var cycleStart = walking.IndexOf(node);
            if (cycleStart >= 0)
            {
                var cycle = walking.Skip(cycleStart).Concat(new[] { node }).ToList();
                var exception = new InvalidOperationException(
                    string.Format("Circular dependency detected: {0}", string.Join(" -> ", cycle.Select(x => x.Plugin.Id))));
                foreach (var cycleNode in cycle.Distinct())
                {
                    cycleNode.Plugin.LoadStatus = PluginLoadStatus.Error;
                    cycleNode.Plugin.Exception = exception;
                    cycleNode.IsDiscovered = true;
                }
                return;
            }

            walking.Add(node);
            try
            {
                var depth = 0;
                if (node.Plugin.Manifest?.Dependencies != null)
                {
                    foreach (var dep in node.Plugin.Manifest.Dependencies)
                    {
                        if (!allNodes.TryGetValue(dep.Id, out var depNode))
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
                        if (depNode.Plugin.LoadStatus == PluginLoadStatus.Error ||
                            depNode.Plugin.LoadStatus == PluginLoadStatus.Disabled)
                        {
                            if (dep.IsRequired)
                            {
                                node.Plugin.LoadStatus = PluginLoadStatus.Error;
                                node.Plugin.Exception = new InvalidOperationException(
                                    string.Format("Plugin {0} depends on unavailable plugin {1}", node.Plugin.Id, dep.Id),
                                    depNode.Plugin.Exception);
                                return;
                            }
                            continue;
                        }

                        depth = Math.Max(depth, depNode.Depth);
                    }
                }

                node.Depth = depth + 1;
                node.IsDiscovered = true;
            }
            finally
            {
                walking.Remove(node);
            }
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

            var loadContext = new PluginLoadContext(assemblyPath, info, _assemblyContexts, _authorization);

            try
            {
                // 从字节加载，插件 DLL 不被文件锁占用，运行期可直接覆盖实现热重载。
                var assembly = loadContext.LoadWithoutLockingFile(assemblyPath);
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

                // 每个插件使用独立的宿主包装：Log/LogError 写入 PluginLogs/<plugin-id>/ 自己的文件夹，
                // 不再混入宿主日志 PluginLogs/host/，也不进入主程序日志。
                var pluginLogger = GetLogger(info.Id);
                var pluginHost = new PluginHostProxy(this, pluginLogger, info.Id);

                // 建立本次加载的注册撤销范围，Initialize 期间所有向宿主的注册都记入其中。
                var scope = new PluginRegistrationScope(info.Id);
                _registrationScopes[info.Id] = scope;

                _currentLoadingPlugin = info;
                try
                {
                    pluginInstance.Initialize(pluginHost);
                }
                finally
                {
                    _currentLoadingPlugin = null;
                }
                Log(string.Format("Plugin loaded: {0} v{1} by {2}", info.Name, info.Version, info.Author));
                OnPluginLoaded(info);
            }
            catch (Exception ex)
            {
                try
                {
                    info.Instance?.Shutdown();
                }
                catch (Exception shutdownEx)
                {
                    LogError(string.Format("Plugin {0} raised an error while rolling back failed initialization", info.Name), shutdownEx);
                }
                PluginCanvasResourceCleanup.Release(
                    info.Id,
                    GetService<ICanvasToolService>(),
                    GetService<ICanvasLayerService>(),
                    GetService<IFocusInteractionService>(),
                    GetService<IUndoService>(),
                    GetService<IWhiteboardDocumentService>(),
                    cleanupEx => LogError(
                        string.Format("Plugin {0} initialization rollback failed", info.Name),
                        cleanupEx));
                UnregisterToolbarItems(info.Id);
                _assemblyContexts.Remove(info.Id);
                info.Instance = null;
                info.IsLoaded = false;
                loadContext.Unload();
                info.LoadStatus = PluginLoadStatus.Error;
                info.Exception = ex;
                TrackFailure(info, ex);
                LogError(string.Format("Failed to load plugin {0}", info.Name), ex);
            }
        }

        /// <summary>
        /// 上报一次失败，并按错误恢复策略触发自动禁用。
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
        /// 显式重置插件的错误记录并清除禁用状态，然后尝试热加载。
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

            // 把列表中处于 Disabled/Error 的条目重置为 NotLoaded，再走加载路径
            var existing = _plugins.FirstOrDefault(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                if (existing.LoadStatus == PluginLoadStatus.Loaded || existing.Instance != null
                    || _assemblyContexts.ContainsKey(existing.Id))
                {
                    UnloadPlugin(existing);
                    existing = null;
                }
                else
                {
                    existing.Exception = null;
                    existing.LoadStatus = PluginLoadStatus.NotLoaded;
                    existing.IsLoaded = false;
                }
            }

            DiscoverPlugins();
            existing = _plugins.FirstOrDefault(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase)
                && p.LoadStatus == PluginLoadStatus.NotLoaded);
            if (existing != null)
            {
                try
                {
                    LoadPlugin(existing);
                }
                catch (Exception ex)
                {
                    existing.LoadStatus = PluginLoadStatus.Error;
                    existing.Exception = ex;
                    LogError(string.Format("Failed to reload plugin {0} after error reset", pluginId), ex);
                    return false;
                }
            }

            _market?.RefreshMergedPlugins();
            return existing != null && existing.LoadStatus == PluginLoadStatus.Loaded;
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

        /// <summary>
        /// 卸载插件：撤销所有宿主注册、释放 AssemblyLoadContext，并按需删除插件目录。
        /// </summary>
        /// <param name="plugin">要卸载的插件。</param>
        /// <param name="deleteFolder">
        /// true = 真正卸载，连同插件目录一并删除（用户点"删除"）；
        /// false = 仅卸载实例并释放目录锁，保留文件（热重载 / 覆盖安装）。
        /// </param>
        public void UnloadPlugin(PluginInfo plugin, bool deleteFolder = false)
        {
            if (plugin == null) return;

            try
            {
                try
                {
                    plugin.Instance?.Shutdown();
                }
                catch (Exception shutdownEx)
                {
                    LogError(string.Format("Plugin {0} raised an error during Shutdown", plugin.Name), shutdownEx);
                }

                PluginCanvasResourceCleanup.Release(
                    plugin.Id,
                    GetService<ICanvasToolService>(),
                    GetService<ICanvasLayerService>(),
                    GetService<IFocusInteractionService>(),
                    GetService<IUndoService>(),
                    GetService<IWhiteboardDocumentService>(),
                    cleanupEx => LogError(
                        string.Format("Plugin {0} canvas cleanup failed", plugin.Name),
                        cleanupEx));

                UnregisterToolbarItems(plugin.Id);

                // 撤销该插件在宿主留下的所有注册（工具栏组件、IPC 处理器、DI 服务、URI 处理器等）。
                // 这一步是 ALC 能否真正卸载的关键：漏掉任何一条，宿主就还握着插件程序集里的委托，
                // 可回收 ALC 便不会释放，DLL 也就仍被占用。
                //
                // 同时收集注册过的工具栏组件 Id，用于从 *.json 配置文件里清除残留条目。
                // 必须在 UndoAll 之前抓快照：撤销后内部列表已被清空。
                var itemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in Controls.Toolbar.FloatingToolbar.ToolbarRegistry.GetPluginItems())
                    itemIds.Add(item.Id);
                foreach (var item in Controls.Toolbar.BoardToolbar.BoardToolbarRegistry.GetPluginItems())
                    itemIds.Add(item.Id);
                itemIds.Add(plugin.Id);

                if (_registrationScopes.TryGetValue(plugin.Id, out var scope))
                {
                    _registrationScopes.Remove(plugin.Id);
                    scope.UndoAll((description, ex) =>
                        LogError(string.Format("Plugin {0} failed to undo registration [{1}]", plugin.Name, description), ex));

                    // DI 容器缓存了已解析的单例，必须重建，否则插件实现的服务实例仍被 Provider 持有。
                    if (scope.RegisteredServiceTypes.Count > 0)
                    {
                        BuildServiceProvider();
                    }
                }

                _plugins.Remove(plugin);
                plugin.IsLoaded = false;
                plugin.LoadStatus = PluginLoadStatus.NotLoaded;
                plugin.Instance = null;

                // 从所有配置文件里移除插件组件条目（运行内清理代替重启）。
                foreach (var itemId in itemIds)
                {
                    try
                    {
                        var modified = Controls.Toolbar.FloatingToolbar.ToolbarRegistry.RemovePluginEntryFromAllConfigs(itemId);
                        modified += Controls.Toolbar.BoardToolbar.BoardToolbarRegistry.RemovePluginEntryFromAllConfigs(itemId);
                        if (modified > 0)
                        {
                            Log(string.Format("Removed plugin item [{0}] from {1} config file(s)", itemId, modified));
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError(string.Format("Failed to clean plugin item [{0}] from configs", itemId), ex);
                    }
                }

                // 删除插件目录下的首注册标记，确保重装或重载会再次自动追加组件到工具栏。
                TryDeleteToolbarRegisteredMarker(plugin);

                if (_assemblyContexts.TryGetValue(plugin.Id, out var alc))
                {
                    _assemblyContexts.Remove(plugin.Id);

                    // 摘除插件用 += 订阅到宿主服务上的事件处理器与塞进宿主回调表的委托。
                    // 这类订阅不带插件身份，无法靠 scope 精确撤销，只能按委托所属 ALC 反查。
                    SweepPluginDelegates(alc, plugin);

                    // 留一个弱引用，稍后用来判断 ALC 是否真的被回收了。
                    _unloadingContexts[plugin.Id] = new WeakReference(alc);
                    alc.Unload();

                    // 立即跑一次 GC 帮 loader 释放 native handle，并打印诊断。
                    // 之前只有 ReloadPlugin 才会校验，DeletePlugin / UnloadPlugin 路径下
                    // 即便 ALC 仍存活也无任何日志，导致看上去"卸载成功但 dll 还在"。
                    try
                    {
                        var fullyUnloaded = WaitForUnload(plugin.Id);
                        if (!fullyUnloaded)
                        {
                            Log(string.Format(
                                "Plugin {0} ALC did not fully unload (host code may still hold references); " +
                                "consider restarting before manually overwriting the DLL.",
                                plugin.Name));
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError(string.Format("WaitForUnload failed for plugin {0}", plugin.Name), ex);
                    }
                }

                // 删除整个插件目录（仅在真正卸载时；热重载要留着目录重新加载）。
                if (deleteFolder && !string.IsNullOrEmpty(plugin.PluginFolderPath))
                {
                    TryDeletePluginDirectory(plugin.PluginFolderPath, "folder", plugin.Name,
                        writeUninstallMarkerOnFailure: true);
                    ForceGarbageCollection();

                    // 插件目录之外的残留：配置目录、日志目录、错误恢复记录、禁用标记。
                    // 不清理的话，卸载后重装会读到上一次的配置与自动禁用状态。
                    PurgePluginResidue(plugin);
                }
                else if (!string.IsNullOrEmpty(plugin.PluginFolderPath))
                {
                    // 热重载/替换安装场景：保留目录，但必须释放锁，否则新版 DLL 覆盖不进去。
                    try
                    {
                        ProcessProtectionManager.ReleaseLocksForPath(plugin.PluginFolderPath);
                        ForceGarbageCollection();
                    }
                    catch (Exception ex)
                    {
                        LogError(string.Format("Failed to release process protection locks for plugin {0}", plugin.Name), ex);
                    }
                }

                // 清理插件注册的 URI 处理器，避免卸载后残留
                if (_uriHandlers.Remove(plugin.Id))
                {
                    Log(string.Format("Plugin unregistered URI handlers: {0}", plugin.Name));
                }

                Log(string.Format("Plugin unloaded: {0}", plugin.Name));
                OnPluginUnloaded(plugin);
                _market?.RefreshMergedPlugins();
            }
            catch (Exception ex)
            {
                LogError(string.Format("Failed to unload plugin {0}", plugin.Name), ex);
            }
        }

        /// <summary>
        /// 删除一个未加载插件的全部磁盘残留：插件目录、配置目录、日志目录、
        /// 错误恢复记录与禁用标记。已加载的插件请走 <see cref="UnloadPlugin"/>
        /// 并传 <c>deleteFolder: true</c>。
        /// </summary>
        public void PurgeUnloadedPlugin(PluginInfo plugin)
        {
            if (plugin == null || string.IsNullOrEmpty(plugin.Id)) return;

            TryDeletePluginDirectory(plugin.PluginFolderPath, "folder", plugin.Name,
                writeUninstallMarkerOnFailure: true);
            PurgePluginResidue(plugin);
        }

        /// <summary>
        /// 清理插件目录之外的残留：<c>PluginConfigs/&lt;id&gt;/</c>、<c>PluginLogs/&lt;id&gt;/</c>、
        /// 错误恢复记录与禁用列表条目。仅在真正卸载（而非热重载）时调用。
        /// <para>
        /// 不清理会导致：重装后读到上一次的插件配置；被自动禁用过的插件重装后仍处于禁用态；
        /// 日志目录随卸载次数无限累积。
        /// </para>
        /// </summary>
        private void PurgePluginResidue(PluginInfo plugin)
        {
            if (plugin == null || string.IsNullOrEmpty(plugin.Id)) return;

            // 配置目录
            var configFolder = !string.IsNullOrEmpty(plugin.PluginConfigFolder)
                ? plugin.PluginConfigFolder
                : Path.Combine(_pluginConfigsDirectory, plugin.Id);
            TryDeletePluginDirectory(configFolder, "config", plugin.Name);

            // 日志目录：PluginLogger 用短开短关的方式写文件，卸载时没有常驻句柄，可直接删。
            TryDeletePluginDirectory(Path.Combine(_pluginLogsDirectory, plugin.Id), "log", plugin.Name);

            // 错误恢复记录：留着会让重装后的插件仍被判为"最近频繁失败"甚至保持自动禁用。
            try
            {
                if (_errorRecovery.Reset(plugin.Id))
                    Log(string.Format("Cleared error recovery record for plugin {0}", plugin.Name));
            }
            catch (Exception ex)
            {
                LogError(string.Format("Failed to clear error recovery record for plugin {0}", plugin.Name), ex);
            }

            // 禁用列表：卸载即视为用户不再需要该插件，重装时应从"启用"开始。
            try
            {
                if (_disabledPlugins.Remove(plugin.Id))
                {
                    SaveDisabledPlugins();
                    Log(string.Format("Removed plugin {0} from disabled list", plugin.Name));
                }
            }
            catch (Exception ex)
            {
                LogError(string.Format("Failed to update disabled list for plugin {0}", plugin.Name), ex);
            }
        }

        /// <summary>
        /// 删除插件的附属目录（配置 / 日志 / 插件主目录）。走 <see cref="ProcessProtectionManager.WithWriteAccess"/>
        /// 并显式释放目录内文件锁，避免进程保护开启时删除失败。
        /// </summary>
        /// <param name="folder">要删除的目录路径。</param>
        /// <param name="kind">目录类型描述（用于日志），如 "config" / "log" / "folder"。</param>
        /// <param name="pluginName">插件名称（用于日志）。</param>
        /// <param name="writeUninstallMarkerOnFailure">
        /// true = 删除失败时写入 .uninstall 标记，让下次启动兜底清理；false = 仅记录日志。
        /// </param>
        /// <returns>删除是否成功。</returns>
        private bool TryDeletePluginDirectory(string folder, string kind, string pluginName, bool writeUninstallMarkerOnFailure = false)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return true;

            try
            {
                var deleted = false;
                ProcessProtectionManager.WithWriteAccess(folder, () =>
                {
                    try
                    {
                        ProcessProtectionManager.ReleaseLocksForPath(folder);
                        if (Directory.Exists(folder))
                        {
                            Directory.Delete(folder, true);
                            Log(string.Format("Deleted plugin {0} directory [{1}]", kind, folder));
                            deleted = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError(string.Format("Failed to delete plugin {0} directory [{1}]", kind, folder), ex);
                        if (writeUninstallMarkerOnFailure)
                        {
                            TryWriteUninstallMarker(folder);
                        }
                    }
                });
                return deleted;
            }
            catch (Exception ex)
            {
                LogError(string.Format("Failed to purge {0} directory for plugin {1}", kind, pluginName), ex);
                if (writeUninstallMarkerOnFailure)
                {
                    TryWriteUninstallMarker(folder);
                }
                return false;
            }
        }

        /// <summary>
        /// 在指定目录写入 .uninstall 标记文件，供下次启动时兜底清理。
        /// </summary>
        private void TryWriteUninstallMarker(string folder)
        {
            try
            {
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                {
                    File.WriteAllText(Path.Combine(folder, ".uninstall"), "");
                }
            }
            catch { }
        }

        /// <summary>
        /// 强制执行垃圾回收，释放 FileStream / SafeFileHandle 等非托管句柄，
        /// 并等待终结器队列清空。用于插件卸载后立即归还 OS 资源。
        /// </summary>
        private void ForceGarbageCollection()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
        }

        /// <summary>
        /// 摘除插件订阅到宿主服务上的委托。范围覆盖所有已注册的宿主服务实例
        /// （事件订阅、热键回调表、托盘回调等），以及 IPC 处理器表。
        /// </summary>
        private void SweepPluginDelegates(AssemblyLoadContext alc, PluginInfo plugin)
        {
            var removed = 0;

            try
            {
                // _services 里是宿主服务实例（EventService/HotkeyService/TrayService…），
                // 插件的事件订阅与回调就挂在它们身上。服务常把回调再转交给内部管理器
                // （HotkeyService → GlobalHotkeyManager），故 Sweep 会向内递归若干层。
                foreach (var service in _services.Values)
                {
                    removed += PluginDelegateCleaner.Sweep(service, alc);
                }

                if (_ipc != null)
                {
                    removed += PluginDelegateCleaner.Sweep(_ipc, alc);
                }

                // 宿主的静态事件：插件经服务包装订阅后同样钉住 ALC，实例扫描覆盖不到。
                removed += PluginDelegateCleaner.SweepStaticEvents(typeof(NotificationCenterService), alc);
                removed += PluginDelegateCleaner.SweepStaticEvents(typeof(ClipboardNotification), alc);

                // 通知消息的 Action 回调不挂在事件上，而是作为字段嵌在 NotificationMessage 里。
                // 队列/历史中残留的插件消息会把插件 ALC 一直钉住，必须主动清掉。
                try
                {
                    var cleared = NotificationCenterService.ClearPluginCallbacks(plugin.Id);
                    if (cleared > 0)
                    {
                        removed += cleared;
                        Log(string.Format("Cleared {0} notification callback(s) for plugin {1}", cleared, plugin.Name));
                    }
                }
                catch (Exception ex)
                {
                    LogError(string.Format("Failed to clear notification callbacks for plugin {0}", plugin.Name), ex);
                }

                // 托盘菜单项的 Click 处理器与画布背景层的插件控件都挂在 WPF 可视化树上，
                // 不在任何服务对象的字段里，需要按插件 ID 显式拆除。
                RemovePluginUiArtifacts(plugin);

                if (removed > 0)
                {
                    Log(string.Format("Detached {0} host delegate(s) owned by plugin {1}", removed, plugin.Name));
                }
            }
            catch (Exception ex)
            {
                LogError(string.Format("Failed to detach host delegates for plugin {0}", plugin.Name), ex);
            }
        }

        /// <summary>
        /// 拆除插件留在 WPF 可视化树上的痕迹：托盘菜单项、画布背景层、
        /// 通知回调控件上挂着的 Action。
        /// 这些控件不在任何服务对象的字段里，反射扫描覆盖不到，但它们的事件处理器
        /// 指向插件程序集，同样会阻止 ALC 卸载。
        /// </summary>
        private void RemovePluginUiArtifacts(PluginInfo plugin)
        {
            try
            {
                var app = Application.Current as App;
                if (app == null) return;

                void Cleanup()
                {
                    try
                    {
                        // 托盘菜单项按 "PluginTray.<id>" 命名，插件注册时用的就是自己的组件 Id；
                        // 这里按插件 Id 前缀匹配，覆盖插件注册多个菜单项的情况。
                        app.RemovePluginTrayMenuItemsByPrefix(plugin.Id);

                        // 背景层是插件工厂产出的控件，卸载后必须摘掉，否则画布一直持有它。
                        if (app.MainWindow is MainWindow mainWindow)
                        {
                            if (mainWindow.HasPluginBackgroundLayer)
                            {
                                mainWindow.RemovePluginBackgroundLayer();
                            }

                            // 通知控件:当前正在显示的插件通知.Action 指向插件 ALC，
                            // 关闭它即可释放引用。
                            mainWindow.DetachPluginNotificationAction(plugin.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError(string.Format("Failed to remove UI artifacts for plugin {0}", plugin.Name), ex);
                    }
                }

                // 必须同步执行：卸载流程紧接着就要 GC 校验 ALC 是否释放，
                // 异步排队会让这些引用在校验时仍然存在，导致误报"未完全卸载"。
                if (app.Dispatcher.CheckAccess()) Cleanup();
                else app.Dispatcher.Invoke(Cleanup);
            }
            catch (Exception ex)
            {
                LogError(string.Format("Failed to clean plugin UI artifacts for {0}", plugin.Name), ex);
            }
        }

        /// <summary>
        /// 等待指定插件的 ALC 被 GC 真正回收，返回是否卸载成功。
        /// <para>
        /// <see cref="AssemblyLoadContext.Unload"/> 只是发起卸载请求，实际释放要等 GC 确认
        /// 无人引用。这里做有限次 GC 后检查弱引用；仍存活说明宿主某处还留着插件对象，
        /// 属于注册撤销不完整，调用方据此决定是否回退到重启。
        /// </para>
        /// </summary>
        public bool WaitForUnload(string pluginId, int maxAttempts = 10)
        {
            if (string.IsNullOrEmpty(pluginId)) return true;
            if (!_unloadingContexts.TryGetValue(pluginId, out var weakRef)) return true;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
                GC.WaitForPendingFinalizers();

                if (!weakRef.IsAlive)
                {
                    _unloadingContexts.Remove(pluginId);
                    Log(string.Format("Plugin ALC unloaded after {0} GC pass(es): {1}", attempt + 1, pluginId));
                    return true;
                }

                // loader 与 native DLL 缓存未必在第一轮 GC 内释放，多给几轮机会。
                // 间隔递增到 50ms 便于应付 loader latch。
                System.Threading.Thread.Sleep(Math.Min(50 * (attempt + 1), 200));
            }

            // 仍存活：emit 诊断信息，后端可以照此定位是哪个宿主对象还握有引用。
            LogError(string.Format(
                "Plugin ALC for {0} is still alive after {1} GC passes; some host reference is pinning it (hot reload will fall back to restart).",
                pluginId, maxAttempts));
            DiagnoseLivePluginReferences(pluginId);
            return false;
        }

        /// <summary>
        /// 诊断：列出该插件目录下仍被进程持有的 DLL（system32 忽略）。便于确认
        /// 是插件主 DLL 还在被 mmap，还是某个 native 依赖尚未被卸载。
        /// </summary>
        private void DiagnoseLivePluginReferences(string pluginId)
        {
            try
            {
                // 优先从 _plugins 查；其次按约定路径 Plugins/<pluginId> 推导；
                // 两者都为空时仍打印所有非系统 DLL 路径，便于开发者对照。
                var info = _plugins.FirstOrDefault(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
                var folder = info?.PluginFolderPath;
                if (string.IsNullOrEmpty(folder))
                {
                    var fallback = Path.Combine(_pluginsDirectory, pluginId);
                    if (Directory.Exists(fallback)) folder = fallback;
                }

                var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
                    {
                        try
                        {
                            if (string.IsNullOrEmpty(module.FileName)) continue;
                            loaded.Add(module.FileName);
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    LogError("DiagnoseLivePluginReferences: failed to enumerate modules", ex);
                }

                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                {
                    // 无法定位插件目录时，逐条列出所有非系统模块会让日志被 dotnet runtime
                    // / WinSxS 等无关 DLL 刷屏，对定位根因毫无帮助。这里只汇总一条，
                    // 真正有信息量的字段级诊断交给下面的 TryPinpointPinningReference。
                    int fallbackCount = 0;
                    foreach (var path in loaded)
                    {
                        if (path.StartsWith(Environment.SystemDirectory, StringComparison.OrdinalIgnoreCase)) continue;
                        if (path.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase)) continue;
                        fallbackCount++;
                    }
                    Log(string.Format(
                        "DiagnoseLivePluginReferences: plugin folder unknown, {0} non-system modules still mapped (suppressed; see TryPinpointPinningReference below).",
                        fallbackCount));
                }
                else
                {
                    Log(string.Format("DiagnoseLivePluginReferences: scanning modules for plugin folder [{0}]", folder));
                    foreach (var dll in Directory.GetFiles(folder, "*.dll", SearchOption.AllDirectories))
                    {
                        if (!loaded.Contains(dll)) continue;
                        Log(string.Format("Process still maps [{0}] (path: {1})", Path.GetFileName(dll), dll));
                    }
                }

                // 解 ALC 仍存活的根因：扫描所有宿主服务实例（含 MainWindow 内的 _services），
                // 找出哪个对象的字段/字典/事件还握着属于该插件 ALC 的委托或 Type。
                TryPinpointPinningReference(pluginId);
            }
            catch (Exception ex)
            {
                LogError("DiagnoseLivePluginReferences failed", ex);
            }
        }

        /// <summary>
        /// 通过反射扫描所有已注册的宿主服务，找出仍持有该插件 ALC 之对象的源头。
        /// 输出每个嫌疑字段的所属类型 + 字段名，便于开发者对照宿主代码补一刀。
        /// </summary>
        private void TryPinpointPinningReference(string pluginId)
        {
            try
            {
                var weakRef = _unloadingContexts.TryGetValue(pluginId, out var wr) ? wr : null;
                var alc = weakRef?.Target as AssemblyLoadContext;
                if (alc == null)
                {
                    Log("TryPinpointPinningReference: ALC weak reference unavailable, skipping.");
                    return;
                }

                var roots = new List<object>();
                foreach (var service in _services.Values)
                {
                    if (service != null) roots.Add(service);
                }
                if (_ipc != null) roots.Add(_ipc);
                if (Application.Current?.MainWindow != null) roots.Add(Application.Current.MainWindow);

                var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                foreach (var root in roots)
                {
                    if (root == null) continue;
                    WalkAndReportPinning(root, alc, 0, visited, new HashSet<string>(StringComparer.Ordinal), flags);
                }
            }
            catch (Exception ex)
            {
                LogError("TryPinpointPinningReference failed", ex);
            }
        }

        private void WalkAndReportPinning(object obj, AssemblyLoadContext target,
            int depth, HashSet<object> visited, HashSet<string> seenPath, BindingFlags flags)
        {
            if (obj == null || target == null || depth < 0) return;
            if (!visited.Add(obj)) return;

            var type = obj.GetType();
            while (type != null && type != typeof(object))
            {
                foreach (var field in type.GetFields(flags))
                {
                    if (field.IsStatic) continue;
                    object value;
                    try { value = field.GetValue(obj); }
                    catch { continue; }

                    if (value == null) continue;
                    var path = type.Name + "." + field.Name;

                    if (value is Delegate del)
                    {
                        if (PluginDelegateCleaner.IsOwnedBy(del, target) && seenPath.Add(path))
                            Log(string.Format("Pinning reference: {0} (delegate)", path));
                    }
                    else if (value is Type t)
                    {
                        try
                        {
                            if (AssemblyLoadContext.GetLoadContext(t.Assembly) == target && seenPath.Add(path))
                                Log(string.Format("Pinning reference: {0} (Type={1})", path, t.FullName));
                        }
                        catch { }
                    }
                    else if (depth > 0)
                    {
                        WalkAndReportPinning(value, target, depth - 1, visited, seenPath, flags);
                    }
                }
                type = type.BaseType;
            }
        }

        /// <summary>
        /// 热重载单个插件：卸载 → 校验 ALC 已释放 → 从磁盘重新发现并加载。
        /// 用于插件开发时直接覆盖 DLL 后免重启生效，也用于市场更新的热更新路径。
        /// </summary>
        /// <returns>重载结果，<see cref="PluginReloadResult.Success"/> 为 false 时调用方应提示重启。</returns>
        public PluginReloadResult ReloadPlugin(string pluginId)
        {
            if (string.IsNullOrEmpty(pluginId))
                return PluginReloadResult.Failed("Plugin id is empty.");

            var existing = _plugins.FirstOrDefault(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
            var folderPath = existing?.PluginFolderPath;

            if (existing != null)
            {
                UnloadPlugin(existing);
            }

            var unloaded = WaitForUnload(pluginId);

            // 即便 ALC 未完全释放也继续尝试加载：程序集是从字节加载的，文件没有被锁，
            // 新版本仍能读入；只是旧版本的类型会滞留在内存中，故需要如实告知调用方。
            DiscoverPlugins();

            var reloaded = _plugins.FirstOrDefault(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase)
                                                        && p.LoadStatus == PluginLoadStatus.NotLoaded);
            if (reloaded == null)
            {
                // 插件目录已被删除属于正常卸载，不算失败。
                if (!string.IsNullOrEmpty(folderPath) && !Directory.Exists(folderPath))
                    return new PluginReloadResult { Success = true, FullyUnloaded = unloaded, WasRemoved = true };

                return PluginReloadResult.Failed(string.Format("Plugin {0} was not rediscovered after unload.", pluginId));
            }

            try
            {
                LoadPlugin(reloaded);
            }
            catch (Exception ex)
            {
                reloaded.LoadStatus = PluginLoadStatus.Error;
                reloaded.Exception = ex;
                LogError(string.Format("Failed to reload plugin {0}", pluginId), ex);
                return PluginReloadResult.Failed(ex.Message);
            }

            _plugins.Sort((a, b) => a.Order.CompareTo(b.Order));
            BuildServiceProvider();
            _market?.RefreshMergedPlugins();
            RefreshToolbars();

            if (reloaded.LoadStatus != PluginLoadStatus.Loaded)
            {
                return PluginReloadResult.Failed(
                    reloaded.Exception?.Message ?? string.Format("Plugin {0} did not reach Loaded state.", pluginId));
            }

            Log(string.Format("Plugin hot-reloaded: {0} v{1} (ALC fully unloaded: {2})",
                reloaded.Name, reloaded.Version, unloaded));

            return new PluginReloadResult { Success = true, FullyUnloaded = unloaded };
        }

        /// <summary>
        /// 移除插件目录下的 .toolbar_registered 标记。这一标记的作用是「插件首次
        /// 注册工具栏项时自动加入激活配置」——卸载后它会残留在插件目录里，
        /// 下次重装/重载时便不再自动追加，违背用户预期。这里在卸载时主动清理。
        /// </summary>
        private void TryDeleteToolbarRegisteredMarker(PluginInfo plugin)
        {
            if (plugin == null || string.IsNullOrEmpty(plugin.PluginFolderPath)) return;
            if (!Directory.Exists(plugin.PluginFolderPath)) return;

            var markerPath = Path.Combine(plugin.PluginFolderPath, ".toolbar_registered");
            try
            {
                if (File.Exists(markerPath))
                {
                    File.Delete(markerPath);
                    Log(string.Format("Removed .toolbar_registered marker for plugin {0}", plugin.Name));
                }
            }
            catch (Exception ex)
            {
                LogError(string.Format("Failed to remove .toolbar_registered marker for plugin {0}", plugin.Name), ex);
            }
        }

        /// <summary>
        /// 重建浮动工具栏与白板工具栏，让插件组件的增删立即反映到 UI。
        /// </summary>
        private void RefreshToolbars()
        {
            try
            {
                if (Application.Current?.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.Dispatcher.InvokeAsync(() =>
                    {
                        mainWindow.RebuildToolbar();
                        mainWindow.RebuildBoardToolbar();
                    }, System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
            catch (Exception ex)
            {
                LogError("Failed to rebuild toolbars after plugin reload", ex);
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
            TrackServiceRegistration(typeof(T));
        }

        /// <summary>
        /// 非泛型注册服务，支持 Type 参数批量注册。
        /// </summary>
        public void RegisterService(Type serviceType, object service)
        {
            _services[serviceType] = service;
            _serviceCollection.AddSingleton(serviceType, service);
            TrackServiceRegistration(serviceType);
        }

        /// <summary>
        /// 若当前处于某插件的 Initialize 阶段，记录它注册的服务类型，卸载时一并撤销。
        /// 宿主自身在启动时注册的服务不属于任何插件范围，不会被记录。
        /// </summary>
        private void TrackServiceRegistration(Type serviceType)
        {
            var scope = GetCurrentScope();
            if (scope == null || serviceType == null) return;

            scope.RegisteredServiceTypes.Add(serviceType);
            TrackUndo("service:" + serviceType.Name, () =>
            {
                _services.Remove(serviceType);
                for (var i = _serviceCollection.Count - 1; i >= 0; i--)
                {
                    if (_serviceCollection[i].ServiceType == serviceType)
                        _serviceCollection.RemoveAt(i);
                }
            });
        }

        /// <summary>
        /// 当前正在 Initialize 的插件对应的撤销范围；不在插件加载期间时返回 null。
        /// </summary>
        private PluginRegistrationScope GetCurrentScope()
        {
            var pluginId = _currentLoadingPlugin?.Id;
            if (string.IsNullOrEmpty(pluginId)) return null;
            return _registrationScopes.TryGetValue(pluginId, out var scope) ? scope : null;
        }

        /// <summary>
        /// 把一个撤销动作记入当前插件的范围。不在插件加载期间调用时静默忽略
        /// （宿主自身的注册不需要撤销）。
        /// </summary>
        private void TrackUndo(string description, Action undo)
        {
            GetCurrentScope()?.Track(description, undo);
        }

        /// <summary>
        /// 构建 DI 服务提供者。在所有插件 Initialize 完成后调用。
        /// </summary>
        internal void BuildServiceProvider()
        {
            _serviceProvider?.Dispose();
            _serviceProvider = _serviceCollection.BuildServiceProvider();
        }

        /// <summary>
        /// 批量加载结束后，若有插件注册了白板工具栏项，排队一次 <see cref="MainWindow.RebuildBoardToolbar"/>。
        /// 避免每个插件组件都单独排队重建，与授权弹窗嵌套消息循环交错时引发 UI 渲染错乱。
        /// </summary>
        private void FlushPendingBoardToolbarRebuild()
        {
            if (!_boardToolbarRebuildPending) return;
            _boardToolbarRebuildPending = false;

            try
            {
                if (Application.Current?.MainWindow is MainWindow mw)
                {
                    mw.Dispatcher.BeginInvoke(new Action(mw.RebuildBoardToolbar),
                        System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
            }
            catch (Exception ex)
            {
                LogError("Failed to flush pending board toolbar rebuild", ex);
            }
        }

        public void RegisterToolbarItem(PluginToolbarItemInfo itemInfo)
        {
            if (itemInfo == null || string.IsNullOrEmpty(itemInfo.Id)) return;

            try
            {
                // 仅在插件首次注册时自动追加到浮动工具栏；后续启动只加入组件库，
                // 避免用户删除组件后重启又被自动加回。
                bool isFirstRegistration = IsFirstToolbarRegistration();
                bool registered;
                if (itemInfo.Surface == PluginToolbarSurface.Whiteboard)
                    registered = Controls.Toolbar.BoardToolbar.BoardToolbarRegistry.RegisterPluginItem(
                        itemInfo,
                        autoAddToActiveConfig: isFirstRegistration);
                else
                    registered = Controls.Toolbar.FloatingToolbar.ToolbarRegistry.RegisterPluginItem(
                        itemInfo,
                        autoAddToActiveConfig: isFirstRegistration);
                if (!registered) return;

                if (_currentLoadingPlugin != null)
                {
                    if (!_pluginToolbarItems.TryGetValue(_currentLoadingPlugin.Id, out var registrations))
                    {
                        registrations = new List<KeyValuePair<PluginToolbarSurface, string>>();
                        _pluginToolbarItems[_currentLoadingPlugin.Id] = registrations;
                    }
                    registrations.Add(new KeyValuePair<PluginToolbarSurface, string>(itemInfo.Surface, itemInfo.Id));
                }
                if (isFirstRegistration)
                {
                    MarkToolbarRegistered();
                }

                var itemId = itemInfo.Id;
                TrackUndo("toolbar:" + itemId,
                    () => Controls.Toolbar.FloatingToolbar.ToolbarRegistry.UnregisterPluginItem(itemId));

                Log(string.Format("Plugin registered toolbar item: {0} (autoAdd={1})", itemInfo.Id, isFirstRegistration));
            }
            catch (Exception ex)
            {
                LogError(string.Format("Failed to register toolbar item {0}", itemInfo.Id), ex);
            }
        }

        private void UnregisterToolbarItems(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId)
                || !_pluginToolbarItems.TryGetValue(pluginId, out var registrations)) return;

            foreach (var registration in registrations)
            {
                if (registration.Key == PluginToolbarSurface.Whiteboard)
                    Controls.Toolbar.BoardToolbar.BoardToolbarRegistry.UnregisterPluginItem(registration.Value);
                else
                    Controls.Toolbar.FloatingToolbar.ToolbarRegistry.UnregisterPluginItem(registration.Value);
            }
            _pluginToolbarItems.Remove(pluginId);

            try
            {
                if (System.Windows.Application.Current?.MainWindow is Ink_Canvas.MainWindow mainWindow)
                {
                    mainWindow.Dispatcher.InvokeAsync(() => mainWindow.RebuildPluginToolbars(),
                        System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
            catch (Exception ex)
            {
                LogError(string.Format("Failed to rebuild toolbars after unloading plugin {0}", pluginId), ex);
            }
        }

        /// <summary>
        /// 向白板工具栏注册插件组件。行为与 <see cref="RegisterToolbarItem"/> 相同，仅目标工具栏不同。
        /// </summary>
        public void RegisterBoardToolbarItem(PluginToolbarItemInfo itemInfo)
        {
            if (itemInfo == null || string.IsNullOrEmpty(itemInfo.Id)) return;

            try
            {
                // 复用 .toolbar_registered 标记：首次注册时把组件追加进 active 白板配置，
                // 后续启动只加入组件库，避免用户删除组件后重启又被自动加回。
                bool isFirstRegistration = IsFirstToolbarRegistration();
                Controls.Toolbar.BoardToolbar.BoardToolbarRegistry.RegisterPluginItem(itemInfo, autoAddToActiveConfig: isFirstRegistration);
                if (isFirstRegistration)
                {
                    MarkToolbarRegistered();
                }

                var itemId = itemInfo.Id;
                TrackUndo("boardToolbar:" + itemId,
                    () => Controls.Toolbar.BoardToolbar.BoardToolbarRegistry.UnregisterPluginItem(itemId));

                // 白板工具栏已构建时延迟重建以显示插件组件（在 Initialize 完成后执行，
                // 避免 ViewFactory 依赖尚未初始化完成的插件状态）。
                // 批量加载期间只标记，由加载完成后统一重建一次，避免每个组件都排队导致
                // 重复销毁/创建控件与授权弹窗嵌套消息循环时交错引发 UI 渲染错乱。
                if (_isLoadingBatch)
                {
                    _boardToolbarRebuildPending = true;
                }
                else if (Application.Current?.MainWindow is MainWindow mw)
                {
                    mw.Dispatcher.BeginInvoke(new Action(mw.RebuildBoardToolbar),
                        System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }

                Log(string.Format("Plugin registered board toolbar item: {0} (autoAdd={1})", itemInfo.Id, isFirstRegistration));
            }
            catch (Exception ex)
            {
                LogError(string.Format("Failed to register board toolbar item {0}", itemInfo.Id), ex);
            }
        }

        /// <summary>
        /// 判断当前正在加载的插件是否首次注册工具栏项。
        /// 通过插件目录下的 .toolbar_registered 标记文件判断；插件被删除时，
        /// 其目录会被 CleanupUninstalledPlugins 清理，标记随之消失，重装后会再次自动追加。
        /// </summary>
        private bool IsFirstToolbarRegistration()
        {
            var pluginFolder = _currentLoadingPlugin?.PluginFolderPath;
            if (string.IsNullOrEmpty(pluginFolder) || !Directory.Exists(pluginFolder))
                return true; // 无法确定时默认按首次注册处理，保持向后兼容
            var markerPath = Path.Combine(pluginFolder, ".toolbar_registered");
            return !File.Exists(markerPath);
        }

        /// <summary>
        /// 在当前加载插件的目录下写入 .toolbar_registered 标记，记录已发生过首次注册。
        /// </summary>
        private void MarkToolbarRegistered()
        {
            var pluginFolder = _currentLoadingPlugin?.PluginFolderPath;
            if (string.IsNullOrEmpty(pluginFolder)) return;
            try
            {
                var markerPath = Path.Combine(pluginFolder, ".toolbar_registered");
                File.WriteAllText(markerPath, DateTimeOffset.UtcNow.ToString("O"));
            }
            catch (Exception ex)
            {
                LogError(string.Format("Failed to mark toolbar registration for {0}", _currentLoadingPlugin?.Id), ex);
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

            var ipc = _ipc;
            TrackUndo("ipc:" + method, () => ipc.UnregisterHandler(method, handler));
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

        #region 插件 URI 处理

        /// <summary>
        /// 注册 URI 处理程序。须在插件 Initialize 阶段调用，通过 <see cref="_currentLoadingPlugin"/> 识别调用方插件。
        /// </summary>
        public void RegisterUriHandler(string subPath, Func<PluginUriRequest, bool> handler)
        {
            var pluginId = _currentLoadingPlugin?.Id;
            if (string.IsNullOrEmpty(pluginId))
            {
                LogError("RegisterUriHandler 必须在插件 Initialize 阶段调用（无法确定调用方插件 ID）");
                return;
            }
            RegisterUriHandler(pluginId, subPath, handler);
        }

        internal void RegisterUriHandler(string pluginId, string subPath, Func<PluginUriRequest, bool> handler)
        {
            if (string.IsNullOrEmpty(pluginId) || handler == null) return;

            var key = (subPath ?? "").Trim('/');
            if (!_uriHandlers.TryGetValue(pluginId, out var map))
            {
                map = new Dictionary<string, Func<PluginUriRequest, bool>>(StringComparer.OrdinalIgnoreCase);
                _uriHandlers[pluginId] = map;
            }
            map[key] = handler;
            TrackUndo("uri:" + pluginId + "/" + key, () =>
            {
                if (_uriHandlers.TryGetValue(pluginId, out var m))
                {
                    m.Remove(key);
                    if (m.Count == 0) _uriHandlers.Remove(pluginId);
                }
            });
            Log(string.Format("Plugin registered URI handler: {0}/{1}", pluginId, string.IsNullOrEmpty(key) ? "(catch-all)" : key));

        }

        /// <summary>
        /// 派发插件 URI（由 MainWindow 的路由器调用，UI 线程）。
        /// 子路径按「/」分段做最长前缀匹配（忽略大小写）；插件未注册/处理器返回 false/处理器异常均返回 false。
        /// </summary>
        public bool TryDispatchUri(string pluginId, string subPath, string rawUri)
        {
            if (string.IsNullOrEmpty(pluginId)) return false;
            if (!_uriHandlers.TryGetValue(pluginId, out var map) || map.Count == 0) return false;

            string reqPath = (subPath ?? "").Trim('/');
            string bestKey = null;
            foreach (var key in map.Keys)
            {
                if (key.Length == 0)
                {
                    if (bestKey == null) bestKey = key;
                    continue;
                }
                if (string.Equals(reqPath, key, StringComparison.OrdinalIgnoreCase)
                    || reqPath.StartsWith(key + "/", StringComparison.OrdinalIgnoreCase))
                {
                    if (bestKey == null || key.Length > bestKey.Length) bestKey = key;
                }
            }
            if (bestKey == null) return false;

            var request = new PluginUriRequest
            {
                PluginId = pluginId,
                Path = reqPath,
                Query = ParseUriQuery(rawUri),
                RawUri = rawUri,
            };

            try
            {
                return map[bestKey](request);
            }
            catch (Exception ex)
            {
                LogError(string.Format("插件 URI 处理器异常 ({0}/{1}): {2}", pluginId, reqPath, ex.Message), ex);
                return false;
            }
        }

        /// <summary>
        /// 主动打开一个 <c>icc://</c> 深链接。非 UI 线程时切到 UI 线程执行；
        /// 复用 <see cref="MainWindow.HandleUriCommand"/> 的路由与「启用 URI 协议」守卫。
        /// </summary>
        public bool OpenUri(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri) || !uri.Trim().StartsWith("icc:", StringComparison.OrdinalIgnoreCase))
            {
                LogError(string.Format("OpenUri 仅支持 icc: 协议: {0}", uri));
                return false;
            }

            var app = Application.Current;
            if (app?.Dispatcher == null) return false;

            if (app.Dispatcher.CheckAccess())
            {
                (app.MainWindow as MainWindow)?.HandleUriCommand(uri);
                return true;
            }

            bool dispatched = false;
            app.Dispatcher.Invoke(() =>
            {
                (app.MainWindow as MainWindow)?.HandleUriCommand(uri);
                dispatched = true;
            });
            return dispatched;
        }

        private IReadOnlyDictionary<string, string> ParseUriQuery(string uri)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(uri)) return dict;
            try
            {
                if (Uri.TryCreate(uri, UriKind.Absolute, out var u) && !string.IsNullOrEmpty(u.Query))
                {
                    string q = u.Query.TrimStart('?');
                    foreach (var pair in q.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var kv = pair.Split(new[] { '=' }, 2, StringSplitOptions.None);
                        if (kv.Length == 2 && !string.IsNullOrEmpty(kv[0]))
                        {
                            dict[Uri.UnescapeDataString(kv[0].Trim())] = Uri.UnescapeDataString(kv[1].Trim());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(string.Format("解析 URI 查询参数失败: {0}", ex.Message), ex);
            }
            return dict;
        }

        #endregion

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
            private readonly PluginAuthorizationService _authorization;

            public PluginLoadContext(string pluginPath, PluginInfo info, Dictionary<string, PluginLoadContext> allContexts = null, PluginAuthorizationService authorization = null)
                : base(string.Format("PluginContext_{0}", info?.Id ?? Path.GetFileNameWithoutExtension(pluginPath)), isCollectible: true)
            {
                _resolver = new AssemblyDependencyResolver(pluginPath);
                _info = info;
                _allContexts = allContexts;
                _authorization = authorization;
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
                try
                {
                    var defaultAssembly = Default.LoadFromAssemblyName(assemblyName);
                    if (defaultAssembly != null) return defaultAssembly;
                }
                catch (FileNotFoundException)
                {
                    // 默认上下文没有该程序集，继续从插件目录解析外部依赖。
                }

                // 3. 从插件目录解析依赖
                var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
                if (assemblyPath != null)
                {
                    if (_info != null && _authorization != null && !_authorization.RequestExternalAuthorization(_info, assemblyPath))
                        return null;
                    return LoadWithoutLockingFile(assemblyPath);
                }

                // 4. 扁平回退：直接探测 <插件目录>/<SimpleName>.dll。
                //    插件 .deps.json 若记录了 lib/net6.0-.../ 相对路径而 DLL 实际平铺在插件目录根，
                //    AssemblyDependencyResolver 解析不到；此回退与 .NET 默认 ALC 探测 App 目录的行为一致。
                if (_info?.PluginFolderPath != null)
                {
                    var flatPath = Path.Combine(_info.PluginFolderPath, assemblyName.Name + ".dll");
                    if (File.Exists(flatPath))
                    {
                        if (_authorization != null && !_authorization.RequestExternalAuthorization(_info, flatPath))
                            return null;
                        return LoadWithoutLockingFile(flatPath);
                    }
                }

                return null;
            }

            /// <summary>
            /// 从字节数组加载程序集，避免 <see cref="AssemblyLoadContext.LoadFromAssemblyPath"/>
            /// 对文件加内存映射锁。热重载依赖这一点：DLL 不被占用，才能在运行期直接覆盖。
            /// 若旁边存在 .pdb 则一并载入，保证热重载后异常堆栈仍有行号。
            /// </summary>
            internal Assembly LoadWithoutLockingFile(string path)
            {
                var assemblyBytes = File.ReadAllBytes(path);
                var pdbPath = Path.ChangeExtension(path, ".pdb");

                if (File.Exists(pdbPath))
                {
                    try
                    {
                        using var pdbStream = new MemoryStream(File.ReadAllBytes(pdbPath));
                        using var peStream = new MemoryStream(assemblyBytes);
                        return LoadFromStream(peStream, pdbStream);
                    }
                    catch (Exception)
                    {
                        // pdb 损坏或版本不匹配时退回无符号加载，不因调试信息问题阻断插件加载。
                    }
                }

                using var stream = new MemoryStream(assemblyBytes);
                return LoadFromStream(stream);
            }

            protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
            {
                var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
                if (libraryPath != null)
                {
                    if (_info != null && _authorization != null && !_authorization.RequestExternalAuthorization(_info, libraryPath))
                        return IntPtr.Zero;
                    return LoadUnmanagedDllWithoutLockingFile(libraryPath);
                }
                return IntPtr.Zero;
            }

            /// <summary>
            /// 加载 native DLL 但不锁住原文件。
            /// <para>
            /// <see cref="AssemblyLoadContext.LoadUnmanagedDllFromPath"/> 走 Win32 LoadLibrary，
            /// 加载期间文件一直被 loader 持有，普通覆盖/删除会失败。
            /// 解决思路：把原 DLL 拷贝到 <c>%TEMP%/ICC-CE-NativeCache/{pluginId}/</c>，
            /// 从副本加载；这样插件目录里的 DLL 永远不会被锁，注册表/市场更新可以随时
            /// 删除或覆盖。ALC 卸载时副本随 %TEMP% 清理流程一起回收（不必立即删除---
            /// 删了反而会导致正在运行的 native 代码访问已释放内存）。
            /// </para>
            /// </summary>
            internal IntPtr LoadUnmanagedDllWithoutLockingFile(string originalPath)
            {
                if (string.IsNullOrEmpty(originalPath) || !File.Exists(originalPath))
                    return IntPtr.Zero;

                try
                {
                    var cacheDir = Path.Combine(Path.GetTempPath(), "ICC-CE-NativeCache",
                        _info?.Id ?? Path.GetFileNameWithoutExtension(originalPath));
                    Directory.CreateDirectory(cacheDir);

                    var fileName = Path.GetFileName(originalPath);
                    var cachedPath = Path.Combine(cacheDir, fileName);

                    // 复制并加载：拷贝用自己的 FileStream，读完即关，主进程不再持有原文件。
                    // 副本路径固定后，重复加载同名 DLL 会拿到同一副本，不会内存增长。
                    try
                    {
                        using (var src = new FileStream(originalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var dst = new FileStream(cachedPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            src.CopyTo(dst);
                        }
                    }
                    catch (Exception copyEx)
                    {
                        LogHelper.WriteLogToFile(
                            $"PluginManager: 缓存 native DLL 失败，回退直接加载: {originalPath} -> {copyEx.Message}",
                            LogHelper.LogType.Warning);
                        return LoadUnmanagedDllFromPath(originalPath);
                    }

                    return LoadUnmanagedDllFromPath(cachedPath);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile(
                        $"PluginManager: 加载 native DLL 失败: {originalPath} -> {ex.Message}",
                        LogHelper.LogType.Error);
                    return IntPtr.Zero;
                }
            }
        }

        #endregion
    }
}
