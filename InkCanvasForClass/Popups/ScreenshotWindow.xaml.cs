using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Xml.Linq;
using OSVersionExtension;
using Vanara.PInvoke;
using Color = System.Windows.Media.Color;
using Shell32;
using static Ink_Canvas.MainWindow;
using OperatingSystem = OSVersionExtension.OperatingSystem;

namespace Ink_Canvas.Popups
{
    /// <summary>
    /// ScreenshotWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ScreenshotWindow : Window {

        private MainWindow mainWindow;
        private Settings settings;

        public ScreenshotWindow(MainWindow mainWin, Settings s) {
            InitializeComponent();

            mainWindow = mainWin;
            settings = s;

            iconList = new Border[] {
                FullScreenIcon,
                WindowIcon,
                SelectionIcon,
                DesktopIcon
            };

            WindowIcon.IsHitTestVisible = OSVersion.GetOperatingSystem() >= OperatingSystem.Windows10;
            WindowIcon.Opacity = OSVersion.GetOperatingSystem() >= OperatingSystem.Windows10 ? 1 : 0.5;

            
            foreach (var b in iconList) {
                b.MouseLeave += IconMouseLeave;
                b.MouseUp += IconMouseUp;
                b.MouseDown += IconMouseDown;
                b.Background = new SolidColorBrush(Colors.Transparent);
            }

            ReArrangeWindowPosition();
            mainWin.Hide();

            if (DwmCompositionHelper.DwmIsCompositionEnabled()) {
                AllowsTransparency = false;
                Background = new SolidColorBrush(Colors.Transparent);
                WindowChrome.SetWindowChrome(this, new WindowChrome() {
                    GlassFrameThickness = new Thickness(-1),
                    CaptionHeight = 0,
                    CornerRadius = new CornerRadius(0),
                    ResizeBorderThickness = new Thickness(0),
                });
            } else {
                AllowsTransparency = true;
                Background = new SolidColorBrush(Color.FromArgb(1,0,0,0));
            }

            ToggleSwitchCopyToClipBoard.IsChecked = settings.Snapshot.CopyScreenshotToClipboard;
            ToggleSwitchAttachInk.IsChecked = settings.Snapshot.AttachInkWhenScreenshot;
            ToggleSwitchCopyToClipBoard.Checked += ToggleSwitchCopyToClipBoard_CheckChanged;
            ToggleSwitchCopyToClipBoard.Unchecked += ToggleSwitchCopyToClipBoard_CheckChanged;
            ToggleSwitchAttachInk.Checked += ToggleSwitchAttachInk_CheckChanged;
            ToggleSwitchAttachInk.Unchecked += ToggleSwitchAttachInk_CheckChanged;

            WindowsItemsControl.ItemsSource = _winInfos;
            WindowScreenshotOverlay.Visibility = Visibility.Collapsed;
            WindowsSnapshotLoadingOverlay.Visibility = Visibility.Collapsed;

            EscBorder.MouseDown += EscBorder_MouseDown;
            EscBorder.MouseUp += EscBorder_MouseUp;
            EscBorder.MouseLeave += EscBorder_MouseLeave;

            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            NeedAdminTextPanel.Visibility = principal.IsInRole(WindowsBuiltInRole.Administrator) ? Visibility.Collapsed : Visibility.Visible;
            if (principal.IsInRole(WindowsBuiltInRole.Administrator)) {
                WindowsItemsScrollViewer.Margin = new Thickness(36, 148, 36, 0);
            } else {
                WindowsItemsScrollViewer.Margin = new Thickness(36, 178, 36, 0);
            }
        }

        private bool isEscBorderDown = false;

        private void EscBorder_MouseLeave(object sender, MouseEventArgs e) {
            if (!isEscBorderDown) return;
            isEscBorderDown = false;
            EscBorder.Background = new SolidColorBrush(Colors.Transparent);
        }

        private void EscBorder_MouseUp(object sender, MouseButtonEventArgs e) {
            if (!isEscBorderDown) return;
            if (isWindowsSnapshotLoaded == false) return;
            EscBorder_MouseLeave(null, null);
            ScreenshotPanel.Visibility = Visibility.Visible;
            WindowScreenshotOverlay.Visibility = Visibility.Collapsed;
            _winInfos.Clear();
            isWindowsSnapshotLoaded = null;
        }

        private void EscBorder_MouseDown(object sender, MouseButtonEventArgs e) {
            if (isEscBorderDown) return;
            isEscBorderDown = true;
            EscBorder.Background = new SolidColorBrush(Color.FromRgb(39, 39, 42));
        }

        private ObservableCollection<WinInfo> _winInfos = new ObservableCollection<WinInfo>();

