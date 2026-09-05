using System;
using System.Runtime.InteropServices;
using Vortice.DirectComposition;

namespace Ink_Canvas.Ink.WinRT
{
    [ComImport]
    [Guid("4ce7d875-a981-4140-a1ff-ad93258e8d59")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IInkDesktopHost
    {
        [PreserveSig]
        int QueueWorkItem([MarshalAs(UnmanagedType.Interface)] IInkHostWorkItem workItem);

        [PreserveSig]
        int CreateInkPresenter(
            [In] ref Guid riid,
            [MarshalAs(UnmanagedType.IUnknown)] out object presenter);

        [PreserveSig]
        int CreateAndInitializeInkPresenter(
            [MarshalAs(UnmanagedType.IUnknown)] object rootVisual,
            float width,
            float height,
            [In] ref Guid riid,
            [MarshalAs(UnmanagedType.IUnknown)] out object presenter);
    }

    [ComImport]
    [Guid("73f3c0d9-2e8b-48f3-895e-20cbd27b723b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IInkPresenterDesktop
    {
        [PreserveSig]
        int SetRootVisual(
            [MarshalAs(UnmanagedType.IUnknown)] object rootVisual,
            [MarshalAs(UnmanagedType.IUnknown)] object device);

        [PreserveSig]
        int SetCommitRequestHandler(
            [MarshalAs(UnmanagedType.Interface)] IInkCommitRequestHandler handler);

        [PreserveSig]
        int GetSize(out float width, out float height);

        [PreserveSig]
        int SetSize(float width, float height);

        [PreserveSig]
        int OnHighContrastChanged();
    }

    [ComImport]
    [Guid("ccda0a9a-1b78-4632-bb96-97800662e26c")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IInkHostWorkItem
    {
        [PreserveSig]
        int Invoke();
    }

    [ComImport]
    [Guid("fabea3fc-b108-45b6-a9fc-8d08fa9f85cf")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IInkCommitRequestHandler
    {
        [PreserveSig]
        int OnCommitRequested();
    }

    internal static class InkDesktopHostInterop
    {
        internal static readonly Guid ClsidInkDesktopHost =
            new Guid("062584A6-F830-4BDC-A4D2-0A10AB062B1D");

        internal static readonly Guid IidInkDesktopHost =
            new Guid("4ce7d875-a981-4140-a1ff-ad93258e8d59");

        internal static readonly Guid IidInkPresenterDesktop =
            new Guid("73f3c0d9-2e8b-48f3-895e-20cbd27b723b");

        [DllImport("ole32.dll", ExactSpelling = true)]
        private static extern int CoCreateInstance(
            [In] ref Guid rclsid,
            IntPtr pUnkOuter,
            uint dwClsContext,
            [In] ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IInkDesktopHost host);

        internal static IInkDesktopHost CreateHost()
        {
            var clsctxInprocServer = 0x1u;
            var clsid = ClsidInkDesktopHost;
            var iid = IidInkDesktopHost;
            var result = CoCreateInstance(
                ref clsid,
                IntPtr.Zero,
                clsctxInprocServer,
                ref iid,
                out var host);
            Marshal.ThrowExceptionForHR(result);
            return host;
        }
    }

    internal sealed class ManagedInkWorkItem : IInkHostWorkItem
    {
        private readonly Func<int> _callback;

        internal ManagedInkWorkItem(Func<int> callback)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public int Invoke()
        {
            try
            {
                _callback();
                return 0;
            }
            catch (Exception ex)
            {
                return Marshal.GetHRForException(ex);
            }
        }
    }

    internal sealed class InkCommitRequestHandler : IInkCommitRequestHandler
    {
        private readonly IDCompositionDevice _compositionDevice;

        internal InkCommitRequestHandler(IDCompositionDevice compositionDevice)
        {
            _compositionDevice = compositionDevice ?? throw new ArgumentNullException(nameof(compositionDevice));
        }

        public int OnCommitRequested()
        {
            try
            {
                _compositionDevice.Commit().CheckError();
                return 0;
            }
            catch (Exception ex)
            {
                return Marshal.GetHRForException(ex);
            }
        }
    }
}
