#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Ink_Canvas.Helpers;

namespace Ink_Canvas.Ink;


/// <summary>
/// 原始 HID 触摸读取（RawInput 五步骨架，自 TouchSuite.App 的 HidReader 裁剪移植）。
/// 职责：RawInput 枚举设备 + 能力探测 + WM_INPUT 报文解码，
/// 只取五样：接触尺寸（Digitizer Width 0x48 / Height 0x49）、坐标（0x01:0x30/0x31）、
/// 压感（TipPressure 0x0D:0x30）、Contact Identifier（0x0D:0x51）、接触数量（0x0D:0x54）。
/// <para>
/// 关键点（与 App 的 HidReader 完全对齐）：
/// ① 一份 WM_INPUT 可带多份子报文（dwCount），逐份解；
/// ② 每个 usage 必须按**自己的 LinkCollection（触点槽位）**取，绝不能全传 0
///    —— 多触点设备的 0x48/0x49 声明在 L1..L6，传 0 会取不到；
/// ③ 单位制取 Units 低 4 位（1=cm、3=in），不是 0xF000。
/// </para>
/// <para>
/// 与 App 的两点不同（移植的重点）：App 只挑「面积最大的一根」（它只需要一根手掌），
/// 这里要**全量**——每个子报文各自产出一个 <see cref="RawFrame"/>（等于逐条回放历史采样），
/// 帧内遍历全部槽位、不筛不挑、不做合并取最后一条。外层若要多触点身份，交给 ContactTracker 合成。
/// 不打开设备、不依赖 WM_POINTER、不做 mm 换算与逻辑屏分组。
/// </para>
/// </summary>
internal static class RawHidTouch
{
    /// <summary>
    /// 一帧里的单根触点（原始 HID 量纲）。Down=false 只在「抬手过渡帧的残留幽灵槽」出现。
    /// <para><see cref="HasCid"/>=true 表示设备真的声明并上报了 Contact Identifier，
    /// 此时 <see cref="ContactId"/> <b>连 0 也是有效身份</b>（这台屏 L1 的 CID 恒为 0）。</para>
    /// </summary>
    public sealed record RawContact(int Link, bool HasCid, int ContactId, bool Down,
        int XLogical, int YLogical, int XLogMax, int YLogMax,
        int WLogical, int HLogical, int WLogMax, int HLogMax, double? Pressure01);

    /// <summary>
    /// 一个子报文解出的一帧。SubIndex=0..SubCount-1；
    /// CountZero=设备报了「接触数 0x0D:0x54 == 0」<b>且</b>本帧一根按下的触点都没有（= 真的整体抬手）。
    /// 注意：接触数为 0 但报文里还带着按下的触点，是延迟模式的续报，不算抬手。
    /// </summary>
    public sealed record RawFrame(int SubIndex, int SubCount, bool CountZero,
        IReadOnlyList<RawContact> Contacts);

    /// <summary>被识别为触摸/数字化器的 HID 集合（无分组、无 mm 换算）。</summary>
    public sealed record HidDeviceInfo(string Name, ushort UsagePage, ushort Usage,
        int LinkCount, bool HasContactId, bool HasTipSwitch, bool HasContactCount,
        bool HasWidth, bool HasHeight, bool HasPressure);

    private const uint RIDI_PREPARSEDDATA = 0x20000005;
    private const uint RIDI_DEVICENAME = 0x20000007;
    private const uint RIM_TYPEHID = 2;
    private const int HidP_Input = 0;
    private const int HIDP_STATUS_SUCCESS = 0x00110000;
    private const int HidP_ValueCapsSize = 72;

