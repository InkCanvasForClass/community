using Ink_Canvas.Helpers;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using File = System.IO.File;
using System.Windows.Media;
using System.Windows.Ink;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using Hardcodet.Wpf.TaskbarNotification;
using OSVersionExtension;
using System.Windows.Media.Animation;
using System.Xml.Linq;
using iNKORE.UI.WPF.Modern.Media.Animation;
using System.Security.Principal;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Timers;
using ColorPicker.Models;
using Ink_Canvas.Popups;
using Ookii.Dialogs.Wpf;
using Microsoft.Office.Interop.PowerPoint;
using Application = System.Windows.Application;
using Point = System.Windows.Point;

namespace Ink_Canvas {
    public partial class MainWindow : PerformanceTransparentWin {
        #region Behavior

        private void ToggleSwitchIsAutoUpdate_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Startup.IsAutoUpdate = ToggleSwitchIsAutoUpdate.IsOn;
            ToggleSwitchIsAutoUpdateWithSilence.Visibility =
                ToggleSwitchIsAutoUpdate.IsOn ? Visibility.Visible : Visibility.Collapsed;
            SaveSettingsToFile();
        }

        private void ToggleSwitchIsAutoUpdateWithSilence_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Startup.IsAutoUpdateWithSilence = ToggleSwitchIsAutoUpdateWithSilence.IsOn;
            AutoUpdateTimePeriodBlock.Visibility =
                Settings.Startup.IsAutoUpdateWithSilence ? Visibility.Visible : Visibility.Collapsed;
            SaveSettingsToFile();
        }

        private void AutoUpdateWithSilenceStartTimeComboBox_SelectionChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Startup.AutoUpdateWithSilenceStartTime =
                (string)AutoUpdateWithSilenceStartTimeComboBox.SelectedItem;
            SaveSettingsToFile();
        }

        private void AutoUpdateWithSilenceEndTimeComboBox_SelectionChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Startup.AutoUpdateWithSilenceEndTime = (string)AutoUpdateWithSilenceEndTimeComboBox.SelectedItem;
            SaveSettingsToFile();
        }

        private void ToggleSwitchRunAtStartup_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            if (ToggleSwitchRunAtStartup.IsOn) {
                StartAutomaticallyDel("InkCanvasForClass");
                StartAutomaticallyCreate("InkCanvasForClass");
            } else {
                StartAutomaticallyDel("InkCanvasForClass");
            }
        }

        private void RunAsAdminButton_Click(object sender, RoutedEventArgs e) {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator)) {
                var file = new FileInfo(Assembly.GetExecutingAssembly().Location);
                var exe = Path.Combine(file.DirectoryName, file.Name.Replace(file.Extension, "") + ".exe");

                var proc = new Process
                {
                    StartInfo = {
                        FileName = exe,
                        Verb = "runas",
                        UseShellExecute = true,
                        Arguments = "-m"
                    }
                };
                proc.Start();

                CloseIsFromButton = true;
                Application.Current.Shutdown();
            }
        }

        private void RunAsUserButton_Click(object sender, RoutedEventArgs e)
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                Process.Start("explorer.exe", Assembly.GetEntryAssembly().Location);

                CloseIsFromButton = true;
                Application.Current.Shutdown();
            }
        }

        private void ToggleSwitchFoldAtStartup_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Startup.IsFoldAtStartup = ToggleSwitchFoldAtStartup.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchSupportPowerPoint_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;

            Settings.PowerPointSettings.PowerPointSupport = ToggleSwitchSupportPowerPoint.IsOn;
            SaveSettingsToFile();

            if (Settings.PowerPointSettings.PowerPointSupport)
                timerCheckPPT.Start();
            else
                timerCheckPPT.Stop();
        }

        private void ToggleSwitchShowCanvasAtNewSlideShow_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;

            Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow = ToggleSwitchShowCanvasAtNewSlideShow.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchRegistryShowSlideShowToolbar_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;

            Settings.PowerPointSettings.RegistryShowSlideShowToolbar = ToggleSwitchRegistryShowSlideShowToolbar.IsOn;
        }

        private void ToggleSwitchRegistryShowBlackScreenLastSlideShow_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.PowerPointSettings.RegistryShowBlackScreenLastSlideShow = ToggleSwitchRegistryShowBlackScreenLastSlideShow.IsOn;
        }

        #endregion

        #region Startup

        private void ToggleSwitchEnableNibMode_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            if (sender == ToggleSwitchEnableNibMode)
                BoardToggleSwitchEnableNibMode.IsOn = ToggleSwitchEnableNibMode.IsOn;
            else
                ToggleSwitchEnableNibMode.IsOn = BoardToggleSwitchEnableNibMode.IsOn;
            Settings.Startup.IsEnableNibMode = ToggleSwitchEnableNibMode.IsOn;

            if (Settings.Startup.IsEnableNibMode)
                BoundsWidth = Settings.Advanced.NibModeBoundsWidth;
            else
                BoundsWidth = Settings.Advanced.FingerModeBoundsWidth;
            SaveSettingsToFile();
        }

        private void ToggleSwitchEnableWindowChromeRendering_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Startup.EnableWindowChromeRendering = ToggleSwitchEnableWindowChromeRendering.IsOn;
            SaveSettingsToFile();
        }

        #endregion

        #region Appearance

        private void ToggleSwitchEnableDisPlayNibModeToggle_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Appearance.IsEnableDisPlayNibModeToggler = ToggleSwitchEnableDisPlayNibModeToggle.IsOn;
            SaveSettingsToFile();
            if (!ToggleSwitchEnableDisPlayNibModeToggle.IsOn) {
                NibModeSimpleStackPanel.Visibility = Visibility.Collapsed;
                BoardNibModeSimpleStackPanel.Visibility = Visibility.Collapsed;
            } else {
                NibModeSimpleStackPanel.Visibility = Visibility.Visible;
                BoardNibModeSimpleStackPanel.Visibility = Visibility.Visible;
            }
        }

        //private void ToggleSwitchIsColorfulViewboxFloatingBar_Toggled(object sender, RoutedEventArgs e) {
        //    if (!isLoaded) return;
        //    Settings.Appearance.IsColorfulViewboxFloatingBar = ToggleSwitchColorfulViewboxFloatingBar.IsOn;
        //    SaveSettingsToFile();
        //}

        private void ToggleSwitchEnableQuickPanel_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Appearance.IsShowQuickPanel = ToggleSwitchEnableQuickPanel.IsOn;
            SaveSettingsToFile();
        }

        private void ViewboxFloatingBarScaleTransformValueSlider_ValueChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Appearance.ViewboxFloatingBarScaleTransformValue =
                ViewboxFloatingBarScaleTransformValueSlider.Value;
            SaveSettingsToFile();
            var val = ViewboxFloatingBarScaleTransformValueSlider.Value;
            ViewboxFloatingBarScaleTransform.ScaleX =
                val > 0.5 && val < 1.25 ? val : val <= 0.5 ? 0.5 : val >= 1.25 ? 1.25 : 1;
            ViewboxFloatingBarScaleTransform.ScaleY =
                val > 0.5 && val < 1.25 ? val : val <= 0.5 ? 0.5 : val >= 1.25 ? 1.25 : 1;
            // auto align
            if (BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible)
                ViewboxFloatingBarMarginAnimation(60);
            else
                ViewboxFloatingBarMarginAnimation(100, true);
        }

        private void ViewboxFloatingBarOpacityValueSlider_ValueChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Appearance.ViewboxFloatingBarOpacityValue = ViewboxFloatingBarOpacityValueSlider.Value;
            SaveSettingsToFile();
            ViewboxFloatingBar.Opacity = Settings.Appearance.ViewboxFloatingBarOpacityValue;
        }

        private void ViewboxFloatingBarOpacityInPPTValueSlider_ValueChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Appearance.ViewboxFloatingBarOpacityInPPTValue = ViewboxFloatingBarOpacityInPPTValueSlider.Value;
            SaveSettingsToFile();
        }

        private void ToggleSwitchEnableTrayIcon_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Appearance.EnableTrayIcon = ToggleSwitchEnableTrayIcon.IsOn;
            ICCTrayIconExampleImage.Visibility = Settings.Appearance.EnableTrayIcon ? Visibility.Visible : Visibility.Collapsed;
            var _taskbar = (TaskbarIcon)Application.Current.Resources["TaskbarTrayIcon"];
            _taskbar.Visibility = ToggleSwitchEnableTrayIcon.IsOn? Visibility.Visible : Visibility.Collapsed;
            SaveSettingsToFile();
        }

        private void ComboBoxUnFoldBtnImg_SelectionChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Appearance.UnFoldButtonImageType = ComboBoxUnFoldBtnImg.SelectedIndex;
            SaveSettingsToFile();
            if (ComboBoxUnFoldBtnImg.SelectedIndex == 0) {
                RightUnFoldBtnImgChevron.Source =
                    new BitmapImage(new Uri("pack://application:,,,/Resources/new-icons/unfold-chevron.png"));
                RightUnFoldBtnImgChevron.Width = 14;
                RightUnFoldBtnImgChevron.Height = 14;
                RightUnFoldBtnImgChevron.RenderTransform = new RotateTransform(180);
                LeftUnFoldBtnImgChevron.Source =
                    new BitmapImage(new Uri("pack://application:,,,/Resources/new-icons/unfold-chevron.png"));
                LeftUnFoldBtnImgChevron.Width = 14;
                LeftUnFoldBtnImgChevron.Height = 14;
                LeftUnFoldBtnImgChevron.RenderTransform = null;
            } else if (ComboBoxUnFoldBtnImg.SelectedIndex == 1) {
                RightUnFoldBtnImgChevron.Source =
                    new BitmapImage(new Uri("pack://application:,,,/Resources/new-icons/pen-white.png"));
                RightUnFoldBtnImgChevron.Width = 18;
                RightUnFoldBtnImgChevron.Height = 18;
                RightUnFoldBtnImgChevron.RenderTransform = null;
                LeftUnFoldBtnImgChevron.Source =
                    new BitmapImage(new Uri("pack://application:,,,/Resources/new-icons/pen-white.png"));
                LeftUnFoldBtnImgChevron.Width = 18;
                LeftUnFoldBtnImgChevron.Height = 18;
                LeftUnFoldBtnImgChevron.RenderTransform = null;
            }
        }

        private void ComboBoxChickenSoupSource_SelectionChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Appearance.ChickenSoupSource = ComboBoxChickenSoupSource.SelectedIndex;
            SaveSettingsToFile();
            if (Settings.Appearance.ChickenSoupSource == 0) {
                int randChickenSoupIndex = new Random().Next(ChickenSoup.OSUPlayerYuLu.Length);
                BlackBoardWaterMark.Text = ChickenSoup.OSUPlayerYuLu[randChickenSoupIndex];
            } else if (Settings.Appearance.ChickenSoupSource == 1) {
                int randChickenSoupIndex = new Random().Next(ChickenSoup.MingYanJingJu.Length);
                BlackBoardWaterMark.Text = ChickenSoup.MingYanJingJu[randChickenSoupIndex];
            } else if (Settings.Appearance.ChickenSoupSource == 2) {
                int randChickenSoupIndex = new Random().Next(ChickenSoup.GaoKaoPhrases.Length);
                BlackBoardWaterMark.Text = ChickenSoup.GaoKaoPhrases[randChickenSoupIndex];
            }
        }

        private void ToggleSwitchEnableViewboxBlackBoardScaleTransform_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Appearance.EnableViewboxBlackBoardScaleTransform =
                ToggleSwitchEnableViewboxBlackBoardScaleTransform.IsOn;

            if (Settings.Appearance.EnableViewboxBlackBoardScaleTransform) // 画板 UI 缩放 80%
            {
                ViewboxBlackboardLeftSideScaleTransform.ScaleX = 0.8;
                ViewboxBlackboardLeftSideScaleTransform.ScaleY = 0.8;
                ViewboxBlackboardCenterSideScaleTransform.ScaleX = 0.8;
                ViewboxBlackboardCenterSideScaleTransform.ScaleY = 0.8;
                ViewboxBlackboardRightSideScaleTransform.ScaleX = 0.8;
                ViewboxBlackboardRightSideScaleTransform.ScaleY = 0.8;
            }
            else
            {
                ViewboxBlackboardLeftSideScaleTransform.ScaleX = 1;
                ViewboxBlackboardLeftSideScaleTransform.ScaleY = 1;
                ViewboxBlackboardCenterSideScaleTransform.ScaleX = 1;
                ViewboxBlackboardCenterSideScaleTransform.ScaleY = 1;
                ViewboxBlackboardRightSideScaleTransform.ScaleX = 1;
                ViewboxBlackboardRightSideScaleTransform.ScaleY = 1;
            }

            SaveSettingsToFile();
        }

        public void ComboBoxFloatingBarImg_SelectionChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Appearance.FloatingBarImg = ComboBoxFloatingBarImg.SelectedIndex;
            if (ComboBoxFloatingBarImg.SelectedIndex == 0) {
                FloatingbarHeadIconImg.Source =
                    new BitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/icc.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(0.5);
            } else if (ComboBoxFloatingBarImg.SelectedIndex == 1) {
                FloatingbarHeadIconImg.Source =
                    new BitmapImage(
                        new Uri("pack://application:,,,/Resources/Icons-png/icc-transparent-dark-small.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(1.2);
            } else if (ComboBoxFloatingBarImg.SelectedIndex == 2) {
                FloatingbarHeadIconImg.Source =
                    new BitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/kuandoujiyanhuaji.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(2, 2, 2, 1.5);
            } else if (ComboBoxFloatingBarImg.SelectedIndex == 3) {
                FloatingbarHeadIconImg.Source =
                    new BitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/kuanshounvhuaji.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(2, 2, 2, 1.5);
            } else if (ComboBoxFloatingBarImg.SelectedIndex == 4) {
                FloatingbarHeadIconImg.Source =
                    new BitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/kuanciya.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(2, 2, 2, 1.5);
            } else if (ComboBoxFloatingBarImg.SelectedIndex == 5) {
                FloatingbarHeadIconImg.Source =
                    new BitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/kuanneikuhuaji.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(2, 2, 2, 1.5);
            } else if (ComboBoxFloatingBarImg.SelectedIndex == 6) {
                FloatingbarHeadIconImg.Source =
                    new BitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/kuandogeyuanliangwo.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(2, 2, 2, 1.5);
            } else if (ComboBoxFloatingBarImg.SelectedIndex == 7) {
                FloatingbarHeadIconImg.Source =
                    new BitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/tiebahuaji.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(2, 2, 2, 1);
            }
            SaveSettingsToFile();
        }

        private void ToggleSwitchEnableTimeDisplayInWhiteboardMode_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Appearance.EnableTimeDisplayInWhiteboardMode = ToggleSwitchEnableTimeDisplayInWhiteboardMode.IsOn;
            if (currentMode == 1) {
                if (ToggleSwitchEnableTimeDisplayInWhiteboardMode.IsOn) {
                    WaterMarkTime.Visibility = Visibility.Visible;
                    WaterMarkDate.Visibility = Visibility.Visible;
                } else {
                    WaterMarkTime.Visibility = Visibility.Collapsed;
                    WaterMarkDate.Visibility = Visibility.Collapsed;
                }
            }

            SaveSettingsToFile();
        }

        private void ToggleSwitchEnableChickenSoupInWhiteboardMode_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Appearance.EnableChickenSoupInWhiteboardMode = ToggleSwitchEnableChickenSoupInWhiteboardMode.IsOn;
            if (currentMode == 1) {
                if (ToggleSwitchEnableTimeDisplayInWhiteboardMode.IsOn) {
                    BlackBoardWaterMark.Visibility = Visibility.Visible;
                } else {
                    BlackBoardWaterMark.Visibility = Visibility.Collapsed;
                }
            }

            SaveSettingsToFile();
        }

        //[Obsolete]
        //private void ToggleSwitchShowButtonPPTNavigation_OnToggled(object sender, RoutedEventArgs e) {
        //    if (!isLoaded) return;
        //    Settings.PowerPointSettings.IsShowPPTNavigation = ToggleSwitchShowButtonPPTNavigation.IsOn;
        //    var vis = Settings.PowerPointSettings.IsShowPPTNavigation ? Visibility.Visible : Visibility.Collapsed;
        //    PPTLBPageButton.Visibility = vis;
        //    PPTRBPageButton.Visibility = vis;
        //    PPTLSPageButton.Visibility = vis;
        //    PPTRSPageButton.Visibility = vis;
        //    SaveSettingsToFile();
        //}

        //[Obsolete]
        //private void ToggleSwitchShowBottomPPTNavigationPanel_OnToggled(object sender, RoutedEventArgs e) {
        //    if (!isLoaded) return;
        //    Settings.PowerPointSettings.IsShowBottomPPTNavigationPanel = ToggleSwitchShowBottomPPTNavigationPanel.IsOn;
        //    if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
        //        //BottomViewboxPPTSidesControl.Visibility = Settings.PowerPointSettings.IsShowBottomPPTNavigationPanel
        //        //    ? Visibility.Visible
        //        //    : Visibility.Collapsed;
        //    SaveSettingsToFile();
        //}

        //[Obsolete]
        //private void ToggleSwitchShowSidePPTNavigationPanel_OnToggled(object sender, RoutedEventArgs e) {
        //    if (!isLoaded) return;
        //    Settings.PowerPointSettings.IsShowSidePPTNavigationPanel = ToggleSwitchShowSidePPTNavigationPanel.IsOn;
        //    if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible) {
        //        LeftSidePanelForPPTNavigation.Visibility = Settings.PowerPointSettings.IsShowSidePPTNavigationPanel
        //            ? Visibility.Visible
        //            : Visibility.Collapsed;
        //        RightSidePanelForPPTNavigation.Visibility = Settings.PowerPointSettings.IsShowSidePPTNavigationPanel
        //            ? Visibility.Visible
        //            : Visibility.Collapsed;
        //    }

        //    SaveSettingsToFile();
        //}

        private void ToggleSwitchShowPPTButton_OnToggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.PowerPointSettings.ShowPPTButton = ToggleSwitchShowPPTButton.IsOn;
            SaveSettingsToFile();
            if (BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible) UpdatePPTBtnDisplaySettingsStatus();
            UpdatePPTBtnPreview();
        }

        private void ToggleSwitchEnablePPTButtonPageClickable_OnToggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.PowerPointSettings.EnablePPTButtonPageClickable = ToggleSwitchEnablePPTButtonPageClickable.IsOn;
            SaveSettingsToFile();
        }

        private void CheckboxEnableLBPPTButton_IsCheckChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            var str = Settings.PowerPointSettings.PPTButtonsDisplayOption.ToString();
            char[] c = str.ToCharArray();
            c[0] = (bool)((CheckBox)sender).IsChecked ? '2' : '1';
            Settings.PowerPointSettings.PPTButtonsDisplayOption = int.Parse(new string(c));
            SaveSettingsToFile();
            if (BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible) UpdatePPTBtnDisplaySettingsStatus();
            UpdatePPTBtnPreview();
        }

        private void CheckboxEnableRBPPTButton_IsCheckChanged(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var str = Settings.PowerPointSettings.PPTButtonsDisplayOption.ToString();
            char[] c = str.ToCharArray();
            c[1] = (bool)((CheckBox)sender).IsChecked ? '2' : '1';
            Settings.PowerPointSettings.PPTButtonsDisplayOption = int.Parse(new string(c));
            SaveSettingsToFile();
            if (BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible) UpdatePPTBtnDisplaySettingsStatus();
            UpdatePPTBtnPreview();
        }

        private void CheckboxEnableLSPPTButton_IsCheckChanged(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var str = Settings.PowerPointSettings.PPTButtonsDisplayOption.ToString();
            char[] c = str.ToCharArray();
            c[2] = (bool)((CheckBox)sender).IsChecked ? '2' : '1';
            Settings.PowerPointSettings.PPTButtonsDisplayOption = int.Parse(new string(c));
            SaveSettingsToFile();
            if (BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible) UpdatePPTBtnDisplaySettingsStatus();
            UpdatePPTBtnPreview();
        }

        private void CheckboxEnableRSPPTButton_IsCheckChanged(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var str = Settings.PowerPointSettings.PPTButtonsDisplayOption.ToString();
            char[] c = str.ToCharArray();
            c[3] = (bool)((CheckBox)sender).IsChecked ? '2' : '1';
            Settings.PowerPointSettings.PPTButtonsDisplayOption = int.Parse(new string(c));
            SaveSettingsToFile();
            if (BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible) UpdatePPTBtnDisplaySettingsStatus();
            UpdatePPTBtnPreview();
        }

        private void CheckboxSPPTDisplayPage_IsCheckChange(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var str = Settings.PowerPointSettings.PPTSButtonsOption.ToString();
            char[] c = str.ToCharArray();
            c[0] = (bool)((CheckBox)sender).IsChecked ? '2' : '1';
            Settings.PowerPointSettings.PPTSButtonsOption = int.Parse(new string(c));
            SaveSettingsToFile();
            if (BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible) UpdatePPTBtnStyleSettingsStatus();
            UpdatePPTBtnPreview();
        }

        private void CheckboxSPPTHalfOpacity_IsCheckChange(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var str = Settings.PowerPointSettings.PPTSButtonsOption.ToString();
            char[] c = str.ToCharArray();
            c[1] = (bool)((CheckBox)sender).IsChecked ? '2' : '1';
            Settings.PowerPointSettings.PPTSButtonsOption = int.Parse(new string(c));
            SaveSettingsToFile();
            if (BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible) UpdatePPTBtnStyleSettingsStatus();
            UpdatePPTBtnPreview();
        }

        private void CheckboxSPPTBlackBackground_IsCheckChange(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var str = Settings.PowerPointSettings.PPTSButtonsOption.ToString();
            char[] c = str.ToCharArray();
            c[2] = (bool)((CheckBox)sender).IsChecked ? '2' : '1';
            Settings.PowerPointSettings.PPTSButtonsOption = int.Parse(new string(c));
            SaveSettingsToFile();
            if (BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible) UpdatePPTBtnStyleSettingsStatus();
            UpdatePPTBtnPreview();
        }

        private void CheckboxBPPTDisplayPage_IsCheckChange(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var str = Settings.PowerPointSettings.PPTBButtonsOption.ToString();
            char[] c = str.ToCharArray();
            c[0] = (bool)((CheckBox)sender).IsChecked ? '2' : '1';
            Settings.PowerPointSettings.PPTBButtonsOption = int.Parse(new string(c));
            SaveSettingsToFile();
            if (BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible) UpdatePPTBtnStyleSettingsStatus();
            UpdatePPTBtnPreview();
        }

        private void CheckboxBPPTHalfOpacity_IsCheckChange(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var str = Settings.PowerPointSettings.PPTBButtonsOption.ToString();
            char[] c = str.ToCharArray();
            c[1] = (bool)((CheckBox)sender).IsChecked ? '2' : '1';
            Settings.PowerPointSettings.PPTBButtonsOption = int.Parse(new string(c));
            SaveSettingsToFile();
            if (BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible) UpdatePPTBtnStyleSettingsStatus();
            UpdatePPTBtnPreview();
        }

        private void CheckboxBPPTBlackBackground_IsCheckChange(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var str = Settings.PowerPointSettings.PPTBButtonsOption.ToString();
            char[] c = str.ToCharArray();
            c[2] = (bool)((CheckBox)sender).IsChecked ? '2' : '1';
            Settings.PowerPointSettings.PPTBButtonsOption = int.Parse(new string(c));
            SaveSettingsToFile();
            if (BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible) UpdatePPTBtnStyleSettingsStatus();
            UpdatePPTBtnPreview();
        }

        private void PPTButtonLeftPositionValueSlider_ValueChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.PowerPointSettings.PPTLSButtonPosition = (int)PPTButtonLeftPositionValueSlider.Value;
            UpdatePPTBtnSlidersStatus();
            if (BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible) UpdatePPTBtnDisplaySettingsStatus();
            SliderDelayAction.DebounceAction(2000, null, SaveSettingsToFile);
            UpdatePPTBtnPreview();
        }

        private void UpdatePPTBtnSlidersStatus() {
            if (PPTButtonLeftPositionValueSlider.Value <= -500 || PPTButtonLeftPositionValueSlider.Value >= 500) {
                if (PPTButtonLeftPositionValueSlider.Value >= 500) {
                    PPTBtnLSPlusBtn.IsEnabled = false;
                    PPTBtnLSPlusBtn.Opacity = 0.5;
                    PPTButtonLeftPositionValueSlider.Value = 500;
                } else if (PPTButtonLeftPositionValueSlider.Value <= -500) {
                    PPTBtnLSMinusBtn.IsEnabled = false;
                    PPTBtnLSMinusBtn.Opacity = 0.5;
                    PPTButtonLeftPositionValueSlider.Value = -500;
                }
            }
            else
            {
                PPTBtnLSPlusBtn.IsEnabled = true;
                PPTBtnLSPlusBtn.Opacity = 1;
                PPTBtnLSMinusBtn.IsEnabled = true;
                PPTBtnLSMinusBtn.Opacity = 1;
            }

            if (PPTButtonRightPositionValueSlider.Value <= -500 || PPTButtonRightPositionValueSlider.Value >= 500)
            {
                if (PPTButtonRightPositionValueSlider.Value >= 500)
                {
                    PPTBtnRSPlusBtn.IsEnabled = false;
                    PPTBtnRSPlusBtn.Opacity = 0.5;
                    PPTButtonRightPositionValueSlider.Value = 500;
                }
                else if (PPTButtonRightPositionValueSlider.Value <= -500)
                {
                    PPTBtnRSMinusBtn.IsEnabled = false;
                    PPTBtnRSMinusBtn.Opacity = 0.5;
                    PPTButtonRightPositionValueSlider.Value = -500;
                }
            }
            else
            {
                PPTBtnRSPlusBtn.IsEnabled = true;
                PPTBtnRSPlusBtn.Opacity = 1;
                PPTBtnRSMinusBtn.IsEnabled = true;
                PPTBtnRSMinusBtn.Opacity = 1;
            }
        }

        private void PPTBtnLSPlusBtn_Clicked(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            PPTButtonLeftPositionValueSlider.Value++;
            UpdatePPTBtnSlidersStatus();
            Settings.PowerPointSettings.PPTLSButtonPosition = (int)PPTButtonLeftPositionValueSlider.Value;
            SaveSettingsToFile();
            UpdatePPTBtnPreview();
        }

        private void PPTBtnLSMinusBtn_Clicked(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            PPTButtonLeftPositionValueSlider.Value--;
            UpdatePPTBtnSlidersStatus();
            Settings.PowerPointSettings.PPTLSButtonPosition = (int)PPTButtonLeftPositionValueSlider.Value;
            SaveSettingsToFile();
            UpdatePPTBtnPreview();
        }

        private void PPTBtnLSSyncBtn_Clicked(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            PPTButtonRightPositionValueSlider.Value = PPTButtonLeftPositionValueSlider.Value;
            UpdatePPTBtnSlidersStatus();
            Settings.PowerPointSettings.PPTRSButtonPosition = (int)PPTButtonLeftPositionValueSlider.Value;
            SaveSettingsToFile();
            UpdatePPTBtnPreview();
        }

        private void PPTBtnLSResetBtn_Clicked(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            PPTButtonLeftPositionValueSlider.Value = 0;
            UpdatePPTBtnSlidersStatus();
            Settings.PowerPointSettings.PPTLSButtonPosition = 0;
            SaveSettingsToFile();
            UpdatePPTBtnPreview();
        }

        private void PPTBtnRSPlusBtn_Clicked(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            PPTButtonRightPositionValueSlider.Value++;
            UpdatePPTBtnSlidersStatus();
            Settings.PowerPointSettings.PPTRSButtonPosition = (int)PPTButtonRightPositionValueSlider.Value;
            SaveSettingsToFile();
            UpdatePPTBtnPreview();
        }

        private void PPTBtnRSMinusBtn_Clicked(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            PPTButtonRightPositionValueSlider.Value--;
            UpdatePPTBtnSlidersStatus();
            Settings.PowerPointSettings.PPTRSButtonPosition = (int)PPTButtonRightPositionValueSlider.Value;
            SaveSettingsToFile();
            UpdatePPTBtnPreview();
        }

        private void PPTBtnRSSyncBtn_Clicked(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            PPTButtonLeftPositionValueSlider.Value = PPTButtonRightPositionValueSlider.Value;
            UpdatePPTBtnSlidersStatus();
            Settings.PowerPointSettings.PPTLSButtonPosition = (int)PPTButtonRightPositionValueSlider.Value;
            SaveSettingsToFile();
            UpdatePPTBtnPreview();
        }

        private void PPTBtnRSResetBtn_Clicked(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            PPTButtonRightPositionValueSlider.Value = 0;
            UpdatePPTBtnSlidersStatus();
            Settings.PowerPointSettings.PPTRSButtonPosition = 0;
            SaveSettingsToFile();
            UpdatePPTBtnPreview();
        }

        private DelayAction SliderDelayAction = new DelayAction();

        private void PPTButtonRightPositionValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.PowerPointSettings.PPTRSButtonPosition = (int)PPTButtonRightPositionValueSlider.Value;
            UpdatePPTBtnSlidersStatus();
            if (BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible) UpdatePPTBtnDisplaySettingsStatus();
            SliderDelayAction.DebounceAction(2000,null, SaveSettingsToFile);
            UpdatePPTBtnPreview();
        }

        private void UpdatePPTBtnPreview() {
            //new BitmapImage(new Uri("pack://application:,,,/Resources/new-icons/unfold-chevron.png"));
            var bopt = Settings.PowerPointSettings.PPTBButtonsOption.ToString();
            char[] boptc = bopt.ToCharArray();
            if (boptc[1] == '2') {
                PPTBtnPreviewLB.Opacity = 0.5;
                PPTBtnPreviewRB.Opacity = 0.5;
            } else {
                PPTBtnPreviewLB.Opacity = 1;
                PPTBtnPreviewRB.Opacity = 1;
            }

            if (boptc[2] == '2') {
                PPTBtnPreviewLB.Source =
                    new BitmapImage(
                        new Uri("pack://application:,,,/Resources/PresentationExample/bottombar-dark.png"));
                PPTBtnPreviewRB.Source = new BitmapImage(
                    new Uri("pack://application:,,,/Resources/PresentationExample/bottombar-dark.png"));
            } else {
                PPTBtnPreviewLB.Source =
                    new BitmapImage(
                        new Uri("pack://application:,,,/Resources/PresentationExample/bottombar-white.png"));
                PPTBtnPreviewRB.Source = new BitmapImage(
                    new Uri("pack://application:,,,/Resources/PresentationExample/bottombar-white.png"));
            }

            var sopt = Settings.PowerPointSettings.PPTSButtonsOption.ToString();
            char[] soptc = sopt.ToCharArray();
            if (soptc[1] == '2')
            {
                PPTBtnPreviewLS.Opacity = 0.5;
                PPTBtnPreviewRS.Opacity = 0.5;
            }
            else
            {
                PPTBtnPreviewLS.Opacity = 1;
                PPTBtnPreviewRS.Opacity = 1;
            }

            if (soptc[2] == '2')
            {
                PPTBtnPreviewLS.Source =
                    new BitmapImage(
                        new Uri("pack://application:,,,/Resources/PresentationExample/sidebar-dark.png"));
                PPTBtnPreviewRS.Source = new BitmapImage(
                    new Uri("pack://application:,,,/Resources/PresentationExample/sidebar-dark.png"));
            }
            else
            {
                PPTBtnPreviewLS.Source =
                    new BitmapImage(
                        new Uri("pack://application:,,,/Resources/PresentationExample/sidebar-white.png"));
                PPTBtnPreviewRS.Source = new BitmapImage(
                    new Uri("pack://application:,,,/Resources/PresentationExample/sidebar-white.png"));
            }

            var dopt = Settings.PowerPointSettings.PPTButtonsDisplayOption.ToString();
            char[] doptc = dopt.ToCharArray();

            if (Settings.PowerPointSettings.ShowPPTButton) {
                PPTBtnPreviewLB.Visibility = doptc[0] == '2' ? Visibility.Visible : Visibility.Collapsed;
                PPTBtnPreviewRB.Visibility = doptc[1] == '2' ? Visibility.Visible : Visibility.Collapsed;
                PPTBtnPreviewLS.Visibility = doptc[2] == '2' ? Visibility.Visible : Visibility.Collapsed;
                PPTBtnPreviewRS.Visibility = doptc[3] == '2' ? Visibility.Visible : Visibility.Collapsed;
            } else {
                PPTBtnPreviewLB.Visibility = Visibility.Collapsed;
                PPTBtnPreviewRB.Visibility = Visibility.Collapsed;
                PPTBtnPreviewLS.Visibility = Visibility.Collapsed;
                PPTBtnPreviewRS.Visibility = Visibility.Collapsed;
            }
            
            PPTBtnPreviewRSTransform.Y = -(Settings.PowerPointSettings.PPTRSButtonPosition * 0.5);
            PPTBtnPreviewLSTransform.Y = -(Settings.PowerPointSettings.PPTLSButtonPosition * 0.5);
        }

        private void ToggleSwitchShowCursor_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;

            Settings.Canvas.IsShowCursor = ToggleSwitchShowCursor.IsOn;
            inkCanvas_EditingModeChanged(inkCanvas, null);

            SaveSettingsToFile();
        }

        private async void ToggleSwitchFloatingBarButtonLabelVisibility_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;

            Settings.Appearance.FloatingBarButtonLabelVisibility = ToggleSwitchFloatingBarButtonLabelVisibility.IsOn;
            FloatingBarTextVisibilityBindingLikeAPieceOfShit.Visibility = Settings.Appearance.FloatingBarButtonLabelVisibility ? Visibility.Visible : Visibility.Collapsed;
            UpdateFloatingBarIconsLayout();
            await Task.Delay(1);
            ViewboxFloatingBarMarginAnimation(60,true);
            SaveSettingsToFile();
        }

        private void CheckboxFloatingBarIconsVisibility_CheckedChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;

            var items = new CheckBox[] {
                CheckboxEnableFloatingBarShapes,
                CheckboxEnableFloatingBarFreeze,
                CheckboxEnableFloatingBarHand,
                CheckboxEnableFloatingBarUndo,
                CheckboxEnableFloatingBarRedo,
                CheckboxEnableFloatingBarCAM,
                CheckboxEnableFloatingBarLasso,
                CheckboxEnableFloatingBarWhiteboard,
                CheckboxEnableFloatingBarFold,
                CheckboxEnableFloatingBarGesture
            };

            if (!items.Contains((CheckBox)sender)) return;
            if (Settings.Appearance.FloatingBarIconsVisibility.Length != 10) {
                Settings.Appearance.FloatingBarIconsVisibility =
                    Settings.Appearance.FloatingBarIconsVisibility.PadRight(10, '1');
                SaveSettingsToFile();
            }
            var value = Settings.Appearance.FloatingBarIconsVisibility;
            var vsb = new StringBuilder(value);
            vsb[Array.IndexOf(items, (CheckBox)sender)] = (bool)((CheckBox)sender).IsChecked ? '1' : '0';
            Settings.Appearance.FloatingBarIconsVisibility = vsb.ToString();

            UpdateFloatingBarIconsVisibility();
            ForceUpdateToolSelection(null);
            Dispatcher.InvokeAsync(async () => {
                if (BorderFloatingBarExitPPTBtn.Visibility == Visibility.Visible) {
                    await Task.Delay(10);
                    ViewboxFloatingBarMarginAnimation(60);
                } else if (Topmost == true) //非黑板
                {
                    await Task.Delay(10);
                    ViewboxFloatingBarMarginAnimation(100, true);
                } else //黑板
                {
                    await Task.Delay(10);
                    ViewboxFloatingBarMarginAnimation(60);
                }
            });

            SaveSettingsToFile();
        }

        private void ComboBoxEraserButton_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!isLoaded) return;
            Settings.Appearance.EraserButtonsVisibility = ComboBoxEraserButton.SelectedIndex;
            UpdateFloatingBarIconsVisibility();
            SaveSettingsToFile();
        }

        private void ToggleSwitchOnlyDisplayEraserBtn_OnToggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Appearance.OnlyDisplayEraserBtn = ToggleSwitchOnlyDisplayEraserBtn.IsOn;
            UpdateFloatingBarIconsVisibility();
            SaveSettingsToFile();
        }

        #endregion

        #region Canvas

        private void ComboBoxPenStyle_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!isLoaded) return;
            if (sender == ComboBoxPenStyle) {
                Settings.Canvas.InkStyle = ComboBoxPenStyle.SelectedIndex;
                BoardComboBoxPenStyle.SelectedIndex = ComboBoxPenStyle.SelectedIndex;
            } else {
                Settings.Canvas.InkStyle = BoardComboBoxPenStyle.SelectedIndex;
                ComboBoxPenStyle.SelectedIndex = BoardComboBoxPenStyle.SelectedIndex;
            }

            SaveSettingsToFile();
        }

        private void ComboBoxEraserSize_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.EraserSize = ComboBoxEraserSize.SelectedIndex;

            SaveSettingsToFile();
        }

        private void ComboBoxEraserSizeFloatingBar_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!isLoaded) return;

            ComboBox s = (ComboBox)sender;
            Settings.Canvas.EraserSize = s.SelectedIndex;
            if (s == ComboBoxEraserSizeFloatingBar) {
                BoardComboBoxEraserSize.SelectedIndex = s.SelectedIndex;
                ComboBoxEraserSize.SelectedIndex = s.SelectedIndex;
            } else if (s == BoardComboBoxEraserSize) {
                ComboBoxEraserSizeFloatingBar.SelectedIndex = s.SelectedIndex;
                ComboBoxEraserSize.SelectedIndex = s.SelectedIndex;
            }
            double width = 24;
            switch (Settings.Canvas.EraserSize)
            {
                case 0:
                    width = 24;
                    break;
                case 1:
                    width = 38;
                    break;
                case 2:
                    width = 46;
                    break;
                case 3:
                    width = 62;
                    break;
                case 4:
                    width = 78;
                    break;
            }

            eraserWidth = width;
            isEraserCircleShape = Settings.Canvas.EraserShapeType == 0;
            SaveSettingsToFile();
        }

        private void SwitchToCircleEraser(object sender, MouseButtonEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.EraserShapeType = 0;
            SaveSettingsToFile();
            CheckEraserTypeTab();
            isEraserCircleShape = true;
        }

        private void SwitchToRectangleEraser(object sender, MouseButtonEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.EraserShapeType = 1;
            SaveSettingsToFile();
            CheckEraserTypeTab();
            isEraserCircleShape = false;
        }


        private void InkWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (!isLoaded) return;
            if (sender == BoardInkWidthSlider) InkWidthSlider.Value = ((Slider)sender).Value;
            if (sender == InkWidthSlider) BoardInkWidthSlider.Value = ((Slider)sender).Value;
            drawingAttributes.Height = ((Slider)sender).Value / 2;
            drawingAttributes.Width = ((Slider)sender).Value / 2;
            Settings.Canvas.InkWidth = ((Slider)sender).Value / 2;
            SaveSettingsToFile();
        }

        private void HighlighterWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (!isLoaded) return;
            // if (sender == BoardInkWidthSlider) InkWidthSlider.Value = ((Slider)sender).Value;
            // if (sender == InkWidthSlider) BoardInkWidthSlider.Value = ((Slider)sender).Value;
            drawingAttributes.Height = ((Slider)sender).Value;
            drawingAttributes.Width = ((Slider)sender).Value / 2;
            Settings.Canvas.HighlighterWidth = ((Slider)sender).Value;
            SaveSettingsToFile();
        }

        private void InkAlphaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (!isLoaded) return;
            // if (sender == BoardInkWidthSlider) InkWidthSlider.Value = ((Slider)sender).Value;
            // if (sender == InkWidthSlider) BoardInkWidthSlider.Value = ((Slider)sender).Value;
            var NowR = drawingAttributes.Color.R;
            var NowG = drawingAttributes.Color.G;
            var NowB = drawingAttributes.Color.B;
            // Trace.WriteLine(BitConverter.GetBytes(((Slider)sender).Value));
            drawingAttributes.Color = Color.FromArgb((byte)((Slider)sender).Value, NowR, NowG, NowB);
            // drawingAttributes.Width = ((Slider)sender).Value / 2;
            // Settings.Canvas.InkAlpha = ((Slider)sender).Value;
            // SaveSettingsToFile();
        }

        private void ComboBoxHyperbolaAsymptoteOption_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.HyperbolaAsymptoteOption =
                (OptionalOperation)ComboBoxHyperbolaAsymptoteOption.SelectedIndex;
            SaveSettingsToFile();
        }

        private void ComboBoxBlackboardBackgroundColor_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.BlackboardBackgroundColor = (BlackboardBackgroundColorEnum)ComboBoxBlackboardBackgroundColor.SelectedIndex;
            SaveSettingsToFile();
        }

        private void ComboBoxBlackboardBackgroundPattern_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Canvas.BlackboardBackgroundPattern = (BlackboardBackgroundPatternEnum)ComboBoxBlackboardBackgroundPattern.SelectedIndex;
            SaveSettingsToFile();
        }

        private void ToggleSwitchUseDefaultBackgroundColorForEveryNewAddedBlackboardPage_Toggled(object sender,
            RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.UseDefaultBackgroundColorForEveryNewAddedBlackboardPage =
                ToggleSwitchUseDefaultBackgroundColorForEveryNewAddedBlackboardPage.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchUseDefaultBackgroundPatternForEveryNewAddedBlackboardPage_Toggled(object sender,
            RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Canvas.UseDefaultBackgroundPatternForEveryNewAddedBlackboardPage =
                ToggleSwitchUseDefaultBackgroundPatternForEveryNewAddedBlackboardPage.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchIsEnableAutoConvertInkColorWhenBackgroundChanged_Toggled(object sender,
            RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.IsEnableAutoConvertInkColorWhenBackgroundChanged =
                ToggleSwitchIsEnableAutoConvertInkColorWhenBackgroundChanged.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchApplyScaleToStylusTip_OnToggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.ApplyScaleToStylusTip = ToggleSwitchApplyScaleToStylusTip.IsOn;
            FloatingToolBarV2.SelectionV2.ApplyScaleToStylusTip = ToggleSwitchApplyScaleToStylusTip.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchOnlyHitTestFullyContainedStrokes_OnToggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.OnlyHitTestFullyContainedStrokes = ToggleSwitchOnlyHitTestFullyContainedStrokes.IsOn;
            FloatingToolBarV2.SelectionV2.OnlyHitTestFullyContainedStrokes = ToggleSwitchOnlyHitTestFullyContainedStrokes.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchAllowClickToSelectLockedStroke_OnToggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.AllowClickToSelectLockedStroke = ToggleSwitchAllowClickToSelectLockedStroke.IsOn;
            FloatingToolBarV2.SelectionV2.AllowClickToSelectLockedStroke = ToggleSwitchAllowClickToSelectLockedStroke.IsOn;
            SaveSettingsToFile();
        }

        private void ComboBoxSelectionMethod_SelectionChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.SelectionMethod = ComboBoxSelectionMethod.SelectedIndex;
            FloatingToolBarV2.SelectionV2.SelectionModeSelected = (SelectionPopup.SelectionMode)ComboBoxSelectionMethod.SelectedIndex;
            SaveSettingsToFile();
        }

        #endregion

        #region Automation

        private void StartOrStoptimerCheckAutoFold() {
            if (Settings.Automation.IsEnableAutoFold)
                timerCheckAutoFold.Start();
            else
                timerCheckAutoFold.Stop();
        }

        private void ToggleSwitchAutoFoldInEasiNote_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldInEasiNote = ToggleSwitchAutoFoldInEasiNote.IsOn;
            SaveSettingsToFile();
            StartOrStoptimerCheckAutoFold();
        }

        private void ToggleSwitchAutoFoldInEasiNoteIgnoreDesktopAnno_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldInEasiNoteIgnoreDesktopAnno =
                ToggleSwitchAutoFoldInEasiNoteIgnoreDesktopAnno.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchAutoFoldInEasiCamera_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldInEasiCamera = ToggleSwitchAutoFoldInEasiCamera.IsOn;
            SaveSettingsToFile();
            StartOrStoptimerCheckAutoFold();
        }

        private void ToggleSwitchAutoFoldInEasiNote3_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldInEasiNote3 = ToggleSwitchAutoFoldInEasiNote3.IsOn;
            SaveSettingsToFile();
            StartOrStoptimerCheckAutoFold();
        }

        private void ToggleSwitchAutoFoldInEasiNote3C_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldInEasiNote3C = ToggleSwitchAutoFoldInEasiNote3C.IsOn;
            SaveSettingsToFile();
            StartOrStoptimerCheckAutoFold();
        }

        private void ToggleSwitchAutoFoldInEasiNote5C_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldInEasiNote5C = ToggleSwitchAutoFoldInEasiNote5C.IsOn;
            SaveSettingsToFile();
            StartOrStoptimerCheckAutoFold();
        }

        private void ToggleSwitchAutoFoldInSeewoPincoTeacher_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldInSeewoPincoTeacher = ToggleSwitchAutoFoldInSeewoPincoTeacher.IsOn;
            SaveSettingsToFile();
            StartOrStoptimerCheckAutoFold();
        }

        private void ToggleSwitchAutoFoldInHiteTouchPro_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldInHiteTouchPro = ToggleSwitchAutoFoldInHiteTouchPro.IsOn;
            SaveSettingsToFile();
            StartOrStoptimerCheckAutoFold();
        }

        private void ToggleSwitchAutoFoldInHiteLightBoard_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldInHiteLightBoard = ToggleSwitchAutoFoldInHiteLightBoard.IsOn;
            SaveSettingsToFile();
            StartOrStoptimerCheckAutoFold();
        }

        private void ToggleSwitchAutoFoldInHiteCamera_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldInHiteCamera = ToggleSwitchAutoFoldInHiteCamera.IsOn;
            SaveSettingsToFile();
            StartOrStoptimerCheckAutoFold();
        }

        private void ToggleSwitchAutoFoldInWxBoardMain_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldInWxBoardMain = ToggleSwitchAutoFoldInWxBoardMain.IsOn;
            SaveSettingsToFile();
            StartOrStoptimerCheckAutoFold();
        }

        private void ToggleSwitchAutoFoldInOldZyBoard_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldInOldZyBoard = ToggleSwitchAutoFoldInOldZyBoard.IsOn;
            SaveSettingsToFile();
            StartOrStoptimerCheckAutoFold();
        }

        private void ToggleSwitchAutoFoldInMSWhiteboard_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldInMSWhiteboard = ToggleSwitchAutoFoldInMSWhiteboard.IsOn;
            SaveSettingsToFile();
            StartOrStoptimerCheckAutoFold();
        }

        private void ToggleSwitchAutoFoldInAdmoxWhiteboard_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldInAdmoxWhiteboard = ToggleSwitchAutoFoldInAdmoxWhiteboard.IsOn;
            SaveSettingsToFile();
            StartOrStoptimerCheckAutoFold();
        }

        private void ToggleSwitchAutoFoldInAdmoxBooth_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldInAdmoxBooth = ToggleSwitchAutoFoldInAdmoxBooth.IsOn;
            SaveSettingsToFile();
            StartOrStoptimerCheckAutoFold();
        }

        private void ToggleSwitchAutoFoldInQPoint_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldInQPoint = ToggleSwitchAutoFoldInQPoint.IsOn;
            SaveSettingsToFile();
            StartOrStoptimerCheckAutoFold();
        }

        private void ToggleSwitchAutoFoldInYiYunVisualPresenter_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldInYiYunVisualPresenter = ToggleSwitchAutoFoldInYiYunVisualPresenter.IsOn;
            SaveSettingsToFile();
            StartOrStoptimerCheckAutoFold();
        }

        private void ToggleSwitchAutoFoldInMaxHubWhiteboard_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldInMaxHubWhiteboard = ToggleSwitchAutoFoldInMaxHubWhiteboard.IsOn;
            SaveSettingsToFile();
            StartOrStoptimerCheckAutoFold();
        }

        private void ToggleSwitchAutoFoldInPPTSlideShow_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldInPPTSlideShow = ToggleSwitchAutoFoldInPPTSlideShow.IsOn;
            if (Settings.Automation.IsAutoFoldInPPTSlideShow)
            {
                SettingsPPTInkingAndAutoFoldExplictBorder.IsOpen = true;
                SettingsShowCanvasAtNewSlideShowStackPanel.Opacity = 0.5;
                SettingsShowCanvasAtNewSlideShowStackPanel.IsHitTestVisible = false;
            } else {
                SettingsPPTInkingAndAutoFoldExplictBorder.IsOpen = false;
                SettingsShowCanvasAtNewSlideShowStackPanel.Opacity = 1;
                SettingsShowCanvasAtNewSlideShowStackPanel.IsHitTestVisible = true;
            }
            SaveSettingsToFile();
            StartOrStoptimerCheckAutoFold();
        }

        private void ToggleSwitchAutoKillPptService_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoKillPptService = ToggleSwitchAutoKillPptService.IsOn;
            SaveSettingsToFile();

            if (Settings.Automation.IsAutoKillEasiNote || Settings.Automation.IsAutoKillPptService ||
                Settings.Automation.IsAutoKillHiteAnnotation || Settings.Automation.IsAutoKillInkCanvas
                || Settings.Automation.IsAutoKillICA || Settings.Automation.IsAutoKillIDT || Settings.Automation.IsAutoKillVComYouJiao
                || Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation)
                timerKillProcess.Start();
            else
                timerKillProcess.Stop();
        }

        private void ToggleSwitchAutoKillEasiNote_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoKillEasiNote = ToggleSwitchAutoKillEasiNote.IsOn;
            SaveSettingsToFile();
            if (Settings.Automation.IsAutoKillEasiNote || Settings.Automation.IsAutoKillPptService ||
                Settings.Automation.IsAutoKillHiteAnnotation || Settings.Automation.IsAutoKillInkCanvas
                || Settings.Automation.IsAutoKillICA || Settings.Automation.IsAutoKillIDT || Settings.Automation.IsAutoKillVComYouJiao
                || Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation)
                timerKillProcess.Start();
            else
                timerKillProcess.Stop();
        }

        private void ToggleSwitchAutoKillHiteAnnotation_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoKillHiteAnnotation = ToggleSwitchAutoKillHiteAnnotation.IsOn;
            SaveSettingsToFile();
            if (Settings.Automation.IsAutoKillEasiNote || Settings.Automation.IsAutoKillPptService ||
                Settings.Automation.IsAutoKillHiteAnnotation || Settings.Automation.IsAutoKillInkCanvas
                || Settings.Automation.IsAutoKillICA || Settings.Automation.IsAutoKillIDT || Settings.Automation.IsAutoKillVComYouJiao
                || Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation)
                timerKillProcess.Start();
            else
                timerKillProcess.Stop();
        }

        private void ToggleSwitchAutoKillVComYouJiao_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.IsAutoKillVComYouJiao = ToggleSwitchAutoKillVComYouJiao.IsOn;
            SaveSettingsToFile();
            if (Settings.Automation.IsAutoKillEasiNote || Settings.Automation.IsAutoKillPptService ||
                Settings.Automation.IsAutoKillHiteAnnotation || Settings.Automation.IsAutoKillInkCanvas
                || Settings.Automation.IsAutoKillICA || Settings.Automation.IsAutoKillIDT || Settings.Automation.IsAutoKillVComYouJiao
                || Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation)
                timerKillProcess.Start();
            else
                timerKillProcess.Stop();
        }

        private void ToggleSwitchAutoKillSeewoLauncher2DesktopAnnotation_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation = ToggleSwitchAutoKillSeewoLauncher2DesktopAnnotation.IsOn;
            SaveSettingsToFile();
            if (Settings.Automation.IsAutoKillEasiNote || Settings.Automation.IsAutoKillPptService ||
                Settings.Automation.IsAutoKillHiteAnnotation || Settings.Automation.IsAutoKillInkCanvas
                || Settings.Automation.IsAutoKillICA || Settings.Automation.IsAutoKillIDT || Settings.Automation.IsAutoKillVComYouJiao
                || Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation)
                timerKillProcess.Start();
            else
                timerKillProcess.Stop();
        }

        private void ToggleSwitchAutoKillInkCanvas_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.IsAutoKillInkCanvas = ToggleSwitchAutoKillInkCanvas.IsOn;
            SaveSettingsToFile();
            if (Settings.Automation.IsAutoKillEasiNote || Settings.Automation.IsAutoKillPptService ||
                Settings.Automation.IsAutoKillHiteAnnotation || Settings.Automation.IsAutoKillInkCanvas
                || Settings.Automation.IsAutoKillICA || Settings.Automation.IsAutoKillIDT || Settings.Automation.IsAutoKillVComYouJiao
                || Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation)
                timerKillProcess.Start();
            else
                timerKillProcess.Stop();
        }

        private void ToggleSwitchAutoKillICA_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.IsAutoKillICA = ToggleSwitchAutoKillICA.IsOn;
            SaveSettingsToFile();
            if (Settings.Automation.IsAutoKillEasiNote || Settings.Automation.IsAutoKillPptService ||
                Settings.Automation.IsAutoKillHiteAnnotation || Settings.Automation.IsAutoKillInkCanvas
                || Settings.Automation.IsAutoKillICA || Settings.Automation.IsAutoKillIDT || Settings.Automation.IsAutoKillVComYouJiao
                || Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation)
                timerKillProcess.Start();
            else
                timerKillProcess.Stop();
        }

        //private void ToggleSwitchAutoKillIDT_Toggled(object sender, RoutedEventArgs e)
        //{
        //    if (!isLoaded) return;
        //    Settings.Automation.IsAutoKillIDT = ToggleSwitchAutoKillIDT.IsOn;
        //    SaveSettingsToFile();
        //    if (Settings.Automation.IsAutoKillEasiNote || Settings.Automation.IsAutoKillPptService ||
        //        Settings.Automation.IsAutoKillHiteAnnotation || Settings.Automation.IsAutoKillInkCanvas
        //        || Settings.Automation.IsAutoKillICA || Settings.Automation.IsAutoKillIDT || Settings.Automation.IsAutoKillVComYouJiao
        //        || Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation)
        //        timerKillProcess.Start();
        //    else
        //        timerKillProcess.Stop();
        //}

        private void ToggleSwitchSaveScreenshotsInDateFolders_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsSaveScreenshotsInDateFolders = ToggleSwitchSaveScreenshotsInDateFolders.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchAutoSaveStrokesAtScreenshot_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoSaveStrokesAtScreenshot = ToggleSwitchAutoSaveStrokesAtScreenshot.IsOn;
            ToggleSwitchAutoSaveStrokesAtClear.Header =
                ToggleSwitchAutoSaveStrokesAtScreenshot.IsOn ? "清屏时自动截图并保存墨迹" : "清屏时自动截图";
            SaveSettingsToFile();
        }

        private void ToggleSwitchAutoSaveStrokesAtClear_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoSaveStrokesAtClear = ToggleSwitchAutoSaveStrokesAtClear.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchHideStrokeWhenSelecting_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.HideStrokeWhenSelecting = ToggleSwitchHideStrokeWhenSelecting.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchClearCanvasAndClearTimeMachine_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.ClearCanvasAndClearTimeMachine = ToggleSwitchClearCanvasAndClearTimeMachine.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchFitToCurve_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            drawingAttributes.FitToCurve = ToggleSwitchFitToCurve.IsOn;
            Settings.Canvas.FitToCurve = ToggleSwitchFitToCurve.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchAutoSaveStrokesInPowerPoint_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint = ToggleSwitchAutoSaveStrokesInPowerPoint.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchNotifyPreviousPage_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.PowerPointSettings.IsNotifyPreviousPage = ToggleSwitchNotifyPreviousPage.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchNotifyHiddenPage_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.PowerPointSettings.IsNotifyHiddenPage = ToggleSwitchNotifyHiddenPage.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchNotifyAutoPlayPresentation_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.PowerPointSettings.IsNotifyAutoPlayPresentation = ToggleSwitchNotifyAutoPlayPresentation.IsOn;
            SaveSettingsToFile();
        }

        private void SideControlMinimumAutomationSlider_ValueChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.MinimumAutomationStrokeNumber = (int)SideControlMinimumAutomationSlider.Value;
            SaveSettingsToFile();
        }

        private void ToggleSwitchAutoDelSavedFiles_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.AutoDelSavedFiles = ToggleSwitchAutoDelSavedFiles.IsOn;
            SaveSettingsToFile();
        }

        private void
            ComboBoxAutoDelSavedFilesDaysThreshold_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.AutoDelSavedFilesDaysThreshold =
                int.Parse(((ComboBoxItem)ComboBoxAutoDelSavedFilesDaysThreshold.SelectedItem).Content.ToString());
            SaveSettingsToFile();
        }

        private void ToggleSwitchAutoSaveScreenShotInPowerPoint_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint =
                ToggleSwitchAutoSaveScreenShotInPowerPoint.IsOn;
            SaveSettingsToFile();
        }


        private void ToggleSwitchLimitAutoSaveAmount_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.IsEnableLimitAutoSaveAmount = ToggleSwitchLimitAutoSaveAmount.IsOn;
            SaveSettingsToFile();
        }

        private void ComboBoxLimitAutoSaveAmount_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.LimitAutoSaveAmount = ComboBoxLimitAutoSaveAmount.SelectedIndex;
            SaveSettingsToFile();
        }

        #endregion

        #region Gesture

        private void ToggleSwitchAutoSwitchTwoFingerGesture_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Gesture.AutoSwitchTwoFingerGesture = ToggleSwitchAutoSwitchTwoFingerGesture.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchEnableTwoFingerZoom_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            if (sender == ToggleSwitchEnableTwoFingerZoom)
                BoardToggleSwitchEnableTwoFingerZoom.IsOn = ToggleSwitchEnableTwoFingerZoom.IsOn;
            else
                ToggleSwitchEnableTwoFingerZoom.IsOn = BoardToggleSwitchEnableTwoFingerZoom.IsOn;
            Settings.Gesture.IsEnableTwoFingerZoom = ToggleSwitchEnableTwoFingerZoom.IsOn;
            CheckEnableTwoFingerGestureBtnColorPrompt();
            SaveSettingsToFile();
        }

        private void ToggleSwitchEnableMultiTouchMode_Toggled(object sender, RoutedEventArgs e) {
            //if (!isLoaded) return;
            if (sender == ToggleSwitchEnableMultiTouchMode)
                BoardToggleSwitchEnableMultiTouchMode.IsOn = ToggleSwitchEnableMultiTouchMode.IsOn;
            else
                ToggleSwitchEnableMultiTouchMode.IsOn = BoardToggleSwitchEnableMultiTouchMode.IsOn;
            if (ToggleSwitchEnableMultiTouchMode.IsOn) {
                if (!isInMultiTouchMode) {
                    inkCanvas.StylusDown += MainWindow_StylusDown;
                    inkCanvas.StylusMove += MainWindow_StylusMove;
                    inkCanvas.StylusUp += MainWindow_StylusUp;
                    inkCanvas.TouchDown += MainWindow_TouchDown;
                    inkCanvas.TouchDown -= Main_Grid_TouchDown;
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    inkCanvas.Children.Clear();
                    isInMultiTouchMode = true;
                }
            } else {
                if (isInMultiTouchMode) {
                    inkCanvas.StylusDown -= MainWindow_StylusDown;
                    inkCanvas.StylusMove -= MainWindow_StylusMove;
                    inkCanvas.StylusUp -= MainWindow_StylusUp;
                    inkCanvas.TouchDown -= MainWindow_TouchDown;
                    inkCanvas.TouchDown += Main_Grid_TouchDown;
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    inkCanvas.Children.Clear();
                    isInMultiTouchMode = false;
                }
            }

            Settings.Gesture.IsEnableMultiTouchMode = ToggleSwitchEnableMultiTouchMode.IsOn;
            CheckEnableTwoFingerGestureBtnColorPrompt();
            SaveSettingsToFile();
        }

        private void ToggleSwitchEnableTwoFingerTranslate_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            if (sender == ToggleSwitchEnableTwoFingerTranslate)
                BoardToggleSwitchEnableTwoFingerTranslate.IsOn = ToggleSwitchEnableTwoFingerTranslate.IsOn;
            else
                ToggleSwitchEnableTwoFingerTranslate.IsOn = BoardToggleSwitchEnableTwoFingerTranslate.IsOn;
            Settings.Gesture.IsEnableTwoFingerTranslate = ToggleSwitchEnableTwoFingerTranslate.IsOn;
            CheckEnableTwoFingerGestureBtnColorPrompt();
            SaveSettingsToFile();
        }

        private void ToggleSwitchEnableTwoFingerRotation_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;

            if (sender == ToggleSwitchEnableTwoFingerRotation)
                BoardToggleSwitchEnableTwoFingerRotation.IsOn = ToggleSwitchEnableTwoFingerRotation.IsOn;
            else
                ToggleSwitchEnableTwoFingerRotation.IsOn = BoardToggleSwitchEnableTwoFingerRotation.IsOn;
            Settings.Gesture.IsEnableTwoFingerRotation = ToggleSwitchEnableTwoFingerRotation.IsOn;
            Settings.Gesture.IsEnableTwoFingerRotationOnSelection = ToggleSwitchEnableTwoFingerRotationOnSelection.IsOn;
            CheckEnableTwoFingerGestureBtnColorPrompt();
            SaveSettingsToFile();
        }

        private void ToggleSwitchEnableTwoFingerGestureInPresentationMode_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode =
                ToggleSwitchEnableTwoFingerGestureInPresentationMode.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchDisableGestureEraser_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Gesture.DisableGestureEraser = ToggleSwitchDisableGestureEraser.IsOn;
            if (Settings.Gesture.DisableGestureEraser) {
                GestureEraserSettingsItemsPanel.Opacity = 0.5;
                GestureEraserSettingsItemsPanel.IsHitTestVisible = false;
                SettingsGestureEraserDisabledBorder.IsOpen = true;
            } else {
                GestureEraserSettingsItemsPanel.Opacity = 1;
                GestureEraserSettingsItemsPanel.IsHitTestVisible = true;
                SettingsGestureEraserDisabledBorder.IsOpen = false;
            }
            SaveSettingsToFile();
        }

        private void ComboBoxDefaultMultiPointHandWriting_SelectionChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Gesture.DefaultMultiPointHandWritingMode = ComboBoxDefaultMultiPointHandWriting.SelectedIndex;
            SaveSettingsToFile();
        }

        private void ToggleSwitchHideCursorWhenUsingTouchDevice_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Gesture.HideCursorWhenUsingTouchDevice = ToggleSwitchHideCursorWhenUsingTouchDevice.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchEnableMouseGesture_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Gesture.EnableMouseGesture = ToggleSwitchEnableMouseGesture.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchEnableMouseRightBtnGesture_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Gesture.EnableMouseRightBtnGesture = ToggleSwitchEnableMouseRightBtnGesture.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchEnableMouseWheelGesture_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Gesture.EnableMouseWheelGesture = ToggleSwitchEnableMouseWheelGesture.IsOn;
            SaveSettingsToFile();
        }

        private void ComboBoxWindowsInkEraserButtonAction_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!isLoaded) return;
            Settings.Gesture.WindowsInkEraserButtonAction = ComboBoxWindowsInkEraserButtonAction.SelectedIndex;
            SaveSettingsToFile();
        }

        private void ComboBoxWindowsInkBarrelButtonAction_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!isLoaded) return;
            Settings.Gesture.WindowsInkBarrelButtonAction = ComboBoxWindowsInkBarrelButtonAction.SelectedIndex;
            SaveSettingsToFile();
        }

        #endregion

        #region Reset

        public static void SetSettingsToRecommendation() {
            var AutoDelSavedFilesDays = Settings.Automation.AutoDelSavedFiles;
            var AutoDelSavedFilesDaysThreshold = Settings.Automation.AutoDelSavedFilesDaysThreshold;
            Settings = new Settings();
            Settings.Advanced.IsSpecialScreen = true;
            Settings.Advanced.IsQuadIR = false;
            Settings.Advanced.TouchMultiplier = 0.3;
            Settings.Advanced.NibModeBoundsWidth = 5;
            Settings.Advanced.FingerModeBoundsWidth = 20;
            Settings.Advanced.EraserBindTouchMultiplier = true;
            Settings.Advanced.IsLogEnabled = true;
            Settings.Advanced.IsEnableEdgeGestureUtil = false;
            Settings.Advanced.EdgeGestureUtilOnlyAffectBlackboardMode = false;
            Settings.Advanced.IsEnableFullScreenHelper = false;
            Settings.Advanced.IsEnableForceFullScreen = false;
            Settings.Advanced.IsEnableDPIChangeDetection = false;
            Settings.Advanced.IsEnableResolutionChangeDetection = false;
            Settings.Advanced.IsDisableCloseWindow = true;
            Settings.Advanced.EnableForceTopMost = false;

            Settings.Appearance.IsEnableDisPlayNibModeToggler = false;
            Settings.Appearance.IsColorfulViewboxFloatingBar = false;
            Settings.Appearance.ViewboxFloatingBarScaleTransformValue = 1;
            Settings.Appearance.EnableViewboxBlackBoardScaleTransform = false;
            Settings.Appearance.IsTransparentButtonBackground = true;
            Settings.Appearance.IsShowExitButton = true;
            Settings.Appearance.IsShowEraserButton = true;
            Settings.Appearance.IsShowHideControlButton = false;
            Settings.Appearance.IsShowLRSwitchButton = false;
            Settings.Appearance.IsShowModeFingerToggleSwitch = true;
            Settings.Appearance.IsShowQuickPanel = true;
            Settings.Appearance.Theme = 0;
            Settings.Appearance.EnableChickenSoupInWhiteboardMode = true;
            Settings.Appearance.EnableTimeDisplayInWhiteboardMode = true;
            Settings.Appearance.ChickenSoupSource = 1;
            Settings.Appearance.ViewboxFloatingBarOpacityValue = 1.0;
            Settings.Appearance.ViewboxFloatingBarOpacityInPPTValue = 1.0;
            Settings.Appearance.EnableTrayIcon = true;
            Settings.Appearance.FloatingBarButtonLabelVisibility = true;
            Settings.Appearance.FloatingBarIconsVisibility = "11111111";
            Settings.Appearance.EraserButtonsVisibility = 0;
            Settings.Appearance.OnlyDisplayEraserBtn = false;

            Settings.Automation.IsAutoFoldInEasiNote = true;
            Settings.Automation.IsAutoFoldInEasiNoteIgnoreDesktopAnno = true;
            Settings.Automation.IsAutoFoldInEasiCamera = true;
            Settings.Automation.IsAutoFoldInEasiNote3C = false;
            Settings.Automation.IsAutoFoldInEasiNote3 = false;
            Settings.Automation.IsAutoFoldInEasiNote5C = true;
            Settings.Automation.IsAutoFoldInSeewoPincoTeacher = false;
            Settings.Automation.IsAutoFoldInHiteTouchPro = false;
            Settings.Automation.IsAutoFoldInHiteCamera = false;
            Settings.Automation.IsAutoFoldInWxBoardMain = false;
            Settings.Automation.IsAutoFoldInOldZyBoard = false;
            Settings.Automation.IsAutoFoldInMSWhiteboard = false;
            Settings.Automation.IsAutoFoldInAdmoxWhiteboard = false;
            Settings.Automation.IsAutoFoldInAdmoxBooth = false;
            Settings.Automation.IsAutoFoldInQPoint = false;
            Settings.Automation.IsAutoFoldInYiYunVisualPresenter = false;
            Settings.Automation.IsAutoFoldInMaxHubWhiteboard = false;
            Settings.Automation.IsAutoFoldInPPTSlideShow = false;
            Settings.Automation.IsAutoKillPptService = false;
            Settings.Automation.IsAutoKillEasiNote = false;
            Settings.Automation.IsAutoKillVComYouJiao = false;
            Settings.Automation.IsAutoKillInkCanvas = false;
            Settings.Automation.IsAutoKillICA = false;
            Settings.Automation.IsAutoKillIDT = true;
            Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation = false;
            Settings.Automation.IsSaveScreenshotsInDateFolders = false;
            Settings.Automation.IsAutoSaveStrokesAtScreenshot = true;
            Settings.Automation.IsAutoSaveStrokesAtClear = true;
            Settings.Automation.IsAutoClearWhenExitingWritingMode = false;
            Settings.Automation.MinimumAutomationStrokeNumber = 0;
            Settings.Automation.AutoDelSavedFiles = AutoDelSavedFilesDays;
            Settings.Automation.AutoDelSavedFilesDaysThreshold = AutoDelSavedFilesDaysThreshold;
            Settings.Automation.IsEnableLimitAutoSaveAmount = false;
            Settings.Automation.LimitAutoSaveAmount = 3;

            //Settings.PowerPointSettings.IsShowPPTNavigation = true;
            //Settings.PowerPointSettings.IsShowBottomPPTNavigationPanel = false;
            //Settings.PowerPointSettings.IsShowSidePPTNavigationPanel = true;
            Settings.PowerPointSettings.PowerPointSupport = true;
            Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow = false;
            Settings.PowerPointSettings.IsNoClearStrokeOnSelectWhenInPowerPoint = true;
            //Settings.PowerPointSettings.IsShowStrokeOnSelectInPowerPoint = false;
            Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint = true;
            Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint = true;
            Settings.PowerPointSettings.IsNotifyPreviousPage = false;
            Settings.PowerPointSettings.IsNotifyHiddenPage = false;
            Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode = false;
            Settings.PowerPointSettings.IsSupportWPS = true;
            Settings.PowerPointSettings.RegistryShowBlackScreenLastSlideShow = false;
            Settings.PowerPointSettings.RegistryShowSlideShowToolbar = false;

            Settings.Canvas.InkWidth = 2.5;
            Settings.Canvas.IsShowCursor = false;
            Settings.Canvas.InkStyle = 0;
            Settings.Canvas.HighlighterWidth = 20;
            Settings.Canvas.EraserSize = 1;
            Settings.Canvas.EraserType = 0;
            Settings.Canvas.EraserShapeType = 1;
            Settings.Canvas.HideStrokeWhenSelecting = false;
            Settings.Canvas.ClearCanvasAndClearTimeMachine = false;
            Settings.Canvas.FitToCurve = false;
            //Settings.Canvas.UsingWhiteboard = false;
            Settings.Canvas.HyperbolaAsymptoteOption = 0;
            Settings.Canvas.BlackboardBackgroundColor = BlackboardBackgroundColorEnum.White;
            Settings.Canvas.BlackboardBackgroundPattern = BlackboardBackgroundPatternEnum.None;
            Settings.Canvas.IsEnableAutoConvertInkColorWhenBackgroundChanged = false;
            Settings.Canvas.UseDefaultBackgroundColorForEveryNewAddedBlackboardPage = false;
            Settings.Canvas.UseDefaultBackgroundPatternForEveryNewAddedBlackboardPage = false;
            Settings.Canvas.SelectionMethod = 0;
            Settings.Canvas.ApplyScaleToStylusTip = false;
            Settings.Canvas.OnlyHitTestFullyContainedStrokes = false;
            Settings.Canvas.AllowClickToSelectLockedStroke = false;

            Settings.Gesture.AutoSwitchTwoFingerGesture = true;
            Settings.Gesture.IsEnableTwoFingerTranslate = true;
            Settings.Gesture.IsEnableTwoFingerZoom = false;
            Settings.Gesture.IsEnableTwoFingerRotation = false;
            Settings.Gesture.IsEnableTwoFingerRotationOnSelection = false;
            Settings.Gesture.DisableGestureEraser = true;
            Settings.Gesture.DefaultMultiPointHandWritingMode = 2;
            Settings.Gesture.HideCursorWhenUsingTouchDevice = true;
            Settings.Gesture.EnableMouseGesture = true;
            Settings.Gesture.EnableMouseRightBtnGesture = true;
            Settings.Gesture.EnableMouseWheelGesture = true;
            Settings.Gesture.WindowsInkEraserButtonAction = 2;
            Settings.Gesture.WindowsInkBarrelButtonAction = 2;

            Settings.InkToShape.IsInkToShapeEnabled = true;
            Settings.InkToShape.IsInkToShapeNoFakePressureRectangle = false;
            Settings.InkToShape.IsInkToShapeNoFakePressureTriangle = false;
            Settings.InkToShape.IsInkToShapeTriangle = true;
            Settings.InkToShape.IsInkToShapeRectangle = true;
            Settings.InkToShape.IsInkToShapeRounded = true;


            Settings.Startup.IsEnableNibMode = false;
            Settings.Startup.IsAutoUpdate = true;
            Settings.Startup.IsAutoUpdateWithSilence = true;
            Settings.Startup.AutoUpdateWithSilenceStartTime = "18:20";
            Settings.Startup.AutoUpdateWithSilenceEndTime = "07:40";
            Settings.Startup.IsFoldAtStartup = false;
            Settings.Startup.EnableWindowChromeRendering = false;

            Settings.Snapshot.CopyScreenshotToClipboard = true;
            Settings.Snapshot.AttachInkWhenScreenshot = true;
            Settings.Snapshot.OnlySnapshotMaximizeWindow = false;
            Settings.Snapshot.ScreenshotFileName = "Screenshot-[YYYY]-[MM]-[DD]-[HH]-[mm]-[ss].png";
            Settings.Snapshot.ScreenshotUsingMagnificationAPI = false;

            Settings.Storage.StorageLocation = "fr";
            Settings.Storage.UserStorageLocation = "";
        }

        private void BtnResetToSuggestion_Click(object sender, RoutedEventArgs e) {
            try {
                isLoaded = false;
                SetSettingsToRecommendation();
                SaveSettingsToFile();
                LoadSettings();
                isLoaded = true;

                isChangingUserStorageSelectionProgramically = true;
                UpdateStorageLocations();
                UpdateUserStorageSelection();
                isChangingUserStorageSelectionProgramically = false;
                HandleUserCustomStorageLocation();
                InitStorageFoldersStructure(storageLocationItems[ComboBoxStoragePath.SelectedIndex].Path);
                StartAnalyzeStorage();
                CustomStorageLocationGroup.Visibility = ((StorageLocationItem)ComboBoxStoragePath.SelectedItem).SelectItem == "c-" ? Visibility.Visible : Visibility.Collapsed;
                CustomStorageLocationCheckPanel.Visibility = ((StorageLocationItem)ComboBoxStoragePath.SelectedItem).SelectItem == "c-" ? Visibility.Visible : Visibility.Collapsed;
                CustomStorageLocation.Text = Settings.Storage.UserStorageLocation;

                ToggleSwitchRunAtStartup.IsOn = true;
            }
            catch { }

            ShowNewToast("设置已重置为默认推荐设置~", MW_Toast.ToastType.Success, 2500);
        }

        private async void SpecialVersionResetToSuggestion_Click() {
            await Task.Delay(1000);
            try {
                isLoaded = false;
                SetSettingsToRecommendation();
                Settings.Automation.AutoDelSavedFiles = true;
                Settings.Automation.AutoDelSavedFilesDaysThreshold = 15;
                SaveSettingsToFile();
                LoadSettings();
                isLoaded = true;
            }
            catch { }
        }

        #endregion

        #region Ink To Shape

        private void ToggleSwitchEnableInkToShape_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.InkToShape.IsInkToShapeEnabled = ToggleSwitchEnableInkToShape.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchEnableInkToShapeNoFakePressureTriangle_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.InkToShape.IsInkToShapeNoFakePressureTriangle =
                ToggleSwitchEnableInkToShapeNoFakePressureTriangle.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchEnableInkToShapeNoFakePressureRectangle_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.InkToShape.IsInkToShapeNoFakePressureRectangle =
                ToggleSwitchEnableInkToShapeNoFakePressureRectangle.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleCheckboxEnableInkToShapeTriangle_CheckedChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.InkToShape.IsInkToShapeTriangle = (bool)ToggleCheckboxEnableInkToShapeTriangle.IsChecked;
            SaveSettingsToFile();
        }

        private void ToggleCheckboxEnableInkToShapeRectangle_CheckedChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.InkToShape.IsInkToShapeRectangle = (bool)ToggleCheckboxEnableInkToShapeRectangle.IsChecked;
            SaveSettingsToFile();
        }

        private void ToggleCheckboxEnableInkToShapeRounded_CheckedChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.InkToShape.IsInkToShapeRounded = (bool)ToggleCheckboxEnableInkToShapeRounded.IsChecked;
            SaveSettingsToFile();
        }

        #endregion

        #region Advanced

        private void ToggleSwitchIsSpecialScreen_OnToggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Advanced.IsSpecialScreen = ToggleSwitchIsSpecialScreen.IsOn;
            TouchMultiplierSlider.Visibility =
                ToggleSwitchIsSpecialScreen.IsOn ? Visibility.Visible : Visibility.Collapsed;
            SaveSettingsToFile();
        }

        private void TouchMultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (!isLoaded) return;
            Settings.Advanced.TouchMultiplier = e.NewValue;
            SaveSettingsToFile();
        }

        private void BorderCalculateMultiplier_TouchDown(object sender, TouchEventArgs e) {
            var args = e.GetTouchPoint(null).Bounds;
            double value;
            if (!Settings.Advanced.IsQuadIR) value = args.Width;
            else value = Math.Sqrt(args.Width * args.Height); //四边红外

            TextBlockShowCalculatedMultiplier.Text = (5 / (value * 1.1)).ToString();
        }

        private void ToggleSwitchIsEnableFullScreenHelper_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Advanced.IsEnableFullScreenHelper = ToggleSwitchIsEnableFullScreenHelper.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchIsEnableEdgeGestureUtil_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Advanced.IsEnableEdgeGestureUtil = ToggleSwitchIsEnableEdgeGestureUtil.IsOn;
            if (OSVersion.GetOperatingSystem() >= OSVersionExtension.OperatingSystem.Windows10) EdgeGestureUtil.DisableEdgeGestures(new WindowInteropHelper(this).Handle, ToggleSwitchIsEnableEdgeGestureUtil.IsOn);
            SaveSettingsToFile();
        }

        private void ToggleSwitchIsEnableForceFullScreen_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Advanced.IsEnableForceFullScreen = ToggleSwitchIsEnableForceFullScreen.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchIsEnableDPIChangeDetection_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Advanced.IsEnableDPIChangeDetection = ToggleSwitchIsEnableDPIChangeDetection.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchIsEnableResolutionChangeDetection_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Advanced.IsEnableResolutionChangeDetection = ToggleSwitchIsEnableResolutionChangeDetection.IsOn;
            SaveSettingsToFile();
        }

        private void NibModeBoundsWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (!isLoaded) return;
            Settings.Advanced.NibModeBoundsWidth = (int)e.NewValue;

            if (Settings.Startup.IsEnableNibMode)
                BoundsWidth = Settings.Advanced.NibModeBoundsWidth;
            else
                BoundsWidth = Settings.Advanced.FingerModeBoundsWidth;

            SaveSettingsToFile();
        }

        private void FingerModeBoundsWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (!isLoaded) return;
            Settings.Advanced.FingerModeBoundsWidth = (int)e.NewValue;

            if (Settings.Startup.IsEnableNibMode)
                BoundsWidth = Settings.Advanced.NibModeBoundsWidth;
            else
                BoundsWidth = Settings.Advanced.FingerModeBoundsWidth;

            SaveSettingsToFile();
        }

        private void NibModeBoundsWidthThresholdValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isLoaded) return;
            Settings.Advanced.NibModeBoundsWidthThresholdValue = (double)e.NewValue;
            SaveSettingsToFile();
        }

        private void FingerModeBoundsWidthThresholdValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isLoaded) return;
            Settings.Advanced.FingerModeBoundsWidthThresholdValue = (double)e.NewValue;
            SaveSettingsToFile();
        }

        private void NibModeBoundsWidthEraserSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isLoaded) return;
            Settings.Advanced.NibModeBoundsWidthEraserSize = (double)e.NewValue;
            SaveSettingsToFile();
        }

        private void FingerModeBoundsWidthEraserSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isLoaded) return;
            Settings.Advanced.FingerModeBoundsWidthEraserSize = (double)e.NewValue;
            SaveSettingsToFile();
        }

        private void ToggleSwitchIsQuadIR_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Advanced.IsQuadIR = ToggleSwitchIsQuadIR.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchIsLogEnabled_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Advanced.IsLogEnabled = ToggleSwitchIsLogEnabled.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchEnsureFloatingBarVisibleInScreen_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Advanced.IsEnableDPIChangeDetection = ToggleSwitchEnsureFloatingBarVisibleInScreen.IsOn;
            Settings.Advanced.IsEnableResolutionChangeDetection = ToggleSwitchEnsureFloatingBarVisibleInScreen.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchIsDisableCloseWindow_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Advanced.IsDisableCloseWindow = ToggleSwitchIsDisableCloseWindow.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchEnableForceTopMost_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Advanced.EnableForceTopMost = ToggleSwitchEnableForceTopMost.IsOn;
            SaveSettingsToFile();
        }

        #endregion

        #region RandSettings

        private void ToggleSwitchDisplayRandWindowNamesInputBtn_OnToggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.RandSettings.DisplayRandWindowNamesInputBtn = ToggleSwitchDisplayRandWindowNamesInputBtn.IsOn;
            SaveSettingsToFile();
        }

        private void RandWindowOnceCloseLatencySlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.RandSettings.RandWindowOnceCloseLatency = RandWindowOnceCloseLatencySlider.Value;
            SaveSettingsToFile();
        }

        private void RandWindowOnceMaxStudentsSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.RandSettings.RandWindowOnceMaxStudents = (int)RandWindowOnceMaxStudentsSlider.Value;
            SaveSettingsToFile();
        }

        #endregion

        #region SettingsPane

        public void SettingsPane_ScrollChanged(object sender, RoutedEventArgs e) {
            UpdateSettingsIndexSidebarDisplayStatus();
            UpdateSettingsPaneCustomScrollBarStatus();
        }

        public void SettingsPaneScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
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
            Storyboard.SetTargetName(animation,"SettingsPanelScrollViewer");
            sb.Children.Add(animation);
            scrollViewer.ScrollToVerticalOffset(ofs);
            sb.Begin(scrollViewer);
        }

        public void UpdateSettingsPaneCustomScrollBarStatus() {
            var scrollPercentage = SettingsPanelScrollViewer.VerticalOffset /
                                   (SettingsPanelScrollViewer.ExtentHeight - SettingsPanelScrollViewer.ActualHeight);
            // 6 is top and bottom track padding
            var scrollBarTraceTranslateActualHeight =
                SettingsPaneScrollBarTrack.ActualHeight - SettingsPaneScrollBarThumb.ActualHeight - 6;
            SettingsPaneScrollBarThumbTranslateTransform.Y = Math.Round(scrollBarTraceTranslateActualHeight * scrollPercentage);
        }

        public void SettingsPaneBackBtn_MouseEnter(object sender, MouseEventArgs e) {
            var sb = new Storyboard();
            var fadeAnimation = new DoubleAnimation {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.1)
            };
            fadeAnimation.EasingFunction = new CubicBezierEase();
            Storyboard.SetTargetProperty(fadeAnimation, new PropertyPath(UIElement.OpacityProperty));
            sb.Children.Add(fadeAnimation);
            sb.Begin((FrameworkElement)SettingsPaneBackBtnHighlight);
            sb.Completed += (o, args) => {
                SettingsPaneBackBtnHighlight.Opacity = 1;
            };
        }

        public void SettingsPaneBackBtn_MouseLeave(object sender, MouseEventArgs e) {
            var sb = new Storyboard();
            var fadeAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.1)
            };
            fadeAnimation.EasingFunction = new CubicBezierEase();
            Storyboard.SetTargetProperty(fadeAnimation, new PropertyPath(UIElement.OpacityProperty));
            sb.Children.Add(fadeAnimation);
            sb.Begin((FrameworkElement)SettingsPaneBackBtnHighlight);
            sb.Completed += (o, args) => {
                SettingsPaneBackBtnHighlight.Opacity = 0;
            };
        }

        public void ScrollToTrackPositionByMouseEvent(MouseEventArgs e) {
            var position = e.GetPosition(SettingsPaneScrollBarTrack);
            var scrollOffset = (SettingsPanelScrollViewer.ExtentHeight - SettingsPanelScrollViewer.ActualHeight) *
                               (position.Y / (SettingsPaneScrollBarTrack.ActualHeight - 3));
            Trace.WriteLine(scrollOffset);
            SettingsPanelScrollViewer.ScrollToVerticalOffset(scrollOffset);
        }

        public void SettingsPaneScrollBarTrack_MouseDown(object sender, MouseButtonEventArgs e) {
            ScrollToTrackPositionByMouseEvent(e);
        }

        private bool isSettingsPaneScrollBarThumbMouseButtonDown = false;

        public void SettingsPaneScrollBarThumb_MouseDown(object sender, MouseButtonEventArgs e) {
            ScrollToTrackPositionByMouseEvent(e);
            SettingsPaneScrollBarThumb.CaptureMouse();
            isSettingsPaneScrollBarThumbMouseButtonDown = true;
        }

        public void SettingsPaneScrollBarThumb_MouseMove(object sender, MouseEventArgs e) {
            if (isSettingsPaneScrollBarThumbMouseButtonDown) ScrollToTrackPositionByMouseEvent(e);
        }

        public void SettingsPaneScrollBarThumb_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ScrollToTrackPositionByMouseEvent(e);
            SettingsPaneScrollBarThumb.ReleaseMouseCapture();
            isSettingsPaneScrollBarThumbMouseButtonDown = false;
        }

        private GroupBox[] settingsPaneGroupBoxes = new GroupBox[]{};
        private Border[] jumpToBorders = new Border[]{};

        private void InitSettingsPaneControls() {
            settingsPaneGroupBoxes = new GroupBox[] {
                SettingsStartupGroupBox,
                SettingsCanvasGroupBox,
                SettingsGestureGroupBox,
                SettingsInkRecognitionGroupBox,
                SettingsAppearanceGroupBox,
                SettingsPPTGroupBox,
                SettingsAdvancedGroupBox,
                SettingsAutomationGroupBox,
                SettingsStorageGroupBox,
                SettingsSnapshotGroupBox,
                SettingsRandWindowGroupBox,
                SettingsDonationGroupBox,
                SettingsAboutGroupBox
            };
            
            jumpToBorders = new Border[] {
                SettingsStartupJumpToGroupBoxButton,
                SettingsCanvasJumpToGroupBoxButton,
                SettingsGestureJumpToGroupBoxButton,
                SettingsInkRecognitionJumpToGroupBoxButton,
                SettingsAppearanceJumpToGroupBoxButton,
                SettingsPPTJumpToGroupBoxButton,
                SettingsAdvancedJumpToGroupBoxButton,
                SettingsAutomationJumpToGroupBoxButton,
                SettingsStorageJumpToGroupBoxButton,
                SettingsSnapshotJumpToGroupBoxButton,
                SettingsRandWindowJumpToGroupBoxButton,
                SettingsDonationJumpToGroupBoxButton,
                SettingsAboutJumpToGroupBoxButton
            };
        }

        public void UpdateSettingsIndexSidebarDisplayStatus() {

            if (Math.Truncate(SettingsAboutGroupBox.MinHeight) != Math.Truncate(SettingsPanelScrollViewer.ActualHeight)) 
                SettingsAboutGroupBox.MinHeight = SettingsPanelScrollViewer.ActualHeight;

            if (settingsPaneGroupBoxes.Length == 0 || jumpToBorders.Length == 0) InitSettingsPaneControls();

            foreach (var jtb in jumpToBorders) {
                jtb.BorderThickness = new Thickness(0, 0, 0, 0);
                jtb.Background = new SolidColorBrush(Colors.Transparent);
            }

            foreach (var gbx in settingsPaneGroupBoxes) {
                var transform = gbx.TransformToVisual(SettingsPanelScrollViewer);
                var top = transform.Transform(new Point(0, 0));
                var bottom = transform.Transform(new Point(0, gbx.ActualHeight));
                if (settingsPaneGroupBoxes.Length - Array.IndexOf(settingsPaneGroupBoxes, gbx) - 1 <= 4) {
                    if (!(top.Y < SettingsPanelScrollViewer.ActualHeight * 0.9) || !(bottom.Y > 50)) continue;
                    jumpToBorders[Array.IndexOf(settingsPaneGroupBoxes, gbx)].BorderThickness = new Thickness(0, 0, 4, 0);
                    jumpToBorders[Array.IndexOf(settingsPaneGroupBoxes, gbx)].Background = new SolidColorBrush(Color.FromRgb(39, 39, 42));
                    break;
                } else if (top.Y < SettingsPanelScrollViewer.ActualHeight / 2 && bottom.Y > 50) {
                    jumpToBorders[Array.IndexOf(settingsPaneGroupBoxes, gbx)].BorderThickness = new Thickness(0, 0, 4, 0);
                    jumpToBorders[Array.IndexOf(settingsPaneGroupBoxes, gbx)].Background = new SolidColorBrush(Color.FromRgb(39, 39, 42));
                    break;
                }
            }
        }

        private void SettingsPaneScrollViewer_ScrollToAnimated(double offset, int animateMs = 155) {
            var sb = new Storyboard();
            var ofs = SettingsPanelScrollViewer.VerticalOffset;
            var animation = new DoubleAnimation
            {
                From = ofs,
                To = offset,
                Duration = TimeSpan.FromMilliseconds(animateMs)
            };
            animation.EasingFunction = new CubicEase() {
                EasingMode = EasingMode.EaseOut,
            };
            Storyboard.SetTargetProperty(animation, new PropertyPath(ColorPalette.ScrollViewerBehavior.VerticalOffsetProperty));
            Storyboard.SetTargetName(animation,"SettingsPanelScrollViewer");
            sb.Children.Add(animation);
            SettingsPanelScrollViewer.ScrollToVerticalOffset(ofs);
            sb.Begin(SettingsPanelScrollViewer);
        }

        public void SettingsJumpToGroupBox_MouseDown(object sender, MouseButtonEventArgs e) {
            if (settingsPaneGroupBoxes.Length == 0 || jumpToBorders.Length == 0) InitSettingsPaneControls();
            var index = SettingsJumpToGroupBoxButtonsPanel.Children.IndexOf((Border)sender);
            var transform = settingsPaneGroupBoxes[index].TransformToVisual(SettingsPanelScrollViewer);
            var position = transform.Transform(new Point(0, 0));
            SettingsPaneScrollViewer_ScrollToAnimated(SettingsPanelScrollViewer.VerticalOffset + position.Y - 10,
                (int)Math.Truncate(Math.Abs(position.Y - 10) / 8));
        }

        #endregion

        #region Screenshot

        private void ToggleSwitchScreenshotUsingMagnificationAPI_OnToggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Snapshot.ScreenshotUsingMagnificationAPI = ToggleSwitchScreenshotUsingMagnificationAPI.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchCopyScreenshotToClipboard_OnToggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Snapshot.CopyScreenshotToClipboard = ToggleSwitchCopyScreenshotToClipboard.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchHideMainWinWhenScreenshot_OnToggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Snapshot.HideMainWinWhenScreenshot = ToggleSwitchHideMainWinWhenScreenshot.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchAttachInkWhenScreenshot_OnToggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Snapshot.AttachInkWhenScreenshot = ToggleSwitchAttachInkWhenScreenshot.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchOnlySnapshotMaximizeWindow_OnToggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Snapshot.OnlySnapshotMaximizeWindow = ToggleSwitchOnlySnapshotMaximizeWindow.IsOn;
            SaveSettingsToFile();
        }

        private DelayAction screenshotFileNameDelayAction = new DelayAction();

        private void ScreenshotFileName_TextChanged(object sender, TextChangedEventArgs e) {
            if (!isLoaded) return;
            screenshotFileNameDelayAction.DebounceAction(2000,null, () => {
                Settings.Snapshot.ScreenshotFileName = ScreenshotFileName.Text;
            });
            SaveSettingsToFile();
        }

        #endregion

        public static void SaveSettingsToFile() {
            var text = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            try {
                File.WriteAllText(App.RootPath + settingsFileName, text);
            }
            catch { }
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e) {
            e.Handled = true;
        }

        private void HyperlinkSourceToICCRepository_Click(object sender, RoutedEventArgs e) {
            Process.Start("https://gitea.bliemhax.com/kriastans/InkCanvasForClass");
            HideSubPanels();
        }

        private void HyperlinkSourceToICCGithubRepository_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/InkCanvas/InkCanvasForClass");
            HideSubPanels();
        }

        private void HyperlinkSourceToPresentRepository_Click(object sender, RoutedEventArgs e) {
            Process.Start("https://github.com/InkCanvas/Ink-Canvas-Artistry");
            HideSubPanels();
        }

        private void HyperlinkSourceToOringinalRepository_Click(object sender, RoutedEventArgs e) {
            Process.Start("https://github.com/WXRIW/Ink-Canvas");
            HideSubPanels();
        }

    }
}