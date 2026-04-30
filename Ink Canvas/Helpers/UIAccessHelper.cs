using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 通过 Winlogon 令牌模拟实现 UIAccess 提权重启。
    /// 1. 找到当前会话中 winlogon.exe 的令牌，复制为模拟令牌；
    /// 2. SetThreadToken 暂时模拟 winlogon（拥有 TCB 权限）；
    /// 3. 在自身令牌副本上 SetTokenInformation(TokenUIAccess, TRUE)；
    /// 4. RevertToSelf 后用 CreateProcessWithTokenW 启动新进程；
    /// 5. 新进程具有 UIAccess 权限，可置顶于 UAC 提示之上。
    /// </summary>
    public static class UIAccessHelper
    {
        #region Constants

        private const uint TOKEN_QUERY = 0x0008;
        private const uint TOKEN_DUPLICATE = 0x0002;
        private const uint TOKEN_IMPERSONATE = 0x0004;
        private const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
        private const uint TOKEN_ADJUST_DEFAULT = 0x0080;
        private const uint TOKEN_ADJUST_SESSIONID = 0x0100;
        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;

        private const int SecurityAnonymous = 0;
        private const int SecurityImpersonation = 2;
        private const int TokenPrimary = 1;
        private const int TokenImpersonation = 2;

        // TOKEN_INFORMATION_CLASS
        private const int TokenSessionId = 12;
        private const int TokenElevationType = 18;
        private const int TokenUIAccess = 26;

        // TOKEN_ELEVATION_TYPE
        private const int TokenElevationTypeDefault = 1;
        private const int TokenElevationTypeFull = 2;
        private const int TokenElevationTypeLimited = 3;

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint TH32CS_SNAPPROCESS = 0x00000002;

        private const uint LOGON_WITH_PROFILE = 0x00000001;
        private const uint CREATE_NEW_CONSOLE = 0x00000010;
        private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;

        private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
        private const string SE_ASSIGNPRIMARYTOKEN_NAME = "SeAssignPrimaryTokenPrivilege";

        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        #endregion

        #region Structs

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID_AND_ATTRIBUTES Privilege;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PROCESSENTRY32W
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFOW
        {
            public uint cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public uint dwX, dwY, dwXSize, dwYSize;
            public uint dwXCountChars, dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public ushort wShowWindow;
            public ushort cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        #endregion

        #region P/Invoke

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DuplicateTokenEx(
            IntPtr hExistingToken,
            uint dwDesiredAccess,
            IntPtr lpTokenAttributes,
            int ImpersonationLevel,
            int TokenType,
            out IntPtr phNewToken);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetTokenInformation(
            IntPtr TokenHandle,
            int TokenInformationClass,
            IntPtr TokenInformation,
            uint TokenInformationLength,
            out uint ReturnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetTokenInformation(
            IntPtr TokenHandle,
            int TokenInformationClass,
            IntPtr TokenInformation,
            uint TokenInformationLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetThreadToken(IntPtr Thread, IntPtr Token);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RevertToSelf();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32FirstW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32NextW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValueW(string lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(
            IntPtr TokenHandle,
            [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState,
            uint BufferLength,
            IntPtr PreviousState,
            IntPtr ReturnLength);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateProcessWithTokenW(
            IntPtr hToken,
            uint dwLogonFlags,
            string lpApplicationName,
            StringBuilder lpCommandLine,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFOW lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern void GetStartupInfoW(ref STARTUPINFOW lpStartupInfo);

        #endregion

        #region Public API

        /// <summary>
        /// 检查当前进程是否已具有 UIAccess 标志。
        /// </summary>
        public static bool HasUIAccess()
        {
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, out IntPtr hToken))
                return false;

            try
            {
                IntPtr buf = Marshal.AllocHGlobal(sizeof(uint));
                try
                {
                    Marshal.WriteInt32(buf, 0);
                    if (!GetTokenInformation(hToken, TokenUIAccess, buf, sizeof(uint), out _))
                        return false;
                    return Marshal.ReadInt32(buf) != 0;
                }
                finally
                {
                    Marshal.FreeHGlobal(buf);
                }
            }
            finally
            {
                CloseHandle(hToken);
            }
        }

        /// <summary>
        /// 以 UIAccess 令牌重启自身。当前进程必须已经以管理员身份运行。
        /// 成功时新进程已启动，调用方应立即退出当前进程。
        /// </summary>
        /// <param name="extraArgs">追加到新进程的额外命令行参数（例如 --skip-mutex-check）。</param>
        public static bool RestartWithUIAccess(string extraArgs = null)
        {
            try
            {
                if (HasUIAccess())
                {
                    LogHelper.WriteLogToFile("UIAccess | 当前进程已具有 UIAccess，跳过重启");
                    return true;
                }

                if (!CreateUIAccessToken(out IntPtr uiaToken))
                {
                    LogHelper.WriteLogToFile($"UIAccess | 创建 UIAccess 令牌失败 (LastError={Marshal.GetLastWin32Error()})", LogHelper.LogType.Error);
                    return false;
                }

                try
                {
                    return LaunchWithToken(uiaToken, extraArgs);
                }
                finally
                {
                    CloseHandle(uiaToken);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"UIAccess | RestartWithUIAccess 异常: {ex}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 以普通用户权限（非提升）重启自身。
        /// 通过获取 explorer.exe / ctfmon.exe 的非特权令牌，再用 CreateProcessWithTokenW 启动新进程，
        /// 避免经由 explorer.exe 中转可能产生的 UAC 提示或丢失参数问题。
        /// 成功时调用方应立即退出当前进程。
        /// </summary>
        /// <param name="extraArgs">追加到新进程的额外命令行参数。</param>
        public static bool RestartAsNormalUser(string extraArgs = null)
        {
            try
            {
                if (!GetUserPrimaryToken(out IntPtr userToken))
                {
                    LogHelper.WriteLogToFile($"UIAccess | 获取用户令牌失败 (LastError={Marshal.GetLastWin32Error()})", LogHelper.LogType.Error);
                    return false;
                }

                try
                {
                    return LaunchWithToken(userToken, extraArgs);
                }
                finally
                {
                    CloseHandle(userToken);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"UIAccess | RestartAsNormalUser 异常: {ex}", LogHelper.LogType.Error);
                return false;
            }
        }

        #endregion

        #region Token Manipulation

        private static bool CreateUIAccessToken(out IntPtr uiaToken)
        {
            uiaToken = IntPtr.Zero;

            // 1. 获取当前进程的 session id
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, out IntPtr hSelfQuery))
            {
                LogHelper.WriteLogToFile($"UIAccess | OpenProcessToken(query) 失败: {Marshal.GetLastWin32Error()}", LogHelper.LogType.Error);
                return false;
            }

            uint sessionId;
            try
            {
                IntPtr sesBuf = Marshal.AllocHGlobal(sizeof(uint));
                try
                {
                    if (!GetTokenInformation(hSelfQuery, TokenSessionId, sesBuf, sizeof(uint), out _))
                    {
                        LogHelper.WriteLogToFile($"UIAccess | GetTokenInformation(SessionId) 失败: {Marshal.GetLastWin32Error()}", LogHelper.LogType.Error);
                        return false;
                    }
                    sessionId = (uint)Marshal.ReadInt32(sesBuf);
                }
                finally { Marshal.FreeHGlobal(sesBuf); }
            }
            finally { CloseHandle(hSelfQuery); }

            // 2. 找到同一会话的 winlogon 模拟令牌
            if (!GetWinlogonImpersonationToken(sessionId, out IntPtr winlogonToken))
            {
                LogHelper.WriteLogToFile("UIAccess | 未能获取 winlogon 模拟令牌（需要管理员权限）", LogHelper.LogType.Error);
                return false;
            }

            try
            {
                // 3. 模拟 winlogon
                if (!SetThreadToken(IntPtr.Zero, winlogonToken))
                {
                    LogHelper.WriteLogToFile($"UIAccess | SetThreadToken(winlogon) 失败: {Marshal.GetLastWin32Error()}", LogHelper.LogType.Error);
                    return false;
                }

                try
                {
                    // 4. 复制自身令牌为主令牌
                    if (!OpenProcessToken(GetCurrentProcess(), TOKEN_DUPLICATE, out IntPtr hSelfDup))
                    {
                        LogHelper.WriteLogToFile($"UIAccess | OpenProcessToken(dup) 失败: {Marshal.GetLastWin32Error()}", LogHelper.LogType.Error);
                        return false;
                    }

                    IntPtr dupToken;
                    try
                    {
                        bool ok = DuplicateTokenEx(
                            hSelfDup,
                            TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_QUERY | TOKEN_ADJUST_DEFAULT | TOKEN_ADJUST_SESSIONID,
                            IntPtr.Zero,
                            SecurityAnonymous,
                            TokenPrimary,
                            out dupToken);

                        if (!ok)
                        {
                            LogHelper.WriteLogToFile($"UIAccess | DuplicateTokenEx 失败: {Marshal.GetLastWin32Error()}", LogHelper.LogType.Error);
                            return false;
                        }
                    }
                    finally { CloseHandle(hSelfDup); }

                    // 5. 在副本上设置 UIAccess = TRUE
                    IntPtr uiBuf = Marshal.AllocHGlobal(sizeof(uint));
                    try
                    {
                        Marshal.WriteInt32(uiBuf, 1);
                        if (!SetTokenInformation(dupToken, TokenUIAccess, uiBuf, sizeof(uint)))
                        {
                            int err = Marshal.GetLastWin32Error();
                            LogHelper.WriteLogToFile($"UIAccess | SetTokenInformation(UIAccess) 失败: {err}", LogHelper.LogType.Error);
                            CloseHandle(dupToken);
                            return false;
                        }
                    }
                    finally { Marshal.FreeHGlobal(uiBuf); }

                    uiaToken = dupToken;
                    return true;
                }
                finally
                {
                    RevertToSelf();
                }
            }
            finally
            {
                CloseHandle(winlogonToken);
            }
        }

        private static bool GetWinlogonImpersonationToken(uint sessionId, out IntPtr winlogonToken)
        {
            winlogonToken = IntPtr.Zero;

            IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snapshot == INVALID_HANDLE_VALUE || snapshot == IntPtr.Zero)
            {
                LogHelper.WriteLogToFile($"UIAccess | CreateToolhelp32Snapshot 失败: {Marshal.GetLastWin32Error()}", LogHelper.LogType.Error);
                return false;
            }

            try
            {
                var pe = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32W)) };
                bool more = Process32FirstW(snapshot, ref pe);

                while (more)
                {
                    if (string.Equals(pe.szExeFile, "winlogon.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryDuplicateWinlogonToken(pe.th32ProcessID, sessionId, out winlogonToken))
                            return true;
                    }
                    more = Process32NextW(snapshot, ref pe);
                }
            }
            finally { CloseHandle(snapshot); }

            return false;
        }

        private static bool TryDuplicateWinlogonToken(uint pid, uint sessionId, out IntPtr dupToken)
        {
            dupToken = IntPtr.Zero;

            IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProc == IntPtr.Zero) return false;

            try
            {
                if (!OpenProcessToken(hProc, TOKEN_QUERY | TOKEN_DUPLICATE, out IntPtr hToken))
                    return false;

                try
                {
                    // 检查 session id 匹配
                    IntPtr sesBuf = Marshal.AllocHGlobal(sizeof(uint));
                    try
                    {
                        if (!GetTokenInformation(hToken, TokenSessionId, sesBuf, sizeof(uint), out _))
                            return false;
                        if ((uint)Marshal.ReadInt32(sesBuf) != sessionId)
                            return false;
                    }
                    finally { Marshal.FreeHGlobal(sesBuf); }

                    if (!DuplicateTokenEx(
                        hToken,
                        TOKEN_IMPERSONATE | TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY | TOKEN_DUPLICATE,
                        IntPtr.Zero,
                        SecurityImpersonation,
                        TokenImpersonation,
                        out dupToken))
                    {
                        return false;
                    }

                    // 启用 SeAssignPrimaryTokenPrivilege（Inkeys 行为）
                    var tkp = new TOKEN_PRIVILEGES
                    {
                        PrivilegeCount = 1,
                        Privilege = new LUID_AND_ATTRIBUTES { Attributes = SE_PRIVILEGE_ENABLED }
                    };
                    if (LookupPrivilegeValueW(null, SE_ASSIGNPRIMARYTOKEN_NAME, out tkp.Privilege.Luid))
                    {
                        AdjustTokenPrivileges(dupToken, false, ref tkp, (uint)Marshal.SizeOf(tkp), IntPtr.Zero, IntPtr.Zero);
                    }

                    return true;
                }
                finally { CloseHandle(hToken); }
            }
            finally { CloseHandle(hProc); }
        }

        /// <summary>
        /// 从 explorer.exe / ctfmon.exe 取得普通用户（非提升）令牌的主令牌副本，用于降权启动。
        /// 仅当当前进程为管理员时才能成功。
        /// </summary>
        private static bool GetUserPrimaryToken(out IntPtr userToken)
        {
            userToken = IntPtr.Zero;

            string[] candidates = { "explorer.exe", "ctfmon.exe" };
            foreach (var name in candidates)
            {
                IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
                if (snapshot == INVALID_HANDLE_VALUE || snapshot == IntPtr.Zero) continue;

                try
                {
                    var pe = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32W)) };
                    bool more = Process32FirstW(snapshot, ref pe);
                    while (more)
                    {
                        if (string.Equals(pe.szExeFile, name, StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryDuplicateUserPrimaryToken(pe.th32ProcessID, out userToken))
                            {
                                LogHelper.WriteLogToFile($"UIAccess | 已从 {name} (PID={pe.th32ProcessID}) 取得用户令牌");
                                return true;
                            }
                        }
                        more = Process32NextW(snapshot, ref pe);
                    }
                }
                finally { CloseHandle(snapshot); }
            }

            return false;
        }

        private static bool TryDuplicateUserPrimaryToken(uint pid, out IntPtr dupToken)
        {
            dupToken = IntPtr.Zero;

            IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProc == IntPtr.Zero) return false;

            try
            {
                if (!OpenProcessToken(hProc, TOKEN_QUERY | TOKEN_DUPLICATE, out IntPtr hToken))
                    return false;

                try
                {
                    // 仅接受非提升令牌（否则降权失败）
                    IntPtr elevBuf = Marshal.AllocHGlobal(sizeof(int));
                    try
                    {
                        if (!GetTokenInformation(hToken, TokenElevationType, elevBuf, sizeof(int), out _))
                            return false;
                        int elev = Marshal.ReadInt32(elevBuf);
                        if (elev == TokenElevationTypeFull)
                            return false; // 该进程是提升令牌，跳过
                    }
                    finally { Marshal.FreeHGlobal(elevBuf); }

                    return DuplicateTokenEx(
                        hToken,
                        TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_QUERY | TOKEN_ADJUST_DEFAULT | TOKEN_ADJUST_SESSIONID,
                        IntPtr.Zero,
                        SecurityAnonymous,
                        TokenPrimary,
                        out dupToken);
                }
                finally { CloseHandle(hToken); }
            }
            finally { CloseHandle(hProc); }
        }

        #endregion

        #region Process Launch

        private static bool LaunchWithToken(IntPtr token, string extraArgs)
        {
            string exePath = Process.GetCurrentProcess().MainModule.FileName;
            string workDir = System.IO.Path.GetDirectoryName(exePath);

            // 重建命令行：保留原始参数，追加 --skip-mutex-check 防止单实例阻塞
            var cmdBuilder = new StringBuilder(32768);
            cmdBuilder.Append('"').Append(exePath).Append('"');

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                cmdBuilder.Append(' ');
                AppendQuoted(cmdBuilder, args[i]);
            }

            if (!string.IsNullOrEmpty(extraArgs))
                cmdBuilder.Append(' ').Append(extraArgs);

            // 防止单实例 Mutex 阻塞新进程
            if (Array.IndexOf(args, "--skip-mutex-check") < 0
                && (extraArgs == null || extraArgs.IndexOf("--skip-mutex-check", StringComparison.Ordinal) < 0))
            {
                cmdBuilder.Append(" --skip-mutex-check");
            }

            var si = new STARTUPINFOW { cb = (uint)Marshal.SizeOf(typeof(STARTUPINFOW)) };
            GetStartupInfoW(ref si);

            bool ok = CreateProcessWithTokenW(
                token,
                LOGON_WITH_PROFILE,
                null,
                cmdBuilder,
                CREATE_UNICODE_ENVIRONMENT,
                IntPtr.Zero,
                workDir,
                ref si,
                out PROCESS_INFORMATION pi);

            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                LogHelper.WriteLogToFile($"UIAccess | CreateProcessWithTokenW 失败: {err}", LogHelper.LogType.Error);
                return false;
            }

            CloseHandle(pi.hProcess);
            CloseHandle(pi.hThread);
            LogHelper.WriteLogToFile($"UIAccess | 已使用 UIAccess 令牌启动新进程 (PID={pi.dwProcessId})");
            return true;
        }

        private static void AppendQuoted(StringBuilder sb, string arg)
        {
            if (arg == null) { sb.Append("\"\""); return; }

            bool needQuote = arg.Length == 0 || arg.IndexOfAny(new[] { ' ', '\t', '"' }) >= 0;
            if (!needQuote) { sb.Append(arg); return; }

            sb.Append('"');
            int backslashes = 0;
            foreach (char c in arg)
            {
                if (c == '\\') { backslashes++; continue; }
                if (c == '"')
                {
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                }
                else
                {
                    sb.Append('\\', backslashes);
                    sb.Append(c);
                }
                backslashes = 0;
            }
            sb.Append('\\', backslashes * 2);
            sb.Append('"');
        }

        #endregion
    }
}