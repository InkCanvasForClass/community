namespace Ink_Canvas.WorkflowAutomation.Actions
{
    /// <summary>
    /// 杀进程行动的设置
    /// </summary>
    public class KillProcessActionSettings
    {
        /// <summary>
        /// 要杀死的进程名称（不含.exe）
        /// </summary>
        public string ProcessName { get; set; } = "";
    }
}
