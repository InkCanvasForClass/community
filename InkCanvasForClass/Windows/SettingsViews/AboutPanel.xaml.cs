using OSVersionExtension;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using iNKORE.UI.WPF.Helpers;
using static Ink_Canvas.Windows.SettingsWindow;

namespace Ink_Canvas.Windows.SettingsViews {
    /// <summary>
    /// AboutPanel.xaml 的交互逻辑
    /// </summary>
    public partial class AboutPanel : UserControl {
        public AboutPanel() {
            InitializeComponent();

            // 关于页面图片横幅
            if (File.Exists(App.RootPath + "icc-about-illustrations.png")) {
                try {
                    CopyrightBannerImage.Visibility = Visibility.Visible;
                    CopyrightBannerImage.Source =
                        new BitmapImage(new Uri($"file://{App.RootPath + "icc-about-illustrations.png"}"));
                }
                catch { }
            } else {
                CopyrightBannerImage.Visibility = Visibility.Collapsed;
            }

            // 关于页面构建时间
            var buildTime = FileBuildTimeHelper.GetBuildDateTime(System.Reflection.Assembly.GetExecutingAssembly());
            if (buildTime != null) {
                var bt = ((DateTimeOffset)buildTime).LocalDateTime;
                var m = bt.Month.ToString().PadLeft(2, '0');
                var d = bt.Day.ToString().PadLeft(2, '0');
                var h = bt.Hour.ToString().PadLeft(2, '0');
                var min = bt.Minute.ToString().PadLeft(2, '0');
                var s = bt.Second.ToString().PadLeft(2, '0');
                AboutBuildTime.Text =
                    $"build-{bt.Year}-{m}-{d}-{h}:{min}:{s}";
            }

            // 关于页面系统版本
            AboutSystemVersion.Text = $"{OSVersion.GetOperatingSystem()} {OSVersion.GetOSVersion().Version}";

            // 关于页面触摸设备
            var _t_touch = new Thread(() => {
                var touchcount = TouchTabletDetectHelper.GetTouchTabletDevices().Count;
                var support = TouchTabletDetectHelper.IsTouchEnabled();
                Dispatcher.BeginInvoke(() =>
                    AboutTouchTabletText.Text = $"{touchcount}个设备，{(support ? "支持触摸设备" : "无触摸支持")}");
            });
            _t_touch.Start();
        }

        public static class TouchTabletDetectHelper {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
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
                    this.DeviceID = deviceID;
                    this.PnpDeviceID = pnpDeviceID;
                    this.Description = description;
                }
                public string DeviceID { get; private set; }
                public string PnpDeviceID { get; private set; }
                public string Description { get; private set; }
            }

