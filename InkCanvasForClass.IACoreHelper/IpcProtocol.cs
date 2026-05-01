using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace InkCanvasForClass.IACoreHelper
{
    // Named Pipe 名称，主进程和辅助进程共用
    internal static class IpcConstants
    {
        public const string PipeName = "ICC_IACoreHelper_{0}";  // {0} = 主进程 PID
        public const int RequestTimeout = 5000;                  // ms
        public const byte CmdRecognize = 0x01;
        public const byte CmdShutdown  = 0xFF;
    }

    // 单个 StylusPoint 的轻量传输结构
    internal struct StylusPointDto
    {
        public float X;
        public float Y;
        public float Pressure;
    }

    // 单条笔画
    internal class StrokeDto
    {
        public StylusPointDto[] Points;
    }

    // 识别请求（主进程 → 辅助进程）
    internal class RecognizeRequest
    {
        public StrokeDto[] Strokes;

        public void WriteTo(BinaryWriter w)
        {
            w.Write(IpcConstants.CmdRecognize);
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

    // 识别响应（辅助进程 → 主进程）
    internal class RecognizeResponse
    {
        public bool Success;
        public string ShapeName;        // e.g. "Circle", "Rectangle", "Triangle" ...
        public float CentroidX;
        public float CentroidY;
        public float ShapeWidth;
        public float ShapeHeight;
        public float[] HotPointsX;
        public float[] HotPointsY;
        public int[]   StrokeIndices;   // 参与识别的笔画在原始数组中的下标

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
                Success    = r.ReadBoolean(),
                ShapeName  = r.ReadString(),
                CentroidX  = r.ReadSingle(),
                CentroidY  = r.ReadSingle(),
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
}