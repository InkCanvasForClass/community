using System;
using System.IO;

namespace Ink_Canvas.Helpers
{
    /// <summary>统一 PPT 墨迹与附属持久化数据使用的演示文稿标识和目录。</summary>
    internal static class PPTPresentationStorage
    {
        public static string CreatePresentationId(string presentationName, int slideCount, string presentationPath)
        {
            if (string.IsNullOrWhiteSpace(presentationName))
                throw new ArgumentException("演示文稿名称不能为空。", nameof(presentationName));
            if (slideCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(slideCount));
            if (string.IsNullOrWhiteSpace(presentationPath))
                throw new ArgumentException("演示文稿路径不能为空。", nameof(presentationPath));

            // 与原 PPTInkManager.GeneratePresentationId 使用完全相同的路径哈希算法。
            string hash = HashHelper.GetFileHash(presentationPath);
            return $"{presentationName}_{slideCount}_{hash}";
        }

        public static string GetPresentationFolderPath(string autoSaveLocation, string presentationName,
            int slideCount, string presentationPath)
        {
            if (string.IsNullOrWhiteSpace(autoSaveLocation))
                throw new ArgumentException("自动保存目录不能为空。", nameof(autoSaveLocation));

            return Path.Combine(autoSaveLocation, "Auto Saved - Presentations",
                CreatePresentationId(presentationName, slideCount, presentationPath));
        }
    }
}
