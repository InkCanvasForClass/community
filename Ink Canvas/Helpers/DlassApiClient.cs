using Ink_Canvas.Helpers;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// Dlass API 客户端，用于与服务端通信
    /// </summary>
    public class DlassApiClient : IDisposable
    {
        private const string DEFAULT_BASE_URL = "https://dlass.tech"; 
        private readonly string _appId;
        private readonly string _appSecret;
        private readonly string _baseUrl;
        private HttpClient _httpClient;
        private string _accessToken;
        private DateTime _tokenExpiresAt;

        private string _userToken;

        /// <summary>
        /// 初始化 Dlass API 客户端
        /// </summary>
        /// <param name="appId">应用ID</param>
        /// <param name="appSecret">应用密钥</param>
        /// <param name="baseUrl">API基础URL，如果为空则使用默认URL</param>
        /// <param name="userToken">用户Token，如果提供则优先使用用户token而不是App Secret</param>
        public DlassApiClient(string appId, string appSecret, string baseUrl = null, string userToken = null)
        {
            _appId = appId ?? throw new ArgumentNullException(nameof(appId));
            _appSecret = appSecret ?? throw new ArgumentNullException(nameof(appSecret));
            _userToken = userToken;
            _baseUrl = baseUrl ?? DEFAULT_BASE_URL;
            
            _baseUrl = _baseUrl.TrimEnd('/');
            if (!_baseUrl.StartsWith("http://") && !_baseUrl.StartsWith("https://"))
            {
                _baseUrl = "https://" + _baseUrl;
            }
            
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "InkCanvas/1.0");
        }

        /// <summary>
        /// 获取访问令牌（Access Token）
        /// </summary>
        public async Task<string> GetAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_userToken))
            {
                return _userToken;
            }

            if (!string.IsNullOrEmpty(_accessToken) && DateTime.Now < _tokenExpiresAt.AddMinutes(-5))
            {
                return _accessToken;
            }

            try
            {
                var requestData = new
                {
                    app_id = _appId,
                    app_secret = _appSecret,
                    grant_type = "client_credentials"
                };

                var json = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/oauth/token", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseContent);
                    _accessToken = tokenResponse.AccessToken;
                    _tokenExpiresAt = DateTime.Now.AddSeconds(tokenResponse.ExpiresIn ?? 3600);
                    return _accessToken;
                }
                else
                {
                    throw new Exception($"获取Access Token失败: {response.StatusCode}");
                }
            }
            catch (HttpRequestException httpEx)
            {
                throw new Exception($"获取Access Token时网络错误: {httpEx.Message}", httpEx);
            }
            catch (TaskCanceledException timeoutEx)
            {
                throw new Exception("获取Access Token时请求超时", timeoutEx);
            }
            catch (Exception ex)
            {
                throw new Exception($"获取Access Token时出错: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 发送GET请求
        /// </summary>
        public async Task<T> GetAsync<T>(string endpoint, bool requireAuth = true)
        {
            try
            {
                string token = null;
                if (requireAuth)
                {
                    token = await GetAccessTokenAsync();
                }

                var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                
                if (requireAuth && !string.IsNullOrEmpty(token))
                {
                    if (!string.IsNullOrEmpty(_userToken))
                    {
                        request.Headers.Add("X-User-Token", token);
                    }
                    else
                    {
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    }
                }

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    if (string.IsNullOrEmpty(content))
                    {
                        return default(T);
                    }
                    return JsonConvert.DeserializeObject<T>(content);
                }
                else
                {
                    throw new Exception($"API请求失败: {response.StatusCode} - {content}");
                }
            }
            catch (HttpRequestException httpEx)
            {
                throw new Exception($"发送请求时出错: {httpEx.Message}", httpEx);
            }
            catch (TaskCanceledException timeoutEx)
            {
                throw new Exception($"请求超时: {endpoint}", timeoutEx);
            }
            catch (Exception ex)
            {
                throw new Exception($"发送请求时出错: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 发送POST请求
        /// </summary>
        public async Task<T> PostAsync<T>(string endpoint, object data = null, bool requireAuth = true)
        {
            try
            {
                string token = null;
                if (requireAuth)
                {
                    token = await GetAccessTokenAsync();
                }

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                
                if (requireAuth && !string.IsNullOrEmpty(token))
                {
                    if (!string.IsNullOrEmpty(_userToken))
                    {
                        request.Headers.Add("X-User-Token", token);
                    }
                    else
                    {
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    }
                }

                if (data != null)
                {
                    var json = JsonConvert.SerializeObject(data);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    if (string.IsNullOrEmpty(content))
                    {
                        return default(T);
                    }
                    return JsonConvert.DeserializeObject<T>(content);
                }
                else
                {
                    throw new Exception($"API请求失败: {response.StatusCode} - {content}");
                }
            }
            catch (HttpRequestException httpEx)
            {
                throw new Exception($"发送请求时出错: {httpEx.Message}", httpEx);
            }
            catch (TaskCanceledException timeoutEx)
            {
                throw new Exception($"请求超时: {endpoint}", timeoutEx);
            }
            catch (Exception ex)
            {
                throw new Exception($"发送请求时出错: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 发送PUT请求
        /// </summary>
        public async Task<T> PutAsync<T>(string endpoint, object data = null, bool requireAuth = true)
        {
            try
            {
                string token = null;
                if (requireAuth)
                {
                    token = await GetAccessTokenAsync();
                }

                var request = new HttpRequestMessage(HttpMethod.Put, endpoint);
                
                if (requireAuth && !string.IsNullOrEmpty(token))
                {
                    // 如果是用户token，使用X-User-Token header
                    if (!string.IsNullOrEmpty(_userToken))
                    {
                        request.Headers.Add("X-User-Token", token);
                    }
                    else
                    {
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    }
                }

                if (data != null)
                {
                    var json = JsonConvert.SerializeObject(data);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    if (string.IsNullOrEmpty(content))
                    {
                        return default(T);
                    }
                    return JsonConvert.DeserializeObject<T>(content);
                }
                else
                {
                    throw new Exception($"API请求失败: {response.StatusCode} - {content}");
                }
            }
            catch (HttpRequestException httpEx)
            {
                throw new Exception($"发送请求时出错: {httpEx.Message}", httpEx);
            }
            catch (TaskCanceledException timeoutEx)
            {
                throw new Exception($"请求超时: {endpoint}", timeoutEx);
            }
            catch (Exception ex)
            {
                throw new Exception($"发送请求时出错: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 发送DELETE请求
        /// </summary>
        public async Task<bool> DeleteAsync(string endpoint, bool requireAuth = true)
        {
            try
            {
                string token = null;
                if (requireAuth)
                {
                    token = await GetAccessTokenAsync();
                }

                var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
                
                if (requireAuth && !string.IsNullOrEmpty(token))
                {
                    // 如果是用户token，使用X-User-Token header
                    if (!string.IsNullOrEmpty(_userToken))
                    {
                        request.Headers.Add("X-User-Token", token);
                    }
                    else
                    {
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    }
                }

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 上传笔记文件
        /// </summary>
        /// <param name="endpoint">上传端点</param>
        /// <param name="filePath">文件路径</param>
        /// <param name="boardId">白板ID</param>
        /// <param name="secretKey">白板密钥</param>
        /// <param name="title">笔记标题（可选）</param>
        /// <param name="description">笔记描述（可选）</param>
        /// <param name="tags">笔记标签（可选）</param>
        public async Task<T> UploadNoteAsync<T>(string endpoint, string filePath, string boardId, string secretKey, string title = null, string description = null, string tags = null)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"文件不存在: {filePath}");
                }

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                
                // 设置白板认证头
                request.Headers.Add("X-Board-ID", boardId);
                request.Headers.Add("X-Secret-Key", secretKey);

                // 创建multipart/form-data内容
                var content = new MultipartFormDataContent();
                
                // 添加文件
                var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                var fileName = Path.GetFileName(filePath);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(fileContent, "file", fileName);

                // 添加可选参数
                if (!string.IsNullOrEmpty(title))
                {
                    content.Add(new StringContent(title), "title");
                }
                if (!string.IsNullOrEmpty(description))
                {
                    content.Add(new StringContent(description), "description");
                }
                if (!string.IsNullOrEmpty(tags))
                {
                    content.Add(new StringContent(tags), "tags");
                }

                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    if (string.IsNullOrEmpty(responseContent))
                    {
                        return default(T);
                    }
                    return JsonConvert.DeserializeObject<T>(responseContent);
                }
                else
                {
                    throw new Exception($"上传文件失败: {response.StatusCode} - {responseContent}");
                }
            }
            catch (HttpRequestException httpEx)
            {
                throw new Exception($"上传文件时网络错误: {httpEx.Message}", httpEx);
            }
            catch (TaskCanceledException timeoutEx)
            {
                throw new Exception($"上传文件超时: {endpoint}", timeoutEx);
            }
            catch (Exception ex)
            {
                throw new Exception($"上传文件时出错: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        #region 内部类

        /// <summary>
        /// Token响应模型
        /// </summary>
        private class TokenResponse
        {
            [JsonProperty("access_token")]
            public string AccessToken { get; set; }

            [JsonProperty("expires_in")]
            public int? ExpiresIn { get; set; }

            [JsonProperty("token_type")]
            public string TokenType { get; set; }
        }

        #endregion
    }
}

