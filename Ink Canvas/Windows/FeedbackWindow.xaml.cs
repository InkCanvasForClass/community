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

        public FeedbackWindow()
        {
            InitializeComponent();
            LoadInformation();
            UpdatePreviewText();
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
        }

        private void UpdatePreviewText()
        {
            TextAppVersionInfo.Text = $"{_appVersion} ({_updateChannel})";
            TextSystemInfo.Text = $"{_osVersion} | {_netVersion} | 触控:{_touchSupport}";
            TextDeviceInfo.Text = $"设备ID: {_deviceId}";
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ButtonSubmit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string versionInfo = "";
                string systemInfo = "";

                if (CheckBoxAppVersion.IsChecked == true || CheckBoxUpdateChannel.IsChecked == true)
                {
                    if (CheckBoxAppVersion.IsChecked == true)
                    {
                        versionInfo += _appVersion;
                    }
                    if (CheckBoxUpdateChannel.IsChecked == true)
                    {
                        if (!string.IsNullOrEmpty(versionInfo))
                        {
                            versionInfo += " ";
                        }
                        versionInfo += $"({_updateChannel})";
                    }
                }

                if (CheckBoxOSVersion.IsChecked == true || CheckBoxNetVersion.IsChecked == true || CheckBoxTouchSupport.IsChecked == true)
                {
                    if (CheckBoxOSVersion.IsChecked == true)
                    {
                        systemInfo += _osVersion;
                    }
                    if (CheckBoxNetVersion.IsChecked == true)
                    {
                        if (!string.IsNullOrEmpty(systemInfo))
                        {
                            systemInfo += " | ";
                        }
                        systemInfo += _netVersion;
                    }
                    if (CheckBoxTouchSupport.IsChecked == true)
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

                if (CheckBoxDeviceId.IsChecked == true)
                {
                    url += $"&device={Uri.EscapeDataString(_deviceId)}";
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
    }
}
