using Ink_Canvas;
using iNKORE.UI.WPF.Helpers;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Application = System.Windows.Application;

namespace Ink_Canvas.Windows.SettingsViews
{
    /// <summary>
    /// SnapshotPanel.xaml 的交互逻辑
    /// </summary>
    public partial class SnapshotPanel : System.Windows.Controls.UserControl
    {
        private bool _isLoaded = false;

        public SnapshotPanel()
        {
            InitializeComponent();
            Loaded += SnapshotPanel_Loaded;
        }

        private void SnapshotPanel_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            // 添加触摸支持
            EnableTouchSupport();
            // 应用主题
            ApplyTheme();
            _isLoaded = true;
        }

        /// <summary>
        /// 为面板中的所有交互控件启用触摸支持
        /// </summary>
        private void EnableTouchSupport()
        {
            try
            {
                // 延迟执行，确保所有控件都已加载
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MainWindowSettingsHelper.EnableTouchSupportForControls(this);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SnapshotPanel 启用触摸支持时出错: {ex.Message}");
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

        /// <summary>
        /// 加载设置到UI
        /// </summary>
        public void LoadSettings()
        {
            if (MainWindow.Settings == null) return;

            _isLoaded = false;

            try
            {
                // 清屏时自动截图
                if (MainWindow.Settings.Automation != null)
                {
                    SetToggleSwitchState(FindToggleSwitch("ToggleSwitchAutoSaveStrokesAtClear"), MainWindow.Settings.Automation.IsAutoSaveStrokesAtClear);
                    SetToggleSwitchState(FindToggleSwitch("ToggleSwitchSaveScreenshotsInDateFolders"), MainWindow.Settings.Automation.IsSaveScreenshotsInDateFolders);
                    SetToggleSwitchState(FindToggleSwitch("ToggleSwitchAutoSaveStrokesAtScreenshot"), MainWindow.Settings.Automation.IsAutoSaveStrokesAtScreenshot);
                    SetToggleSwitchState(FindToggleSwitch("ToggleSwitchAutoDelSavedFiles"), MainWindow.Settings.Automation.AutoDelSavedFiles);

                    // 自动截图最小墨迹量
                    if (SideControlMinimumAutomationSlider != null)
                    {
                        double minValue = MainWindow.Settings.Automation.MinimumAutomationStrokeNumber;
                        if (minValue == 0) minValue = 1.0;
                        SideControlMinimumAutomationSlider.Value = minValue;
                        if (SideControlMinimumAutomationText != null)
                        {
                            SideControlMinimumAutomationText.Text = minValue.ToString("F2");
                        }
                    }

                    // 保存路径
                    if (AutoSavedStrokesLocation != null)
                    {
                        AutoSavedStrokesLocation.Text = MainWindow.Settings.Automation.AutoSavedStrokesLocation;
                    }

                    // 自动删除保存时长
                    if (ComboBoxAutoDelSavedFilesDaysThreshold != null)
                    {
                        int days = MainWindow.Settings.Automation.AutoDelSavedFilesDaysThreshold;
                        int selectedIndex = 4; // 默认15天
                        switch (days)
                        {
                            case 1: selectedIndex = 0; break;
                            case 3: selectedIndex = 1; break;
                            case 5: selectedIndex = 2; break;
                            case 7: selectedIndex = 3; break;
                            case 15: selectedIndex = 4; break;
                            case 30: selectedIndex = 5; break;
                            case 60: selectedIndex = 6; break;
                            case 100: selectedIndex = 7; break;
                            case 365: selectedIndex = 8; break;
                        }
                        ComboBoxAutoDelSavedFilesDaysThreshold.SelectedIndex = selectedIndex;
                    }

                    if (AutoDelIntervalPanel != null)
                    {
                        AutoDelIntervalPanel.Visibility = MainWindow.Settings.Automation.AutoDelSavedFiles ? Visibility.Visible : Visibility.Collapsed;
                    }
                }

                // 自动幻灯片截屏
                if (MainWindow.Settings.PowerPointSettings != null)
                {
                    SetToggleSwitchState(FindToggleSwitch("ToggleSwitchAutoSaveScreenShotInPowerPoint"), MainWindow.Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载截图设置时出错: {ex.Message}");
            }

            _isLoaded = true;
        }

        /// <summary>
        /// 查找ToggleSwitch控件
        /// </summary>
        private Border FindToggleSwitch(string name)
        {
            return this.FindDescendantByName(name) as Border;
        }

        /// <summary>
        /// 设置ToggleSwitch状态
        /// </summary>
        private void SetToggleSwitchState(Border toggleSwitch, bool isOn)
        {
            if (toggleSwitch == null) return;
            toggleSwitch.Background = isOn 
                ? new SolidColorBrush(Color.FromRgb(53, 132, 228)) 
                : (ThemeHelper.IsDarkTheme ? ThemeHelper.GetButtonBackgroundBrush() : new SolidColorBrush(Color.FromRgb(225, 225, 225)));
            var innerBorder = toggleSwitch.Child as Border;
            if (innerBorder != null)
            {
                innerBorder.HorizontalAlignment = isOn ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left;
                innerBorder.Background = new SolidColorBrush(Colors.White);
            }
        }

        private bool GetCurrentSettingValue(string tag)
        {
            if (MainWindow.Settings == null) return false;

            try
            {
                switch (tag)
                {
                    case "AutoSaveStrokesAtClear":
                        return MainWindow.Settings.Automation?.IsAutoSaveStrokesAtClear ?? false;

                    case "SaveScreenshotsInDateFolders":
                        return MainWindow.Settings.Automation?.IsSaveScreenshotsInDateFolders ?? false;

                    case "AutoSaveStrokesAtScreenshot":
                        return MainWindow.Settings.Automation?.IsAutoSaveStrokesAtScreenshot ?? false;

                    case "AutoSaveScreenShotInPowerPoint":
                        return MainWindow.Settings.PowerPointSettings?.IsAutoSaveScreenShotInPowerPoint ?? false;

                    case "AutoDelSavedFiles":
                        return MainWindow.Settings.Automation?.AutoDelSavedFiles ?? false;

                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// ToggleSwitch点击事件处理
        /// </summary>
        private void ToggleSwitch_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            var border = sender as Border;
            if (border == null) return;

            string tag = border.Tag?.ToString();
            if (string.IsNullOrEmpty(tag)) return;

            bool currentState = GetCurrentSettingValue(tag);
            bool newState = !currentState;
            
            SetToggleSwitchState(border, newState);

            switch (tag)
            {
                case "AutoSaveStrokesAtClear":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchAutoSaveStrokesAtClear", newState);
                    break;

                case "SaveScreenshotsInDateFolders":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchSaveScreenshotsInDateFolders", newState);
                    break;

                case "AutoSaveStrokesAtScreenshot":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchAutoSaveStrokesAtScreenshot", newState);
                    break;

                case "AutoSaveScreenShotInPowerPoint":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchAutoSaveScreenShotInPowerPoint", newState);
                    break;

                case "AutoDelSavedFiles":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchAutoDelSavedFiles", newState);
                    // 更新UI状态
                    if (AutoDelIntervalPanel != null)
                    {
                        AutoDelIntervalPanel.Visibility = newState ? Visibility.Visible : Visibility.Collapsed;
                    }
                    break;
            }
        }

        /// <summary>
        /// Slider值变化事件处理
        /// </summary>
        private void SideControlMinimumAutomationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            if (SideControlMinimumAutomationSlider != null && SideControlMinimumAutomationText != null)
            {
                double value = SideControlMinimumAutomationSlider.Value;
                SideControlMinimumAutomationText.Text = value.ToString("F2");
                // 调用 MainWindow 中的方法
                MainWindowSettingsHelper.InvokeSliderValueChanged("SideControlMinimumAutomationSlider", value);
            }
        }

        /// <summary>
        /// 保存路径文本框变化事件处理
        /// </summary>
        private void AutoSavedStrokesLocation_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (AutoSavedStrokesLocation != null)
            {
                // 调用 MainWindow 中的方法
                MainWindowSettingsHelper.InvokeTextBoxTextChanged("AutoSavedStrokesLocation", AutoSavedStrokesLocation.Text);
            }
        }

        /// <summary>
        /// 浏览按钮点击事件处理
        /// </summary>
        private void AutoSavedStrokesLocationButton_Click(object sender, RoutedEventArgs e)
        {
            // 调用 MainWindow 中的方法
            MainWindowSettingsHelper.InvokeMainWindowMethod("AutoSavedStrokesLocationButton_Click", sender, e);
            // 同步新面板中的文本框
            if (AutoSavedStrokesLocation != null)
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    var textBox = mainWindow.FindName("AutoSavedStrokesLocation") as System.Windows.Controls.TextBox;
                    if (textBox != null)
                    {
                        AutoSavedStrokesLocation.Text = textBox.Text;
                    }
                }
            }
        }

        /// <summary>
        /// 设置保存到 D:\Ink Canvas 按钮点击事件处理
        /// </summary>
        private void SetAutoSavedStrokesLocationToDiskDButton_Click(object sender, RoutedEventArgs e)
        {
            // 调用 MainWindow 中的方法
            MainWindowSettingsHelper.InvokeMainWindowMethod("SetAutoSavedStrokesLocationToDiskDButton_Click", sender, e);
            // 同步新面板中的文本框
            if (AutoSavedStrokesLocation != null)
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    var textBox = mainWindow.FindName("AutoSavedStrokesLocation") as System.Windows.Controls.TextBox;
                    if (textBox != null)
                    {
                        AutoSavedStrokesLocation.Text = textBox.Text;
                    }
                }
            }
        }

