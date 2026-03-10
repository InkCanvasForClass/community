using Ink_Canvas.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews
{
    public partial class SecurityPanel : SettingsPanelBase
    {
        /// <summary>
        /// 初始化 SecurityPanel 实例并构建其界面组件。
        /// </summary>
        public SecurityPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 从 MainWindow.Settings 将安全相关设置加载并同步到面板的开关控件上。
        /// </summary>
        /// <remarks>
        /// 确保 MainWindow.Settings.Security 存在（若为 null 则创建），在加载期间暂时禁用变更处理以避免触发回调，设置各个开关的状态并更新与密码相关的 UI 状态；任何加载期间的异常会被捕获并静默忽略。
        /// </remarks>
        public override void LoadSettings()
        {
            if (MainWindow.Settings == null) return;
            if (MainWindow.Settings.Security == null) MainWindow.Settings.Security = new Security();

            _isLoaded = false;
            try
            {
                var sec = MainWindow.Settings.Security;

                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchPasswordEnabled"), sec.PasswordEnabled);
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchRequirePasswordOnExit"), sec.RequirePasswordOnExit);
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchRequirePasswordOnEnterSettings"), sec.RequirePasswordOnEnterSettings);
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchRequirePasswordOnResetConfig"), sec.RequirePasswordOnResetConfig);
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnableProcessProtection"), sec.EnableProcessProtection);

                UpdatePasswordUiState();
            }
            catch
            {
            }
            _isLoaded = true;
        }

        /// <summary>
        /// 更新与主密码相关的界面控件的可用性：根据当前设置启用或禁用“设置/更改密码”按钮及相关用途开关。
        /// </summary>
        /// <remarks>
        /// 当全局安全设置的 PasswordEnabled 为 true 时，启用 BtnSetOrChangePassword 以及以下用途开关：
        /// ToggleSwitchRequirePasswordOnExit、ToggleSwitchRequirePasswordOnEnterSettings、ToggleSwitchRequirePasswordOnResetConfig；
        /// 否则禁用它们以阻止操作。
        /// </remarks>
        private void UpdatePasswordUiState()
        {
            var sec = MainWindow.Settings?.Security;
            var enabled = sec != null && sec.PasswordEnabled;

            if (BtnSetOrChangePassword != null) BtnSetOrChangePassword.IsEnabled = enabled;

            // 用途开关：仅在启用密码功能时可操作
            var usageEnabled = enabled;
            var t1 = FindToggleSwitch("ToggleSwitchRequirePasswordOnExit");
            var t2 = FindToggleSwitch("ToggleSwitchRequirePasswordOnEnterSettings");
            var t3 = FindToggleSwitch("ToggleSwitchRequirePasswordOnResetConfig");
            if (t1 != null) t1.IsEnabled = usageEnabled;
            if (t2 != null) t2.IsEnabled = usageEnabled;
            if (t3 != null) t3.IsEnabled = usageEnabled;
        }

        /// <summary>
        /// 根据切换项标识更新对应的安全设置并保存变更。
        /// </summary>
        /// <remarks>
        /// 对于启用或禁用主密码会在必要时弹出密码设置或验证对话框；对进程保护的修改会同时应用到 ProcessProtectionManager。方法会在成功变更后持久化设置并更新相关 UI 状态，若用户在交互中取消，则会恢复切换控件到原始状态。
        /// </remarks>
        /// <param name="tag">切换项的标识字符串，支持的值：`"PasswordEnabled"`、`"RequirePasswordOnExit"`、`"RequirePasswordOnEnterSettings"`、`"RequirePasswordOnResetConfig"`、`"EnableProcessProtection"`。</param>
        /// <param name="newState">切换的新布尔状态：`true` 表示启用，`false` 表示禁用。</param>
        protected override async void HandleToggleSwitchChange(string tag, bool newState)
        {
            if (MainWindow.Settings == null) return;
            if (MainWindow.Settings.Security == null) MainWindow.Settings.Security = new Security();
            var sec = MainWindow.Settings.Security;

            switch (tag)
            {
                case "PasswordEnabled":
                    if (newState)
                    {
                        var havePassword = SecurityManager.HasPasswordConfigured(MainWindow.Settings);

                        if (!havePassword)
                        {
                            var pwd = await SecurityManager.PromptSetNewPasswordAsync(Window.GetWindow(this));
                            if (string.IsNullOrEmpty(pwd))
                            {
                                _isLoaded = false;
                                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchPasswordEnabled"), false);
                                _isLoaded = true;
                                return;
                            }
                            SecurityManager.SetPassword(MainWindow.Settings, pwd);
                        }

                        sec.PasswordEnabled = true;
                        MainWindow.SaveSettingsToFile();
                        UpdatePasswordUiState();
                    }
                    else
                    {
                        // 关闭：需要输入当前密码确认（已设置密码时）
                        if (SecurityManager.HasPasswordConfigured(MainWindow.Settings))
                        {
                            bool ok = await SecurityManager.PromptAndVerifyAsync(MainWindow.Settings, Window.GetWindow(this),
                                "关闭安全密码", "请输入当前密码以关闭安全密码功能。");
                            if (!ok)
                            {
                                _isLoaded = false;
                                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchPasswordEnabled"), true);
                                _isLoaded = true;
                                return;
                            }
                        }

                        sec.PasswordEnabled = false;
                        SecurityManager.ClearPassword(MainWindow.Settings);
                        MainWindow.SaveSettingsToFile();
                        UpdatePasswordUiState();
                    }
                    break;

                case "RequirePasswordOnExit":
                    sec.RequirePasswordOnExit = newState;
                    MainWindow.SaveSettingsToFile();
                    break;
                case "RequirePasswordOnEnterSettings":
                    sec.RequirePasswordOnEnterSettings = newState;
                    MainWindow.SaveSettingsToFile();
                    break;
                case "RequirePasswordOnResetConfig":
                    sec.RequirePasswordOnResetConfig = newState;
                    MainWindow.SaveSettingsToFile();
                    break;
                case "EnableProcessProtection":
                    sec.EnableProcessProtection = newState;
                    MainWindow.SaveSettingsToFile();
                    ProcessProtectionManager.SetEnabled(newState);
                    break;
            }
        }

        /// <summary>
        /// 处理选项按钮组的选择更改。此面板不包含选项按钮组，因此不会执行任何操作。
        /// </summary>
        /// <param name="group">选项组的标识（未使用）。</param>
        /// <param name="value">被选中的值（未使用）。</param>
        protected override void HandleOptionChange(string group, string value)
        {
            // 本面板无选项按钮组
        }

        /// <summary>
        /// 处理切换开关的点击事件。
        /// </summary>
        protected override void ToggleSwitch_Click(object sender, RoutedEventArgs e)
        {
            base.ToggleSwitch_Click(sender, e);
        }

        /// <summary>
        /// 向用户弹出设置或更改密码的对话框；当用户输入非空新密码时，将该密码保存到设置中、启用密码功能、持久化设置并更新密码相关的 UI 状态。
        /// </summary>
        private async void BtnSetOrChangePassword_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Settings == null) return;
            if (MainWindow.Settings.Security == null) MainWindow.Settings.Security = new Security();

            var owner = Window.GetWindow(this);

            var newPwd = await SecurityManager.PromptChangePasswordAsync(MainWindow.Settings, owner);
            if (!string.IsNullOrEmpty(newPwd))
            {
                SecurityManager.SetPassword(MainWindow.Settings, newPwd);
                MainWindow.Settings.Security.PasswordEnabled = true;
                MainWindow.SaveSettingsToFile();
                UpdatePasswordUiState();
            }
        }

        public event EventHandler<RoutedEventArgs> IsTopBarNeedShadowEffect;
        public event EventHandler<RoutedEventArgs> IsTopBarNeedNoShadowEffect;

        /// <summary>
        /// 根据 ScrollViewer 的垂直滚动偏移触发顶部栏阴影显示或隐藏事件。
        /// </summary>
        /// <param name="sender">触发事件的 ScrollViewer 控件。</param>
        /// <param name="e">滚动更改的事件参数（未被方法使用）。</param>
        private void ScrollViewerEx_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = (ScrollViewer)sender;
            if (scrollViewer.VerticalOffset >= 10) IsTopBarNeedShadowEffect?.Invoke(this, new RoutedEventArgs());
            else IsTopBarNeedNoShadowEffect?.Invoke(this, new RoutedEventArgs());
        }

        /// <summary>
        /// 将当前主题应用到此面板并重新加载面板设置。
        /// </summary>
        /// <remarks>
        /// 在应用主题或重载设置时抛出的异常会被捕获并忽略，不会向上抛出。
        /// </remarks>
        public void ApplyTheme()
        {
            try
            {
                ThemeHelper.ApplyThemeToControl(this);
                LoadSettings();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }
    }
}
