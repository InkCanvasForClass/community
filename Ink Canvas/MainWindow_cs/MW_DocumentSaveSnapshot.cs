using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private readonly SemaphoreSlim _documentSaveGate = new(1, 1);

        private sealed class SavedPageSnapshot
        {
            public int Index;
            public byte[] Ink;
            public Dictionary<string, string> Sidecars = new();
        }

        /// <summary>
        /// Snapshot WPF/plugin state before the first await. Disk/compression/encoding
        /// only consume immutable snapshots; changing pages while saving is safe.
        /// </summary>
        private async Task SaveLegacySnapshotAsync(string path, bool newNotice)
        {
            if (!await _documentSaveGate.WaitAsync(0))
            {
                LogHelper.WriteLogToFile("[Save] A document save is already in progress.", LogHelper.LogType.Warning);
                if (newNotice) ShowNotification(MainWindowStrings.Main_Strokes_SaveFailed);
                return;
            }
            try
            {
                bool xml = Settings.Automation.IsSaveStrokesAsXML;
                bool images = !xml && Settings.Automation.IsSaveFullPageStrokes;
                bool whiteBackground = Settings.Canvas.UsingWhiteboard;
                var savedAt = DateTime.Now;
                bool ppt = IsInPPTPresentationMode && _pptManager?.IsConnected == true;
                int pageCount = ppt ? _pptManager.SlidesCount : currentMode != 0 ? WhiteboardTotalCount : 1;
                int currentPage = ppt ? Math.Max(1, _currentSlideShowPosition) : CurrentWhiteboardIndex;
                double width = Math.Max(1, inkCanvas.ActualWidth), height = Math.Max(1, inkCanvas.ActualHeight);
                var elements = new List<CanvasElementInfo>();
                CollectCanvasElementsMetadata(elements);
                string elementsJson = JsonConvert.SerializeObject(elements, Formatting.Indented);
                var pages = new List<SavedPageSnapshot>();
                if (currentMode != 0) CaptureCurrentPluginPageStates();
                for (int index = 1; index <= Math.Max(1, pageCount); index++)
                {
                    StrokeCollection strokes;
                    if (pageCount == 1 || index == currentPage) strokes = inkCanvas.Strokes;
                    else if (ppt) strokes = _singlePPTInkManager?.LoadSlideStrokes(index) ?? new StrokeCollection();
                    else strokes = TimeMachineHistories[index] == null ? new StrokeCollection()
                        : ApplyHistoriesToNewStrokeCollection(TimeMachineHistories[index]);
                    using var buffer = new MemoryStream();
                    strokes.Save(buffer);
                    var page = new SavedPageSnapshot { Index = index, Ink = buffer.ToArray() };
                    if (currentMode != 0)
                    {
                        page.Sidecars[".plugins.json"] = _pluginWhiteboardStateStore.ExportDocumentJson(index, 1);
                        foreach (var sidecar in _pluginWhiteboardStateStore.GetCompanionStates(index, LogPluginPageStateError))
                            page.Sidecars[sidecar.FileExtension] = sidecar.Content;
                    }
                    pages.Add(page);
                }
                string pluginDocument = currentMode != 0
                    ? _pluginWhiteboardStateStore.ExportDocumentJson(1, WhiteboardTotalCount) : null;
                string metadata = $"保存时间: {savedAt:yyyy-MM-dd HH:mm:ss}\n总页数: {pageCount}\n"
                    + $"模式: {(currentMode == 0 ? "PPT放映" : FloatingBarStrings.FloatingBar_Whiteboard)}\n"
                    + $"当前页面: {currentPage}\n总页面数: {pageCount}\n格式: {(xml ? "XML" : "icstk")}\n";
                BitmapSource currentFrame = null;
                if (images && pageCount == 1)
                {
                    var frame = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
                    var visual = new DrawingVisual();
                    using (var drawing = visual.RenderOpen())
                    {
                        drawing.DrawRectangle(whiteBackground ? Brushes.White : new SolidColorBrush(Color.FromRgb(22, 41, 36)),
                            null, new Rect(0, 0, width, height));
                        drawing.DrawRectangle(new VisualBrush(inkCanvas), null, new Rect(0, 0, width, height));
                    }
                    frame.Render(visual);
                    frame.Freeze();
                    currentFrame = frame;
                }
                bool zip = pageCount > 1 && (images || (xml && !ppt));
                string directory = zip ? Path.Combine(Path.GetTempPath(), "ICC_Save_" + Guid.NewGuid().ToString("N"))
                    : Path.GetDirectoryName(path);
                var savedPaths = new List<string>();
                try
                {
                    await Task.Run(() => Directory.CreateDirectory(directory));
                    foreach (var page in pages)
                    {
                        string basename = zip ? $"page_{page.Index:D4}"
                            : Path.GetFileNameWithoutExtension(path) + (pageCount > 1 ? $"_Page-{page.Index}" : "");
                        string target = Path.Combine(directory, basename + (xml ? ".xml" : ".icstk"));
                        BitmapSource image = null;
                        if (images && currentFrame != null) image = currentFrame;
                        else if (images)
                        {
                            using var stream = new MemoryStream(page.Ink, writable: false);
                            var canvas = new System.Windows.Controls.InkCanvas
                            {
                                Width = width, Height = height, Strokes = new StrokeCollection(stream),
                                Background = whiteBackground ? Brushes.White : new SolidColorBrush(Color.FromRgb(22, 41, 36))
                            };
                            canvas.Measure(new Size(width, height));
                            canvas.Arrange(new Rect(0, 0, width, height));
                            var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
                            bitmap.Render(canvas);
                            bitmap.Freeze();
                            image = bitmap;
                        }
                        var frozenImage = image;
                        await Task.Run(() =>
                        {
                            if (xml)
                            {
                                using var stream = new MemoryStream(page.Ink, writable: false);
                                var strokes = new StrokeCollection(stream);
                                AtomicFileWriter.WriteNow(target, LegacyInkXmlCodec.Serialize(strokes, savedAt));
                            }
                            else AtomicFileWriter.WriteBytesNow(target, page.Ink);
                            AtomicFileWriter.WriteNow(Path.ChangeExtension(target, ".elements.json"), elementsJson);
                            foreach (var sidecar in page.Sidecars)
                                AtomicFileWriter.WriteNow(Path.ChangeExtension(target, sidecar.Key), sidecar.Value);
                            if (frozenImage != null)
                            {
                                var encoder = new PngBitmapEncoder();
                                encoder.Frames.Add(BitmapFrame.Create(frozenImage));
                                using var output = File.Create(Path.ChangeExtension(target, ".png"));
                                encoder.Save(output);
                            }
                        });
                        savedPaths.Add(target);
                    }
                    if (zip)
                    {
                        var archivePath = Path.ChangeExtension(path, ".zip");
                        await Task.Run(() =>
                        {
                            File.WriteAllText(Path.Combine(directory, "metadata.txt"), metadata, Encoding.UTF8);
                            if (pluginDocument != null)
                                File.WriteAllText(Path.Combine(directory, PluginDocumentStateFileName), pluginDocument, Encoding.UTF8);
                            var temporaryArchive = archivePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                            try
                            {
                                ZipFile.CreateFromDirectory(directory, temporaryArchive);
                                File.Move(temporaryArchive, archivePath, overwrite: true);
                            }
                            finally { if (File.Exists(temporaryArchive)) File.Delete(temporaryArchive); }
                        });
                        savedPaths.Clear();
                        savedPaths.Add(archivePath);
                    }
                    foreach (var saved in savedPaths) _ = Task.Run(() => UploadSavedDocumentAsync(saved));
                    if (newNotice) ShowNotification(string.Format(MainWindowStrings.Main_Strokes_SaveSuccess,
                        savedPaths.FirstOrDefault() ?? path));
                }
                finally
                {
                    if (zip) await Task.Run(() => { if (Directory.Exists(directory)) Directory.Delete(directory, true); });
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[Save] {ex}", LogHelper.LogType.Error);
                ShowNotification(MainWindowStrings.Main_Strokes_SaveFailed);
            }
            finally { _documentSaveGate.Release(); }
        }

        private static async Task UploadSavedDocumentAsync(string path)
        {
            try { await UploadHelper.UploadFileAsync(path); }
            catch (Exception ex) { LogHelper.WriteLogToFile($"[Save upload] {ex}", LogHelper.LogType.Warning); }
        }
    }
}
