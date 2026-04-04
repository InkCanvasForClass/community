using Ink_Canvas.Helpers;
using OSVersionExtension;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

namespace Ink_Canvas
{
    public partial class FeedbackWindow : Window
    {
        private string _appVersion = "";
        private string _updateChannel = "";
        private string _osVersion = "";
        private string _netVersion = "";
        private string _touchSupport = "";
        private string _deviceId = "";
        private string _telemetryId = "";
        private string _pptLinkageSettings = "";
        private string _inkRecognitionSettings = "";
        private string _inkRecognitionEngine = "";

        public FeedbackWindow()
        {
            InitializeComponent();
            LoadInformation();
            UpdateTextBlocks();
            CheckTelemetryIdAvailability();
        }

        private void UpdateTextBlocks()
        {
            TextAppVersionInfo.Text = _appVersion;
            TextSystemInfo.Text = $"{_osVersion} | {_netVersion} | 触控:{_touchSupport}";
            TextDeviceInfo.Text = $"设备ID: {_deviceId}";
            TextTelemetryInfo.Text = $"遥测ID: {_telemetryId}";
        }

        private void LoadInformation()
        {
            try
            {
                var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
                _appVersion = $"v{assemblyVersion}";
            }
            catch (Exception ex)
            {
                _appVersion = "未知";
                System.Diagnostics.Debug.WriteLine($"获取软件版本失败: {ex.Message}");
            }

            try
            {
                if (MainWindow.Settings?.Startup != null)
                {
                    _updateChannel = MainWindow.Settings.Startup.UpdateChannel.ToString();
                }
            }
            catch (Exception ex)
            {
                _updateChannel = "未知";
                System.Diagnostics.Debug.WriteLine($"获取更新通道失败: {ex.Message}");
            }

            try
            {
                _osVersion = $"{OSVersion.GetOperatingSystem()} {OSVersion.GetOSVersion().Version}";
            }
            catch (Exception ex)
            {
                _osVersion = "未知";
                System.Diagnostics.Debug.WriteLine($"获取系统版本失败: {ex.Message}");
            }

            try
            {
                _netVersion = RuntimeInformation.FrameworkDescription;
            }
            catch (Exception ex)
            {
                _netVersion = "未知";
                System.Diagnostics.Debug.WriteLine($"获取.NET版本失败: {ex.Message}");
            }

            try
            {
                var support = Ink_Canvas.Windows.SettingsViews.AboutPanel.TouchTabletDetectHelper.IsTouchEnabled();
                var touchcount = Ink_Canvas.Windows.SettingsViews.AboutPanel.TouchTabletDetectHelper.GetTouchTabletDevices().Count;
                if (support)
                {
                    if (touchcount > 0)
                    {
                        _touchSupport = $"支持({touchcount}个设备)";
                    }
                    else
                    {
                        _touchSupport = "支持";
                    }
                }
                else
                {
                    _touchSupport = "不支持";
                }
            }
            catch (Exception ex)
            {
                _touchSupport = "未知";
                System.Diagnostics.Debug.WriteLine($"获取触控支持失败: {ex.Message}");
            }

            try
            {
                _deviceId = DeviceIdentifier.GetDeviceId();
            }
            catch (Exception ex)
            {
                _deviceId = "获取失败";
                System.Diagnostics.Debug.WriteLine($"获取设备ID失败: {ex.Message}");
            }

            try
            {
                _telemetryId = GetTelemetryId();
            }
            catch (Exception ex)
            {
                _telemetryId = "获取失败";
                System.Diagnostics.Debug.WriteLine($"获取遥测ID失败: {ex.Message}");
            }

            try
            {
                if (MainWindow.Settings?.PowerPointSettings != null)
                {
                    _pptLinkageSettings = $"启用PPT联动: {MainWindow.Settings.PowerPointSettings.PowerPointSupport}\n";
                    _pptLinkageSettings += $"WPS支持: {MainWindow.Settings.PowerPointSettings.IsSupportWPS}\n";
                    _pptLinkageSettings += $"MSO支持: {MainWindow.Settings.PowerPointSettings.PowerPointSupport}\n";
                    _pptLinkageSettings += $"Rot联动: {MainWindow.Settings.PowerPointSettings.UseRotPptLink}\n";
                }
                else
                {
                    _pptLinkageSettings = "未配置PPT联动设置";
                }
            }
            catch (Exception ex)
            {
                _pptLinkageSettings = "获取PPT联动设置失败";
                System.Diagnostics.Debug.WriteLine($"获取PPT联动设置失败: {ex.Message}");
            }

            try
            {
                if (MainWindow.Settings?.InkToShape != null)
                {
                    _inkRecognitionSettings = $"启用墨迹识别: {MainWindow.Settings.InkToShape.IsInkToShapeEnabled}\n";
                }
                else
                {
                    _inkRecognitionSettings = "未配置墨迹识别设置";
                }
            }
            catch (Exception ex)
            {
                _inkRecognitionSettings = "获取墨迹识别设置失败";
                System.Diagnostics.Debug.WriteLine($"获取墨迹识别设置失败: {ex.Message}");
            }

            try
            {
                if (MainWindow.Settings?.InkToShape != null)
                {
                    var engineMode = Helpers.ShapeRecognitionRouter.FromSettingsInt(MainWindow.Settings.InkToShape.ShapeRecognitionEngine);
                    bool useWinRT = Helpers.ShapeRecognitionRouter.ResolveUseWinRt(engineMode);
                    _inkRecognitionEngine = useWinRT ? "WinRT" : "IACore";
                    _inkRecognitionSettings += $"识别引擎: {_inkRecognitionEngine}\n";
                }
                else
                {
                    _inkRecognitionEngine = "未配置";
                    _inkRecognitionSettings += $"识别引擎: {_inkRecognitionEngine}\n";
                }
            }
            catch (Exception ex)
            {
                _inkRecognitionEngine = "获取失败";
                _inkRecognitionSettings += $"识别引擎: {_inkRecognitionEngine}\n";
                System.Diagnostics.Debug.WriteLine($"获取墨迹识别引擎失败: {ex.Message}");
            }
        }

