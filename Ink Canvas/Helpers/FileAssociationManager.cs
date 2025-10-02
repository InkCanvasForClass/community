using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Windows;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 文件关联管理器，用于注册和处理.icstk文件的关联
    /// </summary>
    public static class FileAssociationManager
    {
        private const string FileExtension = ".icstk";
        private const string FileTypeName = "InkCanvasStrokesFile";
        private const string AppName = "Ink Canvas";
        private const string AppDescription = "Ink Canvas Strokes File";

        // IPC相关常量
        private const string IpcMutexName = "InkCanvasFileAssociationIpc";
        private const string IpcEventName = "InkCanvasFileAssociationEvent";
        private const string IpcFilePrefix = "InkCanvasFileAssociation_";
        private const string IpcBoardModePrefix = "InkCanvasBoardMode_";
        private const string IpcShowModePrefix = "InkCanvasShowMode_";
        private const int IpcTimeout = 5000; // 5秒超时

        /// <summary>
        /// 注册.icstk文件关联
        /// </summary>
        public static bool RegisterFileAssociation()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;

                // 注册文件类型
                using (RegistryKey fileTypeKey = Registry.ClassesRoot.CreateSubKey(FileTypeName))
                {
                    fileTypeKey.SetValue("", AppDescription);
                    fileTypeKey.SetValue("FriendlyTypeName", AppDescription);

                    // 设置默认图标
                    using (RegistryKey defaultIconKey = fileTypeKey.CreateSubKey("DefaultIcon"))
                    {
                        defaultIconKey.SetValue("", $"\"{exePath}\",0");
                    }

                    // 设置打开命令
                    using (RegistryKey shellKey = fileTypeKey.CreateSubKey("shell"))
                    using (RegistryKey openKey = shellKey.CreateSubKey("open"))
                    using (RegistryKey commandKey = openKey.CreateSubKey("command"))
                    {
                        commandKey.SetValue("", $"\"{exePath}\" \"%1\"");
                    }
                }

                // 注册文件扩展名
                using (RegistryKey extensionKey = Registry.ClassesRoot.CreateSubKey(FileExtension))
                {
                    extensionKey.SetValue("", FileTypeName);
                }

                // 刷新系统文件关联缓存
                RefreshSystemFileAssociations();

                LogHelper.WriteLogToFile($"成功注册{FileExtension}文件关联", LogHelper.LogType.Event);
                return true;
            }
            catch (SecurityException ex)
            {
                LogHelper.WriteLogToFile($"注册文件关联时权限不足: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                LogHelper.WriteLogToFile($"注册文件关联时访问被拒绝: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"注册文件关联时出错: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 注销.icstk文件关联
        /// </summary>
        public static bool UnregisterFileAssociation()
        {
            try
            {
                // 删除文件扩展名关联
                Registry.ClassesRoot.DeleteSubKeyTree(FileExtension, false);

                // 删除文件类型定义
                Registry.ClassesRoot.DeleteSubKeyTree(FileTypeName, false);

                // 刷新系统文件关联缓存
                RefreshSystemFileAssociations();

                LogHelper.WriteLogToFile($"成功注销{FileExtension}文件关联", LogHelper.LogType.Event);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"注销文件关联时出错: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 检查文件关联是否已注册
        /// </summary>
        public static bool IsFileAssociationRegistered()
        {
            try
            {
                using (RegistryKey extensionKey = Registry.ClassesRoot.OpenSubKey(FileExtension))
                {
                    if (extensionKey == null) return false;

                    string fileType = extensionKey.GetValue("") as string;
                    if (string.IsNullOrEmpty(fileType)) return false;

                    using (RegistryKey fileTypeKey = Registry.ClassesRoot.OpenSubKey(fileType))
                    {
                        if (fileTypeKey == null) return false;

                        using (RegistryKey shellKey = fileTypeKey.OpenSubKey("shell\\open\\command"))
                        {
                            if (shellKey == null) return false;

                            string command = shellKey.GetValue("") as string;
                            if (string.IsNullOrEmpty(command)) return false;

                            // 检查命令是否指向当前应用程序
                            string currentExePath = Process.GetCurrentProcess().MainModule.FileName;
                            return command.Contains(currentExePath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查文件关联状态时出错: {ex.Message}", LogHelper.LogType.Error);
            }

            return false;
        }

        /// <summary>
        /// 显示文件关联状态
        /// </summary>
        public static void ShowFileAssociationStatus()
        {
            bool isRegistered = IsFileAssociationRegistered();
            LogHelper.WriteLogToFile($"{FileExtension}文件关联状态: {(isRegistered ? "已注册" : "未注册")}", LogHelper.LogType.Event);
        }

        /// <summary>
        /// 刷新系统文件关联缓存
        /// </summary>
        private static void RefreshSystemFileAssociations()
        {
            try
            {
                // 通知系统文件关联已更改
                SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"刷新文件关联缓存时出错: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        /// <summary>
        /// 处理命令行参数中的文件路径
        /// </summary>
        /// <param name="args">命令行参数</param>
        /// <returns>找到的.icstk文件路径，如果没有找到则返回null</returns>
        public static string GetIcstkFileFromArgs(string[] args)
        {
            if (args == null || args.Length == 0) return null;

            foreach (string arg in args)
            {
                if (string.IsNullOrEmpty(arg)) continue;

                // 检查是否为.icstk文件
                if (Path.GetExtension(arg).Equals(FileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    // 检查文件是否存在
                    if (File.Exists(arg))
                    {
                        LogHelper.WriteLogToFile($"从命令行参数中找到.icstk文件: {arg}", LogHelper.LogType.Event);
                        return arg;
                    }
                    else
                    {
                        LogHelper.WriteLogToFile($"命令行参数中的.icstk文件不存在: {arg}", LogHelper.LogType.Warning);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 尝试通过IPC将文件路径发送给已运行的实例
        /// </summary>
        /// <param name="filePath">要打开的文件路径</param>
        /// <returns>是否成功发送</returns>
        public static bool TrySendFileToExistingInstance(string filePath)
        {
            try
            {
                LogHelper.WriteLogToFile($"尝试通过IPC发送文件路径给已运行实例: {filePath}", LogHelper.LogType.Event);

                // 创建IPC文件
                string tempDir = Path.GetTempPath();
                string ipcFileName = IpcFilePrefix + Guid.NewGuid().ToString("N") + ".tmp";
                string ipcFilePath = Path.Combine(tempDir, ipcFileName);

                // 写入文件路径到IPC文件
                File.WriteAllText(ipcFilePath, filePath, Encoding.UTF8);

                // 创建事件通知已运行实例
                using (EventWaitHandle ipcEvent = new EventWaitHandle(false, EventResetMode.ManualReset, IpcEventName))
                {
                    ipcEvent.Set();
                }

                // 等待一段时间让已运行实例处理文件
                Thread.Sleep(1000);

                // 清理IPC文件
                try
                {
                    if (File.Exists(ipcFilePath))
                    {
                        File.Delete(ipcFilePath);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"清理IPC文件失败: {ex.Message}", LogHelper.LogType.Warning);
                }

                LogHelper.WriteLogToFile("IPC文件路径发送完成", LogHelper.LogType.Event);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"通过IPC发送文件路径失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 尝试通过IPC将白板模式命令发送给已运行的实例
        /// </summary>
        /// <returns>是否成功发送</returns>
        public static bool TrySendBoardModeCommandToExistingInstance()
        {
            try
            {
                LogHelper.WriteLogToFile("尝试通过IPC发送白板模式命令给已运行实例", LogHelper.LogType.Event);

                // 创建IPC文件
                string tempDir = Path.GetTempPath();
                string ipcFileName = IpcBoardModePrefix + Guid.NewGuid().ToString("N") + ".tmp";
                string ipcFilePath = Path.Combine(tempDir, ipcFileName);

                // 写入白板模式命令到IPC文件
                File.WriteAllText(ipcFilePath, "BOARD_MODE", Encoding.UTF8);

                // 创建事件通知已运行实例
                using (EventWaitHandle ipcEvent = new EventWaitHandle(false, EventResetMode.ManualReset, IpcEventName))
                {
                    ipcEvent.Set();
                }

                // 等待一段时间让已运行实例处理命令
                Thread.Sleep(1000);

                // 清理IPC文件
                try
                {
                    if (File.Exists(ipcFilePath))
                    {
                        File.Delete(ipcFilePath);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"清理IPC文件失败: {ex.Message}", LogHelper.LogType.Warning);
                }

                LogHelper.WriteLogToFile("IPC白板模式命令发送完成", LogHelper.LogType.Event);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"通过IPC发送白板模式命令失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 尝试通过IPC将展开浮动栏命令发送给已运行的实例
        /// </summary>
        /// <returns>是否成功发送</returns>
        public static bool TrySendShowModeCommandToExistingInstance()
        {
            try
            {
                LogHelper.WriteLogToFile("尝试通过IPC发送展开浮动栏命令给已运行实例", LogHelper.LogType.Event);

                // 创建IPC文件
                string tempDir = Path.GetTempPath();
                string ipcFileName = IpcShowModePrefix + Guid.NewGuid().ToString("N") + ".tmp";
                string ipcFilePath = Path.Combine(tempDir, ipcFileName);

                // 写入展开浮动栏命令到IPC文件
                File.WriteAllText(ipcFilePath, "SHOW_MODE", Encoding.UTF8);

                // 创建事件通知已运行实例
                using (EventWaitHandle ipcEvent = new EventWaitHandle(false, EventResetMode.ManualReset, IpcEventName))
                {
                    ipcEvent.Set();
                }

                // 等待一段时间让已运行实例处理命令
                Thread.Sleep(1000);

                // 清理IPC文件
                try
                {
                    if (File.Exists(ipcFilePath))
                    {
                        File.Delete(ipcFilePath);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"清理IPC文件失败: {ex.Message}", LogHelper.LogType.Warning);
                }

                LogHelper.WriteLogToFile("IPC展开浮动栏命令发送完成", LogHelper.LogType.Event);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"通过IPC发送展开浮动栏命令失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 启动IPC监听器，等待其他实例发送文件路径
        /// </summary>
        public static void StartIpcListener()
        {
            try
            {
                Thread ipcThread = new Thread(() =>
                {
                    try
                    {
                        LogHelper.WriteLogToFile("启动IPC监听器", LogHelper.LogType.Event);

                        using (EventWaitHandle ipcEvent = new EventWaitHandle(false, EventResetMode.ManualReset, IpcEventName))
                        {
                            while (true)
                            {
                                // 等待IPC事件
                                if (ipcEvent.WaitOne(IpcTimeout))
                                {
                                    // 处理IPC文件
                                    ProcessIpcFiles();

                                    // 重置事件
                                    ipcEvent.Reset();
                                }

                                // 检查应用是否还在运行
                                if (Application.Current == null || Application.Current.Dispatcher == null)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"IPC监听器出错: {ex.Message}", LogHelper.LogType.Error);
                    }
                });

                ipcThread.IsBackground = true;
                ipcThread.Start();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动IPC监听器失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 处理IPC文件
        /// </summary>
        private static void ProcessIpcFiles()
        {
            try
            {
                string tempDir = Path.GetTempPath();
                
                // 处理文件路径IPC文件
                string[] ipcFiles = Directory.GetFiles(tempDir, IpcFilePrefix + "*.tmp");
                foreach (string ipcFile in ipcFiles)
                {
                    try
                    {
                        // 读取文件路径
                        string filePath = File.ReadAllText(ipcFile, Encoding.UTF8);

                        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                        {
                            LogHelper.WriteLogToFile($"IPC接收到文件路径: {filePath}", LogHelper.LogType.Event);

                            // 在UI线程中处理文件打开
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    // 获取主窗口并打开文件
                                    if (Application.Current.MainWindow is MainWindow mainWindow)
                                    {
                                        mainWindow.OpenSingleStrokeFile(filePath);
                                        mainWindow.ShowNotification($"已加载墨迹文件: {Path.GetFileName(filePath)}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogHelper.WriteLogToFile($"IPC处理文件打开失败: {ex.Message}", LogHelper.LogType.Error);
                                }
                            }));
                        }

                        // 删除IPC文件
                        File.Delete(ipcFile);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"处理IPC文件失败: {ex.Message}", LogHelper.LogType.Warning);

                        // 尝试删除损坏的IPC文件
                        try
                        {
                            if (File.Exists(ipcFile))
                            {
                                File.Delete(ipcFile);
                            }
                        }
                        catch { }
                    }
                }

                // 处理白板模式命令IPC文件
                string[] boardModeFiles = Directory.GetFiles(tempDir, IpcBoardModePrefix + "*.tmp");
                foreach (string ipcFile in boardModeFiles)
                {
                    try
                    {
                        // 读取命令内容
                        string command = File.ReadAllText(ipcFile, Encoding.UTF8);

                        if (command == "BOARD_MODE")
                        {
                            LogHelper.WriteLogToFile("IPC接收到白板模式命令", LogHelper.LogType.Event);

                            // 在UI线程中处理白板模式切换
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    // 获取主窗口并切换到白板模式
                                    if (Application.Current.MainWindow is MainWindow mainWindow)
                                    {
                                        mainWindow.SwitchToBoardMode();
                                        mainWindow.ShowNotification("已切换到白板模式");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogHelper.WriteLogToFile($"IPC处理白板模式切换失败: {ex.Message}", LogHelper.LogType.Error);
                                }
                            }));
                        }

                        // 删除IPC文件
                        File.Delete(ipcFile);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"处理白板模式IPC文件失败: {ex.Message}", LogHelper.LogType.Warning);

                        // 尝试删除损坏的IPC文件
                        try
                        {
                            if (File.Exists(ipcFile))
                            {
                                File.Delete(ipcFile);
                            }
                        }
                        catch { }
                    }
                }

                // 处理展开浮动栏命令IPC文件
                string[] showModeFiles = Directory.GetFiles(tempDir, IpcShowModePrefix + "*.tmp");
                foreach (string ipcFile in showModeFiles)
                {
                    try
                    {
                        // 读取命令内容
                        string command = File.ReadAllText(ipcFile, Encoding.UTF8);

                        if (command == "SHOW_MODE")
                        {
                            LogHelper.WriteLogToFile("IPC接收到展开浮动栏命令", LogHelper.LogType.Event);

                            // 在UI线程中处理展开浮动栏
                            Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
                            {
                                try
                                {
                                    // 获取主窗口并展开浮动栏
                                    if (Application.Current.MainWindow is MainWindow mainWindow)
                                    {
                                        // 如果当前处于收纳模式，则展开浮动栏
                                        if (mainWindow.isFloatingBarFolded)
                                        {
                                            await mainWindow.UnFoldFloatingBar(new object());
                                        }
                                        mainWindow.ShowNotification("已退出收纳模式并恢复浮动栏");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogHelper.WriteLogToFile($"IPC处理展开浮动栏失败: {ex.Message}", LogHelper.LogType.Error);
                                }
                            }));
                        }

                        // 删除IPC文件
                        File.Delete(ipcFile);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"处理展开浮动栏IPC文件失败: {ex.Message}", LogHelper.LogType.Warning);

                        // 尝试删除损坏的IPC文件
                        try
                        {
                            if (File.Exists(ipcFile))
                            {
                                File.Delete(ipcFile);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理IPC文件时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}