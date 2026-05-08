using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class CloudStoragePage : Page
    {
        private const string APP_ID = "app_WkjocWqsrVY7T6zQV2CfiA";
        private const string APP_SECRET = "o7dx5b5ASGUMcM72PCpmRQYAhSijqaOVHoGyBK0IxbA";
        private static readonly Regex _nonDigitRegex = new Regex("[^0-9]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private DlassApiClient _apiClient;
        private List<WhiteboardInfo> _currentWhiteboards = new List<WhiteboardInfo>();
        private UserInfo _currentUser;
        private bool _isLoadingCloudSettings;

        public CloudStoragePage()
        {
            InitializeComponent();
            Unloaded += (s, e) => _apiClient?.Dispose();

            InitializeCloudStorageControls();
        }

        #region 云存储设置

        private void InitializeCloudStorageControls()
        {
            _isLoadingCloudSettings = true;
            CmbClassSelection.Items.Clear();
            CmbClassSelection.Items.Add("（等待连接）");
            CmbClassSelection.SelectedIndex = 0;
            CmbClassSelection.IsEnabled = false;

            LoadUserToken();
            LoadAutoUploadSettings();
            LoadUniversalUploadSettings();
            LoadWebDavSettings();
            _isLoadingCloudSettings = false;

            InitializeApiClient();
            _ = TestConnectionAsync();
        }

        private void InitializeApiClient()
        {
            var userToken = GetUserToken();
            var apiBaseUrl = MainWindow.Settings?.Dlass?.ApiBaseUrl;

            if (string.IsNullOrEmpty(apiBaseUrl) || apiBaseUrl.Contains("api.dlass.tech"))
            {
                apiBaseUrl = "https://dlass.tech";
                if (MainWindow.Settings?.Dlass != null)
                {
                    MainWindow.Settings.Dlass.ApiBaseUrl = apiBaseUrl;
                    MainWindow.SaveSettingsToFile();
                }
            }

            _apiClient?.Dispose();
            _apiClient = string.IsNullOrEmpty(userToken)
                ? new DlassApiClient(APP_ID, APP_SECRET, baseUrl: apiBaseUrl)
                : new DlassApiClient(APP_ID, APP_SECRET, baseUrl: apiBaseUrl, userToken: userToken);
        }

        private string GetUserToken()
        {
            return MainWindow.Settings?.Dlass?.UserToken ?? string.Empty;
        }

        private List<string> GetSavedTokens()
        {
            return MainWindow.Settings?.Dlass?.SavedTokens ?? new List<string>();
        }

        private void LoadUserToken()
        {
            var savedTokens = GetSavedTokens();
            var currentToken = GetUserToken();

            CmbSavedTokens.Items.Clear();
            CmbSavedTokens.IsEnabled = true;

            if (savedTokens.Count > 0)
            {
                foreach (var token in savedTokens)
                    CmbSavedTokens.Items.Add(token);

                if (!string.IsNullOrEmpty(currentToken))
                {
                    var index = savedTokens.IndexOf(currentToken);
                    CmbSavedTokens.SelectedIndex = index >= 0 ? index : 0;
                }
                else
                {
                    CmbSavedTokens.SelectedIndex = 0;
                }
            }
            else
            {
                CmbSavedTokens.Items.Add("（无保存的 Token）");
                CmbSavedTokens.SelectedIndex = 0;
                CmbSavedTokens.IsEnabled = false;
            }

            TxtNewToken.Text = string.Empty;
            SetStatusText(TxtTokenStatus,
                string.IsNullOrEmpty(currentToken) ? "未设置 Token" : "已选择 Token",
                string.IsNullOrEmpty(currentToken) ? StatusColor.Neutral : StatusColor.Success);
        }

        private void SaveUserToken(string token)
        {
            if (MainWindow.Settings?.Dlass == null) return;

            MainWindow.Settings.Dlass.UserToken = token ?? string.Empty;
            MainWindow.SaveSettingsToFile();
        }

        private void AddTokenToList(string token)
        {
            if (MainWindow.Settings?.Dlass == null) return;

            if (MainWindow.Settings.Dlass.SavedTokens == null)
                MainWindow.Settings.Dlass.SavedTokens = new List<string>();

            if (!string.IsNullOrEmpty(token) && !MainWindow.Settings.Dlass.SavedTokens.Contains(token))
            {
                MainWindow.Settings.Dlass.SavedTokens.Add(token);
                MainWindow.SaveSettingsToFile();
            }
        }

        private void RemoveTokenFromList(string token)
        {
            if (MainWindow.Settings?.Dlass?.SavedTokens == null) return;

            MainWindow.Settings.Dlass.SavedTokens.Remove(token);
            MainWindow.SaveSettingsToFile();
        }

        private void LoadClasses(List<WhiteboardInfo> whiteboards, UserInfo user = null)
        {
            CmbClassSelection.Items.Clear();

            if (whiteboards != null && whiteboards.Count > 0)
            {
                var teacherName = user?.Username ?? "未知教师";
                var classGroups = whiteboards
                    .Where(w => !string.IsNullOrEmpty(w.ClassName))
                    .GroupBy(w => w.ClassName)
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var group in classGroups)
                {
                    CmbClassSelection.Items.Add(new ClassSelectionItem
                    {
                        DisplayText = $"{teacherName} - {group.Key}",
                        ClassName = group.Key,
                        TeacherName = teacherName
                    });
                }

                var savedClassName = MainWindow.Settings?.Dlass?.SelectedClassName ?? string.Empty;
                if (!string.IsNullOrEmpty(savedClassName))
                {
                    var savedItem = CmbClassSelection.Items.Cast<ClassSelectionItem>()
                        .FirstOrDefault(item => item.ClassName == savedClassName);
                    CmbClassSelection.SelectedItem = savedItem ?? (CmbClassSelection.Items.Count > 0 ? CmbClassSelection.Items[0] : null);
                }
                else if (CmbClassSelection.Items.Count > 0)
                {
                    CmbClassSelection.SelectedIndex = 0;
                }

                CmbClassSelection.IsEnabled = CmbClassSelection.Items.Count > 0;
            }
            else
            {
                CmbClassSelection.Items.Add("（无可用班级）");
                CmbClassSelection.SelectedIndex = 0;
                CmbClassSelection.IsEnabled = false;
            }
        }

        private void CmbClassSelection_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (CmbClassSelection.SelectedItem is ClassSelectionItem selectedItem && MainWindow.Settings?.Dlass != null)
                {
                    MainWindow.Settings.Dlass.SelectedClassName = selectedItem.ClassName;
                    MainWindow.SaveSettingsToFile();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"选择班级时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void LoadAutoUploadSettings()
        {
            try
            {
                if (MainWindow.Settings?.Dlass != null)
                    ToggleSwitchAutoUploadNotes.IsOn = MainWindow.Settings.Dlass.IsAutoUploadNotes;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载自动上传设置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ToggleSwitchAutoUploadNotes_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoadingCloudSettings) return;

            try
            {
                if (MainWindow.Settings?.Dlass == null) return;

                MainWindow.Settings.Dlass.IsAutoUploadNotes = ToggleSwitchAutoUploadNotes.IsOn;
                SetProviderEnabled("Dlass", ToggleSwitchAutoUploadNotes.IsOn, saveImmediately: false);
                MainWindow.SaveSettingsToFile();
                LoadUploadProvidersList();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存自动上传设置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void LoadUniversalUploadSettings()
        {
            try
            {
                if (MainWindow.Settings?.Upload != null)
                    TxtUniversalUploadDelayMinutes.Text = MainWindow.Settings.Upload.UploadDelayMinutes.ToString();

                LoadUploadProvidersList();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载通用上传设置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void LoadUploadProvidersList()
        {
            var wasLoading = _isLoadingCloudSettings;
            try
            {
                _isLoadingCloudSettings = true;
                LstUploadProviders.ItemsSource = null;
                LstUploadProviders.ItemsSource = UploadHelper.GetProviders();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载上传提供者列表时出错: {ex.Message}", LogHelper.LogType.Error);
            }
            finally
            {
                _isLoadingCloudSettings = wasLoading;
            }
        }

        private void TxtUniversalUploadDelayMinutes_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isLoadingCloudSettings) return;

            try
            {
                if (MainWindow.Settings?.Upload == null || TxtUniversalUploadDelayMinutes == null) return;

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
            e.Handled = _nonDigitRegex.IsMatch(e.Text);
        }

        private void ToggleProviderEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoadingCloudSettings) return;

            try
            {
                if (sender is iNKORE.UI.WPF.Modern.Controls.ToggleSwitch toggleSwitch &&
                    toggleSwitch.DataContext is IUploadProvider provider)
                {
                    SetProviderEnabled(provider.Name, toggleSwitch.IsOn, saveImmediately: false);

                    if (provider.Name == "Dlass" && MainWindow.Settings?.Dlass != null &&
                        MainWindow.Settings.Dlass.IsAutoUploadNotes != toggleSwitch.IsOn)
                    {
                        MainWindow.Settings.Dlass.IsAutoUploadNotes = toggleSwitch.IsOn;
                        ToggleSwitchAutoUploadNotes.IsOn = toggleSwitch.IsOn;
                    }

                    MainWindow.SaveSettingsToFile();
                    LoadUploadProvidersList();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存上传提供者启用状态时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private static void SetProviderEnabled(string providerName, bool isEnabled, bool saveImmediately)
        {
            if (MainWindow.Settings?.Upload == null) return;

            if (MainWindow.Settings.Upload.EnabledProviders == null)
                MainWindow.Settings.Upload.EnabledProviders = new List<string>();

            if (isEnabled)
            {
                if (!MainWindow.Settings.Upload.EnabledProviders.Contains(providerName))
                    MainWindow.Settings.Upload.EnabledProviders.Add(providerName);
            }
            else
            {
                MainWindow.Settings.Upload.EnabledProviders.Remove(providerName);
            }

            if (saveImmediately)
                MainWindow.SaveSettingsToFile();
        }

        private void CmbSavedTokens_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isLoadingCloudSettings) return;

            try
            {
                if (CmbSavedTokens.SelectedItem == null || CmbSavedTokens.SelectedItem.ToString() == "（无保存的 Token）")
                    return;

                SaveUserToken(CmbSavedTokens.SelectedItem.ToString());
                InitializeApiClient();
                SetStatusText(TxtTokenStatus, "已选择 Token", StatusColor.Success);
                _ = TestConnectionAsync();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"选择 Token 时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void BtnSaveToken_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var token = TxtNewToken.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(token))
                {
                    MessageBox.Show("请输入新的用户 Token", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                AddTokenToList(token);
                SaveUserToken(token);
                InitializeApiClient();
                LoadUserToken();
                MessageBox.Show("Token 已成功保存并已选择", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                _ = TestConnectionAsync();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存 Token 时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"保存 Token 时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClearToken_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CmbSavedTokens.SelectedItem == null || CmbSavedTokens.SelectedItem.ToString() == "（无保存的 Token）")
                {
                    MessageBox.Show("请先选择一个 Token", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedToken = CmbSavedTokens.SelectedItem.ToString();
                var result = MessageBox.Show("确定要删除已选中的 Token 吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                RemoveTokenFromList(selectedToken);
                if (GetUserToken() == selectedToken)
                    SaveUserToken(string.Empty);

                InitializeApiClient();
                LoadUserToken();
                ResetDlassConnectionState();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"删除 Token 时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"删除 Token 时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnTestToken_Click(object sender, RoutedEventArgs e)
        {
            await TestConnectionAsync();
        }

        private void LoadWebDavSettings()
        {
            try
            {
                if (MainWindow.Settings?.Dlass == null) return;

                TxtWebDavUrl.Text = MainWindow.Settings.Dlass.WebDavUrl;
                TxtWebDavUsername.Text = MainWindow.Settings.Dlass.WebDavUsername;
                TxtWebDavPassword.Password = MainWindow.Settings.Dlass.WebDavPassword;
                TxtWebDavRootDirectory.Text = MainWindow.Settings.Dlass.WebDavRootDirectory;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载 WebDav 设置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void BtnSaveWebDav_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MainWindow.Settings?.Dlass == null) return;

                MainWindow.Settings.Dlass.WebDavUrl = TxtWebDavUrl.Text;
                MainWindow.Settings.Dlass.WebDavUsername = TxtWebDavUsername.Text;
                MainWindow.Settings.Dlass.WebDavPassword = TxtWebDavPassword.Password;
                MainWindow.Settings.Dlass.WebDavRootDirectory = TxtWebDavRootDirectory.Text;
                MainWindow.SaveSettingsToFile();

                MessageBox.Show("WebDav 设置已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存 WebDav 设置时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"保存 WebDav 设置时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancelWebDav_Click(object sender, RoutedEventArgs e)
        {
            LoadWebDavSettings();
        }

        private async Task TestConnectionAsync()
        {
            SetStatusText(TxtConnectionStatus, "测试中...", StatusColor.Neutral);

            try
            {
                var userToken = GetUserToken();
                if (string.IsNullOrEmpty(userToken))
                {
                    SetStatusText(TxtConnectionStatus, "未设置 Token", StatusColor.Error);
                    return;
                }

                var authData = new
                {
                    app_id = APP_ID,
                    app_secret = APP_SECRET,
                    user_token = userToken
                };

                var result = await _apiClient.PostAsync<AuthWithTokenResponse>(
                    "/api/whiteboard/framework/auth-with-token",
                    authData,
                    requireAuth: false);

                if (result == null || !result.Success)
                    throw new Exception("认证响应失败");

                _currentWhiteboards = result.Whiteboards ?? new List<WhiteboardInfo>();
                _currentUser = result.User;

                SetStatusText(TxtConnectionStatus, $"已连接 (找到 {_currentWhiteboards.Count} 个白板)", StatusColor.Success);
                LoadClasses(_currentWhiteboards, result.User);
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(GetUserToken()) && GetUserToken().Length < 10)
                    LogHelper.WriteLogToFile("Token 格式可能不正确（长度过短，至少需要 10 个字符）", LogHelper.LogType.Error);

                LogHelper.WriteLogToFile($"Dlass API 连接测试失败: {ex.Message}", LogHelper.LogType.Error);
                SetStatusText(TxtConnectionStatus, "连接失败", StatusColor.Error);

                CmbClassSelection.Items.Clear();
                CmbClassSelection.Items.Add("（无可用班级）");
                CmbClassSelection.SelectedIndex = 0;
                CmbClassSelection.IsEnabled = false;
                _currentWhiteboards.Clear();
            }
        }

        private void ResetDlassConnectionState()
        {
            CmbClassSelection.Items.Clear();
            CmbClassSelection.Items.Add("（等待连接）");
            CmbClassSelection.SelectedIndex = 0;
            CmbClassSelection.IsEnabled = false;
            _currentWhiteboards.Clear();
            _currentUser = null;
            SetStatusText(TxtConnectionStatus, "未连接", StatusColor.Neutral);
        }

        private enum StatusColor
        {
            Neutral,
            Success,
            Error
        }

        private static void SetStatusText(System.Windows.Controls.TextBlock textBlock, string text, StatusColor color)
        {
            textBlock.Text = text;
            textBlock.Foreground = color switch
            {
                StatusColor.Success => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94)),
                StatusColor.Error => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)),
                _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(161, 161, 170))
            };
        }

        #endregion
    }

    public class AuthWithTokenResponse
    {
        [Newtonsoft.Json.JsonProperty("success")]
        public bool Success { get; set; }

        [Newtonsoft.Json.JsonProperty("whiteboards")]
        public List<WhiteboardInfo> Whiteboards { get; set; }

        [Newtonsoft.Json.JsonProperty("count")]
        public int Count { get; set; }

        [Newtonsoft.Json.JsonProperty("user")]
        public UserInfo User { get; set; }
    }

    public class WhiteboardInfo
    {
        [Newtonsoft.Json.JsonProperty("id")]
        public int Id { get; set; }

        [Newtonsoft.Json.JsonProperty("name")]
        public string Name { get; set; }

        [Newtonsoft.Json.JsonProperty("board_id")]
        public string BoardId { get; set; }

        [Newtonsoft.Json.JsonProperty("secret_key")]
        public string SecretKey { get; set; }

        [Newtonsoft.Json.JsonProperty("class_name")]
        public string ClassName { get; set; }

        [Newtonsoft.Json.JsonProperty("class_id")]
        public int ClassId { get; set; }

        [Newtonsoft.Json.JsonProperty("is_online")]
        public bool IsOnline { get; set; }

        [Newtonsoft.Json.JsonProperty("last_heartbeat")]
        public string LastHeartbeat { get; set; }

        [Newtonsoft.Json.JsonProperty("created_at")]
        public string CreatedAt { get; set; }
    }

    public class UserInfo
    {
        [Newtonsoft.Json.JsonProperty("id")]
        public int Id { get; set; }

        [Newtonsoft.Json.JsonProperty("username")]
        public string Username { get; set; }

        [Newtonsoft.Json.JsonProperty("email")]
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
