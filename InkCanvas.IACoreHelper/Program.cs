using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Windows;
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
            string sharedMemoryNamePrefix = string.Format(IpcConstants.SharedMemoryName, parentPid, string.Empty);

            try
            {
                // 提前解析父进程句柄并启动守护线程：父进程消失时立即清理并退出，
                // 避免 helper 永远阻塞在 WaitForConnection 上变成孤儿。
                Process parent;
                try
                {
                    parent = Process.GetProcessById(parentPid);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("IACoreHelper fatal: parent process not found: " + ex.Message);
                    return;
                }

                using (var parentExited = new ManualResetEventSlim(false))
                {
                    var parentWatcher = new Thread(() =>
                    {
                        try
                        {
                            parent.WaitForExit();
                        }
                        catch { }
                        finally
                        {
                            parentExited.Set();
                        }
                    })
                    { IsBackground = true, Name = "IACoreHelper.ParentWatcher" };
                    parentWatcher.Start();

                    // 把 WaitForConnection 与守护等待放到一起：任一信号先到就退出。
                    var serverThread = new Thread(() =>
                    {
                        try
                        {
                            RunPipeServer(pipeName, sharedMemoryNamePrefix, () => parentExited.IsSet);
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine("IACoreHelper fatal: " + ex.Message);
                        }
                    })
                    { IsBackground = true, Name = "IACoreHelper.PipeServer" };
                    serverThread.Start();

                    // 主线程阻塞直到父进程消失。守护线程一旦 Set，主线程退出，进程随之终止。
                    parentExited.Wait();
                }
                try { parent.Dispose(); } catch { }
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("IACoreHelper fatal: " + ex.Message);
            }
        }

        private static void RunPipeServer(string pipeName, string sharedMemoryNamePrefix, Func<bool> shouldExit)
        {
            MemoryMappedFile sharedMemory = null;
            string openedSharedMemoryName = null;
            NamedPipeServerStream currentServer = null;
            var shouldRun = true;
            try
            {
                // 用一个后台线程在父进程消失时把当前 server 强制 Dispose，
                // WaitForConnection 因此抛 IOException 而非永远阻塞 — 避免 helper 变成孤儿。
                var serverCanceller = new Thread(() =>
                {
                    while (Volatile.Read(ref shouldRun))
                    {
                        Thread.Sleep(500);
                        if (shouldExit == null) continue;
                        if (!shouldExit())
                            continue;
                        var server = currentServer;
                        try { server?.Dispose(); } catch { }
                        return;
                    }
                })
                { IsBackground = true, Name = "IACoreHelper.CancelOnParentExit" };
                serverCanceller.Start();

                while (!shouldExit())
                {
                    var server = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.WriteThrough);
                    currentServer = server;
                    try
                    {
                        server.WaitForConnection();
                    }
                    catch
                    {
                        // 父进程消失触发 server.Dispose()，抛出 ObjectDisposedException/IOException，跳出循环。
                        server.Dispose();
                        currentServer = null;
                        return;
                    }

                    try
                    {
                        using (var reader = new BinaryReader(server, System.Text.Encoding.UTF8, leaveOpen: true))
                        using (var writer = new BinaryWriter(server, System.Text.Encoding.UTF8, leaveOpen: true))
                        {
                            byte cmd = reader.ReadByte();

                            if (cmd == IpcConstants.CmdShutdown)
                                return;

                            if (cmd == IpcConstants.CmdPingSharedMemoryGeneration)
                            {
                                // 客户端通过 GrowSharedMemory 切到新 generation 后立即下达：
                                // 立即按当前 generation 打开新共享内存句柄，关闭"客户端已 Dispose、
                                // helper 仍持旧句柄"中间窗口的 race。
                                int pingGen = reader.ReadInt32();
                                string targetName = sharedMemoryNamePrefix + pingGen;
                                try
                                {
                                    if (sharedMemory == null || targetName != openedSharedMemoryName)
                                    {
                                        sharedMemory?.Dispose();
                                        sharedMemory = MemoryMappedFile.OpenExisting(targetName);
                                        openedSharedMemoryName = targetName;
                                    }
                                    writer.Write(IpcConstants.StatusOk);
                                }
                                catch (FileNotFoundException)
                                {
                                    writer.Write(IpcConstants.StatusError);
                                }
                                writer.Flush();
                                continue;
                            }

                            if (cmd == IpcConstants.CmdRecognize)
                            {
                                var request = RecognizeRequest.ReadFrom(reader);
                                var response = HandleRecognize(request);
                                response.WriteTo(writer);
                                writer.Flush();
                            }
                            else if (cmd == IpcConstants.CmdRecognizeSharedMemory)
                            {
                                int requestLength = reader.ReadInt32();
                                int capacity = reader.ReadInt32();
                                int generation = reader.ReadInt32();
                                string currentSharedMemoryName = sharedMemoryNamePrefix + generation;

                                if (sharedMemory == null || currentSharedMemoryName != openedSharedMemoryName)
                                {
                                    sharedMemory?.Dispose();
                                    sharedMemory = MemoryMappedFile.OpenExisting(currentSharedMemoryName);
                                    openedSharedMemoryName = currentSharedMemoryName;
                                }

                                int status = HandleSharedMemoryRecognize(sharedMemory, requestLength, capacity, out int responseLength);
                                writer.Write(status);
                                writer.Write(responseLength);
                                writer.Flush();
                            }
                            else if (cmd == IpcConstants.CmdRecognizeTextSharedMemory)
                            {
                                int requestLength = reader.ReadInt32();
                                int capacity = reader.ReadInt32();
                                int generation = reader.ReadInt32();
                                string currentSharedMemoryName = sharedMemoryNamePrefix + generation;

                                if (sharedMemory == null || currentSharedMemoryName != openedSharedMemoryName)
                                {
                                    sharedMemory?.Dispose();
                                    sharedMemory = MemoryMappedFile.OpenExisting(currentSharedMemoryName);
                                    openedSharedMemoryName = currentSharedMemoryName;
                                }

                                int status = HandleSharedMemoryRecognizeText(sharedMemory, requestLength, capacity, out int responseLength);
                                writer.Write(status);
                                writer.Write(responseLength);
                                writer.Flush();
                            }
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        sharedMemory?.Dispose();
                        sharedMemory = null;
                        Console.Error.WriteLine("IACoreHelper shared memory missing");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("IACoreHelper pipe error: " + ex.Message);
                    }
                    finally
                    {
                        try { server.Dispose(); } catch { }
                        currentServer = null;
                    }
                }
            }
            finally
            {
                Volatile.Write(ref shouldRun, false);
                sharedMemory?.Dispose();
            }
        }

        private static int HandleSharedMemoryRecognize(
            MemoryMappedFile sharedMemory,
            int requestLength,
            int capacity,
            out int responseLength)
        {
            responseLength = 0;
            try
            {
                if (requestLength <= 0 || requestLength > capacity - IpcConstants.SharedMemoryHeaderSize)
                    return IpcConstants.StatusError;

                RecognizeRequest request;
                using (var stream = sharedMemory.CreateViewStream(
                    IpcConstants.SharedMemoryHeaderSize,
                    requestLength,
                    MemoryMappedFileAccess.Read))
                using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8))
                {
                    request = RecognizeRequest.ReadFrom(reader);
                }

                var response = HandleRecognize(request);
                int responseOffset = IpcConstants.SharedMemoryHeaderSize + requestLength;
                int maxResponseLength = capacity - responseOffset;
                if (maxResponseLength <= 0)
                    return IpcConstants.StatusResponseTooLarge;

                using (var stream = sharedMemory.CreateViewStream(
                    responseOffset,
                    maxResponseLength,
                    MemoryMappedFileAccess.Write))
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8))
                {
                    response.WriteTo(writer);
                    writer.Flush();
                    responseLength = checked((int)stream.Position);
                }

                using (var accessor = sharedMemory.CreateViewAccessor(0, IpcConstants.SharedMemoryHeaderSize))
                {
                    accessor.Write(SharedMemoryHeader.Magic, IpcConstants.SharedMemoryMagic);
                    accessor.Write(SharedMemoryHeader.Version, IpcConstants.ProtocolVersion);
                    accessor.Write(SharedMemoryHeader.RequestLength, requestLength);
                    accessor.Write(SharedMemoryHeader.ResponseOffset, responseOffset);
                    accessor.Write(SharedMemoryHeader.ResponseLength, responseLength);
                    accessor.Write(SharedMemoryHeader.Status, IpcConstants.StatusOk);
                }

                return IpcConstants.StatusOk;
            }
            catch (NotSupportedException)
            {
                return IpcConstants.StatusResponseTooLarge;
            }
            catch (IOException)
            {
                return IpcConstants.StatusResponseTooLarge;
            }
            catch
            {
                return IpcConstants.StatusError;
            }
        }

        private static RecognizeResponse HandleRecognize(RecognizeRequest request)
        {
            try
            {
                var strokes = BuildStrokeCollection(request?.Strokes);
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

        private static int HandleSharedMemoryRecognizeText(
            MemoryMappedFile sharedMemory,
            int requestLength,
            int capacity,
            out int responseLength)
        {
            responseLength = 0;
            try
            {
                if (requestLength <= 0 || requestLength > capacity - IpcConstants.SharedMemoryHeaderSize)
                    return IpcConstants.StatusError;

                RecognizeTextRequest request;
                using (var stream = sharedMemory.CreateViewStream(
                    IpcConstants.SharedMemoryHeaderSize,
                    requestLength,
                    MemoryMappedFileAccess.Read))
                using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8))
                {
                    request = RecognizeTextRequest.ReadFrom(reader);
                }

                var response = HandleRecognizeText(request);
                int responseOffset = IpcConstants.SharedMemoryHeaderSize + requestLength;
                int maxResponseLength = capacity - responseOffset;
                if (maxResponseLength <= 0)
                    return IpcConstants.StatusResponseTooLarge;

                using (var stream = sharedMemory.CreateViewStream(
                    responseOffset,
                    maxResponseLength,
                    MemoryMappedFileAccess.Write))
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8))
                {
                    response.WriteTo(writer);
                    writer.Flush();
                    responseLength = checked((int)stream.Position);
                }

                using (var accessor = sharedMemory.CreateViewAccessor(0, IpcConstants.SharedMemoryHeaderSize))
                {
                    accessor.Write(SharedMemoryHeader.Magic, IpcConstants.SharedMemoryMagic);
                    accessor.Write(SharedMemoryHeader.Version, IpcConstants.ProtocolVersion);
                    accessor.Write(SharedMemoryHeader.RequestLength, requestLength);
                    accessor.Write(SharedMemoryHeader.ResponseOffset, responseOffset);
                    accessor.Write(SharedMemoryHeader.ResponseLength, responseLength);
                    accessor.Write(SharedMemoryHeader.Status, IpcConstants.StatusOk);
                }

                return IpcConstants.StatusOk;
            }
            catch (NotSupportedException)
            {
                return IpcConstants.StatusResponseTooLarge;
            }
            catch (IOException)
            {
                return IpcConstants.StatusResponseTooLarge;
            }
            catch
            {
                return IpcConstants.StatusError;
            }
        }

        private static RecognizeTextResponse HandleRecognizeText(RecognizeTextRequest request)
        {
            try
            {
                var strokes = BuildStrokeCollection(request.Strokes);
                if (strokes.Count == 0)
                    return new RecognizeTextResponse { Success = false, CombinedText = string.Empty, Words = Array.Empty<RecognizeTextWordDto>() };

                return RecognizeTextCore(strokes, request);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("IACoreHelper recognize-text error: " + ex.Message);
                return new RecognizeTextResponse { Success = false, CombinedText = string.Empty, Words = Array.Empty<RecognizeTextWordDto>() };
            }
        }

        /// <summary>
        /// 基于 IAWinFX <see cref="InkAnalyzer"/> + <see cref="AnalysisHintNode"/> 的文字识别。
        /// 通过 hint 暴露 Factoid/WordList/WordMode/CoerceToFactoid 等上下文层（UWP WinRT InkAnalyzer 无法访问），
        /// 这是系统手写输入面板使用的同一套上下文机制。笔画按 <see cref="StrokeType.Writing"/> 识别为 InkWord 节点。
        /// </summary>
        private static RecognizeTextResponse RecognizeTextCore(StrokeCollection strokes, RecognizeTextRequest request)
        {
            var analyzer = new InkAnalyzer();
            try
            {
                analyzer.AddStrokes(strokes);
                analyzer.SetStrokesType(strokes, StrokeType.Writing);

                // 创建分析提示节点作为上下文载体。Hint 位置为 0/0/0/0 时表示无限区域（属性作用于全部笔画）。
                Rect hintRect = (request.HintWidth > 0 && request.HintHeight > 0)
                    ? new Rect(request.HintLeft, request.HintTop, request.HintWidth, request.HintHeight)
                    : Rect.Empty;
                AnalysisHintNode hint = hintRect != Rect.Empty
                    ? analyzer.CreateAnalysisHint(hintRect)
                    : analyzer.CreateAnalysisHint();

                if (!string.IsNullOrEmpty(request.Factoid))
                    hint.Factoid = request.Factoid;

                if (request.WordList != null && request.WordList.Length > 0)
                {
                    // IAWinFX 托管包装：SetWordlist 直接接收 string[]，无需构造 WordList 对象。
                    hint.SetWordlist(request.WordList);
                }

                // WordMode / CoerceToFactoid / TopInkBreaksOnly 是 AnalysisHintNode 的强类型属性。
                hint.WordMode = request.WordMode;
                hint.CoerceToFactoid = request.CoerceToFactoid;

                var status = analyzer.Analyze();
                if (!status.Successful)
                    return new RecognizeTextResponse { Success = false, CombinedText = string.Empty, Words = Array.Empty<RecognizeTextWordDto>() };

                // 取所有 InkWord 叶子节点，按位置从左到右排序（兼容多行：先 Y 后 X）。
                var wordNodes = analyzer.FindNodesOfType(ContextNodeType.InkWord);
                if (wordNodes == null || wordNodes.Count == 0)
                    return new RecognizeTextResponse { Success = false, CombinedText = string.Empty, Words = Array.Empty<RecognizeTextWordDto>() };

                var ordered = wordNodes
                    .OfType<InkWordNode>()
                    .OrderBy(n => n.Location.GetBounds().Top)
                    .ThenBy(n => n.Location.GetBounds().Left)
                    .ToList();

                var wordDtos = new List<RecognizeTextWordDto>();
                var combined = new System.Text.StringBuilder();

                foreach (var node in ordered)
                {
                    var bounds = node.Location.GetBounds();
                    var primary = node.GetRecognizedString() ?? string.Empty;
                    combined.Append(primary);

                    // 取该词的候选串：对该节点笔画调用 GetAlternates(StrokeCollection, Int32)。
                    // （托管包装未暴露按名称的 GetAlternatesForStrokes，但 GetAlternates 重载可按笔画取候选。）
                    var candidates = new List<string> { primary };
                    try
                    {
                        var nodeStrokes = node.Strokes;
                        if (nodeStrokes != null && nodeStrokes.Count > 0)
                        {
                            var alts = analyzer.GetAlternates(nodeStrokes, 8);
                            if (alts != null)
                            {
                                foreach (AnalysisAlternate alt in alts)
                                {
                                    if (alt == null) continue;
                                    var s = alt.RecognizedString;
                                    if (!string.IsNullOrEmpty(s) && !candidates.Contains(s))
                                        candidates.Add(s);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 候选获取失败不影响主结果。
                    }

                    var strokeIndices = new List<int>();
                    var participating = node.Strokes;
                    if (participating != null)
                    {
                        foreach (var s in participating)
                        {
                            int idx = strokes.IndexOf(s);
                            if (idx >= 0)
                                strokeIndices.Add(idx);
                        }
                    }

                    wordDtos.Add(new RecognizeTextWordDto
                    {
                        Text = primary,
                        Candidates = candidates.ToArray(),
                        Left = (float)bounds.Left,
                        Top = (float)bounds.Top,
                        Width = (float)bounds.Width,
                        Height = (float)bounds.Height,
                        StrokeIndices = strokeIndices.ToArray()
                    });
                }

                return new RecognizeTextResponse
                {
                    Success = wordDtos.Count > 0,
                    CombinedText = combined.ToString(),
                    Words = wordDtos.ToArray()
                };
            }
            finally
            {
                analyzer.Dispose();
            }
        }

        private static StrokeCollection BuildStrokeCollection(StrokeDto[] strokesDto)
        {
            var sc = new StrokeCollection();
            if (strokesDto == null)
                return sc;
            foreach (var strokeDto in strokesDto)
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