        private bool AllOneColor(Bitmap bmp)
        {
            // Lock the bitmap's bits.  
            System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.ReadWrite, bmp.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int bytes = bmpData.Stride * bmp.Height;
            byte[] rgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

            bool AllOneColor = true;
            for (int index = 0; index < rgbValues.Length; index++) {
                //compare the current A or R or G or B with the A or R or G or B at position 0,0.
                if (rgbValues[index] != rgbValues[index % 4]) {
                    AllOneColor= false;
                    break;
                }
            }
            // Unlock the bits.
            bmp.UnlockBits(bmpData);
            return AllOneColor;
        }

        private async Task<bool> AllOneColorAsync(Bitmap bmp) {
            var result = await Task.Run(() => AllOneColor(bmp));
            return result;
        }

        public BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }

        private static ImageSource IconToImageSource(Icon icon)
        {
            ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            return imageSource;
        }

        private class WinInfo {
            public string Title { get; set; }
            public BitmapImage Snapshot { get; set; }
            public ImageSource Icon { get; set; }
            public HWND Handle { get; set; }
            public Bitmap OriginBitmap { get; set; }
            public double Width { get; set; }
            public double TextBlockWidth { get; set; }
            public bool IsAllOneColor { get; set; }
            public bool IsDisplayFailedBorder { get; set; }
            public Visibility ShouldDisplayFailedBorder {
                get => IsDisplayFailedBorder ? Visibility.Visible : Visibility.Collapsed;
            }
            public bool IsHidden { get; set; }
            public Visibility Visibility {
                get => IsHidden ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private Border lastDownIcon;

        private void ToggleSwitchCopyToClipBoard_CheckChanged(object sender, RoutedEventArgs e) {
            if (!mainWindow.isLoaded) return;
            mainWindow.ToggleSwitchCopyScreenshotToClipboard.IsOn = ToggleSwitchCopyToClipBoard.IsChecked ?? true;
        }

        private void ToggleSwitchAttachInk_CheckChanged(object sender, RoutedEventArgs e) {
            if (!mainWindow.isLoaded) return;
            mainWindow.ToggleSwitchAttachInkWhenScreenshot.IsOn = ToggleSwitchAttachInk.IsChecked ?? true;
        }

        private void ReArrangeWindowPosition() {
            var workAreaWidth = SystemParameters.WorkArea.Width;
            var workAreaHeight = SystemParameters.WorkArea.Height;
            var toolbarHeight = SystemParameters.PrimaryScreenHeight - SystemParameters.FullPrimaryScreenHeight -
                                SystemParameters.WindowCaptionHeight;
            Left = (workAreaWidth - Width) / 2;
            Top = workAreaHeight - Height - toolbarHeight - 64;
        }

        private bool isCaptureButtonDown = false;

        private async void CaptureFullScreen() {
            LoadingOverlay.Visibility = Visibility.Visible;
            MainFuncPanel.Effect = new BlurEffect() {
                KernelType = KernelType.Gaussian,
                Radius = 24,
                RenderingBias = RenderingBias.Performance,
            };
            try {
                var bm = await mainWindow.FullscreenSnapshot(new MainWindow.SnapshotConfig() {
                    BitmapSavePath =
                        new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
                    ExcludedHwnds = new HWND[] {
                        new HWND(new WindowInteropHelper(this).Handle)
                    },
                    IsCopyToClipboard = settings.Snapshot.CopyScreenshotToClipboard,
                    IsSaveToLocal = true,
                    OutputMIMEType = MainWindow.OutputImageMIMEFormat.Png,
                });
                bm.Dispose();
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MainFuncPanel.Effect = null;
                mainWindow.ShowNewToast("已保存截图到桌面！", MW_Toast.ToastType.Success, 3000);
                await Task.Delay(1);
                Close();
            }
            catch (Exception e) {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MainFuncPanel.Effect = null;
                mainWindow.ShowNewToast($"截图失败！{e.Message}", MW_Toast.ToastType.Error, 3000);
                await Task.Delay(1);
                Close();
            }
        }

        private void IconMouseLeave(object sender, MouseEventArgs e) {
            if (lastDownIcon == null) return;
            lastDownIcon = null;
            var b = (Border)sender;
            b.Background = new SolidColorBrush(Colors.Transparent);
        }

        private void IconMouseDown(object sender, MouseButtonEventArgs e) {
            if (lastDownIcon != null) return;
            lastDownIcon = (Border)sender;
            var b = (Border)sender;
            b.Background = new SolidColorBrush(Color.FromArgb(22, 24, 24, 27));
        }

        private bool? isWindowsSnapshotLoaded = false;


        private async void IconMouseUp(object sender, MouseButtonEventArgs e) {
            if (lastDownIcon == null) return;
            IconMouseLeave(sender, null);


            /*if (selectedMode == 1) {
                try {
                    MainWindow.WindowInformation[] windows = await mainWindow.GetAllWindowsAsync(new HWND[] {
                        new HWND(new WindowInteropHelper(this).Handle), new HWND(new WindowInteropHelper(mainWindow).Handle)
                    });
                    _screenshotGridWindow = new WindowScreenshotGridWindow(windows, mainWindow);
                    _screenshotGridWindow.Show();
                }
                catch (TaskCanceledException) {}
                catch (Exception ex) {}
            } else {
                try {
                    _screenshotGridWindow.Close();
                } catch (Exception ex) { }
                
            }*/

            if (Array.IndexOf(iconList, (Border)sender) == 0) {
                CaptureFullScreen();
            } else if (Array.IndexOf(iconList, (Border)sender) == 3) {
                Shell shellObject = new Shell();
                shellObject.ToggleDesktop();
            } else if (Array.IndexOf(iconList, ((Border)sender)) == 1) {
                isWindowsSnapshotLoaded = false;
                await Dispatcher.InvokeAsync(() => {
                    _winInfos.Clear();
                    ScreenshotPanel.Visibility = Visibility.Collapsed;
                    WindowScreenshotOverlay.Visibility = Visibility.Visible;
                    WindowsSnapshotLoadingOverlay.Visibility = Visibility.Visible;
                    WindowScreenshotWindowsGrid.Effect = new BlurEffect() {
                        KernelType = KernelType.Gaussian,
                        RenderingBias = RenderingBias.Performance,
                        Radius = 32,
                    };
                });
                var wins = await mainWindow.GetAllWindowsAsync(new HWND[] {
                    new HWND(new WindowInteropHelper(this).Handle)
                });
                foreach (var windowInformation in wins) {
                    var bitmapHeight = windowInformation.WindowBitmap.Height;
                    var w = windowInformation.WindowBitmap.Width * (226D / bitmapHeight);
                    var allonecolor = await AllOneColorAsync(windowInformation.WindowBitmap);
                    _winInfos.Add(new WinInfo() {
                        Title = windowInformation.Title,
                        Snapshot = BitmapToImageSource(windowInformation.WindowBitmap.Clone(windowInformation.ContentRect,windowInformation.WindowBitmap.PixelFormat)),
                        Handle = windowInformation.hwnd,
                        OriginBitmap = windowInformation.WindowBitmap,
                        Icon = windowInformation.AppIcon == null ? new BitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/classic-icons/program-icon.png")) : IconToImageSource(windowInformation.AppIcon),
                        Width = w,
                        TextBlockWidth = w - 48 - 8,
                        IsAllOneColor = allonecolor,
                        IsDisplayFailedBorder = allonecolor,
                        IsHidden = settings.Snapshot.OnlySnapshotMaximizeWindow,
                    });
                    if (Array.IndexOf(wins, windowInformation)>= wins.Length - 1) Dispatcher.InvokeAsync(() => {
                        WindowScreenshotWindowsGrid.Effect = null;
                        WindowsSnapshotLoadingOverlay.Visibility = Visibility.Collapsed;});
                }
                Dispatcher.InvokeAsync(() => {
                    WindowScreenshotWindowsGrid.Effect = null;
                    WindowsSnapshotLoadingOverlay.Visibility = Visibility.Collapsed;});
                isWindowsSnapshotLoaded = true;
            }
        }

        private Border[] iconList = new Border[] { };

        private void CloseButton_CloseWindow(object sender, MouseButtonEventArgs e) {
            Close();
        }

        protected override void OnClosed(EventArgs e) {
            mainWindow.Show();
            base.OnClosed(e);
        }

        private void ScreenshotWindow_OnKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                if (isWindowsSnapshotLoaded==false) return;
                ScreenshotPanel.Visibility = Visibility.Visible;
                WindowScreenshotOverlay.Visibility = Visibility.Collapsed;
                _winInfos.Clear();
                isWindowsSnapshotLoaded = null;
            }
        }

        public void WindowsItemsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            var scrollViewer = (ScrollViewer)sender;
            var sb = new Storyboard();
            var ofs = scrollViewer.VerticalOffset;
            var animation = new DoubleAnimation
            {
                From = ofs,
                To = ofs - e.Delta * 2.5,
                Duration = TimeSpan.FromMilliseconds(155)
            };
            animation.EasingFunction = new CubicEase() {
                EasingMode = EasingMode.EaseOut,
            };
            Storyboard.SetTargetProperty(animation, new PropertyPath(ColorPalette.ScrollViewerBehavior.VerticalOffsetProperty));
            Storyboard.SetTargetName(animation,"WindowsItemsScrollViewer");
            sb.Children.Add(animation);
            scrollViewer.ScrollToVerticalOffset(ofs);
            sb.Begin(scrollViewer);
        }
    }
}
