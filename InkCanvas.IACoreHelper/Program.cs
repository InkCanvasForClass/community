using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Windows.Ink;
using System.Windows.Input;

namespace InkCanvas.IACoreHelper
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int parentPid))
            {
                Console.Error.WriteLine("Usage: IACoreHelper.exe <parentPid>");
                return;
            }

            string pipeName = string.Format(IpcConstants.PipeName, parentPid);

            try
            {
                RunPipeServer(pipeName);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("IACoreHelper fatal: " + ex.Message);
            }
        }

        private static void RunPipeServer(string pipeName)
        {
            while (true)
            {
                using (var server = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.WriteThrough))
                {
                    server.WaitForConnection();

                    try
                    {
                        using (var reader = new BinaryReader(server, System.Text.Encoding.UTF8, leaveOpen: true))
                        using (var writer = new BinaryWriter(server, System.Text.Encoding.UTF8, leaveOpen: true))
                        {
                            byte cmd = reader.ReadByte();

                            if (cmd == IpcConstants.CmdShutdown)
                                return;

                            if (cmd == IpcConstants.CmdRecognize)
                            {
                                var request = RecognizeRequest.ReadFrom(reader);
                                var response = HandleRecognize(request);
                                response.WriteTo(writer);
                                writer.Flush();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("IACoreHelper pipe error: " + ex.Message);
                    }
                }
            }
        }

        private static RecognizeResponse HandleRecognize(RecognizeRequest request)
        {
            try
            {
                var strokes = BuildStrokeCollection(request);
                if (strokes.Count == 0)
                    return new RecognizeResponse { Success = false, ShapeName = string.Empty };

                return RecognizeCore(strokes);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("IACoreHelper recognize error: " + ex.Message);
                return new RecognizeResponse { Success = false, ShapeName = string.Empty };
            }
        }

        private static StrokeCollection BuildStrokeCollection(RecognizeRequest request)
        {
            var sc = new StrokeCollection();
            foreach (var strokeDto in request.Strokes)
            {
                if (strokeDto.Points == null || strokeDto.Points.Length == 0)
                    continue;

                var stylusPoints = new StylusPointCollection();
                foreach (var pt in strokeDto.Points)
                    stylusPoints.Add(new StylusPoint(pt.X, pt.Y, pt.Pressure));

                sc.Add(new Stroke(stylusPoints));
            }
            return sc;
        }

        private static RecognizeResponse RecognizeCore(StrokeCollection strokes)
        {
            var analyzer = new InkAnalyzer();
            analyzer.AddStrokes(strokes);
            analyzer.SetStrokesType(strokes, StrokeType.Drawing);

            AnalysisAlternate analysisAlternate = null;
            int strokesCount = strokes.Count;
            var analysisStatus = analyzer.Analyze();

            if (analysisStatus.Successful)
            {
                var alternates = analyzer.GetAlternates();
                if (alternates.Count > 0)
                {
                    while (strokesCount >= 2)
                    {
                        var alt0 = alternates[0];
                        if (alt0?.AlternateNodes == null || alt0.AlternateNodes.Count == 0)
                            break;
                        var drawNode = alt0.AlternateNodes[0] as InkDrawingNode;
                        if (drawNode == null)
                            break;
                        bool shapeOk = IsContainShapeType(drawNode.GetShapeName());
                        if (alt0.Strokes.Contains(strokes.Last()) && shapeOk)
                            break;
                        analyzer.RemoveStroke(strokes[strokes.Count - strokesCount]);
                        strokesCount--;
                        analysisStatus = analyzer.Analyze();
                        if (analysisStatus.Successful)
                            alternates = analyzer.GetAlternates();
                        else
                            break;
                        if (alternates.Count == 0)
                            break;
                    }

                    if (alternates.Count > 0)
                    {
                        var altFinal = alternates[0];
                        if (altFinal?.AlternateNodes != null && altFinal.AlternateNodes.Count > 0)
                            analysisAlternate = altFinal;
                    }
                }
            }

            analyzer.Dispose();

            if (analysisAlternate?.AlternateNodes == null || analysisAlternate.AlternateNodes.Count == 0)
                return new RecognizeResponse { Success = false, ShapeName = string.Empty };

            var node = analysisAlternate.AlternateNodes[0] as InkDrawingNode;
            if (node == null)
                return new RecognizeResponse { Success = false, ShapeName = string.Empty };

            var shape = node.GetShape();
            var center = node.Centroid;
            var hot = node.HotPoints;

            float[] hotX = new float[hot?.Count ?? 0];
            float[] hotY = new float[hot?.Count ?? 0];
            if (hot != null)
                for (int i = 0; i < hot.Count; i++) { hotX[i] = (float)hot[i].X; hotY[i] = (float)hot[i].Y; }

            var participatingStrokes = analysisAlternate.Strokes;
            int[] strokeIndices = new int[participatingStrokes?.Count ?? 0];
            if (participatingStrokes != null)
                for (int i = 0; i < participatingStrokes.Count; i++)
                    strokeIndices[i] = strokes.IndexOf(participatingStrokes[i]);

            return new RecognizeResponse
            {
                Success = true,
                ShapeName = node.GetShapeName() ?? string.Empty,
                CentroidX = (float)center.X,
                CentroidY = (float)center.Y,
                ShapeWidth = shape != null ? (float)shape.Width : 0f,
                ShapeHeight = shape != null ? (float)shape.Height : 0f,
                HotPointsX = hotX,
                HotPointsY = hotY,
                StrokeIndices = strokeIndices
            };
        }

        private static bool IsContainShapeType(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.Contains("Triangle") || name.Contains("Circle") ||
                   name.Contains("Rectangle") || name.Contains("Diamond") ||
                   name.Contains("Parallelogram") || name.Contains("Square") ||
                   name.Contains("Ellipse");
        }
    }
}