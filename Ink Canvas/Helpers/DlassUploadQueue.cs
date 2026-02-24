using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// Dlass上传队列
    /// </summary>
    public class DlassUploadQueue : BaseUploadQueue
    {
        private const string APP_ID = "app_WkjocWqsrVY7T6zQV2CfiA";
        private const string APP_SECRET = "o7dx5b5ASGUMcM72PCpmRQYAhSijqaOVHoGyBK0IxbA";

        /// <summary>
        /// 队列文件名
        /// </summary>
        protected override string QueueFileName => "DlassUploadQueue.json";

        /// <summary>
        /// 上传笔记响应模型
        /// </summary>
        public class UploadNoteResponse
        {
            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; }

            [JsonProperty("note_id")]
            public int? NoteId { get; set; }

            [JsonProperty("filename")]
            public string Filename { get; set; }

            [JsonProperty("file_path")]
            public string FilePath { get; set; }

            [JsonProperty("file_url")]
            public string FileUrl { get; set; }
        }

        /// <summary>
        /// 白板信息模型（用于查找白板）
        /// </summary>
        private class WhiteboardInfo
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
        }

        /// <summary>
        /// 认证响应模型
        /// </summary>
        private class AuthWithTokenResponse
        {
            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("whiteboards")]
            public List<WhiteboardInfo> Whiteboards { get; set; }
        }

        /// <summary>
        /// 检查上传是否启用
        /// </summary>
        protected override bool IsUploadEnabled()
        {
            return MainWindow.Settings?.Dlass?.IsAutoUploadNotes == true;
        }

        /// <summary>
        /// 内部上传方法，执行实际上传操作
        /// </summary>
        protected override async Task<bool> UploadFileInternalAsync(string filePath, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 再次检查文件是否存在（可能在队列等待时被删除）
                if (!File.Exists(filePath))
                {
                    return false;
                }

                // 获取白板信息
                var whiteboard = await GetWhiteboardInfo(cancellationToken);
                if (whiteboard == null)
                {
                    return false;
                }

                // 获取API基础URL和用户Token
                var apiBaseUrl = MainWindow.Settings?.Dlass?.ApiBaseUrl ?? "https://dlass.tech";
                var userToken = MainWindow.Settings?.Dlass?.UserToken;

                // 准备上传参数
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var fileExtension = Path.GetExtension(filePath).ToLower();
                var title = fileName;
                string fileType;
                string tags;
                if (fileExtension == ".zip")
                {
                    fileType = "多页面墨迹压缩包";
                    tags = "自动上传,多页面,zip,压缩包";
                }
                else if (fileExtension == ".icstk")
                {
                    fileType = "墨迹文件";
                    tags = "自动上传,墨迹,icstk";
                }
                else if (fileExtension == ".xml")
                {
                    fileType = "XML文件";
                    tags = "自动上传,xml";
                }
                else
                {
                    fileType = "笔记";
                    tags = "自动上传,笔记,png";
                }
                var description = $"自动上传的{fileType} - {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

                // 创建API客户端并上传文件
                var apiClient = new DlassApiClient(APP_ID, APP_SECRET, apiBaseUrl, userToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var uploadResult = await apiClient.UploadNoteAsync<UploadNoteResponse>(
                        "/api/whiteboard/upload_note",
                        filePath,
                        whiteboard.BoardId,
                        whiteboard.SecretKey,
                        title,
                        description,
                        tags,
                        cancellationToken);

                    if (uploadResult != null && uploadResult.Success)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                finally
                {
                    apiClient.Dispose();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 获取白板信息
        /// </summary>
        private async Task<WhiteboardInfo> GetWhiteboardInfo(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var selectedClassName = MainWindow.Settings?.Dlass?.SelectedClassName;
                if (string.IsNullOrEmpty(selectedClassName))
                {
                    LogHelper.WriteLogToFile("[DlassUploadQueue] 上传失败：未选择班级", LogHelper.LogType.Error);
                    return null;
                }

                var userToken = MainWindow.Settings?.Dlass?.UserToken;
                if (string.IsNullOrEmpty(userToken))
                {
                    LogHelper.WriteLogToFile("[DlassUploadQueue] 上传失败：未设置用户Token", LogHelper.LogType.Error);
                    return null;
                }

                var apiBaseUrl = MainWindow.Settings?.Dlass?.ApiBaseUrl ?? "https://dlass.tech";

                // 创建API客户端并获取白板信息
                var apiClient = new DlassApiClient(APP_ID, APP_SECRET, apiBaseUrl, userToken);
                try
                {
                    var authData = new
                    {
                        app_id = APP_ID,
                        app_secret = APP_SECRET,
                        user_token = userToken
                    };

                    var authResult = await apiClient.PostAsync<AuthWithTokenResponse>("/api/whiteboard/framework/auth-with-token", authData, requireAuth: false, cancellationToken: cancellationToken);

                    if (authResult == null || !authResult.Success || authResult.Whiteboards == null)
                    {
                        LogHelper.WriteLogToFile("[DlassUploadQueue] 上传失败：无法获取白板信息", LogHelper.LogType.Error);
                        return null;
                    }

                    // 查找匹配班级的白板
                    var whiteboard = authResult.Whiteboards
                        .FirstOrDefault(w => !string.IsNullOrEmpty(w.ClassName) && w.ClassName == selectedClassName);

                    if (whiteboard == null || string.IsNullOrEmpty(whiteboard.BoardId) || string.IsNullOrEmpty(whiteboard.SecretKey))
                    {
                        LogHelper.WriteLogToFile($"[DlassUploadQueue] 上传失败：未找到班级'{selectedClassName}'对应的白板", LogHelper.LogType.Error);
                        return null;
                    }

                    return whiteboard;
                }
                finally
                {
                    apiClient.Dispose();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}