using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Ink;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        /// <summary>
        /// 保存自动化行动指定的笔画，不读取或修改全局保存格式设置。
        /// SavePath 为空时使用默认自动保存目录；否则将其作为目标目录。
        /// </summary>
        public void SaveStrokesForAutomation(string savePath, bool saveAsXml)
        {
            if (inkCanvas == null)
            {
                LogHelper.WriteLogToFile("[Automation] 保存笔画失败：墨迹画布不可用。", LogHelper.LogType.Warning);
                return;
            }

            try
            {
                var targetDirectory = string.IsNullOrWhiteSpace(savePath)
                    ? Path.Combine(
                        Settings.Automation.AutoSavedStrokesLocation,
                        "Automation Saved - " + (currentMode == 0 ? "Annotation Strokes" : "BlackBoard Strokes"))
                    : Environment.ExpandEnvironmentVariables(savePath.Trim());

                Directory.CreateDirectory(targetDirectory);

                var fileName = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss-fff");
                if (currentMode != 0)
                    fileName += " Page-" + CurrentWhiteboardIndex;
                fileName += " StrokesCount-" + inkCanvas.Strokes.Count;

                var targetExtension = saveAsXml ? ".xml" : ".icstk";
                var targetPath = Path.Combine(targetDirectory, fileName + targetExtension);

                // 与常规保存保持一致：PPT 放映和多页白板都需要收集全部页面的笔画。
                var hasMultiplePages = false;
                var isPptMode = IsInPPTPresentationMode && _pptManager?.IsConnected == true;
                var allPageStrokes = new List<StrokeCollection>();

                if (isPptMode)
                {
                    hasMultiplePages = true;
                    var totalSlides = _pptManager.SlidesCount;
                    var currentSlide = _pptManager.GetCurrentSlideNumber();

                    for (int i = 1; i <= totalSlides; i++)
                    {
                        var slideStrokes = _singlePPTInkManager?.LoadSlideStrokes(i);
                        if (slideStrokes != null && slideStrokes.Count > 0)
                        {
                            allPageStrokes.Add(slideStrokes);
                        }
                        else if (i == currentSlide && inkCanvas.Strokes.Count > 0)
                        {
                            allPageStrokes.Add(inkCanvas.Strokes.Clone());
                        }
                        else
                        {
                            allPageStrokes.Add(new StrokeCollection());
                        }
                    }
                }
                else if (currentMode != 0 && WhiteboardTotalCount > 1)
                {
                    hasMultiplePages = true;
                    for (int i = 1; i <= WhiteboardTotalCount; i++)
                    {
                        var strokes = TimeMachineHistories[i] != null
                            ? ApplyHistoriesToNewStrokeCollection(TimeMachineHistories[i])
                            : new StrokeCollection();
                        allPageStrokes.Add(strokes);
                    }
                }

                if (hasMultiplePages && allPageStrokes.Count > 0)
                {
                    if (saveAsXml && !isPptMode)
                    {
                        // 白板 XML 多页格式沿用现有 ZIP 结构，确保可由对应恢复流程读取。
                        var zipPath = Path.ChangeExtension(targetPath, ".zip");
                        SaveMultiPageStrokesAsXMLZip(allPageStrokes, zipPath, false);
                        LogHelper.WriteLogToFile(
                            $"[Automation] 多页笔画已保存：{zipPath}（格式：XML ZIP，页面数：{allPageStrokes.Count}）",
                            LogHelper.LogType.Info);
                    }
                    else
                    {
                        // PPT 每页单独保存 XML；普通多页白板每页单独保存 ICSTK。
                        var baseName = Path.GetFileNameWithoutExtension(targetPath);
                        var savedCount = 0;
                        for (int i = 0; i < allPageStrokes.Count; i++)
                        {
                            var strokes = allPageStrokes[i];
                            if (strokes == null || strokes.Count == 0) continue;

                            var pagePath = Path.Combine(
                                targetDirectory,
                                $"{baseName}_Page-{i + 1}{targetExtension}");

                            if (saveAsXml)
                            {
                                SaveStrokesAsXML(strokes, pagePath, false);
                            }
                            else
                            {
                                using (var stream = new FileStream(pagePath, FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    strokes.Save(stream);
                                }
                            }

                            savedCount++;
                        }

                        LogHelper.WriteLogToFile(
                            $"[Automation] 多页笔画已保存：目录 {targetDirectory}（格式：{(saveAsXml ? "XML" : "ICSTK")}，页面数：{savedCount}）",
                            LogHelper.LogType.Info);
                    }
                }
                else
                {
                    if (saveAsXml)
                    {
                        SaveStrokesAsXML(inkCanvas.Strokes, targetPath);
                    }
                    else
                    {
                        using (var stream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            inkCanvas.Strokes.Save(stream);
                        }
                    }

                    LogHelper.WriteLogToFile(
                        $"[Automation] 笔画已保存：{targetPath}（格式：{(saveAsXml ? "XML" : "ICSTK")}，数量：{inkCanvas.Strokes.Count}）",
                        LogHelper.LogType.Info);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[Automation] 保存笔画失败：{ex}", LogHelper.LogType.Error);
                throw;
            }
        }
    }
}
