using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace InkCanvasWordLinkHost;

/// <summary>Late-bound IDispatch calls without per-call-site dynamic binder compilation.</summary>
internal sealed class OfficeObject(object value)
{
    internal object Value { get; } = value;
    internal bool IsRange => Supports(new Guid("0002095e-0000-0000-c000-000000000046"));
    internal bool Supports(Guid iid)
    {
        IntPtr unknown = Marshal.GetIUnknownForObject(Value);
        try
        {
            int result = Marshal.QueryInterface(unknown, in iid, out var pointer);
            if (pointer != IntPtr.Zero) Marshal.Release(pointer);
            return result >= 0;
        }
        finally { Marshal.Release(unknown); }
    }
    internal T Get<T>(string name, params object[] arguments)
        => (T)Invoke(name, BindingFlags.GetProperty, arguments);
    internal void Set(string name, object value) => Invoke(name, BindingFlags.SetProperty, new[] { value });
    internal object Call(string name, params object[] arguments)
        => Invoke(name, BindingFlags.InvokeMethod, arguments);
    internal object GoTo(int page) => Call("GoTo", 1, 1, page, Type.Missing);
    internal void GetPoint(out int x, out int y, out int width, out int height, OfficeObject range)
    {
        object[] args = { 0, 0, 0, 0, range.Value };
        var byRef = new ParameterModifier(args.Length);
        for (int i = 0; i < 4; i++) byRef[i] = true;
        Invoke("GetPoint", BindingFlags.InvokeMethod, args, new[] { byRef });
        x = (int)args[0]; y = (int)args[1]; width = (int)args[2]; height = (int)args[3];
    }
    private object Invoke(string name, BindingFlags flags, object[] args, ParameterModifier[] modifiers = null)
    {
        try
        {
            return Value.GetType().InvokeMember(DispatchName(name, flags), flags | BindingFlags.OptionalParamBinding,
                null, Value, args, modifiers, CultureInfo.CurrentCulture, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            // Keep Office HRESULTs intact for the busy/retry policy.
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    // Stable Word automation DISPIDs, verified against Microsoft's Word 15 PIA.
    // Using IDs avoids a cross-process GetIDsOfNames lookup for every property.
    // FontName distinguishes _Font.Name (142) from _Document.Name (0).
    private static string DispatchName(string name, BindingFlags flags) => name switch
    {
        "Name" or "Text" or "Type" or "Percentage" or "Item" => "[DispID=0]",
        "Document" or "Count" => "[DispID=2]",
        "Application" => "[DispID=1000]", "CommandBars" => "[DispID=57]",
        "FullScreen" => "[DispID=1]", "GetPressedMso" => "[DispID=1610809376]",
        "ExecuteMso" => "[DispID=1610809373]",
        "ActiveWindow" => "[DispID=4]", "Hwnd" => "[DispID=39]",
        "Path" or "Panes" or "Start" => "[DispID=3]",
        "Selection" or "End" => "[DispID=4]",
        "Font" => "[DispID=5]", "Duplicate" => "[DispID=6]", "StoryType" => "[DispID=7]",
        "Zoom" => "[DispID=10]", "View" => "[DispID=14]", "Sections" => "[DispID=15]",
        "FullName" => "[DispID=29]", "Content" => "[DispID=41]",
        "TopMargin" => "[DispID=100]", "BottomMargin" or "Collapse" => "[DispID=101]",
        "LeftMargin" => "[DispID=102]", "RightMargin" => "[DispID=103]",
        "PageWidth" => "[DispID=105]", "PageHeight" => "[DispID=106]",
        "RangeFromPoint" => "[DispID=110]", "ScrollIntoView" or "MoveEnd" => "[DispID=111]",
        "GetPoint" or "HeaderDistance" => "[DispID=112]", "FooterDistance" => "[DispID=113]",
        "GoTo" => "[DispID=115]", "Size" => "[DispID=141]", "FontName" => "[DispID=142]",
        "Information" => "[DispID=313]", "PageSetup" => "[DispID=1101]",
        "Range" => flags == BindingFlags.GetProperty ? "[DispID=400]" : "[DispID=2000]",
        _ => throw new ArgumentException("Unsupported Word member: " + name, nameof(name))
    };
}
