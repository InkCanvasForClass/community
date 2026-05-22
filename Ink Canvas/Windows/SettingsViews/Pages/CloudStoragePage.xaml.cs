using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
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
        private static string NoSavedTokenText => CloudStorageStrings.CloudStorage_NoSavedToken;

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
            InitializeClassSelectionPlaceholder(CloudStorageStrings.CloudStorage_WaitingForConnection);
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
                    var teacherName = user?.Username ?? CloudStorageStrings.CloudStorage_UnknownTeacher;
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
                    CmbClassSelection.Items.Add(CloudStorageStrings.CloudStorage_NoAvailableClasses);
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
                    MessageBox.Show(CloudStorageStrings.CloudStorage_PleaseEnterNewToken, CloudStorageStrings.CloudStorage_Tip, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                AddTokenToList(token);
                SaveUserToken(token);
                InitializeApiClient();
                RunWithoutUiEvents(LoadUserToken);
                MessageBox.Show(CloudStorageStrings.CloudStorage_TokenSavedAndSelected, CloudStorageStrings.CloudStorage_Success, MessageBoxButton.OK, MessageBoxImage.Information);
                _ = TestConnectionAsync();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存Token时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"{CloudStorageStrings.CloudStorage_SaveTokenError}{ex.Message}", CloudStorageStrings.CloudStorage_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClearToken_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CmbSavedTokens.SelectedItem == null ||
                    CmbSavedTokens.SelectedItem.ToString() == NoSavedTokenText)
                {
                    MessageBox.Show(CloudStorageStrings.CloudStorage_PleaseSelectToken, CloudStorageStrings.CloudStorage_Tip, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedToken = CmbSavedTokens.SelectedItem.ToString();
                var result = MessageBox.Show(CloudStorageStrings.CloudStorage_ConfirmDeleteToken, CloudStorageStrings.CloudStorage_Confirm, MessageBoxButton.YesNo, MessageBoxImage.Question);
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
                InitializeClassSelectionPlaceholder(CloudStorageStrings.CloudStorage_WaitingForConnection);
                _currentWhiteboards.Clear();
                SetConnectionStatus(CloudStorageStrings.CloudStorage_NotConnected, Colors.Gray);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"删除Token时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"{CloudStorageStrings.CloudStorage_DeleteTokenError}{ex.Message}", CloudStorageStrings.CloudStorage_Error, MessageBoxButton.OK, MessageBoxImage.Error);
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

                MessageBox.Show(CloudStorageStrings.CloudStorage_WebDavSettingsSaved, CloudStorageStrings.CloudStorage_Success, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存WebDav设置时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"{CloudStorageStrings.CloudStorage_SaveWebDavError}{ex.Message}", CloudStorageStrings.CloudStorage_Error, MessageBoxButton.OK, MessageBoxImage.Error);
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
                CloudStorageStrings.CloudStorage_DlassRegistrationPrompt,
                CloudStorageStrings.CloudStorage_DlassRegistrationTitle,
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
                MessageBox.Show(CloudStorageStrings.CloudStorage_CannotOpenBrowser,
                    CloudStorageStrings.CloudStorage_Tip, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task TestConnectionAsync()
        {
            CancelConnectionTest();
            _connectionTestCts = new CancellationTokenSource();
            var cancellationToken = _connectionTestCts.Token;

            SetConnectionStatus(CloudStorageStrings.CloudStorage_Testing, Colors.Gray);

            try
            {
                if (_apiClient == null)
                {
                    InitializeApiClient();
                }

                var userToken = GetUserToken();
                if (string.IsNullOrEmpty(userToken))
                {
                    SetConnectionStatus(CloudStorageStrings.CloudStorage_TokenNotSet, Colors.Red);
                    InitializeClassSelectionPlaceholder(CloudStorageStrings.CloudStorage_NoAvailableClasses);
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

                SetConnectionStatus(string.Format(CloudStorageStrings.CloudStorage_ConnectedFormat, whiteboards.Count), Color.FromRgb(34, 197, 94));
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
                SetConnectionStatus(CloudStorageStrings.CloudStorage_ConnectionFailed, Colors.Red);
                _currentWhiteboards.Clear();
                InitializeClassSelectionPlaceholder(CloudStorageStrings.CloudStorage_NoAvailableClasses);
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
                TxtTokenStatus.Text = CloudStorageStrings.CloudStorage_TokenSelected;
                TxtTokenStatus.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            }
            else
            {
                TxtTokenStatus.Text = CloudStorageStrings.CloudStorage_TokenNotSet;
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