        /// <summary>
        /// 设置保存到文档按钮点击事件处理
        /// </summary>
        private void SetAutoSavedStrokesLocationToDocumentFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (AutoSavedStrokesLocation != null)
            {
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                AutoSavedStrokesLocation.Text = Path.Combine(documentsPath, "Ink Canvas");
            }
        }

        /// <summary>
        /// ComboBox选择变化事件处理
        /// </summary>
        private void ComboBoxAutoDelSavedFilesDaysThreshold_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (ComboBoxAutoDelSavedFilesDaysThreshold?.SelectedItem is ComboBoxItem selectedItem)
            {
                // 调用 MainWindow 中的方法
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    var comboBox = mainWindow.FindName("ComboBoxAutoDelSavedFilesDaysThreshold") as System.Windows.Controls.ComboBox;
                    if (comboBox != null)
                    {
                        // 找到对应的选项并设置
                        string tag = selectedItem.Tag?.ToString();
                        if (!string.IsNullOrEmpty(tag) && tag.StartsWith("AutoDelSavedFilesDaysThreshold_"))
                        {
                            string daysStr = tag.Replace("AutoDelSavedFilesDaysThreshold_", "");
                            foreach (ComboBoxItem item in comboBox.Items)
                            {
                                if (item.Tag?.ToString() == tag || item.Content?.ToString() == daysStr + "天")
                                {
                                    comboBox.SelectedItem = item;
                                    MainWindowSettingsHelper.InvokeComboBoxSelectionChanged("ComboBoxAutoDelSavedFilesDaysThreshold", item);
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        // 如果找不到控件，直接更新设置
                        string tag = selectedItem.Tag?.ToString();
                        if (!string.IsNullOrEmpty(tag) && tag.StartsWith("AutoDelSavedFilesDaysThreshold_"))
                        {
                            string daysStr = tag.Replace("AutoDelSavedFilesDaysThreshold_", "");
                            if (int.TryParse(daysStr, out int days))
                            {
                                MainWindowSettingsHelper.UpdateSettingDirectly(() =>
                                {
                                    if (MainWindow.Settings.Automation != null)
                                    {
                                        MainWindow.Settings.Automation.AutoDelSavedFilesDaysThreshold = days;
                                    }
                                }, "ComboBoxAutoDelSavedFilesDaysThreshold");
                            }
                        }
                    }
                }
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
                if (_isLoaded)
                {
                    LoadSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SnapshotPanel 应用主题时出错: {ex.Message}");
            }
        }
    }
}

