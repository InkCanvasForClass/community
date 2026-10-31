using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// CPU 检测器：所有 OpenCV 运算在 CPU 上执行（Mat 类型）。
    /// 兼容性最好，所有平台和显卡都可使用。性能：~20-30ms/帧（500px 下采样后）。
    /// </summary>
    internal sealed class CpuPaperDetector : PaperDetectorBase, IPaperDetector
    {
        public bool TryDetect(Bitmap frame, out List<Point> cornersOut, bool verbose)
        {
            cornersOut = null;
            try
            {
                if (frame == null) return false;
                using var src = BitmapConverter.ToMat(frame);
                if (src.Empty()) return false;

                int ow = src.Width, oh = src.Height;
                double scale = 1.0;
                Mat detect = src;
                if (ow > PaperDetectTargetWidth)
                {
                    scale = (double)ow / PaperDetectTargetWidth;
                    int nh = (int)Math.Round(oh / scale);
                    var resized = new Mat();
                    Cv2.Resize(src, resized, new Size(PaperDetectTargetWidth, nh), 0, 0, InterpolationFlags.Area);
                    detect = resized;
                }

                using var gray = new Mat();
                int ch = detect.Channels();
                if (ch == 1) detect.CopyTo(gray);
                else if (ch == 4) Cv2.CvtColor(detect, gray, ColorConversionCodes.BGRA2GRAY);
                else Cv2.CvtColor(detect, gray, ColorConversionCodes.BGR2GRAY);

                using var blurred = new Mat();
                Cv2.GaussianBlur(gray, blurred, new Size(3, 3), 2, 2);

                double mean = Cv2.Mean(blurred).Val0;
                // 自适应 + 经典 + 低阈值三组
                double baseLower = Math.Max(30, (1 - 0.33) * (mean == 0 ? 100 : mean));
                double baseUpper = Math.Min(220, (1 + 0.33) * (mean == 0 ? 100 : mean));
                if (baseUpper - baseLower < 20) baseUpper = baseLower + 20;
                var thresholdSets = new[]
                {
                    new[] { baseLower, baseUpper },
                    new[] { 50.0, 150.0 },
                    new[] { 30.0, 100.0 },
                };

                double imgArea = detect.Width * detect.Height;
                List<(Point[] approx, double area, OpenCvSharp.Rect bound)> candidates = null;
                double usedLower = 0, usedUpper = 0;
                int[] contourCounts = new int[thresholdSets.Length];

                for (int i = 0; i < thresholdSets.Length; i++)
                {
                    using var edges = new Mat();
                    Cv2.Canny(blurred, edges, thresholdSets[i][0], thresholdSets[i][1], 3);
                    Cv2.Dilate(edges, edges, null, new Point(-1, -1), 3, BorderTypes.Constant, default);
                    Cv2.FindContours(edges, out Point[][] contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
                    contourCounts[i] = contours.Length;
                    var found = FilterContours(contours, imgArea);
                    if (found.Count > 0)
                    {
                        candidates = found;
                        usedLower = thresholdSets[i][0];
                        usedUpper = thresholdSets[i][1];
                        break;
                    }
                }

                if (candidates != null && candidates.Count > 0)
                {
                    var best = candidates[0];
                    var ordered = OrderCorners(best.approx);
                    if (scale != 1.0)
                    {
                        ordered = ordered
                            .Select(p => new Point((int)Math.Round(p.X * scale), (int)Math.Round(p.Y * scale)))
                            .ToArray();
                    }
                    cornersOut = ordered.ToList();

                    if (verbose)
                    {
                        var diag = candidates.Take(3)
                            .Select(c => $"area={c.area:F0} bound={c.bound.Width}x{c.bound.Height}")
                            .ToArray();
                        LogHelper.WriteLogToFile(
                            $"照片矫正[CPU]: 检测到角点 tl={ordered[0]}, tr={ordered[1]}, br={ordered[2]}, bl={ordered[3]} (scale={scale:F2}, n={candidates.Count}, mean={mean:F1}, Canny=[{usedLower:F0},{usedUpper:F0}], contours=[{string.Join(",", contourCounts)}])\n  候选: {string.Join(" | ", diag)}",
                            LogHelper.LogType.Trace);
                    }
                    return true;
                }

                if (verbose)
                {
                    LogHelper.WriteLogToFile(
                        $"照片矫正[CPU]: 未检测到 (contours=[{string.Join(",", contourCounts)}], scale={scale:F2}, mean={mean:F1})",
                        LogHelper.LogType.Trace);
                }
                return false;
            }
            catch (Exception ex)
            {
                if (verbose) LogHelper.WriteLogToFile($"照片矫正[CPU]: 异常 {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
        }
    }

    /// <summary>
    /// OpenCL 检测器：高耗时运算（Canny/Dilate/GaussianBlur）在 GPU 上执行（UMat 类型）。
    /// 兼容 NVIDIA/AMD/Intel 集显，需驱动支持 OpenCL。性能：~10-20ms/帧（GPU 加速 30-50%）。
    /// FindContours/ApproxPolyDP 等 CPU-only API 仍走 Mat（OpenCvSharp 限制）。
    /// </summary>
    internal class OpenCLPaperDetector : PaperDetectorBase, IPaperDetector
    {
        private static bool _oclInitialized;

        public OpenCLPaperDetector()
        {
            if (!_oclInitialized)
            {
                // OpenCvSharp4 4.13 移除了显式 OpenCL 控制 API。
                // UMat 内部由 OpenCV 自动启用 OpenCL；不可用时透明回退到 CPU。
                _oclInitialized = true;
            }
        }

        public bool TryDetect(Bitmap frame, out List<Point> cornersOut, bool verbose)
        {
            cornersOut = null;
            try
            {
                if (frame == null) return false;
                using var src = BitmapConverter.ToMat(frame);
                if (src.Empty()) return false;

                int ow = src.Width, oh = src.Height;
                double scale = 1.0;
                Mat detect = src;
                if (ow > PaperDetectTargetWidth)
                {
                    scale = (double)ow / PaperDetectTargetWidth;
                    int nh = (int)Math.Round(oh / scale);
                    var resized = new Mat();
                    Cv2.Resize(src, resized, new Size(PaperDetectTargetWidth, nh), 0, 0, InterpolationFlags.Area);
                    detect = resized;
                }

                // 上传到 GPU（UMat）：从这步开始的 GaussianBlur/Canny/Dilate 走 GPU
                using var detectU = detect.GetUMat(OpenCvSharp.AccessFlag.RW, OpenCvSharp.UMatUsageFlags.None);
                using var grayU = new UMat();
                int ch = detectU.Channels();
                if (ch == 1) detectU.CopyTo(grayU);
                else if (ch == 4) Cv2.CvtColor(detectU, grayU, ColorConversionCodes.BGRA2GRAY);
                else Cv2.CvtColor(detectU, grayU, ColorConversionCodes.BGR2GRAY);

                using var blurredU = new UMat();
                Cv2.GaussianBlur(grayU, blurredU, new Size(3, 3), 2, 2);

                // mean 需要下载到 CPU 计算（UMat 的 Mean 也支持但下载更稳）
                double mean;
                using (var blurredCpu = blurredU.GetMat(OpenCvSharp.AccessFlag.READ))
                {
                    mean = Cv2.Mean(blurredCpu).Val0;
                }

                double baseLower = Math.Max(30, (1 - 0.33) * (mean == 0 ? 100 : mean));
                double baseUpper = Math.Min(220, (1 + 0.33) * (mean == 0 ? 100 : mean));
                if (baseUpper - baseLower < 20) baseUpper = baseLower + 20;
                var thresholdSets = new[]
                {
                    new[] { baseLower, baseUpper },
                    new[] { 50.0, 150.0 },
                    new[] { 30.0, 100.0 },
                };

                double imgArea = detectU.Width * detectU.Height;
                List<(Point[] approx, double area, OpenCvSharp.Rect bound)> candidates = null;
                double usedLower = 0, usedUpper = 0;
                int[] contourCounts = new int[thresholdSets.Length];

                for (int i = 0; i < thresholdSets.Length; i++)
                {
                    // Canny + Dilate 在 GPU 上执行
                    using var edgesU = new UMat();
                    Cv2.Canny(blurredU, edgesU, thresholdSets[i][0], thresholdSets[i][1], 3);
                    Cv2.Dilate(edgesU, edgesU, null, new Point(-1, -1), 3, BorderTypes.Constant, default);

                    // FindContours 是 CPU-only API，必须下载到 Mat
                    using var edgesCpu = edgesU.GetMat(OpenCvSharp.AccessFlag.READ);
                    Cv2.FindContours(edgesCpu, out Point[][] contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
                    contourCounts[i] = contours.Length;
                    var found = FilterContours(contours, imgArea);
                    if (found.Count > 0)
                    {
                        candidates = found;
                        usedLower = thresholdSets[i][0];
                        usedUpper = thresholdSets[i][1];
                        break;
                    }
                }

                if (candidates != null && candidates.Count > 0)
                {
                    var best = candidates[0];
                    var ordered = OrderCorners(best.approx);
                    if (scale != 1.0)
                    {
                        ordered = ordered
                            .Select(p => new Point((int)Math.Round(p.X * scale), (int)Math.Round(p.Y * scale)))
                            .ToArray();
                    }
                    cornersOut = ordered.ToList();

                    if (verbose)
                    {
                        LogHelper.WriteLogToFile(
                            $"照片矫正[OpenCL]: 检测到角点 tl={ordered[0]}, tr={ordered[1]}, br={ordered[2]}, bl={ordered[3]} (scale={scale:F2}, n={candidates.Count}, mean={mean:F1}, Canny=[{usedLower:F0},{usedUpper:F0}], contours=[{string.Join(",", contourCounts)}])",
                            LogHelper.LogType.Trace);
                    }
                    return true;
                }

                if (verbose)
                {
                    LogHelper.WriteLogToFile(
                        $"照片矫正[OpenCL]: 未检测到 (contours=[{string.Join(",", contourCounts)}], scale={scale:F2}, mean={mean:F1})",
                        LogHelper.LogType.Trace);
                }
                return false;
            }
            catch (Exception ex)
            {
                if (verbose) LogHelper.WriteLogToFile($"照片矫正[OpenCL]: 异常 {ex.Message}（建议回退到 CPU）", LogHelper.LogType.Warning);
                return false;
            }
        }
    }

    /// <summary>
    /// CUDA 检测器（NVIDIA OpenCL 路径）：检测到 NVIDIA 显卡后，通过 OPENCV_OPENCL_DEVICE 环境变量
    /// 强制 OpenCV 选用 NVIDIA OpenCL 平台。NVIDIA OpenCL 实现底层由 CUDA 驱动，性能接近原生 CUDA。
    /// 此方案避免引入 OpenCvSharp4WithCuda 包 + CUDA Toolkit，包体积与编译复杂度不变。
    /// 不可用时（无 NVIDIA 显卡）由 PaperDetectorFactory 自动回退到通用 OpenCL 检测器。
    /// </summary>
    internal sealed class CudaPaperDetector : OpenCLPaperDetector
    {
        public CudaPaperDetector()
        {
            try
            {
                // OPENCV_OPENCL_DEVICE 格式：<Platform>:<Device type>:<Device name>
                // NVIDIA:GPU: 强制选 NVIDIA 平台的 GPU 设备
                // 环境变量必须在第一次 UMat 创建前设置才能生效，由 PaperDetectorFactory 保证
                // （工厂在构造本类之前不会创建任何 UMat）
                var prev = Environment.GetEnvironmentVariable("OPENCV_OPENCL_DEVICE");
                Environment.SetEnvironmentVariable("OPENCV_OPENCL_DEVICE", "NVIDIA:GPU:");
                LogHelper.WriteLogToFile(
                    $"照片矫正[CUDA]: 已设置 OPENCV_OPENCL_DEVICE=NVIDIA:GPU: 强制使用 NVIDIA OpenCL 平台（prev={prev ?? "null"}）。NVIDIA OpenCL 底层由 CUDA 驱动，性能接近原生 CUDA。",
                    LogHelper.LogType.Trace);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"照片矫正[CUDA]: 设置 OPENCV_OPENCL_DEVICE 失败 {ex.Message}", LogHelper.LogType.Warning);
            }
        }
    }
}
