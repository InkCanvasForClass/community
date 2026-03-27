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

        public FeedbackWindow()
        {
            InitializeComponent();
            LoadInformation();
            CheckTelemetryIdAvailability();
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
                    CardFanceId.Visibility = Visibility.Collapsed;
                    CheckFanceId.IsChecked = false;
                    CheckFanceId.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查遥测ID可用性失败: {ex.Message}");
                CardFanceId.Visibility = Visibility.Collapsed;
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
            Page1.Visibility = Visibility.Visible;
            Page2.Visibility = Visibility.Collapsed;
            ButtonCancel.Visibility = Visibility.Visible;
            ButtonNext.Visibility = Visibility.Visible;
            ButtonBack.Visibility = Visibility.Collapsed;
            ButtonSubmit.Visibility = Visibility.Collapsed;
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

                Page1.Visibility = Visibility.Collapsed;
                Page2.Visibility = Visibility.Visible;
                ButtonCancel.Visibility = Visibility.Collapsed;
                ButtonNext.Visibility = Visibility.Collapsed;
                ButtonBack.Visibility = Visibility.Visible;
                ButtonSubmit.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"切换到第二页失败: {ex.Message}");
            }
        }

        private void ButtonSubmit_Click(object sender, RoutedEventArgs e)
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

                if (CheckDeviceId.IsChecked == true)
                {
                    url += $"&device={Uri.EscapeDataString(_deviceId)}";
                }

                if (CheckFanceId.IsChecked == true)
                {
                    url += $"&telemetry={Uri.EscapeDataString(_telemetryId)}";
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