        private string GetTelemetryId()
        {
            try
            {
                var telemetryIdFilePath = System.IO.Path.Combine(App.RootPath, "telemetry_id.dat");
                if (System.IO.File.Exists(telemetryIdFilePath))
                {
                    return System.IO.File.ReadAllText(telemetryIdFilePath).Trim();
                }
                return "";
            }
            catch
            {
                return "";
            }
        }

        private void CheckTelemetryIdAvailability()
        {
            try
            {
                bool hasTelemetryId = CheckTelemetryIdExists();
                if (!hasTelemetryId)
                {
                    CardFanceId.Visibility = Visibility.Visible;
                    CheckFanceId.IsChecked = false;
                    CheckFanceId.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查遥测ID可用性失败: {ex.Message}");
                CardFanceId.Visibility = Visibility.Visible;
                CheckFanceId.IsChecked = false;
                CheckFanceId.IsEnabled = false;
            }
        }

        private bool CheckTelemetryIdExists()
        {
            try
            {
                var telemetryIdFilePath = System.IO.Path.Combine(App.RootPath, "telemetry_id.dat");
                return System.IO.File.Exists(telemetryIdFilePath);
            }
            catch
            {
                return false;
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ButtonBack_Click(object sender, RoutedEventArgs e)
        {
            if (Page3.Visibility == Visibility.Visible)
            {
                Page3.Visibility = Visibility.Collapsed;
                Page2.Visibility = Visibility.Visible;
                ButtonCancel.Visibility = Visibility.Collapsed;
                ButtonNext.Visibility = Visibility.Collapsed;
                ButtonBack.Visibility = Visibility.Visible;
                ButtonConfirm.Visibility = Visibility.Visible;
            }
            else if (Page2.Visibility == Visibility.Visible)
            {
                Page2.Visibility = Visibility.Collapsed;
                Page1.Visibility = Visibility.Visible;
                ButtonCancel.Visibility = Visibility.Visible;
                ButtonNext.Visibility = Visibility.Visible;
                ButtonBack.Visibility = Visibility.Collapsed;
                ButtonConfirm.Visibility = Visibility.Collapsed;
            }
        }

        private void ButtonNext_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string versionInfo = "";
                string systemInfo = "";

                if (CheckAppVersion.IsChecked == true || CheckUpdateChannel.IsChecked == true)
                {
                    if (CheckAppVersion.IsChecked == true)
                    {
                        versionInfo += _appVersion;
                    }
                    if (CheckUpdateChannel.IsChecked == true)
                    {
                        if (!string.IsNullOrEmpty(versionInfo))
                        {
                            versionInfo += " ";
                        }
                        versionInfo += $"({_updateChannel})";
                    }
                }

                if (CheckOSVersion.IsChecked == true || CheckNetVersion.IsChecked == true || CheckTouchSupport.IsChecked == true)
                {
                    if (CheckOSVersion.IsChecked == true)
                    {
                        systemInfo += _osVersion;
                    }
                    if (CheckNetVersion.IsChecked == true)
                    {
                        if (!string.IsNullOrEmpty(systemInfo))
                        {
                            systemInfo += " | ";
                        }
                        systemInfo += _netVersion;
                    }
                    if (CheckTouchSupport.IsChecked == true)
                    {
                        if (!string.IsNullOrEmpty(systemInfo))
                        {
                            systemInfo += " | ";
                        }
                        systemInfo += $"触控:{_touchSupport}";
                    }
                }

                TextAppVersionInfo.Text = versionInfo;
                TextSystemInfo.Text = systemInfo;

                if (CheckDeviceId.IsChecked == true)
                {
                    TextDeviceInfo.Text = $"设备ID: {_deviceId}";
                }
                else
                {
                    TextDeviceInfo.Text = "设备ID: (不包含)";
                }

                if (CheckFanceId.IsChecked == true)
                {
                    TextTelemetryInfo.Text = $"遥测ID: {_telemetryId}";
                    TextTelemetryInfo.Visibility = Visibility.Visible;
                }
                else
                {
                    TextTelemetryInfo.Text = "遥测ID: (不包含)";
                    TextTelemetryInfo.Visibility = Visibility.Visible;
                }

                // Display software configuration settings in the dedicated card
                if (CheckPPTLinkage.IsChecked == true || CheckInkRecognition.IsChecked == true)
                {
                    CardConfiguration.Visibility = Visibility.Visible;
                    TextConfigurationInfo.Text = "";
                    if (CheckPPTLinkage.IsChecked == true)
                    {
                        TextConfigurationInfo.Text += $"PPT联动设置:\n{_pptLinkageSettings}\n";
                    }
                    if (CheckInkRecognition.IsChecked == true)
                    {
                        TextConfigurationInfo.Text += $"墨迹识别设置:\n{_inkRecognitionSettings}\n";
                    }
                }
                else
                {
                    CardConfiguration.Visibility = Visibility.Collapsed;
                }

                Page1.Visibility = Visibility.Collapsed;
                Page2.Visibility = Visibility.Visible;
                ButtonCancel.Visibility = Visibility.Collapsed;
                ButtonNext.Visibility = Visibility.Collapsed;
                ButtonBack.Visibility = Visibility.Visible;
                ButtonConfirm.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"切换到第二页失败: {ex.Message}");
            }
        }

