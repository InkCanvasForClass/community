using Ink_Canvas.Helpers;
using IWshRuntimeLibrary;
using System;
using System.Windows;

namespace Ink_Canvas {
    public partial class MainWindow : PerformanceTransparentWin {
        public static bool StartAutomaticallyCreate(string exeName) {
            try {
                var shell = new WshShell();
                var shortcut = (IWshShortcut)shell.CreateShortcut(
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup) + "\\" + exeName + ".lnk");
                //设置快捷方式的目标所在的位置(源程序完整路径)
                shortcut.TargetPath = System.Windows.Forms.Application.ExecutablePath;
                //应用程序的工作目录
                //当用户没有指定一个具体的目录时，快捷方式的目标应用程序将使用该属性所指定的目录来装载或保存文件。
                shortcut.WorkingDirectory = Environment.CurrentDirectory;
                //目标应用程序窗口类型(1.Normal window普通窗口,3.Maximized最大化窗口,7.Minimized最小化)
                shortcut.WindowStyle = 1;
                //快捷方式的描述
                shortcut.Description = exeName + "_Ink";
                //设置快捷键(如果有必要的话.)
                //shortcut.Hotkey = "CTRL+ALT+D";
                shortcut.Save();
                return true;
            }
            catch (Exception) { }

            return false;
        }

        public static bool StartAutomaticallyDel(string exeName) {
            try {
                System.IO.File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Startup) + "\\" + exeName +
                                      ".lnk");
                return true;
            }
            catch (Exception) { }

            return false;
        }
    }
}