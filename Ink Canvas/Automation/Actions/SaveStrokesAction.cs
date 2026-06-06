using Ink_Canvas.WorkflowAutomation.Models;

namespace Ink_Canvas.WorkflowAutomation.Actions
{
    /// <summary>
    /// 保存笔画行动的设置
    /// </summary>
    public class SaveStrokesActionSettings
    {
        /// <summary>
        /// 保存路径（空则使用默认路径）
        /// </summary>
        public string SavePath { get; set; } = "";

        /// <summary>
        /// 是否保存为XML格式
        /// </summary>
        public bool SaveAsXml { get; set; } = false;
    }

    /// <summary>
    /// 保存笔画的行动注册。
    /// </summary>
    public static class SaveStrokesAction
    {
        public const string ActionId = "inkcanvas.savestrokes";

        public static ActionRegistryInfo Register()
        {
            var info = new ActionRegistryInfo(ActionId, "保存笔画", "ContentSaveOutline")
            {
                SettingsType = typeof(SaveStrokesActionSettings)
            };

            info.Handle = (settings, guid) =>
            {
                // 保存笔画功能暂未实现完整集成
                // TODO: 调用 MainWindow 的保存逻辑
            };

            // 保存笔画不支持恢复
            info.RevertHandle = null;

            return info;
        }
    }
}
