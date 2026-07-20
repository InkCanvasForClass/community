using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 插件市场源（Source）配置管理。参考 ClassIsland ClassIsland/ClassIsland/Plugins/PluginMarketService.cs，
    /// 用户可添加多个第三方插件源（指向不同的 index.json URL），并在每个源下选择具体的镜像。
    /// </summary>
    public class PluginMarketSourcesService
    {
        private const string FileName = "plugin_market_sources.json";

        public static readonly PluginMarketSourceInfo OfficialSource = new()
        {
            Id = "__official__",
            Url = "https://github.com/InkCanvasForClass/PluginIndex/releases/download/latest/index.json",
            SelectedMirror = ""
        };

        private readonly string _filePath;
        private readonly object _lock = new();
        private readonly List<PluginMarketSourceInfo> _sources = new();

        /// <summary>
        /// 当前所有源（含官方源）。官方源不可删除或编辑，固定在列表首位。
        /// </summary>
        public IReadOnlyList<PluginMarketSourceInfo> Sources
        {
            get { lock (_lock) return _sources.ToList(); }
        }

        /// <summary>
        /// 当前激活的源 id。
        /// </summary>
        public string ActiveSourceId
        {
            get { lock (_lock) return _activeSourceId ?? OfficialSource.Id; }
            set { lock (_lock) _activeSourceId = string.IsNullOrEmpty(value) ? OfficialSource.Id : value; Save(); }
        }

        private string _activeSourceId;

        public PluginMarketSourcesService(string basePath)
        {
            _filePath = Path.Combine(basePath, "Configs", FileName);
            Load();
        }

        /// <summary>
        /// 获取当前激活源对象（找不到则回退到官方源）。
        /// </summary>
        public PluginMarketSourceInfo GetActiveSource()
        {
            lock (_lock)
            {
                return _sources.FirstOrDefault(s => s.Id == _activeSourceId) ?? OfficialSource;
            }
        }

        /// <summary>
        /// 添加一个新源。会校验 URL 必须以 http(s):// 开头。
        /// </summary>
        public bool TryAdd(PluginMarketSourceInfo source, out string error)
        {
            error = null;
            if (source == null) { error = "源信息不能为空"; return false; }
            if (string.IsNullOrWhiteSpace(source.Id)) { error = "缺少源 id"; return false; }
            if (string.Equals(source.Id, OfficialSource.Id, StringComparison.OrdinalIgnoreCase))
            {
                error = "官方源不可重复添加";
                return false;
            }
            if (string.IsNullOrWhiteSpace(source.Url) ||
                !(source.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                  || source.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                error = "源 URL 必须以 http(s):// 开头";
                return false;
            }

            lock (_lock)
            {
                if (_sources.Any(s => string.Equals(s.Id, source.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    error = "已存在同 id 的源";
                    return false;
                }
                _sources.Add(source);
                Save();
            }
            return true;
        }

        /// <summary>
        /// 删除一个用户自定义源。官方源不可删除。
        /// </summary>
        public bool Remove(string sourceId)
        {
            if (string.Equals(sourceId, OfficialSource.Id, StringComparison.OrdinalIgnoreCase)) return false;
            lock (_lock)
            {
                var idx = _sources.FindIndex(s => string.Equals(s.Id, sourceId, StringComparison.OrdinalIgnoreCase));
                if (idx < 0) return false;
                _sources.RemoveAt(idx);
                if (string.Equals(_activeSourceId, sourceId, StringComparison.OrdinalIgnoreCase))
                {
                    _activeSourceId = OfficialSource.Id;
                }
                Save();
            }
            return true;
        }

        /// <summary>
        /// 更新一个已存在源。注意官方源不可编辑。
        /// </summary>
        public bool Update(PluginMarketSourceInfo source, out string error)
        {
            error = null;
            if (source == null || string.IsNullOrWhiteSpace(source.Id)) { error = "参数错误"; return false; }
            if (string.Equals(source.Id, OfficialSource.Id, StringComparison.OrdinalIgnoreCase))
            {
                error = "官方源不可编辑";
                return false;
            }

            lock (_lock)
            {
                var idx = _sources.FindIndex(s => string.Equals(s.Id, source.Id, StringComparison.OrdinalIgnoreCase));
                if (idx < 0) { error = "源不存在"; return false; }
                _sources[idx] = source;
                Save();
            }
            return true;
        }

        /// <summary>
        /// 设置当前源的自选镜像名。仅在该源的 <c>DownloadMirrors</c> 字典里存在的 key 才允许保存。
        /// </summary>
        public bool SelectMirror(string sourceId, string mirrorKey)
        {
            lock (_lock)
            {
                var src = _sources.FirstOrDefault(s => string.Equals(s.Id, sourceId, StringComparison.OrdinalIgnoreCase));
                if (src == null) return false;
                src.SelectedMirror = mirrorKey ?? "";
                if (string.Equals(_activeSourceId, sourceId, StringComparison.OrdinalIgnoreCase))
                {
                    Save();
                }
                return true;
            }
        }

        private void Load()
        {
            lock (_lock)
            {
                _sources.Clear();
                try
                {
                    if (File.Exists(_filePath))
                    {
                        var json = File.ReadAllText(_filePath);
                        var list = JsonConvert.DeserializeObject<List<PluginMarketSourceInfo>>(json);
                        if (list != null)
                        {
                            foreach (var s in list)
                            {
                                if (string.IsNullOrWhiteSpace(s.Id)) continue;
                                if (string.Equals(s.Id, OfficialSource.Id, StringComparison.OrdinalIgnoreCase)) continue;
                                _sources.Add(s);
                            }
                        }
                    }
                }
                catch
                {
                    // 损坏则忽略，使用空源列表
                }
            }
        }

        private void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                List<PluginMarketSourceInfo> snapshot;
                lock (_lock) snapshot = _sources.ToList();
                File.WriteAllText(_filePath, JsonConvert.SerializeObject(snapshot, Formatting.Indented));
            }
            catch
            {
                // 保存失败不阻塞主流程
            }
        }

        /// <summary>
        /// 提供给 UI 的活跃源展示名称。无自定义名时显示 host。
        /// </summary>
        public static string DisplayNameOf(PluginMarketSourceInfo source)
        {
            if (source == null) return OfficialSource.Id;
            if (source.Id == OfficialSource.Id) return "官方源";
            return source.Id;
        }
    }
}
