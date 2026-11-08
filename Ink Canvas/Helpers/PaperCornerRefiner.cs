using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 纸张角点亚像素级精细化器。
    /// 核心原理：
    /// 在 500px 低分辨率上粗测定位出的角点，映射回原始高分辨率（如 1080p/4K）时会存在数像素的缩放量化误差。
    /// 本类截取原图对应角点的局部邻域 ROI（避免对全图做大尺寸灰度转换），利用正交梯度约束与正交法向量迭代（CornerSubPix），
    /// 将角点精度提升到 0.1 像素亚像素级，消除梯形校正后的边缘倾斜与局部文字拉伸。
    /// </summary>
    internal static class PaperCornerRefiner
    {
        private const int DefaultWinSize = 11; // 局部窗口半宽，对应 23x23 像素局部邻域
        private const int MaxRefineOffset = 25; // 允许的最大亚像素漂移距离（防局部杂乱纹理漂移）

        /// <summary>
        /// 对粗测出的 4 个角点在原图上进行局部亚像素精修。
        /// </summary>
        /// <param name="src">原始全分辨率图像（Mat 类型，单通道灰度或多通道彩色均可）。</param>
        /// <param name="coarseCorners">粗测出的 4 个角点（tl, tr, br, bl）。</param>
        /// <param name="winSize">搜索窗口半长（默认为 11 像素）。</param>
        /// <returns>精修后的 4 个角点（Point2f），如果精修失败或漂移超标则平滑保留粗测点。</returns>
        public static Point2f[] RefineCornersSubPix(Mat src, IEnumerable<Point> coarseCorners, int winSize = DefaultWinSize)
        {
            if (src == null || src.Empty() || coarseCorners == null) return null;

            var cornersList = coarseCorners.ToList();
            if (cornersList.Count != 4) return null;

            var result = new Point2f[4];
            int imgW = src.Width;
            int imgH = src.Height;

            for (int i = 0; i < 4; i++)
            {
                var pt = cornersList[i];
                result[i] = new Point2f(pt.X, pt.Y);

                // 边界安全防护：如果角点过分贴近图像边缘，无法截取完整 ROI，直接使用原粗测点
                int margin = winSize * 2;
                if (pt.X < margin || pt.X >= imgW - margin || pt.Y < margin || pt.Y >= imgH - margin)
                {
                    continue;
                }

                try
                {
                    // 局部 ROI 截取（例如 (winSize*2) x (winSize*2) 的局部区域）
                    int roiX = Math.Max(0, pt.X - margin);
                    int roiY = Math.Max(0, pt.Y - margin);
                    int roiW = Math.Min(imgW - roiX, margin * 2);
                    int roiH = Math.Min(imgH - roiY, margin * 2);

                    var roiRect = new Rect(roiX, roiY, roiW, roiH);
                    using var roiSrc = new Mat(src, roiRect);

                    using var roiGray = new Mat();
                    if (roiSrc.Channels() == 1)
                    {
                        roiSrc.CopyTo(roiGray);
                    }
                    else if (roiSrc.Channels() == 4)
                    {
                        Cv2.CvtColor(roiSrc, roiGray, ColorConversionCodes.BGRA2GRAY);
                    }
                    else
                    {
                        Cv2.CvtColor(roiSrc, roiGray, ColorConversionCodes.BGR2GRAY);
                    }

                    // 局部坐标
                    var localPts = new[] { new Point2f(pt.X - roiX, pt.Y - roiY) };
                    var criteria = new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 0.01);

                    Cv2.CornerSubPix(roiGray, localPts, new Size(winSize, winSize), new Size(-1, -1), criteria);

                    // 还原到全图坐标
                    float refinedX = localPts[0].X + roiX;
                    float refinedY = localPts[0].Y + roiY;

                    // 合理性校验：精修点与粗测点的漂移不得超过 MaxRefineOffset
                    float dx = refinedX - pt.X;
                    float dy = refinedY - pt.Y;
                    if (Math.Sqrt(dx * dx + dy * dy) <= MaxRefineOffset)
                    {
                        result[i] = new Point2f(refinedX, refinedY);
                    }
                }
                catch
                {
                    // 局部精修异常时不影响主流程，平滑降级使用粗测角点
                    result[i] = new Point2f(pt.X, pt.Y);
                }
            }

            return result;
        }
    }
}
