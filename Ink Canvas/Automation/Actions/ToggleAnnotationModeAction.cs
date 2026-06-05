using Ink_Canvas.WorkflowAutomation.Models;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.WorkflowAutomation.Actions
{
    /// <summary>
    /// 切换批注模式行动的设置
    /// </summary>
    public class ToggleAnnotationModeActionSettings
    {
        /// <summary>
        /// true = 进入批注模式，false = 退出批注模式
        /// </summary>
        public bool EnterAnnotation { get; set; } = true;
    }

    /// <summary>
    /// 切换批注模式的行动注册。
    /// </summary>
    public static class ToggleAnnotationModeAction
    {
        public const string ActionId = "inkcanvas.toggleannotationmode";

        public static ActionRegistryInfo Register()
        {
            var info = new ActionRegistryInfo(ActionId, "切换批注模式", "PenTool")
            {
                SettingsType = typeof(ToggleAnnotationModeActionSettings)
            };

            info.Handle = (settings, guid) =>
            {
                var s = settings as ToggleAnnotationModeActionSettings;
                if (s == null) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    if (mw?.inkCanvas == null) return;

                    if (s.EnterAnnotation)
                    {
                        mw.inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    }
                    else
                    {
                        mw.inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    }
                });
            };

            info.RevertHandle = (settings, guid) =>
            {
                var s = settings as ToggleAnnotationModeActionSettings;
                if (s == null) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = Application.Current.MainWindow as MainWindow;
                    if (mw?.inkCanvas == null) return;

                    // 恢复：进入→退出，退出→进入
                    if (s.EnterAnnotation)
                    {
                        mw.inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    }
                    else
                    {
                        mw.inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    }
                });
            };

            return info;
        }
    }
}
