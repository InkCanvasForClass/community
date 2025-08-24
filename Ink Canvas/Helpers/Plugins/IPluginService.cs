using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;

namespace Ink_Canvas.Helpers.Plugins
{
    /// <summary>
    /// 插件服务接口，提供对软件内部功能的访问
    /// </summary>
    public interface IPluginService
    {
        #region 窗口和UI访问

        /// <summary>
        /// 获取主窗口引用
        /// </summary>
        Window MainWindow { get; }

        /// <summary>
        /// 获取当前画布
        /// </summary>
        InkCanvas CurrentCanvas { get; }

        /// <summary>
        /// 获取所有画布页面
        /// </summary>
        List<Canvas> AllCanvasPages { get; }

        /// <summary>
        /// 获取当前页面索引
        /// </summary>
        int CurrentPageIndex { get; }

        /// <summary>
        /// 获取当前页面数量
        /// </summary>
        int TotalPageCount { get; }

        /// <summary>
        /// 获取浮动工具栏
        /// </summary>
        FrameworkElement FloatingToolBar { get; }

        /// <summary>
        /// 获取左侧面板
        /// </summary>
        FrameworkElement LeftPanel { get; }

        /// <summary>
        /// 获取右侧面板
        /// </summary>
        FrameworkElement RightPanel { get; }

        /// <summary>
        /// 获取顶部面板
        /// </summary>
        FrameworkElement TopPanel { get; }

        /// <summary>
        /// 获取底部面板
        /// </summary>
        FrameworkElement BottomPanel { get; }

        #endregion

        #region 绘制工具状态

        /// <summary>
        /// 获取当前绘制模式
        /// </summary>
        int CurrentDrawingMode { get; }

        /// <summary>
        /// 获取当前笔触宽度
        /// </summary>
        double CurrentInkWidth { get; }

        /// <summary>
        /// 获取当前笔触颜色
        /// </summary>
        Color CurrentInkColor { get; }

        /// <summary>
        /// 获取当前高亮笔宽度
        /// </summary>
        double CurrentHighlighterWidth { get; }

        /// <summary>
        /// 获取当前橡皮擦大小
        /// </summary>
        int CurrentEraserSize { get; }

        /// <summary>
        /// 获取当前橡皮擦类型
        /// </summary>
        int CurrentEraserType { get; }

        /// <summary>
        /// 获取当前橡皮擦形状
        /// </summary>
        int CurrentEraserShape { get; }

        /// <summary>
        /// 获取当前笔触透明度
        /// </summary>
        double CurrentInkAlpha { get; }

        /// <summary>
        /// 获取当前笔触样式
        /// </summary>
        int CurrentInkStyle { get; }

        /// <summary>
        /// 获取当前背景颜色
        /// </summary>
        string CurrentBackgroundColor { get; }

        #endregion

        #region 应用状态

        /// <summary>
        /// 获取当前主题模式
        /// </summary>
        bool IsDarkTheme { get; }

        /// <summary>
        /// 获取当前是否为白板模式
        /// </summary>
        bool IsWhiteboardMode { get; }

        /// <summary>
        /// 获取当前是否为PPT模式
        /// </summary>
        bool IsPPTMode { get; }

        /// <summary>
        /// 获取当前是否为全屏模式
        /// </summary>
        bool IsFullScreenMode { get; }

        /// <summary>
        /// 获取当前是否为画板模式
        /// </summary>
        bool IsCanvasMode { get; }

        /// <summary>
        /// 获取当前是否为选择模式
        /// </summary>
        bool IsSelectionMode { get; }

        /// <summary>
        /// 获取当前是否为擦除模式
        /// </summary>
        bool IsEraserMode { get; }

        /// <summary>
        /// 获取当前是否为形状绘制模式
        /// </summary>
        bool IsShapeDrawingMode { get; }

        /// <summary>
        /// 获取当前是否为高亮模式
        /// </summary>
        bool IsHighlighterMode { get; }

        #endregion

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

        #region 撤销重做

        /// <summary>
        /// 撤销操作
        /// </summary>
        void Undo();

        /// <summary>
        /// 重做操作
        /// </summary>
        void Redo();

        /// <summary>
        /// 是否可以撤销
        /// </summary>
        bool CanUndo { get; }

        /// <summary>
        /// 是否可以重做
        /// </summary>
        bool CanRedo { get; }

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

        #region 窗口管理

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

        #region 通知和消息

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

        #region 系统功能

        /// <summary>
        /// 获取系统设置
        /// </summary>
        /// <typeparam name="T">设置类型</typeparam>
        /// <param name="key">设置键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>设置值</returns>
        T GetSetting<T>(string key, T defaultValue = default(T));

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

        #region 插件管理

        /// <summary>
        /// 获取所有已加载的插件
        /// </summary>
        /// <returns>插件列表</returns>
        List<IPlugin> GetAllPlugins();

        /// <summary>
        /// 获取指定插件
        /// </summary>
        /// <param name="pluginName">插件名称</param>
        /// <returns>插件实例</returns>
        IPlugin GetPlugin(string pluginName);

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

        #region 事件系统

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
    }

    /// <summary>
    /// 通知类型枚举
    /// </summary>
    public enum NotificationType
    {
        /// <summary>
        /// 信息
        /// </summary>
        Info,

        /// <summary>
        /// 成功
        /// </summary>
        Success,

        /// <summary>
        /// 警告
        /// </summary>
        Warning,

        /// <summary>
        /// 错误
        /// </summary>
        Error
    }
} 