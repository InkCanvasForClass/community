using Ink_Canvas.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 插件市场服务，负责索引获取、插件下载安装、本地/远程插件合并。
    /// </summary>
    public class PluginMarketService : INotifyPropertyChanged
    {
        private static readonly Lazy<PluginMarketService> _lazy = new Lazy<PluginMarketService>(() => new PluginMarketService());
        public static PluginMarketService Instance => _lazy.Value;

        // 官方索引地址（支持 ZIP 或 JSON 格式）
        private const string OfficialIndexUrl = "https://github.com/InkCanvasForClass/PluginIndex/releases/download/latest/index.json";

        private static readonly string MarketCachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PluginMarketCache");
        private static readonly string IndexCachePath = Path.Combine(MarketCachePath, "index.json");
        private static readonly string IndexMetaPath = Path.Combine(MarketCachePath, "meta.json");

        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        private PluginMarketIndex _marketIndex;
        private readonly Dictionary<string, string> _resolvedIcons = new Dictionary<string, string>();

        #region 绑定属性

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { if (_isLoading == value) return; _isLoading = value; OnPropertyChanged(); }
        }

        private double _loadProgress;
        public double LoadProgress
        {
            get => _loadProgress;
            set { if (Math.Abs(_loadProgress - value) < 0.01) return; _loadProgress = value; OnPropertyChanged(); }
        }

        private string _loadError;
        public string LoadError
        {
            get => _loadError;
            set { if (_loadError == value) return; _loadError = value; OnPropertyChanged(); }
        }

        private List<MergedPluginInfo> _mergedPlugins = new List<MergedPluginInfo>();
        public List<MergedPluginInfo> MergedPlugins
        {
            get => _mergedPlugins;
            set { _mergedPlugins = value; OnPropertyChanged(); }
        }

        private readonly Dictionary<string, DownloadTaskInfo> _downloadTasks = new Dictionary<string, DownloadTaskInfo>();

        #endregion

        private PluginMarketService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "InkCanvasForClass/1.0");
        }

        #region 索引加载

        /// <summary>
        /// 刷新插件市场索引。优先从网络获取，失败时使用缓存。
        /// </summary>
        public async Task RefreshIndexAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            LoadError = null;
            LoadProgress = 0;

            try
            {
                // 确保缓存目录存在
                if (!Directory.Exists(MarketCachePath))
                    Directory.CreateDirectory(MarketCachePath);

                PluginMarketIndex index = null;

                // 尝试从网络获取
                try
                {
                    LoadProgress = 10;
                    var response = await _httpClient.GetAsync(OfficialIndexUrl);
                    response.EnsureSuccessStatusCode();

                    var data = await response.Content.ReadAsByteArrayAsync();
                    LoadProgress = 50;

                    // 判断是 ZIP 还是 JSON
                    if (IsZipFile(data))
                    {
                        index = ExtractIndexFromZip(data);
                    }
                    else
                    {
                        var json = System.Text.Encoding.UTF8.GetString(data);
                        index = JsonConvert.DeserializeObject<PluginMarketIndex>(json);
                    }

                    LoadProgress = 80;

                    // 写入缓存
                    if (index != null)
                    {
                        File.WriteAllText(IndexCachePath, JsonConvert.SerializeObject(index, Formatting.Indented));
                        File.WriteAllText(IndexMetaPath, JsonConvert.SerializeObject(new
                        {
                            lastRefresh = DateTime.Now.ToString("o"),
                            source = OfficialIndexUrl
                        }));
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"PluginMarket | 网络获取索引失败: {ex.Message}", LogHelper.LogType.Warning);

                    // 尝试从缓存加载
                    if (File.Exists(IndexCachePath))
                    {
                        try
                        {
                            var cached = File.ReadAllText(IndexCachePath);
                            index = JsonConvert.DeserializeObject<PluginMarketIndex>(cached);
                            LogHelper.WriteLogToFile("PluginMarket | 使用缓存索引");
                        }
                        catch
                        {
                            // 缓存也损坏了
                        }
                    }
                }

                if (index == null)
                {
                    LoadError = "无法加载插件市场索引，请检查网络连接。";
                    _marketIndex = new PluginMarketIndex();
                }
                else
                {
                    _marketIndex = index;
                }

                LoadProgress = 90;
                MergePlugins();
                LoadProgress = 100;
            }
            catch (Exception ex)
            {
                LoadError = ex.Message;
                LogHelper.WriteLogToFile($"PluginMarket | 刷新索引异常: {ex}", LogHelper.LogType.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 仅从缓存加载索引（不联网）。
        /// </summary>
        public void LoadFromCache()
        {
            try
            {
                if (!File.Exists(IndexCachePath))
                {
                    _marketIndex = new PluginMarketIndex();
                    MergePlugins();
                    return;
                }

                var cached = File.ReadAllText(IndexCachePath);
                _marketIndex = JsonConvert.DeserializeObject<PluginMarketIndex>(cached) ?? new PluginMarketIndex();
                MergePlugins();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PluginMarket | 加载缓存索引失败: {ex.Message}", LogHelper.LogType.Warning);
                _marketIndex = new PluginMarketIndex();
                MergePlugins();
            }
        }

        #endregion

        #region 合并插件

        /// <summary>
        /// 合并本地已加载插件与市场插件列表。
        /// </summary>
        private void MergePlugins()
        {
            var merged = new Dictionary<string, MergedPluginInfo>();
            var selectedMirror = "";

            // 确定镜像根
            if (_marketIndex.DownloadMirrors != null && _marketIndex.DownloadMirrors.Count > 0)
            {
                selectedMirror = _marketIndex.DownloadMirrors.Values.First();
            }

            // 1. 加入本地已安装的插件
            foreach (var local in PluginManager.Instance.Plugins)
            {
                var info = new MergedPluginInfo
                {
                    Id = local.Id,
                    Name = local.Name,
                    Version = local.Version,
                    Description = local.Description,
                    Author = local.Author,
                    IsLocal = true,
                    LoadStatus = local.LoadStatus,
                    PluginFolderPath = local.PluginFolderPath,
                    PluginConfigFolder = local.PluginConfigFolder,
                    LocalInfo = local
                };
                merged[local.Id] = info;
            }

            // 2. 合并市场插件
            if (_marketIndex.Plugins != null)
            {
                foreach (var entry in _marketIndex.Plugins)
                {
                    var id = entry.Manifest?.Id;
                    if (string.IsNullOrEmpty(id)) continue;

                    // 解析模板 URL
                    var resolvedDownloadUrl = ResolveUrl(entry.DownloadUrl, selectedMirror);
                    var resolvedIconUrl = ResolveUrl(entry.IconUrl, selectedMirror);

                    if (merged.TryGetValue(id, out var existing))
                    {
                        // 已安装，检查是否有更新
                        existing.IsOnMarket = true;
                        existing.MarketEntry = entry;
                        existing.DownloadUrl = resolvedDownloadUrl;
                        existing.IconUrl = resolvedIconUrl;
                        existing.ReadmeUrl = ResolveUrl(entry.ReadmeUrl, selectedMirror);

                        if (Version.TryParse(NormalizeVersion(existing.Version), out var localVer) &&
                            Version.TryParse(NormalizeVersion(entry.Manifest?.Version), out var remoteVer) &&
                            remoteVer > localVer)
                        {
                            existing.IsUpdateAvailable = true;
                            existing.MarketVersion = entry.Manifest?.Version;
                        }
                    }
                    else
                    {
                        // 仅市场有
                        var info = new MergedPluginInfo
                        {
                            Id = id,
                            Name = entry.Manifest?.Name ?? "",
                            Version = entry.Manifest?.Version ?? "",
                            Description = entry.Manifest?.Description ?? "",
                            Author = entry.Manifest?.Author ?? "",
                            IsLocal = false,
                            IsOnMarket = true,
                            MarketEntry = entry,
                            DownloadUrl = resolvedDownloadUrl,
                            IconUrl = resolvedIconUrl,
                            ReadmeUrl = ResolveUrl(entry.ReadmeUrl, selectedMirror),
                            DownloadCount = entry.DownloadCount,
                            StarsCount = entry.StarsCount
                        };
                        merged[id] = info;
                    }
                }
            }

            MergedPlugins = merged.Values.OrderBy(p => p.IsLocal ? 0 : 1).ThenBy(p => p.Name).ToList();
        }

        #endregion

        #region 下载安装

        /// <summary>
        /// 获取指定插件的市场条目。
        /// </summary>
        public PluginMarketEntry ResolveMarketPlugin(string id)
        {
            return _marketIndex?.Plugins?.FirstOrDefault(p => p.Manifest?.Id == id);
        }

        /// <summary>
        /// 获取当前下载任务字典。
        /// </summary>
        public IReadOnlyDictionary<string, DownloadTaskInfo> DownloadTasks => _downloadTasks;

        /// <summary>
        /// 请求下载安装/更新指定插件。
        /// </summary>
        public async Task<bool> RequestDownloadPluginAsync(string id)
        {
            var merged = MergedPlugins.FirstOrDefault(p => p.Id == id);
            if (merged == null || string.IsNullOrEmpty(merged.DownloadUrl))
            {
                LogHelper.WriteLogToFile($"PluginMarket | 找不到插件下载信息: {id}", LogHelper.LogType.Warning);
                return false;
            }

            if (_downloadTasks.ContainsKey(id))
            {
                LogHelper.WriteLogToFile($"PluginMarket | 插件正在下载中: {id}", LogHelper.LogType.Warning);
                return false;
            }

            var task = new DownloadTaskInfo { IsDownloading = true };
            _downloadTasks[id] = task;
            merged.DownloadTask = task;
            OnPropertyChanged(nameof(DownloadTasks));

            try
            {
                // 下载到临时文件
                var tempFile = Path.GetTempFileName() + ".icpx.tmp";

                using (var response = await _httpClient.GetAsync(merged.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? -1;
                    var totalRead = 0L;
                    var buffer = new byte[8192];

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        int bytesRead;
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            if (task.CancellationToken.IsCancellationRequested)
                            {
                                task.IsCancelled = true;
                                break;
                            }

                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;

                            if (totalBytes > 0)
                            {
                                task.Progress = (double)totalRead / totalBytes * 100;
                            }
                        }
                    }
                }

                if (task.IsCancelled)
                {
                    try { File.Delete(tempFile); } catch { }
                    return false;
                }

                // 校验 SHA256（如果有）
                if (!string.IsNullOrEmpty(merged.MarketEntry?.DownloadSha256))
                {
                    var hash = ComputeSha256(tempFile);
                    if (!string.Equals(hash, merged.MarketEntry.DownloadSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        task.Error = "文件校验失败，可能已损坏。";
                        try { File.Delete(tempFile); } catch { }
                        return false;
                    }
                }

                // 移动到 PluginPackages 目录，下次启动时自动安装
                var packagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PluginPackages");
                if (!Directory.Exists(packagesDir))
                    Directory.CreateDirectory(packagesDir);

                var targetPath = Path.Combine(packagesDir, id + ".icpx");
                ProcessProtectionManager.WithWriteAccess(targetPath, () =>
                {
                    if (File.Exists(targetPath)) File.Delete(targetPath);
                    File.Move(tempFile, targetPath);
                });

                task.IsCompleted = true;
                task.Progress = 100;
                merged.RestartRequired = true;

                // 检查是否为全新安装（当前未加载的插件）
                var isAlreadyLoaded = PluginManager.Instance.Plugins.Any(p => p.Id == id);
                if (!isAlreadyLoaded)
                {
                    // 全新安装：尝试立即加载
                    try
                    {
                        PluginManager.Instance.InstallPendingPackages();
                        merged.RestartRequired = false;
                    }
                    catch
                    {
                        // 加载失败，保留 RestartRequired
                    }
                }
                // 已加载插件的更新：仅下载到 PluginPackages/，下次启动时 ProcessPluginPackages 自动覆盖

                LogHelper.WriteLogToFile($"PluginMarket | 插件下载完成: {id}");

                return true;
            }
            catch (Exception ex)
            {
                task.Error = ex.Message;
                LogHelper.WriteLogToFile($"PluginMarket | 下载插件失败 {id}: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
            finally
            {
                task.IsDownloading = false;
                _downloadTasks.Remove(id);
                merged.DownloadTask = null;
                OnPropertyChanged(nameof(DownloadTasks));
            }
        }

        /// <summary>
        /// 取消指定插件的下载。
        /// </summary>
        public void CancelDownload(string id)
        {
            if (_downloadTasks.TryGetValue(id, out var task))
            {
                task.CancellationTokenSource.Cancel();
            }
        }

        /// <summary>
        /// 解析插件依赖，返回需要一起安装的插件 ID 列表。
        /// </summary>
        public List<string> ResolveDependencies(string id)
        {
            var result = new List<string>();
            var visited = new HashSet<string>();
            ResolveDependenciesRecursive(id, result, visited);
            return result;
        }

        private void ResolveDependenciesRecursive(string id, List<string> result, HashSet<string> visited)
        {
            if (visited.Contains(id)) return;
            visited.Add(id);

            var entry = _marketIndex?.Plugins?.FirstOrDefault(p => p.Manifest?.Id == id);
            if (entry?.Manifest?.Dependencies == null) return;

            foreach (var dep in entry.Manifest.Dependencies)
            {
                if (!dep.IsRequired) continue;
                // 跳过已安装的
                if (PluginManager.Instance.Plugins.Any(p => p.Id == dep.Id)) continue;

                result.Add(dep.Id);
                ResolveDependenciesRecursive(dep.Id, result, visited);
            }
        }

        #endregion

        #region 辅助方法

        private static bool IsZipFile(byte[] data)
        {
            return data.Length >= 4 && data[0] == 0x50 && data[1] == 0x4B && data[2] == 0x03 && data[3] == 0x04;
        }

        private static PluginMarketIndex ExtractIndexFromZip(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Read))
            {
                var entry = archive.GetEntry("index.json");
                if (entry == null) return null;

                using (var reader = new StreamReader(entry.Open()))
                {
                    var json = reader.ReadToEnd();
                    return JsonConvert.DeserializeObject<PluginMarketIndex>(json);
                }
            }
        }

        private static string ResolveUrl(string url, string mirrorRoot)
        {
            if (string.IsNullOrEmpty(url)) return "";
            return url.Replace("{root}", mirrorRoot ?? "");
        }

        private static string NormalizeVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return "0.0.0";
            version = version.Trim().TrimStart('v', 'V');
            // 确保至少有 Major.Minor.Build
            var parts = version.Split('.');
            if (parts.Length == 2) version += ".0";
            if (parts.Length == 1) version += ".0.0";
            return version;
        }

        private static string ComputeSha256(string filePath)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// 获取缓存的索引最后刷新时间。
        /// </summary>
        public DateTime? GetLastRefreshTime()
        {
            try
            {
                if (!File.Exists(IndexMetaPath)) return null;
                var json = JsonConvert.DeserializeAnonymousType(File.ReadAllText(IndexMetaPath), new { lastRefresh = "" });
                if (DateTime.TryParse(json?.lastRefresh, out var dt)) return dt;
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 获取市场插件的图标本地缓存路径（异步下载）。
        /// </summary>
        public async Task<string> GetCachedIconPathAsync(string iconUrl, string pluginId)
        {
            if (string.IsNullOrEmpty(iconUrl)) return null;

            var cacheDir = Path.Combine(MarketCachePath, "icons");
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);

            var ext = Path.GetExtension(new Uri(iconUrl).AbsolutePath);
            if (string.IsNullOrEmpty(ext)) ext = ".png";
            var localPath = Path.Combine(cacheDir, pluginId + ext);

            if (File.Exists(localPath))
                return localPath;

            try
            {
                var data = await _httpClient.GetByteArrayAsync(iconUrl);
                File.WriteAllBytes(localPath, data);
                return localPath;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// 合并后的插件信息（本地 + 市场）。
    /// </summary>
    public class MergedPluginInfo : INotifyPropertyChanged
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }

        // 本地信息
        public bool IsLocal { get; set; }
        public PluginLoadStatus LoadStatus { get; set; }
        public string PluginFolderPath { get; set; }
        public string PluginConfigFolder { get; set; }
        public PluginInfo LocalInfo { get; set; }

        // 市场信息
        public bool IsOnMarket { get; set; }
        public PluginMarketEntry MarketEntry { get; set; }
        public string DownloadUrl { get; set; }
        public string IconUrl { get; set; }
        public string ReadmeUrl { get; set; }
        public long DownloadCount { get; set; }
        public long StarsCount { get; set; }

        // 状态
        public bool IsUpdateAvailable { get; set; }
        public string MarketVersion { get; set; }

        private bool _restartRequired;
        public bool RestartRequired
        {
            get => _restartRequired;
            set { if (_restartRequired == value) return; _restartRequired = value; OnPropertyChanged(); }
        }

        public DownloadTaskInfo DownloadTask { get; set; }

        // UI 辅助属性
        public string VersionText => $"v{Version}";
        public string DownloadCountText => DownloadCount > 0 ? FormatCount(DownloadCount) : null;
        public bool IsDownloading => DownloadTask?.IsDownloading == true;
        public bool ShowInstallButton => IsOnMarket && !IsLocal && !RestartRequired && !IsDownloading;
        public bool ShowInstalledBadge => IsLocal && !IsOnMarket;
        public string IconDisplayPath { get; set; }

        private static string FormatCount(long count)
        {
            if (count >= 10000) return $"{count / 10000.0:F1}w";
            if (count >= 1000) return $"{count / 1000.0:F1}k";
            return count.ToString();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 下载任务状态。
    /// </summary>
    public class DownloadTaskInfo : INotifyPropertyChanged
    {
        private double _progress;
        public double Progress
        {
            get => _progress;
            set { if (Math.Abs(_progress - value) < 0.01) return; _progress = value; OnPropertyChanged(); }
        }

        private bool _isDownloading;
        public bool IsDownloading
        {
            get => _isDownloading;
            set { if (_isDownloading == value) return; _isDownloading = value; OnPropertyChanged(); }
        }

        public bool IsCompleted { get; set; }
        public bool IsCancelled { get; set; }
        public string Error { get; set; }

        public CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();
        public CancellationToken CancellationToken => CancellationTokenSource.Token;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
