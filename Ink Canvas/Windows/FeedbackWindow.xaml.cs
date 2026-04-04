using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.FeedbackPages;
using iNKORE.UI.WPF.Modern.Media.Animation;
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

        private FeedbackPage1 _page1;
        private FeedbackPage2 _page2;
        private FeedbackPage3 _page3;

        private NavigationTransitionInfo _transitionInfo = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight };

        public FeedbackWindow()
        {
            InitializeComponent();
            _page1 = new FeedbackPage1();
            _page2 = new FeedbackPage2();
            _page3 = new FeedbackPage3();

            _page3.BtnOpenGitHubIssueClick += BtnOpenGitHubIssue_Click;
            _page3.CardCopyIssueUrlClick += CardCopyIssueUrl_Click;
            _page3.BtnCopyMarkdownClick += BtnCopyMarkdown_Click;

            ContentFrame.Navigated += ContentFrame_Navigated;
            LoadInformation();
            CheckTelemetryIdAvailability();
            ContentFrame.Navigate(_page1);
        }

        private void ContentFrame_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            UpdateButtonVisibility();
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
                    _page1.CheckFanceId.IsChecked = false;
                    _page1.CheckFanceId.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查遥测ID可用性失败: {ex.Message}");
                _page1.CheckFanceId.IsChecked = false;
                _page1.CheckFanceId.IsEnabled = false;
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
            if (ContentFrame.BackStackDepth > 0)
            {
                ContentFrame.GoBack();
                UpdateButtonVisibility();
            }
        }

        private void ButtonNext_Click(object sender, RoutedEventArgs e)
        {
            UpdatePage2Info();
            ContentFrame.Navigate(_page2, null, _transitionInfo);
            UpdateButtonVisibility();
        }

        private void ButtonConfirm_Click(object sender, RoutedEventArgs e)
        {
            GenerateMarkdownTemplate();
            ContentFrame.Navigate(_page3, null, _transitionInfo);
            UpdateButtonVisibility();
        }

        private void UpdateButtonVisibility()
        {
            if (ContentFrame.Content == _page1)
            {
                ButtonCancel.Visibility = Visibility.Visible;
                ButtonNext.Visibility = Visibility.Visible;
                ButtonBack.Visibility = Visibility.Collapsed;
                ButtonConfirm.Visibility = Visibility.Collapsed;
            }
            else if (ContentFrame.Content == _page2)
            {
                ButtonCancel.Visibility = Visibility.Collapsed;
                ButtonNext.Visibility = Visibility.Collapsed;
                ButtonBack.Visibility = Visibility.Visible;
                ButtonConfirm.Visibility = Visibility.Visible;
            }
            else if (ContentFrame.Content == _page3)
            {
                ButtonCancel.Visibility = Visibility.Collapsed;
                ButtonNext.Visibility = Visibility.Collapsed;
                ButtonBack.Visibility = Visibility.Visible;
                ButtonConfirm.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdatePage2Info()
        {
            try
            {
                string versionInfo = "";
                string systemInfo = "";

                if (_page1.CheckAppVersion.IsChecked == true || _page1.CheckUpdateChannel.IsChecked == true)
                {
                    if (_page1.CheckAppVersion.IsChecked == true)
                    {
                        versionInfo += _appVersion;
                    }
                    if (_page1.CheckUpdateChannel.IsChecked == true)
                    {
                        if (!string.IsNullOrEmpty(versionInfo))
                        {
                            versionInfo += " ";
                        }
                        versionInfo += $"({_updateChannel})";
                    }
                }

                if (_page1.CheckOSVersion.IsChecked == true || _page1.CheckNetVersion.IsChecked == true || _page1.CheckTouchSupport.IsChecked == true)
                {
                    if (_page1.CheckOSVersion.IsChecked == true)
                    {
                        systemInfo += _osVersion;
                    }
                    if (_page1.CheckNetVersion.IsChecked == true)
                    {
                        if (!string.IsNullOrEmpty(systemInfo))
                        {
                            systemInfo += " | ";
                        }
                        systemInfo += _netVersion;
                    }
                    if (_page1.CheckTouchSupport.IsChecked == true)
                    {
                        if (!string.IsNullOrEmpty(systemInfo))
                        {
                            systemInfo += " | ";
                        }
                        systemInfo += $"触控:{_touchSupport}";
                    }
                }

                _page2.TextAppVersionInfo.Text = versionInfo;
                _page2.TextSystemInfo.Text = systemInfo;

                if (_page1.CheckDeviceId.IsChecked == true)
                {
                    _page2.TextDeviceInfo.Text = $"设备ID: {_deviceId}";
                }
                else
                {
                    _page2.TextDeviceInfo.Text = "设备ID: (不包含)";
                }

                if (_page1.CheckFanceId.IsChecked == true)
                {
                    _page2.TextTelemetryInfo.Text = $"遥测ID: {_telemetryId}";
                    _page2.TextTelemetryInfo.Visibility = Visibility.Visible;
                }
                else
                {
                    _page2.TextTelemetryInfo.Text = "遥测ID: (不包含)";
                    _page2.TextTelemetryInfo.Visibility = Visibility.Visible;
                }

                if (_page1.CheckPPTLinkage.IsChecked == true || _page1.CheckInkRecognition.IsChecked == true)
                {
                    _page2.CardConfiguration.Visibility = Visibility.Visible;
                    _page2.TextConfigurationInfo.Text = "";
                    if (_page1.CheckPPTLinkage.IsChecked == true)
                    {
                        _page2.TextConfigurationInfo.Text += $"PPT联动设置:\n{_pptLinkageSettings}\n";
                    }
                    if (_page1.CheckInkRecognition.IsChecked == true)
                    {
                        _page2.TextConfigurationInfo.Text += $"墨迹识别设置:\n{_inkRecognitionSettings}\n";
                    }
                }
                else
                {
                    _page2.CardConfiguration.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新第二页信息失败: {ex.Message}");
            }
        }

        private void GenerateMarkdownTemplate()
        {
            string template = "## 环境信息\n";

            if (_page1.CheckAppVersion.IsChecked == true)
            {
                template += $"- 软件版本: {_appVersion}\n";
            }
            if (_page1.CheckUpdateChannel.IsChecked == true)
            {
                template += $"- 更新通道: {_updateChannel}\n";
            }
            if (_page1.CheckOSVersion.IsChecked == true)
            {
                template += $"- 操作系统: {_osVersion}\n";
            }
            if (_page1.CheckNetVersion.IsChecked == true)
            {
                template += $"- .NET 版本: {_netVersion}\n";
            }
            if (_page1.CheckTouchSupport.IsChecked == true)
            {
                template += $"- 触控支持: {_touchSupport}\n";
            }

            template += "\n## 设备信息\n";
            if (_page1.CheckDeviceId.IsChecked == true)
            {
                template += $"- 设备ID: {_deviceId}\n";
            }
            if (_page1.CheckFanceId.IsChecked == true && !string.IsNullOrEmpty(_telemetryId))
            {
                template += $"- 遥测ID: {_telemetryId}\n";
            }

            if (_page1.CheckPPTLinkage.IsChecked == true || _page1.CheckInkRecognition.IsChecked == true)
            {
                template += "\n## 软件配置\n";
                if (_page1.CheckPPTLinkage.IsChecked == true)
                {
                    template += "### PPT联动设置\n";
                    template += _pptLinkageSettings + "\n";
                }
                if (_page1.CheckInkRecognition.IsChecked == true)
                {
                    template += "### 墨迹识别设置\n";
                    template += _inkRecognitionSettings + "\n";
                }
            }

            _page3.TextBoxMarkdownTemplate.Text = template;
        }

        private (string versionInfo, string systemInfo, string extraInfo) BuildFeedbackInfo()
        {
            string versionInfo = "";
            string systemInfo = "";
            string extraInfo = "";

            if (_page1.CheckAppVersion.IsChecked == true || _page1.CheckUpdateChannel.IsChecked == true)
            {
                if (_page1.CheckAppVersion.IsChecked == true)
                {
                    versionInfo += _appVersion;
                }
                if (_page1.CheckUpdateChannel.IsChecked == true)
                {
                    if (!string.IsNullOrEmpty(versionInfo))
                    {
                        versionInfo += " ";
                    }
                    versionInfo += $"({_updateChannel})";
                }
            }

            if (_page1.CheckOSVersion.IsChecked == true || _page1.CheckNetVersion.IsChecked == true || _page1.CheckTouchSupport.IsChecked == true)
            {
                if (_page1.CheckOSVersion.IsChecked == true)
                {
                    systemInfo += _osVersion;
                }
                if (_page1.CheckNetVersion.IsChecked == true)
                {
                    if (!string.IsNullOrEmpty(systemInfo))
                    {
                        systemInfo += " | ";
                    }
                    systemInfo += _netVersion;
                }
                if (_page1.CheckTouchSupport.IsChecked == true)
                {
                    if (!string.IsNullOrEmpty(systemInfo))
                    {
                        systemInfo += " | ";
                    }
                    systemInfo += $"触控:{_touchSupport}";
                }
            }

            if (_page1.CheckDeviceId.IsChecked == true)
            {
                extraInfo += $"设备ID: {_deviceId}\n";
            }

            if (_page1.CheckFanceId.IsChecked == true)
            {
                extraInfo += $"遥测ID: {_telemetryId}\n";
            }

            if (_page1.CheckPPTLinkage.IsChecked == true)
            {
                extraInfo += "\nPPT联动设置:\n";
                extraInfo += _pptLinkageSettings;
            }

            if (_page1.CheckInkRecognition.IsChecked == true)
            {
                extraInfo += "\n墨迹识别设置:\n";
                extraInfo += _inkRecognitionSettings;
            }

            return (versionInfo, systemInfo, extraInfo);
        }

        private string BuildGitHubIssueUrl()
        {
            var (versionInfo, systemInfo, extraInfo) = BuildFeedbackInfo();

            string url = "https://github.com/InkCanvasForClass/community/issues/new?template=01-bug_report.yml";

            if (!string.IsNullOrEmpty(versionInfo))
            {
                url += $"&version={Uri.EscapeDataString(versionInfo)}";
            }

            if (!string.IsNullOrEmpty(systemInfo))
            {
                url += $"&os={Uri.EscapeDataString(systemInfo)}";
            }

            if (!string.IsNullOrEmpty(extraInfo))
            {
                url += $"&extra={Uri.EscapeDataString(extraInfo)}";
            }

            return url;
        }

        public void BtnOpenGitHubIssue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string url = BuildGitHubIssueUrl();

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

        public void CardCopyIssueUrl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string url = BuildGitHubIssueUrl();

                Clipboard.SetText(url);
                _page3.CardCopyIssueUrl.Header = "已复制 ✓";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"复制反馈链接失败: {ex.Message}");
            }
        }

        public void BtnCopyMarkdown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_page3.MarkdownTemplate);
                _page3.BtnCopyMarkdown.Content = "已复制 ✓";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"复制Markdown模板失败: {ex.Message}");
            }
        }
    }
}
