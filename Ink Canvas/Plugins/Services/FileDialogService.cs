using Microsoft.Win32;
using System;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="IFileDialogService"/> 的宿主实现：在 UI 线程弹出标准 WPF 打开/保存对话框。
    /// </summary>
    internal sealed class FileDialogService : IFileDialogService
    {
        private readonly MainWindow _mainWindow;

        public FileDialogService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public string OpenFile(string title, string filter = null, string initialDirectory = null)
        {
            return RunOnUi(() =>
            {
                var dialog = new OpenFileDialog
                {
                    Title = title ?? "",
                    Filter = filter ?? "所有文件 (*.*)|*.*",
                    InitialDirectory = initialDirectory,
                    Multiselect = false,
                };
                return dialog.ShowDialog() == true ? dialog.FileName : null;
            });
        }

        public string[] OpenFiles(string title, string filter = null, string initialDirectory = null)
        {
            return RunOnUi(() =>
            {
                var dialog = new OpenFileDialog
                {
                    Title = title ?? "",
                    Filter = filter ?? "所有文件 (*.*)|*.*",
                    InitialDirectory = initialDirectory,
                    Multiselect = true,
                };
                return dialog.ShowDialog() == true ? dialog.FileNames : Array.Empty<string>();
            });
        }

        public string SaveFile(string title, string filter = null, string defaultFileName = null, string initialDirectory = null)
        {
            return RunOnUi(() =>
            {
                var dialog = new SaveFileDialog
                {
                    Title = title ?? "",
                    Filter = filter ?? "所有文件 (*.*)|*.*",
                    FileName = defaultFileName ?? "",
                    InitialDirectory = initialDirectory,
                };
                return dialog.ShowDialog() == true ? dialog.FileName : null;
            });
        }

        private T RunOnUi<T>(Func<T> func)
        {
            if (_mainWindow.Dispatcher.CheckAccess()) return func();
            return _mainWindow.Dispatcher.Invoke(func);
        }
    }
}
