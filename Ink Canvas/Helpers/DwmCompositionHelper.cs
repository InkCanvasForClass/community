using System;
using System.Runtime.InteropServices;

namespace Ink_Canvas.Helpers
{
    public static class DwmCompositionHelper
    {
        private const string LibraryName = "dwmapi.dll";

        [DllImport(LibraryName, EntryPoint = "DwmIsCompositionEnabled", ExactSpelling = true)]
        private static extern int DwmIsCompositionEnabledCore([MarshalAs(UnmanagedType.Bool)] out bool enabled);

        public static bool IsCompositionEnabled()
        {
            try
            {
                return DwmIsCompositionEnabledCore(out bool enabled) >= 0 && enabled;
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