            public static List<USBDeviceInfo> GetTouchTabletDevices()
            {
                List<USBDeviceInfo> devices = new List<USBDeviceInfo>();

                ManagementObjectCollection collection;
                using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_PnPEntity"))
                    collection = searcher.Get();      

                foreach (var device in collection) {
                    var name = new StringBuilder((string)device.GetPropertyValue("Name")).ToString();
                    if (!name.Contains("Pentablet")) continue;
                    devices.Add(new USBDeviceInfo(
                        (string)device.GetPropertyValue("DeviceID"),
                        (string)device.GetPropertyValue("PNPDeviceID"),
                        (string)device.GetPropertyValue("Description")
                    ));
                }

                collection.Dispose();
                return devices;
            }
        }

        public static class FileBuildTimeHelper {
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
                var path = assembly.Location;
                if (File.Exists(path))
                {
                    var buffer = new byte[Math.Max(Marshal.SizeOf(typeof(_IMAGE_FILE_HEADER)), 4)];
                    using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        fileStream.Position = 0x3C;
                        fileStream.Read(buffer, 0, 4);
                        fileStream.Position = BitConverter.ToUInt32(buffer, 0); // COFF header offset
                        fileStream.Read(buffer, 0, 4); // "PE\0\0"
                        fileStream.Read(buffer, 0, buffer.Length);
                    }
                    var pinnedBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                    try
                    {
                        var coffHeader = (_IMAGE_FILE_HEADER)Marshal.PtrToStructure(pinnedBuffer.AddrOfPinnedObject(), typeof(_IMAGE_FILE_HEADER));
                        return DateTimeOffset.FromUnixTimeSeconds(coffHeader.TimeDateStamp);
                    }
                    finally
                    {
                        pinnedBuffer.Free();
                    }
                }
                else 
                {
                    return null;
                }
            }
        }

        public event EventHandler<RoutedEventArgs> IsTopBarNeedShadowEffect; 
        public event EventHandler<RoutedEventArgs> IsTopBarNeedNoShadowEffect;

        private void ScrollViewerEx_ScrollChanged(object sender, ScrollChangedEventArgs e) {
            var scrollViewer = (ScrollViewer)sender;
            if (scrollViewer.VerticalOffset >= 10) {
                IsTopBarNeedShadowEffect?.Invoke(this, new RoutedEventArgs());
            } else {
                IsTopBarNeedNoShadowEffect?.Invoke(this, new RoutedEventArgs());
            }
        }

        private void ScrollBar_Scroll(object sender, RoutedEventArgs e) {
            var scrollbar = (ScrollBar)sender;
            var scrollviewer = scrollbar.FindAscendant<ScrollViewer>();
            if (scrollviewer != null) scrollviewer.ScrollToVerticalOffset(scrollbar.Track.Value);
        }

        private void ScrollBarTrack_MouseEnter(object sender, MouseEventArgs e) {
            var border = (Border)sender;
            if (border.Child is Track track) {
                track.Width = 16;
                track.Margin = new Thickness(0, 0, -2, 0);
                var scrollbar = track.FindAscendant<ScrollBar>();
                if (scrollbar != null) scrollbar.Width = 16;
                var grid = track.FindAscendant<Grid>();
                if (grid.FindDescendantByName("ScrollBarBorderTrackBackground") is Border backgroundBorder) {
                    backgroundBorder.Width = 8;
                    backgroundBorder.CornerRadius = new CornerRadius(4);
                    backgroundBorder.Opacity = 1;
                }
                var thumb = track.Thumb.Template.FindName("ScrollbarThumbEx", track.Thumb) ;
                if (thumb != null) {
                    var _thumb = thumb as Border;
                    _thumb.CornerRadius = new CornerRadius(4);
                    _thumb.Width = 8;
                    _thumb.Margin = new Thickness(-0.75, 0, 1, 0);
                    _thumb.Background = new SolidColorBrush(Color.FromRgb(138, 138, 138));
                }
            }
        }

        private void ScrollBarTrack_MouseLeave(object sender, MouseEventArgs e) {
            var border = (Border)sender;
            border.Background = new SolidColorBrush(Colors.Transparent);
            border.CornerRadius = new CornerRadius(0);
            if (border.Child is Track track) {
                track.Width = 6;
                track.Margin = new Thickness(0, 0, 0, 0);
                var scrollbar = track.FindAscendant<ScrollBar>();
                if (scrollbar != null) scrollbar.Width = 6;
                var grid = track.FindAscendant<Grid>();
                if (grid.FindDescendantByName("ScrollBarBorderTrackBackground") is Border backgroundBorder) {
                    backgroundBorder.Width = 3;
                    backgroundBorder.CornerRadius = new CornerRadius(1.5);
                    backgroundBorder.Opacity = 0;
                }
                var thumb = track.Thumb.Template.FindName("ScrollbarThumbEx", track.Thumb) ;
                if (thumb != null) {
                    var _thumb = thumb as Border;
                    _thumb.CornerRadius = new CornerRadius(1.5);
                    _thumb.Width = 3;
                    _thumb.Margin = new Thickness(0);
                    _thumb.Background = new SolidColorBrush(Color.FromRgb(195, 195, 195));
                }
            }
        }

        private void ScrollbarThumb_MouseDown(object sender, MouseButtonEventArgs e) {
            var thumb = (Thumb)sender;
            var border = thumb.Template.FindName("ScrollbarThumbEx",thumb);
            ((Border)border).Background = new SolidColorBrush(Color.FromRgb(95, 95, 95));
        }

        private void ScrollbarThumb_MouseUp(object sender, MouseButtonEventArgs e) {
            var thumb = (Thumb)sender;
            var border = thumb.Template.FindName("ScrollbarThumbEx",thumb);
            ((Border)border).Background = new SolidColorBrush(Color.FromRgb(138, 138, 138));
        }
    }
}
