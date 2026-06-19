using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// VSTO PowerPoint 插件自动注册/反注册辅助类。
    /// 当用户切换到 Agent 架构时，自动将 VSTO DLL 注册为 PowerPoint 加载项。
    /// </summary>
    public static class VstoRegistrationHelper
    {
        private const string AddInKeyName = @"Software\Microsoft\Office\PowerPoint\Addins\InkCanvas.PowerPointAddIn";
        private const string AddInDllName = "InkCanvas.PowerPointAddIn.dll";
        private const string FriendlyName = "ICC PowerPoint Agent";
        private const string Description = "ICC PowerPoint Agent - NamedPipe PPT Linkage";

        /// <summary>
        /// VSTO 插件 DLL 相对于应用程序基目录的路径。
        /// </summary>
        private static string VstoDllPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ppt-agent", AddInDllName);

        /// <summary>
        /// VSTO Contracts DLL 路径（需与插件 DLL 同目录）。
        /// </summary>
        private static string ContractsDllPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ppt-agent", "InkCanvas.PptAgent.Contracts.dll");

        /// <summary>
        /// 检查 VSTO 插件是否已注册到 PowerPoint。
        /// </summary>
        public static bool IsRegistered()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(AddInKeyName))
                {
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查 VSTO 插件 DLL 是否存在于预期位置。
        /// </summary>
        public static bool IsDllAvailable()
        {
            return File.Exists(VstoDllPath);
        }

        /// <summary>
        /// 确保 VSTO 插件已注册。未注册则执行注册。
        /// </summary>
        /// <returns>注册是否成功或已注册。</returns>
        public static bool EnsureRegistered()
        {
            if (IsRegistered())
            {
                LogHelper.WriteLogToFile("VSTO 插件已注册，跳过", LogHelper.LogType.Trace);
                return true;
            }

            if (!IsDllAvailable())
            {
                LogHelper.WriteLogToFile($"VSTO 插件 DLL 不存在: {VstoDllPath}", LogHelper.LogType.Warning);
                return false;
            }

            return Register();
        }

        /// <summary>
        /// 注册 VSTO 插件到 PowerPoint。
        /// </summary>
        public static bool Register()
        {
            if (!IsDllAvailable())
            {
                LogHelper.WriteLogToFile($"VSTO 插件 DLL 不存在: {VstoDllPath}", LogHelper.LogType.Warning);
                return false;
            }

            // 1. 执行 regasm 注册 COM 组件
            if (!RunRegasm(false))
            {
                LogHelper.WriteLogToFile("VSTO regasm 注册失败", LogHelper.LogType.Error);
                return false;
            }

            // 2. 写入加载项注册表
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(AddInKeyName))
                {
                    if (key == null)
                    {
                        LogHelper.WriteLogToFile("无法创建注册表项", LogHelper.LogType.Error);
                        return false;
                    }
                    key.SetValue("Description", Description, RegistryValueKind.String);
                    key.SetValue("FriendlyName", FriendlyName, RegistryValueKind.String);
                    key.SetValue("LoadBehavior", 3, RegistryValueKind.DWord);
                }

                LogHelper.WriteLogToFile("VSTO 插件注册成功", LogHelper.LogType.Event);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"写入 VSTO 注册表失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 反注册 VSTO 插件。
        /// </summary>
        public static bool Unregister()
        {
            // 1. 执行 regasm /u 反注册
            if (File.Exists(VstoDllPath))
            {
                RunRegasm(true);
            }

            // 2. 删除注册表项
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(AddInKeyName, false);
                LogHelper.WriteLogToFile("VSTO 插件已反注册", LogHelper.LogType.Event);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"VSTO 反注册失败: {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
        }

        /// <summary>
        /// 执行 regasm.exe 注册或反注册。
        /// </summary>
        /// <param name="unregister">true 为反注册 (/u)，false 为注册。</param>
        private static bool RunRegasm(bool unregister)
        {
            var regasmPaths = new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    @"Microsoft.NET\Framework64\v4.0.30319\regasm.exe"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    @"Microsoft.NET\Framework\v4.0.30319\regasm.exe")
            };

            string regasmExe = null;
            foreach (var path in regasmPaths)
            {
                if (File.Exists(path))
                {
                    regasmExe = path;
                    break;
                }
            }

            if (regasmExe == null)
            {
                LogHelper.WriteLogToFile("未找到 regasm.exe", LogHelper.LogType.Error);
                return false;
            }

            string args = unregister
                ? $"\"{VstoDllPath}\" /u"
                : $"\"{VstoDllPath}\" /codebase /tlb";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = regasmExe,
                    Arguments = args,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };

                var process = Process.Start(psi);
                if (process == null) return false;

                process.WaitForExit(15000);
                bool success = process.ExitCode == 0;
                process.Dispose();

                if (success)
                {
                    LogHelper.WriteLogToFile(
                        $"regasm {(unregister ? "反注册" : "注册")}成功: {VstoDllPath}",
                        LogHelper.LogType.Trace);
                }
                else
                {
                    LogHelper.WriteLogToFile(
                        $"regasm 退出码 {process.ExitCode}: {VstoDllPath}",
                        LogHelper.LogType.Warning);
                }

                return success;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"regasm 执行失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }
    }
}
