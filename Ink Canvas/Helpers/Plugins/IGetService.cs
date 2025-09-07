using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ink_Canvas.Helpers.Plugins
{
    /// <summary>
    /// 获取服务接口，统一所有获取类的方法
    /// </summary>
    public interface IGetService
    {
        #region 窗口和UI获取

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

        #region 绘制工具状态获取

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

        #region 应用状态获取

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

        #region 撤销重做状态获取

        /// <summary>
        /// 获取是否可以撤销
        /// </summary>
        bool CanUndo { get; }

        /// <summary>
        /// 获取是否可以重做
        /// </summary>
        bool CanRedo { get; }

        #endregion

        #region 系统设置获取

        /// <summary>
        /// 获取系统设置
        /// </summary>
        /// <typeparam name="T">设置类型</typeparam>
        /// <param name="key">设置键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>设置值</returns>
        T GetSetting<T>(string key, T defaultValue = default(T));

        #endregion

        #region 插件信息获取

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

        #endregion
    }
} 