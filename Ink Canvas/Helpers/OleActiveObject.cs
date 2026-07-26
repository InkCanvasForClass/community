using System;
using System.Runtime.InteropServices;
using Windows.Win32;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// .NET Core / 5+ 未提供 <see cref="Marshal.GetActiveObject"/>，通过 OLE 实现等效行为。
    /// </summary>
    internal static class OleActiveObject
    {
        //[DllImport("ole32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        //private static extern int CLSIDFromProgID(string lpszProgId, out Guid lpclsid);

        //[DllImport("oleaut32.dll", PreserveSig = true)]
        //private static extern int GetActiveObject(ref Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

        public unsafe static object GetActiveObject(string progId)
        {
            int hr = PInvoke.CLSIDFromProgID(progId, out Guid clsid);
            Marshal.ThrowExceptionForHR(hr);
             hr = PInvoke.GetActiveObject(clsid, null, out object obj); 
            Marshal.ThrowExceptionForHR(hr);
            return obj;
        }
    }
}
