using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using Point = OpenCvSharp.Point;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 照片矫正加速模式可用性解析与统一调度入口。
    /// 用户在 UI 选择的加速模式（CPU/OpenCL/CUDA）经 <see cref="ResolveEffective"/> 解析后得到实际可用模式，
    /// 检测算法按实际模式分派到对应实现：<see cref="CpuPaperDetector"/> / <see cref="OpenCLPaperDetector"/> / <see cref="CudaPaperDetector"/>。
    /// </summary>
    /// <remarks>
    /// "CUDA" 选项的实际机制：检测到 NVIDIA 显卡后，通过 OPENCV_OPENCL_DEVICE 环境变量强制 OpenCV
    /// 选用 NVIDIA OpenCL 平台（NVIDIA OpenCL 实现底层即由 CUDA 驱动），无需引入 OpenCvSharp4WithCuda 包
    /// 与本地 CUDA Toolkit，避免 +200MB 包体积与编译复杂度。NVIDIA 显卡上 OpenCL 与原生 CUDA 性能差异 &lt; 5ms。
    /// </remarks>
    internal static class PaperDetectAccelerationResolver
    {
        // OpenCvSharp4 4.13 移除了显式 OpenCL 控制 API（Cv2.HaveOpenCL/UseOpenCL）。
        // UMat 内部由 OpenCV 自动检测 OpenCL 可用性并透明切换；不可用时回退到 CPU。
        // 此处认为 OpenCL "可用"——实际是否真正走 GPU 由 OpenCV 运行时决定。
        private static readonly Lazy<bool> _openCLAvailable = new Lazy<bool>(() =>
        {
            try
            {
                // 尝试创建一个 UMat 实例验证 OpenCL 子系统可初始化
                using var probe = new UMat();
                return probe != null;
            }
            catch { return false; }
        });

        // NVIDIA 显卡检测：通过 LoadLibrary 加载 nvcuda.dll（NVIDIA 用户态 CUDA 驱动 DLL）。
        // 存在即说明有 NVIDIA 显卡 + 驱动已安装。AMD/Intel 显卡不会存在此 DLL。
        private static readonly Lazy<bool> _cudaAvailable = new Lazy<bool>(() =>
        {
            try
            {
                // nvcuda.dll 是 NVIDIA CUDA 驱动的用户态入口，存在于所有 NVIDIA 显卡驱动包中
                // LoadLibrary 会按系统搜索路径查找，加载后立即 FreeLibrary 释放引用计数
                IntPtr h = LoadLibrary("nvcuda.dll");
                if (h == IntPtr.Zero) return false;
                FreeLibrary(h);
                return true;
            }
            catch { return false; }
        });

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        /// <summary>
        /// 解析用户选择的加速模式到实际可用的模式。
        /// CUDA（NVIDIA OpenCL）不可用时回退到通用 OpenCL，OpenCL 不可用时回退到 CPU。
        /// </summary>
        public static PhotoCorrectionAccelerationMode ResolveEffective(PhotoCorrectionAccelerationMode requested)
        {
            switch (requested)
            {
                case PhotoCorrectionAccelerationMode.CUDA:
                    if (_cudaAvailable.Value) return PhotoCorrectionAccelerationMode.CUDA;
                    if (_openCLAvailable.Value) return PhotoCorrectionAccelerationMode.OpenCL;
                    return PhotoCorrectionAccelerationMode.Cpu;
                case PhotoCorrectionAccelerationMode.OpenCL:
                    return _openCLAvailable.Value ? PhotoCorrectionAccelerationMode.OpenCL : PhotoCorrectionAccelerationMode.Cpu;
                default:
                    return PhotoCorrectionAccelerationMode.Cpu;
            }
        }
    }

    /// <summary>
    /// 照片矫正检测器统一接口。三套实现（CPU/OpenCL/CUDA）共用同一签名，
    /// 由 <see cref="PaperDetectorFactory"/> 按当前加速模式实例化。
    /// </summary>
    internal interface IPaperDetector
    {
        /// <summary>
        /// 在给定帧中检测 A4 纸四个角点，按 (左上、右上、右下、左下) 顺序返回，坐标已还原到原始帧空间。
        /// </summary>
        bool TryDetect(Bitmap frame, out List<Point> cornersOut, bool verbose);
    }

    /// <summary>
    /// 按加速模式创建对应检测器。CUDA 模式尝试加载 OpenCvSharp4WithCuda 类型失败时回退到 OpenCL 检测器。
    /// </summary>
    internal static class PaperDetectorFactory
    {
        private static IPaperDetector _cpu;
        private static IPaperDetector _openCL;
        private static IPaperDetector _cuda;
        private static Exception _cudaLoadError;

        public static IPaperDetector Get(PhotoCorrectionAccelerationMode mode)
        {
            var effective = PaperDetectAccelerationResolver.ResolveEffective(mode);
            return effective switch
            {
                PhotoCorrectionAccelerationMode.CUDA => GetCudaOrDefault(),
                PhotoCorrectionAccelerationMode.OpenCL => GetOpenCL(),
                _ => GetCpu(),
            };
        }

        private static IPaperDetector GetCpu() => _cpu ??= new CpuPaperDetector();
        private static IPaperDetector GetOpenCL() => _openCL ??= new OpenCLPaperDetector();

        private static IPaperDetector GetCudaOrDefault()
        {
            if (_cuda != null) return _cuda;
            try
            {
                // 尝试加载 CUDA 检测器类型；若 OpenCvSharp4WithCuda 未引用，类型解析会抛异常
                _cuda = new CudaPaperDetector();
            }
            catch (Exception ex)
            {
                _cudaLoadError = ex;
                _cuda = GetOpenCL(); // 回退到 OpenCL
            }
            return _cuda;
        }
    }

    /// <summary>
    /// 共用检测参数与筛选逻辑（三套实现共用）。
    /// 算法参考 CSDN 博客：灰度→高斯模糊→Canny→dilate→findContours→convexHull→approxPolyDP→角度筛选→boundingRect 最大。
    /// </summary>
    internal abstract class PaperDetectorBase
    {
        protected const int PaperDetectTargetWidth = 500;
        protected const double MaxCosine = 0.4;
        protected const double AreaMinRatio = 0.05;
        protected const double AreaMaxRatio = 0.98;
        protected const double ApproxEpsilonRatio = 0.02;

        // 三组尝试的 Canny 阈值
        protected static readonly double[][] DefaultThresholdSets =
        {
            new[] { 50.0, 150.0 },
            new[] { 30.0, 100.0 },
        };

        protected static double GetAngleCosine(Point pt1, Point pt2, Point pt0)
        {
            double dx1 = pt1.X - pt0.X, dy1 = pt1.Y - pt0.Y;
            double dx2 = pt2.X - pt0.X, dy2 = pt2.Y - pt0.Y;
            return (dx1 * dx2 + dy1 * dy2) /
                   Math.Sqrt((dx1 * dx1 + dy1 * dy1) * (dx2 * dx2 + dy2 * dy2) + 1e-10);
        }

        protected static double GetMaxCosine(Point[] approx)
        {
            double maxCos = 0;
            for (int j = 2; j < 5; j++)
            {
                double cos = Math.Abs(GetAngleCosine(approx[j % 4], approx[j - 2], approx[j - 1]));
                if (cos > maxCos) maxCos = cos;
            }
            return maxCos;
        }

        protected static Point[] OrderCorners(Point[] pts)
        {
            var tl = pts.OrderBy(p => p.X + p.Y).First();
            var br = pts.OrderByDescending(p => p.X + p.Y).First();
            var tr = pts.OrderBy(p => p.Y - p.X).First();
            var bl = pts.OrderByDescending(p => p.Y - p.X).First();
            return new[] { tl, tr, br, bl };
        }

        protected static List<(Point[] approx, double area, OpenCvSharp.Rect bound)> FilterContours(
            Point[][] contours, double imgArea)
        {
            var found = new List<(Point[] approx, double area, OpenCvSharp.Rect bound)>();
            foreach (var contour in contours)
            {
                if (contour.Length < 4) continue;
                int[] hullIdx = Cv2.ConvexHullIndices(contour, false);
                if (hullIdx.Length < 4) continue;
                Point[] hull = hullIdx.Select(idx => contour[idx]).ToArray();
                double hullPeri = Cv2.ArcLength(hull, true);
                Point[] approx = Cv2.ApproxPolyDP(hull, ApproxEpsilonRatio * hullPeri, true);
                if (approx.Length != 4) continue;
                if (!Cv2.IsContourConvex(approx)) continue;

                double area = Math.Abs(Cv2.ContourArea(approx));
                if (area < imgArea * AreaMinRatio) continue;
                if (area > imgArea * AreaMaxRatio) continue;

                double maxCos = GetMaxCosine(approx);
                if (maxCos >= MaxCosine) continue;

                OpenCvSharp.Rect bound = Cv2.BoundingRect(approx);
                found.Add((approx, area, bound));
            }
            found.Sort((a, b) => (b.bound.Width * b.bound.Height).CompareTo(a.bound.Width * a.bound.Height));
            return found;
        }
    }
}
