using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.PopupDiagnostics
{
    /// <summary>
    /// 菜单弹出动画的诊断工具入口。
    /// </summary>
    /// <remarks>
    /// 与 InkCanvas.NativeInk.Tests 的分工：
    ///   - NativeInk.Tests 跑纯数学、不碰 WPF，适合无人值守与 CI；
    ///   - 本工具把动画代码放进真实 WPF 管线，用真窗口、真 Popup 核对几何行为，
    ///     适合改完动画后手动跑一次，或排查"第二次打开原点歪了""过冲被裁了"这类问题。
    ///
    /// 用法：
    ///   dotnet run --project InkCanvas.PopupDiagnostics
    ///   退出码 0 表示全部检查通过。
    /// </remarks>
    internal static class Program
    {
        /// <summary>把控制台挂到父进程上，双击运行或从 IDE 启动时也能看到输出。</summary>
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);

        private const int AttachParentProcess = -1;

        [STAThread]
        private static int Main(string[] args)
        {
            AttachConsole(AttachParentProcess);
            Console.WriteLine("InkCanvas 菜单弹出动画诊断");

            var exitCode = 1;
            var window = new Window
            {
                Title = "InkCanvas 菜单弹出动画诊断",
                Width = 900,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new Grid()
            };

            window.Loaded += async (sender, e) =>
            {
                try
                {
                    exitCode = await RunAsync(window);
                }
                catch (Exception exception)
                {
                    Console.WriteLine("诊断过程中抛出异常：" + exception);
                    exitCode = 2;
                }
                finally
                {
                    window.Close();
                }
            };

            var app = new Application();
            app.Run(window);
            return exitCode;
        }

        private static async Task<int> RunAsync(Window window)
        {
            var curveReport = PopupCurveReport.Run();
            var geometryReport = await PopupGeometryReport.RunAsync(window);

            Console.WriteLine();
            var curveExit = curveReport.Summarize("曲线");
            var geometryExit = geometryReport.Summarize("几何");

            var passed = curveExit == 0 && geometryExit == 0;
            Console.WriteLine(passed
                ? "结论：菜单弹出动画的曲线与几何均符合预期。"
                : "结论：存在不符合预期的检查项，见上方 FAIL 行。");

            return passed ? 0 : 1;
        }
    }
}
