using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ink_Canvas.Helpers
{
    internal class SoftwareLauncher
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public static void LaunchEasiCamera(string softwareName)
        {
            string executablePath = FindEasiCameraExecutablePath(softwareName);

            if (!string.IsNullOrEmpty(executablePath))
            {
                try
                {
                    Process.Start(executablePath);
                    //Console.WriteLine(softwareName + " 启动成功！");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("启动失败: " + ex.Message);
                    //MessageBox.Show("启动失败: " + ex.Message);
                }
            }
            else
            {
                //Console.WriteLine(softwareName + " 未找到可执行文件路径。");
            }
        }

        private static string FindEasiCameraExecutablePath(string softwareName)
        {
            string executablePath = null;

            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
            {
                foreach (string subkeyName in key.GetSubKeyNames())
                {
                    using (RegistryKey subkey = key.OpenSubKey(subkeyName))
                    {
                        string displayName = subkey.GetValue("DisplayName") as string;
                        string installLocation = subkey.GetValue("InstallLocation") as string;
                        string uninstallString = subkey.GetValue("UninstallString") as string;

                        if (!string.IsNullOrEmpty(displayName) && displayName.Contains(softwareName))
                        {
                            if (!string.IsNullOrEmpty(installLocation))
                            {
                                executablePath = System.IO.Path.Combine(installLocation, "sweclauncher.exe");
                            }
                            else if (!string.IsNullOrEmpty(uninstallString))
                            {
                                int lastSlashIndex = uninstallString.LastIndexOf("\\");
                                if (lastSlashIndex >= 0)
                                {
                                    string folderPath = uninstallString.Substring(0, lastSlashIndex);
                                    executablePath = System.IO.Path.Combine(folderPath, "sweclauncher", "sweclauncher.exe");
                                }
                            }
                            break;
                        }
                    }
                }
            }

            return executablePath;
        }
    }
}
