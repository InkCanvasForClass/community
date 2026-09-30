using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Helpers.Styles;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Ink_Canvas.Helpers
{
    internal static class WindowBackdropHelper
    {
        /// <summary>
        /// 记录每个窗口的背景应用状态与首次显示后重应用所需的回调，避免重复挂载 Loaded 处理器。
        /// </summary>
        private static readonly ConditionalWeakTable<Window, BackdropRequest> Requests =
            new ConditionalWeakTable<Window, BackdropRequest>();

        private sealed class BackdropRequest
        {
            public BackdropType Type;
            public bool Hooked;
            public bool ReappliedAfterShow;
        }

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
                var normalizedName = string.IsNullOrWhiteSpace(backdropName) ? "None" : backdropName;
                if (!Enum.TryParse(normalizedName, true, out BackdropType backdropType))
                {
                    backdropType = BackdropType.None;
                }

                // 目标效果与窗口当前效果一致时必须走这条分支，不能再执行下面的“先移除再设置”：
                // BackdropHelper.Remove 会清掉 DWM 系统背景并移除窗口深色模式，而随后的
                // SetSystemBackdropType 传入相同值不会触发库内部的属性变更回调，于是窗口会
                // 停留在“背景已被移除”的状态。Windows 11 上系统背景生效时窗口自身背景是透明的，
                // 此时整页会直接透出桌面与 DWM 回退色，表现为发白；Win10 没有系统背景所以不复现。
                if (TryGetWindowHelperBackdropType(window, out BackdropType currentType) &&
                    currentType == backdropType)
                {
                    ApplyNativeBackdrop(window, backdropType);
                    return;
                }

                BackdropHelper.Remove(window);
                Acrylic10Helper.Remove(window);

                if (TrySetWindowHelperBackdrop(window, backdropType))
                {
                    // 属性值发生变化，库内部已完成样式重建、深色模式同步与 DWM 背景应用。
                    AttachReapplyAfterShow(window, backdropType);
                    return;
                }

                if (backdropType == BackdropType.None)
                {
                    SyncWindowDarkMode(window);
                    return;
                }

                if (string.Equals(normalizedName, "Acrylic10", StringComparison.OrdinalIgnoreCase))
                {
                    Acrylic10Helper.Apply(window, true);
                }
                else
                {
                    BackdropHelper.Apply(window, backdropType, true);
                }

                AttachReapplyAfterShow(window, backdropType);
            }
            catch
            {
                // Unsupported systems simply fall back to the normal window background.
            }
        }

        /// <summary>
        /// 只刷新 DWM 侧的系统背景与深色模式，不重建窗口样式。
        /// </summary>
        private static void ApplyNativeBackdrop(Window window, BackdropType backdropType)
        {
            try
            {
                if (backdropType.IsSupported())
                {
                    BackdropHelper.Apply(window, backdropType, true);
                }

                SyncWindowDarkMode(window);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 让窗口的 DWM 深色模式与当前实际主题保持一致（BackdropHelper.Remove 会把深色模式一并清除）。
        /// </summary>
        private static void SyncWindowDarkMode(Window window)
        {
            try
            {
                var theme = ThemeManager.GetActualTheme(window);
                bool isDark = theme == ElementTheme.Dark ||
                    (theme == ElementTheme.Default && ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Dark);

                if (isDark)
                {
                    BackdropHelper.ApplyDarkMode(window);
                }
                else
                {
                    BackdropHelper.RemoveDarkMode(window);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 窗口显示完成后再应用一次系统背景：构造阶段窗口尚未显示时设置的 DWM 系统背景
        /// 在部分系统上不会被采纳，首帧会退回纯色回退背景，需要窗口可见后重新应用。
        /// </summary>
        private static void AttachReapplyAfterShow(Window window, BackdropType backdropType)
        {
            try
            {
                var request = Requests.GetValue(window, _ => new BackdropRequest());
                request.Type = backdropType;

                if (request.Hooked || request.ReappliedAfterShow || window.IsLoaded) return;
                request.Hooked = true;

                RoutedEventHandler onLoaded = null;
                onLoaded = (sender, e) =>
                {
                    window.Loaded -= onLoaded;
                    request.ReappliedAfterShow = true;

                    if (!window.IsVisible) return;

                    ApplyNativeBackdrop(window, request.Type);
                };
                window.Loaded += onLoaded;
            }
            catch
            {
            }
        }

        private static bool TrySetWindowHelperBackdrop(Window window, BackdropType backdropType)
        {
            try
            {
                var method = FindWindowHelperMethod(
                    "SetSystemBackdropType",
                    new[] { typeof(Window), typeof(BackdropType) });

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

        private static bool TryGetWindowHelperBackdropType(Window window, out BackdropType backdropType)
        {
            backdropType = BackdropType.None;

            try
            {
                var method = FindWindowHelperMethod("GetSystemBackdropType", new[] { typeof(Window) });
                if (method == null)
                {
                    return false;
                }

                if (method.Invoke(null, new object[] { window }) is BackdropType value)
                {
                    backdropType = value;
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static MethodInfo FindWindowHelperMethod(string methodName, Type[] parameterTypes)
        {
            var windowHelperType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("iNKORE.UI.WPF.Modern.Controls.Helpers.WindowHelper", false))
                .FirstOrDefault(type => type != null);

            return windowHelperType?.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                parameterTypes,
                null);
        }
    }
}
