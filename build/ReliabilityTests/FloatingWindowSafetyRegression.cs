using Ink_Canvas.Helpers;

internal static class FloatingWindowSafetyRegression
{
    internal static void Run(Action<bool, string> check)
    {
        const uint floatingStyle = 0x16080000;
        foreach (uint dpi in new uint[] { 96, 120, 144, 168, 192, 240, 288, 384 })
        {
            int width = (int)Math.Round(550 * dpi / 96.0), height = (int)Math.Round(200 * dpi / 96.0);
            check(FloatingWindowSafetyPolicy.IsSafeSeewoTarget(floatingStyle, 0, "", width, height, dpi, 7680, 4320),
                $"Known floating bar rejected at {dpi} DPI");
            check(FloatingWindowSafetyPolicy.IsSafeSeewoTarget(floatingStyle, 0, "", width + 2, height - 2, dpi, 7680, 4320),
                "DPI rounding tolerance lost");
            check(!FloatingWindowSafetyPolicy.IsSafeSeewoTarget(floatingStyle, 0, "", width + 10, height + 10, dpi, 7680, 4320),
                "Unknown floating size accepted by aspect ratio");
        }
        foreach (uint extra in new uint[] { 0x40000000, 0x00C00000, 0x00040000, 0x01000000, 0x20000000, 0x08000000 })
            check(!FloatingWindowSafetyPolicy.IsSafeSeewoTarget(floatingStyle | extra, 0, "", 550, 200, 96, 1920, 1080),
                "Child, captioned, resizable, maximized or minimized teaching window accepted");
        check(!FloatingWindowSafetyPolicy.IsSafeSeewoTarget(floatingStyle, 0x00040000, "", 550, 200, 96, 1920, 1080),
            "Taskbar application window accepted");
        foreach (string title in new[] { "希沃白板", "Note", "课程 - 希沃白板5", "设置", "Login", "手机传图", "图片上传", "扫码上传" })
            check(!FloatingWindowSafetyPolicy.IsSafeSeewoTarget(floatingStyle, 0, title, 550, 200, 96, 1920, 1080),
                "Titled teaching/dialog window accepted");
        check(!FloatingWindowSafetyPolicy.IsSafeSeewoTarget(floatingStyle, 1, "", 550, 200, 96, 1920, 1080),
            "Untitled modal-frame upload dialog accepted");
        foreach (var monitor in new[] { (1920, 1080), (3840, 2160), (550, 200), (1100, 400) })
            check(!FloatingWindowSafetyPolicy.IsSafeSeewoTarget(floatingStyle, 0, "", monitor.Item1, monitor.Item2, 192,
                monitor.Item1, monitor.Item2), "Borderless full-screen window accepted");
        // Seewo's uploaded-photo viewer uses a monitor-sized HWND. Its collapsed
        // desktop icon also lives inside a maximized transparent HWND, not a small
        // floating-bar HWND. Neither variant should match, regardless of DPI.
        foreach (var monitor in new[] { (1920, 1080), (3840, 2160), (1080, 1920), (2560, 1440) })
        foreach (uint dpi in new uint[] { 96, 120, 144, 192 })
        {
            check(!FloatingWindowSafetyPolicy.IsSafeSeewoTarget(floatingStyle, 0, "",
                monitor.Item1, monitor.Item2, dpi, monitor.Item1, monitor.Item2),
                "Uploaded-photo full-screen surface accepted");
            check(!FloatingWindowSafetyPolicy.IsSafeSeewoTarget(floatingStyle | 0x01000000, 0x00080000, "",
                monitor.Item1, monitor.Item2 - 48, dpi, monitor.Item1, monitor.Item2),
                "Collapsed photo icon's maximized transparent surface accepted");
        }
        foreach (uint dpi in new uint[] { 96, 120, 144, 168, 192, 240, 288, 384 })
        {
            int iconSize = (int)Math.Round(108 * dpi / 96.0);
            check(!FloatingWindowSafetyPolicy.IsSafeSeewoTarget(floatingStyle, 0, "", iconSize, iconSize, dpi, 7680, 4320),
                "Standalone photo icon dimensions accepted as the 550x200 floating bar");
        }
        check(!FloatingWindowSafetyPolicy.IsSafeSeewoTarget(floatingStyle, 0, "", 1920, 698, 96, 3840, 2160),
            "Large aspect-ratio match accepted without the expected DPI");
        foreach (uint dpi in new uint[] { 0, 72, 1000 })
            check(!FloatingWindowSafetyPolicy.IsSafeSeewoTarget(floatingStyle, 0, "", 550, 200, dpi, 1920, 1080),
                "Missing or invalid DPI allowed interception");
    }
}
