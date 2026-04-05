using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ink_Canvas.Helpers
{
    public static class ExternalCallerLauncher
    {
        private static readonly string[] ClassIslandProtocols =
        {
            "classisland://plugins/IslandCaller/Simple/1",
            "classisland://plugins/IslandCaller/Simple",
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
