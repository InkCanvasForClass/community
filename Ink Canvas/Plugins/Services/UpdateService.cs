using System;
using System.Threading.Tasks;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="IUpdateService"/> 的宿主实现：包装 <see cref="Ink_Canvas.Helpers.AutoUpdateHelper"/>，
    /// 与软件内置的检查更新共用同一套更新源。
    /// </summary>
    internal sealed class UpdateService : IUpdateService
    {
        private static Ink_Canvas.UpdateChannel Map(PluginUpdateChannel channel) => channel switch
        {
            PluginUpdateChannel.Preview => Ink_Canvas.UpdateChannel.Preview,
            PluginUpdateChannel.Beta => Ink_Canvas.UpdateChannel.Beta,
            _ => Ink_Canvas.UpdateChannel.Release,
        };

        public async Task<PluginUpdateCheckResult> CheckForUpdatesAsync(
            PluginUpdateChannel channel = PluginUpdateChannel.Release)
        {
            try
            {
                var (remoteVersion, lineGroup, releaseNotes) =
                    await Ink_Canvas.Helpers.AutoUpdateHelper.CheckForUpdates(Map(channel));
                return new PluginUpdateCheckResult
                {
                    RemoteVersion = remoteVersion ?? "",
                    ReleaseNotes = releaseNotes ?? "",
                    LineGroupName = lineGroup?.GroupName ?? "",
                };
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"UpdateService.CheckForUpdatesAsync failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return new PluginUpdateCheckResult();
            }
        }

        public async Task<string> GetUpdateLogAsync(PluginUpdateChannel channel = PluginUpdateChannel.Release)
        {
            try
            {
                return await Ink_Canvas.Helpers.AutoUpdateHelper.GetUpdateLog(Map(channel)) ?? "";
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"UpdateService.GetUpdateLogAsync failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return "";
            }
        }

        public void InstallNewVersion(string version, bool isInSilence)
        {
            try
            {
                if (!string.IsNullOrEmpty(version))
                {
                    Ink_Canvas.Helpers.AutoUpdateHelper.InstallNewVersionApp(version, isInSilence);
                }
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"UpdateService.InstallNewVersion failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
            }
        }

        public void RequestCancelDownload()
        {
            try
            {
                Ink_Canvas.Helpers.AutoUpdateHelper.RequestCancelDownload();
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"UpdateService.RequestCancelDownload failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
            }
        }

        public string LastDownloadFailure
        {
            get
            {
                try
                {
                    var reason = Ink_Canvas.Helpers.AutoUpdateHelper.LastDownloadFailure;
                    return reason == Ink_Canvas.Helpers.AutoUpdateHelper.DownloadFailureReason.None ? null : reason.ToString();
                }
                catch (Exception ex)
                {
                    Helpers.LogHelper.WriteLogToFile($"UpdateService.LastDownloadFailure failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                    return null;
                }
            }
        }
    }
}