        private void ButtonConfirm_Click(object sender, RoutedEventArgs e)
        {
            GenerateMarkdownTemplate();
            Page2.Visibility = Visibility.Collapsed;
            Page3.Visibility = Visibility.Visible;
            ButtonCancel.Visibility = Visibility.Visible;
            ButtonNext.Visibility = Visibility.Collapsed;
            ButtonBack.Visibility = Visibility.Visible;
            ButtonConfirm.Visibility = Visibility.Collapsed;
        }

        private void GenerateMarkdownTemplate()
        {
            string template = "## 环境信息\n";
            
            if (CheckAppVersion.IsChecked == true)
            {
                template += $"- 软件版本: {_appVersion}\n";
            }
            if (CheckUpdateChannel.IsChecked == true)
            {
                template += $"- 更新通道: {_updateChannel}\n";
            }
            if (CheckOSVersion.IsChecked == true)
            {
                template += $"- 操作系统: {_osVersion}\n";
            }
            if (CheckNetVersion.IsChecked == true)
            {
                template += $"- .NET 版本: {_netVersion}\n";
            }
            if (CheckTouchSupport.IsChecked == true)
            {
                template += $"- 触控支持: {_touchSupport}\n";
            }
            
            template += "\n## 设备信息\n";
            if (CheckDeviceId.IsChecked == true)
            {
                template += $"- 设备ID: {_deviceId}\n";
            }
            if (CheckFanceId.IsChecked == true && !string.IsNullOrEmpty(_telemetryId))
            {
                template += $"- 遥测ID: {_telemetryId}\n";
            }
            
            if (CheckPPTLinkage.IsChecked == true || CheckInkRecognition.IsChecked == true)
            {
                template += "\n## 软件配置\n";
                if (CheckPPTLinkage.IsChecked == true)
                {
                    template += "### PPT联动设置\n";
                    template += _pptLinkageSettings + "\n";
                }
                if (CheckInkRecognition.IsChecked == true)
                {
                    template += "### 墨迹识别设置\n";
                    template += _inkRecognitionSettings + "\n";
                }
            }
            
            TextBoxMarkdownTemplate.Text = template;
        }

        private void BtnOpenGitHubIssue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string versionInfo = "";
                string systemInfo = "";

                if (CheckAppVersion.IsChecked == true || CheckUpdateChannel.IsChecked == true)
                {
                    if (CheckAppVersion.IsChecked == true)
                    {
                        versionInfo += _appVersion;
                    }
                    if (CheckUpdateChannel.IsChecked == true)
                    {
                        if (!string.IsNullOrEmpty(versionInfo))
                        {
                            versionInfo += " ";
                        }
                        versionInfo += $"({_updateChannel})";
                    }
                }

