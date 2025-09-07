namespace Ink_Canvas.Helpers.Plugins
{
    /// <summary>
    /// 窗口服务接口，统一所有窗口操作相关的方法
    /// </summary>
    public interface IWindowService
    {
        #region 窗口显示和隐藏

        /// <summary>
        /// 显示设置窗口
        /// </summary>
        void ShowSettingsWindow();

        /// <summary>
        /// 隐藏设置窗口
        /// </summary>
        void HideSettingsWindow();

        /// <summary>
        /// 显示插件设置窗口
        /// </summary>
        void ShowPluginSettingsWindow();

        /// <summary>
        /// 隐藏插件设置窗口
        /// </summary>
        void HidePluginSettingsWindow();

        /// <summary>
        /// 显示帮助窗口
        /// </summary>
        void ShowHelpWindow();

        /// <summary>
        /// 隐藏帮助窗口
        /// </summary>
        void HideHelpWindow();

        /// <summary>
        /// 显示关于窗口
        /// </summary>
        void ShowAboutWindow();

        /// <summary>
        /// 隐藏关于窗口
        /// </summary>
        void HideAboutWindow();

        #endregion

        #region 对话框和通知

        /// <summary>
        /// 显示通知消息
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="type">消息类型</param>
        void ShowNotification(string message, NotificationType type = NotificationType.Info);

        /// <summary>
        /// 显示确认对话框
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题</param>
        /// <returns>用户选择结果</returns>
        bool ShowConfirmDialog(string message, string title = "确认");

        /// <summary>
        /// 显示输入对话框
        /// </summary>
        /// <param name="message">提示消息</param>
        /// <param name="title">标题</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>用户输入内容</returns>
        string ShowInputDialog(string message, string title = "输入", string defaultValue = "");

        #endregion

        #region 窗口状态控制

        /// <summary>
        /// 设置窗口全屏状态
        /// </summary>
        /// <param name="isFullScreen">是否全屏</param>
        void SetFullScreen(bool isFullScreen);

        /// <summary>
        /// 设置窗口置顶状态
        /// </summary>
        /// <param name="isTopMost">是否置顶</param>
        void SetTopMost(bool isTopMost);

        /// <summary>
        /// 设置窗口可见性
        /// </summary>
        /// <param name="isVisible">是否可见</param>
        void SetWindowVisibility(bool isVisible);

        /// <summary>
        /// 最小化窗口
        /// </summary>
        void MinimizeWindow();

        /// <summary>
        /// 最大化窗口
        /// </summary>
        void MaximizeWindow();

        /// <summary>
        /// 恢复窗口
        /// </summary>
        void RestoreWindow();

        /// <summary>
        /// 关闭窗口
        /// </summary>
        void CloseWindow();

        #endregion

        #region 窗口位置和大小

        /// <summary>
        /// 设置窗口位置
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        void SetWindowPosition(double x, double y);

        /// <summary>
        /// 设置窗口大小
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        void SetWindowSize(double width, double height);

        /// <summary>
        /// 获取窗口位置
        /// </summary>
        /// <returns>窗口位置</returns>
        (double x, double y) GetWindowPosition();

        /// <summary>
        /// 获取窗口大小
        /// </summary>
        /// <returns>窗口大小</returns>
        (double width, double height) GetWindowSize();

        #endregion
    }
} 