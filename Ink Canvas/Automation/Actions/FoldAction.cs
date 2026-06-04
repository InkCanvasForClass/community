using System;
using System.Windows;
using Ink_Canvas.WorkflowAutomation.Models;

namespace Ink_Canvas.WorkflowAutomation.Actions
{
    /// <summary>
    /// 折叠/展开浮动栏行动的设置
    /// </summary>
    public class FoldActionSettings
    {
        /// <summary>
        /// true = 折叠，false = 展开
        /// </summary>
        public bool Fold { get; set; } = true;
    }

    /// <summary>
    /// 折叠/展开浮动栏的行动注册。
    /// </summary>
    public static class FoldAction
    {
        public const string ActionId = "inkcanvas.fold";

        public static ActionRegistryInfo Register()
        {
            var info = new ActionRegistryInfo(ActionId, "折叠/展开工具栏", "ArrowCollapse")
            {
                SettingsType = typeof(FoldActionSettings)
            };

            info.Handle = (settings, guid) =>
            {
                var s = settings as FoldActionSettings;
                if (s == null) return;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = System.Windows.Application.Current.MainWindow as MainWindow;
                    if (mw == null) return;

                    if (s.Fold && !mw.isFloatingBarFolded)
                    {
                        mw.FoldFloatingBar(null, true);
                    }
                });
            };

            info.RevertHandle = (settings, guid) =>
            {
                var s = settings as FoldActionSettings;
                if (s == null) return;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var mw = System.Windows.Application.Current.MainWindow as MainWindow;
                    if (mw == null) return;

                    if (!s.Fold && mw.isFloatingBarFolded)
                    {
                        // 展开浮动栏
                        mw.FoldFloatingBar(null, false);
                    }
                });
            };

            return info;
        }
    }
}
