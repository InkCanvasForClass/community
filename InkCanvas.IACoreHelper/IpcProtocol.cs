using System.IO;

namespace InkCanvas.IACoreHelper
{
    public static class IpcConstants
    {
        public const string PipeName = "ICC_IACoreHelper_{0}";
        public const string SharedMemoryName = "ICC_IACoreHelper_Shared_{0}_{1}";
        public const int RequestTimeout = 5000;
        public const int ProtocolVersion = 2;
        public const int SharedMemoryHeaderSize = 24;
        public const int DefaultSharedMemoryCapacity = 4 * 1024 * 1024;
        public const int MaxSharedMemoryCapacity = 32 * 1024 * 1024;
        public const int SharedMemoryMagic = 0x49414348;
        public const byte CmdRecognize = 0x01;
        public const byte CmdRecognizeSharedMemory = 0x02;
        public const byte CmdRecognizeTextSharedMemory = 0x03;
        // 由客户端在 GrowSharedMemory 后立即发送：helper 立即换成新 generation 共享内存名打开，
        // 关闭 GrowSharedMemory → 下次共享内存请求之间 helper 仍持有旧句柄的 race window，
        // 否则该次 OpenExisting 抛 FileNotFoundException 被吞，返回 StatusError。
        public const byte CmdPingSharedMemoryGeneration = 0x04;
        public const byte CmdShutdown = 0xFF;
        public const int StatusOk = 0;
        public const int StatusError = 1;
        public const int StatusResponseTooLarge = 2;
    }

    public static class SharedMemoryHeader
    {
        public const int Magic = 0;
        public const int Version = 4;
        public const int RequestLength = 8;
        public const int ResponseOffset = 12;
        public const int ResponseLength = 16;
        public const int Status = 20;
    }

    internal struct StylusPointDto
    {
        public float X;
        public float Y;
        public float Pressure;
    }

    internal class StrokeDto
    {
        public StylusPointDto[] Points;
    }

    internal class RecognizeRequest
    {
        public StrokeDto[] Strokes;

        public void WriteTo(BinaryWriter w)
        {
            w.Write(IpcConstants.CmdRecognize);
            WritePayloadTo(w);
        }

        public void WritePayloadTo(BinaryWriter w)
        {
            w.Write(Strokes.Length);
            foreach (var stroke in Strokes)
            {
                w.Write(stroke.Points.Length);
                foreach (var pt in stroke.Points)
                {
                    w.Write(pt.X);
                    w.Write(pt.Y);
                    w.Write(pt.Pressure);
                }
            }
        }

        public static RecognizeRequest ReadFrom(BinaryReader r)
        {
            int strokeCount = r.ReadInt32();
            var strokes = new StrokeDto[strokeCount];
            for (int i = 0; i < strokeCount; i++)
            {
                int ptCount = r.ReadInt32();
                var pts = new StylusPointDto[ptCount];
                for (int j = 0; j < ptCount; j++)
                    pts[j] = new StylusPointDto { X = r.ReadSingle(), Y = r.ReadSingle(), Pressure = r.ReadSingle() };
                strokes[i] = new StrokeDto { Points = pts };
            }
            return new RecognizeRequest { Strokes = strokes };
        }
    }

    internal class RecognizeResponse
    {
        public bool Success;
        public string ShapeName;
        public float CentroidX;
        public float CentroidY;
        public float ShapeWidth;
        public float ShapeHeight;
        public float[] HotPointsX;
        public float[] HotPointsY;
        public int[] StrokeIndices;

        public void WriteTo(BinaryWriter w)
        {
            w.Write(Success);
            w.Write(ShapeName ?? string.Empty);
            w.Write(CentroidX);
            w.Write(CentroidY);
            w.Write(ShapeWidth);
            w.Write(ShapeHeight);

            int hotLen = HotPointsX != null ? HotPointsX.Length : 0;
            w.Write(hotLen);
            for (int i = 0; i < hotLen; i++)
            {
                w.Write(HotPointsX[i]);
                w.Write(HotPointsY[i]);
            }

            int idxLen = StrokeIndices != null ? StrokeIndices.Length : 0;
            w.Write(idxLen);
            for (int i = 0; i < idxLen; i++)
                w.Write(StrokeIndices[i]);
        }

        public static RecognizeResponse ReadFrom(BinaryReader r)
        {
            var resp = new RecognizeResponse
            {
                Success = r.ReadBoolean(),
                ShapeName = r.ReadString(),
                CentroidX = r.ReadSingle(),
                CentroidY = r.ReadSingle(),
                ShapeWidth = r.ReadSingle(),
                ShapeHeight = r.ReadSingle()
            };

            int hotLen = r.ReadInt32();
            resp.HotPointsX = new float[hotLen];
            resp.HotPointsY = new float[hotLen];
            for (int i = 0; i < hotLen; i++)
            {
                resp.HotPointsX[i] = r.ReadSingle();
                resp.HotPointsY[i] = r.ReadSingle();
            }

            int idxLen = r.ReadInt32();
            resp.StrokeIndices = new int[idxLen];
            for (int i = 0; i < idxLen; i++)
                resp.StrokeIndices[i] = r.ReadInt32();

            return resp;
        }
    }

