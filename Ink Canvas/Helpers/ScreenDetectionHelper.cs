using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Point = System.Windows.Point;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 屏幕检测帮助类 - 用于检测窗口所在的屏幕和屏幕信息
    /// </summary>
    public static class ScreenDetectionHelper
    {
        /// <summary>
        /// 获取窗口所在的屏幕
        /// </summary>
        /// <param name="window">要检测的窗口</param>
        /// <returns>窗口所在的屏幕，如果无法检测则返回主屏幕</returns>
        public static Screen GetWindowScreen(Window window)
        {
            try
            {
                if (window == null)
                    return Screen.PrimaryScreen;

                // 获取窗口的句柄
                var hwndSource = PresentationSource.FromVisual(window) as HwndSource;
                if (hwndSource == null)
                    return Screen.PrimaryScreen;

                // 获取窗口在屏幕上的位置
                var windowRect = GetWindowScreenBounds(window);
                
                // 查找与窗口重叠最多的屏幕
                Screen targetScreen = null;
                double maxIntersection = 0;

                foreach (var screen in Screen.AllScreens)
                {
                    var intersection = Rectangle.Intersect(windowRect, screen.Bounds);
                    if (intersection.Width * intersection.Height > maxIntersection)
                    {
                        maxIntersection = intersection.Width * intersection.Height;
                        targetScreen = screen;
                    }
                }

                return targetScreen ?? Screen.PrimaryScreen;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检测窗口屏幕时出错: {ex.Message}", LogHelper.LogType.Warning);
                return Screen.PrimaryScreen;
            }
        }

        /// <summary>
        /// 获取窗口在屏幕坐标系中的边界
        /// </summary>
        /// <param name="window">要检测的窗口</param>
        /// <returns>窗口的屏幕边界</returns>
        private static Rectangle GetWindowScreenBounds(Window window)
        {
            try
            {
                // 获取窗口左上角在屏幕上的位置
                var topLeft = window.PointToScreen(new Point(0, 0));
                
                // 获取窗口右下角在屏幕上的位置
                var bottomRight = window.PointToScreen(new Point(window.ActualWidth, window.ActualHeight));
                
                return new Rectangle(
                    (int)topLeft.X,
                    (int)topLeft.Y,
                    (int)(bottomRight.X - topLeft.X),
                    (int)(bottomRight.Y - topLeft.Y));
            }
            catch
            {
                // 如果无法获取精确位置，返回窗口的Left和Top
                return new Rectangle(
                    (int)window.Left,
                    (int)window.Top,
                    (int)window.Width,
                    (int)window.Height);
            }
        }

        /// <summary>
        /// 检查是否有多个屏幕
        /// </summary>
        /// <returns>如果有多个屏幕返回true，否则返回false</returns>
        public static bool HasMultipleScreens()
        {
            try
            {
                return Screen.AllScreens.Length > 1;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取主屏幕
        /// </summary>
        /// <returns>主屏幕</returns>
        public static Screen GetPrimaryScreen()
        {
            try
            {
                return Screen.PrimaryScreen;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取所有屏幕信息
        /// </summary>
        /// <returns>所有屏幕的数组</returns>
        public static Screen[] GetAllScreens()
        {
            try
            {
                return Screen.AllScreens;
            }
            catch
            {
                return new Screen[] { Screen.PrimaryScreen };
            }
        }

        /// <summary>
        /// 检查窗口是否在主屏幕上
        /// </summary>
        /// <param name="window">要检查的窗口</param>
        /// <returns>如果窗口在主屏幕上返回true，否则返回false</returns>
        public static bool IsWindowOnPrimaryScreen(Window window)
        {
            try
            {
                var windowScreen = GetWindowScreen(window);
                return windowScreen == Screen.PrimaryScreen;
            }
            catch
            {
                return true; // 出错时假设在主屏幕
            }
        }
    }
}