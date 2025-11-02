using Ink_Canvas.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// Dlass笔记自动上传辅助类
    /// </summary>
    public class DlassNoteUploader
    {
        private const string APP_ID = "app_WkjocWqsrVY7T6zQV2CfiA";
        private const string APP_SECRET = "o7dx5b5ASGUMcM72PCpmRQYAhSijqaOVHoGyBK0IxbA";

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
        /// 异步上传笔记文件到Dlass（支持PNG和ICSTK格式）
        /// </summary>
        /// <param name="filePath">文件路径（支持PNG和ICSTK）</param>
        /// <returns>是否上传成功</returns>
        public static async Task<bool> UploadNoteFileAsync(string filePath)
        {
            return await UploadPngNoteAsync(filePath);
        }

        /// <summary>
        /// 异步上传PNG文件到Dlass
        /// </summary>
        /// <param name="pngFilePath">PNG文件路径</param>
        /// <returns>是否上传成功</returns>
        public static async Task<bool> UploadPngNoteAsync(string pngFilePath)
        {
            try
            {
                // 检查是否启用自动上传
                if (MainWindow.Settings?.Dlass?.IsAutoUploadNotes != true)
                {
                    return false;
                }

                // 检查文件是否存在
                if (!File.Exists(pngFilePath))
                {
                    LogHelper.WriteLogToFile($"上传失败：文件不存在 - {pngFilePath}", LogHelper.LogType.Error);
                    return false;
                }

                // 检查文件扩展名
                var fileExtension = Path.GetExtension(pngFilePath).ToLower();
                if (fileExtension != ".png" && fileExtension != ".icstk")
                {
                    LogHelper.WriteLogToFile($"上传失败：不支持的文件格式 - {fileExtension}，仅支持PNG和ICSTK", LogHelper.LogType.Error);
                    return false;
                }

                // 检查文件大小（最大10MB）
                var fileInfo = new FileInfo(pngFilePath);
                if (fileInfo.Length > 10 * 1024 * 1024)
                {
                    LogHelper.WriteLogToFile($"上传失败：文件过大（{fileInfo.Length / 1024 / 1024}MB），超过10MB限制", LogHelper.LogType.Error);
                    return false;
                }

                // 获取设置的班级名称
                var selectedClassName = MainWindow.Settings?.Dlass?.SelectedClassName;
                if (string.IsNullOrEmpty(selectedClassName))
                {
                    LogHelper.WriteLogToFile("上传失败：未选择班级", LogHelper.LogType.Error);
                    return false;
                }

                // 获取用户Token
                var userToken = MainWindow.Settings?.Dlass?.UserToken;
                if (string.IsNullOrEmpty(userToken))
                {
                    LogHelper.WriteLogToFile("上传失败：未设置用户Token", LogHelper.LogType.Error);
                    return false;
                }

                // 获取API基础URL
                var apiBaseUrl = MainWindow.Settings?.Dlass?.ApiBaseUrl ?? "https://dlass.tech";

                // 创建API客户端并获取白板信息
                DlassApiClient apiClient = null;
                try
                {
                    apiClient = new DlassApiClient(APP_ID, APP_SECRET, apiBaseUrl, userToken);

                    // 调用认证接口获取白板列表
                    var authData = new
                    {
                        app_id = APP_ID,
                        app_secret = APP_SECRET,
                        user_token = userToken
                    };

                    var authResult = await apiClient.PostAsync<AuthWithTokenResponse>("/api/whiteboard/framework/auth-with-token", authData, requireAuth: false);

                    if (authResult == null || !authResult.Success || authResult.Whiteboards == null)
                    {
                        LogHelper.WriteLogToFile("上传失败：无法获取白板信息", LogHelper.LogType.Error);
                        return false;
                    }

                    // 查找匹配班级的白板
                    var whiteboard = authResult.Whiteboards
                        .FirstOrDefault(w => !string.IsNullOrEmpty(w.ClassName) && w.ClassName == selectedClassName);

                    if (whiteboard == null || string.IsNullOrEmpty(whiteboard.BoardId) || string.IsNullOrEmpty(whiteboard.SecretKey))
                    {
                        LogHelper.WriteLogToFile($"上传失败：未找到班级'{selectedClassName}'对应的白板", LogHelper.LogType.Error);
                        return false;
                    }

                    // 准备上传参数
                    var fileName = Path.GetFileNameWithoutExtension(pngFilePath);
                    var title = fileName;
                    var fileType = fileExtension == ".icstk" ? "墨迹文件" : "笔记";
                    var description = $"自动上传的{fileType} - {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                    var tags = fileExtension == ".icstk" ? "自动上传,墨迹,icstk" : "自动上传,笔记,png";

                    // 上传文件
                    var uploadResult = await apiClient.UploadNoteAsync<UploadNoteResponse>(
                        "/api/whiteboard/upload_note",
                        pngFilePath,
                        whiteboard.BoardId,
                        whiteboard.SecretKey,
                        title,
                        description,
                        tags);

                    if (uploadResult != null && uploadResult.Success)
                    {
                        LogHelper.WriteLogToFile($"笔记上传成功：{fileName} -> {uploadResult.FileUrl}", LogHelper.LogType.Event);
                        return true;
                    }
                    else
                    {
                        LogHelper.WriteLogToFile($"上传失败：服务器响应失败 - {uploadResult?.Message ?? "未知错误"}", LogHelper.LogType.Error);
                        return false;
                    }
                }
                finally
                {
                    apiClient?.Dispose();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"上传笔记时出错: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }
    }
}

