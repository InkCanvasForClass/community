using IWshRuntimeLibrary;
using System;
using System.Windows;
using Application = System.Windows.Forms.Application;
using File = System.IO.File;
using Path = System.IO.Path;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 创建开机自启动快捷方式。
        /// </summary>
        /// <param name="exeName">可执行文件名，用于命名快捷方式。</param>
        /// <returns>创建成功返回true，失败返回false。</returns>
        /// <remarks>
        /// 操作包括：
        /// 1. 创建Windows Shell对象
        /// 2. 在启动文件夹中创建快捷方式
        /// 3. 设置快捷方式的目标路径为当前可执行文件路径
        /// 4. 设置工作目录为当前目录
        /// 5. 设置窗口样式为普通窗口
        /// 6. 设置快捷方式描述
        /// 7. 保存快捷方式
        /// 8. 捕获可能的异常，确保方法不会因异常而崩溃
        /// </remarks>
        private const string CurrentStartupShortcutName = "Ink Canvas Annotation";
        private const string LegacyStartupShortcutName = "InkCanvas";

        private static string GetStartupShortcutPath(string shortcutName)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                $"{shortcutName}.lnk");
        }

        public static bool IsAutoStartEnabled()
        {
            return File.Exists(GetStartupShortcutPath(CurrentStartupShortcutName)) ||
                   File.Exists(GetStartupShortcutPath(LegacyStartupShortcutName));
        }

        public static bool StartAutomaticallyCreate(string exeName)
        {
            try
            {
                var shell = new WshShell();
                var startupShortcutPath = GetStartupShortcutPath(exeName);
                var shortcut = (IWshShortcut)shell.CreateShortcut(startupShortcutPath);
                //设置快捷方式的目标所在的位置(源程序完整路径)
                shortcut.TargetPath = Application.ExecutablePath;
                //应用程序的工作目录
                //当用户没有指定一个具体的目录时，快捷方式的目标应用程序将使用该属性所指定的目录来装载或保存文件。
                shortcut.WorkingDirectory = Environment.CurrentDirectory;
                //目标应用程序窗口类型(1.Normal window普通窗口,3.Maximized最大化窗口,7.Minimized最小化)
                shortcut.WindowStyle = 1;
                //快捷方式的描述
                shortcut.Description = exeName + "_Ink";
                //设置快捷键(如果有必要的话.)
                //shortcut.Hotkey = "CTRL+ALT+D";
                shortcut.Save();
                return true;
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"创建开机自启动快捷方式失败: {ex}", Helpers.LogHelper.LogType.Error);
            }

            return false;
        }

        /// <summary>
        /// 删除开机自启动快捷方式。
        /// </summary>
        /// <param name="exeName">可执行文件名，用于定位要删除的快捷方式。</param>
        /// <returns>删除成功返回true，失败返回false。</returns>
        /// <remarks>
        /// 操作包括：
        /// 1. 在启动文件夹中删除指定名称的快捷方式
        /// 2. 捕获可能的异常，确保方法不会因异常而崩溃
        /// </remarks>
        public static bool StartAutomaticallyDel(string exeName)
        {
            try
            {
                var startupShortcutPath = GetStartupShortcutPath(exeName);
                File.Delete(startupShortcutPath);
                return true;
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"删除开机自启动快捷方式失败: {ex}", Helpers.LogHelper.LogType.Error);
            }

            return false;
        }
    }
}
