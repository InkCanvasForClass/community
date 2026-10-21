using System;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 当前插件专属的配置存储。所有路径均相对于宿主分配的插件配置目录，
    /// 存储实现负责路径隔离、目录创建与原子写入。
    /// </summary>
    public interface IPluginConfigurationStore
    {
        /// <summary>检查相对路径对应的配置文件是否存在。</summary>
        bool Exists(string relativePath);

        /// <summary>读取文本配置。文件不存在时抛出 <see cref="System.IO.FileNotFoundException"/>。</summary>
        string ReadAllText(string relativePath);

        /// <summary>以原子方式写入文本配置，避免进程中断造成目标文件被截断。</summary>
        void WriteAllText(string relativePath, string content);

        /// <summary>读取 JSON 配置；文件不存在时返回 <paramref name="defaultValue"/>。</summary>
        T ReadJson<T>(string relativePath, T defaultValue = default);

        /// <summary>以 JSON 格式写入配置。</summary>
        void WriteJson<T>(string relativePath, T value, bool indented = true);

        /// <summary>删除配置文件；文件不存在时不执行操作。</summary>
        void Delete(string relativePath);
    }
}
