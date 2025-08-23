using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Ink_Canvas.Helpers {
    public static class DirectoryUtils
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint dwDesiredAccess,
            FileShare dwShareMode,
            IntPtr securityAttrs_MustBeZero,
            FileMode dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile_MustBeZero);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "SetFileTime", ExactSpelling = true)]
        private static extern bool SetFileTime(
            SafeFileHandle hFile,
            IntPtr lpCreationTimeUnused,
            IntPtr lpLastAccessTimeUnused,
            ref long lpLastWriteTime);

        private const uint FILE_ACCESS_GENERIC_READ = 0x80000000;
        private const uint FILE_ACCESS_GENERIC_WRITE = 0x40000000;

        private const int FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        private const int OPEN_EXISTING = 3;

        public static bool SetDirectoryLastWriteUtc(string dirPath, DateTime lastWriteDate) {
            using (var hDir = CreateFile(dirPath, FILE_ACCESS_GENERIC_READ | FILE_ACCESS_GENERIC_WRITE,
                FileShare.ReadWrite, IntPtr.Zero, (FileMode) OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero)) {
                var lastWriteTime = lastWriteDate.ToFileTime();
                if (!SetFileTime(hDir, IntPtr.Zero, IntPtr.Zero, ref lastWriteTime)) {
                    return false;
                }
            }
            return true;
        }

        public static bool IsWritable(string dirPath) {
            try {
                var lastWriteDate = Directory.GetLastWriteTimeUtc(dirPath);
                using (File.Create(Path.Combine(dirPath, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose)) { }

                try {
                    SetDirectoryLastWriteUtc(dirPath, lastWriteDate);
                } catch { }

                return true;
            } catch (UnauthorizedAccessException) { } catch (Exception) { }
            return false;
        }

        public static async Task<bool> IsWritableAsync(string dirPath) {
            var fn = new DirectoryInfo(dirPath).FullName;
            var result = await Task.Run(() => IsWritable(dirPath));
            return result;
        }
    }
}
