using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualBasic;
using System.Collections;
using System.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Ink_Canvas.Helpers
{

    public static class EdgeGestureUtil
    {

        private static Guid DISABLE_TOUCH_SCREEN = new Guid("32CE38B2-2C9A-41B1-9BC5-B3784394AA44");
        private static Guid IID_PROPERTY_STORE = new Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99");

        private static short VT_BOOL = 11;
        #region "Structures"

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct PropertyKey
        {
            public PropertyKey(Guid guid, UInt32 pid)
            {
                fmtid = guid;
                this.pid = pid;
            }

            [MarshalAs(UnmanagedType.Struct)]
            public Guid fmtid;
            public uint pid;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct PropVariant
        {
            [FieldOffset(0)]
            public short vt;
            [FieldOffset(2)]
            private short wReserved1;
            [FieldOffset(4)]
            private short wReserved2;
            [FieldOffset(6)]
            private short wReserved3;
            [FieldOffset(8)]
            private sbyte cVal;
            [FieldOffset(8)]
            private byte bVal;
            [FieldOffset(8)]
            private short iVal;
            [FieldOffset(8)]
            public ushort uiVal;
            [FieldOffset(8)]
            private int lVal;
            [FieldOffset(8)]
            private uint ulVal;
            [FieldOffset(8)]
            private int intVal;
            [FieldOffset(8)]
            private uint uintVal;
            [FieldOffset(8)]
            private long hVal;
            [FieldOffset(8)]
            private long uhVal;
            [FieldOffset(8)]
            private float fltVal;
            [FieldOffset(8)]
            private double dblVal;
            [FieldOffset(8)]
            public bool boolVal;
            [FieldOffset(8)]
            private int scode;
            [FieldOffset(8)]
            private DateTime date;
            [FieldOffset(8)]
            private System.Runtime.InteropServices.ComTypes.FILETIME filetime;

            [FieldOffset(8)]
            private Blob blobVal;
            [FieldOffset(8)]
            private IntPtr pwszVal;


            /// <summary>
            /// Helper method to gets blob data
            /// </summary>
            private byte[] GetBlob()
            {
                byte[] Result = new byte[blobVal.Length];
                Marshal.Copy(blobVal.Data, Result, 0, Result.Length);
                return Result;
            }

            /// <summary>
            /// Property value
            /// </summary>
            public object Value
            {
                get
                {
                    VarEnum ve = (VarEnum)vt;
                    switch (ve)
                    {
                        case VarEnum.VT_I1:
                            return bVal;
                        case VarEnum.VT_I2:
                            return iVal;
                        case VarEnum.VT_I4:
                            return lVal;
                        case VarEnum.VT_I8:
                            return hVal;
                        case VarEnum.VT_INT:
                            return iVal;
                        case VarEnum.VT_UI4:
                            return ulVal;
                        case VarEnum.VT_LPWSTR:
                            return Marshal.PtrToStringUni(pwszVal);
                        case VarEnum.VT_BLOB:
                            return GetBlob();
                    }
                    throw new NotImplementedException("PropVariant " + ve.ToString());
                }
            }
        }

        internal struct Blob
        {
            public int Length;

            public IntPtr Data;
            //Code Should Compile at warning level4 without any warnings, 
            //However this struct will give us Warning CS0649: Field [Fieldname] 
            //is never assigned to, and will always have its default value
            //You can disable CS0649 in the project options but that will disable
            //the warning for the whole project, it's a nice warning and we do want 
            //it in other places so we make a nice dummy function to keep the compiler
            //happy.
            // 代码应该在警告级别 4 下编译而不会出现任何警告，但是此结构将给出警告 CS0649：字段 [Fieldname] 从未分配，并且始终具有其默认值。您可以在项目选项中禁用 CS0649，但这将禁用整个项目的警告，这是一个很好的警告，我们确实希望它在其他地方，所以我们制作了一个不错的虚拟函数来让编译器满意。
            private void FixCS0649()
            {
                Length = 0;
                Data = IntPtr.Zero;
            }
        }

        #endregion

        #region "Interfaces"

        [ComImport(), Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IPropertyStore
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void GetCount([Out(), In()] ref uint cProps);
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void GetAt([In()] uint iProp, ref PropertyKey pkey);
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void GetValue([In()] ref PropertyKey key, ref PropVariant pv);
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void SetValue([In()] ref PropertyKey key, [In()] ref PropVariant pv);
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void Commit();
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void Release();
        }

        #endregion

        #region "Methods"

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern int SHGetPropertyStoreForWindow(IntPtr handle, ref Guid riid, ref IPropertyStore propertyStore);

        public static void DisableEdgeGestures(IntPtr hwnd, bool enable)
        {
            IPropertyStore pPropStore = null;
            int hr = 0;
            hr = SHGetPropertyStoreForWindow(hwnd, ref IID_PROPERTY_STORE, ref pPropStore);
            if (hr == 0)
            {
                PropertyKey propKey = new PropertyKey();
                propKey.fmtid = DISABLE_TOUCH_SCREEN;
                propKey.pid = 2;
                PropVariant var = new PropVariant();
                var.vt = VT_BOOL;
                var.boolVal = enable;
                pPropStore.SetValue(ref propKey, ref var);
                Marshal.FinalReleaseComObject(pPropStore);
            }
        }

        #endregion

    }
}