    /// <summary>
    /// 文字识别请求：笔画集合 + 上下文提示（Factoid/WordList/WordMode/Coerce），载荷与 <see cref="RecognizeRequest"/> 共享笔画编码，
    /// 前缀一段提示头。仅在共享内存命令 <see cref="IpcConstants.CmdRecognizeTextSharedMemory"/> 下使用。
    /// </summary>
    internal class RecognizeTextRequest
    {
        public StrokeDto[] Strokes;

        /// <summary>提示区矩形（ink 坐标系）。全 0 表示无限区域（hint 不限定位置，仅作为属性载体）。</summary>
        public float HintLeft;
        public float HintTop;
        public float HintWidth;
        public float HintHeight;

        /// <summary>Factoid 字符串（如 "(!IS_DEFAULT)"）；空串表示不设置。</summary>
        public string Factoid;

        /// <summary>词表（每项一个候选词）；null/空数组表示不设置。</summary>
        public string[] WordList;

        /// <summary>是否启用 WordMode（优先单字/单词结果）。用于 CJK 单字模式。</summary>
        public bool WordMode;

        /// <summary>是否强制按 Factoid 约束（CoerceToFactoid）。</summary>
        public bool CoerceToFactoid;

        public void WritePayloadTo(BinaryWriter w)
        {
            w.Write(HintLeft);
            w.Write(HintTop);
            w.Write(HintWidth);
            w.Write(HintHeight);
            w.Write(Factoid ?? string.Empty);

            int wlLen = WordList != null ? WordList.Length : 0;
            w.Write(wlLen);
            for (int i = 0; i < wlLen; i++)
                w.Write(WordList[i] ?? string.Empty);

            w.Write(WordMode);
            w.Write(CoerceToFactoid);

            w.Write(Strokes.Length);
            foreach (var stroke in Strokes)
            {
                w.Write(stroke.Points.Length);
                foreach (var pt in stroke.Points)
                {
                    w.Write(pt.X);
                    w.Write(pt.Y);
                    w.Write(pt.Pressure);
                }
            }
        }

        public static RecognizeTextRequest ReadFrom(BinaryReader r)
        {
            var req = new RecognizeTextRequest
            {
                HintLeft = r.ReadSingle(),
                HintTop = r.ReadSingle(),
                HintWidth = r.ReadSingle(),
                HintHeight = r.ReadSingle(),
                Factoid = r.ReadString()
            };

            int wlLen = r.ReadInt32();
            var wl = new string[wlLen];
            for (int i = 0; i < wlLen; i++)
                wl[i] = r.ReadString();
            req.WordList = wl;

            req.WordMode = r.ReadBoolean();
            req.CoerceToFactoid = r.ReadBoolean();

            int strokeCount = r.ReadInt32();
            var strokes = new StrokeDto[strokeCount];
            for (int i = 0; i < strokeCount; i++)
            {
                int ptCount = r.ReadInt32();
                var pts = new StylusPointDto[ptCount];
                for (int j = 0; j < ptCount; j++)
                    pts[j] = new StylusPointDto { X = r.ReadSingle(), Y = r.ReadSingle(), Pressure = r.ReadSingle() };
                strokes[i] = new StrokeDto { Points = pts };
            }
            req.Strokes = strokes;
            return req;
        }
    }

    internal class RecognizeTextWordDto
    {
        public string Text;
        public string[] Candidates;
        public float Left;
        public float Top;
        public float Width;
        public float Height;
        public int[] StrokeIndices;
    }

    internal class RecognizeTextResponse
    {
        public bool Success;
        public string CombinedText;
        public RecognizeTextWordDto[] Words;

        public void WriteTo(BinaryWriter w)
        {
            w.Write(Success);
            w.Write(CombinedText ?? string.Empty);

            int wLen = Words != null ? Words.Length : 0;
            w.Write(wLen);
            for (int i = 0; i < wLen; i++)
            {
                var word = Words[i];
                w.Write(word.Text ?? string.Empty);

                int cLen = word.Candidates != null ? word.Candidates.Length : 0;
                w.Write(cLen);
                for (int j = 0; j < cLen; j++)
                    w.Write(word.Candidates[j] ?? string.Empty);

                w.Write(word.Left);
                w.Write(word.Top);
                w.Write(word.Width);
                w.Write(word.Height);

                int sLen = word.StrokeIndices != null ? word.StrokeIndices.Length : 0;
                w.Write(sLen);
                for (int j = 0; j < sLen; j++)
                    w.Write(word.StrokeIndices[j]);
            }
        }

        public static RecognizeTextResponse ReadFrom(BinaryReader r)
        {
            var resp = new RecognizeTextResponse
            {
                Success = r.ReadBoolean(),
                CombinedText = r.ReadString()
            };

            int wLen = r.ReadInt32();
            var words = new RecognizeTextWordDto[wLen];
            for (int i = 0; i < wLen; i++)
            {
                var word = new RecognizeTextWordDto
                {
                    Text = r.ReadString()
                };
                int cLen = r.ReadInt32();
                var cands = new string[cLen];
                for (int j = 0; j < cLen; j++)
                    cands[j] = r.ReadString();
                word.Candidates = cands;

                word.Left = r.ReadSingle();
                word.Top = r.ReadSingle();
                word.Width = r.ReadSingle();
                word.Height = r.ReadSingle();

                int sLen = r.ReadInt32();
                var idxs = new int[sLen];
                for (int j = 0; j < sLen; j++)
                    idxs[j] = r.ReadInt32();
                word.StrokeIndices = idxs;

                words[i] = word;
            }
            resp.Words = words;
            return resp;
        }
    }
}