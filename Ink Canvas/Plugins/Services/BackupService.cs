using System;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="IBackupService"/> 的宿主实现：包装 <see cref="Ink_Canvas.Helpers.AutoBackupManager"/>。
    /// 使用宿主当前设置（MainWindow.Settings）判定备份时机与执行备份。
    /// </summary>
    internal sealed class BackupService : IBackupService
    {
        public bool ShouldPerformAutoBackup()
        {
            try
            {
                var settings = MainWindow.Settings;
                return settings != null && Ink_Canvas.Helpers.AutoBackupManager.ShouldPerformAutoBackup(settings);
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"BackupService.ShouldPerformAutoBackup failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return false;
            }
        }

        public bool PerformAutoBackup()
        {
            try
            {
                var settings = MainWindow.Settings;
                return settings != null && Ink_Canvas.Helpers.AutoBackupManager.PerformAutoBackup(settings);
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"BackupService.PerformAutoBackup failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return false;
            }
        }

        public bool TryRestoreFromBackup()
        {
            try { return Ink_Canvas.Helpers.AutoBackupManager.TryRestoreFromBackup(); }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"BackupService.TryRestoreFromBackup failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return false;
            }
        }

        public void CleanupOldBackups()
        {
            try { Ink_Canvas.Helpers.AutoBackupManager.CleanupOldBackups(); }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"BackupService.CleanupOldBackups failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
            }
        }
    }
}
