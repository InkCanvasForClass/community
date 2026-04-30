using IWshRuntimeLibrary;
using System;
using File = System.IO.File;

namespace Ink_Canvas.Windows.SettingsViews.Helpers
{
    public static class AutoStartHelper
    {
        public static bool StartAutomaticallyCreate(string exeName)
        {
            try
            {
                var shell = new WshShell();
                var shortcut = (IWshShortcut)shell.CreateShortcut(
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup) + "\\" + exeName + ".lnk");
                shortcut.TargetPath = System.Windows.Forms.Application.ExecutablePath;
                shortcut.WorkingDirectory = Environment.CurrentDirectory;
                shortcut.WindowStyle = 1;
                shortcut.Description = exeName + "_Ink";
                shortcut.Save();
                return true;
            }
            catch (Exception) { }

            return false;
        }

        public static bool StartAutomaticallyDel(string exeName)
        {
            try
            {
                File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Startup) + "\\" + exeName +
                            ".lnk");
                return true;
            }
            catch (Exception) { }

            return false;
        }

        public static bool IsAutoStartEnabled(string exeName)
        {
            return File.Exists(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup) + "\\" + exeName + ".lnk");
        }
    }
}
