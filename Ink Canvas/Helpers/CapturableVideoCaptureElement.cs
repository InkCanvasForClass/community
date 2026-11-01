using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using WPFMediaKit.DirectShow.Controls;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 继承自 WPFMediaKit 的 VideoCaptureElement，额外暴露从 D3DImage 直接拍照的能力。
    /// 参考 EasiCamera.Control.CameraPreviewer：
    ///   public BitmapSource CameraImage => base.D3DImage.GetBitmapSource();
    /// D3DImage 属性是 protected，只能在子类中访问。
    /// </summary>
    /// <remarks>
    /// 拍照路径：CaptureCurrentFrame() —— 直接从 D3DImage.CopyBackBuffer 拿 BitmapSource（GPU 内存拷贝）。
    /// 不依赖 NewVideoSample 事件：VMR9 Renderless 模式下 SampleGrabber 无法连入图，事件不触发。
    /// D3DImage.CopyBackBuffer 是 protected virtual 方法，返回 BitmapSource，
    /// 反射调用可绕过可见性。WPF 的 D3DImage 在子类中可以通过 D3DImage 属性拿到引用。
    /// </remarks>
    public class CapturableVideoCaptureElement : VideoCaptureElement
    {
        private bool _analysisEnabled;
        private int _analysisVersion;
        private long _analysisAt;
        private sealed record AnalysisFrame(BitmapSource Image, long At);
        private AnalysisFrame _analysisFrame;

        internal bool AnalysisEnabled
        {
            set
            {
                if (System.Threading.Volatile.Read(ref _analysisEnabled) == value) return;
                System.Threading.Volatile.Write(ref _analysisEnabled, value);
                System.Threading.Interlocked.Increment(ref _analysisVersion);
                System.Threading.Volatile.Write(ref _analysisFrame, null);
            }
        }

        internal BitmapSource GetAnalysisFrame()
        {
            var frame = System.Threading.Volatile.Read(ref _analysisFrame);
            return frame != null && Environment.TickCount64 - frame.At < 1000 ? frame.Image : null;
        }

        public CapturableVideoCaptureElement()
        {
            NewVideoSample += (_, e) =>
            {
                if (!System.Threading.Volatile.Read(ref _analysisEnabled) || e?.VideoFrame == null) return;
                long now = Environment.TickCount64, previous = System.Threading.Interlocked.Read(ref _analysisAt);
                if (now - previous < 400 || System.Threading.Interlocked.CompareExchange(ref _analysisAt, now, previous) != previous) return;
                int version = System.Threading.Volatile.Read(ref _analysisVersion);
                try
                {
                    var image = CreateAnalysisFrame(e.VideoFrame);
                    if (version == System.Threading.Volatile.Read(ref _analysisVersion) && System.Threading.Volatile.Read(ref _analysisEnabled))
                        System.Threading.Volatile.Write(ref _analysisFrame, new AnalysisFrame(image, now));
                }
                catch (Exception ex) { Debug.WriteLine($"Video analysis frame: {ex.Message}"); }
            };
        }

        internal static BitmapSource CreateAnalysisFrame(System.Drawing.Bitmap source)
        {
            double ratio = Math.Min(1, 500.0 / Math.Max(source.Width, source.Height));
            int width = Math.Max(1, (int)(source.Width * ratio)), height = Math.Max(1, (int)(source.Height * ratio));
            using var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap)) graphics.DrawImage(source, 0, 0, width, height);
            var data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);
            try
            {
                var bytes = new byte[data.Stride * height];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                var image = BitmapSource.Create(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgr24, null, bytes, data.Stride);
                image.Freeze();
                return image;
            }
            finally { bitmap.UnlockBits(data); }
        }

        /// <summary>
        /// 从当前 D3DImage 拷贝一帧为 BitmapSource（线程安全，可跨线程使用）。
        /// 返回 null 表示当前没有可用帧（如设备未打开/前端缓冲未就绪）。
        /// </summary>
        public BitmapSource CaptureCurrentFrame()
        {
            try
            {
                // D3DImage 是 D3DRenderer 基类的 protected 属性
                // 用反射获取（避免子类 visibility 问题）
                var d3dImage = GetD3DImage();
                if (d3dImage == null)
                {
                    LogCaptureDiag("CaptureCurrentFrame: GetD3DImage 返回 null（D3DRenderer.D3DImage 属性可能命名不同或未初始化）");
                    return null;
                }
                if (!d3dImage.IsFrontBufferAvailable)
                {
                    LogCaptureDiag("CaptureCurrentFrame: D3DImage.IsFrontBufferAvailable=false（预览未就绪）");
                    return null;
                }

                // D3DImage.CopyBackBuffer() 是 protected virtual，返回 BitmapSource
                // 用反射调用（不能直接调用 protected 方法）
                var method = typeof(D3DImage).GetMethod(
                    "CopyBackBuffer",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null)
                {
                    // 尝试另一个常见方法名（不同 WPF 版本可能不同）
                    method = typeof(D3DImage).GetMethod(
                        "CopyBackBuffer",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                }
                if (method == null)
                {
                    LogCaptureDiag("CaptureCurrentFrame: 找不到 D3DImage.CopyBackBuffer 方法");
                    return null;
                }

                var result = method.Invoke(d3dImage, null) as BitmapSource;
                if (result == null)
                {
                    LogCaptureDiag("CaptureCurrentFrame: CopyBackBuffer 返回 null");
                    return null;
                }

                if (!result.IsFrozen)
                {
                    result = result.Clone();
                    result.Freeze();
                }
                LogCaptureDiag($"CaptureCurrentFrame: 成功，pixel={result.PixelWidth}×{result.PixelHeight}");
                return result;
            }
            catch (Exception ex)
            {
                LogCaptureDiag($"CaptureCurrentFrame 异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 当前 D3DImage 的前端缓冲是否就绪（可用于拍照）。
        /// </summary>
        public bool IsFrontBufferAvailable
        {
            get
            {
                try
                {
                    var d3dImage = GetD3DImage();
                    return d3dImage?.IsFrontBufferAvailable ?? false;
                }
                catch { return false; }
            }
        }

        /// <summary>
        /// 反射获取 D3DRenderer 基类的 D3DImage protected 属性。
        /// EasiCamera 的 CameraPreviewer 通过 base.D3DImage 访问，可见性是 protected。
        /// </summary>
        private D3DImage GetD3DImage()
        {
            try
            {
                // D3DImage 是 D3DRenderer 类的 protected 属性
                var prop = typeof(D3DRenderer).GetProperty(
                    "D3DImage",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (prop == null)
                {
                    // 备选：可能是字段（_d3dImage 或 m_d3dImage）
                    var fields = typeof(D3DRenderer).GetFields(
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    foreach (var f in fields)
                    {
                        if (typeof(D3DImage).IsAssignableFrom(f.FieldType))
                        {
                            return f.GetValue(this) as D3DImage;
                        }
                    }
                    return null;
                }
                return prop.GetValue(this, null) as D3DImage;
            }
            catch { return null; }
        }

        private static void LogCaptureDiag(string message)
        {
            try
            {
                LogHelper.WriteLogToFile($"[CapturableVideoCaptureElement] {message}", LogHelper.LogType.Info);
                Debug.WriteLine($"[CapturableVideoCaptureElement] {message}");
            }
            catch { }
        }
    }
}
