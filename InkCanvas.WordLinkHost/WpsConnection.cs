using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace InkCanvasWordLinkHost;

/// <summary>Borrow the running WPS application and verify its active window. Never launch or quit WPS.</summary>
internal sealed class WpsConnection : IDisposable
{
    private object _application;
    private AutomationElement _document;
    private IntPtr _root;

    internal object Window(IntPtr root, IntPtr documentWindow)
    {
        // Compatible native document windows can expose their automation object
        // even when a WPS edition has not registered the application in the ROT.
        if (documentWindow != root)
        {
            Guid dispatch = new("00020400-0000-0000-C000-000000000046");
            int result = Native.AccessibleObjectFromWindow(documentWindow, 0xfffffff0, ref dispatch, out object native);
            if (result >= 0 && native != null)
            {
                bool nativeAccepted = false;
                try
                {
                    object document = new OfficeObject(native, true).Get<object>("Document");
                    nativeAccepted = document != null;
                    if (document != null && Marshal.IsComObject(document)) Marshal.ReleaseComObject(document);
                    if (nativeAccepted) return native;
                }
                catch (MissingMemberException) { }
                catch (COMException ex) when (!OfficeMessageFilter.IsBusy(ex.HResult)) { }
                finally { if (!nativeAccepted) Marshal.ReleaseComObject(native); }
            }
        }
        if (_application == null)
        {
            var type = Type.GetTypeFromProgID("KWPS.Application", throwOnError: false);
            if (type == null) throw new InvalidOperationException("WPS automation is not registered.");
            Guid id = type.GUID;
            Marshal.ThrowExceptionForHR(GetActiveObject(ref id, IntPtr.Zero, out _application));
        }
        var window = new OfficeObject(_application, true).Get<object>("ActiveWindow");
        bool accepted = false;
        try
        {
            long handle = new OfficeObject(window, true).Get<long>("Hwnd");
            accepted = GetAncestor(new IntPtr(handle), 2) == root;
            if (!accepted) throw new InvalidOperationException("The active WPS document belongs to another window.");
            if (_root != root) { _root = root; _document = null; }
            return window;
        }
        finally { if (!accepted && window != null && Marshal.IsComObject(window)) Marshal.ReleaseComObject(window); }
    }

    internal bool TryViewport(IntPtr root, out Native.Rect bounds)
    {
        bounds = default;
        try
        {
            if (_root != root) { _root = root; _document = null; }
            if (_document == null)
            {
                var candidates = AutomationElement.FromHandle(root)?.FindAll(TreeScope.Descendants,
                    new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document),
                        new PropertyCondition(AutomationElement.IsOffscreenProperty, false)));
                double largest = 0;
                for (int i = 0; candidates != null && i < Math.Min(candidates.Count, 64); i++)
                {
                    var area = candidates[i].Current.BoundingRectangle;
                    if (!area.IsEmpty && area.Width * area.Height > largest)
                    { largest = area.Width * area.Height; _document = candidates[i]; }
                }
            }
            if (_document == null) return false;
            if (_document.Current.IsOffscreen) { _document = null; return false; }
            var rect = _document.Current.BoundingRectangle;
            if (!double.IsFinite(rect.X) || !double.IsFinite(rect.Y) || !double.IsFinite(rect.Width) || !double.IsFinite(rect.Height)) return false;
            if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0 || !Native.GetWindowRect(root, out var window)) return false;
            bounds = new Native.Rect { Left = Math.Max(window.Left, (int)Math.Ceiling(rect.Left)), Top = Math.Max(window.Top, (int)Math.Ceiling(rect.Top)),
                Right = Math.Min(window.Right, (int)Math.Floor(rect.Right)), Bottom = Math.Min(window.Bottom, (int)Math.Floor(rect.Bottom)) };
            return bounds.Right > bounds.Left && bounds.Bottom > bounds.Top;
        }
        catch (ElementNotAvailableException) { _document = null; return false; }
        catch (InvalidOperationException) { _document = null; return false; }
    }

    public void Dispose()
    {
        _document = null; _root = IntPtr.Zero;
        if (_application != null) { Marshal.ReleaseComObject(_application); _application = null; }
    }
    [DllImport("oleaut32.dll", PreserveSig = true)] private static extern int GetActiveObject(ref Guid id, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object value);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
}
