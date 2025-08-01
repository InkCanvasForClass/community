using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using Ink_Canvas.Helpers;
using Newtonsoft.Json;
using OSVersionExtension;
using Application = System.Windows.Application;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using File = System.IO.File;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using OperatingSystem = OSVersionExtension.OperatingSystem;
using RadioButton = System.Windows.Controls.RadioButton;

namespace Ink_Canvas {
    public partial class MainWindow : Window {
        #region Behavior

        private void ToggleSwitchIsAutoUpdate_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Startup.IsAutoUpdate = ToggleSwitchIsAutoUpdate.IsOn;
            
            // 自动更新关闭时隐藏静默更新选项
            ToggleSwitchIsAutoUpdateWithSilence.Visibility = 
                ToggleSwitchIsAutoUpdate.IsOn ? Visibility.Visible : Visibility.Collapsed;
                
            // 如果关闭了自动更新，同时也关闭静默更新
            if (!ToggleSwitchIsAutoUpdate.IsOn) {
                Settings.Startup.IsAutoUpdateWithSilence = false;
                ToggleSwitchIsAutoUpdateWithSilence.IsOn = false;
            }
            
            // 无论如何，静默更新时间区域的显示都要跟随静默更新设置
            AutoUpdateTimePeriodBlock.Visibility =
                (Settings.Startup.IsAutoUpdateWithSilence && Settings.Startup.IsAutoUpdate) ? 
                Visibility.Visible : Visibility.Collapsed;
                
            SaveSettingsToFile();
        }

        private void ToggleSwitchIsAutoUpdateWithSilence_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Startup.IsAutoUpdateWithSilence = ToggleSwitchIsAutoUpdateWithSilence.IsOn;
            
