using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.VisualStyles;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Hardcodet.Wpf.TaskbarNotification;

namespace Ink_Canvas
{
    public partial class App : Application {

        private void SysTrayMenu_Opened(object sender, RoutedEventArgs e) {
            var s = (ContextMenu)sender;
            var FoldFloatingBarTrayIconMenuItemIconEyeOff =
                (Image)((Grid)((MenuItem)s.Items[s.Items.Count-5]).Icon).Children[0];
            var FoldFloatingBarTrayIconMenuItemIconEyeOn =
                (Image)((Grid)((MenuItem)s.Items[s.Items.Count - 5]).Icon).Children[1];
            var FoldFloatingBarTrayIconMenuItemHeaderText =
                (TextBlock)((SimpleStackPanel)((MenuItem)s.Items[s.Items.Count - 5]).Header).Children[0];
            var ResetFloatingBarPositionTrayIconMenuItem = (MenuItem)s.Items[s.Items.Count - 4];
            var HideICCMainWindowTrayIconMenuItem = (MenuItem)s.Items[s.Items.Count - 9];
            var mainWin = (MainWindow)Application.Current.MainWindow;
            if (mainWin.IsLoaded) {
                // 判斷是否在收納模式中
                if (mainWin.isFloatingBarFolded) {
                    FoldFloatingBarTrayIconMenuItemIconEyeOff.Visibility = Visibility.Hidden;
                    FoldFloatingBarTrayIconMenuItemIconEyeOn.Visibility = Visibility.Visible;
                    FoldFloatingBarTrayIconMenuItemHeaderText.Text = "退出收纳模式";
                    if (!HideICCMainWindowTrayIconMenuItem.IsChecked) {
                        ResetFloatingBarPositionTrayIconMenuItem.IsEnabled = false;
                        ResetFloatingBarPositionTrayIconMenuItem.Opacity = 0.5;
                    }
                } else {
                    FoldFloatingBarTrayIconMenuItemIconEyeOff.Visibility = Visibility.Visible;
                    FoldFloatingBarTrayIconMenuItemIconEyeOn.Visibility = Visibility.Hidden;
                    FoldFloatingBarTrayIconMenuItemHeaderText.Text = "切换为收纳模式";
                    if (!HideICCMainWindowTrayIconMenuItem.IsChecked) {
                        ResetFloatingBarPositionTrayIconMenuItem.IsEnabled = true;
                        ResetFloatingBarPositionTrayIconMenuItem.Opacity = 1;
                    }
                    
                }
            }
        }

        private void CloseAppTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e) {
            var mainWin = (MainWindow)Application.Current.MainWindow;
            if (mainWin.IsLoaded) mainWin.BtnExit_Click(null,null);
        }

        private void RestartAppTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e) {
            var mainWin = (MainWindow)Application.Current.MainWindow;
            if (mainWin.IsLoaded) mainWin.BtnRestart_Click(null,null);
        }

        private void ForceFullScreenTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e) {
            var mainWin = (MainWindow)Application.Current.MainWindow;
            if (mainWin.IsLoaded) {
                Ink_Canvas.MainWindow.MoveWindow(new WindowInteropHelper(mainWin).Handle, 0, 0,
                    System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width, System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height, true);
                Ink_Canvas.MainWindow.ShowNewMessage($"已强制全屏化：{System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width}x{System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height}（缩放比例为{System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width / SystemParameters.PrimaryScreenWidth}x{System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height / SystemParameters.PrimaryScreenHeight}）");
            }
        }

        private void FoldFloatingBarTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e)
        {
            var mainWin = (MainWindow)Application.Current.MainWindow;
            if (mainWin.IsLoaded)
                if (mainWin.isFloatingBarFolded) mainWin.UnFoldFloatingBar_MouseUp(new object(),null); 
                    else mainWin.FoldFloatingBar_MouseUp(new object(),null);
        }

        private void ResetFloatingBarPositionTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e)
        {
            var mainWin = (MainWindow)Application.Current.MainWindow;
            if (mainWin.IsLoaded) {
                var isInPPTPresentationMode = false;
                Dispatcher.Invoke(() => {
                    isInPPTPresentationMode = mainWin.BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible;
                });
                if (!mainWin.isFloatingBarFolded) {
                    if (!isInPPTPresentationMode) mainWin.PureViewboxFloatingBarMarginAnimationInDesktopMode();
                    else mainWin.PureViewboxFloatingBarMarginAnimationInPPTMode();
                }
            }
        }

        private void HideICCMainWindowTrayIconMenuItem_Checked(object sender, RoutedEventArgs e) {
            var mi = (MenuItem)sender;
            var mainWin = (MainWindow)Application.Current.MainWindow;
            if (mainWin.IsLoaded) {
                mainWin.Hide();
                var s = ((TaskbarIcon)Application.Current.Resources["TaskbarTrayIcon"]).ContextMenu;
                var ResetFloatingBarPositionTrayIconMenuItem = (MenuItem)s.Items[s.Items.Count - 4];
                var FoldFloatingBarTrayIconMenuItem = (MenuItem)s.Items[s.Items.Count - 5];
                var ForceFullScreenTrayIconMenuItem = (MenuItem)s.Items[s.Items.Count - 6];
                ResetFloatingBarPositionTrayIconMenuItem.IsEnabled = false;
                FoldFloatingBarTrayIconMenuItem.IsEnabled = false;
                ForceFullScreenTrayIconMenuItem.IsEnabled = false;
                ResetFloatingBarPositionTrayIconMenuItem.Opacity = 0.5;
                FoldFloatingBarTrayIconMenuItem.Opacity = 0.5;
                ForceFullScreenTrayIconMenuItem.Opacity = 0.5;
            } else {
                mi.IsChecked = false;
            }
            
        }

        private void HideICCMainWindowTrayIconMenuItem_UnChecked(object sender, RoutedEventArgs e) {
            var mi = (MenuItem)sender;
            var mainWin = (MainWindow)Application.Current.MainWindow;
            if (mainWin.IsLoaded) {
                mainWin.Show();
                var s = ((TaskbarIcon)Application.Current.Resources["TaskbarTrayIcon"]).ContextMenu;
                var ResetFloatingBarPositionTrayIconMenuItem = (MenuItem)s.Items[s.Items.Count - 4];
                var FoldFloatingBarTrayIconMenuItem = (MenuItem)s.Items[s.Items.Count - 5];
                var ForceFullScreenTrayIconMenuItem = (MenuItem)s.Items[s.Items.Count - 6];
                ResetFloatingBarPositionTrayIconMenuItem.IsEnabled = true;
                FoldFloatingBarTrayIconMenuItem.IsEnabled = true;
                ForceFullScreenTrayIconMenuItem.IsEnabled = true;
                ResetFloatingBarPositionTrayIconMenuItem.Opacity = 1;
                FoldFloatingBarTrayIconMenuItem.Opacity = 1;
                ForceFullScreenTrayIconMenuItem.Opacity = 1;
            } else {
                mi.IsChecked = false;
            }
        }

    }
}
