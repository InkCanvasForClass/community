using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Drawing;
using Size = OpenCvSharp.Size;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 高清晰度文档扫描增强引擎（希沃 EasiCamera 同款方案）。
    /// 核心亮点：
    /// 1. 单通道平滑照度场估计与统一白平衡除法，从源头杜绝多通道除法导致的色偏与边缘色相分离；
    /// 2. CIE-Lab 色彩空间亮度与色度彻底解耦，文字暗部加深与反遮罩微锐化仅作用于 L 亮度通道，
    ///    物理上彻底杜绝字迹轮廓出现反向颜色（如红字边缘泛青绿光晕、蓝字边缘泛黄边等补色条纹）；
    /// 3. 文字前景与羽化过渡带软掩膜保护，坚决避免将文字边缘轮廓误判为白底而造成镂空断笔；
    /// 4. 连续平滑的色度增益模型，消除硬阈值截断阶跃，红笔、蓝墨水鲜艳纯正、边缘清爽干净；
    /// 5. 提供增效彩色（默认）、增效黑白、灰度模式三种情景滤镜，兼顾清晰度与保真度。
    /// </summary>
    internal static class DocumentScanEnhancer
    {
        /// <summary>
        /// 对输入位图应用文档扫描增强。
        /// </summary>
        /// <param name="srcBitmap">输入位图（未修改）。</param>
        /// <param name="mode">扫描方案模式（增效彩色 / 增效黑白 / 灰度模式）。</param>
        /// <returns>增强后的新位图；失败时返回 null。</returns>
        public static Bitmap Enhance(Bitmap srcBitmap, ScanColorMode mode)
        {
            if (srcBitmap == null) return null;

            try
            {
                using var src = BitmapConverter.ToMat(srcBitmap);
                if (src.Empty()) return null;

                using var enhanced = EnhanceMat(src, mode);
                if (enhanced == null || enhanced.Empty()) return null;

                return BitmapConverter.ToBitmap(enhanced);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[DocumentScan] 扫描增强异常: {ex.Message}", LogHelper.LogType.Warning);
                return null;
            }
        }

        /// <summary>
        /// 对 OpenCV Mat 进行核心扫描增强运算。
        /// </summary>
        public static Mat EnhanceMat(Mat src, ScanColorMode mode)
        {
            if (src == null || src.Empty()) return null;

            // 统一为 3 通道 BGR 格式处理
            Mat bgr = src;
            bool ownsBgr = false;
            if (src.Channels() == 4)
            {
                bgr = new Mat();
                Cv2.CvtColor(src, bgr, ColorConversionCodes.BGRA2BGR);
                ownsBgr = true;
            }
            else if (src.Channels() == 1)
            {
                bgr = new Mat();
                Cv2.CvtColor(src, bgr, ColorConversionCodes.GRAY2BGR);
                ownsBgr = true;
            }

            try
            {
                switch (mode)
                {
                    case ScanColorMode.EnhancedBlackWhite:
                        return ProcessEnhancedBlackWhite(bgr);
                    case ScanColorMode.Grayscale:
                        return ProcessGrayscale(bgr);
                    case ScanColorMode.EnhancedColor:
                    default:
                        return ProcessEnhancedColor(bgr);
                }
            }
            finally
            {
                if (ownsBgr) bgr.Dispose();
            }
        }

        /// <summary>
        /// 方案一：增效彩色（软件默认）。
        /// 具备字迹边缘保护、LAB 亮度色度解耦与色相防光晕的高保真彩色扫描滤镜：
        /// 1. 单通道照度除法统一归一化，消除顶灯光斑阴影，杜绝三通道独立除法造成的边缘色偏；
        /// 2. CIE-Lab 空间精准处理：L 亮度通道负责纯白底提纯与细节微锐化，色度通道 a/b 绝对不进行反遮罩相减；
        /// 3. 彻底消除字迹边缘反向互补色光晕（如红字青绿边、黑字亮白光晕）；
        /// 4. 连续平滑色度增益，红笔、蓝笔色彩饱满鲜亮，边缘过渡清爽自然。
        /// </summary>
        private static Mat ProcessEnhancedColor(Mat bgr)
        {
            int w = bgr.Width;
            int h = bgr.Height;

            // 1. 估计单通道背景照度场（提取纸张真实明度低频场）
            using var bgIlluminationSingle = EstimateIlluminationBackgroundSingleChannel(bgr);

            // 2. 通道除法统一归一化：BGR 三通道同时除以同一个背景明度标量场
            //    I_norm(c) = (I(c) / I_bg_single) * 255.0
            //    各通道色彩比例严格保持不变，从源头消灭除法造成的边缘反向色偏
            using var bgrFloat = new Mat();
            bgr.ConvertTo(bgrFloat, MatType.CV_32FC3);

            using var bg3Ch = new Mat();
            Cv2.CvtColor(bgIlluminationSingle, bg3Ch, ColorConversionCodes.GRAY2BGR);

            using var normFloat = new Mat();
            Cv2.Divide(bgrFloat, bg3Ch, normFloat, scale: 255.0);
            Cv2.Min(normFloat, new Scalar(255.0, 255.0, 255.0), normFloat);
            Cv2.Max(normFloat, new Scalar(0.0, 0.0, 0.0), normFloat);

            using var normalized = new Mat();
            normFloat.ConvertTo(normalized, MatType.CV_8UC3);

            // 3. 转入 CIE-Lab 色彩空间：实现明度（L）与色度（a, b）的完全解耦
            using var lab = new Mat();
            Cv2.CvtColor(normalized, lab, ColorConversionCodes.BGR2Lab);

            Mat[] labChannels = Cv2.Split(lab);
            using var lChan = labChannels[0]; // 亮度 L (0 ~ 255)
            using var aChan = labChannels[1]; // 绿-红色度 a (中性为 128)
            using var bChan = labChannels[2]; // 蓝-黄色度 b (中性为 128)

            // 4. 基于 L 亮度通道构建“文字与轮廓过渡带”软掩膜（取值 0.0 ~ 1.0）
            using var textWeight = CreateTextEdgeSoftMask(lChan);

            // 5. 背景纯白化与色度净化（消除纸张泛黄泛灰杂色，白底极致纯白雪净）：
            //    - 亮度 L：在背景区平滑拉升到 255，文字区受掩膜保护保留抗锯齿厚度；
            //    - 色度 a/b：在背景区平滑推向 128（中性灰/纯白），文字区保留原始色相
            using var whitenedL = WhiteLevelStretch(lChan, whiteCutoff: 215);

            using var cleanL = new Mat();
            BlendSingleChannelWithMask(whitenedL, lChan, textWeight, cleanL);

            using var neutralCh = new Mat(h, w, MatType.CV_8UC1, new Scalar(128));
            using var cleanA = new Mat();
            BlendSingleChannelWithMask(neutralCh, aChan, textWeight, cleanA);
            using var cleanB = new Mat();
            BlendSingleChannelWithMask(neutralCh, bChan, textWeight, cleanB);

            // 6. 文字暗部对比度加深（仅作用于 L 亮度通道）：
            //    对文字核心区域做轻微 Gamma 暗部下压，使浅灰字迹变得黑润浓郁，色度 a/b 完全不变（绝不偏色发焦）
            using var darkenedL = DarkenTextCore(cleanL, textWeight);

            // 7. 色彩饱和度平滑连续补偿（在 a, b 色度平面进行平滑增益）：
            //    消除硬阈值截断阶跃，红笔红亮纯正、蓝墨水深邃，边缘自然过渡
            using var boostedA = new Mat();
            using var boostedB = new Mat();
            BoostChromaSmoothly(cleanA, cleanB, boostedA, boostedB);

            // 8. 细节反遮罩微锐化（至关重要：仅作用于 L 亮度通道）：
            //    只增强明度对比度，色度通道 a/b 绝对不参与反遮罩相减！
            //    彻底根除 RGB 空间反遮罩导致的字边缘互补色（如红字边缘青绿边、黑字白边等反向颜色）
            using var blurredL = new Mat();
            Cv2.GaussianBlur(darkenedL, blurredL, new Size(0, 0), sigmaX: 2.0);

            using var sharpLFloat = new Mat();
            using var darkLFloat = new Mat();
            using var blurLFloat = new Mat();
            darkenedL.ConvertTo(darkLFloat, MatType.CV_32FC1);
            blurredL.ConvertTo(blurLFloat, MatType.CV_32FC1);

            // L_sharp = 1.22 * darkL - 0.22 * blurL
            Cv2.AddWeighted(darkLFloat, 1.22, blurLFloat, -0.22, 0, sharpLFloat);
            Cv2.Min(sharpLFloat, new Scalar(255.0), sharpLFloat);
            Cv2.Max(sharpLFloat, new Scalar(0.0), sharpLFloat);

            using var sharpL = new Mat();
            sharpLFloat.ConvertTo(sharpL, MatType.CV_8UC1);

            // 9. 合并增强后的 Lab 通道并转回 BGR
            using var finalLab = new Mat();
            Cv2.Merge(new[] { sharpL, boostedA, boostedB }, finalLab);

            var result = new Mat();
            Cv2.CvtColor(finalLab, result, ColorConversionCodes.Lab2BGR);

            foreach (var ch in labChannels) ch.Dispose();
            return result;
        }

        /// <summary>
        /// 方案二：增效黑白。
        /// 试卷与答题卡专用，高对比度纯白底纯黑字，保留完整连笔笔画。
        /// </summary>
        private static Mat ProcessEnhancedBlackWhite(Mat bgr)
        {
            // 1. 基于字迹保护与去阴影后的高质量彩色图
            using var enhancedColor = ProcessEnhancedColor(bgr);

            // 2. 转灰度
            using var gray = new Mat();
            Cv2.CvtColor(enhancedColor, gray, ColorConversionCodes.BGR2GRAY);

            // 3. 自适应局部阈值二值化（动态块大小保护笔画）
            using var binary = new Mat();
            int blockSize = (int)Math.Round(Math.Max(gray.Width, gray.Height) / 70.0);
            if (blockSize % 2 == 0) blockSize += 1;
            if (blockSize < 17) blockSize = 17;

            Cv2.AdaptiveThreshold(gray, binary, 255,
                AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, blockSize, 8.0);

            // 4. 转回 3 通道返回
            var result = new Mat();
            Cv2.CvtColor(binary, result, ColorConversionCodes.GRAY2BGR);
            return result;
        }

        /// <summary>
        /// 方案三：灰度模式。
        /// 去色，消除纸张底色与杂色，保留复杂示意图的平滑灰阶层次。
        /// </summary>
        private static Mat ProcessGrayscale(Mat bgr)
        {
            // 1. 基于字迹保护与去阴影后的高质量彩色图
            using var enhancedColor = ProcessEnhancedColor(bgr);

            // 2. 转灰度
            using var gray = new Mat();
            Cv2.CvtColor(enhancedColor, gray, ColorConversionCodes.BGR2GRAY);

            // 3. 对比度自适应直方图均衡（CLAHE）：突出微弱文字同时保持整体柔和
            using var clahe = Cv2.CreateCLAHE(clipLimit: 2.0, tileGridSize: new Size(8, 8));
            using var claheResult = new Mat();
            clahe.Apply(gray, claheResult);

            // 4. 转回 3 通道返回
            var result = new Mat();
            Cv2.CvtColor(claheResult, result, ColorConversionCodes.GRAY2BGR);
            return result;
        }

        /// <summary>
        /// 基于单通道灰度/亮度构建文字及笔画轮廓过渡带的软权重掩膜（CV_32FC1，0.0 ~ 1.0）。
        /// 值为 1.0 表示属于文字或笔画羽化边缘（受保护）；
        /// 值为 0.0 表示平坦空白纸张背景（可安全漂白/提纯）。
        /// </summary>
        private static Mat CreateTextEdgeSoftMask(Mat grayOrL)
        {
            // 1. 文字核心区：亮度较暗的区域（显著深于纸面）
            using var textCore = new Mat();
            Cv2.Threshold(grayOrL, textCore, 205, 255, ThresholdTypes.BinaryInv);

            // 2. 文字边缘与渐变轮廓区：利用形态学梯度检测高频边缘过渡带
            using var morphKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            using var grad = new Mat();
            Cv2.MorphologyEx(grayOrL, grad, MorphTypes.Gradient, morphKernel);

            using var edgeBand = new Mat();
            Cv2.Threshold(grad, edgeBand, 15, 255, ThresholdTypes.Binary);

            // 3. 合并核心区与边缘过渡带，并轻微膨胀 1 像素囊括抗锯齿过渡区
            using var combined = new Mat();
            Cv2.BitwiseOr(textCore, edgeBand, combined);

            using var textRegion = new Mat();
            Cv2.Dilate(combined, textRegion, morphKernel, iterations: 1);

            // 4. 转换为浮点型并轻度高斯羽化，形成 0.0 ~ 1.0 的柔和边缘权重
            using var regionFloat = new Mat();
            textRegion.ConvertTo(regionFloat, MatType.CV_32FC1, 1.0 / 255.0);

            var softMask = new Mat();
            Cv2.GaussianBlur(regionFloat, softMask, new Size(5, 5), sigmaX: 1.2);
            return softMask;
        }

        /// <summary>
        /// 单通道软掩膜线性插值融合：
        /// output = background * (1 - mask) + foreground * mask
        /// </summary>
        private static void BlendSingleChannelWithMask(Mat background, Mat foreground, Mat maskFloat, Mat output)
        {
            using var bgFloat = new Mat();
            background.ConvertTo(bgFloat, MatType.CV_32FC1);

            using var fgFloat = new Mat();
            foreground.ConvertTo(fgFloat, MatType.CV_32FC1);

            using var invMask = new Mat();
            Cv2.Subtract(new Scalar(1.0), maskFloat, invMask);

            using var partBg = new Mat();
            Cv2.Multiply(bgFloat, invMask, partBg);

            using var partFg = new Mat();
            Cv2.Multiply(fgFloat, maskFloat, partFg);

            using var sum = new Mat();
            Cv2.Add(partBg, partFg, sum);

            sum.ConvertTo(output, MatType.CV_8UC1);
        }

        /// <summary>
        /// 文字暗部下压加深（仅作用于 L 亮度通道）：
        /// 针对被掩膜标记为文字笔画的区域，应用 Gamma 曲线把深灰/中灰文字压向浓黑，提升可读性。
        /// 保持色度不变，杜绝偏色与边缘脏色。
        /// </summary>
        private static Mat DarkenTextCore(Mat lChan, Mat maskFloat)
        {
            byte[] lut = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                double normalized = i / 255.0;
                double darkened = Math.Pow(normalized, 1.32);
                lut[i] = (byte)Math.Min(255, Math.Max(0, (int)Math.Round(darkened * 255.0)));
            }

            using var darkenedFull = new Mat();
            Cv2.LUT(lChan, lut, darkenedFull);

            var result = new Mat();
            BlendSingleChannelWithMask(lChan, darkenedFull, maskFloat, result);
            return result;
        }

        /// <summary>
        /// 连续平滑的色度（饱和度）增益（在 CIE-Lab 空间）：
        /// 以中性点 (128, 128) 为基准，根据色彩偏离程度 rho 施加连续平滑的放大曲线。
        /// 消除硬阈值导致的阶跃断层伪影，红笔、蓝笔红润深邃，边缘过渡平滑纯净。
        /// </summary>
        private static void BoostChromaSmoothly(Mat aChan, Mat bChan, Mat outA, Mat outB)
        {
            using var aFloat = new Mat();
            using var bFloat = new Mat();
            aChan.ConvertTo(aFloat, MatType.CV_32FC1);
            bChan.ConvertTo(bFloat, MatType.CV_32FC1);

            // da = a - 128, db = b - 128
            using var da = new Mat();
            using var db = new Mat();
            Cv2.Subtract(aFloat, new Scalar(128.0), da);
            Cv2.Subtract(bFloat, new Scalar(128.0), db);

            // rho_sq = da^2 + db^2
            using var daSq = new Mat();
            using var dbSq = new Mat();
            Cv2.Multiply(da, da, daSq);
            Cv2.Multiply(db, db, dbSq);

            using var rhoSq = new Mat();
            Cv2.Add(daSq, dbSq, rhoSq);

            using var rho = new Mat();
            Cv2.Sqrt(rhoSq, rho);

            // 连续平滑增益曲线：
            // 当 rho <= 3.0（中性黑灰字或白纸背景），增益为 1.0；
            // 当 rho 在 3.0 ~ 15.0 时，平滑过渡到 1.35 倍增益；
            // gain = 1.0 + 0.35 * clamp((rho - 3.0) / 12.0, 0, 1)
            using var rhoMinus = new Mat();
            Cv2.Subtract(rho, new Scalar(3.0), rhoMinus);
            Cv2.Max(rhoMinus, new Scalar(0.0), rhoMinus);

            using var t = new Mat();
            Cv2.Divide(rhoMinus, new Scalar(12.0), t);
            Cv2.Min(t, new Scalar(1.0), t);

            using var extraGain = new Mat();
            Cv2.Multiply(t, new Scalar(0.35), extraGain);

            using var gain = new Mat();
            Cv2.Add(extraGain, new Scalar(1.0), gain);

            using var boostedDa = new Mat();
            using var boostedDb = new Mat();
            Cv2.Multiply(da, gain, boostedDa);
            Cv2.Multiply(db, gain, boostedDb);

            using var finalAFloat = new Mat();
            using var finalBFloat = new Mat();
            Cv2.Add(boostedDa, new Scalar(128.0), finalAFloat);
            Cv2.Add(boostedDb, new Scalar(128.0), finalBFloat);
            Cv2.Min(finalAFloat, new Scalar(255.0), finalAFloat);
            Cv2.Max(finalAFloat, new Scalar(0.0), finalAFloat);
            Cv2.Min(finalBFloat, new Scalar(255.0), finalBFloat);
            Cv2.Max(finalBFloat, new Scalar(0.0), finalBFloat);

            finalAFloat.ConvertTo(outA, MatType.CV_8UC1);
            finalBFloat.ConvertTo(outB, MatType.CV_8UC1);
        }

        /// <summary>
        /// 单通道平滑背景照度场估计：
        /// 取三通道最大值（最亮白底表征），通过大尺度形态学闭运算抹去笔迹，
        /// 再经大核高斯滤波和双三次插值还原为极其平滑的照度场（CV_32FC1，取值 1.0 ~ 255.0）。
        /// 统一用于三通道除法，彻底消除除法造成的通道不平衡与边缘色斑。
        /// </summary>
        private static Mat EstimateIlluminationBackgroundSingleChannel(Mat bgr)
        {
            int ow = bgr.Width;
            int oh = bgr.Height;

            int targetW = 600;
            double scale = (double)ow / targetW;
            if (scale < 1.0) scale = 1.0;
            int scaledW = (int)Math.Round(ow / scale);
            int scaledH = (int)Math.Round(oh / scale);

            using var small = new Mat();
            Cv2.Resize(bgr, small, new Size(scaledW, scaledH), 0, 0, InterpolationFlags.Area);

            // 取 B、G、R 三通道最大值作为未受彩色笔迹压暗的基底亮度
            Mat[] channels = Cv2.Split(small);
            using var maxBG = new Mat();
            Cv2.Max(channels[0], channels[1], maxBG);
            using var baseLum = new Mat();
            Cv2.Max(maxBG, channels[2], baseLum);
            foreach (var ch in channels) ch.Dispose();

            // 大核形态学闭运算（Close）：抹去黑色字迹与细小彩色笔迹，保留平滑纸张底色与大范围渐变光斑
            int ksize = Math.Max(17, (int)Math.Round(scaledW / 24.0));
            if (ksize % 2 == 0) ksize += 1;
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(ksize, ksize));

            using var closed = new Mat();
            Cv2.MorphologyEx(baseLum, closed, MorphTypes.Close, kernel);

            // 大核高斯模糊抚平闭运算可能残留的微小过渡阶跃
            using var blurred = new Mat();
            Cv2.GaussianBlur(closed, blurred, new Size(ksize, ksize), 0);

            // 双三次插值放回原图尺寸，并转为浮点型单通道
            using var fullBg8U = new Mat();
            Cv2.Resize(blurred, fullBg8U, new Size(ow, oh), 0, 0, InterpolationFlags.Cubic);

            var fullBgFloat = new Mat();
            fullBg8U.ConvertTo(fullBgFloat, MatType.CV_32FC1);
            Cv2.Max(fullBgFloat, new Scalar(1.0), fullBgFloat); // 防止除以 0
            return fullBgFloat;
        }

        /// <summary>
        /// 高光平滑拉伸：将白底区域（>= cutoff）平滑映射到 255 纯白。
        /// </summary>
        private static Mat WhiteLevelStretch(Mat src, byte whiteCutoff = 215)
        {
            byte[] lut = new byte[256];
            double factor = 255.0 / whiteCutoff;
            for (int i = 0; i < 256; i++)
            {
                if (i >= whiteCutoff) lut[i] = 255;
                else
                {
                    int val = (int)Math.Round(i * factor);
                    lut[i] = (byte)Math.Min(255, Math.Max(0, val));
                }
            }

            var result = new Mat();
            Cv2.LUT(src, lut, result);
            return result;
        }
    }
}
