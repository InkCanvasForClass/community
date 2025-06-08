using Ink_Canvas.Helpers;
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using File = System.IO.File;

namespace Ink_Canvas {
    public partial class MainWindow : Window {
        private void SymbolIconSaveStrokes_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender || inkCanvas.Visibility != Visibility.Visible) return;

            AnimationsHelper.HideWithSlideAndFade(BorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);

            GridNotifications.Visibility = Visibility.Collapsed;

            SaveInkCanvasStrokes(true, true);
        }

        private void SaveInkCanvasStrokes(Boolean newNotice, Boolean saveByUser) {
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

                try {
                    using (FileStream fs = new FileStream(savePathWithName, FileMode.Create)) { // 修复未定义的filePath引用
                        inkCanvas.Strokes.Save(fs);
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is DirectoryNotFoundException) {
                    var docPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "Auto Saved - Annotation Strokes",
                        DateTime.Now.ToString("yyyyMMdd"),
                        Path.GetFileNameWithoutExtension(savePathWithName) + "_retry.icstk"); // 使用正确的原始文件名

                    try {
                        Directory.CreateDirectory(Path.GetDirectoryName(docPath));
                        using (FileStream fs = new FileStream(docPath, FileMode.Create)) {
                            inkCanvas.Strokes.Save(fs);
                            savePathWithName = docPath; // 更新通知使用的路径变量
                        }
                    }
                    catch (Exception fallbackEx) {
                        // 如果文档路径保存失败，尝试用户目录
                        string userDocPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            "Documents",
                            "墨迹备份",
                            Path.GetFileName(docPath)
                        );

                        try {
                            Directory.CreateDirectory(Path.GetDirectoryName(userDocPath));
                            using (FileStream fs = new FileStream(userDocPath, FileMode.Create)) {
                                inkCanvas.Strokes.Save(fs);
                                savePathWithName = userDocPath;
                            }
                        }
                        catch (Exception userFallbackEx) {
                            ShowNotification($"墨迹保存失败: {userFallbackEx.Message}");
                            return;
                        }
                    }
                }
                catch (Exception ex) {
                    ShowNotification($"墨迹保存失败: {ex.Message}");
                    return;
                }

                if (newNotice) ShowNotification("墨迹成功保存至 " + savePathWithName);
            }
            catch (Exception ex) {
                ShowNotification("墨迹保存失败");
                LogHelper.WriteLogToFile("墨迹保存失败 | " + ex.ToString(), LogHelper.LogType.Error);
            }
        }

        private void SymbolIconOpenStrokes_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;
            AnimationsHelper.HideWithSlideAndFade(BorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);

            var openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Settings.Automation.AutoSavedStrokesLocation;
            openFileDialog.Title = "打开墨迹文件";
            openFileDialog.Filter = "Ink Canvas Strokes File (*.icstk)|*.icstk";
            if (openFileDialog.ShowDialog() != true) return;
            LogHelper.WriteLogToFile($"Strokes Insert: Name: {openFileDialog.FileName}",
                LogHelper.LogType.Event);
            try {
                var fileStreamHasNoStroke = false;
                using (var fs = new FileStream(openFileDialog.FileName, FileMode.Open, FileAccess.Read)) {
                    var strokes = new StrokeCollection(fs);
                    fileStreamHasNoStroke = strokes.Count == 0;
                    if (!fileStreamHasNoStroke) {
                        ClearStrokes(true);
                        timeMachine.ClearStrokeHistory();
                        inkCanvas.Strokes.Add(strokes);
                        LogHelper.NewLog($"Strokes Insert: Strokes Count: {inkCanvas.Strokes.Count.ToString()}");
                    }
                }

                if (fileStreamHasNoStroke)
                    using (var ms = new MemoryStream(File.ReadAllBytes(openFileDialog.FileName))) {
                        ms.Seek(0, SeekOrigin.Begin);
                        var strokes = new StrokeCollection(ms);
                        ClearStrokes(true);
                        timeMachine.ClearStrokeHistory();
                        inkCanvas.Strokes.Add(strokes);
                        LogHelper.NewLog($"Strokes Insert (2): Strokes Count: {strokes.Count.ToString()}");
                    }

                if (inkCanvas.Visibility != Visibility.Visible) SymbolIconCursor_Click(sender, null);
            }
            catch {
                ShowNotification("墨迹打开失败");
            }
        }
    }
}