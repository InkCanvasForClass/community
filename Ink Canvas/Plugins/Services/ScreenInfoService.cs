using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="IScreenInfoService"/> 的宿主实现：包装 <see cref="Ink_Canvas.Helpers.ScreenDetectionHelper"/>，
    /// 把 System.Windows.Forms.Screen 映射为 <see cref="PluginScreenInfo"/>。
    /// </summary>
    internal sealed class ScreenInfoService : IScreenInfoService
    {
        public IReadOnlyList<PluginScreenInfo> GetAllScreens()
        {
            try
            {
                return Ink_Canvas.Helpers.ScreenDetectionHelper.GetAllScreens()
                    .Select(Map)
                    .ToList();
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"ScreenInfoService.GetAllScreens failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return Array.Empty<PluginScreenInfo>();
            }
        }

        public PluginScreenInfo GetPrimaryScreen()
        {
            try
            {
                var screen = Ink_Canvas.Helpers.ScreenDetectionHelper.GetPrimaryScreen();
                return screen == null ? null : Map(screen);
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"ScreenInfoService.GetPrimaryScreen failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return null;
            }
        }

        public bool HasMultipleScreens()
        {
            try
            {
                return Ink_Canvas.Helpers.ScreenDetectionHelper.HasMultipleScreens();
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"ScreenInfoService.HasMultipleScreens failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return false;
            }
        }

        private static PluginScreenInfo Map(System.Windows.Forms.Screen screen)
        {
            return new PluginScreenInfo
            {
                Bounds = ToRect(screen.Bounds),
                WorkingArea = ToRect(screen.WorkingArea),
                DeviceName = screen.DeviceName ?? "",
                IsPrimary = screen.Primary,
            };
        }

        private static Rect ToRect(System.Drawing.Rectangle r)
        {
            return new Rect(r.X, r.Y, r.Width, r.Height);
        }
    }
}
