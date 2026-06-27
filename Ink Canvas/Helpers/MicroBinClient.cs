using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// MicroBin pastebin 客户端。
    /// 支持自部署的 MicroBin 服务，用于上传反馈数据。
    /// </summary>
    public class MicroBinClient
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        /// <summary>
        /// 上传纯文本内容到 MicroBin，返回 paste URL。
        /// 使用 MicroBin API v2: POST /api/v2/paste/raw
        /// </summary>
        /// <param name="serverUrl">MicroBin 服务地址（如 https://microbin.example.com）</param>
        /// <param name="content">要上传的文本内容</param>
        /// <returns>paste 的 URL，失败时返回 null</returns>
        public static async Task<string> UploadRawAsync(string serverUrl, string content)
        {
            if (string.IsNullOrWhiteSpace(serverUrl) || string.IsNullOrWhiteSpace(content))
                return null;

            try
            {
                // 规范化地址：移除末尾斜杠
                serverUrl = serverUrl.TrimEnd('/');

                var request = new HttpRequestMessage(HttpMethod.Post, $"{serverUrl}/api/v2/paste/raw")
                {
                    Content = new StringContent(content, Encoding.UTF8, "text/plain")
                };

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    return null;

                var responseBody = await response.Content.ReadAsStringAsync();

                // MicroBin API v2 返回 JSON: { "url": "...", "id": "...", ... }
                // 尝试解析 JSON 获取 URL
                try
                {
                    var json = Newtonsoft.Json.Linq.JObject.Parse(responseBody);
                    var url = json["url"]?.ToString();
                    if (!string.IsNullOrEmpty(url))
                        return url;
                }
                catch
                {
                    // 如果不是 JSON，检查是否直接返回了 URL 文本
                    var trimmed = responseBody.Trim();
                    if (trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        return trimmed;
                }

                // 降级：构造 URL
                return $"{serverUrl}/raw/{responseBody.Trim()}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MicroBin 上传失败: {ex.Message}");
                return null;
            }
        }
    }
}
