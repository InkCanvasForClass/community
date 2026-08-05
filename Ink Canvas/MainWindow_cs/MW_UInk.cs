using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Ink_Canvas.UInk;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using File = System.IO.File;

namespace Ink_Canvas
{
    /// <summary>
    /// UInk 1.0 规范集成（保存/打开）。保存走两阶段提交（先 .uink.extra 后主文件）；
    /// 打开按 Workspace 类型恢复到对应模式（PPT/白板/屏幕批注），媒体资源经预算检查解压并拷贝到持久缓存目录。
    /// </summary>
    public partial class MainWindow
    {
        // ==================== 保存 ====================

        /// <summary>把当前 ICC 状态保存为 .uink（多页白板/PPT 幻灯片逐页、单页批注单页）。</summary>
        internal void SaveCurrentStateToUInk(string path, bool newNotice)
        {
            try
            {
                var devices = UInkIccMapper.BuildDisplayDevices();
                var workspaces = new List<UInkWorkspace>();
                var pages = new List<UInkPageInput>();

                bool isPPT = IsInPPTPresentationMode && _pptManager?.IsConnected == true;
                string wsGuid = Guid.NewGuid().ToString();
                UInkWorkspace ws = currentMode != 0
                    ? new UInkWorkspace { Guid = wsGuid, WorkspaceType = (int)UInkWorkspaceType.Whiteboard, Name = "白板" }
                    : isPPT
                        ? new UInkWorkspace { Guid = wsGuid, WorkspaceType = (int)UInkWorkspaceType.Presentation, Name = "演示" }
                        : new UInkWorkspace { Guid = wsGuid, WorkspaceType = (int)UInkWorkspaceType.ScreenAnnotation, Name = "屏幕批注" };
                UInkIccMapper.EnsureWorkspace(workspaces, ws);
                string deviceGuid = devices.Count > 0 ? devices[0].Guid : "";

                // 当前页媒体（图片/PDF/音视频）
                var currentMedia = new List<(UInkMedia media, string sourceFile)>();
                CollectMediaToUInk(currentMedia);

                if (isPPT)
                {
                    int totalSlides = _pptManager.SlidesCount;
                    int currentSlide = _pptManager.GetCurrentSlideNumber();
                    for (int i = 1; i <= totalSlides; i++)
                    {
                        var strokes = _singlePPTInkManager?.LoadSlideStrokes(i);
                        if ((strokes == null || strokes.Count == 0) && i == currentSlide)
                            strokes = inkCanvas.Strokes.Clone();
                        pages.Add(new UInkPageInput
                        {
                            Canvas = UInkIccMapper.BuildCanvas(wsGuid, deviceGuid,
                                Guid.NewGuid().ToString(), (uint)(i - 1), (uint)i, TryGetSlideId(i), null),
                            Strokes = strokes ?? new StrokeCollection(),
                            Media = i == currentSlide ? currentMedia.Select(x => x.media).ToList() : new List<UInkMedia>(),
                        });
                    }
                }
                else if (currentMode != 0 && WhiteboardTotalCount > 1)
                {
                    for (int i = 1; i <= WhiteboardTotalCount; i++)
                    {
                        var strokes = TimeMachineHistories[i] != null
                            ? ApplyHistoriesToNewStrokeCollection(TimeMachineHistories[i])
                            : new StrokeCollection();
                        if (strokes.Count == 0 && i == CurrentWhiteboardIndex)
                            strokes = inkCanvas.Strokes.Clone();
                        pages.Add(new UInkPageInput
                        {
                            Canvas = UInkIccMapper.BuildCanvas(wsGuid, deviceGuid,
                                Guid.NewGuid().ToString(), (uint)(i - 1), (uint)i, null, null),
                            Strokes = strokes,
                            Media = i == CurrentWhiteboardIndex ? currentMedia.Select(x => x.media).ToList() : new List<UInkMedia>(),
                        });
                    }
                }
                else
                {
                    pages.Add(new UInkPageInput
                    {
                        Canvas = UInkIccMapper.BuildCanvas(wsGuid, deviceGuid,
                            Guid.NewGuid().ToString(), 0, 1, null, UInkIccMapper.IdentityViewport()),
                        Strokes = inkCanvas.Strokes.Clone(),
                        Media = currentMedia.Select(x => x.media).ToList(),
                    });
                }

                var doc = UInkIccMapper.BuildDocument(
                    UInkIccMapper.NewFileGuid(), devices, workspaces, pages,
                    (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                // 资源集（entryPath, sourceFile）
                var resources = new List<(string, string)>();
                foreach (var (m, src) in currentMedia)
                    resources.Add((m.Path, src));

                UInkSaveService.SaveFull(doc, path, resources);

                if (newNotice)
                {
                    Task.Delay(100).ContinueWith(t =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ShowNotification(string.Format(MainWindowStrings.Main_Strokes_SaveUInkSuccess, path));
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                ShowNotification(MainWindowStrings.Main_Strokes_SaveUInkFailed);
                LogHelper.WriteLogToFile($"UInk 保存失败 | {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>遍历 inkCanvas 子元素，收集文件型媒体为 UInkMedia + 源文件（剪贴板/内存位图无源文件则跳过）。</summary>
        private void CollectMediaToUInk(List<(UInkMedia media, string sourceFile)> result)
        {
            if (inkCanvas == null) return;
            var elements = new List<CanvasElementInfo>();
            CollectCanvasElementsMetadata(elements);

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in elements)
            {
                if (string.IsNullOrEmpty(e.SourcePath) || !File.Exists(e.SourcePath)) continue;
                var entryPath = MakeUniqueEntryPath(e.SourcePath, used);

                var media = new UInkMedia
                {
                    Path = entryPath,
                    MimeType = UInkExtraArchive.MimeForPath(e.SourcePath),
                    Width = e.Width > 0 ? (float)e.Width : 800f,
                    Height = e.Height > 0 ? (float)e.Height : 520f,
                    Transform = new[] { 1f, 0f, 0f, 1f, (float)e.Left, (float)e.Top },
                    Opacity = 1f,
                };
                if (string.Equals(e.Type, "Pdf", StringComparison.OrdinalIgnoreCase))
                {
                    media.PageCount = e.PdfPageCount.HasValue ? (uint?)e.PdfPageCount.Value : null;
                    media.PageIndex = e.PdfCurrentPage.HasValue ? (uint?)e.PdfCurrentPage.Value : null;
                }
                else if (string.Equals(e.Type, "Media", StringComparison.OrdinalIgnoreCase))
                {
                    media.Volume = e.MediaVolume.HasValue ? (float)e.MediaVolume.Value : 1f;
                    media.StartTime = e.MediaPositionSeconds ?? 0.0;
                    media.PlaybackRate = e.MediaSpeedRatio.HasValue ? (float)e.MediaSpeedRatio.Value : 1f;
                }
                result.Add((media, e.SourcePath));
            }
        }

        private static string MakeUniqueEntryPath(string sourceFile, HashSet<string> used)
        {
            string name = Path.GetFileName(sourceFile);
            if (string.IsNullOrWhiteSpace(name)) name = "resource";
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            string candidate = "media/" + name;
            int n = 1;
            while (!used.Add(candidate))
                candidate = "media/" + (n++) + "_" + name;
            return candidate;
        }

        /// <summary>best-effort 取 PowerPoint COM SlideID（失败返回 null，省略 slideId）。</summary>
        private int? TryGetSlideId(int slideIndex)
        {
            try
            {
                if (pptApplication == null) return null;
                var presentation = pptApplication.SlideShowWindows?[1]?.Presentation;
                if (presentation == null || slideIndex < 1 || slideIndex > presentation.Slides.Count) return null;
                return presentation.Slides[slideIndex].SlideID;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ==================== 打开 ====================

        /// <summary>打开 .uink 主文件（含 .uink.extra 资源包）。</summary>
        private void OpenUInkFile(string path)
        {
            if (TryBlockFrozenPageMutation("打开 UInk 文件")) return;
            try
            {
                var doc = UInkReader.Load(path);
                if (doc == null)
                {
                    ShowNotification(MainWindowStrings.Main_Strokes_UInkInvalid);
                    return;
                }

                // 资源包：预算检查 + 安全解压到临时目录（媒体随后拷贝到持久缓存）
                Dictionary<string, string> extraMap = null;
                string extractDir = null;
                string extraPath = path + ".extra";
                if (File.Exists(extraPath))
                {
                    extractDir = Path.Combine(Path.GetTempPath(), "UInkOpen_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(extractDir);
                    extraMap = UInkExtraArchive.ExtractWithBudget(extraPath, extractDir);
                    if (extraMap == null)
                        LogHelper.WriteLogToFile($"UInk 资源包预算/安全校验失败，仅加载墨迹: {extraPath}", LogHelper.LogType.Warning);
                }

                try
                {
                    var pages = UInkIccMapper.ToPages(doc, UInkConversion.BlockToStroke);
                    if (pages.Count == 0)
                    {
                        ShowNotification(MainWindowStrings.Main_Strokes_UInkInvalid);
                        return;
                    }

                    // UInk 可注册父子 Workspace（如主白板 + 板中板）。ICC 当前一次只能承载一个 Workspace，
                    // 因此优先加载根 Workspace（无 parentWorkspaceGuid）；否则同 pageIndex 的空子 Workspace
                    // 会覆盖主 Workspace 的墨迹（explicit-multilayer fixture 即为此情形）。
                    var rootWorkspace = doc.HeaderExtension?.Workspaces?
                        .FirstOrDefault(x => string.IsNullOrEmpty(x.ParentWorkspaceGuid))
                        ?? doc.HeaderExtension?.Workspaces?.FirstOrDefault();
                    if (rootWorkspace != null)
                    {
                        var rootPages = pages
                            .Where(p => string.Equals(p.Canvas?.WorkspaceGuid, rootWorkspace.Guid, StringComparison.Ordinal))
                            .ToList();
                        if (rootPages.Count > 0) pages = rootPages;
                    }

                    var wsType = rootWorkspace?.WorkspaceType ?? (int)UInkWorkspaceType.ScreenAnnotation;
                    bool isPPT = IsInPPTPresentationMode && _pptManager?.IsConnected == true;
                    bool isWhiteboard = currentMode != 0;

                    if (wsType == (int)UInkWorkspaceType.Presentation && isPPT)
                        RestoreUInkToPPT(pages, extraMap, extractDir);
                    else if (wsType == (int)UInkWorkspaceType.Whiteboard && isWhiteboard)
                        RestoreUInkToWhiteboard(pages, extraMap, extractDir);
                    else
                        RestoreUInkToAnnotation(pages, extraMap, extractDir);

                    ShowNotification(string.Format(MainWindowStrings.Main_Strokes_OpenUInkSuccess, pages.Count));
                }
                finally
                {
                    if (extractDir != null)
                    {
                        try { if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowNotification(MainWindowStrings.Main_Strokes_OpenFailed);
                LogHelper.WriteLogToFile($"UInk 打开失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>屏幕批注/单页恢复：当前画布载入首个可见页墨迹 + 撤回链 + 媒体。</summary>
        private void RestoreUInkToAnnotation(List<UInkPageData> pages, Dictionary<string, string> extraMap, string extractDir)
        {
            var page = pages.FirstOrDefault(p => p.Canvas?.LayerIndex == 0) ?? pages[0];
            ClearStrokes(true);
            timeMachine.ClearStrokeHistory();
            if (page.History != null && page.History.Length > 0)
                timeMachine.ImportTimeMachineHistory(page.History);
            inkCanvas.Strokes.Add(page.FinalStrokes);
            RestoreUInkMediaForPage(page.Media, extraMap, extractDir);
            var bounds = page.FinalStrokes.Count > 0 ? page.FinalStrokes.GetBounds() : Rect.Empty;
            LogHelper.WriteLogToFile($"UInk 屏幕批注恢复: {page.FinalStrokes.Count} 条墨迹, bounds={bounds}", LogHelper.LogType.Event);
        }

        /// <summary>白板恢复：按 pageIndex 填充 TimeMachineHistories，切到第 1 页。</summary>
        private void RestoreUInkToWhiteboard(List<UInkPageData> pages, Dictionary<string, string> extraMap, string extractDir)
        {
            ClearStrokes(true);
            timeMachine.ClearStrokeHistory();

            WhiteboardTotalCount = Math.Max(pages.Count, 1);
            CurrentWhiteboardIndex = 1;
            ResetInkFreezePageStates();
            for (int i = 0; i < TimeMachineHistories.Length; i++) TimeMachineHistories[i] = null;

            foreach (var page in pages)
            {
                int idx = (int)(page.Canvas?.PageIndex ?? 0) + 1;
                if (idx < 1 || idx >= TimeMachineHistories.Length) continue;
                TimeMachineHistories[idx] = page.History != null && page.History.Length > 0
                    ? page.History
                    : new[] { new TimeMachineHistory(page.FinalStrokes, TimeMachineHistoryType.UserInput, false) };
            }

            if (TimeMachineHistories[1] != null) RestoreStrokes();
            if (pages.Count > 0) RestoreUInkMediaForPage(pages[0].Media, extraMap, extractDir);
            UpdateIndexInfoDisplay();
            LogHelper.WriteLogToFile($"UInk 白板恢复: {pages.Count} 页", LogHelper.LogType.Event);
        }

        /// <summary>PPT 放映恢复：按 pageIndex 写入 _singlePPTInkManager，恢复当前幻灯片墨迹。</summary>
        private void RestoreUInkToPPT(List<UInkPageData> pages, Dictionary<string, string> extraMap, string extractDir)
        {
            if (!IsInPPTPresentationMode || _pptManager == null) return;
            ClearStrokes(true);
            timeMachine.ClearStrokeHistory();
            _singlePPTInkManager?.ClearAllStrokes();

            foreach (var page in pages)
            {
                int slide = (int)(page.Canvas?.PageIndex ?? 0) + 1;
                if (slide < 1) continue;
                _singlePPTInkManager?.ForceSaveSlideStrokes(slide, page.FinalStrokes);
            }

            int currentSlide = _pptManager.GetCurrentSlideNumber();
            var current = _singlePPTInkManager?.LoadSlideStrokes(currentSlide);
            if (current != null && current.Count > 0) inkCanvas.Strokes.Add(current);

            var curPage = pages.FirstOrDefault(p => (int)(p.Canvas?.PageIndex ?? 0) + 1 == currentSlide);
            if (curPage != null) RestoreUInkMediaForPage(curPage.Media, extraMap, extractDir);
            LogHelper.WriteLogToFile($"UInk PPT 恢复: {pages.Count} 张幻灯片", LogHelper.LogType.Event);
        }

        /// <summary>把 UInkMedia 还原为画布元素：资源拷贝到持久缓存目录后，复用既有 Image/PDF/媒体恢复管线。</summary>
        private void RestoreUInkMediaForPage(IEnumerable<UInkMedia> mediaList, Dictionary<string, string> extraMap, string extractDir)
        {
            if (mediaList == null) return;
            var cacheDir = Path.Combine(Settings.Automation.AutoSavedStrokesLocation, "UInk Media");
            try { Directory.CreateDirectory(cacheDir); } catch { }

            foreach (var m in mediaList)
            {
                try
                {
                    string localFile = null;
                    if (!string.IsNullOrEmpty(m.Path) && extraMap != null && extraMap.TryGetValue(m.Path, out var extracted))
                        localFile = extracted;
                    if (string.IsNullOrEmpty(localFile) || !File.Exists(localFile))
                    {
                        LogHelper.WriteLogToFile($"UInk 媒体资源缺失（保留布局）: {m.Path}", LogHelper.LogType.Warning);
                        continue;
                    }

                    string cached = Path.Combine(cacheDir, Path.GetFileName(localFile));
                    if (!File.Exists(cached)) File.Copy(localFile, cached, overwrite: true);

                    float left = m.Transform != null && m.Transform.Length == 6 ? m.Transform[4] : 0f;
                    float top = m.Transform != null && m.Transform.Length == 6 ? m.Transform[5] : 0f;
                    double width = m.Width.HasValue && m.Width.Value > 0 ? m.Width.Value : 800.0;
                    double height = m.Height.HasValue && m.Height.Value > 0 ? m.Height.Value : 520.0;
                    bool isPdf = string.Equals(m.MimeType, "application/pdf", StringComparison.OrdinalIgnoreCase);
                    bool isAudio = m.MimeType != null && m.MimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);
                    bool isVideo = m.MimeType != null && m.MimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

                    var info = new CanvasElementInfo
                    {
                        Type = isPdf ? "Pdf" : (isAudio || isVideo ? "Media" : "Image"),
                        SourcePath = cached,
                        Left = left,
                        Top = top,
                        Width = width,
                        Height = height,
                        Stretch = "Uniform",
                        MediaKind = isAudio ? "Audio" : (isVideo ? "Video" : null),
                        MediaDisplayName = Path.GetFileName(cached),
                        MediaPositionSeconds = m.StartTime > 0 ? m.StartTime : (double?)null,
                        MediaSpeedRatio = Math.Abs(m.PlaybackRate - 1f) > 0.0001f ? (double?)m.PlaybackRate : null,
                        MediaVolume = Math.Abs(m.Volume - 1f) > 0.0001f ? (double?)m.Volume : null,
                        PdfCurrentPage = m.PageIndex.HasValue ? (int?)m.PageIndex.Value : null,
                        PdfPageCount = m.PageCount.HasValue ? (int?)m.PageCount.Value : null,
                    };

                    if (isPdf)
                    {
                        Dispatcher.BeginInvoke(new Action(() => { _ = RestorePdfFromElementInfoAsync(info); }), DispatcherPriority.Loaded);
                    }
                    else if (isAudio || isVideo)
                    {
                        RestoreMediaFromElementInfo(info);
                    }
                    else
                    {
                        var bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.UriSource = new Uri(info.SourcePath);
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze();
                        var img = new Image
                        {
                            Source = bitmapImage,
                            Width = width,
                            Height = height,
                            Stretch = System.Windows.Media.Stretch.Uniform,
                        };
                        InkCanvas.SetLeft(img, left);
                        InkCanvas.SetTop(img, top);
                        inkCanvas.Children.Add(img);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"UInk 媒体恢复失败: {ex.Message}", LogHelper.LogType.Error);
                }
            }
        }
    }
}
