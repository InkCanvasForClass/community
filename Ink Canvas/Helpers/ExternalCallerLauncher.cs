using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ink_Canvas.Helpers
{
    public static class ExternalCallerLauncher
    {
        // IslandCaller 外部调用 URL 列表（按优先级依次尝试）。
        // v2.0+ 通过 IUriNavigationService.HandlePluginsNavigation 注册：
        //   • "IslandCaller/Simple"        → ShowRandomStudent(1)（快抽 1 人）
        //   • "IslandCaller/Advanced/GUI"  → 打开 PersonalCall 高级点名窗口
        // 旧版仍使用 "IslandCaller/Run"，因此保留作为兼容回退。
        // 插件按约定的协议形式分发。
        private static readonly string[] ClassIslandProtocols =
        {
            // 新版（IslandCaller 2.0.0+）：单次抽取 1 人
            "classisland://plugins/IslandCaller/Simple/1",
            // 新版（IslandCaller 2.0.0+）：不带人数段，plugin 内部默认抽 1 人
            "classisland://plugins/IslandCaller/Simple",
            // 新版（IslandCaller 2.0.0+）：打开高级点名窗口（GUI 选择人数）
            "classisland://plugins/IslandCaller/Advanced/GUI",
            // 旧版兼容回退
            "classisland://plugins/IslandCaller/Run"
        };

        public static string[] GetProtocolsByType(int externalCallerType)
        {
            switch (externalCallerType)
            {
                case 0:
                    return ClassIslandProtocols;
                case 1:
                    return new[]
                    {
                        "secrandom://roll_call/quick_draw",
                        "secrandom://direct_extraction"
                    };
                case 2:
                    return new[] { "namepicker://" };
                default:
                    return ClassIslandProtocols;
            }
        }

        public static string[] GetProtocolsByName(string externalCallerName)
        {
            switch (externalCallerName)
            {
                case "ClassIsland":
                    return ClassIslandProtocols;
                case "SecRandom":
                    return new[]
                    {
                        "secrandom://roll_call/quick_draw",
                        "secrandom://direct_extraction"
                    };
                case "NamePicker":
                    return new[] { "namepicker://" };
                default:
                    return ClassIslandProtocols;
            }
        }

        public static bool TryLaunch(IEnumerable<string> protocols, out Exception lastException)
        {
            lastException = null;
            if (protocols == null) return false;

            foreach (var protocol in protocols)
            {
                if (string.IsNullOrWhiteSpace(protocol)) continue;

                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = protocol,
                        UseShellExecute = true
                    });
                    return true;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }
            }

            return false;
        }
    }
}
