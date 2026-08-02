using Ink_Canvas.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace Ink_Canvas
{
    /// <summary>
    /// 处理 icc: URL 协议命令
    /// 支持：收纳/展开/切换、彻底隐藏、点名/计时器/白板、工具状态切换与查询、配置方案列表与切换。
    /// 支持：重启/退出、清空墨迹、撤销/重做、翻页/新建/删除白板页、截图、选择工具。
    /// 配置方案：icc://config-profile/list 输出列表到 %TEMP%\InkCanvasConfigProfileList.json；
    ///          icc://config-profile/switch?name=方案名 切换方案，结果写入 %TEMP%\InkCanvasConfigProfileSwitchResult.txt。
    /// </summary>
    public partial class MainWindow
    {
        // 防止同一命令在短时间内重复执行
        private static readonly HashSet<string> _uriNonRepeatableCommands = new HashSet<string>
        {
            "restart", "restart/admin", "restart/normal", "exit", "quit"
        };
        private static readonly Dictionary<string, DateTime> _uriCommandLastExecuted = new Dictionary<string, DateTime>();
        private static readonly TimeSpan _uriCommandDebounceWindow = TimeSpan.FromSeconds(3);

        public void HandleUriCommand(string uri)
        {
            try
            {
                if (string.IsNullOrEmpty(uri)) return;

                if (!Settings.Advanced.IsEnableUriScheme)
                {
                    LogHelper.WriteLogToFile($"URI 协议已禁用，忽略请求: {uri}", LogHelper.LogType.Warning);
                    return;
                }

                LogHelper.WriteLogToFile($"正在处理 URI 命令: {uri}", LogHelper.LogType.Event);

                string command = ParseUriCommand(uri);
                if (string.IsNullOrEmpty(command)) return;

                string path = command;
                string pathLower = path.ToLowerInvariant();

                // 防止危险命令（restart/exit等）在短时间内重复执行
                if (_uriNonRepeatableCommands.Contains(pathLower))
                {
                    if (_uriCommandLastExecuted.TryGetValue(pathLower, out DateTime lastTime)
                        && DateTime.Now - lastTime < _uriCommandDebounceWindow)
                    {
                        LogHelper.WriteLogToFile($"URI 命令被去重过滤（{DateTime.Now - lastTime} 内重复）: {pathLower}", LogHelper.LogType.Warning);
                        return;
                    }
                    _uriCommandLastExecuted[pathLower] = DateTime.Now;
                }

                switch (pathLower)
                {
                    case "fold":
                        if (!isFloatingBarFolded)
                        {
                            FoldFloatingBar_MouseUp(new object(), null);
                            ShowNotification(Properties.MainWindowStrings.Main_Uri_EnterFoldMode);
                        }
                        return;
                    case "unfold":
                    case "show":
                        if (isFloatingBarFolded)
                        {
                            UnFoldFloatingBar_MouseUp(new object(), null);
                            ShowNotification(Properties.MainWindowStrings.Main_Uri_ExitFoldMode);
                        }
                        return;
                    case "toggle":
                        if (isFloatingBarFolded)
                        {
                            UnFoldFloatingBar_MouseUp(new object(), null);
                            ShowNotification(Properties.MainWindowStrings.Main_Uri_ExitFoldMode);
                        }
                        else
                        {
                            FoldFloatingBar_MouseUp(new object(), null);
                            ShowNotification(Properties.MainWindowStrings.Main_Uri_EnterFoldMode);
                        }
                        return;
                    case "thoroughhideon":
                        Settings.Automation.ThoroughlyHideWhenFolded = true;
                        SaveSettingsToFile();
                        ShowNotification(Properties.MainWindowStrings.Main_Uri_HideOnFoldEnabled);
                        if (isFloatingBarFolded)
                            this.Visibility = Visibility.Hidden;
                        return;
                    case "thoroughhideoff":
                        Settings.Automation.ThoroughlyHideWhenFolded = false;
                        SaveSettingsToFile();
                        ShowNotification(Properties.MainWindowStrings.Main_Uri_HideOnFoldDisabled);
                        this.Visibility = Visibility.Visible;
                        return;
                    case "thoroughhidetoggle":
                        Settings.Automation.ThoroughlyHideWhenFolded = !Settings.Automation.ThoroughlyHideWhenFolded;
                        SaveSettingsToFile();
                        ShowNotification(Settings.Automation.ThoroughlyHideWhenFolded ? Properties.MainWindowStrings.Main_Uri_HideOnFoldEnabled : Properties.MainWindowStrings.Main_Uri_HideOnFoldDisabled);
                        if (isFloatingBarFolded)
                            this.Visibility = Settings.Automation.ThoroughlyHideWhenFolded ? Visibility.Hidden : Visibility.Visible;
                        return;
                    case "randone":
                        SymbolIconRandOne_MouseUp(null, null);
                        return;
                    case "rand":
                        SymbolIconRand_MouseUp(null, null);
                        return;
                    case "timer":
                        ImageCountdownTimer_MouseUp(null, null);
                        return;
                    case "whiteboard":
                    case "board":
                        ImageBlackboard_MouseUp(null, null);
                        return;
                    case "restart":
                        ShowNotification(Properties.MainWindowStrings.Main_Uri_Restart);
                        _ = Task.Delay(300).ContinueWith(_ => Dispatcher.Invoke(() => AppRestartHelper.RestartWithCurrentPrivileges()));
                        return;
                    case "restart/admin":
                        ShowNotification(Properties.MainWindowStrings.Main_Uri_RestartAdmin);
                        _ = Task.Delay(300).ContinueWith(_ => Dispatcher.Invoke(() => AppRestartHelper.RestartAsAdmin()));
                        return;
                    case "restart/normal":
                        ShowNotification(Properties.MainWindowStrings.Main_Uri_RestartNormal);
                        _ = Task.Delay(300).ContinueWith(_ => Dispatcher.Invoke(() => AppRestartHelper.RestartAsNormal()));
                        return;
                    case "exit":
                    case "quit":
                        ShowNotification(Properties.MainWindowStrings.Main_Uri_Exit);
                        _ = Task.Delay(300).ContinueWith(_ => Dispatcher.Invoke(() => ExitApplication(null, null)));
                        return;
                    case "clear":
                    case "clearink":
                        EraserPanelSymbolIconDelete_MouseUp(null, null);
                        ShowNotification(Properties.MainWindowStrings.Main_Uri_ClearInk);
                        return;
                    case "clearall":
                    case "clearinkandhistory":
                        BoardSymbolIconDeleteInkAndHistories_MouseUp(null, null);
                        ShowNotification(Properties.MainWindowStrings.Main_Uri_ClearInkAndHistory);
                        return;
                    case "undo":
                        SymbolIconUndo_MouseUp(null, null);
                        ShowNotification(Properties.MainWindowStrings.Main_Uri_Undo);
                        return;
                    case "redo":
                        SymbolIconRedo_MouseUp(null, null);
                        ShowNotification(Properties.MainWindowStrings.Main_Uri_Redo);
                        return;
                    case "nextpage":
                    case "page/next":
                        SwitchToNextPage();
                        ShowNotification(Properties.MainWindowStrings.Main_Uri_NextPage);
                        return;
                    case "previouspage":
                    case "prevpage":
                    case "page/previous":
                        SwitchToPreviousPage();
                        ShowNotification(Properties.MainWindowStrings.Main_Uri_PreviousPage);
                        return;
                    case "newpage":
                    case "page/add":
                        AddWhiteboardPage();
                        ShowNotification(Properties.MainWindowStrings.Main_Uri_NewPage);
                        return;
                    case "deletepage":
                    case "page/delete":
                        DeleteWhiteboardPage();
                        ShowNotification(Properties.MainWindowStrings.Main_Uri_DeletePage);
                        return;
                    case "screenshot":
                        ShowNotification(Properties.MainWindowStrings.Main_Uri_Screenshot);
                        _ = Task.Delay(300).ContinueWith(_ => Dispatcher.Invoke(async () => await CaptureScreenshotAndInsert()));
                        return;
                    case "freeze":
                    case "lock":
                    case "ink-freeze":
                    case "ink/lock":
                        FreezePage(GetUriFreezePageOrCurrent(uri), true);
                        return;
                    case "unfreeze":
                    case "unlock":
                    case "ink-unfreeze":
                    case "ink/unlock":
                        _ = UnfreezePageAsync(GetUriFreezePageOrCurrent(uri), skipVerification: true);
                        return;
                    case "freeze/start":
                    case "lock/start":
                    case "ink-freeze/start":
                    case "ink/lock/start":
                        HandleInkFreezeCourseStart(GetUriFreezePageOrCurrent(uri));
                        return;
                    case "freeze/end":
                    case "lock/end":
                    case "ink-freeze/end":
                    case "ink/lock/end":
                        HandleInkFreezeCourseEnd(GetUriFreezePageOrCurrent(uri, allowMissing: true));
                        return;
                    case "freeze/cancel":
                    case "lock/cancel":
                    case "ink-freeze/cancel":
                    case "ink/lock/cancel":
                        HandleInkFreezeCourseCancel();
                        return;
                }

                if (pathLower == "tool/state")
                {
                    string state = GetCurrentSelectedMode() ?? "cursor";
                    string stateFile = Path.Combine(Path.GetTempPath(), "InkCanvasToolState.txt");
                    File.WriteAllText(stateFile, state, System.Text.Encoding.UTF8);
                    return;
                }

                if (pathLower.StartsWith("tool/"))
                {
                    string tool = pathLower.Length > 5 ? pathLower.Substring(5).TrimEnd('/') : "";
                    switch (tool)
                    {
                        case "pen":
                        case "color":
                            PenIcon_Click(null, null);
                            break;
                        case "cursor":
                            CursorIcon_Click(null, null);
                            break;
                        case "eraser":
                            PenIcon_Click(null, null);
                            EraserIcon_Click(null, null);
                            break;
                        case "eraserbystrokes":
                        case "eraserstroke":
                            PenIcon_Click(null, null);
                            EraserIconByStrokes_Click(EraserByStrokes_Icon, null);
                            break;
                        case "select":
                        case "lasso":
                            SymbolIconSelect_MouseUp(null, null);
                            break;
                        default:
                            LogHelper.WriteLogToFile($"未知的 URI 工具: {tool}", LogHelper.LogType.Warning);
                            break;
                    }
                    return;
                }

                if (pathLower == "config-profile/list")
                {
                    WriteConfigProfileListToTemp();
                    return;
                }

                if (pathLower.StartsWith("config-profile/switch"))
                {
                    string profileName = GetUriQueryValue(uri, "name");
                    HandleUriConfigProfileSwitch(profileName);
                    return;
                }

                if (pathLower == "settings" || pathLower.StartsWith("settings/"))
                {
                    HandleUriSettingsNavigation(uri);
                    return;
                }

                if (pathLower.StartsWith("plugin/"))
                {
                    HandlePluginUriNavigation(uri, pathLower);
                    return;
                }

                LogHelper.WriteLogToFile($"未知的 URI 命令: {command}", LogHelper.LogType.Warning);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理 URI 命令时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private static string ParseUriCommand(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri) || !uri.Trim().StartsWith("icc:", StringComparison.OrdinalIgnoreCase))
                return "";

            if (Uri.TryCreate(uri, UriKind.Absolute, out Uri uriObj))
            {
                string host = (uriObj.Host ?? "").Trim().ToLowerInvariant();
                string path = (uriObj.AbsolutePath ?? "").Trim('/').ToLowerInvariant();
                if (!string.IsNullOrEmpty(host))
                    return string.IsNullOrEmpty(path) ? host : host + "/" + path;
                if (!string.IsNullOrEmpty(path))
                    return path;
            }

            string raw = uri.Trim().Substring(4).TrimStart('/').ToLowerInvariant();
            return raw;
        }

        private static string GetUriQueryValue(string uri, string key)
        {
            if (string.IsNullOrEmpty(uri) || string.IsNullOrEmpty(key)) return "";
            try
            {
                if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri u) || string.IsNullOrEmpty(u.Query))
                    return "";
                string q = u.Query.TrimStart('?');
                foreach (var pair in q.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = pair.Split(new[] { '=' }, 2, StringSplitOptions.None);
                    if (kv.Length == 2 && kv[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                        return Uri.UnescapeDataString(kv[1].Trim());
                }
            }
            catch (Exception ex) { LogHelper.WriteLogToFile($"解析 URI 参数失败: {ex.Message}", LogHelper.LogType.Warning); }
            return "";
        }

        private int GetUriFreezePageOrCurrent(string uri, bool allowMissing = false)
        {
            string pageText = GetUriQueryValue(uri, "page");
            if (int.TryParse(pageText, out int page) && page >= 0 && page <= 100)
                return page;

            return allowMissing ? -1 : GetCurrentFreezePageIndex();
        }

        private const string ConfigProfileListTempFile = "InkCanvasConfigProfileList.json";
        private const string ConfigProfileSwitchResultTempFile = "InkCanvasConfigProfileSwitchResult.txt";

        private void WriteConfigProfileListToTemp()
        {
            try
            {
                var names = ConfigProfileManager.ListProfileNames();
                var current = _lastAppliedProfileName ?? "";
                var payload = new { list = names, current = current };
                string path = Path.Combine(Path.GetTempPath(), ConfigProfileListTempFile);
                File.WriteAllText(path, JsonConvert.SerializeObject(payload, Formatting.Indented), System.Text.Encoding.UTF8);
                LogHelper.WriteLogToFile($"URI 已输出配置方案列表到: {path}", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"URI 输出配置方案列表失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void HandleUriConfigProfileSwitch(string profileName)
        {
            string resultPath = Path.Combine(Path.GetTempPath(), ConfigProfileSwitchResultTempFile);
            try
            {
                if (string.IsNullOrWhiteSpace(profileName))
                {
                    File.WriteAllText(resultPath, "error: 缺少参数 name", System.Text.Encoding.UTF8);
                    LogHelper.WriteLogToFile("URI 切换配置方案: 未指定方案名", LogHelper.LogType.Warning);
                    return;
                }
                if (!ConfigProfileManager.ApplyProfile(profileName.Trim()))
                {
                    File.WriteAllText(resultPath, "error: 方案不存在或应用失败", System.Text.Encoding.UTF8);
                    ShowNotification(string.Format(Properties.MainWindowStrings.Main_Uri_SchemeNotFound, profileName));
                    return;
                }
                _lastAppliedProfileName = profileName.Trim();
                ReloadSettingsFromFile();
                File.WriteAllText(resultPath, "ok", System.Text.Encoding.UTF8);
                ShowNotification(string.Format(Properties.MainWindowStrings.Main_Uri_SwitchedScheme, profileName));
                LogHelper.WriteLogToFile($"URI 已切换配置方案: {profileName}", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(resultPath, "error: " + ex.Message, System.Text.Encoding.UTF8); } catch { }
                LogHelper.WriteLogToFile($"URI 切换配置方案失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 打开设置窗口并导航到指定页面 / 设置项。
        /// URI 形式：icc://settings[ /&lt;PageTag&gt;][?key=&lt;SettingsJsonKey&gt;]
        /// 例如：icc://settings/CanvasPage?key=inkFadeSpeedMultiplier
        /// </summary>
        private void HandleUriSettingsNavigation(string uri)
        {
            try
            {
                if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri parsed))
                {
                    LogHelper.WriteLogToFile($"URI 设置导航失败：无法解析 {uri}", LogHelper.LogType.Warning);
                    return;
                }

                // 提取页面 tag：路径首段去掉前导 '/'
                string pageTag = parsed.AbsolutePath?.Trim('/') ?? string.Empty;
                if (string.IsNullOrEmpty(pageTag))
                {
                    pageTag = "HomePage";
                }

                string settingKey = GetUriQueryValue(uri, "key");

                // 优先复用已打开的设置窗口
                Windows.SettingsViews.SettingsWindow window = null;
                if (Application.Current != null)
                {
                    foreach (Window w in Application.Current.Windows)
                    {
                        if (w is Windows.SettingsViews.SettingsWindow sw)
                        {
                            window = sw;
                            break;
                        }
                    }
                }
                if (window == null)
                {
                    window = new Windows.SettingsViews.SettingsWindow();
                    // 跳过 Loaded 中默认导航到 HomePage 的行为，由本方法指定目标页
                    window.SuppressInitialNavigation = true;
                    window.Show();
                    // 强制窗口处于正常可见状态（避免新窗口被意外最小化）
                    if (window.WindowState == WindowState.Minimized)
                        window.WindowState = WindowState.Normal;
                    window.Activate();
                    // 同步到 BtnSettings_Click 使用的静态字段，避免软件按钮再开一个新窗口
                    _settingsWindow = window;
                    window.Closed += (s, args) =>
                    {
                        if (ReferenceEquals(_settingsWindow, window))
                            _settingsWindow = null;
                    };
                }
                else
                {
                    if (window.WindowState == WindowState.Minimized)
                        window.WindowState = WindowState.Normal;
                    window.Activate();
                    // 同步静态引用，确保软件按钮也能复用此窗口
                    if (_settingsWindow == null)
                    {
                        _settingsWindow = window;
                        window.Closed += (s, args) =>
                        {
                            if (ReferenceEquals(_settingsWindow, window))
                                _settingsWindow = null;
                        };
                    }
                }

                window.NavigateToPage(pageTag);

                // 选中对应导航项（菜单 + 子菜单 + 底部菜单）
                var navView = window.GetNavigationView();
                iNKORE.UI.WPF.Modern.Controls.NavigationViewItem navItem = null;
                foreach (var item in navView.MenuItems)
                {
                    if (item is iNKORE.UI.WPF.Modern.Controls.NavigationViewItem ni)
                    {
                        if ((ni.Tag as string) == pageTag)
                        {
                            navItem = ni;
                            break;
                        }
                        foreach (var child in ni.MenuItems)
                        {
                            if (child is iNKORE.UI.WPF.Modern.Controls.NavigationViewItem cni
                                && (cni.Tag as string) == pageTag)
                            {
                                ni.IsExpanded = true;
                                navItem = cni;
                                break;
                            }
                        }
                        if (navItem != null) break;
                    }
                }
                if (navItem == null)
                {
                    foreach (var item in navView.FooterMenuItems)
                    {
                        if (item is iNKORE.UI.WPF.Modern.Controls.NavigationViewItem ni
                            && (ni.Tag as string) == pageTag)
                        {
                            navItem = ni;
                            break;
                        }
                    }
                }
                if (navItem != null)
                {
                    navView.SelectedItem = navItem;
                }

                if (!string.IsNullOrEmpty(settingKey))
                {
                    // 设置挂起的高亮 key，等页面 Loaded 后再触发，避免可视树尚未构建导致高亮失效
                    window.SetPendingHighlightKey(settingKey);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"URI 设置导航失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 把 <c>icc://plugin/&lt;pluginId&gt;/&lt;subPath&gt;?&lt;query&gt;</c> 派发给注册的插件 URI 处理器。
        /// 未处理（插件未注册/未加载/处理器返回 false）时仅写日志，不弹通知。
        /// </summary>
        private void HandlePluginUriNavigation(string uri, string pathLower)
        {
            try
            {
                string rest = pathLower.Substring("plugin/".Length); // <pluginId>/<subPath> 或 <pluginId>
                string[] segs = rest.Split(new[] { '/' }, 2, StringSplitOptions.None);
                string pluginId = segs[0];
                string subPath = segs.Length > 1 ? segs[1] : "";

                bool handled = Plugins.PluginManager.Instance.TryDispatchUri(pluginId, subPath, uri);
                if (!handled)
                {
                    LogHelper.WriteLogToFile($"插件 URI 未被处理: {uri}（插件 {pluginId} 未注册处理器或处理器返回 false）", LogHelper.LogType.Warning);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"派发插件 URI 失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }
    }
}
