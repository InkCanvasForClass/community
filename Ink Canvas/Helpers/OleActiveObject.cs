using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

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

        public static unsafe object GetActiveObject(string progID)
        {
            if (string.IsNullOrEmpty(progID))
                throw new ArgumentNullException(nameof(progID));

            HRESULT hr;

            hr = PInvoke.CLSIDFromProgIDEx(progID, out Guid clsid);

            if (hr.Failed)
            {
                hr = PInvoke.CLSIDFromProgID(progID, out clsid);
            }

            if (hr.Failed)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
            hr = PInvoke.GetActiveObject(in clsid, null, out object obj);

            if (hr.Failed)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            return obj;
        }
    }
}
