using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Ink_Canvas.Helpers
{
    public class UsbDriveInfo
    {
        public string DeviceId { get; set; }
        public string SerialNumber { get; set; }
        public string Model { get; set; }
        public string DriveLetter { get; set; }
        public string VolumeLabel { get; set; }
    }

    public static class UsbSecurityManager
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetVolumeInformation(
            string lpRootPathName,
            StringBuilder lpVolumeNameBuffer,
            int nVolumeNameSize,
            out uint lpVolumeSerialNumber,
            out uint lpMaximumComponentLength,
            out uint lpFileSystemFlags,
            StringBuilder lpFileSystemNameBuffer,
            int nFileSystemNameSize);

        public static string GetVolumeSerialNumber(string driveLetter)
        {
            try
            {
                string rootPath = driveLetter.TrimEnd('\\') + "\\";
                uint serialNumber = 0;
                uint maxComponentLength = 0;
                uint fileSystemFlags = 0;
                var volumeName = new StringBuilder(256);
                var fileSystemName = new StringBuilder(256);

                bool success = GetVolumeInformation(
                    rootPath,
                    volumeName,
                    volumeName.Capacity,
                    out serialNumber,
                    out maxComponentLength,
                    out fileSystemFlags,
                    fileSystemName,
                    fileSystemName.Capacity);

                if (success)
                {
                    return string.Format("{0:X4}-{1:X4}", (serialNumber >> 16) & 0xFFFF, serialNumber & 0xFFFF);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetVolumeSerialNumber error: {ex.Message}");
            }
            return "";
        }

        public static List<UsbDriveInfo> GetConnectedUsbDrives()
        {
            var list = new List<UsbDriveInfo>();
            try
            {
                var drives = DriveInfo.GetDrives();
                foreach (var d in drives)
                {
                    if (d.DriveType == DriveType.Removable && d.IsReady)
                    {
                        string driveLetter = d.Name.TrimEnd('\\');
                        string sn = GetVolumeSerialNumber(d.Name);
                        string label = string.IsNullOrEmpty(d.VolumeLabel) ? "U盘" : d.VolumeLabel;

                        list.Add(new UsbDriveInfo
                        {
                            DeviceId = driveLetter,
                            SerialNumber = sn,
                            Model = d.DriveFormat + " Removable Drive",
                            DriveLetter = driveLetter,
                            VolumeLabel = label
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetConnectedUsbDrives error: {ex.Message}");
                throw new Exception($"扫描U盘失败: {ex.Message}", ex);
            }
            return list;
        }

        public static bool VerifyCurrentUsbDrives(Settings settings)
        {
            if (settings?.Security == null || !settings.Security.UsbVerificationEnabled) return false;

            var sec = settings.Security;
            var connectedDrives = GetConnectedUsbDrives();
            if (connectedDrives.Count == 0) return false;

            var authorizedSns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(sec.UsbAuthorizedSns))
            {
                var parts = sec.UsbAuthorizedSns.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts) authorizedSns.Add(part.Trim());
            }

            foreach (var drive in connectedDrives)
            {
                if (!string.IsNullOrEmpty(drive.SerialNumber) && authorizedSns.Contains(drive.SerialNumber))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
