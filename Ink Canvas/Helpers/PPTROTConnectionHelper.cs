using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Ink_Canvas.Helpers
{
    public static class PPTROTConnectionHelper
    {
        #region Win32 API Declarations
        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        #endregion

        #region Constants
        private static readonly Guid PowerPointApplicationGuid = new Guid("91493441-5A91-11CF-8700-00AA0060263B");

        private static readonly string[] PptLikeExtensions = new[]
        {
            ".pptx", ".pptm", ".ppt",
            ".ppsx", ".ppsm", ".pps",
            ".potx", ".potm", ".pot",
            ".dps", ".dpt"
        };
        #endregion

        #region Public Methods
        public static Microsoft.Office.Interop.PowerPoint.Application TryConnectViaROT(bool isSupportWPS = false)
        {
            try
            {
                object bestApp = GetAnyActivePowerPoint(null, out int bestPriority, out _);

                if (bestApp != null && bestPriority > 0)
                {
                    try
                    {
                        Type appType = typeof(Microsoft.Office.Interop.PowerPoint.Application);
                        Microsoft.Office.Interop.PowerPoint.Application pptApp = null;
                        if (appType.IsInstanceOfType(bestApp))
                        {
                            pptApp = (Microsoft.Office.Interop.PowerPoint.Application)bestApp;
                        }
                        if (pptApp != null)
                        {
                            try
                            {
                                var nameObj = pptApp.GetType().InvokeMember("Name", BindingFlags.GetProperty, null, pptApp, null);
                                SafeReleaseComObject(nameObj);
                                return pptApp;
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"ROT 连接验证 Name 不可用（将依赖 SlideShowWindows）: {ex.Message}", LogHelper.LogType.Warning);
                                return pptApp;
                            }
                        }
                        else
                        {
                            SafeReleaseComObject(bestApp);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"ROT 连接验证失败: {ex.Message}", LogHelper.LogType.Warning);
                        SafeReleaseComObject(bestApp);
                    }
                }
                else if (bestApp != null)
                {
                    SafeReleaseComObject(bestApp);
                }

                try
                {
                    var pptApp = (Microsoft.Office.Interop.PowerPoint.Application)Marshal.GetActiveObject("PowerPoint.Application");
                    if (pptApp != null && Marshal.IsComObject(pptApp))
                    {
                        try
                        {
                            var _ = pptApp.Name;
                        }
                        catch (COMException)
                        {
                        }
                        return pptApp;
                    }
                }
                catch (COMException) { }
                catch (InvalidCastException) { }

                if (isSupportWPS)
                {
                    try
                    {
                        var wpsApp = (Microsoft.Office.Interop.PowerPoint.Application)Marshal.GetActiveObject("kwpp.Application");
                        if (wpsApp != null && Marshal.IsComObject(wpsApp))
                        {
                            try
                            {
                                var _ = wpsApp.Name;
                            }
                            catch (COMException)
                            {
                            }
                            return wpsApp;
                        }
                    }
                    catch (COMException) { }
                    catch (InvalidCastException) { }
                }

                return null;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ROT 连接过程发生异常: {ex}", LogHelper.LogType.Error);
                return null;
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 在系统的运行对象表（ROT）中查找并返回最合适的正在运行的 PowerPoint 应用实例。
        /// </summary>
        /// <param name="targetApp">可选的目标 PowerPoint COM 对象，用于优先比较；传入 null 表示不指定目标。</param>
        /// <param name="bestPriority">输出参数：返回找到的最佳实例的优先级（0 表示未找到或无活动演示）。</param>
        /// <param name="targetPriority">输出参数：返回与 <paramref name="targetApp"/> 对应实例的优先级（如果未提供或未命中则为 0）。</param>
        /// <returns>最合适的 PowerPoint 应用对象（通常为 COM Application 实例），若未找到则返回 null。</returns>
        public static object GetAnyActivePowerPoint(object targetApp, out int bestPriority, out int targetPriority)
        {
            IRunningObjectTable rot = null;
            IEnumMoniker enumMoniker = null;

            object bestApp = null;
            bestPriority = 0;
            targetPriority = 0;
            int highestPriority = 0;

            List<object> foundAppObjects = new List<object>();

            try
            {
                int hr = GetRunningObjectTable(0, out rot);
                if (hr != 0 || rot == null)
                {
                    LogHelper.WriteLogToFile("无法获取 Running Object Table", LogHelper.LogType.Warning);
                    return null;
                }

                rot.EnumRunning(out enumMoniker);
                if (enumMoniker == null)
                {
                    LogHelper.WriteLogToFile("无法枚举 ROT 中的对象", LogHelper.LogType.Warning);
                    return null;
                }

                IMoniker[] moniker = new IMoniker[1];
                IntPtr fetched = IntPtr.Zero;

                while (enumMoniker.Next(1, moniker, fetched) == 0)
                {
                    IBindCtx bindCtx = null;
                    object comObject = null;
                    dynamic candidateApp = null;
                    string displayName = "Unknown";
                    dynamic activePres = null;
                    dynamic ssWindow = null;
                    bool keepAlive = false;

                    try
                    {
                        CreateBindCtx(0, out bindCtx);
                        moniker[0].GetDisplayName(bindCtx, null, out displayName);

                        if (LooksLikePresentationFile(displayName) || displayName == "!{91493441-5A91-11CF-8700-00AA0060263B}")
                        {
                            rot.GetObject(moniker[0], out comObject);
                            if (comObject != null)
                            {
                                try
                                {
                                    object appObj = comObject.GetType().InvokeMember("Application", BindingFlags.GetProperty, null, comObject, null);
                                    candidateApp = appObj;
                                }
                                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                            }
                        }
                        bool isDuplicate = false;
                        if (candidateApp != null)
                        {
                            foreach (var processedApp in foundAppObjects)
                            {
                                if (AreComObjectsEqual((object)candidateApp, processedApp))
                                {
                                    isDuplicate = true;
                                    break;
                                }
                            }

                            if (!isDuplicate)
                            {
                                foundAppObjects.Add(candidateApp);
                                keepAlive = true;
                            }
                        }

                        if (candidateApp != null && !isDuplicate)
                        {
                            int currentPriority = 0;
                            bool isTarget = false;

                            if (targetApp != null && AreComObjectsEqual((object)candidateApp, targetApp))
                            {
                                isTarget = true;
                            }

                            try
                            {
                                try
                                {
                                    activePres = candidateApp.ActivePresentation;
                                }
                                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

                                if (activePres != null)
                                {
                                    currentPriority = 1;

                                    try
                                    {
                                        ssWindow = activePres.SlideShowWindow;
                                    }
                                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

                                    if (ssWindow != null)
                                    {
                                        currentPriority = 2;

                                        try
                                        {
                                            bool isActive = false;
                                            try
                                            {
                                                object val = ssWindow.Active;
                                                if (val is int && (int)val == -1) isActive = true;
                                                else if (val is bool && (bool)val == true) isActive = true;
                                            }
                                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

                                            if (isActive)
                                            {
                                                currentPriority = 3;
                                            }
                                            else
                                            {
                                                if (IsSlideShowWindowActive(ssWindow))
                                                {
                                                    currentPriority = 3;
                                                }
                                            }
                                        }
                                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"计算优先级时出错: {ex.Message}", LogHelper.LogType.Warning);
                            }

                            if (isTarget)
                            {
                                targetPriority = currentPriority;
                            }

                            if (currentPriority > 0)
                            {
                                if (currentPriority > highestPriority)
                                {
                                    highestPriority = currentPriority;
                                    SafeReleaseComObject(bestApp);
                                    bestApp = candidateApp;
                                    candidateApp = null;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"ROT 枚举循环中出错: {ex.Message}", LogHelper.LogType.Warning);
                    }
                    finally
                    {
                        SafeReleaseComObject(ssWindow);
                        SafeReleaseComObject(activePres);

                        if (!keepAlive)
                        {
                            SafeReleaseComObject(candidateApp);
                        }

                        CleanUpLoopObjects(bindCtx, moniker[0], comObject);
                    }
                }

                bestPriority = highestPriority;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ROT 扫描关键错误: {ex}", LogHelper.LogType.Error);
            }
            finally
            {
                if (foundAppObjects != null)
                {
                    foreach (var cachedApp in foundAppObjects)
                    {
                        if (bestApp != null && ReferenceEquals(cachedApp, bestApp))
                            continue;

                        SafeReleaseComObject(cachedApp);
                    }
                    foundAppObjects.Clear();
                }

                if (enumMoniker != null) Marshal.ReleaseComObject(enumMoniker);
                if (rot != null) Marshal.ReleaseComObject(rot);
            }

            return bestApp;
        }

        public static bool AreComObjectsEqual(object o1, object o2)
        {
            if (o1 == null || o2 == null) return false;
            if (ReferenceEquals(o1, o2)) return true;

            IntPtr pUnk1 = IntPtr.Zero;
            IntPtr pUnk2 = IntPtr.Zero;
            try
            {
                pUnk1 = Marshal.GetIUnknownForObject(o1);
                pUnk2 = Marshal.GetIUnknownForObject(o2);
                return pUnk1 == pUnk2;
            }
            catch { return false; }
            finally
            {
                if (pUnk1 != IntPtr.Zero) Marshal.Release(pUnk1);
                if (pUnk2 != IntPtr.Zero) Marshal.Release(pUnk2);
            }
        }

        private static bool LooksLikePresentationFile(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
                return false;

            string lower = displayName.ToLowerInvariant();
            foreach (var ext in PptLikeExtensions)
            {
                if (lower.Contains(ext))
                    return true;
            }
            return false;
        }

        public static bool IsSlideShowWindowActive(object sswObj)
        {
            try
            {
                dynamic ssw = sswObj;

                IntPtr foregroundHwnd = GetForegroundWindow();
                if (foregroundHwnd == IntPtr.Zero) return false;

                uint fgPid;
                GetWindowThreadProcessId(foregroundHwnd, out fgPid);

                IntPtr sswHwnd = IntPtr.Zero;
                try
                {
                    sswHwnd = GetPptHwndFromSlideShowWindow(sswObj);
                }
                catch { return false; }
                if (sswHwnd == IntPtr.Zero) return false;

                uint sswPid;
                GetWindowThreadProcessId(sswHwnd, out sswPid);

                if (fgPid == sswPid) return true;

                try
                {
                    using (Process fgProc = Process.GetProcessById((int)fgPid))
                    using (Process appProc = Process.GetProcessById((int)sswPid))
                    {
                        string fgName = fgProc.ProcessName.ToLower();
                        string appName = appProc.ProcessName.ToLower();

                        if (fgName.StartsWith("wps") && appName.StartsWith("wpp"))
                        {
                            return true;
                        }
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static IntPtr GetPptHwndFromSlideShowWindow(object pptSlideShowWindowObj)
        {
            IntPtr hwnd = IntPtr.Zero;
            if (pptSlideShowWindowObj == null) return IntPtr.Zero;

            try
            {
                Microsoft.Office.Interop.PowerPoint.SlideShowWindow slideWindow = (Microsoft.Office.Interop.PowerPoint.SlideShowWindow)pptSlideShowWindowObj;

                int hwndVal = slideWindow.HWND;

                hwnd = new IntPtr(hwndVal);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            return hwnd;
        }

        public static void SafeReleaseComObject(object comObj)
        {
            if (comObj == null) return;

            if (Marshal.IsComObject(comObj))
            {
                try
                {
                    Marshal.ReleaseComObject(comObj);
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            }
        }

        private static void CleanUpLoopObjects(IBindCtx bindCtx, IMoniker moniker, object comObject)
        {
            if (comObject != null && Marshal.IsComObject(comObject))
                Marshal.ReleaseComObject(comObject);
            if (moniker != null)
                Marshal.ReleaseComObject(moniker);
            if (bindCtx != null)
                Marshal.ReleaseComObject(bindCtx);
        }

        public static int GetSlideShowWindowsCount(Microsoft.Office.Interop.PowerPoint.Application pptApp)
        {
            try
            {
                if (pptApp == null) return 0;
                return pptApp.SlideShowWindows.Count;
            }
            catch
            {
                return 0;
            }
        }

        public static bool IsValidSlideShowWindow(object pptSlideShowWindow)
        {
            if (pptSlideShowWindow == null) return false;

            try
            {
                dynamic ssw = pptSlideShowWindow;
                var _ = ssw.Active;
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }
}
