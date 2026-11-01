using IWshRuntimeLibrary;
using Ink_Canvas.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using File = System.IO.File;

namespace Ink_Canvas.Windows.SettingsViews.Helpers
{
    public static class AutoStartHelper
    {
        public const string ShortcutName = "Ink Canvas Annotation";

        private const string LegacyShortcutName = "InkCanvas";
        private const int OperationRetryCount = 3;
        private static readonly string[] ManagedShortcutNames = { ShortcutName, LegacyShortcutName };

        public static bool StartAutomaticallyCreate(string exeName)
        {
            string executablePath = GetCurrentExecutablePath();
            if (string.IsNullOrEmpty(executablePath))
            {
                LogHelper.WriteLogToFile("[AutoStart] 无法获取当前程序路径，未创建开机自启快捷方式", LogHelper.LogType.Error);
                return false;
            }

            string startupDirectory = GetStartupDirectory();
            if (string.IsNullOrEmpty(startupDirectory))
            {
                LogHelper.WriteLogToFile("[AutoStart] 无法获取当前用户的启动文件夹", LogHelper.LogType.Error);
                return false;
            }

            string shortcutPath = GetShortcutPath(exeName);
            string tempDirectory = Path.Combine(startupDirectory, ".icc-autostart");
            string tempShortcutPath = Path.Combine(tempDirectory, exeName + ".lnk");
            Exception lastException = null;

            for (int attempt = 1; attempt <= OperationRetryCount; attempt++)
            {
                try
                {
                    Directory.CreateDirectory(tempDirectory);
                    if (File.Exists(tempShortcutPath)) File.Delete(tempShortcutPath);

                    CreateShortcut(tempShortcutPath, executablePath, exeName);
                    if (!IsShortcutValid(tempShortcutPath, executablePath))
                    {
                        throw new InvalidDataException("新建的快捷方式未通过目标路径或工作目录校验");
                    }

                    File.Move(tempShortcutPath, shortcutPath, true);
                    if (!IsShortcutValid(shortcutPath, executablePath))
                    {
                        throw new InvalidDataException("替换后的快捷方式未通过目标路径或工作目录校验");
                    }

                    TryDeleteTemporaryDirectory(tempDirectory);
                    LogHelper.WriteLogToFile($"[AutoStart] 开机自启快捷方式已指向当前程序: {executablePath}", LogHelper.LogType.Info);
                    return true;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    TryDeleteFile(tempShortcutPath);
                    if (attempt < OperationRetryCount)
                    {
                        Thread.Sleep(75 * attempt);
                    }
                }
            }

            TryDeleteTemporaryDirectory(tempDirectory);
            LogHelper.WriteLogToFile(
                $"[AutoStart] 创建或修复开机自启快捷方式 {exeName}.lnk 失败: {lastException?.Message}",
                LogHelper.LogType.Error);
            return false;
        }

        public static bool StartAutomaticallyDel(string exeName)
        {
            string shortcutPath = GetShortcutPath(exeName);
            Exception lastException = null;

            for (int attempt = 1; attempt <= OperationRetryCount; attempt++)
            {
                try
                {
                    if (File.Exists(shortcutPath)) File.Delete(shortcutPath);
                    if (!File.Exists(shortcutPath)) return true;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }

                if (attempt < OperationRetryCount)
                {
                    Thread.Sleep(75 * attempt);
                }
            }

            LogHelper.WriteLogToFile(
                $"[AutoStart] 删除开机自启快捷方式 {exeName}.lnk 失败: {lastException?.Message}",
                LogHelper.LogType.Error);
            return false;
        }

        public static bool IsAutoStartEnabled(string exeName)
        {
            string executablePath = GetCurrentExecutablePath();
            return !string.IsNullOrEmpty(executablePath) &&
                   IsShortcutValid(GetShortcutPath(exeName), executablePath);
        }

