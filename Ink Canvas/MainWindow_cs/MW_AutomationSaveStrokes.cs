using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using System;
using System.IO;

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
                var extension = saveAsXml ? ".xml" : ".icstk";
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

                var targetPath = Path.Combine(targetDirectory, fileName + extension);
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
