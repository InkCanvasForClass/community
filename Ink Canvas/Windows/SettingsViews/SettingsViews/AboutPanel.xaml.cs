using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Helpers;
using OSVersionExtension;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ink_Canvas.Windows.SettingsViews
{
    /// <summary>
    /// AboutPanel.xaml 的交互逻辑
    /// </summary>
    public partial class AboutPanel : UserControl
    {
        public AboutPanel()
        {
            InitializeComponent();

            // 关于页面图片横幅
            if (File.Exists(App.RootPath + "icc-about-illustrations.png"))
            {
                try
                {
                    CopyrightBannerImage.Visibility = Visibility.Visible;
                    CopyrightBannerImage.Source =
                        new BitmapImage(new Uri($"file://{App.RootPath + "icc-about-illustrations.png"}"));
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            }
            else
            {
                CopyrightBannerImage.Visibility = Visibility.Collapsed;
            }

            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                AboutAppVersion.Text = $"InkCanvasForClass v{version}";
            }
            catch (Exception ex)
            {
                AboutAppVersion.Text = "InkCanvasForClass v未知";
                System.Diagnostics.Debug.WriteLine($"获取软件版本失败: {ex.Message}");
            }

            try
            {
                string deviceId = DeviceIdentifier.GetDeviceId();
                AboutDeviceID.Text = deviceId;
            }
            catch (Exception ex)
            {
                AboutDeviceID.Text = "获取失败";
                System.Diagnostics.Debug.WriteLine($"获取设备ID失败: {ex.Message}");
            }

            try
            {
                var copyright = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCopyrightAttribute>();
                if (copyright != null && !string.IsNullOrEmpty(copyright.Copyright))
                {
                    var copyrightText = copyright.Copyright;
                    AboutCopyright.Text = copyrightText;
                    AboutBottomCopyright.Text = copyrightText.Replace("Copyright ©", "© Copyright") + " 所有";
                }
                else
                {
                    AboutCopyright.Text = "© Copyright 2025-2026 CJK_mkp 所有";
                    AboutBottomCopyright.Text = "© Copyright 2025-2026 CJK_mkp 所有";
                }
            }
            catch (Exception ex)
            {
                AboutCopyright.Text = "© Copyright 2025-2026 CJK_mkp 所有";
                AboutBottomCopyright.Text = "© Copyright 2025-2026 CJK_mkp 所有";
                System.Diagnostics.Debug.WriteLine($"获取版权信息失败: {ex.Message}");
            }

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var filePath = assembly.Location;

                if (File.Exists(filePath))
                {
                    var bt = File.GetCreationTime(filePath);
                    var m = bt.Month.ToString().PadLeft(2, '0');
                    var d = bt.Day.ToString().PadLeft(2, '0');
                    var h = bt.Hour.ToString().PadLeft(2, '0');
                    var min = bt.Minute.ToString().PadLeft(2, '0');
                    var s = bt.Second.ToString().PadLeft(2, '0');
                    AboutBuildTime.Text = $"{bt.Year}-{m}-{d} {h}:{min}:{s}";
                }
                else
                {
                    AboutBuildTime.Text = "获取失败";
                }
            }
            catch (Exception ex)
            {
                AboutBuildTime.Text = "获取失败";
                System.Diagnostics.Debug.WriteLine($"获取构建时间失败: {ex.Message}");
            }

            AboutSystemVersion.Text = $"{OSVersion.GetOperatingSystem()} {OSVersion.GetOSVersion().Version}";

            var _t_touch = new Thread(() =>
            {
                try
                {
                    var support = TouchTabletDetectHelper.IsTouchEnabled();
                    var touchcount = TouchTabletDetectHelper.GetTouchTabletDevices().Count;

                    Dispatcher.BeginInvoke(() =>
                    {
                        if (support)
                        {
                            if (touchcount > 0)
                            {
                                AboutTouchTabletText.Text = $"{touchcount}个设备，支持触摸设备";
                            }
                            else
                            {
                                AboutTouchTabletText.Text = "支持触摸设备";
                            }
                        }
                        else
                        {
                            AboutTouchTabletText.Text = "无触摸支持";
                        }
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(() =>
                            AboutTouchTabletText.Text = "检测失败");
                    System.Diagnostics.Debug.WriteLine($"检测触摸设备失败: {ex.Message}");
                }
            });
            _t_touch.Start();

            CheckUpdateStatus();
        }

        private void CheckUpdateStatus()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    var field = typeof(MainWindow).GetField("AvailableLatestVersion", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var availableVersion = field.GetValue(mainWindow) as string;
                        if (!string.IsNullOrEmpty(availableVersion))
                        {
                            UpdateAvailableIcon.Visibility = Visibility.Visible;
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        public static class TouchTabletDetectHelper
        {
            [DllImport("user32.dll")]
            public static extern int GetSystemMetrics(int nIndex);

            public static bool IsTouchEnabled()
            {
                const int MAXTOUCHES_INDEX = 95;
                int maxTouches = GetSystemMetrics(MAXTOUCHES_INDEX);

                return maxTouches > 0;
            }

            public class USBDeviceInfo
            {
                public USBDeviceInfo(string deviceID, string pnpDeviceID, string description)
                {
                    DeviceID = deviceID;
                    PnpDeviceID = pnpDeviceID;
                    Description = description;
                }
                public string DeviceID { get; private set; }
                public string PnpDeviceID { get; private set; }
                public string Description { get; private set; }
            }

            public static List<USBDeviceInfo> GetTouchTabletDevices()
            {
                List<USBDeviceInfo> devices = new List<USBDeviceInfo>();

                try
                {
                    ManagementObjectCollection collection;
                    using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_PnPEntity"))
                        collection = searcher.Get();

                    foreach (var device in collection)
                    {
                        try
                        {
                            var name = device.GetPropertyValue("Name")?.ToString() ?? "";
                            var description = device.GetPropertyValue("Description")?.ToString() ?? "";

                            if (string.IsNullOrEmpty(name)) continue;

                            var nameLower = name.ToLower();
                            var descLower = description.ToLower();

                            if (nameLower.Contains("pentablet") ||
                                nameLower.Contains("tablet") ||
                                nameLower.Contains("touch") ||
                                nameLower.Contains("digitizer") ||
                                descLower.Contains("touch") ||
                                descLower.Contains("digitizer"))
                            {
                                devices.Add(new USBDeviceInfo(
                                                device.GetPropertyValue("DeviceID")?.ToString() ?? "",
                                                device.GetPropertyValue("PNPDeviceID")?.ToString() ?? "",
                                                description
                                            ));
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    collection.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"获取触摸设备列表失败: {ex.Message}");
                }

                return devices;
            }
        }

        public static class FileBuildTimeHelper
        {
            public struct _IMAGE_FILE_HEADER
            {
                public ushort Machine;
                public ushort NumberOfSections;
                public uint TimeDateStamp;
                public uint PointerToSymbolTable;
                public uint NumberOfSymbols;
                public ushort SizeOfOptionalHeader;
                public ushort Characteristics;
            };

            public static DateTimeOffset? GetBuildDateTime(Assembly assembly)
            {
                try
                {
                    var path = assembly.Location;
                    if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    {
                        return null;
                    }

                    using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        var peHeader = new byte[4];
                        fileStream.Position = 0x3C;
                        fileStream.Read(peHeader, 0, 4);
                        var peHeaderOffset = BitConverter.ToUInt32(peHeader, 0);

                        fileStream.Position = peHeaderOffset;
                        var signature = new byte[4];
                        fileStream.Read(signature, 0, 4);

                        if (signature[0] != 0x50 || signature[1] != 0x45 || signature[2] != 0x00 || signature[3] != 0x00)
                        {
                            return null;
                        }

                        var fileHeader = new byte[Marshal.SizeOf(typeof(_IMAGE_FILE_HEADER))];
                        fileStream.Read(fileHeader, 0, fileHeader.Length);

                        var pinnedBuffer = GCHandle.Alloc(fileHeader, GCHandleType.Pinned);
                        try
                        {
                            var coffHeader = (_IMAGE_FILE_HEADER)Marshal.PtrToStructure(pinnedBuffer.AddrOfPinnedObject(), typeof(_IMAGE_FILE_HEADER));
                            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                            var buildTime = epoch.AddSeconds(coffHeader.TimeDateStamp);
                            return new DateTimeOffset(buildTime.ToLocalTime());
                        }
                        finally
                        {
                            pinnedBuffer.Free();
                        }
                    }
                }
                catch
                {
                    return null;
                }
            }
        }

        public event EventHandler<RoutedEventArgs> IsTopBarNeedShadowEffect;
        public event EventHandler<RoutedEventArgs> IsTopBarNeedNoShadowEffect;

        private void ScrollViewerEx_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = (ScrollViewer)sender;
            if (scrollViewer.VerticalOffset >= 10)
            {
                IsTopBarNeedShadowEffect?.Invoke(this, new RoutedEventArgs());
            }
            else
            {
                IsTopBarNeedNoShadowEffect?.Invoke(this, new RoutedEventArgs());
            }
        }

        private void ScrollBar_Scroll(object sender, RoutedEventArgs e)
        {
            var scrollbar = (ScrollBar)sender;
            var scrollviewer = scrollbar.FindAscendant<ScrollViewer>();
            if (scrollviewer != null) scrollviewer.ScrollToVerticalOffset(scrollbar.Track.Value);
        }

        private void ScrollBarTrack_MouseEnter(object sender, MouseEventArgs e)
        {
            var border = (Border)sender;
            if (border.Child is Track track)
            {
                track.Width = 16;
                track.Margin = new Thickness(0, 0, -2, 0);
                var scrollbar = track.FindAscendant<ScrollBar>();
                if (scrollbar != null) scrollbar.Width = 16;
                var grid = track.FindAscendant<Grid>();
                if (grid.FindDescendantByName("ScrollBarBorderTrackBackground") is Border backgroundBorder)
                {
                    backgroundBorder.Width = 8;
                    backgroundBorder.CornerRadius = new CornerRadius(4);
                    backgroundBorder.Opacity = 1;
                }
                var thumb = track.Thumb.Template.FindName("ScrollbarThumbEx", track.Thumb);
                if (thumb != null)
                {
                    var _thumb = thumb as Border;
                    _thumb.CornerRadius = new CornerRadius(4);
                    _thumb.Width = 8;
                    _thumb.Margin = new Thickness(-0.75, 0, 1, 0);
                    _thumb.Background = new SolidColorBrush(Color.FromRgb(138, 138, 138));
                }
            }
        }

        private void ScrollBarTrack_MouseLeave(object sender, MouseEventArgs e)
        {
            var border = (Border)sender;
            border.Background = new SolidColorBrush(Colors.Transparent);
            border.CornerRadius = new CornerRadius(0);
            if (border.Child is Track track)
            {
                track.Width = 6;
                track.Margin = new Thickness(0, 0, 0, 0);
                var scrollbar = track.FindAscendant<ScrollBar>();
                if (scrollbar != null) scrollbar.Width = 6;
                var grid = track.FindAscendant<Grid>();
                if (grid.FindDescendantByName("ScrollBarBorderTrackBackground") is Border backgroundBorder)
                {
                    backgroundBorder.Width = 3;
                    backgroundBorder.CornerRadius = new CornerRadius(1.5);
                    backgroundBorder.Opacity = 0;
                }
                var thumb = track.Thumb.Template.FindName("ScrollbarThumbEx", track.Thumb);
                if (thumb != null)
                {
                    var _thumb = thumb as Border;
                    _thumb.CornerRadius = new CornerRadius(1.5);
                    _thumb.Width = 3;
                    _thumb.Margin = new Thickness(0);
                    _thumb.Background = new SolidColorBrush(Color.FromRgb(195, 195, 195));
                }
            }
        }

        private void ScrollbarThumb_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var thumb = (Thumb)sender;
            var border = thumb.Template.FindName("ScrollbarThumbEx", thumb);
            ((Border)border).Background = new SolidColorBrush(Color.FromRgb(95, 95, 95));
        }

        private void ScrollbarThumb_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var thumb = (Thumb)sender;
            var border = thumb.Template.FindName("ScrollbarThumbEx", thumb);
            ((Border)border).Background = new SolidColorBrush(Color.FromRgb(138, 138, 138));
        }


        private void LinkOfficialWebsite_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://inkcanvasforclass.github.io",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开官方网站失败: {ex.Message}");
            }
        }

        private void LinkGithubRepo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/InkCanvasForClass/community",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开GitHub仓库失败: {ex.Message}");
            }
        }

        private void LinkContributors_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/InkCanvasForClass/community#贡献者",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开贡献者名单失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用主题
        /// </summary>
        public void ApplyTheme()
        {
            try
            {
                ThemeHelper.ApplyThemeToControl(this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AboutPanel 应用主题时出错: {ex.Message}");
            }
        }
    }
}
