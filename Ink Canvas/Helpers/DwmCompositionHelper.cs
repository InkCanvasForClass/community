using System;
using System.Runtime.InteropServices;

namespace Ink_Canvas.Helpers
{
    public static class DwmCompositionHelper
    {
        private const string LibraryName = "dwmapi.dll";

        [DllImport(LibraryName, ExactSpelling = true, PreserveSig = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DwmIsCompositionEnabledCore();

        public static bool IsCompositionEnabled()
        {
            try
            {
                return DwmIsCompositionEnabledCore();
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
