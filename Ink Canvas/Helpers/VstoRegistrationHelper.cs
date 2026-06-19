using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// VSTO PowerPoint 插件自动注册/反注册辅助类。
    /// 优先使用 .vsto 清单加载，回退到 regasm COM 注册。
    /// </summary>
    public static class VstoRegistrationHelper
    {
        private const string AddInKeyName = @"Software\Microsoft\Office\PowerPoint\Addins\InkCanvas.PowerPointAddIn";
        private const string AddInDllName = "InkCanvas.PowerPointAddIn.dll";
        private const string AddInVstoName = "InkCanvas.PowerPointAddIn.vsto";
        private const string FriendlyName = "ICC PowerPoint Agent";
        private const string Description = "ICC PowerPoint Agent - NamedPipe PPT Linkage";

        private static string AgentDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ppt-agent");
        private static string VstoManifestPath => Path.Combine(AgentDir, AddInVstoName);
        private static string VstoDllPath => Path.Combine(AgentDir, AddInDllName);

        public static bool IsRegistered()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(AddInKeyName))
                {
                    return key != null;
                }
            }
            catch { return false; }
        }

        /// <summary>
        /// 检查 VSTO 插件文件是否可用（优先检查 .vsto 清单，回退到 DLL）。
        /// </summary>
        public static bool IsVstoAvailable() => File.Exists(VstoManifestPath) || File.Exists(VstoDllPath);

        public static bool EnsureRegistered()
        {
            CleanupRegistry();

            if (!IsVstoAvailable())
            {
                LogHelper.WriteLogToFile($"VSTO 插件文件不存在: {AgentDir}", LogHelper.LogType.Warning);
                return false;
            }
            return Register();
        }

        public static bool Register()
        {
            if (!IsVstoAvailable())
            {
                LogHelper.WriteLogToFile($"VSTO 插件文件不存在: {AgentDir}", LogHelper.LogType.Warning);
                return false;
            }

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

                    // 优先使用 .vsto 清单（ClickOnce 加载方式，不需要 regasm）
                    if (File.Exists(VstoManifestPath))
                    {
                        string manifestUrl = $"file:///{VstoManifestPath.Replace("\\", "/")}";
                        key.SetValue("Manifest", manifestUrl, RegistryValueKind.String);
                        LogHelper.WriteLogToFile($"VSTO 插件注册成功（.vsto 清单）: {VstoManifestPath}", LogHelper.LogType.Event);
                    }
                    else
                    {
                        // 回退到 regasm COM 注册
                        bool regasmOk = RunRegasm("/codebase /tlb");
                        LogHelper.WriteLogToFile(regasmOk
                            ? "VSTO 插件注册成功（regasm + 注册表）"
                            : "VSTO 插件注册表已写入（regasm 失败）",
                            regasmOk ? LogHelper.LogType.Event : LogHelper.LogType.Warning);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"写入 VSTO 注册表失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        public static void CleanupRegistry()
        {
            try
            {
                if (Registry.CurrentUser.OpenSubKey(AddInKeyName) != null)
                {
                    Registry.CurrentUser.DeleteSubKeyTree(AddInKeyName, false);
                    LogHelper.WriteLogToFile("VSTO 旧注册表项已清理", LogHelper.LogType.Trace);
                }
            }
            catch { }
        }

        public static bool Unregister()
        {
            try
            {
                RunRegasm("/u");
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

        private static bool RunRegasm(string extraArgs)
        {
            string regasm = FindRegasm();
            if (regasm == null)
            {
                LogHelper.WriteLogToFile("未找到 regasm.exe", LogHelper.LogType.Error);
                return false;
            }

            string args = $"\"{VstoDllPath}\" {extraArgs}";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = regasm,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                var process = Process.Start(psi);
                if (process == null) return false;

                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit(30000);
                int exitCode = process.ExitCode;
                process.Dispose();

                if (exitCode == 0)
                {
                    LogHelper.WriteLogToFile($"regasm 成功: {args}", LogHelper.LogType.Trace);
                    return true;
                }
                else
                {
                    LogHelper.WriteLogToFile($"regasm 退出码 {exitCode}: {stderr}", LogHelper.LogType.Warning);
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"regasm 异常: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        private static string FindRegasm()
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                @"Microsoft.NET\Framework64\v4.0.30319\regasm.exe");
            if (File.Exists(root)) return root;

            root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                @"Microsoft.NET\Framework\v4.0.30319\regasm.exe");
            if (File.Exists(root)) return root;

            return null;
        }
    }
}
