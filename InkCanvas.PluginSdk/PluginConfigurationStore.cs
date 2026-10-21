using System;
using System.IO;
using System.Text.Json;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 文件系统实现。实例的根目录由宿主按插件 ID 分配；调用者只能访问根目录内的相对路径。
    /// </summary>
    internal sealed class PluginConfigurationStore : IPluginConfigurationStore
    {
        private readonly string _rootDirectory;
        private static readonly JsonSerializerOptions DefaultJsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public PluginConfigurationStore(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("A plugin configuration directory is required.", nameof(rootDirectory));

            _rootDirectory = Path.GetFullPath(rootDirectory);
            Directory.CreateDirectory(_rootDirectory);
        }

        public bool Exists(string relativePath) => File.Exists(ResolvePath(relativePath));

        public string ReadAllText(string relativePath) => File.ReadAllText(ResolvePath(relativePath));

        public void WriteAllText(string relativePath, string content)
        {
            var path = ResolvePath(relativePath);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tempPath, content ?? string.Empty);
                if (File.Exists(path))
                    File.Replace(tempPath, path, null);
                else
                {
                    try
                    {
                        File.Move(tempPath, path);
                    }
                    catch (IOException) when (File.Exists(path))
                    {
                        File.Replace(tempPath, path, null);
                    }
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        public T ReadJson<T>(string relativePath, T defaultValue = default)
        {
            var path = ResolvePath(relativePath);
            if (!File.Exists(path))
                return defaultValue;

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, DefaultJsonOptions);
        }

        public void WriteJson<T>(string relativePath, T value, bool indented = true)
        {
            var options = new JsonSerializerOptions(DefaultJsonOptions) { WriteIndented = indented };
            WriteAllText(relativePath, JsonSerializer.Serialize(value, options));
        }

        public void Delete(string relativePath)
        {
            var path = ResolvePath(relativePath);
            if (File.Exists(path))
                File.Delete(path);
        }

        private string ResolvePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("A relative configuration path is required.", nameof(relativePath));
            if (Path.IsPathRooted(relativePath))
                throw new ArgumentException("Configuration paths must be relative.", nameof(relativePath));

            var fullPath = Path.GetFullPath(Path.Combine(_rootDirectory, relativePath));
            var rootPrefix = _rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The configuration path must remain inside the plugin configuration directory.", nameof(relativePath));

            return fullPath;
        }
    }
}
