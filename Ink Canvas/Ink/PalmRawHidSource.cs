using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Ink_Canvas.Ink
{
    internal readonly record struct RawPalmContact(string Device, int Id, double X, double Y,
        double Width, double Height, int WidthMaximum, int HeightMaximum, double? Pressure, bool Released);

    // One registration per process: Windows sends a device class to only its last registered
    // HWND. Both calibration and the board subscribe here instead of stealing each other's input.
    internal sealed class PalmRawHidSource : IDisposable
    {
        private const int Success = 0x00110000;
        private static readonly ushort[] TouchUsages = { 0x04, 0x20, 0x22, 0x05 };
        private static PalmRawHidSource _shared;
        private readonly HwndSource _window;
        private readonly Dictionary<IntPtr, Device> _devices = new();
        private readonly Dictionary<string, Dictionary<int, (double Pressure, long Time)>> _pressure = new(StringComparer.OrdinalIgnoreCase);
        private int _callbackErrors;
        private int _readErrors;
        private event Action<RawPalmContact> Sample;

        private readonly record struct Value(ushort Page, ushort Usage, ushort Link, int Min, int Max);
        private sealed class Device : IDisposable
        {
            internal IntPtr Descriptor;
            internal string Name;
            internal string Group;
            internal readonly List<Value> Values = new();
            internal readonly HashSet<ushort> TipLinks = new();
            internal readonly Dictionary<int, RawPalmContact> Pressed = new();
            internal bool TipSeen;
            public void Dispose() { if (Descriptor != IntPtr.Zero) Marshal.FreeHGlobal(Descriptor); Descriptor = IntPtr.Zero; }
        }

        private sealed class Subscription : IDisposable
        {
            private PalmRawHidSource _owner;
            private readonly Action<RawPalmContact> _callback;
            internal Subscription(PalmRawHidSource owner, Action<RawPalmContact> callback) { _owner = owner; _callback = callback; }
            public void Dispose()
            {
                if (_owner == null) return;
                _owner._window.Dispatcher.VerifyAccess();
                _owner.Sample -= _callback;
                if (_owner.Sample == null) _owner.Dispose();
                _owner = null;
            }
        }

        internal static IDisposable Subscribe(Action<RawPalmContact> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            _shared ??= new PalmRawHidSource();
            _shared._window.Dispatcher.VerifyAccess();
            _shared.Sample += callback;
            return new Subscription(_shared, callback);
        }

        private PalmRawHidSource()
        {
            _window = new HwndSource(new HwndSourceParameters("ICC.Palm.RawHid")
            { ParentWindow = new IntPtr(-3), WindowStyle = 0 });
            _window.AddHook(WindowMessage);
            var registrations = TouchUsages.Select(usage => new Registration { Page = 0x0d, Usage = usage, Flags = 0x2100, Window = _window.Handle }).ToArray();
            if (!RegisterRawInputDevices(registrations, (uint)registrations.Length, (uint)Marshal.SizeOf<Registration>()))
            {
                int error = Marshal.GetLastWin32Error();
                _window.Dispose();
                throw new Win32Exception(error);
            }
        }

        private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Leave WM_INPUT unhandled so DefWindowProc performs foreground input cleanup.
            if (message == 0xfe && wParam.ToInt64() == 2 && _devices.Remove(lParam, out var gone) && gone != null)
            {
                foreach (var contact in gone.Pressed.Values) Publish(contact with { Released = true });
                if (!_devices.Values.Any(d => d?.Group == gone.Group)) _pressure.Remove(gone.Group);
                gone.Dispose();
            }
            if (message != 0xff) return IntPtr.Zero;
            try { Read(lParam); }
            catch (Exception ex) when (ex is ArgumentException || ex is OverflowException || ex is Win32Exception)
            {
                if (++_readErrors == 1 || _readErrors % 100 == 0)
                    Helpers.LogHelper.WriteLogToFile("[PalmHid] " + ex.Message, Helpers.LogHelper.LogType.Warning);
            }
            return IntPtr.Zero;
        }

        private void Read(IntPtr handle)
        {
            uint length = 0, headerSize = (uint)Marshal.SizeOf<Header>();
            if (GetRawInputData(handle, 0x10000003, IntPtr.Zero, ref length, headerSize) == uint.MaxValue
                || length < headerSize + 8 || length > 1024 * 1024) return;
            IntPtr memory = Marshal.AllocHGlobal((int)length);
            try
            {
                if (GetRawInputData(handle, 0x10000003, memory, ref length, headerSize) == uint.MaxValue) return;
                Header header = Marshal.PtrToStructure<Header>(memory);
                if (header.Type != 2) return;
                if (!_devices.TryGetValue(header.Device, out var device))
                {
                    if (_devices.Count >= 64) return;
                    _devices[header.Device] = device = OpenDevice(header.Device);
                }
                if (device == null) return;
                int offset = (int)headerSize;
                uint bytes = unchecked((uint)Marshal.ReadInt32(memory, offset));
                uint count = unchecked((uint)Marshal.ReadInt32(memory, offset + 4));
                if (bytes == 0 || count == 0 || (ulong)bytes * count > length - headerSize - 8) return;
                var report = new byte[bytes];
                for (uint index = 0; index < count; index++)
                {
                    Marshal.Copy(IntPtr.Add(memory, checked(offset + 8 + (int)(index * bytes))), report, 0, report.Length);
                    Decode(device, report);
                }
            }
            finally { Marshal.FreeHGlobal(memory); }
        }

        private static Device OpenDevice(IntPtr handle)
        {
            uint length = 0;
            GetRawInputDeviceInfo(handle, 0x20000005, IntPtr.Zero, ref length);
            if (length == 0 || length > 1024 * 1024) return null;
            var device = new Device { Descriptor = Marshal.AllocHGlobal((int)length) };
            bool keep = false;
            try
            {
                if (GetRawInputDeviceInfo(handle, 0x20000005, device.Descriptor, ref length) == uint.MaxValue) return null;
                var caps = new byte[64];
                // HidP_GetCaps returns NTSTATUS, not BOOL. Fixed buffers avoid array marshaler
                // ownership hazards in out structs on .NET 10.
                if (HidP_GetCaps(device.Descriptor, caps) != Success || !TouchUsages.Contains(U16(caps, 0)) || U16(caps, 2) != 0x0d) return null;
                ushort valueCount = U16(caps, 48), buttonCount = U16(caps, 46);
                if (valueCount > 4096 || buttonCount > 4096) return null;
                byte[] values = new byte[valueCount * 72];
                if (HidP_GetValueCaps(0, values, ref valueCount, device.Descriptor) != Success) return null;
                for (int i = 0; i < valueCount; i++)
                {
                    int start = i * 72;
                    ushort first = U16(values, start + 56);
                    ushort last = values[start + 12] == 0 ? first : U16(values, start + 58);
                    for (int usage = first; usage <= last && usage - first < 256; usage++)
                        device.Values.Add(new(U16(values, start), (ushort)usage, U16(values, start + 6),
                            BitConverter.ToInt32(values, start + 40), BitConverter.ToInt32(values, start + 44)));
                }
                byte[] buttons = new byte[buttonCount * 72];
                if (buttonCount > 0 && HidP_GetButtonCaps(0, buttons, ref buttonCount, device.Descriptor) == Success)
                    for (int i = 0; i < buttonCount; i++)
                    {
                        int start = i * 72;
                        ushort first = U16(buttons, start + 56);
                        ushort last = buttons[start + 12] == 0 ? first : U16(buttons, start + 58);
                        if (U16(buttons, start) == 0x0d && first <= 0x42 && last >= 0x42)
                            device.TipLinks.Add(U16(buttons, start + 6));
                    }
                length = 0;
                GetRawInputDeviceInfo(handle, 0x20000007, IntPtr.Zero, ref length);
                if (length == 0 || length > 32768) return null;
                IntPtr name = Marshal.AllocHGlobal(checked((int)(length + 1) * 2));
                try
                {
                    if (GetRawInputDeviceInfo(handle, 0x20000007, name, ref length) == uint.MaxValue) return null;
                    device.Name = Marshal.PtrToStringUni(name, (int)length).TrimEnd('\0');
                }
                finally { Marshal.FreeHGlobal(name); }
                if (device.Name.Contains("Microsoft HID RID", StringComparison.OrdinalIgnoreCase))
                {
                    var desktop = System.Windows.Forms.SystemInformation.VirtualScreen;
                    for (int i = 0; i < device.Values.Count; i++)
                    {
                        var value = device.Values[i];
                        if (value.Page == 1 && value.Usage is 0x30 or 0x31)
                            device.Values[i] = value with { Max = value.Usage == 0x30 ? desktop.Width : desktop.Height };
                    }
                }
                device.Group = ContainerKey(device.Name);
                keep = true;
                return device;
            }
            finally { if (!keep) device.Dispose(); }
        }

        private void Decode(Device device, byte[] report)
        {
            // Some touchscreens expose pressure in another top-level collection. Share
            // fresh pressure only within the same physical device container, never by model.
            foreach (var cap in device.Values)
                if (cap.Page == 0x0d && cap.Usage == 0x30 && cap.Max > cap.Min
                    && ReadValue(device, cap, report, out uint rawPressure))
                {
                    double value = Math.Clamp((rawPressure - (double)cap.Min) / (cap.Max - (double)cap.Min), 0, 1);
                    if (value <= 0) continue;
                    if (device.TipLinks.Contains(cap.Link) && !TipPressed(device, cap.Link, report)) continue;
                    int id = cap.Link;
                    if (Find(device, cap.Link, 0x0d, 0x51) is Value idCap && ReadValue(device, idCap, report, out uint rawId)) id = unchecked((int)rawId);
                    if (!_pressure.TryGetValue(device.Group, out var cache)) _pressure[device.Group] = cache = new();
                    long now = Environment.TickCount64;
                    foreach (int expired in cache.Where(p => now - p.Value.Time > 250).Select(p => p.Key).ToArray()) cache.Remove(expired);
                    if (cache.Count < 128 || cache.ContainsKey(id)) cache[id] = (value, now);
                }
            foreach (ushort link in device.Values.Select(v => v.Link).Distinct())
            {
                var width = Find(device, link, 0x0d, 0x48);
                var height = Find(device, link, 0x0d, 0x49);
                var xCap = Find(device, link, 1, 0x30);
                var yCap = Find(device, link, 1, 0x31);
                if (width == null || height == null || xCap == null || yCap == null) continue;
                if (!ReadValue(device, width.Value, report, out uint w) || !ReadValue(device, height.Value, report, out uint h)
                    || !ReadValue(device, xCap.Value, report, out uint x) || !ReadValue(device, yCap.Value, report, out uint y)) continue;
                int id = link;
                if (Find(device, link, 0x0d, 0x51) is Value idCap && ReadValue(device, idCap, report, out uint contactId))
                    id = unchecked((int)contactId);
                bool pressed = w > 0 && h > 0;
                if (device.TipLinks.Contains(link))
                {
                    bool tip = TipPressed(device, link, report);
                    device.TipSeen |= tip;
                    if (device.TipSeen) pressed &= tip;
                }
                if (Find(device, link, 0x0d, 0x54) is Value countCap && ReadValue(device, countCap, report, out uint contacts) && contacts == 0)
                    pressed = false;
                double? pressure = null;
                if (Find(device, link, 0x0d, 0x30) is Value p && ReadValue(device, p, report, out uint raw) && p.Max > p.Min)
                    pressure = Math.Clamp((raw - (double)p.Min) / (p.Max - (double)p.Min), 0, 1);
                else if (_pressure.TryGetValue(device.Group, out var cache))
                {
                    if (cache.TryGetValue(id, out var exact) && Environment.TickCount64 - exact.Time <= 250) pressure = exact.Pressure;
                    else if (cache.Count > 0)
                    {
                        var latest = cache.Values.MaxBy(v => v.Time);
                        if (Environment.TickCount64 - latest.Time <= 250) pressure = latest.Pressure;
                    }
                }
                var sample = new RawPalmContact(device.Group, id, Normalize(x, xCap.Value), Normalize(y, yCap.Value),
                    w, h, width.Value.Max, height.Value.Max, pressure, !pressed);
                if (pressed)
                {
                    if (device.Pressed.Count >= 128 && !device.Pressed.ContainsKey(id)) continue;
                    device.Pressed[id] = sample;
                    Publish(sample);
                }
                else if (device.Pressed.Remove(id, out var previous)) Publish(previous with { Released = true });
            }
        }

        private void Publish(RawPalmContact sample)
        {
            if (Sample == null) return;
            foreach (Action<RawPalmContact> subscriber in Sample.GetInvocationList())
                try { subscriber(sample); }
                catch (Exception ex)
                {
                    if (++_callbackErrors == 1 || _callbackErrors % 100 == 0)
                        Helpers.LogHelper.WriteLogToFile("[PalmHid] Subscriber failed: " + ex.Message, Helpers.LogHelper.LogType.Warning);
                }
        }

        private static string ContainerKey(string path)
        {
            string instance = path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path[4..] : path;
            int suffix = instance.IndexOf("#{", StringComparison.Ordinal);
            if (suffix >= 0) instance = instance[..suffix];
            instance = instance.Replace('#', '\\');
            if (CM_Locate_DevNodeW(out uint node, instance, 0) == 0)
            {
                var key = new DeviceProperty { Category = new Guid("8c7ed206-3f8a-4827-b6cc-029f3b2e2b5f"), Id = 2 };
                var bytes = new byte[16]; uint length = 16;
                if (CM_Get_DevNode_PropertyW(node, ref key, out uint type, bytes, ref length, 0) == 0 && type == 13 && length == 16)
                {
                    var container = new Guid(bytes);
                    if (container != Guid.Empty) return container.ToString();
                }
            }
            return path;
        }

        private static double Normalize(uint value, Value cap) => cap.Max > cap.Min ? (value - (double)cap.Min) / (cap.Max - (double)cap.Min) : double.NaN;
        private static bool TipPressed(Device device, ushort link, byte[] report)
        {
            var usages = new ushort[256]; uint count = (uint)usages.Length;
            return HidP_GetUsages(0, 0x0d, link, usages, ref count, device.Descriptor, report, (uint)report.Length) == Success
                && usages.Take((int)Math.Min(count, (uint)usages.Length)).Contains((ushort)0x42);
        }
        private static Value? Find(Device device, ushort link, ushort page, ushort usage)
        {
            foreach (var cap in device.Values) if (cap.Link == link && cap.Page == page && cap.Usage == usage) return cap;
            return null;
        }
        private static bool ReadValue(Device device, Value cap, byte[] report, out uint value) =>
            HidP_GetUsageValue(0, cap.Page, cap.Link, cap.Usage, out value, device.Descriptor, report, (uint)report.Length) == Success;
        private static ushort U16(byte[] bytes, int index) => BitConverter.ToUInt16(bytes, index);

        public void Dispose()
        {
            _window.Dispatcher.VerifyAccess();
            var registrations = TouchUsages.Select(usage => new Registration { Page = 0x0d, Usage = usage, Flags = 1 }).ToArray();
            RegisterRawInputDevices(registrations, (uint)registrations.Length, (uint)Marshal.SizeOf<Registration>());
            _window.RemoveHook(WindowMessage);
            _window.Dispose();
            foreach (var device in _devices.Values) device?.Dispose();
            _devices.Clear();
            _pressure.Clear();
            if (ReferenceEquals(_shared, this)) _shared = null;
        }

        [StructLayout(LayoutKind.Sequential)] private struct Header { public uint Type, Size; public IntPtr Device, Parameter; }
        [StructLayout(LayoutKind.Sequential)] private struct Registration { public ushort Page, Usage; public uint Flags; public IntPtr Window; }
        [StructLayout(LayoutKind.Sequential)] private struct DeviceProperty { public Guid Category; public uint Id; }
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern int CM_Locate_DevNodeW(out uint node, string instance, uint flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern int CM_Get_DevNode_PropertyW(uint node, ref DeviceProperty key, out uint type, [Out] byte[] data, ref uint length, uint flags);
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterRawInputDevices(Registration[] devices, uint count, uint size);
        [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);
        [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", SetLastError = true)] private static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, IntPtr data, ref uint size);
        [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr preparsed, [Out] byte[] caps);
        [DllImport("hid.dll")] private static extern int HidP_GetValueCaps(int type, [Out] byte[] caps, ref ushort count, IntPtr preparsed);
        [DllImport("hid.dll")] private static extern int HidP_GetButtonCaps(int type, [Out] byte[] caps, ref ushort count, IntPtr preparsed);
        [DllImport("hid.dll")] private static extern int HidP_GetUsageValue(int type, ushort page, ushort link, ushort usage, out uint value, IntPtr preparsed, byte[] report, uint size);
        [DllImport("hid.dll")] private static extern int HidP_GetUsages(int type, ushort page, ushort link, [Out] ushort[] usages, ref uint count, IntPtr preparsed, byte[] report, uint size);
    }
}