    private const uint RID_INPUT = 0x10000003;
    private const uint RIDEV_INPUTSINK = 0x00000100;
    private const uint RIDEV_REMOVE = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICELIST { public IntPtr hDevice; public uint dwType; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER { public uint dwType; public uint dwSize; public IntPtr hDevice; public IntPtr wParam; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE { public ushort usUsagePage; public ushort usUsage; public uint dwFlags; public IntPtr hwndTarget; }

    /// <summary>
    /// HIDP_CAPS：中间是 USHORT Reserved[17]。这里刻意展平成 17 个独立字段，
    /// 避开 ByValArray 在 out 场景下走 MngdNativeArrayMarshaler（.NET 10 上会触发 CLR 崩溃）。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct HIDP_CAPS
    {
        public ushort Usage; public ushort UsagePage;
        public ushort InputReportByteLength; public ushort OutputReportByteLength; public ushort FeatureReportByteLength;
        public ushort Res01, Res02, Res03, Res04, Res05, Res06, Res07, Res08;
        public ushort Res09, Res10, Res11, Res12, Res13, Res14, Res15, Res16, Res17;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps; public ushort NumberInputValueCaps; public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps; public ushort NumberOutputValueCaps; public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps; public ushort NumberFeatureValueCaps; public ushort NumberFeatureDataIndices;
    }

    /// <summary>一条值帽声明的关键字段（HIDP_VALUE_CAPS 布局：Link@6、UnitsExp@32、Units@36、Logical@40/44、Physical@48/52）。</summary>
    private sealed record Cap(ushort Link, int LogMin, int LogMax, int PhysMin, int PhysMax, int Exp, int Units);

    private sealed class DeviceCtx
    {
        public string Name = "";
        public ushort Usage;
        public IntPtr Preparsed;
        public bool IsTouch;
        public readonly List<Cap> Widths = new();      // 0x0D:0x48（每个触点槽位一条）
        public readonly List<Cap> Heights = new();     // 0x0D:0x49
        public readonly List<Cap> Pressures = new();   // 0x0D:0x30
        public readonly List<Cap> Xs = new();          // 0x01:0x30
        public readonly List<Cap> Ys = new();          // 0x01:0x31
        public bool HasWidth => Widths.Count > 0;
        public bool HasHeight => Heights.Count > 0;
        public bool HasPressure => Pressures.Count > 0;

        /// <summary>是否声明了 Contact Identifier 0x0D:0x51（身份识别用）。</summary>
        public bool HasContactId;

        /// <summary>设备声明的全部触点槽位（LinkCollection，升序）。</summary>
        public ushort[] Links = Array.Empty<ushort>();

        /// <summary>声明了「笔尖接触 0x0D:0x42」的槽位：只有这些槽位才需要用 TipSwitch 判"真的按下"。</summary>
        public readonly HashSet<ushort> TipSwitchLinks = new();

        /// <summary>是否曾经见过某个槽位 TipSwitch=1。一旦见过，就严格按 TipSwitch 判活，
        /// 不再对"整帧都没按下"的帧做兜底（否则抬手帧会被驱动残留的 W/H 重新当成接触）。</summary>
        public bool TipEverSeen;

        /// <summary>上一帧「真的按下」的各槽位取值。抬手过渡帧用它补出最后一组坐标 / 尺寸。</summary>
        public readonly Dictionary<ushort, LinkValues> LastValues = new();

        /// <summary>上一帧是否有「真的按下」的槽位：用来识别抬手过渡帧（只补一帧）。</summary>
        public bool HadPress;

        /// <summary>接触数量 0x0D:0x54 所在的 LinkCollection（未声明则 null）。抬手信号之一：它掉到 0。</summary>
        public ushort? ContactCountLink;

        // ---- 延迟模式（手指比槽位多）的跨报文合并缓冲 ----
        // 实测这台屏（6 个槽位）7 指时**交替**发两条、且每条各自一条 WM_INPUT：
        //   ① 接触数=7，L1..L6（CID 0..5）；
        //   ② 接触数=0，L1 带 CID 6（多出来那根）。
        // 所以缓冲必须挂在设备上跨 WM_INPUT 累积，不能只在一个报文列表里合并 ——
        // 否则每条"接触数=0"都会被当成整屏抬手，所有手指每两帧就被抬掉重建一次。
        public readonly List<RawContact> Pending = new();
        public int PendingNeed;
        public long PendingSinceMs;
    }

    private static readonly Dictionary<IntPtr, DeviceCtx> Ctx = new();

    /// <summary>最近一条解不出来的报文来自哪个设备句柄（IntPtr.Zero = 上一条成功解出）。上层据此重扫。</summary>
    public static IntPtr LastUnknownDevice;

    /// <summary>最近一条成功报文所属 HID 顶层 usage（0x04 触摸屏 / 0x20 笔 / 0x22 手指 / 0x05 触摸板）。</summary>
    public static ushort LastFrameUsage;

    private static IntPtr _loggedUnknown = IntPtr.Zero;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceList([In, Out] RAWINPUTDEVICELIST[]? list, ref uint count, uint size);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, IntPtr data, ref uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint numDevices, uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(IntPtr hRawInput, uint command, IntPtr data, ref uint size, uint headerSize);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    /// <summary>
    /// user32 合成指针注入出来的触摸屏（形如 <c>\\?\Microsoft HID RID\000D_0004\3</c>）：
    /// 它的 HID 报文里 X/Y 填的是**屏幕物理像素**（实测 0..1679 / 0..1049），
    /// 可描述符却声明逻辑量程 0..32000 —— 按描述符归一会把坐标压到左上角外（xn≈0.02）。
    /// 所以这里对该设备单独适配：X/Y 的有效量程改用虚拟屏幕的像素跨度。
    /// W/H（0x0D:0x48/0x49）不动：Windows 已按物理量程换算过，本来就是正确的。
    /// </summary>
    private static bool IsSyntheticPointerTouch(bool isTouch, string deviceName) =>
        isTouch && deviceName.Contains("Microsoft HID RID", StringComparison.OrdinalIgnoreCase);

    [DllImport("hid.dll")]
    private static extern bool HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS caps);

    [DllImport("hid.dll")]
    private static extern int HidP_GetValueCaps(int reportType, byte[] valueCaps, ref ushort valueCapsLength, IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern int HidP_GetButtonCaps(int reportType, byte[] buttonCaps, ref ushort buttonCapsLength, IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern int HidP_GetUsageValue(int reportType, ushort usagePage, ushort linkCollection, ushort usage,
        out uint usageValue, IntPtr preparsedData, byte[] report, uint reportLength);

    [DllImport("hid.dll")]
    private static extern int HidP_GetUsages(int reportType, ushort usagePage, ushort linkCollection,
        [Out] ushort[] usageList, ref uint usageLength, IntPtr preparsedData, byte[] report, uint reportLength);

    private const ushort UsageTipSwitch = 0x42;   // 0x0D:0x42 笔尖接触（该槽位真的按下）

    /// <summary>延迟模式半组的最长等待（ms）：超过就把已收到的发出去，避免续报丢了把手指扣住。</summary>
    private const int PendingTimeoutMs = 60;

    // ================= 枚举 + 能力探测 =================

    /// <summary>
    /// 当前系统里被识别为触摸/数字化器的 HID 集合清单。
    /// 只收顶层集合为 0x0D:0x04(触摸屏)/0x20(笔)/0x22(手指)/0x05(触摸板) 的设备，
    /// 其余（含鼠标键盘）不进表 —— 报文解码只认这张表。
    /// </summary>
    public static List<HidDeviceInfo> Scan()
    {
        var results = new List<HidDeviceInfo>();

        uint count = 0;
        uint structSize = (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>();
        if (GetRawInputDeviceList(null, ref count, structSize) == unchecked((uint)-1) || count == 0)
            return results;

        var list = new RAWINPUTDEVICELIST[count];
        if (GetRawInputDeviceList(list, ref count, structSize) == unchecked((uint)-1))
            return results;

        for (int i = 0; i < list.Length; i++)
        {
            if (list[i].dwType != RIM_TYPEHID)
                continue;

            IntPtr hDevice = list[i].hDevice;
            string name = GetDeviceName(hDevice);
            IntPtr preparsed = GetPreparsedData(hDevice, out bool ok);
            if (!ok || preparsed == IntPtr.Zero)
                continue;

            if (!HidP_GetCaps(preparsed, out HIDP_CAPS caps))
            {
                Marshal.FreeHGlobal(preparsed);
                continue;
            }

            var ctx = new DeviceCtx
            {
                Name = name,
                Usage = caps.Usage,
                Preparsed = preparsed,
                IsTouch = caps.UsagePage == 0x0D && caps.Usage is 0x04 or 0x20 or 0x22 or 0x05,
            };

            if (!ctx.IsTouch)
            {
                // 非触摸集合不进表：注册表只登记 0x0D 系列，WM_INPUT 里不会来它，留着白占 unmanaged 内存。
                Marshal.FreeHGlobal(preparsed);
                continue;
            }

            // user32 合成指针触摸屏：X/Y 实为屏幕像素 → 有效量程取虚拟屏幕像素跨度（见 IsSyntheticPointerTouch）。
            bool syntheticPointer = IsSyntheticPointerTouch(ctx.IsTouch, name);
            int pixelSpanX = syntheticPointer ? GetSystemMetrics(SM_CXVIRTUALSCREEN) : 0;
            int pixelSpanY = syntheticPointer ? GetSystemMetrics(SM_CYVIRTUALSCREEN) : 0;

            if (caps.NumberInputValueCaps > 0)
            {
                var buffer = new byte[HidP_ValueCapsSize * caps.NumberInputValueCaps];
                ushort len = caps.NumberInputValueCaps;
                if (HidP_GetValueCaps(HidP_Input, buffer, ref len, preparsed) == HIDP_STATUS_SUCCESS)
                {
                    for (int k = 0; k < len; k++)
                    {
                        int off = k * HidP_ValueCapsSize;
                        ushort usagePage = BitConverter.ToUInt16(buffer, off + 0);
                        ushort link = BitConverter.ToUInt16(buffer, off + 6);       // LinkCollection＝触点槽位
                        ushort usage = BitConverter.ToUInt16(buffer, off + 56);
                        int logicalMin = BitConverter.ToInt32(buffer, off + 40);
                        int logicalMax = BitConverter.ToInt32(buffer, off + 44);
                        int physicalMin = BitConverter.ToInt32(buffer, off + 48);
                        int physicalMax = BitConverter.ToInt32(buffer, off + 52);
                        int units = BitConverter.ToInt32(buffer, off + 36);
                        int unitsExp = BitConverter.ToInt32(buffer, off + 32);

                        var cap = new Cap(link, logicalMin, logicalMax, physicalMin, physicalMax, unitsExp, units);
                        // 合成指针设备：X/Y 量程换成屏幕像素跨度（描述符写的 32000 与实际报文不符）
                        if (pixelSpanX > 0 && usagePage == 0x01 && usage == 0x30)
                            cap = cap with { LogMax = pixelSpanX };
                        if (pixelSpanY > 0 && usagePage == 0x01 && usage == 0x31)
                            cap = cap with { LogMax = pixelSpanY };
                        if (usagePage == 0x0D && usage == 0x54) ctx.ContactCountLink = link;   // 接触数量：抬手信号
                        if (usagePage == 0x0D && usage == 0x51) ctx.HasContactId = true;       // 身份识别
                        if (usagePage == 0x0D && usage == 0x48) ctx.Widths.Add(cap);
                        if (usagePage == 0x0D && usage == 0x49) ctx.Heights.Add(cap);
                        if (usagePage == 0x0D && usage == 0x30) ctx.Pressures.Add(cap);
                        if (usagePage == 0x01 && usage == 0x30) ctx.Xs.Add(cap);
                        if (usagePage == 0x01 && usage == 0x31) ctx.Ys.Add(cap);
                    }
                }
            }

            // 全部触点槽位（升序）：报文解码时逐个槽位取，而不是一律传 0
            ctx.Links = ctx.Widths.Concat(ctx.Heights).Concat(ctx.Xs).Concat(ctx.Ys).Concat(ctx.Pressures)
                .Select(c => c.Link).Distinct().OrderBy(x => x).ToArray();

            // 读按钮帽：找出声明了「笔尖接触 0x0D:0x42」的槽位（只有这些槽位需要判 TipSwitch）
            if (caps.NumberInputButtonCaps > 0)
            {
                var bbuf = new byte[HidP_ValueCapsSize * caps.NumberInputButtonCaps];
                ushort blen = caps.NumberInputButtonCaps;
                if (HidP_GetButtonCaps(HidP_Input, bbuf, ref blen, preparsed) == HIDP_STATUS_SUCCESS)
                {
                    for (int k = 0; k < blen; k++)
                    {
                        int off = k * HidP_ValueCapsSize;
                        ushort page = BitConverter.ToUInt16(bbuf, off + 0);
                        ushort link = BitConverter.ToUInt16(bbuf, off + 6);
                        bool isRange = bbuf[off + 12] != 0;
                        ushort uMin = BitConverter.ToUInt16(bbuf, off + 56);
                        ushort uMax = BitConverter.ToUInt16(bbuf, off + 58);
                        bool hasTip = page == 0x0D
                            && (isRange ? uMin <= UsageTipSwitch && UsageTipSwitch <= uMax : uMin == UsageTipSwitch);
                        if (hasTip)
                            ctx.TipSwitchLinks.Add(link);
                    }
                }
            }

            if (Ctx.TryGetValue(hDevice, out DeviceCtx? old) && old.Preparsed != IntPtr.Zero && old.Preparsed != preparsed)
                Marshal.FreeHGlobal(old.Preparsed);

            Ctx[hDevice] = ctx;

            results.Add(new HidDeviceInfo(name, caps.UsagePage, caps.Usage, ctx.Links.Length,
                ctx.HasContactId, ctx.TipSwitchLinks.Count > 0, ctx.ContactCountLink is not null,
                ctx.HasWidth, ctx.HasHeight, ctx.HasPressure));

            LogHelper.WriteLogToFile($"[RawInput] 探测 HID 集合 0x{caps.UsagePage:X2}:0x{caps.Usage:X2} 槽位={ctx.Links.Length} "
                    + $"尺寸={ctx.HasWidth}/{ctx.HasHeight} 压感={ctx.HasPressure} CID={ctx.HasContactId} "
                    + $"TipSwitch槽={ctx.TipSwitchLinks.Count} 接触数={(ctx.ContactCountLink is null ? "无" : "有")} "
                    + $"{name}");
        }

        return results;
    }

    // ================= 注册 / 反注册 =================

    /// <summary>Ink 只登记 0x0D（数字化器）系列：每设备类在进程内只能注册一个窗口，
    /// 碰鼠标 0x01 / 键盘 0x06 等会把 WPF 自己的 raw input 顶掉。</summary>
    private static RAWINPUTDEVICE[] DeviceSpec(uint flags, IntPtr hwnd) => new[]
    {
        new RAWINPUTDEVICE { usUsagePage = 0x0D, usUsage = 0x04, dwFlags = flags, hwndTarget = hwnd },   // 触摸屏
        new RAWINPUTDEVICE { usUsagePage = 0x0D, usUsage = 0x20, dwFlags = flags, hwndTarget = hwnd },   // 笔
        new RAWINPUTDEVICE { usUsagePage = 0x0D, usUsage = 0x22, dwFlags = flags, hwndTarget = hwnd },   // 手指
        new RAWINPUTDEVICE { usUsagePage = 0x0D, usUsage = 0x05, dwFlags = flags, hwndTarget = hwnd },   // 触摸板
    };

    private static bool _registered;

    /// <summary>注册触摸/digitizer 的 RawInput（只注册 0x0D 系列），RIDEV_INPUTSINK + hwndTarget：
    /// Ink 的窗口不保证在前台，要抓全量必须脱离焦点。</summary>
    public static bool Register(IntPtr hwnd, out int win32Error)
    {
        win32Error = 0;
        RAWINPUTDEVICE[] devices = DeviceSpec(RIDEV_INPUTSINK, hwnd);
        bool ok = RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
        if (!ok)
            win32Error = Marshal.GetLastWin32Error();

        foreach (RAWINPUTDEVICE d in devices)
            LogHelper.WriteLogToFile($"[RawInput] 注册 0x{d.usUsagePage:X2}:0x{d.usUsage:X2} INPUTSINK hwnd=0x{d.hwndTarget:X} → "
                    + (ok ? "成功" : $"失败 win32Error={win32Error}"));

        if (ok)
            _registered = true;
        return ok;
    }

    /// <summary>反注册：dwFlags = RIDEV_REMOVE，hwndTarget = IntPtr.Zero（文档要求二者搭配）。可安全重复调用。</summary>
    public static bool Unregister()
    {
        if (!_registered)
        {
            LogHelper.WriteLogToFile("[RawInput] 反注册跳过（当前未注册）");
            return true;
        }

        RAWINPUTDEVICE[] devices = DeviceSpec(RIDEV_REMOVE, IntPtr.Zero);
        bool ok = RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
        LogHelper.WriteLogToFile($"[RawInput] 反注册 0x0D 系列 4 条 → {(ok ? "成功" : $"失败 win32Error={Marshal.GetLastWin32Error()}")}");
        _registered = false;   // 无论成败都不再当"已注册"，重复调用走跳过分支
        return ok;
    }

    private static long _lastAutoRescanTicks;

    /// <summary>自动重扫节流：距上次不足 800ms 返回 false。</summary>
    public static bool TryBeginAutoRescan()
    {
        long now = Environment.TickCount64;
        if (now - _lastAutoRescanTicks < 800)
            return false;
        _lastAutoRescanTicks = now;
        return true;
    }

    // ================= WM_INPUT 解码 =================

    /// <summary>一个触点槽位本帧读到的值（不含 Down：活帧整帧为 Down、抬手补位帧整帧为 Up）。</summary>
    private readonly record struct LinkValues(
        ushort Link, bool HasCid, int ContactId,
        int XLogical, int YLogical, int XLogMax, int YLogMax,
        int WLogical, int HLogical, int WLogMax, int HLogMax, double? Pressure01);

    /// <summary>
    /// 从 WM_INPUT 的 lParam 解出这一条消息里的**全部**子报文，每个子报文各产出一个 <see cref="RawFrame"/>：
    /// SubIndex = 0..count-1，等于把驱动攒的这一批历史采样逐条回放（不做合并取最后一条）。
    /// 返回 false = 不是我们的触摸 HID 消息 / 没有可解的子报文。
    /// </summary>
    public static bool TryReadFrames(IntPtr lParam, out List<RawFrame> frames)
    {
        frames = new List<RawFrame>();

        uint size = 0;
        uint header = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
        GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, header);
        if (size == 0)
            return false;

        IntPtr buf = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(lParam, RID_INPUT, buf, ref size, header) != size)
                return false;
            if (Marshal.ReadInt32(buf, 0) != RIM_TYPEHID)
                return false;

            IntPtr hDevice = Marshal.ReadIntPtr(buf, 8);
            if (!Ctx.TryGetValue(hDevice, out DeviceCtx? ctx) || ctx is null || !ctx.IsTouch)
            {
                // 设备表里没有这个句柄：多是"启动之后才出现"的设备（例如 user32 合成指针设备）。
                // 上层据此触发一次限流重扫（Scan 会把新设备补进表）；这里记一次日志，避免静默丢样本。
                LastUnknownDevice = hDevice;
                if (_loggedUnknown != hDevice)
                {
                    _loggedUnknown = hDevice;
                    LogHelper.WriteLogToFile($"[RawInput] 未知设备句柄=0x{hDevice.ToInt64():X}，本条报文丢弃（等重扫把设备补进表）");
                }
                return false;
            }
            LastUnknownDevice = IntPtr.Zero;
            LastFrameUsage = ctx.Usage;

            uint sizeHid = (uint)Marshal.ReadInt32(buf, (int)header);
            uint reportCount = (uint)Marshal.ReadInt32(buf, (int)header + 4);
            if (sizeHid == 0 || reportCount == 0)
                return false;

            IntPtr dataPtr = buf + (int)header + 8;
            var reports = new List<byte[]>((int)reportCount);
            for (int i = 0; i < reportCount; i++)
            {
                var report = new byte[sizeHid];
                Marshal.Copy(dataPtr + (int)(i * sizeHid), report, 0, (int)sizeHid);
                reports.Add(report);
            }

            // 延迟模式合并缓冲跨 WM_INPUT 存活（挂在设备上），见 DeviceCtx.Pending 的说明。
            // 上一次留下的半组如果迟迟等不到续报（手指抬走等），先把已收到的发出去，别把手指扣住。
            long nowMs = Environment.TickCount64;
            if (ctx.PendingNeed > 0 && nowMs - ctx.PendingSinceMs > PendingTimeoutMs)
            {
                if (ctx.Pending.Count > 0)
                {
                    frames.Add(new RawFrame(0, 1, false, ctx.Pending.ToArray()));
                    ctx.HadPress = true;
                }
                ctx.Pending.Clear();
                ctx.PendingNeed = 0;
            }

            for (int i = 0; i < reports.Count; i++)
            {
                byte[] report = reports[i];
                bool requireTip = ctx.TipSwitchLinks.Count > 0;

                // 第一遍：按 TipSwitch 过滤（只认真的按下的槽位，排除残留幽灵槽）；同时记住本帧各槽位取值
                List<RawContact> contacts = ReadAllLinks(ctx, report, requireTip);

                // 兜底：**只在这台设备从来没置过 TipSwitch** 时忽略它再收一次（个别驱动的怪癖）。
                if (contacts.Count == 0 && requireTip && !ctx.TipEverSeen)
                    contacts = ReadAllLinks(ctx, report, requireTip: false);

                bool hasCount = TryReadContactCount(ctx, report, out uint count);
                int total = hasCount ? (int)count : contacts.Count;

                // ===== 延迟模式续报（接触数 = 0，但报文里带着按下的触点）=====
                // 这类报文既不是头帧也不是抬手，只能并进挂着的那一组。
                if (ctx.PendingNeed > 0)
                {
                    bool added = false;
                    foreach (RawContact c in contacts)
                        added |= AddUnique(ctx.Pending, c);
                    if (total > ctx.PendingNeed)
                        ctx.PendingNeed = total;          // 组里又多了新手指
                    ctx.PendingSinceMs = nowMs;

                    if (ctx.Pending.Count >= ctx.PendingNeed)
                    {
                        frames.Add(new RawFrame(i, reports.Count, false, ctx.Pending.ToArray()));
                        ctx.HadPress = true;
                        ctx.Pending.Clear();
                        ctx.PendingNeed = 0;
                        continue;
                    }
                    if (added)
                        continue;                          // 还差几根，继续等续报

                    // 续报没带来新手指（多半是手指抬走了）→ 这一组到此为止
                    if (ctx.Pending.Count > 0)
                    {
                        frames.Add(new RawFrame(i, reports.Count, false, ctx.Pending.ToArray()));
                        ctx.HadPress = true;
                    }
                    ctx.Pending.Clear();
                    ctx.PendingNeed = 0;
                    continue;
                }

                // ===== 延迟模式头帧：接触数超过**槽位数** =====
                // 只有"手指比槽位多"才会这样（正常情况下悬停的手指也算进接触数，但不会超过槽位数）。
                if (total > ctx.Links.Length && contacts.Count > 0)
                {
                    ctx.Pending.Clear();
                    foreach (RawContact c in contacts)
                        AddUnique(ctx.Pending, c);
                    ctx.PendingNeed = total;
                    ctx.PendingSinceMs = nowMs;

                    if (ctx.Pending.Count >= ctx.PendingNeed)
                    {
                        frames.Add(new RawFrame(i, reports.Count, false, ctx.Pending.ToArray()));
                        ctx.HadPress = true;
                        ctx.Pending.Clear();
                        ctx.PendingNeed = 0;
                    }
                    continue;
                }

                // ===== 普通帧 =====
                bool noContact = contacts.Count == 0;
                bool countZero = hasCount && count == 0;

                // 接触数掉到 0 = **整体抬手**。抬手帧里可能还残留着上一帧的槽位（幽灵槽），
                // 那些残留绝不能当"新按下"发出去，否则会凭空多出一笔、或者把旧轨道拖住不放。
                if (countZero)
                {
                    if (!ctx.HadPress)
                        continue;                       // 本来就没按下：这一帧什么都不用做
                    EmitLift(ctx, report, i, reports.Count, frames);
                    continue;
                }

                if (noContact)
                {
                    // 没有接触数可用（有些设备不声明 0x54），或者接触数 > 0 但一根都没真的按下。
                    // 只要上一帧还有按压，这就是抬手。
                    if (!ctx.HadPress)
                        continue;
                    EmitLift(ctx, report, i, reports.Count, frames);
                    continue;
                }

                // 真的有按下的触点
                frames.Add(new RawFrame(i, reports.Count, false, contacts));
                ctx.HadPress = true;
            }

            return frames.Count > 0;
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    /// <summary>
    /// 遍历 <see cref="DeviceCtx.Links"/>，把本帧**所有**触点收进结果（不筛、不挑、不求和）。
    /// requireTip=true 时只认 TipSwitch 真的按下的槽位（排除残留幽灵槽）；
    /// 收下的槽位一律记进 <see cref="DeviceCtx.LastValues"/> 供抬手帧补位。
    /// </summary>
    private static List<RawContact> ReadAllLinks(DeviceCtx ctx, byte[] report, bool requireTip)
    {
        var list = new List<RawContact>();
        uint len = (uint)report.Length;

        foreach (ushort link in ctx.Links)
        {
            if (!TryReadLink(ctx, report, len, link, requireTip, out LinkValues v))
                continue;

            ctx.LastValues[link] = v;   // 记下"真的按下"时的取值，供下一步抬手过渡帧补位
            list.Add(new RawContact(v.Link, v.HasCid, v.ContactId, Down: true,
                v.XLogical, v.YLogical, v.XLogMax, v.YLogMax, v.WLogical, v.HLogical,
                v.WLogMax, v.HLogMax, v.Pressure01));
        }
        return list;
    }

    /// <summary>本子报文的接触数量 0x0D:0x54（设备未声明该 usage 则返回 false）。</summary>
    private static bool TryReadContactCount(DeviceCtx ctx, byte[] report, out uint count)
    {
        count = 0;
        if (ctx.ContactCountLink is not ushort link)
            return false;
        return HidP_GetUsageValue(HidP_Input, 0x0D, link, 0x54, out count, ctx.Preparsed, report, (uint)report.Length)
               == HIDP_STATUS_SUCCESS;
    }

    /// <summary>延迟模式合并用：报了 CID 就按 CID 去重（0 也算有效身份），否则按槽位。</summary>
    private static bool AddUnique(List<RawContact> list, RawContact c)
    {
        foreach (RawContact e in list)
        {
            if (c.HasCid && e.HasCid ? e.ContactId == c.ContactId : e.Link == c.Link)
                return false;
        }
        list.Add(c);
        return true;
    }

    /// <summary>
    /// 发一个「整体抬手」帧：用上一帧真的按下过的槽位补出最后一个抬起点（Down=false），
    /// 让上层先画到真实抬手位置再结束笔画，末点不会漏。
    /// </summary>
    private static void EmitLift(DeviceCtx ctx, byte[] report, int subIndex, int subCount, List<RawFrame> frames)
    {
        IReadOnlyList<RawContact> lift = BuildLiftContacts(ctx, report);
        frames.Add(new RawFrame(subIndex, subCount, true, lift));
        ctx.HadPress = false;
        // 补位用过的槽位取值立刻作废：留着会把上一次手势的残留坐标带到下一次抬手，
        // 那会让另一根手指的轨道被拖到一个早已不存在的位置（画出一条凭空连线）。
        ctx.LastValues.Clear();
        if (lift.Count > 0)
            LogHelper.WriteLogToFile($"[抬手补位] 子报文{subIndex + 1}/{subCount} 补位={lift.Count} 槽");
    }

    /// <summary>
    /// 抬手过渡帧的"残留幽灵槽"补位：设备已认定抬起，但报告里还留着上一次的 W/H，
    /// 所以只取「上一帧真的读到过」的槽位（<see cref="DeviceCtx.LastValues"/>）：
    /// 尺寸沿用上一帧、坐标用本帧（非 0 才采用），让画布能画出最后一个抬起的点。
    /// 这些触点 Down=false（属于"抬起"，供多触点跟踪做末点 Move + Up）。
    /// </summary>
    private static List<RawContact> BuildLiftContacts(DeviceCtx ctx, byte[] report)
    {
        var list = new List<RawContact>();
        uint len = (uint)report.Length;

        foreach (ushort link in ctx.Links)
        {
            if (!ctx.LastValues.TryGetValue(link, out LinkValues last)
                || last.WLogical <= 0 || last.HLogical <= 0)
                continue;

            Cap? xc = ctx.Xs.FirstOrDefault(c => c.Link == link);
            Cap? yc = ctx.Ys.FirstOrDefault(c => c.Link == link);
            uint xv = 0, yv = 0;
            bool hasX = xc is not null && Get(0x01, link, 0x30, out xv);
            bool hasY = yc is not null && Get(0x01, link, 0x31, out yv);

            // 抬手补位只按位置匹配：槽位在 6 通道屏上会被交替复用，LastValues 里那个 CID
            // 可能属于"刚借用过这个槽位的另一根手指"，按它匹配会把 Up 挂到错的轨道上。
            list.Add(new RawContact(link, HasCid: false, last.ContactId, Down: false,
                hasX && xv > 0 ? (int)xv : last.XLogical,
                hasY && yv > 0 ? (int)yv : last.YLogical,
                last.XLogMax, last.YLogMax, last.WLogical, last.HLogical,
                last.WLogMax, last.HLogMax, last.Pressure01));

            bool Get(ushort page, ushort lc, ushort usage, out uint value)
                => HidP_GetUsageValue(HidP_Input, page, lc, usage, out value, ctx.Preparsed, report, len)
                   == HIDP_STATUS_SUCCESS;
        }
        return list;
    }

    /// <summary>
    /// 按**该触点自己的 LinkCollection** 读一个槽位的尺寸/坐标/压感/身份。
    /// <para>① requireTip 且该槽位声明了「笔尖接触 0x0D:0x42」时，必须 TipSwitch 真的按下才算有效
    /// —— 否则设备会把上一次接触的残留 W/H 留在别的槽位里，把坐标抢走（表现为坐标卡在某个固定点）。</para>
    /// <para>② 尺寸（0x48/0x49）必须两个都读到且非 0。</para>
    /// </summary>
    private static bool TryReadLink(DeviceCtx ctx, byte[] report, uint len, ushort link, bool requireTip, out LinkValues v)
    {
        v = default;

        // 该槽位是否真的按下（只对声明了 TipSwitch 的槽位做这个判断）
        if (requireTip && ctx.TipSwitchLinks.Contains(link))
        {
            if (!TipPressed(ctx, report, len, link))
                return false;
            ctx.TipEverSeen = true;   // 见过真按下 → 之后抬手帧不再走兜底
        }

        Cap? wc = ctx.Widths.FirstOrDefault(c => c.Link == link);
        Cap? hc = ctx.Heights.FirstOrDefault(c => c.Link == link);
        Cap? xc = ctx.Xs.FirstOrDefault(c => c.Link == link);
        Cap? yc = ctx.Ys.FirstOrDefault(c => c.Link == link);
        Cap? pc = ctx.Pressures.FirstOrDefault(c => c.Link == link);

        uint w = 0, h = 0, x = 0, y = 0, p = 0;
        bool hasW = wc is not null && Get(0x0D, link, 0x48, out w);
        bool hasH = hc is not null && Get(0x0D, link, 0x49, out h);
        if (!hasW || !hasH || w == 0 || h == 0)
            return false;

        bool hasX = xc is not null && Get(0x01, link, 0x30, out x);
        bool hasY = yc is not null && Get(0x01, link, 0x31, out y);
        bool hasP = pc is not null && Get(0x0D, link, 0x30, out p);
        bool hasCid = Get(0x0D, link, 0x51, out uint cid);

        v = new LinkValues(link, hasCid, hasCid ? (int)cid : 0,
            (int)x, (int)y, hasX ? xc!.LogMax : 0, hasY ? yc!.LogMax : 0,
            (int)w, (int)h, hasW ? wc!.LogMax : 0, hasH ? hc!.LogMax : 0,
            hasP && pc!.LogMax > pc.LogMin
                ? Math.Clamp((double)(p - (uint)pc.LogMin) / (pc.LogMax - pc.LogMin), 0, 1)
                : null);
        return true;

        bool Get(ushort page, ushort lc, ushort usage, out uint value)
            => HidP_GetUsageValue(HidP_Input, page, lc, usage, out value, ctx.Preparsed, report, len) == HIDP_STATUS_SUCCESS;
    }

    /// <summary>该槽位本帧的「笔尖接触 0x0D:0x42」是否按下（读不到就当未按下）。</summary>
    private static bool TipPressed(DeviceCtx ctx, byte[] report, uint len, ushort link)
    {
        var usages = new ushort[64];
        uint n = (uint)usages.Length;
        if (HidP_GetUsages(HidP_Input, 0x0D, link, usages, ref n, ctx.Preparsed, report, len) != HIDP_STATUS_SUCCESS)
            return false;
        for (uint i = 0; i < n; i++)
            if (usages[i] == UsageTipSwitch)
                return true;
        return false;
    }

    // ================= 设备名 / preparsed data =================

    private static string GetDeviceName(IntPtr device)
    {
        uint size = 0;
        GetRawInputDeviceInfo(device, RIDI_DEVICENAME, IntPtr.Zero, ref size);
        if (size == 0)
            return "(无名)";

        IntPtr buf = Marshal.AllocHGlobal((int)(size * 2 + 2));
        try
        {
            uint chars = size;
            GetRawInputDeviceInfo(device, RIDI_DEVICENAME, buf, ref chars);
            return (Marshal.PtrToStringUni(buf) ?? "(无名)").Replace("\0", "");
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    private static IntPtr GetPreparsedData(IntPtr device, out bool ok)
    {
        ok = false;
        uint size = 0;
        GetRawInputDeviceInfo(device, RIDI_PREPARSEDDATA, IntPtr.Zero, ref size);
        if (size == 0)
            return IntPtr.Zero;

        IntPtr buf = Marshal.AllocHGlobal((int)size);
        uint actual = size;
        if (GetRawInputDeviceInfo(device, RIDI_PREPARSEDDATA, buf, ref actual) == unchecked((uint)-1))
        {
            Marshal.FreeHGlobal(buf);
            return IntPtr.Zero;
        }
        ok = true;
        return buf;
    }
}