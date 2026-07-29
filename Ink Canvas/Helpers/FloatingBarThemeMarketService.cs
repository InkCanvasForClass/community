using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// Floating-bar theme marketplace. Theme packages are downloaded outside the application assembly.
    /// </summary>
    public sealed class FloatingBarThemeMarketService
    {
        public const string OfficialIndexUrl = "https://github.com/InkCanvasForClass/ThemeMarket/releases/download/latest/themes.json";
        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        public List<ThemeMarketEntry> Entries { get; private set; } = new List<ThemeMarketEntry>();

        public async Task<bool> RefreshAsync()
        {
            try
            {
                var json = await _httpClient.GetStringAsync(OfficialIndexUrl);
                var index = JsonConvert.DeserializeObject<ThemeMarketIndex>(json);
                Entries = index?.Themes ?? new List<ThemeMarketEntry>();
                // mark installed state for each entry based on local files
                if (Entries != null)
                {
                    for (int i = 0; i < Entries.Count; i++)
                    {
                        var entry = Entries[i];
                        if (entry == null) continue; // tolerant to malformed index containing null entries
                        entry.IsInstalled = IsInstalled(entry);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"FloatingBarThemeMarket | 刷新主题市场失败: {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
        }

        public bool IsInstalled(ThemeMarketEntry entry)
        {
            return entry?.Manifest != null &&
                   File.Exists(Path.Combine(App.RootPath, "FloatingBarThemes", entry.Manifest.Id, "Theme.xaml"));
        }

        public async Task<bool> InstallAsync(ThemeMarketEntry entry)
        {
            if (entry?.Manifest == null || string.IsNullOrWhiteSpace(entry.DownloadUrl)) return false;
            var id = entry.Manifest.Id;
            var tempPath = Path.Combine(Path.GetTempPath(), $"{id}-{Guid.NewGuid():N}.icctheme");
            try
            {
                var data = await _httpClient.GetByteArrayAsync(entry.DownloadUrl);
                if (!string.IsNullOrWhiteSpace(entry.DownloadSha256))
                {
                    using (var sha = SHA256.Create())
                    {
                        var hash = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "");
                        if (!hash.Equals(entry.DownloadSha256, StringComparison.OrdinalIgnoreCase)) return false;
                    }
                }

                File.WriteAllBytes(tempPath, data);
                var themesRoot = Path.Combine(App.RootPath, "FloatingBarThemes");
                var target = Path.Combine(themesRoot, id);
                Directory.CreateDirectory(themesRoot);
                if (!Directory.Exists(target))
                    Directory.CreateDirectory(target);

                using (var archive = ZipFile.OpenRead(tempPath))
                {
                    foreach (var item in archive.Entries)
                    {
                        var destination = Path.GetFullPath(Path.Combine(target, item.FullName));
                        if (!destination.StartsWith(Path.GetFullPath(target) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                            return false;
                        if (string.IsNullOrEmpty(item.Name))
                        {
                            Directory.CreateDirectory(destination);
                            continue;
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        item.ExtractToFile(destination, true);
                    }
                }
                return File.Exists(Path.Combine(target, "Theme.xaml"));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"FloatingBarThemeMarket | 安装主题失败 {id}: {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
            finally
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }
        }
    }

    public sealed class ThemeMarketIndex
    {
        public List<ThemeMarketEntry> Themes { get; set; } = new List<ThemeMarketEntry>();
    }

    public sealed class ThemeMarketEntry
    {
        public ThemeMarketManifest Manifest { get; set; } = new ThemeMarketManifest();
        public string DownloadUrl { get; set; }
        public string DownloadSha256 { get; set; }
        public string BannerUrl { get; set; }
        // whether this theme is already installed locally (computed by RefreshAsync)
        [JsonIgnore]
        private bool _isInstalled;
        [JsonIgnore]
        public bool IsInstalled
        {
            get => _isInstalled;
            set
            {
                if (_isInstalled == value) return;
                _isInstalled = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsInstalled)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    public sealed class ThemeMarketManifest
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
    }
}