            // 静默更新的时间设置区域只在静默更新开启时显示
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
                StartAutomaticallyDel("InkCanvas");
                StartAutomaticallyCreate("Ink Canvas Annotation");
            } else {
                StartAutomaticallyDel("InkCanvas");
                StartAutomaticallyDel("Ink Canvas Annotation");
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

            // 使用新的PPT管理器
            if (Settings.PowerPointSettings.PowerPointSupport)
            {
                if (_pptManager == null)
                {
                    InitializePPTManagers();
                }
                StartPPTMonitoring();
            }
            else
            {
                StopPPTMonitoring();
            }
        }

        private void ToggleSwitchShowCanvasAtNewSlideShow_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;

            Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow = ToggleSwitchShowCanvasAtNewSlideShow.IsOn;
            SaveSettingsToFile();
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
            if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
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
            SaveSettingsToFile();
            LoadSettings();
        }

        public void ComboBoxFloatingBarImg_SelectionChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Appearance.FloatingBarImg = ComboBoxFloatingBarImg.SelectedIndex;
            UpdateFloatingBarIcon();
            SaveSettingsToFile();
        }
        
        public void UpdateFloatingBarIcon()
        {
            int index = Settings.Appearance.FloatingBarImg;
            
            if (index == 0) {
                FloatingbarHeadIconImg.Source =
                    new BitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/icc.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(0.5);
            } else if (index == 1) {
                FloatingbarHeadIconImg.Source =
                    new BitmapImage(
                        new Uri("pack://application:,,,/Resources/Icons-png/icc-transparent-dark-small.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(1.2);
            } else if (index == 2) {
                FloatingbarHeadIconImg.Source =
                    new BitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/kuandoujiyanhuaji.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(2, 2, 2, 1.5);
            } else if (index == 3) {
                FloatingbarHeadIconImg.Source =
                    new BitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/kuanshounvhuaji.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(2, 2, 2, 1.5);
            } else if (index == 4) {
                FloatingbarHeadIconImg.Source =
                    new BitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/kuanciya.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(2, 2, 2, 1.5);
            } else if (index == 5) {
                FloatingbarHeadIconImg.Source =
                    new BitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/kuanneikuhuaji.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(2, 2, 2, 1.5);
            } else if (index == 6) {
                FloatingbarHeadIconImg.Source =
                    new BitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/kuandogeyuanliangwo.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(2, 2, 2, 1.5);
            } else if (index == 7) {
                FloatingbarHeadIconImg.Source =
                    new BitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/tiebahuaji.png"));
                FloatingbarHeadIconImg.Margin = new Thickness(2, 2, 2, 1);
            } else if (index >= 8 && index - 8 < Settings.Appearance.CustomFloatingBarImgs.Count) {
                // 使用自定义图标
                var customIcon = Settings.Appearance.CustomFloatingBarImgs[index - 8];
                try {
                    FloatingbarHeadIconImg.Source = new BitmapImage(new Uri(customIcon.FilePath));
                    FloatingbarHeadIconImg.Margin = new Thickness(2);
                } catch {
                    // 如果加载失败，使用默认图标
                    FloatingbarHeadIconImg.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/Icons-png/icc.png"));
                    FloatingbarHeadIconImg.Margin = new Thickness(0.5);
                }
            }
        }
        
        public void UpdateCustomIconsInComboBox()
        {
            // 保留前8个内置图标选项
            while (ComboBoxFloatingBarImg.Items.Count > 8)
            {
                ComboBoxFloatingBarImg.Items.RemoveAt(ComboBoxFloatingBarImg.Items.Count - 1);
            }
            
            // 添加自定义图标选项
            foreach (var customIcon in Settings.Appearance.CustomFloatingBarImgs)
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = customIcon.Name;
                item.FontFamily = new FontFamily("Microsoft YaHei UI");
                ComboBoxFloatingBarImg.Items.Add(item);
            }
        }
        
        private void ButtonAddCustomIcon_Click(object sender, RoutedEventArgs e)
        {
            AddCustomIconWindow dialog = new AddCustomIconWindow(this);
            dialog.Owner = this;
            dialog.ShowDialog();
            
            if (dialog.IsSuccess)
            {
                // 自动选中新添加的图标
                ComboBoxFloatingBarImg.SelectedIndex = ComboBoxFloatingBarImg.Items.Count - 1;
            }
        }
        
        private void ButtonManageCustomIcons_Click(object sender, RoutedEventArgs e)
        {
            CustomIconWindow dialog = new CustomIconWindow(this);
            dialog.Owner = this;
            dialog.ShowDialog();
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
            LoadSettings();
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
            LoadSettings();
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
            // 更新PPT UI管理器设置
            if (_pptUIManager != null)
            {
                _pptUIManager.ShowPPTButton = Settings.PowerPointSettings.ShowPPTButton;
                _pptUIManager.UpdateNavigationPanelsVisibility();
            }
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
            // 更新PPT UI管理器设置
            if (_pptUIManager != null && BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
            {
                _pptUIManager.PPTButtonsDisplayOption = Settings.PowerPointSettings.PPTButtonsDisplayOption;
                _pptUIManager.UpdateNavigationPanelsVisibility();
            }
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
            // 更新PPT UI管理器设置
            if (_pptUIManager != null && BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
            {
                _pptUIManager.PPTButtonsDisplayOption = Settings.PowerPointSettings.PPTButtonsDisplayOption;
                _pptUIManager.UpdateNavigationPanelsVisibility();
            }
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
            // 更新PPT UI管理器设置
            if (_pptUIManager != null && BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
            {
                _pptUIManager.PPTButtonsDisplayOption = Settings.PowerPointSettings.PPTButtonsDisplayOption;
                _pptUIManager.UpdateNavigationPanelsVisibility();
            }
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
            // 更新PPT UI管理器设置
            if (_pptUIManager != null && BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
            {
                _pptUIManager.PPTButtonsDisplayOption = Settings.PowerPointSettings.PPTButtonsDisplayOption;
                _pptUIManager.UpdateNavigationPanelsVisibility();
            }
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
            // 更新PPT UI管理器设置
            if (_pptUIManager != null && BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
            {
                _pptUIManager.PPTSButtonsOption = Settings.PowerPointSettings.PPTSButtonsOption;
                _pptUIManager.UpdateNavigationButtonStyles();
            }
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
            // 更新PPT UI管理器设置
            if (_pptUIManager != null && BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
            {
                _pptUIManager.PPTSButtonsOption = Settings.PowerPointSettings.PPTSButtonsOption;
                _pptUIManager.UpdateNavigationButtonStyles();
            }
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
            // 更新PPT UI管理器设置
            if (_pptUIManager != null && BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
            {
                _pptUIManager.PPTSButtonsOption = Settings.PowerPointSettings.PPTSButtonsOption;
                _pptUIManager.UpdateNavigationButtonStyles();
            }
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
            // 更新PPT UI管理器设置
            if (_pptUIManager != null && BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
            {
                _pptUIManager.PPTBButtonsOption = Settings.PowerPointSettings.PPTBButtonsOption;
                _pptUIManager.UpdateNavigationButtonStyles();
            }
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
            UpdatePPTUIManagerSettings();
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
            UpdatePPTUIManagerSettings();
            UpdatePPTBtnPreview();
        }

        private void PPTButtonLeftPositionValueSlider_ValueChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.PowerPointSettings.PPTLSButtonPosition = (int)PPTButtonLeftPositionValueSlider.Value;
            UpdatePPTBtnSlidersStatus();
            UpdatePPTUIManagerSettings();
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
            UpdatePPTUIManagerSettings();
            SliderDelayAction.DebounceAction(2000,null, SaveSettingsToFile);
            UpdatePPTBtnPreview();
        }

        /// <summary>
        /// 更新PPT UI管理器设置的通用方法
        /// </summary>
        private void UpdatePPTUIManagerSettings()
        {
            if (_pptUIManager != null && BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
            {
                _pptUIManager.PPTButtonsDisplayOption = Settings.PowerPointSettings.PPTButtonsDisplayOption;
                _pptUIManager.PPTSButtonsOption = Settings.PowerPointSettings.PPTSButtonsOption;
                _pptUIManager.PPTBButtonsOption = Settings.PowerPointSettings.PPTBButtonsOption;
                _pptUIManager.PPTLSButtonPosition = Settings.PowerPointSettings.PPTLSButtonPosition;
                _pptUIManager.PPTRSButtonPosition = Settings.PowerPointSettings.PPTRSButtonPosition;
                _pptUIManager.UpdateNavigationPanelsVisibility();
                _pptUIManager.UpdateNavigationButtonStyles();
            }
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

        private void ToggleSwitchEnablePressureTouchMode_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;

            Settings.Canvas.EnablePressureTouchMode = ToggleSwitchEnablePressureTouchMode.IsOn;
            
            // 如果启用了压感触屏模式，则自动关闭屏蔽压感
            if (Settings.Canvas.EnablePressureTouchMode && Settings.Canvas.DisablePressure) {
                Settings.Canvas.DisablePressure = false;
                ToggleSwitchDisablePressure.IsOn = false;
            }
            
            SaveSettingsToFile();
        }

        private void ToggleSwitchDisablePressure_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;

            Settings.Canvas.DisablePressure = ToggleSwitchDisablePressure.IsOn;
            
            // 如果启用了屏蔽压感，则自动关闭压感触屏模式
            if (Settings.Canvas.DisablePressure && Settings.Canvas.EnablePressureTouchMode) {
                Settings.Canvas.EnablePressureTouchMode = false;
                ToggleSwitchEnablePressureTouchMode.IsOn = false;
            }
            
            SaveSettingsToFile();
        }

        private void ToggleSwitchAutoStraightenLine_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            
            Settings.Canvas.AutoStraightenLine = ToggleSwitchAutoStraightenLine.IsOn;
            SaveSettingsToFile();
        }
        
        private void AutoStraightenLineThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (!isLoaded) return;
            
            Settings.Canvas.AutoStraightenLineThreshold = (int)e.NewValue;
            SaveSettingsToFile();
        }
        
        private void ToggleSwitchLineEndpointSnapping_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            
            Settings.Canvas.LineEndpointSnapping = ToggleSwitchLineEndpointSnapping.IsOn;
            SaveSettingsToFile();
        }
        
        private void LineEndpointSnappingThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (!isLoaded) return;
            
            Settings.Canvas.LineEndpointSnappingThreshold = (int)e.NewValue;
            SaveSettingsToFile();
        }
        
        private void LineStraightenSensitivitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (!isLoaded) return;
            
            // 记录旧值用于调试
            double oldValue = Settings.InkToShape.LineStraightenSensitivity;
            
            // 确保灵敏度值被正确保存到设置中
            Settings.InkToShape.LineStraightenSensitivity = e.NewValue;
            
            // 输出调试信息，观察值变化
            Debug.WriteLine($"LineStraightenSensitivity changed: {oldValue} -> {e.NewValue}");
            
            // 立即保存设置到文件，确保设置不会丢失
            SaveSettingsToFile();
        }
        
        private void ToggleSwitchHighPrecisionLineStraighten_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            
            Settings.Canvas.HighPrecisionLineStraighten = ToggleSwitchHighPrecisionLineStraighten.IsOn;
            Debug.WriteLine($"HighPrecisionLineStraighten changed: {Settings.Canvas.HighPrecisionLineStraighten}");
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

            // 使用新的高级橡皮擦形状应用方法
            ApplyAdvancedEraserShape();

            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint) {
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
            }
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

            // 使用新的高级橡皮擦形状应用方法
            ApplyAdvancedEraserShape();

            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint) {
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
            }
            SaveSettingsToFile();
        }

        private void SwitchToCircleEraser(object sender, MouseButtonEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.EraserShapeType = 0;
            SaveSettingsToFile();
            CheckEraserTypeTab();

            // 使用新的高级橡皮擦形状应用方法
            ApplyAdvancedEraserShape();

            // 确保当前处于橡皮擦模式时能立即看到效果
            inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
        }

        private void SwitchToRectangleEraser(object sender, MouseButtonEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.EraserShapeType = 1;
            SaveSettingsToFile();
            CheckEraserTypeTab();

            // 使用新的高级橡皮擦形状应用方法
            ApplyAdvancedEraserShape();

            // 确保当前处于橡皮擦模式时能立即看到效果
            inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
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
                SettingsPPTInkingAndAutoFoldExplictBorder.Visibility = Visibility.Visible;
                SettingsShowCanvasAtNewSlideShowStackPanel.Opacity = 0.5;
                SettingsShowCanvasAtNewSlideShowStackPanel.IsHitTestVisible = false;
            } else {
                SettingsPPTInkingAndAutoFoldExplictBorder.Visibility = Visibility.Collapsed;
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

        private void ToggleSwitchAutoKillIDT_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.IsAutoKillIDT = ToggleSwitchAutoKillIDT.IsOn;
            SaveSettingsToFile();
            if (Settings.Automation.IsAutoKillEasiNote || Settings.Automation.IsAutoKillPptService ||
                Settings.Automation.IsAutoKillHiteAnnotation || Settings.Automation.IsAutoKillInkCanvas
                || Settings.Automation.IsAutoKillICA || Settings.Automation.IsAutoKillIDT || Settings.Automation.IsAutoKillVComYouJiao
                || Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation)
                timerKillProcess.Start();
            else
                timerKillProcess.Stop();
        }
        
        private void ToggleSwitchAutoEnterAnnotationModeWhenExitFoldMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.IsAutoEnterAnnotationModeWhenExitFoldMode = ToggleSwitchAutoEnterAnnotationModeWhenExitFoldMode.IsOn;
            SaveSettingsToFile();
        }

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
            
            // 启用原来的FitToCurve时自动禁用高级贝塞尔平滑
            if (ToggleSwitchFitToCurve.IsOn)
            {
                ToggleSwitchAdvancedBezierSmoothing.IsOn = false;
                Settings.Canvas.UseAdvancedBezierSmoothing = false;
            }
            
            SaveSettingsToFile();
        }

        private void ToggleSwitchAdvancedBezierSmoothing_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.UseAdvancedBezierSmoothing = ToggleSwitchAdvancedBezierSmoothing.IsOn;

            // 启用高级贝塞尔平滑时自动禁用原来的FitToCurve
            if (ToggleSwitchAdvancedBezierSmoothing.IsOn)
            {
                ToggleSwitchFitToCurve.IsOn = false;
                Settings.Canvas.FitToCurve = false;
                drawingAttributes.FitToCurve = false;
            }

            // 更新墨迹平滑管理器配置
            _inkSmoothingManager?.UpdateConfig();

            SaveSettingsToFile();
        }

        // 注释掉这些方法，因为对应的UI控件还没有在XAML中定义
        /*
        private void ToggleSwitchAsyncInkSmoothing_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.UseAsyncInkSmoothing = ToggleSwitchAsyncInkSmoothing.IsOn;
            _inkSmoothingManager?.UpdateConfig();
            SaveSettingsToFile();
        }

        private void ToggleSwitchHardwareAcceleration_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.UseHardwareAcceleration = ToggleSwitchHardwareAcceleration.IsOn;
            _inkSmoothingManager?.UpdateConfig();
            SaveSettingsToFile();
        }

        private void ComboBoxInkSmoothingQuality_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.InkSmoothingQuality = ComboBoxInkSmoothingQuality.SelectedIndex;
            _inkSmoothingManager?.UpdateConfig();
            SaveSettingsToFile();
        }

        private void SliderMaxConcurrentTasks_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (!isLoaded) return;
            Settings.Canvas.MaxConcurrentSmoothingTasks = (int)SliderMaxConcurrentTasks.Value;
            _inkSmoothingManager?.UpdateConfig();
            SaveSettingsToFile();
        }

        private void ButtonApplyRecommendedSettings_Click(object sender, RoutedEventArgs e) {
            // 应用推荐的性能设置
            Helpers.InkSmoothingManager.ApplyRecommendedSettings();
            LoadSettings(false);
            _inkSmoothingManager?.UpdateConfig();
            SaveSettingsToFile();

            ShowNotification("已应用推荐的性能设置");
        }

        private void ButtonShowPerformanceStats_Click(object sender, RoutedEventArgs e) {
            if (_inkSmoothingManager != null)
            {
                var stats = _inkSmoothingManager.GetPerformanceStats();
                ShowNotification($"性能统计: {stats}");
            }
        }
        */
        
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

        private void AutoSavedStrokesLocationTextBox_TextChanged(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.AutoSavedStrokesLocation = AutoSavedStrokesLocation.Text;
            SaveSettingsToFile();
        }

        private void AutoSavedStrokesLocationButton_Click(object sender, RoutedEventArgs e) {
            var folderBrowser = new FolderBrowserDialog();
            folderBrowser.ShowDialog();
            if (folderBrowser.SelectedPath.Length > 0) AutoSavedStrokesLocation.Text = folderBrowser.SelectedPath;
            SaveSettingsToFile();
        }

        private void SetAutoSavedStrokesLocationToDiskDButton_Click(object sender, RoutedEventArgs e) {
            AutoSavedStrokesLocation.Text = @"D:\Ink Canvas";
            SaveSettingsToFile();
        }

        private void SetAutoSavedStrokesLocationToDocumentFolderButton_Click(object sender, RoutedEventArgs e) {
            AutoSavedStrokesLocation.Text =
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\Ink Canvas";
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
        
        private void ToggleSwitchSaveFullPageStrokes_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsSaveFullPageStrokes = ToggleSwitchSaveFullPageStrokes.IsOn;
            SaveSettingsToFile();
        }

        #endregion

        #region Gesture

        private void ToggleSwitchEnableFingerGestureSlideShowControl_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.PowerPointSettings.IsEnableFingerGestureSlideShowControl =
                ToggleSwitchEnableFingerGestureSlideShowControl.IsOn;
            SaveSettingsToFile();
        }

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
                    // 保存当前编辑模式和绘图工具状态
                    InkCanvasEditingMode currentEditingMode = inkCanvas.EditingMode;
                    int currentDrawingShapeMode = drawingShapeMode;
                    bool currentForceEraser = forceEraser;
                    
                    inkCanvas.StylusDown += MainWindow_StylusDown;
                    inkCanvas.StylusMove += MainWindow_StylusMove;
                    inkCanvas.StylusUp += MainWindow_StylusUp;
                    inkCanvas.TouchDown += MainWindow_TouchDown;
                    inkCanvas.TouchDown -= Main_Grid_TouchDown;
                    
                    // 先设为None再设回原来的模式，避免可能的事件冲突
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    inkCanvas.Children.Clear();
                    isInMultiTouchMode = true;
                    
                    // 恢复到之前的编辑状态
                    inkCanvas.EditingMode = currentEditingMode;
                    drawingShapeMode = currentDrawingShapeMode;
                    forceEraser = currentForceEraser;
                }
            } else {
                if (isInMultiTouchMode) {
                    // 保存当前编辑模式和绘图工具状态
                    InkCanvasEditingMode currentEditingMode = inkCanvas.EditingMode;
                    int currentDrawingShapeMode = drawingShapeMode;
                    bool currentForceEraser = forceEraser;

                    inkCanvas.StylusDown -= MainWindow_StylusDown;
                    inkCanvas.StylusMove -= MainWindow_StylusMove;
                    inkCanvas.StylusUp -= MainWindow_StylusUp;
                    inkCanvas.TouchDown -= MainWindow_TouchDown;
                    inkCanvas.TouchDown += Main_Grid_TouchDown;

                    // 先设为None再设回原来的模式，避免可能的事件冲突
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    // 保存非笔画元素（如图片）
                    var preservedElements = PreserveNonStrokeElements();
                    inkCanvas.Children.Clear();
                    // 恢复非笔画元素
                    RestoreNonStrokeElements(preservedElements);
                    isInMultiTouchMode = false;

                    // 恢复到之前的编辑状态
                    inkCanvas.EditingMode = currentEditingMode;
                    drawingShapeMode = currentDrawingShapeMode;
                    forceEraser = currentForceEraser;
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
            Settings.Advanced.IsSecondConfirmWhenShutdownApp = false;
            Settings.Advanced.IsEnableEdgeGestureUtil = false;
            Settings.Advanced.EdgeGestureUtilOnlyAffectBlackboardMode = false;
            Settings.Advanced.IsEnableFullScreenHelper = false;
            Settings.Advanced.IsEnableAvoidFullScreenHelper = false;    
            Settings.Advanced.IsEnableForceFullScreen = false;
            Settings.Advanced.IsEnableDPIChangeDetection = false;
            Settings.Advanced.IsEnableResolutionChangeDetection = false;

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
            Settings.Automation.IsAutoKillIDT = false;
            Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation = false;
            Settings.Automation.IsSaveScreenshotsInDateFolders = false;
            Settings.Automation.IsAutoSaveStrokesAtScreenshot = true;
            Settings.Automation.IsAutoSaveStrokesAtClear = true;
            Settings.Automation.IsAutoClearWhenExitingWritingMode = false;
            Settings.Automation.MinimumAutomationStrokeNumber = 0;
            Settings.Automation.AutoDelSavedFiles = AutoDelSavedFilesDays;
            Settings.Automation.AutoDelSavedFilesDaysThreshold = AutoDelSavedFilesDaysThreshold;

            //Settings.PowerPointSettings.IsShowPPTNavigation = true;
            //Settings.PowerPointSettings.IsShowBottomPPTNavigationPanel = false;
            //Settings.PowerPointSettings.IsShowSidePPTNavigationPanel = true;
            Settings.PowerPointSettings.PowerPointSupport = true;
            Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow = false;
            Settings.PowerPointSettings.IsNoClearStrokeOnSelectWhenInPowerPoint = true;
            Settings.PowerPointSettings.IsShowStrokeOnSelectInPowerPoint = false;
            Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint = true;
            Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint = true;
            Settings.PowerPointSettings.IsNotifyPreviousPage = false;
            Settings.PowerPointSettings.IsNotifyHiddenPage = false;
            Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode = false;
            Settings.PowerPointSettings.IsEnableFingerGestureSlideShowControl = false;
            Settings.PowerPointSettings.IsSupportWPS = false;

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
            Settings.Canvas.UseAdvancedBezierSmoothing = true;
            Settings.Canvas.EnablePressureTouchMode = false;
            Settings.Canvas.DisablePressure = false;
            Settings.Canvas.AutoStraightenLine = true;
            Settings.Canvas.AutoStraightenLineThreshold = 80;
            Settings.Canvas.LineEndpointSnapping = true;
            Settings.Canvas.LineEndpointSnappingThreshold = 15;
            Settings.Canvas.UsingWhiteboard = false;
            Settings.Canvas.HyperbolaAsymptoteOption = 0;

            Settings.Gesture.AutoSwitchTwoFingerGesture = true;
            Settings.Gesture.IsEnableTwoFingerTranslate = true;
            Settings.Gesture.IsEnableTwoFingerZoom = false;
            Settings.Gesture.IsEnableTwoFingerRotation = false;
            Settings.Gesture.IsEnableTwoFingerRotationOnSelection = false;

            Settings.InkToShape.IsInkToShapeEnabled = true;
            Settings.InkToShape.IsInkToShapeNoFakePressureRectangle = false;
            Settings.InkToShape.IsInkToShapeNoFakePressureTriangle = false;
            Settings.InkToShape.IsInkToShapeTriangle = true;
            Settings.InkToShape.IsInkToShapeRectangle = true;
            Settings.InkToShape.IsInkToShapeRounded = true;


            Settings.Startup.IsEnableNibMode = false;
            Settings.Startup.IsAutoUpdate = true;
            Settings.Startup.IsAutoUpdateWithSilence = true;
            Settings.Startup.AutoUpdateWithSilenceStartTime = "06:00";
            Settings.Startup.AutoUpdateWithSilenceEndTime = "22:00";
            Settings.Startup.IsFoldAtStartup = false;
        }

        private void BtnResetToSuggestion_Click(object sender, RoutedEventArgs e) {
            try {
                isLoaded = false;
                SetSettingsToRecommendation();
                SaveSettingsToFile();
                LoadSettings();
                isLoaded = true;

                ToggleSwitchRunAtStartup.IsOn = true;
            }
            catch { }

            ShowNotification("设置已重置为默认推荐设置~");
        }

        private async void SpecialVersionResetToSuggestion_Click() {
            await Task.Delay(1000);
            try {
                isLoaded = false;
                SetSettingsToRecommendation();
                Settings.Automation.AutoDelSavedFiles = true;
                Settings.Automation.AutoDelSavedFilesDaysThreshold = 15;
                SetAutoSavedStrokesLocationToDiskDButton_Click(null, null);
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

        private void ToggleSwitchIsEnableAvoidFullScreenHelper_OnToggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Advanced.IsEnableAvoidFullScreenHelper = ToggleSwitchIsEnableAvoidFullScreenHelper.IsOn;
            SaveSettingsToFile();
            if (ToggleSwitchIsEnableAvoidFullScreenHelper.IsOn)
            {
                AvoidFullScreenHelper.StartAvoidFullScreen(this);
            }
            else
            {
                AvoidFullScreenHelper.StopAvoidFullScreen(this);
            }
        }

        private void ToggleSwitchIsEnableEdgeGestureUtil_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Advanced.IsEnableEdgeGestureUtil = ToggleSwitchIsEnableEdgeGestureUtil.IsOn;
            if (OSVersion.GetOperatingSystem() >= OperatingSystem.Windows10) EdgeGestureUtil.DisableEdgeGestures(new WindowInteropHelper(this).Handle, ToggleSwitchIsEnableEdgeGestureUtil.IsOn);
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

        private void ToggleSwitchEraserBindTouchMultiplier_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Advanced.EraserBindTouchMultiplier = ToggleSwitchEraserBindTouchMultiplier.IsOn;
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
        
        private void ToggleSwitchIsSaveLogByDate_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Advanced.IsSaveLogByDate = ToggleSwitchIsSaveLogByDate.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchIsSecondConfimeWhenShutdownApp_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Advanced.IsSecondConfirmWhenShutdownApp = ToggleSwitchIsSecondConfimeWhenShutdownApp.IsOn;
            SaveSettingsToFile();
        }
        
        private void ToggleSwitchIsAutoBackupBeforeUpdate_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Advanced.IsAutoBackupBeforeUpdate = ToggleSwitchIsAutoBackupBeforeUpdate.IsOn;
            SaveSettingsToFile();
        }
        
        private void BtnManualBackup_Click(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            
            try {
                // 确保Backups目录存在
                string backupDir = Path.Combine(App.RootPath, "Backups");
                if (!Directory.Exists(backupDir)) {
                    Directory.CreateDirectory(backupDir);
                    LogHelper.WriteLogToFile($"创建备份目录: {backupDir}");
                }
                
                // 创建备份文件名（使用当前日期时间）
                string backupFileName = $"Settings_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string backupPath = Path.Combine(backupDir, backupFileName);
                
                // 序列化当前设置并保存到备份文件
                string settingsJson = JsonConvert.SerializeObject(Settings, Formatting.Indented);
                File.WriteAllText(backupPath, settingsJson);
                
                LogHelper.WriteLogToFile($"成功创建设置备份: {backupPath}");
                MessageBox.Show($"设置已成功备份到:\n{backupPath}", "备份成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile($"创建设置备份时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"创建备份失败: {ex.Message}", "备份失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BtnRestoreBackup_Click(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            
            try {
                // 确保Backups目录存在
                string backupDir = Path.Combine(App.RootPath, "Backups");
                if (!Directory.Exists(backupDir)) {
                    Directory.CreateDirectory(backupDir);
                    LogHelper.WriteLogToFile($"创建备份目录: {backupDir}");
                    MessageBox.Show("没有找到备份文件，请先创建备份", "还原失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 打开文件选择对话框
                OpenFileDialog dlg = new OpenFileDialog();
                dlg.InitialDirectory = backupDir;
                dlg.Filter = "设置备份文件|Settings_Backup_*.json|所有JSON文件|*.json";
                dlg.Title = "选择要还原的备份文件";
                
                if (dlg.ShowDialog() == true) {
                    // 读取备份文件
                    string backupJson = File.ReadAllText(dlg.FileName);
                    
                    // 反序列化备份数据
                    Settings backupSettings = JsonConvert.DeserializeObject<Settings>(backupJson);
                    
                    if (backupSettings != null) {
                        // 确认是否要还原
                        if (MessageBox.Show("确定要还原选择的备份文件吗？当前设置将被覆盖。", "确认还原", 
                                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) {
                            
                            // 备份当前设置，以防出错
                            string currentSettingsJson = JsonConvert.SerializeObject(Settings, Formatting.Indented);
                            string tempBackupPath = Path.Combine(backupDir, $"Settings_Before_Restore_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                            File.WriteAllText(tempBackupPath, currentSettingsJson);
                            
                            // 还原设置
                            Settings = backupSettings;
                            
                            // 保存还原后的设置到文件
                            SaveSettingsToFile();
                            
                            // 重新加载设置到UI
                            LoadSettings();
                            
                            LogHelper.WriteLogToFile($"成功从备份还原设置: {dlg.FileName}");
                            MessageBox.Show("设置已成功还原，部分设置可能需要重启软件后生效。", "还原成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    else {
                        MessageBox.Show("无法解析备份文件，文件可能已损坏", "还原失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile($"还原设置备份时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"还原备份失败: {ex.Message}", "还原失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void ToggleSwitchShowRandomAndSingleDraw_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;

            // 获取开关状态并保存到设置中
            bool isToggled = ToggleSwitchShowRandomAndSingleDraw.IsOn;
            Settings.RandSettings.ShowRandomAndSingleDraw = isToggled;

            // 更新UI显示
            RandomDrawPanel.Visibility = isToggled ? Visibility.Visible : Visibility.Collapsed;
            SingleDrawPanel.Visibility = isToggled ? Visibility.Visible : Visibility.Collapsed;

            // 保存设置到文件
            SaveSettingsToFile();
        }
        
        private void ToggleSwitchDirectCallCiRand_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            
            // 获取开关状态并保存到设置中
            Settings.RandSettings.DirectCallCiRand = ToggleSwitchDirectCallCiRand.IsOn;
            
            // 保存设置到文件
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

        private void HyperlinkSourceToPresentRepository_Click(object sender, RoutedEventArgs e) {
            Process.Start("https://bgithub.xyz/ChangSakura/Ink-Canvas");
            HideSubPanels();
        }

        private void HyperlinkSourceToOringinalRepository_Click(object sender, RoutedEventArgs e) {
            Process.Start("https://bgithub.xyz/WXRIW/Ink-Canvas");
            HideSubPanels();
        }

        private async void UpdateChannelSelector_Checked(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            var radioButton = sender as RadioButton;
            if (radioButton != null) {
                string channel = radioButton.Tag.ToString();
                UpdateChannel newChannel = channel == "Beta" ? UpdateChannel.Beta : UpdateChannel.Release;
                
                // 如果通道没有变化，不需要执行更新检查
                if (Settings.Startup.UpdateChannel == newChannel) {
                    return;
                }
                
                Settings.Startup.UpdateChannel = newChannel;
                LogHelper.WriteLogToFile($"Settings | Update channel changed to {Settings.Startup.UpdateChannel}");
                SaveSettingsToFile();
                
                // 如果启用了自动更新，立即执行完整的检查更新操作
                if (Settings.Startup.IsAutoUpdate) {
                    LogHelper.WriteLogToFile($"AutoUpdate | Channel changed to {newChannel}, performing immediate update check");
                    
                    // 执行完整的更新检查
                    await Task.Run(async () => {
                        try {
                            // 调用主窗口的AutoUpdate方法，它会自动清除之前的更新状态并使用新通道重新检查
                            Dispatcher.Invoke(() => {
                                AutoUpdate();
                            });
                        }
                        catch (Exception ex) {
                            LogHelper.WriteLogToFile($"AutoUpdate | Error during channel switch update check: {ex.Message}", LogHelper.LogType.Error);
                        }
                    });
                }
                else {
                    LogHelper.WriteLogToFile($"AutoUpdate | Channel changed to {newChannel}, but auto-update is disabled");
                }
            }
        }
        
        private async void FixVersionButton_Click(object sender, RoutedEventArgs e) {
            // 显示确认对话框
            var confirm = MessageBox.Show(
                "此操作将下载当前选择通道的最新版本并安装，软件将自动关闭并更新。\n\n确定要执行版本修复吗？",
                "版本修复确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
                
            if (confirm == MessageBoxResult.Yes) {
                // 禁用按钮，避免重复点击
                FixVersionButton.IsEnabled = false;
                FixVersionButton.Content = "正在修复...";
                
                try {
                    // 执行版本修复
                    bool result = await AutoUpdateHelper.FixVersion(Settings.Startup.UpdateChannel);
                    
                    if (!result) {
                        MessageBox.Show(
                            "版本修复失败，可能是网络问题或当前已是最新版本。",
                            "修复失败",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                            
                        // 恢复按钮状态
                        FixVersionButton.IsEnabled = true;
                        FixVersionButton.Content = "版本修复";
                    }
                    // 成功则会自动关闭应用程序并安装
                }
                catch (Exception ex) {
                    LogHelper.WriteLogToFile($"Error in FixVersionButton_Click: {ex.Message}", LogHelper.LogType.Error);
                    MessageBox.Show(
                        $"版本修复过程中发生错误: {ex.Message}",
                        "修复错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                        
                    // 恢复按钮状态
                    FixVersionButton.IsEnabled = true;
                    FixVersionButton.Content = "版本修复";
                }
            }
        }

        // 自定义点名背景相关方法
        public void UpdatePickNameBackgroundsInComboBox()
        {
            // 清除现有的自定义背景选项
            if (ComboBoxPickNameBackground != null) 
            {
                // 保留第一个默认选项
                while (ComboBoxPickNameBackground.Items.Count > 1)
                {
                    ComboBoxPickNameBackground.Items.RemoveAt(ComboBoxPickNameBackground.Items.Count - 1);
                }
                
                // 添加自定义背景选项
                foreach (var background in Settings.RandSettings.CustomPickNameBackgrounds)
                {
                    ComboBoxItem item = new ComboBoxItem();
                    item.Content = background.Name;
                    item.FontFamily = new FontFamily("Microsoft YaHei UI");
                    ComboBoxPickNameBackground.Items.Add(item);
                }
            }
        }

        public void UpdatePickNameBackgroundDisplay()
        {
            // 此方法主要用于在外部窗口更改背景后更新UI
            if (ComboBoxPickNameBackground != null)
            {
                ComboBoxPickNameBackground.SelectedIndex = Settings.RandSettings.SelectedBackgroundIndex;
            }
        }
        
        private void ComboBoxPickNameBackground_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded) return;
            
            Settings.RandSettings.SelectedBackgroundIndex = ComboBoxPickNameBackground.SelectedIndex;
            SaveSettingsToFile();
        }
        
        private void ButtonAddCustomBackground_Click(object sender, RoutedEventArgs e)
        {
            AddPickNameBackgroundWindow dialog = new AddPickNameBackgroundWindow(this);
            dialog.Owner = this;
            dialog.ShowDialog();
            
            if (dialog.IsSuccess)
            {
                // 自动选中新添加的背景
                ComboBoxPickNameBackground.SelectedIndex = ComboBoxPickNameBackground.Items.Count - 1;
            }
        }
        
        private void ButtonManageBackgrounds_Click(object sender, RoutedEventArgs e)
        {
            ManagePickNameBackgroundsWindow dialog = new ManagePickNameBackgroundsWindow(this);
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        private void ToggleSwitchEnableWppProcessKill_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.PowerPointSettings.EnableWppProcessKill = ToggleSwitchEnableWppProcessKill.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchClearCanvasAlsoClearImages_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Canvas.ClearCanvasAlsoClearImages = ToggleSwitchClearCanvasAlsoClearImages.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchCompressPicturesUploaded_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Canvas.IsCompressPicturesUploaded = ToggleSwitchCompressPicturesUploaded.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchAutoFoldAfterPPTSlideShow_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoFoldAfterPPTSlideShow = ToggleSwitchAutoFoldAfterPPTSlideShow.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchAlwaysGoToFirstPageOnReenter_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.PowerPointSettings.IsAlwaysGoToFirstPageOnReenter = ToggleSwitchAlwaysGoToFirstPageOnReenter.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchAutoEnterAnnotationAfterKillHite_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Automation.IsAutoEnterAnnotationAfterKillHite = ToggleSwitchAutoEnterAnnotationAfterKillHite.IsOn;
            SaveSettingsToFile();
        }

        private void ToggleSwitchEnablePalmEraser_Toggled(object sender, RoutedEventArgs e) {
            if (!isLoaded) return;
            Settings.Canvas.EnablePalmEraser = ToggleSwitchEnablePalmEraser.IsOn;
            SaveSettingsToFile();
        }


    }
}
