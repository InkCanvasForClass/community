using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// WebDAV上传队列
    /// </summary>
    public class WebDavUploadQueue : BaseUploadQueue
    {
        /// <summary>
        /// 队列文件名
        /// </summary>
        protected override string QueueFileName => "WebDavUploadQueue.json";

        /// <summary>
        /// 检查上传是否启用
        /// </summary>
        protected override bool IsUploadEnabled()
        {
            return WebDavUploader.IsWebDavEnabled();
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

                // 检查WebDAV是否仍然启用
                if (!WebDavUploader.IsWebDavEnabled())
                {
                    return false;
                }

                // 调用WebDavUploader进行实际上传
                var success = await WebDavUploader.UploadFileAsync(filePath, cancellationToken);
                return success;
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
    }
}