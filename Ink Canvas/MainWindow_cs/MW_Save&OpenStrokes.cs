using Ink_Canvas.Helpers;
using System;
using System.IO;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using File = System.IO.File;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using System.Collections.Generic;
using System.Windows.Controls;
using Newtonsoft.Json;

namespace Ink_Canvas {
    // 1. 定义元素信息结构
    public class CanvasElementInfo
    {
        public string Type { get; set; } // "Image"
        public string SourcePath { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
    public partial class MainWindow : Window {
        private void SymbolIconSaveStrokes_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender || inkCanvas.Visibility != Visibility.Visible) return;

            AnimationsHelper.HideWithSlideAndFade(BorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);

            GridNotifications.Visibility = Visibility.Collapsed;

            SaveInkCanvasStrokes(true, true);
        }

        private void SaveInkCanvasStrokes(bool newNotice = true, bool saveByUser = false) {
            try {
                var savePath = Settings.Automation.AutoSavedStrokesLocation
                               + (saveByUser ? @"\User Saved - " : @"\Auto Saved - ")
                               + (currentMode == 0 ? "Annotation Strokes" : "BlackBoard Strokes");
                if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);
                string savePathWithName;
                if (currentMode != 0) // 黑板模式下
                    savePathWithName = savePath + @"\" + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss-fff") + " Page-" +
                                       CurrentWhiteboardIndex + " StrokesCount-" + inkCanvas.Strokes.Count + ".icstk";
                else
                    //savePathWithName = savePath + @"\" + DateTime.Now.ToString("u").Replace(':', '-') + ".icstk";
                    savePathWithName = savePath + @"\" + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss-fff") + ".icstk";
                
                if (Settings.Automation.IsSaveFullPageStrokes)
                {
                    // 全页面保存模式 - 检查是否存在多页面墨迹
                    bool hasMultiplePages = false;
                    List<StrokeCollection> allPageStrokes = new List<StrokeCollection>();
                    
                    // 检查PPT放映模式下的多页面墨迹
                    if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible && pptApplication != null)
                    {
                        hasMultiplePages = true;
                        // 收集PPT放映模式下的所有页面墨迹
                        for (int i = 1; i <= pptApplication.SlideShowWindows[1].Presentation.Slides.Count; i++)
                        {
                            if (memoryStreams[i] != null && memoryStreams[i].Length > 8)
                            {
                                memoryStreams[i].Position = 0;
                                allPageStrokes.Add(new StrokeCollection(memoryStreams[i]));
                            }
                            else if (i == previousSlideID && inkCanvas.Strokes.Count > 0)
                            {
                                // 当前页面的墨迹
                                allPageStrokes.Add(inkCanvas.Strokes.Clone());
                            }
                            else
                            {
                                allPageStrokes.Add(new StrokeCollection()); // 空页面
                            }
                        }
                    }
                    // 检查白板模式下的多页面墨迹
                    else if (currentMode != 0 && WhiteboardTotalCount > 1)
                    {
                        hasMultiplePages = true;
                        // 收集白板模式下的所有页面墨迹
                        for (int i = 1; i <= WhiteboardTotalCount; i++)
                        {
                            if (TimeMachineHistories[i] != null)
                            {
                                // 从历史记录中恢复墨迹
                                var strokes = ApplyHistoriesToNewStrokeCollection(TimeMachineHistories[i]);
                                allPageStrokes.Add(strokes);
                            }
                            else
                            {
                                allPageStrokes.Add(new StrokeCollection()); // 空页面
                            }
                        }
                    }
                    
