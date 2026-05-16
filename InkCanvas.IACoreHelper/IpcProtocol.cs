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
}