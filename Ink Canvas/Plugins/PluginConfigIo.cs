using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 插件配置导入导出器。导出的 .plugincfg 文件是一个 zip：
    /// <list type="bullet">
    /// <item>manifest.json —— 插件元数据</item>
    /// <item>configs/* —— 插件配置目录下所有文件</item>
    /// </list>
    /// </summary>
    public class PluginConfigIo
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// 将指定插件的配置导出为 .plugincfg 文件。返回导出的文件路径。
        /// </summary>
        public string Export(PluginInfo plugin, string destinationFilePath = null)
        {
            if (plugin == null) throw new ArgumentNullException(nameof(plugin));
            if (string.IsNullOrEmpty(plugin.Id))
                throw new ArgumentException("Plugin id is required", nameof(plugin));

            destinationFilePath ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                $"ICC-CE-Plugin-{Sanitize(plugin.Id)}-{DateTime.Now:yyyyMMddHHmmss}.plugincfg");

            try
            {
                if (File.Exists(destinationFilePath)) File.Delete(destinationFilePath);

                using (var zip = ZipFile.Open(destinationFilePath, ZipArchiveMode.Create))
                {
                    // 1. manifest.json
                    var manifest = plugin.Manifest ?? new PluginManifest
                    {
                        Id = plugin.Id,
                        Name = plugin.Name,
                        Version = plugin.Version,
                        Description = plugin.Description,
                        Author = plugin.Author,
                        EntranceAssembly = ""
                    };
                    var manifestEntry = zip.CreateEntry("manifest.json");
                    using (var writer = new StreamWriter(manifestEntry.Open()))
                    {
                        writer.Write(JsonSerializer.Serialize(manifest, JsonOptions));
                    }

                    // 2. configs/* —— 配置目录下的所有文件（不含子目录层级太深）
                    if (!string.IsNullOrEmpty(plugin.PluginConfigFolder) && Directory.Exists(plugin.PluginConfigFolder))
                    {
                        foreach (var file in Directory.EnumerateFiles(plugin.PluginConfigFolder, "*", SearchOption.AllDirectories))
                        {
                            var relative = GetRelativeName(plugin.PluginConfigFolder, file);
                            zip.CreateEntryFromFile(file, $"configs/{relative}", CompressionLevel.Optimal);
                        }
                    }

                    // 3. info.json —— 导出元数据：宿主版本、导出时间、插件 id
                    var metaEntry = zip.CreateEntry("info.json");
                    using (var writer = new StreamWriter(metaEntry.Open()))
                    {
                        writer.Write(JsonSerializer.Serialize(new
                        {
                            pluginId = plugin.Id,
                            pluginName = plugin.Name,
                            pluginVersion = plugin.Version,
                            hostVersion = HostApiRequirement.HostVersion,
                            exportedAt = DateTime.UtcNow.ToString("o")
                        }, JsonOptions));
                    }
                }

                LogHelper.WriteLogToFile(
                    $"PluginConfigIo | 导出插件配置: {plugin.Id} -> {destinationFilePath}",
                    LogHelper.LogType.Event);

                return destinationFilePath;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"PluginConfigIo | 导出失败: {plugin.Id} - {ex.Message}",
                    LogHelper.LogType.Error);
                throw;
            }
        }

        /// <summary>
        /// 读取 .plugincfg 文件中的 manifest 与 payloads，但只准备导入，不立即写入磁盘。
        /// </summary>
        public PluginConfigPackage Inspect(string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath)) throw new ArgumentNullException(nameof(sourcePath));
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Config package not found", sourcePath);

            var pkg = new PluginConfigPackage { SourcePath = sourcePath };

            using (var zip = ZipFile.OpenRead(sourcePath))
            {
                var manifestEntry = zip.GetEntry("manifest.json");
                if (manifestEntry != null)
                {
                    using (var reader = new StreamReader(manifestEntry.Open()))
                    {
                        var json = reader.ReadToEnd();
                        pkg.Manifest = JsonSerializer.Deserialize<PluginManifest>(json, JsonOptions);
                    }
                }

                foreach (var entry in zip.Entries)
                {
                    if (!entry.FullName.StartsWith("configs/", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var rel = entry.FullName.Substring("configs/".Length);
                    if (string.IsNullOrEmpty(rel)) continue;

                    using (var stream = entry.Open())
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        pkg.Files[rel] = ms.ToArray();
                    }
                }

                var infoEntry = zip.GetEntry("info.json");
                if (infoEntry != null)
                {
                    using (var reader = new StreamReader(infoEntry.Open()))
                    {
                        pkg.InfoJson = reader.ReadToEnd();
                    }
                }
            }

            return pkg;
        }

        /// <summary>
        /// 将 <see cref="Inspect"/> 的 payloads 写入目标插件的配置目录。
        /// <paramref name="overwrite"/> 为 true 时覆盖同名文件；为 false 时保留现有文件并跳过。
        /// </summary>
        public int Import(PluginConfigPackage package, string targetConfigFolder, bool overwrite = true)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            if (string.IsNullOrEmpty(targetConfigFolder))
                throw new ArgumentException("targetConfigFolder is required", nameof(targetConfigFolder));

            try
            {
                Directory.CreateDirectory(targetConfigFolder);
                var written = 0;
                foreach (var (rel, data) in package.Files)
                {
                    var dest = Path.Combine(targetConfigFolder, rel);
                    var dir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    if (File.Exists(dest) && !overwrite) continue;
                    File.WriteAllBytes(dest, data);
                    written++;
                }

                LogHelper.WriteLogToFile(
                    $"PluginConfigIo | 导入插件配置: {package.Manifest?.Id} -> {targetConfigFolder} ({written} 文件)",
                    LogHelper.LogType.Event);

                return written;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"PluginConfigIo | 导入失败 {package.Manifest?.Id}: {ex.Message}",
                    LogHelper.LogType.Error);
                throw;
            }
        }

        /// <summary>
        /// 一步式导入：直接读取 <paramref name="sourcePath"/> 导入到 <paramref name="targetConfigFolder"/>。
        /// </summary>
        public int Import(string sourcePath, string targetConfigFolder, bool overwrite = true)
        {
            return Import(Inspect(sourcePath), targetConfigFolder, overwrite);
        }

        private static string GetRelativeName(string root, string filePath)
        {
            var rel = filePath.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return rel.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        private static string Sanitize(string id)
        {
            if (string.IsNullOrEmpty(id)) return "unknown";
            var invalid = Path.GetInvalidFileNameChars();
            var chars = id.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0) chars[i] = '_';
            }
            return new string(chars);
        }
    }

    /// <summary>
    /// 一个尚未落盘的配置包。
    /// </summary>
    public class PluginConfigPackage
    {
        public string SourcePath { get; set; } = "";
        public PluginManifest Manifest { get; set; }
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string InfoJson { get; set; } = "";
    }
}
