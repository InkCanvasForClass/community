using System;
using System.Windows;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="ITrayService"/> 的宿主实现：托盘图标/菜单操作落在 App（MW_TrayIcon），
    /// 本类负责线程切换（所有方法可从任意线程调用）与事件转发。
    /// </summary>
    internal sealed class TrayService : ITrayService
    {
        private readonly App _app;

        public TrayService(App app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _app.PluginTrayLeftClicked += () => LeftClicked?.Invoke();
            _app.PluginTrayRightClicked += () => RightClicked?.Invoke();
        }

        public event Action LeftClicked;
        public event Action RightClicked;

        private void RunOnUi(Action action)
        {
            if (_app.Dispatcher.CheckAccess()) action();
            else _app.Dispatcher.Invoke(action);
        }

        private T RunOnUi<T>(Func<T> func)
            => _app.Dispatcher.CheckAccess() ? func() : _app.Dispatcher.Invoke(func);

        public bool IsIconVisible
        {
            get => RunOnUi(() => _app.GetPluginTaskbarIcon()?.Visibility == Visibility.Visible);
            set => RunOnUi(() => _app.SetPluginTrayIconVisibility(value));
        }

        public bool IsMainWindowVisible
        {
            get => RunOnUi(() => (_app.MainWindow as MainWindow)?.IsVisible == true);
            set
            {
                RunOnUi(() =>
                {
                    if (value) _app.ShowPluginMainWindow();
                    else _app.HidePluginMainWindow();
                });
            }
        }

        public void ShowContextMenu() => RunOnUi(() => _app.ShowTrayContextMenu());

        public bool AddMenuItem(string id, string text, Action onClicked)
            => RunOnUi(() => _app.AddPluginTrayMenuItem(id, text, onClicked));

        public bool RemoveMenuItem(string id)
            => RunOnUi(() => _app.RemovePluginTrayMenuItem(id));

        public bool HasMenuItem(string id)
            => RunOnUi(() => _app.HasPluginTrayMenuItem(id));
    }
}
