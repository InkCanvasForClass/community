using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// IACore DLL自动释放器
    /// 在应用启动时自动释放IACore相关的DLL文件到应用程序目录
    /// </summary>
    public static class IACoreDllExtractor
    {
        private static readonly string[] RequiredDlls = {
            "IACore.dll",
            "IALoader.dll", 
            "IAWinFX.dll"
        };

        /// <summary>
        /// 在应用启动时释放IACore相关DLL
        /// </summary>
        public static void ExtractIACoreDlls()
        {
            try
            {
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                LogHelper.WriteLogToFile("开始检查并释放IACore相关DLL文件");

                foreach (string dllName in RequiredDlls)
                {
                    string targetPath = Path.Combine(appDirectory, dllName);
                    
                    // 检查文件是否已存在且有效
                    if (File.Exists(targetPath) && IsValidDll(targetPath))
                    {
                        LogHelper.WriteLogToFile($"{dllName} 已存在且有效，跳过释放");
                        continue;
                    }

                    // 从嵌入资源中释放DLL
                    if (ExtractDllFromResource(dllName, targetPath))
                    {
                        LogHelper.WriteLogToFile($"成功释放 {dllName} 到 {targetPath}");
                    }
                    else
                    {
                        LogHelper.WriteLogToFile($"警告：无法释放 {dllName}，可能影响形状识别功能", LogHelper.LogType.Warning);
                    }
                }

                LogHelper.WriteLogToFile("IACore DLL释放检查完成");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"释放IACore DLL时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 从嵌入资源中提取DLL文件
        /// </summary>
        private static bool ExtractDllFromResource(string dllName, string targetPath)
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                string resourceName = $"Ink_Canvas.Resources.IACore.{dllName}";

                using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (resourceStream == null)
                    {
                        LogHelper.WriteLogToFile($"未找到嵌入资源: {resourceName}", LogHelper.LogType.Warning);
                        return false;
                    }

                    // 确保目标目录存在
                    string targetDirectory = Path.GetDirectoryName(targetPath);
                    if (!Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    // 写入文件
                    using (FileStream fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                    {
                        resourceStream.CopyTo(fileStream);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"从资源提取 {dllName} 失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 检查DLL文件是否有效
        /// </summary>
        private static bool IsValidDll(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                FileInfo fileInfo = new FileInfo(filePath);
                
                // 检查文件大小（空文件或过小的文件可能无效）
                if (fileInfo.Length < 1024) // 小于1KB可能无效
                    return false;

                // 简单检查PE头（DLL文件应该以MZ开头）
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] buffer = new byte[2];
                    if (fs.Read(buffer, 0, 2) == 2)
                    {
                        return buffer[0] == 0x4D && buffer[1] == 0x5A; // "MZ"
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 清理释放的DLL文件（可选，在应用退出时调用）
        /// </summary>
        public static void CleanupExtractedDlls()
        {
            try
            {
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                
                foreach (string dllName in RequiredDlls)
                {
                    string filePath = Path.Combine(appDirectory, dllName);
                    
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            File.Delete(filePath);
                            LogHelper.WriteLogToFile($"已清理 {dllName}");
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"清理 {dllName} 失败: {ex.Message}", LogHelper.LogType.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"清理IACore DLL时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
    }
}