                    if (hasMultiplePages && allPageStrokes.Count > 0)
                    {
                        // 多页面墨迹保存为压缩包
                        string zipFileName = Path.ChangeExtension(savePathWithName, "zip");
                        SaveMultiPageStrokesAsZip(allPageStrokes, zipFileName, newNotice);
                    }
                    else
                    {
                        // 单页面墨迹保存为图像
                        SaveSinglePageStrokesAsImage(savePathWithName, newNotice);
                    }
                }
                else
                {
                    // 常规保存模式 - 仅保存墨迹对象
                    var fs = new FileStream(savePathWithName, FileMode.Create);
                    inkCanvas.Strokes.Save(fs);
                    fs.Close();
                    // 保存元素信息
                    var elementInfos = new List<CanvasElementInfo>();
                    foreach (var child in inkCanvas.Children)
                    {
                        if (child is Image img && img.Source is BitmapImage bmp)
                        {
                            elementInfos.Add(new CanvasElementInfo
                            {
                                Type = "Image",
                                SourcePath = bmp.UriSource?.LocalPath ?? "",
                                Left = InkCanvas.GetLeft(img),
                                Top = InkCanvas.GetTop(img),
                                Width = img.Width,
                                Height = img.Height
                            });
                        }
                    }
                    File.WriteAllText(Path.ChangeExtension(savePathWithName, ".elements.json"), JsonConvert.SerializeObject(elementInfos, Formatting.Indented));
                    if (newNotice) ShowNotification("墨迹成功保存至 " + savePathWithName);
                }
            }
            catch (Exception ex) {
                ShowNotification("墨迹保存失败");
                LogHelper.WriteLogToFile("墨迹保存失败 | " + ex.ToString(), LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 将多页面墨迹保存为压缩包
        /// </summary>
        private void SaveMultiPageStrokesAsZip(List<StrokeCollection> allPageStrokes, string zipFileName, bool newNotice)
        {
            try
            {
                // 创建临时目录来存放文件
                string tempDir = Path.Combine(Path.GetTempPath(), $"InkCanvas_MultiPage_{DateTime.Now:yyyyMMdd_HHmmss}");
                Directory.CreateDirectory(tempDir);
                
                try
                {
                    // 保存所有页面的文件到临时目录
                    for (int i = 0; i < allPageStrokes.Count; i++)
                    {
                        var strokes = allPageStrokes[i];
                        if (strokes.Count > 0)
                        {
                            // 保存墨迹文件
                            string strokeFileName = Path.Combine(tempDir, $"page_{i + 1:D4}.icstk");
                            using (var fs = new FileStream(strokeFileName, FileMode.Create))
                            {
                                strokes.Save(fs);
                            }
                            
                            // 保存页面图像
                            string imageFileName = Path.Combine(tempDir, $"page_{i + 1:D4}.png");
                            using (var fs = new FileStream(imageFileName, FileMode.Create))
                            {
                                SavePageAsImage(strokes, fs);
                            }
                        }
                    }
                    
                    // 保存元数据信息
                    string metadataFile = Path.Combine(tempDir, "metadata.txt");
                    using (var writer = new StreamWriter(metadataFile, false, System.Text.Encoding.UTF8))
                    {
                        writer.WriteLine($"保存时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        writer.WriteLine($"总页数: {allPageStrokes.Count}");
                        writer.WriteLine($"模式: {(currentMode == 0 ? "PPT放映" : "白板")}");
                        if (currentMode != 0)
                        {
                            writer.WriteLine($"当前页面: {CurrentWhiteboardIndex}");
                            writer.WriteLine($"总页面数: {WhiteboardTotalCount}");
                        }
                        else if (pptApplication != null)
                        {
                            writer.WriteLine($"PPT名称: {pptApplication.SlideShowWindows[1].Presentation.Name}");
                            writer.WriteLine($"PPT总页数: {pptApplication.SlideShowWindows[1].Presentation.Slides.Count}");
                            writer.WriteLine($"PPT文件路径: {pptApplication.SlideShowWindows[1].Presentation.FullName}");
                        }
                        
                        for (int i = 0; i < allPageStrokes.Count; i++)
                        {
                            writer.WriteLine($"页面 {i + 1}: {allPageStrokes[i].Count} 条墨迹");
                        }
                    }
                    
                    // 使用.NET Framework内置的压缩功能创建ZIP文件
                    if (File.Exists(zipFileName))
                        File.Delete(zipFileName);
                    
                    // 使用System.IO.Compression.FileSystem来创建ZIP
                    System.IO.Compression.ZipFile.CreateFromDirectory(tempDir, zipFileName);
                    
                    if (newNotice) ShowNotification($"多页面墨迹成功保存至压缩包 {zipFileName}");
                }
                finally
                {
                    // 清理临时目录
                    try
                    {
                        if (Directory.Exists(tempDir))
                            Directory.Delete(tempDir, true);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"清理临时目录失败: {ex.ToString()}", LogHelper.LogType.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存多页面墨迹压缩包失败: {ex.ToString()}", LogHelper.LogType.Error);
                throw;
            }
        }

        /// <summary>
        /// 将单页面墨迹保存为图像
        /// </summary>
        private void SaveSinglePageStrokesAsImage(string savePathWithName, bool newNotice)
        {
            // 全页面保存模式 - 保存整个墨迹页面的图像
            var bitmap = new System.Drawing.Bitmap(
                (int)System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width, 
                (int)System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height);
            
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                // 创建黑色或透明背景
                System.Drawing.Color bgColor = Settings.Canvas.UsingWhiteboard 
                    ? System.Drawing.Color.White 
                    : System.Drawing.Color.FromArgb(22, 41, 36); // 黑板背景色
                g.Clear(bgColor);
                
                // 将InkCanvas墨迹渲染到Visual
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    // 创建一个VisualBrush，使用inkCanvas作为源
                    var visualBrush = new VisualBrush(inkCanvas);
                    // 绘制矩形并填充为inkCanvas的内容
                    dc.DrawRectangle(visualBrush, null, new Rect(0, 0, inkCanvas.ActualWidth, inkCanvas.ActualHeight));
                }
                
                // 创建适合墨迹画布尺寸的渲染位图
                var rtb = new RenderTargetBitmap(
                    (int)inkCanvas.ActualWidth, (int)inkCanvas.ActualHeight, 
                    96, 96, 
                    PixelFormats.Pbgra32);
                rtb.Render(visual);
                
                // 转换为GDI+ Bitmap并保存
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                
                using (var ms = new MemoryStream())
                {
                    encoder.Save(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    var imgBitmap = new System.Drawing.Bitmap(ms);
                    
                    // 将生成的墨迹图像绘制到屏幕截图上
                    // 居中绘制，确保墨迹位于屏幕中央
                    int x = (bitmap.Width - imgBitmap.Width) / 2;
                    int y = (bitmap.Height - imgBitmap.Height) / 2;
                    g.DrawImage(imgBitmap, x, y);
                    
                    // 保存为PNG
                    string imagePathWithName = Path.ChangeExtension(savePathWithName, "png");
                    bitmap.Save(imagePathWithName, System.Drawing.Imaging.ImageFormat.Png);
                    
                    // 仍然保存墨迹文件以兼容旧版本
                    var fs = new FileStream(savePathWithName, FileMode.Create);
                    inkCanvas.Strokes.Save(fs);
                    fs.Close();
                }
            }
            
            // 显示提示
            if (newNotice) ShowNotification("墨迹成功全页面保存至 " + Path.ChangeExtension(savePathWithName, "png"));
        }

        /// <summary>
        /// 将指定墨迹集合保存为图像到指定流
        /// </summary>
        private void SavePageAsImage(StrokeCollection strokes, Stream outputStream)
        {
            try
            {
                // 创建临时InkCanvas来渲染墨迹
                var tempCanvas = new InkCanvas();
                tempCanvas.Strokes = strokes;
                tempCanvas.Width = inkCanvas.ActualWidth;
                tempCanvas.Height = inkCanvas.ActualHeight;
                
                // 创建渲染位图
                var rtb = new RenderTargetBitmap(
                    (int)tempCanvas.Width, (int)tempCanvas.Height, 
                    96, 96, 
                    PixelFormats.Pbgra32);
                rtb.Render(tempCanvas);
                
                // 保存为PNG
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                encoder.Save(outputStream);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存页面图像失败: {ex.ToString()}", LogHelper.LogType.Error);
                throw;
            }
        }

        private void SymbolIconOpenStrokes_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;
            AnimationsHelper.HideWithSlideAndFade(BorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);

            var openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Settings.Automation.AutoSavedStrokesLocation;
            openFileDialog.Title = "打开墨迹文件";
            openFileDialog.Filter = "Ink Canvas Strokes File (*.icstk)|*.icstk|ICC压缩包 (*.zip)|*.zip";
            if (openFileDialog.ShowDialog() != true) return;
            LogHelper.WriteLogToFile($"Strokes Insert: Name: {openFileDialog.FileName}",
                LogHelper.LogType.Event);
            
            try {
                string fileExtension = Path.GetExtension(openFileDialog.FileName).ToLower();
                
                if (fileExtension == ".zip") {
                    // 处理ICC压缩包
                    OpenICCZipFile(openFileDialog.FileName);
                } else {
                    // 处理单个墨迹文件
                    OpenSingleStrokeFile(openFileDialog.FileName);
                }

                if (inkCanvas.Visibility != Visibility.Visible) SymbolIconCursor_Click(sender, null);
            }
            catch (Exception ex) {
                ShowNotification("墨迹打开失败");
                LogHelper.WriteLogToFile($"墨迹打开失败: {ex.ToString()}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 打开ICC创建的.zip压缩包
        /// </summary>
        private void OpenICCZipFile(string zipFilePath) {
            try {
                // 创建临时目录来解压文件
                string tempDir = Path.Combine(Path.GetTempPath(), $"InkCanvas_Open_{DateTime.Now:yyyyMMdd_HHmmss}");
                Directory.CreateDirectory(tempDir);
                
                try {
                    // 解压ZIP文件
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, tempDir);
                    
                    // 读取元数据文件
                    string metadataFile = Path.Combine(tempDir, "metadata.txt");
                    if (!File.Exists(metadataFile)) {
                        throw new Exception("压缩包中未找到元数据文件");
                    }
                    
                    var metadata = ReadMetadataFile(metadataFile);
                    
                    // 根据元数据信息决定恢复模式
                    bool isPPTMode = metadata.ContainsKey("模式") && metadata["模式"].Contains("PPT放映");
                    bool isWhiteboardMode = metadata.ContainsKey("模式") && metadata["模式"].Contains("白板");
                    
                    // 检查当前是否处于PPT模式
                    bool isCurrentlyInPPTMode = BtnPPTSlideShowEnd.Visibility == Visibility.Visible && pptApplication != null;
                    
                    // 检查当前是否处于白板模式
                    bool isCurrentlyInWhiteboardMode = currentMode != 0;
                    
                    // 严格模式隔离：只在对应模式下恢复对应墨迹
                    if (isPPTMode && isCurrentlyInPPTMode) {
                        // 只在PPT放映模式下恢复PPT墨迹
                        RestorePPTStrokesFromZip(tempDir, metadata);
                    } else if (isWhiteboardMode && isCurrentlyInWhiteboardMode) {
                        // 只在白板模式下恢复白板墨迹
                        RestoreWhiteboardStrokesFromZip(tempDir, metadata);
                    } else {
                        // 模式不匹配时，显示提示信息
                        string savedMode = isPPTMode ? "PPT放映" : (isWhiteboardMode ? "白板" : "未知");
                        string currentMode = isCurrentlyInPPTMode ? "PPT放映" : (isCurrentlyInWhiteboardMode ? "白板" : "桌面");
                        ShowNotification($"墨迹保存模式({savedMode})与当前模式({currentMode})不匹配，无法恢复墨迹");
                        LogHelper.WriteLogToFile($"模式不匹配：保存模式={savedMode}，当前模式={currentMode}", LogHelper.LogType.Warning);
                    }
                    
                    ShowNotification($"成功打开ICC压缩包，共{(metadata.ContainsKey("总页数") ? metadata["总页数"] : "0")}页");
                }
                finally {
                    // 清理临时目录
                    try {
                        if (Directory.Exists(tempDir))
                            Directory.Delete(tempDir, true);
                    }
                    catch (Exception ex) {
                        LogHelper.WriteLogToFile($"清理临时目录失败: {ex.ToString()}", LogHelper.LogType.Warning);
                    }
                }
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile($"打开ICC压缩包失败: {ex.ToString()}", LogHelper.LogType.Error);
                throw;
            }
        }

        /// <summary>
        /// 读取元数据文件
        /// </summary>
        private Dictionary<string, string> ReadMetadataFile(string metadataPath) {
            var metadata = new Dictionary<string, string>();
            
            using (var reader = new StreamReader(metadataPath, System.Text.Encoding.UTF8)) {
                string line;
                while ((line = reader.ReadLine()) != null) {
                    if (line.Contains(":")) {
                        var parts = line.Split(new[] { ':' }, 2);
                        if (parts.Length == 2) {
                            metadata[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                }
            }
            
            return metadata;
        }

        /// <summary>
        /// 从ZIP文件恢复PPT墨迹
        /// </summary>
        private void RestorePPTStrokesFromZip(string tempDir, Dictionary<string, string> metadata) {
            try {
                // 确保当前处于PPT放映模式
                if (BtnPPTSlideShowEnd.Visibility != Visibility.Visible || pptApplication == null) {
                    throw new InvalidOperationException("当前不在PPT放映模式，无法恢复PPT墨迹");
                }
                
                // 检查PPT文件路径是否匹配
                if (metadata.ContainsKey("PPT文件路径"))
                {
                    string savedPptPath = metadata["PPT文件路径"];
                    string currentPptPath = pptApplication.SlideShowWindows[1].Presentation.FullName;
                    
                    if (!string.IsNullOrEmpty(savedPptPath) && !string.IsNullOrEmpty(currentPptPath))
                    {
                        // 使用文件路径哈希值进行比较，避免路径格式差异
                        string savedHash = GetFileHash(savedPptPath);
                        string currentHash = GetFileHash(currentPptPath);
                        
                        if (savedHash != currentHash)
                        {
                            throw new InvalidOperationException($"墨迹文件与当前PPT文件不匹配。保存的PPT: {savedPptPath}，当前PPT: {currentPptPath}");
                        }
                    }
                }
                
                // 清空当前墨迹
                ClearStrokes(true);
                timeMachine.ClearStrokeHistory();
                
                // 重置PPT墨迹存储
                if (memoryStreams == null) {
                    memoryStreams = new MemoryStream[50];
                }
                
                // 读取所有页面的墨迹文件
                var files = Directory.GetFiles(tempDir, "page_*.icstk");
                foreach (var file in files) {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (fileName.StartsWith("page_") && int.TryParse(fileName.Substring(5), out int pageNumber)) {
                        using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read)) {
                            var strokes = new StrokeCollection(fs);
                            if (strokes.Count > 0) {
                                var ms = new MemoryStream();
                                strokes.Save(ms);
                                ms.Position = 0;
                                memoryStreams[pageNumber] = ms;
                            }
                        }
                    }
                }
                
                // 恢复当前页面的墨迹
                if (pptApplication.SlideShowWindows.Count > 0) {
                    int currentSlide = pptApplication.SlideShowWindows[1].View.CurrentShowPosition;
                    if (memoryStreams[currentSlide] != null && memoryStreams[currentSlide].Length > 0) {
                        memoryStreams[currentSlide].Position = 0;
                        inkCanvas.Strokes.Add(new StrokeCollection(memoryStreams[currentSlide]));
                    }
                    previousSlideID = currentSlide;
                }
                
                LogHelper.WriteLogToFile($"成功恢复PPT墨迹，共{files.Length}页");
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile($"恢复PPT墨迹失败: {ex.ToString()}", LogHelper.LogType.Error);
                throw;
            }
        }

        /// <summary>
        /// 从ZIP文件恢复白板墨迹
        /// </summary>
        private void RestoreWhiteboardStrokesFromZip(string tempDir, Dictionary<string, string> metadata) {
            try {
                // 确保当前处于白板模式
                if (currentMode == 0) {
                    throw new InvalidOperationException("当前不在白板模式，无法恢复白板墨迹");
                }
                
                // 清空当前墨迹
                ClearStrokes(true);
                timeMachine.ClearStrokeHistory();
                
                // 读取总页数
                int totalPages = 1;
                if (metadata.ContainsKey("总页数") && int.TryParse(metadata["总页数"], out int parsedPages)) {
                    totalPages = parsedPages;
                }
                
                // 重置白板状态
                WhiteboardTotalCount = totalPages;
                CurrentWhiteboardIndex = 1;
                
                // 清空历史记录
                for (int i = 0; i < TimeMachineHistories.Length; i++) {
                    TimeMachineHistories[i] = null;
                }
                
                // 读取所有页面的墨迹文件
                var files = Directory.GetFiles(tempDir, "page_*.icstk");
                foreach (var file in files) {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (fileName.StartsWith("page_") && int.TryParse(fileName.Substring(5), out int pageNumber)) {
                        using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read)) {
                            var strokes = new StrokeCollection(fs);
                            if (strokes.Count > 0) {
                                // 创建历史记录
                                var history = new TimeMachineHistory(strokes, TimeMachineHistoryType.UserInput, false);
                                TimeMachineHistories[pageNumber] = new TimeMachineHistory[] { history };
                            }
                        }
                    }
                }
                
                // 恢复第一页的墨迹
                if (TimeMachineHistories[1] != null) {
                    RestoreStrokes();
                }
                
                // 更新UI显示
                UpdateIndexInfoDisplay();
                
                LogHelper.WriteLogToFile($"成功恢复白板墨迹，共{totalPages}页");
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile($"恢复白板墨迹失败: {ex.ToString()}", LogHelper.LogType.Error);
                throw;
            }
        }

        /// <summary>
        /// 打开单个墨迹文件
        /// </summary>
        private void OpenSingleStrokeFile(string filePath) {
                var fileStreamHasNoStroke = false;
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read)) {
                    var strokes = new StrokeCollection(fs);
                    fileStreamHasNoStroke = strokes.Count == 0;
                    if (!fileStreamHasNoStroke) {
                        ClearStrokes(true);
                        timeMachine.ClearStrokeHistory();
                        inkCanvas.Strokes.Add(strokes);
                        LogHelper.NewLog($"Strokes Insert: Strokes Count: {inkCanvas.Strokes.Count.ToString()}");
                    }
                }

                // 恢复元素信息
                var elementsFile = Path.ChangeExtension(filePath, ".elements.json");
                if (File.Exists(elementsFile))
                {
                    var elementInfos = JsonConvert.DeserializeObject<List<CanvasElementInfo>>(File.ReadAllText(elementsFile));
                    foreach (var info in elementInfos)
                    {
                        if (info.Type == "Image" && File.Exists(info.SourcePath))
                        {
                            var img = new Image
                            {
                                Source = new BitmapImage(new Uri(info.SourcePath)),
                                Width = info.Width,
                                Height = info.Height
                            };
                            InkCanvas.SetLeft(img, info.Left);
                            InkCanvas.SetTop(img, info.Top);
                            inkCanvas.Children.Add(img);
                        }
                    }
                }

                if (fileStreamHasNoStroke)
                using (var ms = new MemoryStream(File.ReadAllBytes(filePath))) {
                        ms.Seek(0, SeekOrigin.Begin);
                        var strokes = new StrokeCollection(ms);
                        ClearStrokes(true);
                        timeMachine.ClearStrokeHistory();
                        inkCanvas.Strokes.Add(strokes);
                        LogHelper.NewLog($"Strokes Insert (2): Strokes Count: {strokes.Count.ToString()}");
            }
        }
    }
}
