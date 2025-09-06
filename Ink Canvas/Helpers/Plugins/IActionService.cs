using System;
using System.Windows.Media;

namespace Ink_Canvas.Helpers.Plugins
{
    /// <summary>
    /// 操作服务接口，统一所有执行操作相关的方法
    /// </summary>
    public interface IActionService
    {
        #region 画布操作

        /// <summary>
        /// 清除当前画布
        /// </summary>
        void ClearCanvas();

        /// <summary>
        /// 清除所有画布
        /// </summary>
        void ClearAllCanvases();

        /// <summary>
        /// 添加新页面
        /// </summary>
        void AddNewPage();

        /// <summary>
        /// 删除当前页面
        /// </summary>
        void DeleteCurrentPage();

        /// <summary>
        /// 切换到指定页面
        /// </summary>
        /// <param name="pageIndex">页面索引</param>
        void SwitchToPage(int pageIndex);

        /// <summary>
        /// 切换到下一页
        /// </summary>
        void NextPage();

        /// <summary>
        /// 切换到上一页
        /// </summary>
        void PreviousPage();

        #endregion

        #region 绘制操作

        /// <summary>
        /// 设置绘制模式
        /// </summary>
        /// <param name="mode">绘制模式</param>
        void SetDrawingMode(int mode);

        /// <summary>
        /// 设置笔触宽度
        /// </summary>
        /// <param name="width">宽度</param>
        void SetInkWidth(double width);

        /// <summary>
        /// 设置笔触颜色
        /// </summary>
        /// <param name="color">颜色</param>
        void SetInkColor(Color color);

        /// <summary>
        /// 设置高亮笔宽度
        /// </summary>
        /// <param name="width">宽度</param>
        void SetHighlighterWidth(double width);

        /// <summary>
        /// 设置橡皮擦大小
        /// </summary>
        /// <param name="size">大小</param>
        void SetEraserSize(int size);

        /// <summary>
        /// 设置橡皮擦类型
        /// </summary>
        /// <param name="type">类型</param>
        void SetEraserType(int type);

        /// <summary>
        /// 设置橡皮擦形状
        /// </summary>
        /// <param name="shape">形状</param>
        void SetEraserShape(int shape);

        /// <summary>
        /// 设置笔触透明度
        /// </summary>
        /// <param name="alpha">透明度</param>
        void SetInkAlpha(double alpha);

        /// <summary>
        /// 设置笔触样式
        /// </summary>
        /// <param name="style">样式</param>
        void SetInkStyle(int style);

        /// <summary>
        /// 设置背景颜色
        /// </summary>
        /// <param name="color">颜色</param>
        void SetBackgroundColor(string color);

        #endregion

        #region 文件操作

        /// <summary>
        /// 保存画布内容
        /// </summary>
        /// <param name="filePath">文件路径</param>
        void SaveCanvas(string filePath);

        /// <summary>
        /// 加载画布内容
        /// </summary>
        /// <param name="filePath">文件路径</param>
        void LoadCanvas(string filePath);

        /// <summary>
        /// 导出为图片
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="format">图片格式</param>
        void ExportAsImage(string filePath, string format);

        /// <summary>
        /// 导出为PDF
        /// </summary>
        /// <param name="filePath">文件路径</param>
        void ExportAsPDF(string filePath);

        #endregion

        #region 撤销重做操作

        /// <summary>
        /// 撤销操作
        /// </summary>
        void Undo();

        /// <summary>
        /// 重做操作
        /// </summary>
        void Redo();

        #endregion

        #region 选择操作

        /// <summary>
        /// 全选
        /// </summary>
        void SelectAll();

        /// <summary>
        /// 取消选择
        /// </summary>
        void DeselectAll();

        /// <summary>
        /// 删除选中内容
        /// </summary>
        void DeleteSelected();

        /// <summary>
        /// 复制选中内容
        /// </summary>
        void CopySelected();

        /// <summary>
        /// 剪切选中内容
        /// </summary>
        void CutSelected();

        /// <summary>
        /// 粘贴内容
        /// </summary>
        void Paste();

        #endregion

        #region 系统设置操作

        /// <summary>
        /// 设置系统设置
        /// </summary>
        /// <typeparam name="T">设置类型</typeparam>
        /// <param name="key">设置键</param>
        /// <param name="value">设置值</param>
        void SetSetting<T>(string key, T value);

        /// <summary>
        /// 保存设置到文件
        /// </summary>
        void SaveSettings();

        /// <summary>
        /// 从文件加载设置
        /// </summary>
        void LoadSettings();

        /// <summary>
        /// 重置设置为默认值
        /// </summary>
        void ResetSettings();

        #endregion

        #region 插件管理操作

        /// <summary>
        /// 启用插件
        /// </summary>
        /// <param name="pluginName">插件名称</param>
        void EnablePlugin(string pluginName);

        /// <summary>
        /// 禁用插件
        /// </summary>
        /// <param name="pluginName">插件名称</param>
        void DisablePlugin(string pluginName);

        /// <summary>
        /// 卸载插件
        /// </summary>
        /// <param name="pluginName">插件名称</param>
        void UnloadPlugin(string pluginName);

        #endregion

        #region 事件系统操作

        /// <summary>
        /// 注册事件处理器
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="handler">事件处理器</param>
        void RegisterEventHandler(string eventName, EventHandler handler);

        /// <summary>
        /// 注销事件处理器
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="handler">事件处理器</param>
        void UnregisterEventHandler(string eventName, EventHandler handler);

        /// <summary>
        /// 触发事件
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="sender">事件发送者</param>
        /// <param name="args">事件参数</param>
        void TriggerEvent(string eventName, object sender, EventArgs args);

        #endregion

        #region 应用程序操作

        /// <summary>
        /// 重启应用程序
        /// </summary>
        void RestartApplication();

        /// <summary>
        /// 退出应用程序
        /// </summary>
        void ExitApplication();

        /// <summary>
        /// 检查更新
        /// </summary>
        void CheckForUpdates();

        /// <summary>
        /// 打开帮助文档
        /// </summary>
        void OpenHelpDocument();

        /// <summary>
        /// 打开关于页面
        /// </summary>
        void OpenAboutPage();

        #endregion
    }
} 