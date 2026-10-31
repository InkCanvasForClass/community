using System.Runtime.InteropServices;

namespace InkCanvasWordLinkHost;

internal sealed class OfficeApartment : IDisposable
{
    internal OfficeApartment() => Marshal.ThrowExceptionForHR(OleInitialize(IntPtr.Zero));
    public void Dispose() => OleUninitialize();
    [DllImport("ole32.dll")] private static extern int OleInitialize(IntPtr reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
}

[ComImport, Guid("00000016-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOfficeMessageFilter
{
    [PreserveSig] int HandleIncomingCall(int type, IntPtr caller, int elapsed, IntPtr info);
    [PreserveSig] int RetryRejectedCall(IntPtr callee, int elapsed, int rejection);
    [PreserveSig] int MessagePending(IntPtr callee, int elapsed, int type);
}

[ComVisible(true)]
internal sealed class OfficeMessageFilter : IOfficeMessageFilter, IDisposable
{
    private readonly IOfficeMessageFilter _previous;
    internal OfficeMessageFilter() => Marshal.ThrowExceptionForHR(CoRegisterMessageFilter(this, out _previous));
    internal static int RetryDelay(int elapsed, int rejection) => rejection == 2 && elapsed >= 0 && elapsed < 1200 ? 100 : -1;
    public int HandleIncomingCall(int type, IntPtr caller, int elapsed, IntPtr info) => 0;
    public int RetryRejectedCall(IntPtr callee, int elapsed, int rejection) => RetryDelay(elapsed, rejection);
    public int MessagePending(IntPtr callee, int elapsed, int type) => 2;
    internal static bool IsBusy(int error) => error is unchecked((int)0x80010001) or unchecked((int)0x8001010A);
    public void Dispose()
    {
        CoRegisterMessageFilter(_previous, out _);
        if (_previous != null && Marshal.IsComObject(_previous)) Marshal.ReleaseComObject(_previous);
    }
    [DllImport("ole32.dll")] private static extern int CoRegisterMessageFilter(IOfficeMessageFilter filter, out IOfficeMessageFilter previous);
}