                if (CheckOSVersion.IsChecked == true || CheckNetVersion.IsChecked == true || CheckTouchSupport.IsChecked == true)
                {
                    if (CheckOSVersion.IsChecked == true)
                    {
                        systemInfo += _osVersion;
                    }
                    if (CheckNetVersion.IsChecked == true)
                    {
                        if (!string.IsNullOrEmpty(systemInfo))
                        {
                            systemInfo += " | ";
                        }
                        systemInfo += _netVersion;
                    }
                    if (CheckTouchSupport.IsChecked == true)
                    {
                        if (!string.IsNullOrEmpty(systemInfo))
                        {
                            systemInfo += " | ";
                        }
                        systemInfo += $"触控:{_touchSupport}";
                    }
                }

                string url = "https://github.com/InkCanvasForClass/community/issues/new?template=01-bug_report.yml";

                if (!string.IsNullOrEmpty(versionInfo))
                {
                    url += $"&version={Uri.EscapeDataString(versionInfo)}";
                }

                if (!string.IsNullOrEmpty(systemInfo))
                {
                    url += $"&os={Uri.EscapeDataString(systemInfo)}";
                }

                string extraInfo = "";
                if (CheckDeviceId.IsChecked == true)
                {
                    extraInfo += $"设备ID: {_deviceId}\n";
                }

                if (CheckFanceId.IsChecked == true)
                {
                    extraInfo += $"遥测ID: {_telemetryId}\n";
                }

                if (CheckPPTLinkage.IsChecked == true)
                {
                    extraInfo += "\nPPT联动设置:\n";
                    extraInfo += _pptLinkageSettings;
                }

                if (CheckInkRecognition.IsChecked == true)
                {
                    extraInfo += "\n墨迹识别设置:\n";
                    extraInfo += _inkRecognitionSettings;
                }

                if (!string.IsNullOrEmpty(extraInfo))
                {
                    url += $"&extra={Uri.EscapeDataString(extraInfo)}";
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });

                Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开反馈链接失败: {ex.Message}");
            }
        }

        private void CardCopyIssueUrl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string versionInfo = "";
                string systemInfo = "";

                if (CheckAppVersion.IsChecked == true || CheckUpdateChannel.IsChecked == true)
                {
                    if (CheckAppVersion.IsChecked == true)
                    {
                        versionInfo += _appVersion;
                    }
                    if (CheckUpdateChannel.IsChecked == true)
                    {
                        if (!string.IsNullOrEmpty(versionInfo))
                        {
                            versionInfo += " ";
                        }
                        versionInfo += $"({_updateChannel})";
                    }
                }

                if (CheckOSVersion.IsChecked == true || CheckNetVersion.IsChecked == true || CheckTouchSupport.IsChecked == true)
                {
                    if (CheckOSVersion.IsChecked == true)
                    {
                        systemInfo += _osVersion;
                    }
                    if (CheckNetVersion.IsChecked == true)
                    {
                        if (!string.IsNullOrEmpty(systemInfo))
                        {
                            systemInfo += " | ";
                        }
                        systemInfo += _netVersion;
                    }
                    if (CheckTouchSupport.IsChecked == true)
                    {
                        if (!string.IsNullOrEmpty(systemInfo))
                        {
                            systemInfo += " | ";
                        }
                        systemInfo += $"触控:{_touchSupport}";
                    }
                }

                string url = "https://github.com/InkCanvasForClass/community/issues/new?template=01-bug_report.yml";

                if (!string.IsNullOrEmpty(versionInfo))
                {
                    url += $"&version={Uri.EscapeDataString(versionInfo)}";
                }

                if (!string.IsNullOrEmpty(systemInfo))
                {
                    url += $"&os={Uri.EscapeDataString(systemInfo)}";
                }

                string extraInfo = "";
                if (CheckDeviceId.IsChecked == true)
                {
                    extraInfo += $"设备ID: {_deviceId}\n";
                }

                if (CheckFanceId.IsChecked == true)
                {
                    extraInfo += $"遥测ID: {_telemetryId}\n";
                }

                if (CheckPPTLinkage.IsChecked == true)
                {
                    extraInfo += "\nPPT联动设置:\n";
                    extraInfo += _pptLinkageSettings;
                }

                if (CheckInkRecognition.IsChecked == true)
                {
                    extraInfo += "\n墨迹识别设置:\n";
                    extraInfo += _inkRecognitionSettings;
                }

                if (!string.IsNullOrEmpty(extraInfo))
                {
                    url += $"&extra={Uri.EscapeDataString(extraInfo)}";
                }

                Clipboard.SetText(url);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"复制反馈链接失败: {ex.Message}");
            }
        }

        private void BtnCopyMarkdown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TextBoxMarkdownTemplate.Text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"复制Markdown模板失败: {ex.Message}");
            }
        }
    }
}
