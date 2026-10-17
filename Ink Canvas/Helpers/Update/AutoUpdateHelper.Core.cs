using Ink_Canvas.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Helpers
{
    internal partial class AutoUpdateHelper
    {
        // 定义超时时间为10秒
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
        private static readonly string updatesFolderPath = Path.Combine(AppContext.BaseDirectory, "AutoUpdate");
        private static string statusFilePath;
        private static readonly HashSet<string> UpdateFilesToOverwrite = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "InkCanvas.IACoreHelper.exe",
            "InkCanvas.IACoreHelper.exe.config",
            "InkCanvas.LiquidGlassMagHost.deps.json",
            "InkCanvas.LiquidGlassMagHost.dll",
            "InkCanvas.LiquidGlassMagHost.exe",
            "InkCanvas.LiquidGlassMagHost.runtimeconfig.json",
            "InkCanvasForClass.deps.json",
            "InkCanvasForClass.dll",
            "InkCanvasForClass.dll.config",
            "InkCanvasForClass.exe",
            "InkCanvasForClass.runtimeconfig.json"
        };
        private static readonly HashSet<string> UpdateDirectoriesToOverwrite = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "runtimes"
        };
        // 全局下载取消令牌集合；UI 通过 RequestCancelDownload 取消所有进行中的下载。
        // 并发下载会话互不取消（避免静默更新定时器二次触发把正在进行的下载误判为用户取消）。
        private static readonly HashSet<CancellationTokenSource> _activeDownloadSessions = new HashSet<CancellationTokenSource>();
        private static readonly object _activeDownloadLock = new object();

        // 下载失败原因分类，供 UI 展示精确提示（避免把完整性/文件占用错误统一显示为"网络错误"）
        public enum DownloadFailureReason
        {
            None,
            NetworkOrAllLinesFailed,
            IntegrityCheckFailed,
            FileInUse,
            MergeFailed,
            Cancelled
        }

        public static DownloadFailureReason LastDownloadFailure { get; private set; } = DownloadFailureReason.None;

        private static void SetDownloadFailure(DownloadFailureReason reason)
        {
            LastDownloadFailure = reason;
        }

        public static void RequestCancelDownload()
        {
            lock (_activeDownloadLock)
            {
                foreach (var cts in _activeDownloadSessions)
                {
                    try { cts?.Cancel(); } catch (Exception ex) { LogService.LogException(ex); }
                }
            }
        }

        private static CancellationTokenSource BeginDownloadSession()
        {
            var cts = new CancellationTokenSource();
            lock (_activeDownloadLock)
            {
                _activeDownloadSessions.Add(cts);
            }
            return cts;
        }

        private static void EndDownloadSession(CancellationTokenSource cts)
        {
            lock (_activeDownloadLock)
            {
                _activeDownloadSessions.Remove(cts);
            }
            try { cts?.Dispose(); } catch (Exception ex) { LogService.LogException(ex); }
        }

        public static bool IsX64UpdatePackageSelected()
        {
            try
            {
                return MainWindow.Settings?.Startup?.UpdatePackageArchitecture == UpdatePackageArchitecture.X64;
            }
            catch
            {
                return Environment.Is64BitProcess;
            }
        }

        private static string NormalizeVersionForUpdate(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return version;
            return version.Trim().TrimStart('v', 'V');
        }

        public static string AppendX64SuffixBeforeZipExtension(string url)
        {
            if (string.IsNullOrEmpty(url) || !IsX64UpdatePackageSelected()) return url;
            int query = url.IndexOf('?');
            string pathPart = query >= 0 ? url.Substring(0, query) : url;
            string qs = query >= 0 ? url.Substring(query) : "";
            const string ext = ".zip";
            int idx = pathPart.LastIndexOf(ext, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return url;
            var basePart = pathPart.Substring(0, idx);
            if (basePart.EndsWith("-x64", StringComparison.OrdinalIgnoreCase)) return url;
            return basePart + "-x64" + ext + qs;
        }

        public static string GetUpdateZipFileName(string version)
        {
            var v = NormalizeVersionForUpdate(version);
            return IsX64UpdatePackageSelected()
                ? $"InkCanvasForClass.CE.{v}-x64.zip"
                : $"InkCanvasForClass.CE.{v}.zip";
        }

        public static string GetUpdateDownloadStatusFilePath(string version)
        {
            var v = NormalizeVersionForUpdate(version);
            string name = IsX64UpdatePackageSelected()
                ? $"DownloadV{v}_x64Status.txt"
                : $"DownloadV{v}Status.txt";
            return Path.Combine(updatesFolderPath, name);
        }

        public static string GetLocalUpdateZipFilePath(string version)
        {
            return Path.Combine(updatesFolderPath, GetUpdateZipFileName(version));
        }

        private static string PickBrowserDownloadUrlFromAssets(JToken assets)
        {
            if (assets == null || !assets.Any()) return null;
            bool wantX64 = IsX64UpdatePackageSelected();
            string anyZip = null;
            string x64Zip = null;
            string nonX64Zip = null;
            foreach (JToken a in assets)
            {
                string name = a["name"]?.ToString();
                string url = a["browser_download_url"]?.ToString();
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url)) continue;
                if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                if (anyZip == null) anyZip = url;
                if (name.EndsWith("-x64.zip", StringComparison.OrdinalIgnoreCase))
                {
                    if (x64Zip == null) x64Zip = url;
                }
                else
                {
                    if (nonX64Zip == null) nonX64Zip = url;
                }
            }
            if (wantX64)
                return x64Zip ?? anyZip;
            return nonX64Zip ?? anyZip;
        }

        // 线路组结构体（包含版本、下载、日志地址）
        public class UpdateLineGroup
        {
            public string GroupName { get; set; } // 组名
            public string VersionUrl { get; set; } // 版本检测地址
            public string DownloadUrlFormat { get; set; } // 下载地址格式（带{0}占位符）
            public string LogUrl { get; set; } // 更新日志地址
        }

        // 通道-线路组映射
        public static readonly Dictionary<UpdateChannel, List<UpdateLineGroup>> ChannelLineGroups = new Dictionary<UpdateChannel, List<UpdateLineGroup>>
        {
            { UpdateChannel.Release, new List<UpdateLineGroup>
                {
                    new UpdateLineGroup
                    {
                        GroupName = "GitHub主线",
                        VersionUrl = "https://github.com/InkCanvasForClass/community/raw/refs/heads/net10/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://github.com/InkCanvasForClass/community/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://github.com/InkCanvasForClass/community/raw/refs/heads/net10/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "bgithub备用",
                        VersionUrl = "https://bgithub.xyz/InkCanvasForClass/community/raw/refs/heads/net10/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://bgithub.xyz/InkCanvasForClass/community/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://bgithub.xyz/InkCanvasForClass/community/raw/refs/heads/net10/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "kkgithub线路",
                        VersionUrl = "https://kkgithub.com/InkCanvasForClass/community/raw/refs/heads/net10/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://kkgithub.com/InkCanvasForClass/community/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://kkgithub.com/InkCanvasForClass/community/raw/refs/heads/net10/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "智教联盟",
                        DownloadUrlFormat = "https://get.smart-teach.cn/d/Ningbo-S3/shared/jiangling/community/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://bgithub.xyz/InkCanvasForClass/community/raw/refs/heads/net10/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "inkeys",
                        DownloadUrlFormat = "https://iccce.inkeys.top/Release/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://bgithub.xyz/InkCanvasForClass/community/raw/refs/heads/net10/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "cnb",
                        DownloadUrlFormat = "https://cnb.cool/InkCanvasForClass/downloads/-/git/raw/main/Release/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://bgithub.xyz/InkCanvasForClass/community/raw/refs/heads/net10/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "gh-proxy",
                        VersionUrl = "https://gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community/refs/heads/net10/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://gh-proxy.org/https://github.com/InkCanvasForClass/community/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community/refs/heads/net10/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "hk.gh-proxy",
                        VersionUrl = "https://hk.gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community/refs/heads/net10/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://hk.gh-proxy.org/https://github.com/InkCanvasForClass/community/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://hk.gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community/refs/heads/net10/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "cdn.gh-proxy",
                        VersionUrl = "https://cdn.gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community/refs/heads/net10/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://cdn.gh-proxy.org/https://github.com/InkCanvasForClass/community/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://cdn.gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community/refs/heads/net10/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "edgeone.gh-proxy",
                        VersionUrl = "https://edgeone.gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community/refs/heads/net10/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://edgeone.gh-proxy.org/https://github.com/InkCanvasForClass/community/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://edgeone.gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community/refs/heads/net10/UpdateLog.md"
                    }
                }
            },
            { UpdateChannel.Preview, new List<UpdateLineGroup>
                {
                    new UpdateLineGroup
                    {
                        GroupName = "GitHub主线",
                        VersionUrl = "https://github.com/InkCanvasForClass/community-beta/raw/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://github.com/InkCanvasForClass/community-beta/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://github.com/InkCanvasForClass/community-beta/raw/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "bgithub备用",
                        VersionUrl = "https://bgithub.xyz/InkCanvasForClass/community-beta/raw/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://bgithub.xyz/InkCanvasForClass/community-beta/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://bgithub.xyz/InkCanvasForClass/community-beta/raw/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "kkgithub线路",
                        VersionUrl = "https://kkgithub.com/InkCanvasForClass/community-beta/raw/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://kkgithub.com/InkCanvasForClass/community-beta/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://kkgithub.com/InkCanvasForClass/community-beta/raw/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "智教联盟",
                        DownloadUrlFormat = "https://get.smart-teach.cn/d/Ningbo-S3/shared/jiangling/community-beta/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://bgithub.xyz/InkCanvasForClass/community-beta/raw/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "inkeys",
                        DownloadUrlFormat = "https://iccce.inkeys.top/Beta/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://bgithub.xyz/InkCanvasForClass/community-beta/raw/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "cnb",
                        DownloadUrlFormat = "https://cnb.cool/InkCanvasForClass/downloads/-/git/raw/main/Beta/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://bgithub.xyz/InkCanvasForClass/community-beta/raw/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "gh-proxy",
                        VersionUrl = "https://gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community-beta/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://gh-proxy.org/https://github.com/InkCanvasForClass/community-beta/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community-beta/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "hk.gh-proxy",
                        VersionUrl = "https://hk.gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community-beta/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://hk.gh-proxy.org/https://github.com/InkCanvasForClass/community-beta/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://hk.gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community-beta/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "cdn.gh-proxy",
                        VersionUrl = "https://cdn.gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community-beta/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://cdn.gh-proxy.org/https://github.com/InkCanvasForClass/community-beta/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://cdn.gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community-beta/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "edgeone.gh-proxy",
                        VersionUrl = "https://edgeone.gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community-beta/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://edgeone.gh-proxy.org/https://github.com/InkCanvasForClass/community-beta/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://edgeone.gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community-beta/refs/heads/main/UpdateLog.md"
                    }
                }
            },
            { UpdateChannel.Beta, new List<UpdateLineGroup>
                {
                    new UpdateLineGroup
                    {
                        GroupName = "GitHub主线",
                        VersionUrl = "https://github.com/InkCanvasForClass/community-beta/raw/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://github.com/InkCanvasForClass/community-beta/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://github.com/InkCanvasForClass/community-beta/raw/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "bgithub备用",
                        VersionUrl = "https://bgithub.xyz/InkCanvasForClass/community-beta/raw/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://bgithub.xyz/InkCanvasForClass/community-beta/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://bgithub.xyz/InkCanvasForClass/community-beta/raw/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "kkgithub线路",
                        VersionUrl = "https://kkgithub.com/InkCanvasForClass/community-beta/raw/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://kkgithub.com/InkCanvasForClass/community-beta/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://kkgithub.com/InkCanvasForClass/community-beta/raw/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "智教联盟",
                        DownloadUrlFormat = "https://get.smart-teach.cn/d/Ningbo-S3/shared/jiangling/community-beta/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://bgithub.xyz/InkCanvasForClass/community-beta/raw/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "inkeys",
                        DownloadUrlFormat = "https://iccce.inkeys.top/Beta/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://bgithub.xyz/InkCanvasForClass/community-beta/raw/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "cnb",
                        DownloadUrlFormat = "https://cnb.cool/InkCanvasForClass/downloads/-/git/raw/main/Beta/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://bgithub.xyz/InkCanvasForClass/community-beta/raw/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "gh-proxy",
                        VersionUrl = "https://gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community-beta/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://gh-proxy.org/https://github.com/InkCanvasForClass/community-beta/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community-beta/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "hk.gh-proxy",
                        VersionUrl = "https://hk.gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community-beta/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://hk.gh-proxy.org/https://github.com/InkCanvasForClass/community-beta/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://hk.gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community-beta/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "cdn.gh-proxy",
                        VersionUrl = "https://cdn.gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community-beta/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://cdn.gh-proxy.org/https://github.com/InkCanvasForClass/community-beta/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://cdn.gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community-beta/refs/heads/main/UpdateLog.md"
                    },
                    new UpdateLineGroup
                    {
                        GroupName = "edgeone.gh-proxy",
                        VersionUrl = "https://edgeone.gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community-beta/refs/heads/main/AutomaticUpdateVersionControl.txt",
                        DownloadUrlFormat = "https://edgeone.gh-proxy.org/https://github.com/InkCanvasForClass/community-beta/releases/download/{0}/InkCanvasForClass.CE.{0}.zip",
                        LogUrl = "https://edgeone.gh-proxy.org/https://raw.githubusercontent.com/InkCanvasForClass/community-beta/refs/heads/main/UpdateLog.md"
                    }
                }
            }
        };

    }

    internal class AutoUpdateWithSilenceTimeComboBox
    {
        public static ObservableCollection<string> Hours { get; set; } = new ObservableCollection<string>();
        public static ObservableCollection<string> Minutes { get; set; } = new ObservableCollection<string>();

        public static void InitializeAutoUpdateWithSilenceTimeComboBoxOptions(ComboBox startTimeComboBox, ComboBox endTimeComboBox)
        {
            for (int hour = 0; hour <= 23; ++hour)
            {
                Hours.Add(hour.ToString("00"));
            }
            for (int minute = 0; minute <= 59; minute += 20)
            {
                Minutes.Add(minute.ToString("00"));
            }
            startTimeComboBox.ItemsSource = Hours.SelectMany(h => Minutes.Select(m => $"{h}:{m}"));
            endTimeComboBox.ItemsSource = Hours.SelectMany(h => Minutes.Select(m => $"{h}:{m}"));
        }

        public static bool CheckIsInSilencePeriod(string startTime, string endTime)
        {
            if (startTime == endTime) return true;
            DateTime currentTime = DateTime.Now;

            DateTime StartTime = DateTime.ParseExact(startTime, "HH:mm", null);
            DateTime EndTime = DateTime.ParseExact(endTime, "HH:mm", null);
            if (StartTime <= EndTime)
            { // 单日时间段
                return currentTime >= StartTime && currentTime <= EndTime;
            }  // 跨越两天的时间段
            return currentTime >= StartTime || currentTime <= EndTime;
        }
    }
}
