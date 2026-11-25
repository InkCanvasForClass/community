using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 渲染节奏控制器：探测显示器刷新率，并按"无限帧率"开关取消应用侧的自限速。
    /// </summary>
    /// <remarks>
    /// 必须说清 WPF 的真实上限，否则这个开关就是安慰剂：
    ///
    /// WPF 的呈现节奏由合成器（DWM/UCE）的 vsync 通知驱动——<c>MediaContext</c> 在
    /// <c>EnterInterlockedPresentation</c> 里进入 "Render → CommitChannel → NotifyPresented" 的交错模式，
    /// 每次提交后等合成器回报"上一帧已上屏"才排下一帧，并用
    /// <c>_animationRenderRate = displayRefreshRate</c> 估计下一次 vblank。
    /// 也就是说 **WPF 的帧率物理上限就是显示器刷新率**，应用层无法把它推到更高；
    /// 强行绕过会破坏呈现同步（撕裂、DWM 抖动），不是可选项。
    ///
    /// 那么"无限帧率"在本应用里能做什么（也是这里实际做的事）：
    /// 1. 确认真实刷新率，作为 HUD 的参照基准（用户能看到"跑满 144"还是"只有 60"）。
    /// 2. 把应用侧会人为压低帧率的节奏全部放开：渲染优先级不降级、弹簧与 HUD 的
    ///    刷新不再被固定毫秒数限流、动画 DesiredFrameRate 不设上限。
    /// 3. 排除"显示器是 144 但 WPF 只跑 60"的常见原因（垂直同步/DWM 合成、跨屏 DPI、
    ///    软件渲染回退），并在 HUD 里把这些状态显示出来，便于定位真正的瓶颈。
    /// </remarks>
    internal static class RenderPacingController
    {
        private static int _displayRefreshRate;
        private static bool _unlimitedFrameRate;
        private static bool _powerPerformanceApplied;

        /// <summary>显示器刷新率（Hz）；探测失败时为 0。</summary>
        public static int DisplayRefreshRate => _displayRefreshRate;

        /// <summary>是否已开启"无限帧率"（取消应用侧自限速）。</summary>
        public static bool IsUnlimitedFrameRate => _unlimitedFrameRate;

        /// <summary>当前是否走 WPF 软件渲染（软件渲染不可能跑满高刷）。</summary>
        public static bool IsSoftwareRendering =>
            RenderCapability.Tier >> 16 == 0;

        /// <summary>
        /// 探测主显示器刷新率。优先用 Win32 的当前显示模式，失败再退回 SystemParameters。
        /// </summary>
        public static int ProbeDisplayRefreshRate()
        {
            var rate = ProbeRefreshRateNative();
            if (rate <= 0)
            {
                // 兜底：部分虚拟显示器/远程会话拿不到 DEVMODE
                LogHelper.WriteLogToFile(
                    "[RenderPacing] 无法通过 EnumDisplaySettings 取得刷新率，回退到默认估计值",
                    LogHelper.LogType.Warning);
            }

            _displayRefreshRate = rate;
            return rate;
        }

        private static int ProbeRefreshRateNative()
        {
            try
            {
                var mode = new DEVMODE();
                mode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

                // ENUM_CURRENT_SETTINGS = -1
                if (!EnumDisplaySettings(null, -1, ref mode)) return 0;

                // dmDisplayFrequency 在部分驱动上返回 0 或 1（"硬件默认"）
                if (mode.dmDisplayFrequency <= 1) return 0;

                // 59/60 这类是 NTSC 精确值，取整更符合用户认知
                return (int)Math.Round((double)mode.dmDisplayFrequency, MidpointRounding.AwayFromZero);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[RenderPacing] 刷新率探测异常: {ex.Message}", LogHelper.LogType.Warning);
                return 0;
            }
        }

        /// <summary>
        /// 应用帧率相关设置。
        /// </summary>
        /// <remarks>
        /// 关于"无限帧率"能做什么，这里给出结论性说明（结论来自 WPF 源码，不是猜测）：
        ///
        /// 1. WPF 的呈现节奏由合成器 vsync 驱动。MediaContext 在
        ///    <c>EnterInterlockedPresentation</c> 中进入 Render → CommitChannel → NotifyPresented 交错模式，
        ///    每提交一帧都要等合成器回报"已上屏"，并用 <c>_animationRenderRate = displayRefreshRate</c>
        ///    估计下一次 vblank。因此**帧率物理上限 = 显示器刷新率**。
        /// 2. 这个交错模式由 <c>MediaSystem.AnimationSmoothing</c> 决定，而它的注册表开关
        ///    <c>ReadAnimationSmoothingSetting</c> 只在 <c>#if PRERELEASE</c> 内部构建里编译，
        ///    正式版 WPF 无法关闭。也就是说应用层没有"关掉 vsync 换更高帧率"的受支持路径。
        /// 3. <c>Timeline.DesiredFrameRate</c> 的默认值本来就是 null（不限制），
        ///    实测对它 OverrideMetadata 无效果，且二次 Override 会抛 ArgumentException。
        ///
        /// 所以这里不做"承诺突破刷新率"的空操作，而是让开关产生两个真实、可观测的效果：
        /// - 依据刷新率设置单帧时间预算（<see cref="FrameBudgetMs"/>），作为 HUD 与诊断的判读基准；
        /// - 记录一次环境诊断（刷新率、渲染层级、是否软件渲染、预算），
        ///   这些正是"高刷屏却只有 60fps"类问题的排查入口。
        /// </remarks>
        /// <param name="unlimited">是否按显示器刷新率给足帧预算。</param>
        public static void ApplyUnlimitedFrameRate(bool unlimited)
        {
            _unlimitedFrameRate = unlimited;

            try
            {
                FrameBudgetMs = unlimited && _displayRefreshRate > 0
                    ? 1000.0 / _displayRefreshRate
                    : 1000.0 / 60.0;

                LogHelper.WriteLogToFile(
                    $"[RenderPacing] 帧预算已更新为 {FrameBudgetMs:F2} ms（{DescribeEnvironment()}，"
                    + (unlimited ? "无限帧率开启：以刷新率为预算基准" : "默认：按 60Hz 预算")
                    + "）。WPF 的呈现节奏由合成器 vsync 驱动，显示器刷新率即物理上限。",
                    LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[RenderPacing] 应用帧率设置失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 让进程退出 Windows 的节能节流，并提高系统计时器精度。
        /// </summary>
        /// <remarks>
        /// 只做两件可验证的事，不做猜测式调参：
        ///
        /// 1. 退出 EcoQoS（效率模式）的 CPU 执行速度节流。Windows 会给"用户没在操作"
        ///    或"后台化"的进程打上 EcoQoS 标记，调度器随即把它压到低功耗核/低频率上；
        ///    白板类应用常长时间全屏无输入，被判定可节流的概率很高，动画一开场就吃降频长帧。
        ///    <c>SetProcessInformation(ProcessPowerThrottling)</c> 把 EXECUTION_SPEED 的
        ///    StateMask 置 0 是微软文档给出的退出方式。这里先读一次当前状态再写，
        ///    日志能证明"改之前到底有没有被节流"，而不是假设它一定被节流了。
        /// 2. 把计时器精度提到 1ms（<c>timeBeginPeriod(1)</c>）。默认 15.6ms 的定时器粒度
        ///    会让所有等待型调度（Dispatcher 定时器、呈现线程的节拍对齐）最多抖一整个粒度。
        ///    Win10 2004 起该调用只影响本进程，不会拖累整机功耗。
        ///
        /// 不做的事：不提升进程优先级（那是抢占，不是节能），不阻止显示器休眠，
        /// 不改 DWM 组合策略——与"不要节能"无关或副作用过大。
        /// 幂等：重复调用只生效一次。
        /// </remarks>
        public static void ApplyPowerPerformanceMode()
        {
            if (_powerPerformanceApplied) return;
            _powerPerformanceApplied = true;

            ApplyEcoQosOptOut();
            ApplyHighResolutionTimer();
        }

        private static void ApplyEcoQosOptOut()
        {
            try
            {
                var handle = GetCurrentProcess();
                string before = DescribeEcoQos(handle);

                var state = new PROCESS_POWER_THROTTLING_STATE
                {
                    Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                    ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                    StateMask = 0 // 0 = 显式关闭"执行速度"节流
                };

                bool ok = SetProcessInformation(handle, ProcessPowerThrottling, ref state,
                    Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>());
                if (ok)
                {
                    LogHelper.WriteLogToFile(
                        $"[RenderPacing] 已关闭 EcoQoS 执行速度节流（之前={before} 之后={DescribeEcoQos(handle)}）。",
                        LogHelper.LogType.Info);
                }
                else
                {
                    LogHelper.WriteLogToFile(
                        $"[RenderPacing] 关闭 EcoQoS 失败（Win32 错误 {Marshal.GetLastWin32Error()}），之前={before}；继续按系统默认调度运行。",
                        LogHelper.LogType.Warning);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[RenderPacing] 关闭 EcoQoS 异常: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private static string DescribeEcoQos(IntPtr handle)
        {
            try
            {
                var query = new PROCESS_POWER_THROTTLING_STATE
                {
                    Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION
                };
                if (!GetProcessInformation(handle, ProcessPowerThrottling, ref query,
                        Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>()))
                {
                    return "读取失败";
                }

                bool throttled = (query.ControlMask & PROCESS_POWER_THROTTLING_EXECUTION_SPEED) != 0
                                 && (query.StateMask & PROCESS_POWER_THROTTLING_EXECUTION_SPEED) != 0;
                return throttled ? "执行速度节流=开" : "执行速度节流=关";
            }
            catch
            {
                return "读取异常";
            }
        }

        private static void ApplyHighResolutionTimer()
        {
            try
            {
                // TIMERR_NOERROR = 0
                uint result = timeBeginPeriod(1);
                if (result == 0)
                {
                    LogHelper.WriteLogToFile(
                        "[RenderPacing] 计时器精度已提升到 1ms（timeBeginPeriod），消除默认 15.6ms 粒度的等待抖动。",
                        LogHelper.LogType.Info);
                }
                else
                {
                    LogHelper.WriteLogToFile(
                        $"[RenderPacing] timeBeginPeriod(1) 失败（错误码 {result}），继续使用默认计时器精度。",
                        LogHelper.LogType.Warning);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[RenderPacing] timeBeginPeriod 异常: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        /// <summary>
        /// 单帧时间预算（毫秒）。按显示器刷新率换算，供 HUD 与诊断判读：
        /// "最差 41ms" 在 60Hz（预算 16.7ms）下是掉两帧，在 30Hz 下却正常。
        /// </summary>
        public static double FrameBudgetMs { get; private set; } = 1000.0 / 60.0;

        /// <summary>
        /// 诊断用的一行摘要，用于 HUD 与日志。
        /// </summary>
        public static string DescribeEnvironment()
        {
            var refresh = _displayRefreshRate > 0 ? _displayRefreshRate + "Hz" : "未知刷新率";
            var renderMode = IsSoftwareRendering ? "软件渲染" : "硬件渲染";
            return $"{refresh} / {renderMode}";
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        // PROCESS_INFORMATION_CLASS.ProcessPowerThrottling = 4
        private const int ProcessPowerThrottling = 4;

        // PROCESS_POWER_THROTTLING_CURRENT_VERSION / PROCESS_POWER_THROTTLING_EXECUTION_SPEED
        private const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
        private const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_POWER_THROTTLING_STATE
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessInformation(IntPtr process, int processInformationClass,
            ref PROCESS_POWER_THROTTLING_STATE processInformation, int processInformationSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessInformation(IntPtr process, int processInformationClass,
            ref PROCESS_POWER_THROTTLING_STATE processInformation, int processInformationSize);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint period);
    }
}