using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Ink_Canvas.Helpers
{
    public class DwmCompositionHelper{
        public const string LibraryName = "Dwmapi.dll";

        [DllImport(LibraryName, ExactSpelling = true, PreserveSig = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DwmIsCompositionEnabled();
    }
}
