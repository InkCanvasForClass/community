using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using System;
using System.IO;
using System.Windows;
using System.Windows.Ink;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        /// <summary>
        /// 保存自动化行动指定的笔画，不读取或修改全局保存格式设置。
        /// SavePath 可以是目录，也可以是带文件名的目标路径；为空时使用默认自动保存目录。
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
                var extension = saveAsXml ? ".xml" : ".icstk";
                string targetPath;

                if (string.IsNullOrWhiteSpace(savePath))
                {
                    var defaultDirectory = Path.Combine(
                        Settings.Automation.AutoSavedStrokesLocation,
                        "Automation Saved - " + (currentMode == 0 ? "Annotation Strokes" : "BlackBoard Strokes"));
                    Directory.CreateDirectory(defaultDirectory);

                    var fileName = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss-fff");
                    if (currentMode != 0)
                        fileName += " Page-" + CurrentWhiteboardIndex;
                    fileName += " StrokesCount-" + inkCanvas.Strokes.Count;

                    targetPath = Path.Combine(defaultDirectory, fileName + extension);
                }
                else if (Path.HasExtension(savePath))
                {
                    var directory = Path.GetDirectoryName(savePath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);
                    targetPath = Path.ChangeExtension(savePath, extension);
                }
                else
                {
                    Directory.CreateDirectory(savePath);
                    var fileName = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss-fff");
                    if (currentMode != 0)
                        fileName += " Page-" + CurrentWhiteboardIndex;
                    fileName += " StrokesCount-" + inkCanvas.Strokes.Count;
                    targetPath = Path.Combine(savePath, fileName + extension);
                }

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
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[Automation] 保存笔画失败：{ex}", LogHelper.LogType.Error);
                throw;
            }
        }
    }
}
