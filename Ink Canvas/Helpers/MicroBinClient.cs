using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// MicroBin pastebin 客户端。
    ///
    /// MicroBin 没有 REST API，通过表单提交创建 paste：
    ///   POST /   — multipart/form-data, 字段 "content" = 文本
    ///   302 重定向到 paste 页面，从 Location 头获取 URL
    /// </summary>
    public class MicroBinClient
    {
        private static readonly HttpClient _httpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                // 自部署可能用自签名证书
                ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true,
                // 不自动跟随重定向，手动从 302 Location 头取 URL
                AllowAutoRedirect = false
            };
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        }

        /// <summary>
        /// 上传文本到 MicroBin，返回 paste URL。
        /// </summary>
        public static async Task<(string url, string error)> UploadRawAsync(string serverUrl, string content)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
                return (null, "服务器地址为空");
            if (string.IsNullOrWhiteSpace(content))
                return (null, "内容为空");

            // 自动补全协议
            serverUrl = serverUrl.TrimEnd('/');
            if (!serverUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !serverUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                serverUrl = "https://" + serverUrl;
            }

            // 1. 先尝试 multipart/form-data 表单提交（MicroBin 标准方式）
            var (url, error) = await TryFormUpload(serverUrl, content);
            if (!string.IsNullOrEmpty(url))
                return (url, null);

            // 2. 降级尝试 raw 端点
            var (url2, error2) = await TryRawUpload(serverUrl, content);
            if (!string.IsNullOrEmpty(url2))
                return (url2, null);

            return (null, $"表单上传: {error}; Raw上传: {error2}");
        }

        /// <summary>
        /// multipart/form-data 方式：POST /，字段 content = 文本
        /// </summary>
        private static async Task<(string url, string error)> TryFormUpload(string serverUrl, string content)
        {
            try
            {
                var formData = new MultipartFormDataContent();
                formData.Add(new StringContent(content), "content");

                var response = await _httpClient.PostAsync(serverUrl, formData);
                var responseBody = await response.Content.ReadAsStringAsync();

                // 302 重定向 → Location 头包含 paste URL
                if (response.StatusCode == System.Net.HttpStatusCode.Found ||
                    response.StatusCode == System.Net.HttpStatusCode.MovedPermanently ||
                    response.StatusCode == System.Net.HttpStatusCode.SeeOther ||
                    (int)response.StatusCode == 307 ||
                    (int)response.StatusCode == 308)
                {
                    var location = response.Headers.Location;
                    if (location != null)
                    {
                        // Location 可能是相对路径 /paste/xxxxx
                        if (location.IsAbsoluteUri)
                            return (location.ToString(), null);
                        return ($"{serverUrl}{location}", null);
                    }
                }

                // 200 但返回了 JSON（某些版本）
                if (response.IsSuccessStatusCode)
                {
                    var parsed = ParsePasteUrl(responseBody, serverUrl);
                    if (!string.IsNullOrEmpty(parsed))
                        return (parsed, null);
                }

                return (null, $"{(int)response.StatusCode}");
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        /// <summary>
        /// raw 端点：POST /raw，body = 纯文本
        /// </summary>
        private static async Task<(string url, string error)> TryRawUpload(string serverUrl, string content)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{serverUrl}/raw")
                {
                    Content = new StringContent(content, System.Text.Encoding.UTF8, "text/plain")
                };

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                // 302 重定向
                if (response.StatusCode == System.Net.HttpStatusCode.Found ||
                    response.StatusCode == System.Net.HttpStatusCode.MovedPermanently ||
                    (int)response.StatusCode == 307 ||
                    (int)response.StatusCode == 308)
                {
                    var location = response.Headers.Location;
                    if (location != null)
                    {
                        if (location.IsAbsoluteUri)
                            return (location.ToString(), null);
                        return ($"{serverUrl}{location}", null);
                    }
                }

                if (response.IsSuccessStatusCode)
                {
                    var parsed = ParsePasteUrl(responseBody, serverUrl);
                    if (!string.IsNullOrEmpty(parsed))
                        return (parsed, null);
                }

                return (null, $"{(int)response.StatusCode}");
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        private static string ParsePasteUrl(string responseBody, string serverUrl)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return null;

            var trimmed = responseBody.Trim();

            // JSON: { "url": "..." } 或 { "id": "..." }
            try
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(trimmed);
                var url = json["url"]?.ToString();
                if (!string.IsNullOrEmpty(url)) return url;

                var id = json["id"]?.ToString();
                if (!string.IsNullOrEmpty(id)) return $"{serverUrl}/paste/{id}";
            }
            catch { }

            // 纯 URL
            if (trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !trimmed.Contains("<"))
                return trimmed;

            // HTML 页面：用正则提取 paste URL
            if (trimmed.Contains("<"))
            {
                // MicroBin 页面中常见的 URL 模式：
                // href="/paste/xxxxx"  或  href="/raw/xxxxx"  或  value="https://.../paste/xxxxx"
                var match = System.Text.RegularExpressions.Regex.Match(
                    trimmed,
                    @"(?:href|value|action|src)=[""']([^""']*(?:/paste/|/raw/)[^""']*?)[""']",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var path = match.Groups[1].Value;
                    if (path.StartsWith("http"))
                        return path;
                    return $"{serverUrl}{path}";
                }

                // 从 <link rel="canonical"> 或 og:url 提取
                match = System.Text.RegularExpressions.Regex.Match(
                    trimmed,
                    @"(?:canonical"">\s*<link\s+href|og:url"">\s*<meta\s+content)=[""']([^""']+)[""']",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                    return match.Groups[1].Value;
            }

            // 短 ID（不含空格和换行的短字符串）
            if (!trimmed.Contains(" ") && !trimmed.Contains("\n") && !trimmed.Contains("<") && trimmed.Length < 128)
                return $"{serverUrl}/paste/{trimmed}";

            return null;
        }
    }
}
