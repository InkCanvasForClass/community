using Ink_Canvas.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class CloudStoragePage : Page
    {
        private const string AppId = "app_WkjocWqsrVY7T6zQV2CfiA";
        private const string AppSecret = "o7dx5b5ASGUMcM72PCpmRQYAhSijqaOVHoGyBK0IxbA";
        private const string NoSavedTokenText = "（无保存的Token）";

        private static readonly Regex NonDigitRegex = new Regex("[^0-9]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private DlassApiClient _apiClient;
        private readonly List<WhiteboardInfo> _currentWhiteboards = new List<WhiteboardInfo>();
        private CancellationTokenSource _connectionTestCts;
        private bool _isLoadingSettings;
        private bool _hasPromptedDlassRegistration;

        public CloudStoragePage()
        {
            InitializeComponent();
            Loaded += CloudStoragePage_Loaded;
            Unloaded += CloudStoragePage_Unloaded;
        }

        private async void CloudStoragePage_Loaded(object sender, RoutedEventArgs e)
        {
            EnsureSettingsObjects();
            InitializeClassSelectionPlaceholder("（等待连接）");
            LoadAllSettings();
            InitializeApiClient();
            PromptDlassRegistrationIfNeeded();
            await TestConnectionAsync();
        }

        private void CloudStoragePage_Unloaded(object sender, RoutedEventArgs e)
        {
            CancelConnectionTest();
            _apiClient?.Dispose();
            _apiClient = null;
        }

        private static void EnsureSettingsObjects()
        {
            if (MainWindow.Settings == null)
            {
                MainWindow.Settings = new Settings();
            }

            if (MainWindow.Settings.Dlass == null)
            {
                MainWindow.Settings.Dlass = new DlassSettings();
            }

            if (MainWindow.Settings.Upload == null)
            {
                MainWindow.Settings.Upload = new UploadSettings();
            }
        }

        private void LoadAllSettings()
        {
            RunWithoutUiEvents(() =>
            {
                LoadUserToken();
                LoadAutoUploadSettings();
                LoadUniversalUploadSettings();
                LoadWebDavSettings();
            });
        }

        private void RunWithoutUiEvents(Action action)
        {
            bool previous = _isLoadingSettings;
            _isLoadingSettings = true;
            try
            {
                action();
            }
            finally
            {
                _isLoadingSettings = previous;
            }
        }

        private void InitializeApiClient()
        {
            EnsureSettingsObjects();
            _apiClient?.Dispose();

            var userToken = GetUserToken();
            var apiBaseUrl = MainWindow.Settings.Dlass.ApiBaseUrl;

            if (string.IsNullOrWhiteSpace(apiBaseUrl) || apiBaseUrl.Contains("api.dlass.tech"))
            {
                apiBaseUrl = "https://dlass.tech";
                MainWindow.Settings.Dlass.ApiBaseUrl = apiBaseUrl;
                MainWindow.SaveSettingsToFile();
            }

            _apiClient = string.IsNullOrEmpty(userToken)
                ? new DlassApiClient(AppId, AppSecret, baseUrl: apiBaseUrl)
                : new DlassApiClient(AppId, AppSecret, baseUrl: apiBaseUrl, userToken: userToken);
        }

        private static string GetUserToken()
        {
            return MainWindow.Settings?.Dlass?.UserToken ?? string.Empty;
        }

        private static List<string> GetSavedTokens()
        {
            return MainWindow.Settings?.Dlass?.SavedTokens ?? new List<string>();
        }

        private void LoadUserToken()
        {
            var savedTokens = GetSavedTokens();
            var currentToken = GetUserToken();

            CmbSavedTokens.Items.Clear();
            CmbSavedTokens.IsEnabled = savedTokens.Count > 0;

            if (savedTokens.Count > 0)
            {
                foreach (var token in savedTokens)
                {
                    CmbSavedTokens.Items.Add(token);
                }

                if (string.IsNullOrEmpty(currentToken))
                {
                    currentToken = savedTokens[0];
                    SaveUserToken(currentToken);
                }

                int selectedIndex = savedTokens.IndexOf(currentToken);
                CmbSavedTokens.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
            }
            else
            {
                CmbSavedTokens.Items.Add(NoSavedTokenText);
                CmbSavedTokens.SelectedIndex = 0;
            }

            TxtNewToken.Text = string.Empty;
            UpdateTokenStatus(!string.IsNullOrEmpty(GetUserToken()));
        }

        private static void SaveUserToken(string token)
        {
            EnsureSettingsObjects();
            MainWindow.Settings.Dlass.UserToken = token ?? string.Empty;
            MainWindow.SaveSettingsToFile();
        }

        private static void AddTokenToList(string token)
        {
            EnsureSettingsObjects();

            if (MainWindow.Settings.Dlass.SavedTokens == null)
            {
                MainWindow.Settings.Dlass.SavedTokens = new List<string>();
            }

            if (!string.IsNullOrEmpty(token) && !MainWindow.Settings.Dlass.SavedTokens.Contains(token))
            {
                MainWindow.Settings.Dlass.SavedTokens.Add(token);
                MainWindow.SaveSettingsToFile();
            }
        }

        private static void RemoveTokenFromList(string token)
        {
            if (MainWindow.Settings?.Dlass?.SavedTokens == null)
            {
                return;
            }

            MainWindow.Settings.Dlass.SavedTokens.Remove(token);
            MainWindow.SaveSettingsToFile();
        }

        private void LoadAutoUploadSettings()
        {
            ToggleSwitchAutoUploadNotes.IsOn = MainWindow.Settings?.Dlass?.IsAutoUploadNotes == true;
        }

        private void LoadUniversalUploadSettings()
        {
            int delayMinutes = MainWindow.Settings?.Upload?.UploadDelayMinutes ?? 0;
            delayMinutes = Math.Max(0, Math.Min(60, delayMinutes));
            TxtUniversalUploadDelayMinutes.Text = delayMinutes.ToString();
            LoadUploadProvidersList();
        }

        private void LoadUploadProvidersList()
        {
            try
            {
                LstUploadProviders.ItemsSource = null;
                LstUploadProviders.ItemsSource = UploadHelper.GetProviders();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载上传提供者列表时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ReloadUploadProvidersSilently()
        {
            RunWithoutUiEvents(LoadUploadProvidersList);
        }

        private void LoadWebDavSettings()
        {
            if (MainWindow.Settings?.Dlass == null)
            {
                return;
            }

            TxtWebDavUrl.Text = MainWindow.Settings.Dlass.WebDavUrl;
            TxtWebDavUsername.Text = MainWindow.Settings.Dlass.WebDavUsername;
            TxtWebDavPassword.Password = MainWindow.Settings.Dlass.WebDavPassword;
            TxtWebDavRootDirectory.Text = MainWindow.Settings.Dlass.WebDavRootDirectory;
        }

        private void InitializeClassSelectionPlaceholder(string text)
        {
            RunWithoutUiEvents(() =>
            {
                CmbClassSelection.Items.Clear();
                CmbClassSelection.Items.Add(text);
                CmbClassSelection.SelectedIndex = 0;
                CmbClassSelection.IsEnabled = false;
            });
        }

        private void LoadClasses(List<WhiteboardInfo> whiteboards, UserInfo user = null)
        {
            RunWithoutUiEvents(() =>
            {
                CmbClassSelection.Items.Clear();

                var classGroups = (whiteboards ?? new List<WhiteboardInfo>())
                    .Where(w => !string.IsNullOrEmpty(w.ClassName))
                    .GroupBy(w => w.ClassName)
                    .OrderBy(g => g.Key)
                    .ToList();

                if (classGroups.Count > 0)
                {
                    var teacherName = user?.Username ?? "未知教师";
                    foreach (var group in classGroups)
                    {
                        var className = group.Key;
                        CmbClassSelection.Items.Add(new ClassSelectionItem
                        {
                            DisplayText = $"{teacherName} - {className}",
                            ClassName = className,
                            TeacherName = teacherName
                        });
                    }

                    var savedClassName = MainWindow.Settings?.Dlass?.SelectedClassName ?? string.Empty;
                    var savedItem = CmbClassSelection.Items.Cast<ClassSelectionItem>()
                        .FirstOrDefault(item => item.ClassName == savedClassName);
                    CmbClassSelection.SelectedItem = savedItem ?? CmbClassSelection.Items[0];
                    CmbClassSelection.IsEnabled = true;
                }
                else
                {
                    CmbClassSelection.Items.Add("（无可用班级）");
                    CmbClassSelection.SelectedIndex = 0;
                    CmbClassSelection.IsEnabled = false;
                }
            });
        }

        private void CmbClassSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            try
            {
                if (CmbClassSelection.SelectedItem is ClassSelectionItem selectedItem)
                {
                    EnsureSettingsObjects();
                    MainWindow.Settings.Dlass.SelectedClassName = selectedItem.ClassName;
                    MainWindow.SaveSettingsToFile();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"选择班级时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ToggleSwitchAutoUploadNotes_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            try
            {
                EnsureSettingsObjects();
                bool enabled = ToggleSwitchAutoUploadNotes.IsOn;
                MainWindow.Settings.Dlass.IsAutoUploadNotes = enabled;
                SetProviderEnabled("Dlass", enabled);
                MainWindow.SaveSettingsToFile();
                ReloadUploadProvidersSilently();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存自动上传设置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void TxtUniversalUploadDelayMinutes_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            try
            {
                EnsureSettingsObjects();
                if (int.TryParse(TxtUniversalUploadDelayMinutes.Text, out int delayMinutes))
                {
                    delayMinutes = Math.Max(0, Math.Min(60, delayMinutes));
                    if (TxtUniversalUploadDelayMinutes.Text != delayMinutes.ToString())
                    {
                        TxtUniversalUploadDelayMinutes.Text = delayMinutes.ToString();
                        TxtUniversalUploadDelayMinutes.CaretIndex = TxtUniversalUploadDelayMinutes.Text.Length;
                    }

                    MainWindow.Settings.Upload.UploadDelayMinutes = delayMinutes;
                    MainWindow.SaveSettingsToFile();
                }
                else if (string.IsNullOrWhiteSpace(TxtUniversalUploadDelayMinutes.Text))
                {
                    MainWindow.Settings.Upload.UploadDelayMinutes = 0;
                    MainWindow.SaveSettingsToFile();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存通用上传延迟时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void TxtUniversalUploadDelayMinutes_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = NonDigitRegex.IsMatch(e.Text);
        }

        private void ToggleProviderEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            try
            {
                if (sender is iNKORE.UI.WPF.Modern.Controls.ToggleSwitch toggleSwitch &&
                    toggleSwitch.DataContext is IUploadProvider provider)
                {
                    EnsureSettingsObjects();
                    SetProviderEnabled(provider.Name, toggleSwitch.IsOn);

                    if (provider.Name == "Dlass")
                    {
                        MainWindow.Settings.Dlass.IsAutoUploadNotes = toggleSwitch.IsOn;
                        RunWithoutUiEvents(() => ToggleSwitchAutoUploadNotes.IsOn = toggleSwitch.IsOn);
                    }

                    MainWindow.SaveSettingsToFile();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存上传提供者启用状态时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private static void SetProviderEnabled(string providerName, bool enabled)
        {
            EnsureSettingsObjects();

            if (MainWindow.Settings.Upload.EnabledProviders == null)
            {
                MainWindow.Settings.Upload.EnabledProviders = new List<string>();
            }

            if (enabled)
            {
                if (!MainWindow.Settings.Upload.EnabledProviders.Contains(providerName))
                {
                    MainWindow.Settings.Upload.EnabledProviders.Add(providerName);
                }
            }
            else
            {
                MainWindow.Settings.Upload.EnabledProviders.Remove(providerName);
            }
        }

        private void CmbSavedTokens_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            try
            {
                if (CmbSavedTokens.SelectedItem == null ||
                    CmbSavedTokens.SelectedItem.ToString() == NoSavedTokenText)
                {
                    return;
                }

                SaveUserToken(CmbSavedTokens.SelectedItem.ToString());
                InitializeApiClient();
                UpdateTokenStatus(true);
                _ = TestConnectionAsync();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"选择Token时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void BtnSaveToken_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var token = TxtNewToken.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(token))
                {
                    MessageBox.Show("请输入新的用户Token", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                AddTokenToList(token);
                SaveUserToken(token);
                InitializeApiClient();
                RunWithoutUiEvents(LoadUserToken);
                MessageBox.Show("Token已成功保存并已选择", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                _ = TestConnectionAsync();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存Token时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"保存Token时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClearToken_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CmbSavedTokens.SelectedItem == null ||
                    CmbSavedTokens.SelectedItem.ToString() == NoSavedTokenText)
                {
                    MessageBox.Show("请先选择一个Token", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedToken = CmbSavedTokens.SelectedItem.ToString();
                var result = MessageBox.Show("确定要删除已选中的Token吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                RemoveTokenFromList(selectedToken);
                if (GetUserToken() == selectedToken)
                {
                    SaveUserToken(string.Empty);
                }

                InitializeApiClient();
                RunWithoutUiEvents(LoadUserToken);
                InitializeClassSelectionPlaceholder("（等待连接）");
                _currentWhiteboards.Clear();
                SetConnectionStatus("未连接", Colors.Gray);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"删除Token时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"删除Token时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnTestToken_Click(object sender, RoutedEventArgs e)
        {
            await TestConnectionAsync();
        }

        private void BtnOpenDlassDashboard_Click(object sender, RoutedEventArgs e)
        {
            OpenDlassDashboard();
        }

        private void BtnSaveWebDav_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                EnsureSettingsObjects();
                MainWindow.Settings.Dlass.WebDavUrl = TxtWebDavUrl.Text;
                MainWindow.Settings.Dlass.WebDavUsername = TxtWebDavUsername.Text;
                MainWindow.Settings.Dlass.WebDavPassword = TxtWebDavPassword.Password;
                MainWindow.Settings.Dlass.WebDavRootDirectory = TxtWebDavRootDirectory.Text;
                MainWindow.SaveSettingsToFile();

                MessageBox.Show("WebDav设置已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存WebDav设置时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"保存WebDav设置时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancelWebDav_Click(object sender, RoutedEventArgs e)
        {
            RunWithoutUiEvents(LoadWebDavSettings);
        }

        private void PromptDlassRegistrationIfNeeded()
        {
            if (_hasPromptedDlassRegistration || !string.IsNullOrWhiteSpace(GetUserToken()))
            {
                return;
            }

            _hasPromptedDlassRegistration = true;
            var result = MessageBox.Show(
                "您是否已经注册了Dlass账号？\n\n" +
                "• 如果已注册：请在本页填入用户 Token\n" +
                "• 如果未注册：将打开浏览器跳转到 Dlass 控制台",
                "Dlass账号注册",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                OpenDlassDashboard();
            }
        }

        private static void OpenDlassDashboard()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://dlass.tech/dashboard",
                    UseShellExecute = true
                });
                LogHelper.WriteLogToFile("已打开浏览器跳转到Dlass控制台", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"打开浏览器时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show("无法打开浏览器。请手动访问: https://dlass.tech/dashboard",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task TestConnectionAsync()
        {
            CancelConnectionTest();
            _connectionTestCts = new CancellationTokenSource();
            var cancellationToken = _connectionTestCts.Token;

            SetConnectionStatus("测试中...", Colors.Gray);

            try
            {
                if (_apiClient == null)
                {
                    InitializeApiClient();
                }

                var userToken = GetUserToken();
                if (string.IsNullOrEmpty(userToken))
                {
                    SetConnectionStatus("未设置Token", Colors.Red);
                    InitializeClassSelectionPlaceholder("（无可用班级）");
                    return;
                }

                var authData = new
                {
                    app_id = AppId,
                    app_secret = AppSecret,
                    user_token = userToken
                };

                var result = await _apiClient.PostAsync<AuthWithTokenResponse>(
                    "/api/whiteboard/framework/auth-with-token",
                    authData,
                    requireAuth: false,
                    cancellationToken: cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                if (result == null || !result.Success)
                {
                    throw new Exception("认证响应失败");
                }

                var whiteboards = result.Whiteboards ?? new List<WhiteboardInfo>();
                _currentWhiteboards.Clear();
                _currentWhiteboards.AddRange(whiteboards);

                SetConnectionStatus($"已连接 (找到 {whiteboards.Count} 个白板)", Color.FromRgb(34, 197, 94));
                LoadClasses(whiteboards, result.User);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(GetUserToken()) && GetUserToken().Length < 10)
                {
                    LogHelper.WriteLogToFile("Token格式可能不正确（长度过短，至少需要10个字符）", LogHelper.LogType.Error);
                }

                LogHelper.WriteLogToFile($"Dlass API连接测试失败: {ex.Message}", LogHelper.LogType.Error);
                SetConnectionStatus("连接失败", Colors.Red);
                _currentWhiteboards.Clear();
                InitializeClassSelectionPlaceholder("（无可用班级）");
            }
        }

        private void CancelConnectionTest()
        {
            if (_connectionTestCts == null)
            {
                return;
            }

            try
            {
                _connectionTestCts.Cancel();
            }
            catch
            {
            }
            finally
            {
                _connectionTestCts.Dispose();
                _connectionTestCts = null;
            }
        }

        private void UpdateTokenStatus(bool hasToken)
        {
            if (hasToken)
            {
                TxtTokenStatus.Text = "已选择Token";
                TxtTokenStatus.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            }
            else
            {
                TxtTokenStatus.Text = "未设置Token";
                TxtTokenStatus.Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170));
            }
        }

        private void SetConnectionStatus(string text, Color color)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetConnectionStatus(text, color));
                return;
            }

            TxtConnectionStatus.Text = text;
            TxtConnectionStatus.Foreground = new SolidColorBrush(color);
        }
    }

    public class AuthWithTokenResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("whiteboards")]
        public List<WhiteboardInfo> Whiteboards { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("user")]
        public UserInfo User { get; set; }
    }

    public class WhiteboardInfo
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("board_id")]
        public string BoardId { get; set; }

        [JsonProperty("secret_key")]
        public string SecretKey { get; set; }

        [JsonProperty("class_name")]
        public string ClassName { get; set; }

        [JsonProperty("class_id")]
        public int ClassId { get; set; }

        [JsonProperty("is_online")]
        public bool IsOnline { get; set; }

        [JsonProperty("last_heartbeat")]
        public string LastHeartbeat { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }
    }

    public class UserInfo
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }
    }

    public class ClassSelectionItem
    {
        public string DisplayText { get; set; }
        public string ClassName { get; set; }
        public string TeacherName { get; set; }

        public override string ToString()
        {
            return DisplayText;
        }
    }
}
