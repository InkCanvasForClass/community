using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace Ink_Canvas.Windows
{
    /// <summary>
    /// DlassSettingsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class DlassSettingsWindow : Window
    {
        private const string APP_ID = "app_WkjocWqsrVY7T6zQV2CfiA";
        private const string APP_SECRET = "o7dx5b5ASGUMcM72PCpmRQYAhSijqaOVHoGyBK0IxbA";

        private DlassApiClient _apiClient;
        private List<WhiteboardInfo> _currentWhiteboards = new List<WhiteboardInfo>();
        private UserInfo _currentUser;

        public DlassSettingsWindow(MainWindow mainWindow = null)
        {
            InitializeComponent();
            
            // 初始化班级下拉框
            CmbClassSelection.Items.Clear();
            CmbClassSelection.Items.Add("（等待连接）");
            CmbClassSelection.SelectedIndex = 0;
            CmbClassSelection.IsEnabled = false;
            
            // 加载保存的token
            LoadUserToken();
            
            // 加载自动上传设置
            LoadAutoUploadSettings();
            
            // 初始化API客户端（优先使用用户token）
            InitializeApiClient();
            
            // 窗口关闭时释放资源
            Closed += (s, e) => _apiClient?.Dispose();
            
            // 测试连接
            _ = TestConnectionAsync();
        }

        /// <summary>
        /// 初始化API客户端
        /// </summary>
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
            
            if (!string.IsNullOrEmpty(userToken))
            {
                _apiClient = new DlassApiClient(APP_ID, APP_SECRET, baseUrl: apiBaseUrl, userToken: userToken);
            }
            else
            {
                _apiClient = new DlassApiClient(APP_ID, APP_SECRET, baseUrl: apiBaseUrl);
            }
        }

        /// <summary>
        /// 获取用户token
        /// </summary>
        private string GetUserToken()
        {
            if (MainWindow.Settings?.Dlass != null)
            {
                return MainWindow.Settings.Dlass.UserToken ?? string.Empty;
            }
            return string.Empty;
        }

        /// <summary>
        /// 获取保存的Token列表
        /// </summary>
        private List<string> GetSavedTokens()
        {
            if (MainWindow.Settings?.Dlass != null)
            {
                return MainWindow.Settings.Dlass.SavedTokens ?? new List<string>();
            }
            return new List<string>();
        }

        /// <summary>
        /// 加载用户token到UI
        /// </summary>
        private void LoadUserToken()
        {
            var savedTokens = GetSavedTokens();
            var currentToken = GetUserToken();
            
            CmbSavedTokens.Items.Clear();
            if (savedTokens.Count > 0)
            {
                foreach (var token in savedTokens)
                {
                    CmbSavedTokens.Items.Add(token);
                }
                if (!string.IsNullOrEmpty(currentToken))
                {
                    var index = savedTokens.IndexOf(currentToken);
                    if (index >= 0)
                    {
                        CmbSavedTokens.SelectedIndex = index;
                    }
                    else
                    {
                        CmbSavedTokens.SelectedIndex = 0;
                    }
                }
                else if (CmbSavedTokens.Items.Count > 0)
                {
                    CmbSavedTokens.SelectedIndex = 0;
                }
            }
            else
            {
                CmbSavedTokens.Items.Add("（无保存的Token）");
                CmbSavedTokens.SelectedIndex = 0;
                CmbSavedTokens.IsEnabled = false;
            }
            
            TxtNewToken.Text = string.Empty;
            
            if (!string.IsNullOrEmpty(currentToken))
            {
                TxtTokenStatus.Text = "已选择Token";
                TxtTokenStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94));
            }
            else
            {
                TxtTokenStatus.Text = "未设置Token";
                TxtTokenStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(161, 161, 170));
            }
        }

        /// <summary>
        /// 保存用户token
        /// </summary>
        private void SaveUserToken(string token)
        {
            if (MainWindow.Settings?.Dlass != null)
            {
                MainWindow.Settings.Dlass.UserToken = token ?? string.Empty;
                MainWindow.SaveSettingsToFile();
            }
        }

        /// <summary>
        /// 添加Token到保存列表
        /// </summary>
        private void AddTokenToList(string token)
        {
            if (MainWindow.Settings?.Dlass != null)
            {
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
        }

        /// <summary>
        /// 从列表删除Token
        /// </summary>
        private void RemoveTokenFromList(string token)
        {
            if (MainWindow.Settings?.Dlass != null && MainWindow.Settings.Dlass.SavedTokens != null)
            {
                MainWindow.Settings.Dlass.SavedTokens.Remove(token);
                MainWindow.SaveSettingsToFile();
            }
        }

        /// <summary>
        /// 加载班级列表到下拉框
        /// </summary>
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
                    var className = group.Key;
                    var displayText = $"{teacherName} - {className}";
                    CmbClassSelection.Items.Add(new ClassSelectionItem
                    {
                        DisplayText = displayText,
                        ClassName = className,
                        TeacherName = teacherName
                    });
                }
                
                var savedClassName = MainWindow.Settings?.Dlass?.SelectedClassName ?? string.Empty;
                if (!string.IsNullOrEmpty(savedClassName))
                {
                    var savedItem = CmbClassSelection.Items.Cast<ClassSelectionItem>()
                        .FirstOrDefault(item => item.ClassName == savedClassName);
                    if (savedItem != null)
                    {
                        CmbClassSelection.SelectedItem = savedItem;
                    }
                    else if (CmbClassSelection.Items.Count > 0)
                    {
                        CmbClassSelection.SelectedIndex = 0;
                    }
                }
                else if (CmbClassSelection.Items.Count > 0)
                {
                    CmbClassSelection.SelectedIndex = 0;
                }
                
                CmbClassSelection.IsEnabled = true;
            }
            else
            {
                CmbClassSelection.Items.Add("（无可用班级）");
                CmbClassSelection.SelectedIndex = 0;
                CmbClassSelection.IsEnabled = false;
            }
        }

        /// <summary>
        /// 班级选择改变事件
        /// </summary>
        private void CmbClassSelection_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (CmbClassSelection.SelectedItem is ClassSelectionItem selectedItem)
                {
                    if (MainWindow.Settings?.Dlass != null)
                    {
                        MainWindow.Settings.Dlass.SelectedClassName = selectedItem.ClassName;
                        MainWindow.SaveSettingsToFile();
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"选择班级时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 加载自动上传设置
        /// </summary>
        private void LoadAutoUploadSettings()
        {
            try
            {
                if (MainWindow.Settings?.Dlass != null)
                {
                    ToggleSwitchAutoUploadNotes.IsOn = MainWindow.Settings.Dlass.IsAutoUploadNotes;
                    var delayMinutes = MainWindow.Settings.Dlass.AutoUploadDelayMinutes;
                    if (delayMinutes < 0 || delayMinutes > 60)
                    {
                        delayMinutes = 0;
                    }
                    TxtUploadDelayMinutes.Text = delayMinutes.ToString();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载自动上传设置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 自动上传开关切换事件
        /// </summary>
        private void ToggleSwitchAutoUploadNotes_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MainWindow.Settings?.Dlass != null)
                {
                    MainWindow.Settings.Dlass.IsAutoUploadNotes = ToggleSwitchAutoUploadNotes.IsOn;
                    MainWindow.SaveSettingsToFile();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存自动上传设置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 上传延迟时间输入框文本改变事件
        /// </summary>
        private void TxtUploadDelayMinutes_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                if (MainWindow.Settings?.Dlass != null && int.TryParse(TxtUploadDelayMinutes.Text, out int delayMinutes))
                {
                    // 限制范围在0-60分钟
                    if (delayMinutes < 0)
                    {
                        delayMinutes = 0;
                        TxtUploadDelayMinutes.Text = "0";
                    }
                    else if (delayMinutes > 60)
                    {
                        delayMinutes = 60;
                        TxtUploadDelayMinutes.Text = "60";
                    }
                    
                    MainWindow.Settings.Dlass.AutoUploadDelayMinutes = delayMinutes;
                    MainWindow.SaveSettingsToFile();
                }
                else if (string.IsNullOrWhiteSpace(TxtUploadDelayMinutes.Text))
                {
                    // 空文本时设置为0
                    if (MainWindow.Settings?.Dlass != null)
                    {
                        MainWindow.Settings.Dlass.AutoUploadDelayMinutes = 0;
                        MainWindow.SaveSettingsToFile();
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存上传延迟时间时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 上传延迟时间输入框预览文本输入事件（只允许数字）
        /// </summary>
        private void TxtUploadDelayMinutes_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        /// <summary>
        /// 标题栏拖动事件
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 下拉框选择改变事件
        /// </summary>
        private void CmbSavedTokens_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (CmbSavedTokens.SelectedItem != null && CmbSavedTokens.SelectedItem.ToString() != "（无保存的Token）")
                {
                    var selectedToken = CmbSavedTokens.SelectedItem.ToString();
                    SaveUserToken(selectedToken);
                    
                    _apiClient?.Dispose();
                    InitializeApiClient();
                    
                    TxtTokenStatus.Text = "已选择Token";
                    TxtTokenStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94));
                    
                    _ = TestConnectionAsync();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"选择Token时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 保存Token按钮点击事件
        /// </summary>
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
                
                _apiClient?.Dispose();
                InitializeApiClient();
                
                LoadUserToken();
                
                MessageBox.Show("Token已成功保存并已选择", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                
                _ = TestConnectionAsync();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存Token时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"保存Token时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 清除Token按钮点击事件
        /// </summary>
        private void BtnClearToken_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CmbSavedTokens.SelectedItem == null || CmbSavedTokens.SelectedItem.ToString() == "（无保存的Token）")
                {
                    MessageBox.Show("请先选择一个Token", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedToken = CmbSavedTokens.SelectedItem.ToString();
                var result = MessageBox.Show($"确定要删除已选中的Token吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    RemoveTokenFromList(selectedToken);
                    
                    if (GetUserToken() == selectedToken)
                    {
                        SaveUserToken(string.Empty);
                    }
                    
                    _apiClient?.Dispose();
                    InitializeApiClient();
                    
                    LoadUserToken();
                    
                    CmbClassSelection.Items.Clear();
                    CmbClassSelection.Items.Add("（等待连接）");
                    CmbClassSelection.SelectedIndex = 0;
                    CmbClassSelection.IsEnabled = false;
                    _currentWhiteboards.Clear();
                    _currentUser = null;
                    
                    TxtConnectionStatus.Text = "未连接";
                    TxtConnectionStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(161, 161, 170));
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"删除Token时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"删除Token时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 测试Token连接按钮点击事件
        /// </summary>
        private async void BtnTestToken_Click(object sender, RoutedEventArgs e)
        {
            await TestConnectionAsync();
        }

        /// <summary>
        /// 保存按钮点击事件
        /// </summary>
        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: 根据实际API文档实现保存逻辑
                // 示例：保存设置到服务器
                // var settings = new { ... };
                // await _apiClient.PostAsync<ApiResponse>("/api/settings", settings);
                
                MessageBox.Show("设置已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存设置时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"保存设置时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 测试API连接
        /// </summary>
        private async Task TestConnectionAsync()
        {
            Dispatcher.Invoke(() =>
            {
                TxtConnectionStatus.Text = "测试中...";
                TxtConnectionStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(161, 161, 170)); // 灰色
            });

            try
            {
                var userToken = GetUserToken();
                if (string.IsNullOrEmpty(userToken))
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtConnectionStatus.Text = "未设置Token";
                        TxtConnectionStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)); // 红色
                    });
                    return;
                }

                // 根据文档，使用 auth-with-token 接口验证token
                // 此接口需要POST请求，包含app_id, app_secret和user_token
                try
                {
                    var authData = new
                    {
                        app_id = APP_ID,
                        app_secret = APP_SECRET,
                        user_token = userToken
                    };
                    
                    var result = await _apiClient.PostAsync<AuthWithTokenResponse>("/api/whiteboard/framework/auth-with-token", authData, requireAuth: false);
                    
                    if (result != null && result.Success)
                    {
                        var whiteboards = result.Whiteboards ?? new List<WhiteboardInfo>();
                        _currentWhiteboards = whiteboards;
                        _currentUser = result.User;
                        var whiteboardCount = whiteboards.Count;
                        
                        Dispatcher.Invoke(() =>
                        {
                            TxtConnectionStatus.Text = $"已连接 (找到 {whiteboardCount} 个白板)";
                            TxtConnectionStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94));
                            
                            // 加载班级列表
                            LoadClasses(whiteboards, result.User);
                        });
                    }
                    else
                    {
                        throw new Exception("认证响应失败");
                    }
                }
                catch (Exception ex)
                {
                    if (userToken.Length < 10)
                    {
                        throw new Exception("Token格式可能不正确（长度过短，至少需要10个字符）");
                    }
                    
                    LogHelper.WriteLogToFile($"Token验证失败: {ex.Message}", LogHelper.LogType.Error);
                    throw;
                }
                
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"Dlass API连接测试失败: {ex.Message}", LogHelper.LogType.Error);
                Dispatcher.Invoke(() =>
                {
                    TxtConnectionStatus.Text = "连接失败";
                    TxtConnectionStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
                    
                    // 清空班级列表
                    CmbClassSelection.Items.Clear();
                    CmbClassSelection.Items.Add("（无可用班级）");
                    CmbClassSelection.SelectedIndex = 0;
                    CmbClassSelection.IsEnabled = false;
                    _currentWhiteboards.Clear();
                });
            }
        }
    }
    
    #region API响应模型
    
    /// <summary>
    /// auth-with-token接口响应模型
    /// </summary>
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
    
    /// <summary>
    /// 白板信息模型
    /// </summary>
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
    
    /// <summary>
    /// 用户信息模型
    /// </summary>
    public class UserInfo
    {
        [Newtonsoft.Json.JsonProperty("id")]
        public int Id { get; set; }
        
        [Newtonsoft.Json.JsonProperty("username")]
        public string Username { get; set; }
        
        [Newtonsoft.Json.JsonProperty("email")]
        public string Email { get; set; }
    }
    
    /// <summary>
    /// 班级选择项
    /// </summary>
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
    
    #endregion
}

