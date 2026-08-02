using System.Threading.Tasks;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 更新通道。
    /// </summary>
    public enum PluginUpdateChannel
    {
        /// <summary>稳定版。</summary>
        Release = 0,
        /// <summary>预览版。</summary>
        Preview = 1,
        /// <summary>Beta 版。</summary>
        Beta = 2,
    }

    /// <summary>
    /// 更新检查结果。
    /// </summary>
    public sealed class PluginUpdateCheckResult
    {
        /// <summary>远程最新版本号。</summary>
        public string RemoteVersion { get; set; } = "";

        /// <summary>发布说明（UpdateLog 文本）。</summary>
        public string ReleaseNotes { get; set; } = "";

        /// <summary>命中的更新线路组名（用于后续下载/安装）。</summary>
        public string LineGroupName { get; set; } = "";
    }

    /// <summary>
    /// 更新服务：供插件检查宿主是否有新版本、读取更新日志、触发安装或取消下载。
    /// <para>底层复用宿主 <c>AutoUpdateHelper</c>，与软件内置的检查更新共用同一套更新源与校验。</para>
    /// </summary>
    public interface IUpdateService
    {
        /// <summary>
        /// 检查指定通道是否有新版本。
        /// </summary>
        /// <param name="channel">更新通道。</param>
        /// <returns>检查结果；无新版本时 <see cref="PluginUpdateCheckResult.RemoteVersion"/> 为空。</returns>
        Task<PluginUpdateCheckResult> CheckForUpdatesAsync(
            PluginUpdateChannel channel = PluginUpdateChannel.Release);

        /// <summary>
        /// 获取指定通道的更新日志（UpdateLog 全文）。
        /// </summary>
        Task<string> GetUpdateLogAsync(
            PluginUpdateChannel channel = PluginUpdateChannel.Release);

        /// <summary>
        /// 下载并安装指定版本（后台进行，宿主重启时应用更新）。
        /// </summary>
        /// <param name="version">要安装的版本号（来自 <see cref="CheckForUpdatesAsync"/>）。</param>
        /// <param name="isInSilence">是否静默安装（无确认提示）。</param>
        void InstallNewVersion(string version, bool isInSilence);

        /// <summary>取消正在进行的下载。</summary>
        void RequestCancelDownload();

        /// <summary>最近一次下载失败的原因描述；无失败时为 null。</summary>
        string LastDownloadFailure { get; }
    }
}
