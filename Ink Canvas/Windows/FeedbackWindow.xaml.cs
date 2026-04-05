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
    /// <summary>
    /// 反馈窗口，提供用户反馈和问题报告功能。
    /// 收集系统环境信息并生成GitHub Issue的Markdown模板。
    /// </summary>
    /// <remarks>
    /// 窗口包含三个页面：
    /// - FeedbackPage1: 选择要包含的环境信息
    /// - FeedbackPage2: 预览收集的环境信息
    /// - FeedbackPage3: 生成并复制Markdown模板或打开GitHub Issue
    /// </remarks>
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

        /// <summary>
        /// 构造函数，初始化反馈窗口并加载系统信息。
        /// </summary>
        /// <remarks>
        /// 初始化操作包括：
        /// 1. 初始化UI组件
        /// 2. 创建三个反馈页面实例
        /// 3. 绑定页面3的事件处理器
        /// 4. 加载系统环境信息（版本、系统、触控等）
        /// 5. 检查遥测ID可用性
        /// 6. 导航到第一页
        /// </remarks>
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

        /// <summary>
        /// 内容框架导航事件处理器，用于更新按钮可见性。
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">导航事件参数</param>
        private void ContentFrame_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            UpdateButtonVisibility();
        }

        /// <summary>
        /// 加载系统环境信息，包括软件版本、系统信息、设备信息等。
        /// </summary>
        /// <remarks>
        /// 收集的信息包括：
        /// - 软件版本和更新通道
        /// - 操作系统版本和.NET版本
        /// - 触控支持情况和触摸设备数量
        /// - 设备ID
        /// - 遥测ID
        /// - PPT联动设置
        /// - 墨迹识别设置和识别引擎
        /// </remarks>
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

        /// <summary>
        /// 获取存储在文件中的遥测ID。
        /// </summary>
        /// <returns>遥测ID字符串，如果不存在则返回空字符串</returns>
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

        /// <summary>
        /// 检查遥测ID是否可用，如果不可用则禁用相关复选框。
        /// </summary>
        /// <remarks>
        /// 检查telemetry_id.dat文件是否存在，如果不存在则禁用"包含遥测ID"选项
        /// </remarks>
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

        /// <summary>
        /// 检查遥测ID文件是否存在。
        /// </summary>
        /// <returns>如果遥测ID文件存在返回true，否则返回false</returns>
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

        /// <summary>
        /// 取消按钮点击事件处理器，关闭反馈窗口。
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 返回按钮点击事件处理器，返回上一页面。
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        private void ButtonBack_Click(object sender, RoutedEventArgs e)
        {
            if (ContentFrame.BackStackDepth > 0)
            {
                ContentFrame.GoBack();
                UpdateButtonVisibility();
            }
        }

        /// <summary>
        /// 下一步按钮点击事件处理器，导航到第二页并更新页面信息。
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        private void ButtonNext_Click(object sender, RoutedEventArgs e)
        {
            UpdatePage2Info();
            ContentFrame.Navigate(_page2, null, _transitionInfo);
            UpdateButtonVisibility();
        }

        /// <summary>
        /// 确认按钮点击事件处理器，生成Markdown模板并导航到第三页。
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        private void ButtonConfirm_Click(object sender, RoutedEventArgs e)
        {
            GenerateMarkdownTemplate();
            ContentFrame.Navigate(_page3, null, _transitionInfo);
            UpdateButtonVisibility();
        }

        /// <summary>
        /// 根据当前页面更新按钮的可见性。
        /// </summary>
        /// <remarks>
        /// - 第一页：显示"取消"和"下一步"按钮
        /// - 第二页：显示"返回"和"确认"按钮
        /// - 第三页：显示"返回"按钮
        /// </remarks>
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

        /// <summary>
        /// 更新第二页的显示信息，将用户选择的环境信息展示在页面上。
        /// </summary>
        /// <remarks>
        /// 根据第一页用户的复选框选择情况，更新以下信息：
        /// - 软件版本信息
        /// - 系统信息（操作系统、.NET版本、触控支持）
        /// - 设备ID
        /// - 遥测ID
        /// - 软件配置信息（PPT联动设置、墨迹识别设置）
        /// </remarks>
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

        /// <summary>
        /// 生成Markdown格式的反馈信息模板，并填充到第三页的文本框中。
        /// </summary>
        /// <remarks>
        /// 生成的模板包含以下部分：
        /// - 环境信息（软件版本、操作系统、.NET版本、触控支持）
        /// - 设备信息（设备ID、遥测ID）
        /// - 软件配置（PPT联动设置、墨迹识别设置）
        /// </remarks>
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

        /// <summary>
        /// 构建反馈信息元组，包含版本信息、系统信息和额外信息。
        /// </summary>
        /// <returns>包含版本信息、系统信息和额外信息的元组</returns>
        /// <remarks>
        /// 根据第一页用户的复选框选择情况，收集以下信息：
        /// - versionInfo: 软件版本和更新通道
        /// - systemInfo: 操作系统、.NET版本和触控支持
        /// - extraInfo: 设备ID、遥测ID、PPT联动设置和墨迹识别设置
        /// </remarks>
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

        /// <summary>
        /// 构建GitHub Issue创建页面的URL，包含预填的环境信息参数。
        /// </summary>
        /// <returns>完整的GitHub Issue URL字符串</returns>
        /// <remarks>
        /// URL基于模板01-bug_report.yml，并根据用户选择的信息添加以下查询参数：
        /// - version: 版本信息
        /// - os: 系统信息
        /// - extra: 额外信息（设备ID、遥测ID、配置信息）
        /// </remarks>
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

        /// <summary>
        /// 打开GitHub Issue页面按钮点击事件处理器。
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 1. 构建包含环境信息的GitHub Issue URL
        /// 2. 使用系统默认浏览器打开该URL
        /// 3. 关闭反馈窗口
        /// </remarks>
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

        /// <summary>
        /// 复制Issue链接按钮点击事件处理器，将GitHub Issue URL复制到剪贴板。
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 1. 构建包含环境信息的GitHub Issue URL
        /// 2. 将URL复制到系统剪贴板
        /// 3. 更新按钮显示为"已复制 ✓"
        /// </remarks>
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

        /// <summary>
        /// 复制Markdown模板按钮点击事件处理器，将Markdown模板复制到剪贴板。
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        /// <remarks>
        /// 1. 从第三页获取Markdown模板内容
        /// 2. 将模板复制到系统剪贴板
        /// 3. 更新按钮显示为"已复制 ✓"
        /// </remarks>
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
