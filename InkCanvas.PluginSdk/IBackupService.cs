namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 自动备份服务：供插件控制宿主的设置文件自动备份（复制 Settings.json 到备份目录）。
    /// </summary>
    public interface IBackupService
    {
        /// <summary>是否已到达自动备份时机（由宿主备份间隔设置决定）。</summary>
        bool ShouldPerformAutoBackup();

        /// <summary>执行一次备份。返回是否成功。</summary>
        bool PerformAutoBackup();

        /// <summary>从最近一次备份恢复设置文件。返回是否成功。</summary>
        bool TryRestoreFromBackup();

        /// <summary>清理过期备份。</summary>
        void CleanupOldBackups();
    }
}
