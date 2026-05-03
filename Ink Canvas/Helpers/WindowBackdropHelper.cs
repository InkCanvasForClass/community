using iNKORE.UI.WPF.Modern.Helpers.Styles;
using System;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace Ink_Canvas.Helpers
{
    internal static class WindowBackdropHelper
    {
        public static void Apply(Window window, Settings settings = null)
        {
            if (window == null) return;

            var backdropName = settings?.Appearance?.WindowBackdrop
                ?? MainWindow.Settings?.Appearance?.WindowBackdrop
                ?? "None";

            Apply(window, backdropName);
        }

        public static void Apply(Window window, string backdropName)
        {
            if (window == null) return;

            try
            {
                BackdropHelper.Remove(window);
                Acrylic10Helper.Remove(window);

                var normalizedName = string.IsNullOrWhiteSpace(backdropName) ? "None" : backdropName;
                if (!Enum.TryParse(normalizedName, true, out BackdropType backdropType))
                {
                    backdropType = BackdropType.None;
                }

                if (TrySetWindowHelperBackdrop(window, backdropType))
                {
                    return;
                }

                if (backdropType == BackdropType.None)
                {
                    return;
                }

                if (string.Equals(normalizedName, "Acrylic10", StringComparison.OrdinalIgnoreCase))
                {
                    Acrylic10Helper.Apply(window, true);
                    return;
                }

                BackdropHelper.Apply(window, backdropType, true);
            }
            catch
            {
                // Unsupported systems simply fall back to the normal window background.
            }
        }

        private static bool TrySetWindowHelperBackdrop(Window window, BackdropType backdropType)
        {
            try
            {
                var windowHelperType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("iNKORE.UI.WPF.Modern.Controls.Helpers.WindowHelper", false))
                    .FirstOrDefault(type => type != null);

                if (windowHelperType == null)
                {
                    return false;
                }

                var method = windowHelperType.GetMethod(
                    "SetSystemBackdropType",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(Window), typeof(BackdropType) },
                    null);

                if (method == null)
                {
                    return false;
                }

                method.Invoke(null, new object[] { window, backdropType });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
