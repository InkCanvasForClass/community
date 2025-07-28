using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace Ink_Canvas.Helpers.Plugins.BuiltIn.SuperLauncher
{
    /// <summary>
    /// 启动台按钮位置
    /// </summary>
    public enum LauncherButtonPosition
    {
        /// <summary>
        /// 左侧
        /// </summary>
        Left,
        
        /// <summary>
        /// 右侧
        /// </summary>
        Right
    }
    
    /// <summary>
    /// 启动台配置
    /// </summary>
    public class LauncherConfig
    {
        /// <summary>
        /// 启动台按钮位置
        /// </summary>
        public LauncherButtonPosition ButtonPosition { get; set; } = LauncherButtonPosition.Right;
        
        /// <summary>
        /// 启动台应用程序列表
        /// </summary>
        public List<LauncherItem> Items { get; set; } = new List<LauncherItem>();
    }
    
    /// <summary>
    /// 启动台应用项
    /// </summary>
    public class LauncherItem
    {
        /// <summary>
        /// 应用程序名称
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// 应用程序路径
        /// </summary>
        public string Path { get; set; }
        
        /// <summary>
        /// 是否可见
        /// </summary>
        public bool IsVisible { get; set; } = true;
        
        /// <summary>
        /// 在启动台中的位置（0-39）
        /// </summary>
        public int Position { get; set; } = -1;
        
        /// <summary>
        /// 是否已固定位置
        /// </summary>
        public bool IsPositionFixed { get; set; } = false;
        
        /// <summary>
        /// 图标缓存
        /// </summary>
        [JsonIgnore]
        private ImageSource _iconCache;
        
        /// <summary>
        /// 获取应用程序图标
        /// </summary>
        [JsonIgnore]
        public ImageSource Icon
        {
            get
            {
                if (_iconCache != null)
                {
                    return _iconCache;
                }
                
                try
                {
                    if (File.Exists(Path))
                    {
                        // 从文件中获取图标
                        Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(Path);
                        if (icon != null)
                        {
                            _iconCache = Imaging.CreateBitmapSourceFromHIcon(
                                icon.Handle,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                            
                            icon.Dispose();
                            return _iconCache;
                        }
                    }
                    else
                    {
                        // 从注册表中获取文件类型关联图标
                        string extension = System.IO.Path.GetExtension(Path);
                        if (!string.IsNullOrEmpty(extension))
                        {
                            string fileType = Registry.ClassesRoot.OpenSubKey(extension)?.GetValue(string.Empty) as string;
                            if (!string.IsNullOrEmpty(fileType))
                            {
                                string iconPath = Registry.ClassesRoot.OpenSubKey(fileType + "\\DefaultIcon")?.GetValue(string.Empty) as string;
                                if (!string.IsNullOrEmpty(iconPath))
                                {
                                    string[] parts = iconPath.Split(',');
                                    string iconFile = parts[0].Trim('"');
                                    int iconIndex = parts.Length > 1 ? Convert.ToInt32(parts[1]) : 0;
                                    
                                    if (File.Exists(iconFile))
                                    {
                                        Icon icon = IconExtractor.Extract(iconFile, iconIndex, true);
                                        if (icon != null)
                                        {
                                            _iconCache = Imaging.CreateBitmapSourceFromHIcon(
                                                icon.Handle,
                                                Int32Rect.Empty,
                                                BitmapSizeOptions.FromEmptyOptions());
                                            
                                            icon.Dispose();
                                            return _iconCache;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"获取应用图标时出错: {ex.Message}", LogHelper.LogType.Error);
                }
                
                // 返回默认图标
                return GetDefaultIcon();
            }
        }
        
        /// <summary>
        /// 获取默认图标
        /// </summary>
        private ImageSource GetDefaultIcon()
        {
            try
            {
                // 对于资源管理器，使用特定图标
                if (Path.EndsWith("explorer.exe", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        // 直接从C:\Windows\explorer.exe获取图标
                        string explorerPath = @"C:\Windows\explorer.exe";
                        if (File.Exists(explorerPath))
                        {
                            Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(explorerPath);
                            if (icon != null)
                            {
                                _iconCache = Imaging.CreateBitmapSourceFromHIcon(
                                    icon.Handle,
                                    Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());
                                
                                icon.Dispose();
                                return _iconCache;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"获取资源管理器图标时出错: {ex.Message}", LogHelper.LogType.Warning);
                        // 如果获取Windows图标失败，回退到默认图标
                    }
                    
                    // 回退到备用图标
                    string explorerIconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Icons-Fluent", "ic_fluent_folder_24_regular.png");
                    if (File.Exists(explorerIconPath))
                    {
                        Uri uri = new Uri(explorerIconPath);
                        BitmapImage image = new BitmapImage(uri);
                        _iconCache = image;
                        return _iconCache;
                    }
                }
                
                // 返回一个简单的默认图标
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Icons-png", "icc.png");
                if (File.Exists(iconPath))
                {
                    Uri uri = new Uri(iconPath);
                    BitmapImage image = new BitmapImage(uri);
                    _iconCache = image;
                    return _iconCache;
                }
                
                // 如果还是没有找到，尝试使用应用程序图标
                string appIconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Icons-Fluent", "ic_fluent_apps_24_regular.png");
                if (File.Exists(appIconPath))
                {
                    Uri uri = new Uri(appIconPath);
                    BitmapImage image = new BitmapImage(uri);
                    _iconCache = image;
                    return _iconCache;
                }
            }
            catch (Exception ex) 
            {
                LogHelper.WriteLogToFile($"获取默认图标时出错: {ex.Message}", LogHelper.LogType.Error);
            }
            
            return null;
        }
        
        /// <summary>
        /// 启动应用程序
        /// </summary>
        public void Launch()
        {
            try
            {
                if (string.IsNullOrEmpty(Path))
                {
                    LogHelper.WriteLogToFile("无法启动应用程序：路径为空", LogHelper.LogType.Error);
                    return;
                }
                
                // 检查文件是否存在
                if (!File.Exists(Path) && !Path.Contains(":\\"))
                {
                    // 可能是系统命令，如explorer.exe
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = Path,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                else
                {
                    // 使用Process.Start启动应用程序
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = Path,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                
                LogHelper.WriteLogToFile($"已启动应用程序: {Path}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动应用程序时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"启动应用程序时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    /// <summary>
    /// 图标提取工具类
    /// </summary>
    public static class IconExtractor
    {
        /// <summary>
        /// 从文件中提取图标
        /// </summary>
        /// <param name="file">文件路径</param>
        /// <param name="index">图标索引</param>
        /// <param name="largeIcon">是否提取大图标</param>
        /// <returns>提取的图标</returns>
        public static Icon Extract(string file, int index, bool largeIcon)
        {
            try
            {
                IntPtr large;
                IntPtr small;
                ExtractIconEx(file, index, out large, out small, 1);
                
                try
                {
                    return Icon.FromHandle(largeIcon ? large : small);
                }
                catch
                {
                    return null;
                }
                finally
                {
                    if (large != IntPtr.Zero)
                        DestroyIcon(large);
                    
                    if (small != IntPtr.Zero)
                        DestroyIcon(small);
                }
            }
            catch
            {
                return null;
            }
        }
        
        [DllImport("Shell32.dll", EntryPoint = "ExtractIconEx")]
        private static extern int ExtractIconEx(
            [MarshalAs(UnmanagedType.LPStr)] string lpszFile,
            int nIconIndex,
            out IntPtr phiconLarge,
            out IntPtr phiconSmall,
            int nIcons);
        
        [DllImport("User32.dll")]
        private static extern int DestroyIcon(IntPtr hIcon);
    }
} 