        public static bool HasAnyRegistration()
        {
            try
            {
                foreach (string shortcutName in ManagedShortcutNames)
                {
                    if (File.Exists(GetShortcutPath(shortcutName))) return true;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[AutoStart] 检查已有开机自启快捷方式失败: {ex.Message}", LogHelper.LogType.Warning);
            }

            return false;
        }

        public static bool TrySetEnabled(bool enabled)
        {
            if (!enabled)
            {
                bool deleted = true;
                foreach (string shortcutName in ManagedShortcutNames)
                {
                    deleted &= StartAutomaticallyDel(shortcutName);
                }

                return deleted && !HasAnyRegistration();
            }

            if (!StartAutomaticallyCreate(ShortcutName)) return false;
            if (!StartAutomaticallyDel(LegacyShortcutName)) return false;
            return IsAutoStartEnabled(ShortcutName);
        }

        public static bool TryReconcile(bool expectedEnabled)
        {
            bool isConsistent = expectedEnabled
                ? IsAutoStartEnabled(ShortcutName) && !File.Exists(GetShortcutPath(LegacyShortcutName))
                : !HasAnyRegistration();

            if (isConsistent) return true;

            bool success = TrySetEnabled(expectedEnabled);
            LogHelper.WriteLogToFile(
                success
                    ? $"[AutoStart] 已自动修复开机自启状态，期望状态: {(expectedEnabled ? "开启" : "关闭")}"
                    : $"[AutoStart] 自动修复开机自启状态失败，期望状态: {(expectedEnabled ? "开启" : "关闭")}",
                success ? LogHelper.LogType.Info : LogHelper.LogType.Warning);
            return success;
        }

        private static void CreateShortcut(string shortcutPath, string executablePath, string exeName)
        {
            WshShell shell = null;
            IWshShortcut shortcut = null;

            try
            {
                shell = new WshShell();
                shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = executablePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty;
                shortcut.WindowStyle = 1;
                shortcut.Description = exeName + "_Ink";
                shortcut.IconLocation = executablePath + ",0";
                shortcut.Save();
            }
            finally
            {
                ReleaseComObject(shortcut);
                ReleaseComObject(shell);
            }
        }

        private static bool IsShortcutValid(string shortcutPath, string executablePath)
        {
            if (!File.Exists(shortcutPath) || !File.Exists(executablePath)) return false;

            WshShell shell = null;
            IWshShortcut shortcut = null;

            try
            {
                shell = new WshShell();
                shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
                string expectedWorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty;

                return PathsEqual(shortcut.TargetPath, executablePath) &&
                       PathsEqual(shortcut.WorkingDirectory, expectedWorkingDirectory) &&
                       File.Exists(shortcut.TargetPath);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[AutoStart] 校验开机自启快捷方式失败: {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
            finally
            {
                ReleaseComObject(shortcut);
                ReleaseComObject(shell);
            }
        }

        private static string GetCurrentExecutablePath()
        {
            string executablePath = Environment.ProcessPath;

            if (string.IsNullOrWhiteSpace(executablePath))
            {
                try
                {
                    executablePath = Process.GetCurrentProcess().MainModule?.FileName;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"[AutoStart] 读取当前主模块路径失败: {ex.Message}", LogHelper.LogType.Warning);
                }
            }

            if (string.IsNullOrWhiteSpace(executablePath)) return null;

            try
            {
                return Path.GetFullPath(executablePath);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[AutoStart] 规范化当前程序路径失败: {ex.Message}", LogHelper.LogType.Warning);
                return null;
            }
        }

        private static string GetStartupDirectory()
        {
            string startupDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            return string.IsNullOrWhiteSpace(startupDirectory) ? null : startupDirectory;
        }

        private static string GetShortcutPath(string exeName)
        {
            string startupDirectory = GetStartupDirectory();
            return string.IsNullOrEmpty(startupDirectory)
                ? null
                : Path.Combine(startupDirectory, exeName + ".lnk");
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;

            try
            {
                string normalizedLeft = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(Environment.ExpandEnvironmentVariables(left.Trim().Trim('"'))));
                string normalizedRight = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(Environment.ExpandEnvironmentVariables(right.Trim().Trim('"'))));
                return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(left.Trim().Trim('"'), right.Trim().Trim('"'), StringComparison.OrdinalIgnoreCase);
            }
        }

        private static void ReleaseComObject(object value)
        {
            if (value == null || !Marshal.IsComObject(value)) return;

            try
            {
                Marshal.FinalReleaseComObject(value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private static void TryDeleteTemporaryDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length == 0)
                {
                    Directory.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }
}
