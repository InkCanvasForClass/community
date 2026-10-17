using Ink_Canvas.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Helpers
{
    internal partial class AutoUpdateHelper
    {

        // 检测URL延迟
        private static async Task<long> GetUrlDelay(string url)
        {
            try
            {
                // 检测是否为Windows 7
                var osVersion = Environment.OSVersion;
                bool isWindows7 = osVersion.Version.Major == 6 && osVersion.Version.Minor == 1;

                if (isWindows7)
                {
                    // Windows 7使用特殊配置
                    using (var handler = new HttpClientHandler())
                    {
                        // 配置HttpClientHandler以支持Windows 7
                        handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
                            // 只放行"已构建链但系统报 RemoteCertificateChainErrors（含过期/未吊销检查受限）"；
                            // 链不信任、主机名不匹配、UntrustedRoot 等严格错误继续走系统校验，
                            // 避免 MITM 在客户端网络内任意注入"更新包"。
                            sslPolicyErrors == System.Net.Security.SslPolicyErrors.None
                            || sslPolicyErrors == System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors;

                        using (var client = new HttpClient(handler))
                        {
                            client.Timeout = TimeSpan.FromSeconds(5);
                            var sw = Stopwatch.StartNew();
                            var resp = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                            sw.Stop();
                            if (resp.IsSuccessStatusCode)
                                return sw.ElapsedMilliseconds;
                        }
                    }
                }
                else
                {
                    // 其他Windows版本使用标准配置
                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(5);
                        var sw = Stopwatch.StartNew();
                        var resp = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                        sw.Stop();
                        if (resp.IsSuccessStatusCode)
                            return sw.ElapsedMilliseconds;
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            return -1;
        }

        // 检测线路组延迟，返回最快组（保持向后兼容）
        private static async Task<UpdateLineGroup> GetFastestLineGroup(UpdateChannel channel)
        {
            var availableGroups = await GetAvailableLineGroupsOrdered(channel);
            return availableGroups.Count > 0 ? availableGroups[0] : null;
        }

        // 获取所有可用线路组，按延迟排序
        public static async Task<List<UpdateLineGroup>> GetAvailableLineGroupsOrdered(UpdateChannel channel)
        {
            var cached = TryGetCachedOrderedGroups(channel);
            if (cached != null) return cached;
            var groups = ChannelLineGroups[channel];

            LogHelper.WriteLogToFile($"AutoUpdate | 开始并发检测通道 {channel} 下所有线路组延迟...");

            var testTasks = new List<Task<(UpdateLineGroup group, long delay)>>();
            foreach (var group in groups)
            {
                var testUrl = GetLineGroupTestUrl(group);

                if (string.IsNullOrEmpty(testUrl))
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 线路组 {group.GroupName} 缺少可用测速地址，跳过", LogHelper.LogType.Warning);
                    continue;
                }

                testTasks.Add(MeasureLineGroupDelayAsync(group, testUrl));
            }

            var availableGroups = new List<(UpdateLineGroup group, long delay)>();
            var testResults = await Task.WhenAll(testTasks);

            foreach (var result in testResults)
            {
                var group = result.group;
                var delay = result.delay;
                if (delay >= 0)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 线路组 {group.GroupName} 延迟: {delay}ms");
                    availableGroups.Add((group, delay));
                }
                else
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 线路组 {group.GroupName} 不可用", LogHelper.LogType.Warning);
                }
            }

            // 按延迟排序，延迟最小的排在前面
            var orderedGroups = availableGroups
                .OrderBy(x => x.delay)
                .Select(x => x.group)
                .ToList();

            var inkeysGroup = orderedGroups.FirstOrDefault(g => g.GroupName == "inkeys");
            if (inkeysGroup != null)
            {
                orderedGroups.Remove(inkeysGroup);
                orderedGroups.Insert(0, inkeysGroup);
                LogHelper.WriteLogToFile("AutoUpdate | inkeys线路组已默认优先");
            }

            if (orderedGroups.Count > 0)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 找到 {orderedGroups.Count} 个可用线路组，按延迟排序:");
                for (int i = 0; i < orderedGroups.Count; i++)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | {i + 1}. {orderedGroups[i].GroupName}");
                }
            }
            else
            {
                LogHelper.WriteLogToFile("AutoUpdate | 所有线路组均不可用", LogHelper.LogType.Error);
            }

            CacheOrderedGroups(channel, orderedGroups);
            return orderedGroups;
        }

        private static string GetLineGroupTestUrl(UpdateLineGroup group)
        {
            if (group.GroupName == "智教联盟" || group.GroupName == "inkeys" || group.GroupName == "cnb")
            {
                try
                {
                    if (!string.IsNullOrEmpty(group.DownloadUrlFormat))
                    {
                        var testUrl = group.DownloadUrlFormat.Replace("{0}", "test");
                        return AppendX64SuffixBeforeZipExtension(testUrl);
                    }
                }
                catch
                {
                    return null;
                }
            }

            return group.VersionUrl;
        }

        private static async Task<(UpdateLineGroup group, long delay)> MeasureLineGroupDelayAsync(UpdateLineGroup group, string testUrl)
        {
            var delay = group.GroupName == "智教联盟" || group.GroupName == "inkeys" || group.GroupName == "cnb"
                ? await GetDownloadUrlDelay(testUrl)
                : await GetUrlDelay(testUrl);
            return (group, delay);
        }

        // 缓存按延迟排序后的线路组，避免短时间内重复测速
        private static readonly Dictionary<UpdateChannel, (List<UpdateLineGroup> groups, DateTime cachedAt)> _orderedGroupsCache
            = new Dictionary<UpdateChannel, (List<UpdateLineGroup>, DateTime)>();
        private static readonly TimeSpan _orderedGroupsCacheTtl = TimeSpan.FromMinutes(15);

        private static List<UpdateLineGroup> TryGetCachedOrderedGroups(UpdateChannel channel)
        {
            lock (_orderedGroupsCache)
            {
                if (_orderedGroupsCache.TryGetValue(channel, out var entry) &&
                    entry.groups != null && entry.groups.Count > 0 &&
                    DateTime.UtcNow - entry.cachedAt < _orderedGroupsCacheTtl)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 复用线路组延迟检测缓存（{entry.groups.Count} 个）");
                    return new List<UpdateLineGroup>(entry.groups);
                }
                return null;
            }
        }

        private static void CacheOrderedGroups(UpdateChannel channel, List<UpdateLineGroup> groups)
        {
            lock (_orderedGroupsCache)
            {
                _orderedGroupsCache[channel] = (new List<UpdateLineGroup>(groups), DateTime.UtcNow);
            }
        }

        public static void InvalidateOrderedGroupsCache()
        {
            lock (_orderedGroupsCache)
            {
                _orderedGroupsCache.Clear();
            }
        }

        private static async Task<long> GetDownloadUrlDelay(string url)
        {
            try
            {
                var osVersion = Environment.OSVersion;
                bool isWindows7 = osVersion.Version.Major == 6 && osVersion.Version.Minor == 1;

                if (isWindows7)
                {
                    using (var handler = new HttpClientHandler())
                    {
                        handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
                            // 只放行"已构建链但系统报 RemoteCertificateChainErrors（含过期/未吊销检查受限）"；
                            // 链不信任、主机名不匹配、UntrustedRoot 等严格错误继续走系统校验，
                            // 避免 MITM 在客户端网络内任意注入"更新包"。
                            sslPolicyErrors == System.Net.Security.SslPolicyErrors.None
                            || sslPolicyErrors == System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors;

                        using (var client = new HttpClient(handler))
                        {
                            client.Timeout = TimeSpan.FromSeconds(5);
                            var sw = Stopwatch.StartNew();
                            var resp = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                            sw.Stop();
                            return sw.ElapsedMilliseconds;
                        }
                    }
                }
                else
                {
                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(5);
                        var sw = Stopwatch.StartNew();
                        var resp = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                        sw.Stop();
                        return sw.ElapsedMilliseconds;
                    }
                }
            }
            catch
            {
                return -1;
            }
        }

        // 获取远程版本号
        private static async Task<string> GetRemoteVersion(string fileUrl)
        {
            // 检测是否为Windows 7
            var osVersion = Environment.OSVersion;
            bool isWindows7 = osVersion.Version.Major == 6 && osVersion.Version.Minor == 1;

            if (isWindows7)
            {
                // Windows 7使用特殊配置
                using (var handler = new HttpClientHandler())
                {
                    try
                    {
                        // 配置HttpClientHandler以支持Windows 7
                        handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
                            // 只放行"已构建链但系统报 RemoteCertificateChainErrors（含过期/未吊销检查受限）"；
                            // 链不信任、主机名不匹配、UntrustedRoot 等严格错误继续走系统校验，
                            // 避免 MITM 在客户端网络内任意注入"更新包"。
                            sslPolicyErrors == System.Net.Security.SslPolicyErrors.None
                            || sslPolicyErrors == System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors;

                        using (HttpClient client = new HttpClient(handler))
                        {
                            client.Timeout = RequestTimeout;
                            LogHelper.WriteLogToFile($"AutoUpdate | 发送HTTP请求到: {fileUrl}");

                            var downloadTask = client.GetAsync(fileUrl);
                            var timeoutTask = Task.Delay(RequestTimeout);

                            var completedTask = await Task.WhenAny(downloadTask, timeoutTask);
                            if (completedTask == timeoutTask)
                            {
                                LogHelper.WriteLogToFile($"AutoUpdate | 请求超时 ({RequestTimeout.TotalSeconds}秒)", LogHelper.LogType.Error);
                                return null;
                            }

                            HttpResponseMessage response = await downloadTask;
                            LogHelper.WriteLogToFile($"AutoUpdate | HTTP响应状态: {response.StatusCode}");
                            response.EnsureSuccessStatusCode();

                            string content = await response.Content.ReadAsStringAsync();
                            content = content.Trim();

                            // 如果内容包含HTML（可能是GitHub页面而不是原始内容），尝试提取版本号
                            if (content.Contains("<html") || content.Contains("<!DOCTYPE"))
                            {
                                LogHelper.WriteLogToFile("AutoUpdate | 收到HTML内容而不是原始版本号 - 尝试提取版本");
                                int startPos = content.IndexOf("<table");
                                if (startPos > 0)
                                {
                                    int endPos = content.IndexOf("</table>", startPos);
                                    if (endPos > startPos)
                                    {
                                        string tableContent = content.Substring(startPos, endPos - startPos);
                                        var match = Regex.Match(tableContent, @"(\d+\.\d+\.\d+(\.\d+)?)");
                                        if (match.Success)
                                        {
                                            content = match.Groups[1].Value;
                                            LogHelper.WriteLogToFile($"AutoUpdate | 从HTML提取版本: {content}");
                                        }
                                        else
                                        {
                                            LogHelper.WriteLogToFile("AutoUpdate | 无法从HTML内容提取版本");
                                            return null;
                                        }
                                    }
                                }
                            }

                            LogHelper.WriteLogToFile($"AutoUpdate | 响应内容: {content}");
                            return content;
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | HTTP请求错误: {ex.Message}", LogHelper.LogType.Error);
                    }
                    catch (TaskCanceledException ex)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 请求超时: {ex.Message}", LogHelper.LogType.Error);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 错误: {ex.Message}", LogHelper.LogType.Error);
                    }

                    return null;
                }
            }

            // 其他Windows版本使用标准配置
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    client.Timeout = RequestTimeout;
                    LogHelper.WriteLogToFile($"AutoUpdate | 发送HTTP请求到: {fileUrl}");

                    var downloadTask = client.GetAsync(fileUrl);
                    var timeoutTask = Task.Delay(RequestTimeout);

                    var completedTask = await Task.WhenAny(downloadTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 请求超时 ({RequestTimeout.TotalSeconds}秒)", LogHelper.LogType.Error);
                        return null;
                    }

                    HttpResponseMessage response = await downloadTask;
                    LogHelper.WriteLogToFile($"AutoUpdate | HTTP响应状态: {response.StatusCode}");
                    response.EnsureSuccessStatusCode();

                    string content = await response.Content.ReadAsStringAsync();
                    content = content.Trim();

                    // 如果内容包含HTML（可能是GitHub页面而不是原始内容），尝试提取版本号
                    if (content.Contains("<html") || content.Contains("<!DOCTYPE"))
                    {
                        LogHelper.WriteLogToFile("AutoUpdate | 收到HTML内容而不是原始版本号 - 尝试提取版本");
                        int startPos = content.IndexOf("<table");
                        if (startPos > 0)
                        {
                            int endPos = content.IndexOf("</table>", startPos);
                            if (endPos > startPos)
                            {
                                string tableContent = content.Substring(startPos, endPos - startPos);
                                var match = Regex.Match(tableContent, @"(\d+\.\d+\.\d+(\.\d+)?)");
                                if (match.Success)
                                {
                                    content = match.Groups[1].Value;
                                    LogHelper.WriteLogToFile($"AutoUpdate | 从HTML提取版本: {content}");
                                }
                                else
                                {
                                    LogHelper.WriteLogToFile("AutoUpdate | 无法从HTML内容提取版本");
                                    return null;
                                }
                            }
                        }
                    }

                    LogHelper.WriteLogToFile($"AutoUpdate | 响应内容: {content}");
                    return content;
                }
                catch (HttpRequestException ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | HTTP请求错误: {ex.Message}", LogHelper.LogType.Error);
                }
                catch (TaskCanceledException ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 请求超时: {ex.Message}", LogHelper.LogType.Error);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 错误: {ex.Message}", LogHelper.LogType.Error);
                }

                return null;
            }
        }

        // 通过GitHub API获取指定版本的Release信息
        private static async Task<(string version, string downloadUrl, string releaseNotes, DateTime? releaseTime)> GetGithubReleaseByVersion(string targetVersion, UpdateChannel channel)
        {
            try
            {
                string apiUrl = (channel == UpdateChannel.Beta || channel == UpdateChannel.Preview)
                    ? "https://api.github.com/repos/InkCanvasForClass/community-beta/releases"
                    : "https://api.github.com/repos/InkCanvasForClass/community/releases";
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "ICC-CE Auto Updater");
                    LogHelper.WriteLogToFile("AutoUpdate | 使用GitHub API调用");
                    var response = await client.GetStringAsync(apiUrl);
                    var releases = JArray.Parse(response);

                    foreach (var release in releases)
                    {
                        string version = release["tag_name"]?.ToString();
                        if (version == targetVersion || version == $"v{targetVersion}" || version == $"V{targetVersion}")
                        {
                            string releaseNotes = release["body"]?.ToString();
                            string downloadUrl = PickBrowserDownloadUrlFromAssets(release["assets"]);

                            // 解析发布时间
                            DateTime? releaseTime = null;
                            if (release["published_at"] != null && DateTime.TryParse(release["published_at"].ToString(), out DateTime parsedTime))
                            {
                                releaseTime = parsedTime;
                            }

                            return (version, downloadUrl, releaseNotes, releaseTime);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | GitHub Releases API 获取版本 {targetVersion} 失败: {ex.Message}", LogHelper.LogType.Warning);
            }
            return (null, null, null, null);
        }

        // 通过GitHub API获取最新Release信息
        private static async Task<(string version, string downloadUrl, string releaseNotes, DateTime? releaseTime)> GetLatestGithubRelease(UpdateChannel channel)
        {
            try
            {
                if (channel == UpdateChannel.Beta)
                {
                    string apiUrl = "https://api.github.com/repos/InkCanvasForClass/community-beta/releases";
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "ICC-CE Auto Updater");
                        LogHelper.WriteLogToFile("AutoUpdate | 使用GitHub API调用");
                        var response = await client.GetStringAsync(apiUrl);
                        var releases = JArray.Parse(response);

                        if (releases.Count > 0)
                        {
                            var latestRelease = releases[0];
                            string version = latestRelease["tag_name"]?.ToString();
                            string releaseNotes = latestRelease["body"]?.ToString();
                            string downloadUrl = PickBrowserDownloadUrlFromAssets(latestRelease["assets"]);

                            DateTime? releaseTime = null;
                            if (latestRelease["published_at"] != null && DateTime.TryParse(latestRelease["published_at"].ToString(), out DateTime parsedTime))
                            {
                                releaseTime = parsedTime;
                            }

                            if (!string.IsNullOrEmpty(version) && !string.IsNullOrEmpty(downloadUrl))
                                return (version, downloadUrl, releaseNotes, releaseTime);
                        }
                    }
                }
                else
                {
                    string apiUrl = channel == UpdateChannel.Preview
                        ? "https://api.github.com/repos/InkCanvasForClass/community-beta/releases/latest"
                        : "https://api.github.com/repos/InkCanvasForClass/community/releases/latest";
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "ICC-CE Auto Updater");
                        LogHelper.WriteLogToFile("AutoUpdate | 使用GitHub API调用");
                        var response = await client.GetStringAsync(apiUrl);
                        var json = JObject.Parse(response);
                        string version = json["tag_name"]?.ToString();
                        string releaseNotes = json["body"]?.ToString();
                        string downloadUrl = PickBrowserDownloadUrlFromAssets(json["assets"]);

                        DateTime? releaseTime = null;
                        if (json["published_at"] != null && DateTime.TryParse(json["published_at"].ToString(), out DateTime parsedTime))
                        {
                            releaseTime = parsedTime;
                        }

                        if (!string.IsNullOrEmpty(version) && !string.IsNullOrEmpty(downloadUrl))
                            return (version, downloadUrl, releaseNotes, releaseTime);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | GitHub Releases API 获取失败: {ex.Message}", LogHelper.LogType.Warning);
            }
            return (null, null, null, null);
        }

        private class SmartUpdateResult
        {
            public bool HasUpdate { get; set; }
            public string Version { get; set; }
            public string VersionId { get; set; }
            public string DownloadUrl { get; set; }
            public string ReleaseNotes { get; set; }
            public DateTime? ReleaseTime { get; set; }
        }

        private static string GetSmartUpdateChannel(UpdateChannel channel)
        {
            if (channel == UpdateChannel.Preview) return "preview";
            if (channel == UpdateChannel.Beta) return "beta";
            return "stable";
        }

        private static string GetSmartUpdateOs()
        {
            var version = Environment.OSVersion.Version;
            if (version.Major == 6 && version.Minor == 1) return "win7";
            if (version.Major >= 10) return "win10";
            return "windows";
        }

        private static JToken PickFirstToken(JToken token, params string[] names)
        {
            if (token == null) return null;
            foreach (var name in names)
            {
                var found = token.SelectTokens($"$..{name}").FirstOrDefault(t => t.Type != JTokenType.Null && t.Type != JTokenType.Undefined);
                if (found != null) return found;
            }
            return null;
        }

        private static string PickFirstString(JToken token, params string[] names)
        {
            return PickFirstToken(token, names)?.ToString();
        }

        private static bool PickFirstBool(JToken token, params string[] names)
        {
            var value = PickFirstToken(token, names);
            if (value == null) return false;
            if (value.Type == JTokenType.Boolean) return value.Value<bool>();
            var text = value.ToString();
            return string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) || text == "1";
        }

        private static SmartUpdateResult ParseSmartUpdateResult(string content)
        {
            var json = JToken.Parse(content);
            DateTime? releaseTime = null;
            var releaseTimeText = PickFirstString(json, "published_at", "release_time", "created_at", "updated_at");
            if (!string.IsNullOrWhiteSpace(releaseTimeText) && DateTime.TryParse(releaseTimeText, out var parsedReleaseTime))
            {
                releaseTime = parsedReleaseTime;
            }

            string version = PickFirstString(json, "latest_version", "version", "version_name", "tag_name");
            string versionId = PickFirstString(json, "version_id", "id");
            string downloadUrl = PickFirstString(json, "download_url", "download", "url", "file_url");
            return new SmartUpdateResult
            {
                HasUpdate = PickFirstBool(json, "has_update", "update_available", "need_update", "available") ||
                            !string.IsNullOrWhiteSpace(versionId) ||
                            !string.IsNullOrWhiteSpace(downloadUrl),
                Version = string.IsNullOrWhiteSpace(version) ? null : version.TrimStart('v', 'V'),
                VersionId = versionId,
                DownloadUrl = downloadUrl,
                ReleaseNotes = PickFirstString(json, "release_notes", "changelog", "notes", "body", "description"),
                ReleaseTime = releaseTime
            };
        }

        private static async Task<string> ResolveSmartDownloadUrl(string versionId)
        {
            if (string.IsNullOrWhiteSpace(versionId)) return null;
            string url = $"https://dev-api.dy.ci/api/distribute/download/{Uri.EscapeDataString(versionId)}/";
            using (var handler = new HttpClientHandler { AllowAutoRedirect = false })
            using (var client = new HttpClient(handler))
            {
                client.Timeout = RequestTimeout;
                var response = await client.GetAsync(url);
                if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400 && response.Headers.Location != null)
                {
                    return response.Headers.Location.IsAbsoluteUri
                        ? response.Headers.Location.ToString()
                        : new Uri(new Uri(url), response.Headers.Location).ToString();
                }

                string content = (await response.Content.ReadAsStringAsync()).Trim();
                if (Uri.IsWellFormedUriString(content, UriKind.Absolute)) return content;
                if (string.IsNullOrWhiteSpace(content)) return null;

                var json = JToken.Parse(content);
                return PickFirstString(json, "download_url", "download", "url", "file_url", "location");
            }
        }

        private static async Task<(bool handled, string remoteVersion, UpdateLineGroup lineGroup, string releaseNotes, DateTime? releaseTime)> CheckSmartUpdate(UpdateChannel channel, bool alwaysGetRemote, bool isVersionFix)
        {
            string localVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string arch = IsX64UpdatePackageSelected() ? "x64" : "x86";
            string checkUrl = "https://dev-api.dy.ci/api/distribute/check/ICC-CE/" +
                              $"?token={Uri.EscapeDataString(NotificationSettings.BuiltInSoftwareToken)}" +
                              $"&version={Uri.EscapeDataString(localVersion)}" +
                              $"&os={Uri.EscapeDataString(GetSmartUpdateOs())}" +
                              $"&arch={Uri.EscapeDataString(arch)}" +
                              $"&channel={Uri.EscapeDataString(GetSmartUpdateChannel(channel))}" +
                              $"&user_id={Uri.EscapeDataString(DeviceIdentifier.GetDeviceId())}";

            using (var client = new HttpClient())
            {
                client.Timeout = RequestTimeout;
                LogHelper.WriteLogToFile($"AutoUpdate | 使用智慧更新 API 检查: {checkUrl}");
                string content = await client.GetStringAsync(checkUrl);
                var result = ParseSmartUpdateResult(content);
                var group = new UpdateLineGroup { GroupName = StartupStrings.SmartUpdate, VersionUrl = checkUrl };

                if (!result.HasUpdate)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 智慧更新 API 返回当前已是最新");
                    return (true, null, group, result.ReleaseNotes, result.ReleaseTime);
                }

                if (string.IsNullOrWhiteSpace(result.Version))
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 智慧更新 API 未返回版本号，回退到旧逻辑", LogHelper.LogType.Warning);
                    return (false, null, null, null, null);
                }

                Version local = new Version(localVersion);
                Version remote = new Version(result.Version);
                if (remote <= local && !alwaysGetRemote)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 当前版本已是最新 (智慧更新 API)");
                    return (true, null, group, result.ReleaseNotes, result.ReleaseTime);
                }

                if (!isVersionFix)
                {
                    DateTime releaseTime = result.ReleaseTime ?? DateTime.Now;
                    DateTime? currentVersionReleaseTime = await GetVersionReleaseTime(localVersion, channel);
                    bool shouldPush = DeviceIdentifier.ShouldPushUpdate(result.Version, releaseTime, true, currentVersionReleaseTime, localVersion);
                    if (!shouldPush)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 根据用户优先级({DeviceIdentifier.GetUpdatePriority()})，暂不推送智慧更新 {result.Version}");
                        return (true, null, group, result.ReleaseNotes, result.ReleaseTime);
                    }
                }

                string downloadUrl = result.DownloadUrl;
                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    downloadUrl = await ResolveSmartDownloadUrl(result.VersionId);
                }

                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 智慧更新 API 未返回可用下载链接，回退到旧逻辑", LogHelper.LogType.Warning);
                    return (false, null, null, null, null);
                }

                group.DownloadUrlFormat = downloadUrl;
                LogHelper.WriteLogToFile($"AutoUpdate | 智慧更新 API 发现新版本: {result.Version}");
                return (true, result.Version, group, result.ReleaseNotes, result.ReleaseTime);
            }
        }

        // 主要的更新检测方法（优先检测延迟，失败时自动切换线路组）
        // 仅检测新版本时用GitHub API，实际下载时只用线路组
        public static async Task<(string remoteVersion, UpdateLineGroup lineGroup, string releaseNotes)> CheckForUpdates(UpdateChannel channel = UpdateChannel.Release, bool alwaysGetRemote = false, bool isVersionFix = false)
        {
            try
            {
                // 记录更新检查时间
                DeviceIdentifier.RecordUpdateCheck();

                string localVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                LogHelper.WriteLogToFile($"AutoUpdate | 本地版本: {localVersion}");
                LogHelper.WriteLogToFile($"AutoUpdate | 设备ID: {DeviceIdentifier.GetDeviceId()}");
                LogHelper.WriteLogToFile($"AutoUpdate | 更新优先级: {DeviceIdentifier.GetUpdatePriority()}");

                if (MainWindow.Settings?.Startup?.IsSmartUpdate == true)
                {
                    try
                    {
                        var smart = await CheckSmartUpdate(channel, alwaysGetRemote, isVersionFix);
                        if (smart.handled)
                        {
                            return (smart.remoteVersion, smart.lineGroup, smart.releaseNotes);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 智慧更新 API 检查失败，回退到旧逻辑: {ex.Message}", LogHelper.LogType.Warning);
                    }
                }

                LogHelper.WriteLogToFile("AutoUpdate | 优先通过GitHub Releases API检测...");

                // 1. 优先通过GitHub Releases API获取
                var (apiVersion, _, apiReleaseNotes, apiReleaseTime) = await GetLatestGithubRelease(channel);
                if (!string.IsNullOrEmpty(apiVersion))
                {
                    Version local = new Version(localVersion);
                    Version remote = new Version(apiVersion.TrimStart('v', 'V'));
                    if (remote > local || alwaysGetRemote)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 通过GitHub Releases API发现新版本: {apiVersion}");

                        // 检查是否应该根据用户优先级推送更新（版本修复功能不受限制）
                        if (!isVersionFix)
                        {
                            DateTime releaseTime = apiReleaseTime ?? DateTime.Now;

                            // 尝试获取当前版本的发布时间
                            DateTime? currentVersionReleaseTime = await GetVersionReleaseTime(localVersion, channel);

                            bool shouldPush = DeviceIdentifier.ShouldPushUpdate(apiVersion, releaseTime, true, currentVersionReleaseTime, localVersion); // 明确标记为自动更新
                            if (!shouldPush)
                            {
                                var priority = DeviceIdentifier.GetUpdatePriority();
                                var daysBetweenVersions = currentVersionReleaseTime.HasValue
                                    ? (releaseTime - currentVersionReleaseTime.Value).TotalDays
                                    : (DateTime.Now - releaseTime).TotalDays;
                                LogHelper.WriteLogToFile($"AutoUpdate | 根据用户优先级({priority})，暂不推送更新 {apiVersion}，版本间隔: {daysBetweenVersions:F1} 天");
                                var group = (await GetAvailableLineGroupsOrdered(channel)).FirstOrDefault();
                                return (null, group, apiReleaseNotes); // 返回null表示不推送
                            }
                        }
                        else
                        {
                            LogHelper.WriteLogToFile("AutoUpdate | 版本修复模式，跳过分级策略检查");
                        }

                        LogHelper.WriteLogToFile($"AutoUpdate | 根据用户优先级，推送更新 {apiVersion}");
                        // 只返回版本号和日志，不返回直链
                        var availableGroup = (await GetAvailableLineGroupsOrdered(channel)).FirstOrDefault();
                        return (apiVersion, availableGroup, apiReleaseNotes);
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("AutoUpdate | 当前版本已是最新 (GitHub Releases API)");
                        var availableGroup = (await GetAvailableLineGroupsOrdered(channel)).FirstOrDefault();
                        return (null, availableGroup, apiReleaseNotes);
                    }
                }
                // 2. 回退到原有txt方案
                LogHelper.WriteLogToFile("AutoUpdate | GitHub Releases API获取失败，回退到txt方案...");
                var availableGroups = await GetAvailableLineGroupsOrdered(channel);
                if (availableGroups.Count == 0)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 所有线路组均不可用", LogHelper.LogType.Error);
                    return (null, null, null);
                }
                foreach (var group in availableGroups)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 尝试使用线路组获取版本信息: {group.GroupName}");
                    string remoteVersion = await GetRemoteVersion(group.VersionUrl);
                    if (remoteVersion != null)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 成功从线路组 {group.GroupName} 获取远程版本: {remoteVersion}");
                        Version local = new Version(localVersion);
                        Version remote = new Version(remoteVersion);
                        if (remote > local || alwaysGetRemote)
                        {
                            LogHelper.WriteLogToFile($"AutoUpdate | 发现新版本或强制获取: {remoteVersion}");

                            // 检查是否应该根据用户优先级推送更新（版本修复功能不受限制）
                            if (!isVersionFix)
                            {
                                // 尝试获取当前版本的发布时间
                                DateTime? currentVersionReleaseTime = await GetVersionReleaseTime(localVersion, channel);

                                bool shouldPush = DeviceIdentifier.ShouldPushUpdate(remoteVersion, DateTime.Now, true, currentVersionReleaseTime, localVersion); // 明确标记为自动更新
                                if (!shouldPush)
                                {
                                    var priority = DeviceIdentifier.GetUpdatePriority();
                                    LogHelper.WriteLogToFile($"AutoUpdate | 根据用户优先级({priority})，暂不推送更新 {remoteVersion}");
                                    return (null, group, null); // 返回null表示不推送
                                }
                            }
                            else
                            {
                                LogHelper.WriteLogToFile("AutoUpdate | 版本修复模式，跳过分级策略检查");
                            }

                            LogHelper.WriteLogToFile($"AutoUpdate | 根据用户优先级，推送更新 {remoteVersion}");
                            return (remoteVersion, group, null);
                        }

                        LogHelper.WriteLogToFile("AutoUpdate | 当前版本已是最新");
                        return (null, group, null);
                    }

                    LogHelper.WriteLogToFile($"AutoUpdate | 线路组 {group.GroupName} 获取版本失败，尝试下一个线路组", LogHelper.LogType.Warning);
                }
                LogHelper.WriteLogToFile("AutoUpdate | 所有线路组均无法获取版本信息", LogHelper.LogType.Error);
                return (null, null, null);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | CheckForUpdates错误: {ex.Message}", LogHelper.LogType.Error);
                return (null, null, null);
            }
        }

        // 获取远程内容的通用方法
        public static async Task<string> GetRemoteContent(string fileUrl)
        {
            // 检测是否为Windows 7
            var osVersion = Environment.OSVersion;
            bool isWindows7 = osVersion.Version.Major == 6 && osVersion.Version.Minor == 1;

            if (isWindows7)
            {
                // Windows 7使用特殊配置
                using (var handler = new HttpClientHandler())
                {
                    try
                    {
                        // 配置HttpClientHandler以支持Windows 7
                        handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
                            // 只放行"已构建链但系统报 RemoteCertificateChainErrors（含过期/未吊销检查受限）"；
                            // 链不信任、主机名不匹配、UntrustedRoot 等严格错误继续走系统校验，
                            // 避免 MITM 在客户端网络内任意注入"更新包"。
                            sslPolicyErrors == System.Net.Security.SslPolicyErrors.None
                            || sslPolicyErrors == System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors;

                        using (HttpClient client = new HttpClient(handler))
                        {
                            client.Timeout = RequestTimeout;
                            LogHelper.WriteLogToFile($"AutoUpdate | 发送HTTP请求到: {fileUrl}");
                            var downloadTask = client.GetAsync(fileUrl);
                            var timeoutTask = Task.Delay(RequestTimeout);
                            var completedTask = await Task.WhenAny(downloadTask, timeoutTask);
                            if (completedTask == timeoutTask)
                            {
                                LogHelper.WriteLogToFile($"AutoUpdate | 请求超时 ({RequestTimeout.TotalSeconds}秒)", LogHelper.LogType.Error);
                                return null;
                            }
                            HttpResponseMessage response = await downloadTask;
                            LogHelper.WriteLogToFile($"AutoUpdate | HTTP响应状态: {response.StatusCode}");
                            response.EnsureSuccessStatusCode();
                            string content = await response.Content.ReadAsStringAsync();
                            return content;
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | HTTP请求错误: {ex.Message}", LogHelper.LogType.Error);
                    }
                    catch (TaskCanceledException ex)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 请求超时: {ex.Message}", LogHelper.LogType.Error);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 错误: {ex.Message}", LogHelper.LogType.Error);
                    }
                    return null;
                }
            }

            // 其他Windows版本使用标准配置
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    client.Timeout = RequestTimeout;
                    LogHelper.WriteLogToFile($"AutoUpdate | 发送HTTP请求到: {fileUrl}");
                    var downloadTask = client.GetAsync(fileUrl);
                    var timeoutTask = Task.Delay(RequestTimeout);
                    var completedTask = await Task.WhenAny(downloadTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 请求超时 ({RequestTimeout.TotalSeconds}秒)", LogHelper.LogType.Error);
                        return null;
                    }
                    HttpResponseMessage response = await downloadTask;
                    LogHelper.WriteLogToFile($"AutoUpdate | HTTP响应状态: {response.StatusCode}");
                    response.EnsureSuccessStatusCode();
                    string content = await response.Content.ReadAsStringAsync();
                    return content;
                }
                catch (HttpRequestException ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | HTTP请求错误: {ex.Message}", LogHelper.LogType.Error);
                }
                catch (TaskCanceledException ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 请求超时: {ex.Message}", LogHelper.LogType.Error);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 错误: {ex.Message}", LogHelper.LogType.Error);
                }
                return null;
            }
        }

        // 使用指定线路组获取更新日志
        public static async Task<string> GetUpdateLogWithLineGroup(UpdateLineGroup group)
        {
            return await GetRemoteContent(group.LogUrl);
        }

        // 获取更新日志（自动选择最快线路组）
        public static async Task<string> GetUpdateLog(UpdateChannel channel = UpdateChannel.Release)
        {
            var group = await GetFastestLineGroup(channel);
            if (group == null) return "# 无法获取更新日志\n\n所有线路均不可用。";
            return await GetUpdateLogWithLineGroup(group);
        }

        // 获取所有GitHub历史版本（Release）
        public static async Task<List<(string version, string downloadUrl, string releaseNotes)>> GetAllGithubReleases(UpdateChannel channel = UpdateChannel.Release)
        {
            var result = new List<(string, string, string)>();
            try
            {
                string apiUrl = (channel == UpdateChannel.Beta || channel == UpdateChannel.Preview)
                    ? "https://api.github.com/repos/InkCanvasForClass/community-beta/releases"
                    : "https://api.github.com/repos/InkCanvasForClass/community/releases";
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "ICC-CE Auto Updater");
                    LogHelper.WriteLogToFile("AutoUpdate | 使用GitHub API调用");
                    var response = await client.GetStringAsync(apiUrl);
                    var arr = JArray.Parse(response);
                    foreach (var item in arr)
                    {
                        string version = item["tag_name"]?.ToString();
                        string releaseNotes = item["body"]?.ToString();
                        string downloadUrl = PickBrowserDownloadUrlFromAssets(item["assets"]);
                        if (!string.IsNullOrEmpty(version) && !string.IsNullOrEmpty(downloadUrl))
                            result.Add((version, downloadUrl, releaseNotes));
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 获取历史版本失败: {ex.Message}", LogHelper.LogType.Error);
            }
            return result;
        }

        // 测试Windows 7 TLS连接的方法
        public static async Task<bool> TestWindows7TlsConnection()
        {
            try
            {
                // 检测是否为Windows 7
                var osVersion = Environment.OSVersion;
                bool isWindows7 = osVersion.Version.Major == 6 && osVersion.Version.Minor == 1;

                if (!isWindows7)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 当前系统不是Windows 7，跳过TLS连接测试");
                    return true; // 非Windows 7系统直接返回成功
                }

                LogHelper.WriteLogToFile("AutoUpdate | 开始测试Windows 7 TLS连接...");

                // 测试GitHub连接
                var testUrl = "https://github.com/InkCanvasForClass/community/raw/refs/heads/net10/AutomaticUpdateVersionControl.txt";

                using (var handler = new HttpClientHandler())
                {
                    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
                        sslPolicyErrors == System.Net.Security.SslPolicyErrors.None
                        || sslPolicyErrors == System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors;

                    using (var client = new HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromSeconds(10);
                        var response = await client.GetAsync(testUrl);

                        if (response.IsSuccessStatusCode)
                        {
                            LogHelper.WriteLogToFile("AutoUpdate | Windows 7 TLS连接测试成功");
                            return true;
                        }

                        LogHelper.WriteLogToFile($"AutoUpdate | Windows 7 TLS连接测试失败，状态码: {response.StatusCode}", LogHelper.LogType.Error);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Windows 7 TLS连接测试异常: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 获取指定版本的发布时间
        /// </summary>
        /// <param name="version">版本号</param>
        /// <param name="channel">更新通道</param>
        /// <returns>版本发布时间，如果获取失败则返回null</returns>
        public static async Task<DateTime?> GetVersionReleaseTime(string version, UpdateChannel channel = UpdateChannel.Release)
        {
            try
            {
                var (_, _, _, releaseTime) = await GetGithubReleaseByVersion(version, channel);
                return releaseTime;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 获取版本 {version} 发布时间失败: {ex.Message}", LogHelper.LogType.Warning);
                return null;
            }
        }
    }
}
