using Ink_Canvas.Helpers;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Windows;

namespace Ink_Canvas
{
    /// <summary>
    /// 处理 icc: URL 协议命令
    /// 支持：收纳/展开/切换、彻底隐藏、点名/计时器/白板、工具状态切换与查询、配置方案列表与切换。
    /// 配置方案：icc://config-profile/list 输出列表到 %TEMP%\InkCanvasConfigProfileList.json；
    ///          icc://config-profile/switch?name=方案名 切换方案，结果写入 %TEMP%\InkCanvasConfigProfileSwitchResult.txt。
    /// </summary>
    public partial class MainWindow
    {
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

                switch (pathLower)
                {
                    case "fold":
                        if (!isFloatingBarFolded)
                        {
                            FoldFloatingBar_MouseUp(new object(), null);
                            ShowNotification("已进入收纳模式");
                        }
                        return;
                    case "unfold":
                    case "show":
                        if (isFloatingBarFolded)
                        {
                            UnFoldFloatingBar_MouseUp(new object(), null);
                            ShowNotification("已退出收纳模式");
                        }
                        return;
                    case "toggle":
                        if (isFloatingBarFolded)
                        {
                            UnFoldFloatingBar_MouseUp(new object(), null);
                            ShowNotification("已退出收纳模式");
                        }
                        else
                        {
                            FoldFloatingBar_MouseUp(new object(), null);
                            ShowNotification("已进入收纳模式");
                        }
                        return;
                    case "thoroughhideon":
                        Settings.Automation.ThoroughlyHideWhenFolded = true;
                        SaveSettingsToFile();
                        ShowNotification("已开启：收起时彻底隐藏");
                        if (isFloatingBarFolded)
                            this.Visibility = Visibility.Hidden;
                        return;
                    case "thoroughhideoff":
                        Settings.Automation.ThoroughlyHideWhenFolded = false;
                        SaveSettingsToFile();
                        ShowNotification("已关闭：收起时彻底隐藏");
                        this.Visibility = Visibility.Visible;
                        return;
                    case "thoroughhidetoggle":
                        Settings.Automation.ThoroughlyHideWhenFolded = !Settings.Automation.ThoroughlyHideWhenFolded;
                        SaveSettingsToFile();
                        ShowNotification(Settings.Automation.ThoroughlyHideWhenFolded ? "已开启：收起时彻底隐藏" : "已关闭：收起时彻底隐藏");
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
                    ShowNotification($"切换失败：方案「{profileName}」不存在");
                    return;
                }
                _lastAppliedProfileName = profileName.Trim();
                ReloadSettingsFromFile();
                RefreshConfigProfileList();
                File.WriteAllText(resultPath, "ok", System.Text.Encoding.UTF8);
                ShowNotification($"已通过 URI 切换至方案「{profileName}」");
                LogHelper.WriteLogToFile($"URI 已切换配置方案: {profileName}", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(resultPath, "error: " + ex.Message, System.Text.Encoding.UTF8); } catch { }
                LogHelper.WriteLogToFile($"URI 切换配置方案失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }
    }
}
