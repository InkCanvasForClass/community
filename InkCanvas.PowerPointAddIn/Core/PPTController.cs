using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using InkCanvasPPTAgent.Contracts;
using Newtonsoft.Json;

namespace InkCanvas.PowerPointAddIn.Core
{
    public sealed class PPTController
    {
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }
        private readonly Microsoft.Office.Interop.PowerPoint.Application _application;
        private SynchronizationContext _syncContext;

        public PPTController(Microsoft.Office.Interop.PowerPoint.Application application)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            CaptureSyncContext();
        }

        public PPTState GetState()
        {
            var state = new PPTState();

            try
            {
                if (_application.Presentations.Count > 0)
                {
                    var pres = _application.ActivePresentation;
                    state.PresentationName = pres.Name;
                    try { state.PresentationFullName = pres.FullName; } catch { }
                    state.TotalSlides = pres.Slides.Count;
                    state.HasHiddenSlides = HasHiddenSlides(pres);
                    state.HasAutoPlayTimings = HasAutoPlayTimings(pres);
                }
            }
            catch { }

            try
            {
                if (_application.SlideShowWindows.Count > 0)
                {
                    state.IsRunning = true;
                    state.SlideIndex = _application.SlideShowWindows[1].View.CurrentShowPosition;
                }
            }
            catch { }

            return state;
        }

        public bool Next()
        {
            return Run(() =>
            {
                if (_application.SlideShowWindows.Count <= 0) return false;
                _application.SlideShowWindows[1].View.Next();
                return true;
            });
        }

        public bool Previous()
        {
            return Run(() =>
            {
                if (_application.SlideShowWindows.Count <= 0) return false;
                _application.SlideShowWindows[1].View.Previous();
                return true;
            });
        }

        public bool GotoSlide(int slideNumber)
        {
            return Run(() =>
            {
                if (slideNumber <= 0) return false;
                if (_application.SlideShowWindows.Count <= 0) return false;
                _application.SlideShowWindows[1].View.GotoSlide(slideNumber);
                return true;
            });
        }

        public bool StartSlideShow()
        {
            return Run(() =>
            {
                if (_application.Presentations.Count <= 0) return false;
                _application.ActivePresentation.SlideShowSettings.Run();
                return true;
            });
        }

        public bool EndSlideShow()
        {
            return Run(() =>
            {
                if (_application.SlideShowWindows.Count <= 0) return false;
                _application.SlideShowWindows[1].View.Exit();
                return true;
            });
        }

        public bool ShowSlideNavigation()
        {
            return Run(() =>
            {
                if (_application.SlideShowWindows.Count <= 0) return false;
                try
                {
                    dynamic nav = _application.SlideShowWindows[1].SlideNavigation;
                    if (nav == null) return false;
                    nav.Visible = true;
                    return true;
                }
                catch { return false; }
            });
        }

        public bool DisableAutoPlayTimings()
        {
            return Run(() =>
            {
                if (_application.Presentations.Count <= 0) return false;
                _application.ActivePresentation.SlideShowSettings.AdvanceMode =
                    Microsoft.Office.Interop.PowerPoint.PpSlideShowAdvanceMode.ppSlideShowManualAdvance;
                return true;
            });
        }

        public bool UnhideHiddenSlides()
        {
            return Run(() =>
            {
                if (_application.Presentations.Count <= 0) return false;
                foreach (Microsoft.Office.Interop.PowerPoint.Slide slide in _application.ActivePresentation.Slides)
                {
                    if (slide.SlideShowTransition.Hidden == Microsoft.Office.Core.MsoTriState.msoTrue)
                        slide.SlideShowTransition.Hidden = Microsoft.Office.Core.MsoTriState.msoFalse;
                }
                return true;
            });
        }

        private void CaptureSyncContext()
        {
            if (SynchronizationContext.Current != null)
                _syncContext = SynchronizationContext.Current;
        }

        private bool Run(Func<bool> action)
        {
            if (_syncContext != null)
            {
                bool result = false;
                Exception captured = null;
                _syncContext.Send(_ =>
                {
                    try { result = action(); }
                    catch (Exception ex) { captured = ex; }
                }, null);
                if (captured != null) throw captured;
                return result;
            }
            return action.Invoke();
        }

        private T Run<T>(Func<T> action)
        {
            if (_syncContext != null)
            {
                T result = default;
                Exception captured = null;
                _syncContext.Send(_ =>
                {
                    try { result = action(); }
                    catch (Exception ex) { captured = ex; }
                }, null);
                if (captured != null) throw captured;
                return result;
            }
            return action.Invoke();
        }

        public SmartRegionsResponse GetSmartRegions()
        {
            return Run(() =>
            {
                var response = new SmartRegionsResponse();

                if (_application.SlideShowWindows.Count <= 0)
                    return response;

                var ssw = _application.SlideShowWindows[1];
                var view = ssw.View;
                if (view == null) return response;

                response.SlideIndex = view.CurrentShowPosition;

                var slide = view.Slide;
                if (slide == null) return response;

                // 使用当前放映窗口自身的 HWND，避免 FindWindow 命中其他屏幕或陈旧的放映窗口。
                IntPtr hwnd = new IntPtr(ssw.HWND);
                response.SlideShowWindowHandle = hwnd.ToInt64();

                double winLeft, winTop, winWidth, winHeight;
                uint dpi = 96;
                if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out RECT rect))
                {
                    winLeft = rect.Left;
                    winTop = rect.Top;
                    winWidth = rect.Right - rect.Left;
                    winHeight = rect.Bottom - rect.Top;
                    try { dpi = GetDpiForWindow(hwnd); } catch { dpi = 96; }
                }
                else
                {
                    // 回退：使用 COM 属性（单位为磅，需要 DPI 转换）
                    double dpiFactor = 96.0 / 72.0; // 默认假设 100% DPI
                    winLeft = ssw.Left * dpiFactor;
                    winTop = ssw.Top * dpiFactor;
                    winWidth = ssw.Width * dpiFactor;
                    winHeight = ssw.Height * dpiFactor;
                }

                var pres = ssw.Presentation;
                float slideWidth = pres.PageSetup.SlideWidth;   // 磅
                float slideHeight = pres.PageSetup.SlideHeight; // 磅
                response.SlideWidth = slideWidth;
                response.SlideHeight = slideHeight;

                // 仅识别视频控件（ppMediaTypeMovie = 3，排除音频与普通 OLE 控件）
                foreach (Microsoft.Office.Interop.PowerPoint.Shape shape in slide.Shapes)
                {
                    if (!IsVideoShape(shape)) continue;

                    try
                    {
                        var region = new SmartRegion
                        {
                            X = shape.Left,
                            Y = shape.Top,
                            Width = shape.Width,
                            Height = shape.Height,
                            ShapeName = shape.Name,
                            MediaType = (int)shape.MediaType
                        };
                        response.Regions.Add(region);
                    }
                    catch
                    {
                        // 部分 Shape 的属性可能不可访问，跳过
                    }
                }

                return response;
            }) ?? new SmartRegionsResponse();
        }

        private static bool IsVideoShape(Microsoft.Office.Interop.PowerPoint.Shape shape)
        {
            try
            {
                // msoWebVideo = 26：在线视频（YouTube 等），本身就是视频控件
                if (shape.Type == Microsoft.Office.Core.MsoShapeType.msoWebVideo)
                    return true;

                // msoMedia = 16：多媒体形状，用 MediaType 区分视频/音频
                if (shape.Type == Microsoft.Office.Core.MsoShapeType.msoMedia)
                {
                    try
                    {
                        int mediaType = (int)(object)shape.MediaType;
                        // ppMediaTypeMovie = 3（视频）；raw 15 为旧版 Flash（ppMediaTypeFlash），亦属视频类
                        return mediaType == 3 || mediaType == 15;
                    }
                    catch
                    {
                        // MediaType 读取失败时保守放行（保持原行为）
                        return true;
                    }
                }

                // msoOLEControlObject = 12：ActiveX 控件，仅当确认为媒体播放器时才视为视频
                if (shape.Type == Microsoft.Office.Core.MsoShapeType.msoOLEControlObject)
                {
                    try
                    {
                        string progId = shape.OLEFormat?.ProgID ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(progId))
                        {
                            progId = progId.ToUpperInvariant();
                            // Windows Media Player / VLC / Flash / RealPlayer 等媒体播放器控件
                            if (progId.StartsWith("WMPlayer.", StringComparison.Ordinal) ||
                                progId.StartsWith("VideoLAN.", StringComparison.Ordinal) ||
                                progId.StartsWith("ShockwaveFlash.", StringComparison.Ordinal) ||
                                progId.StartsWith("RealPlayer.", StringComparison.Ordinal) ||
                                progId.StartsWith("RealMedia.", StringComparison.Ordinal))
                                return true;
                        }
                    }
                    catch { }
                    // 无法确认是否为媒体播放器时，不视为视频，避免把普通 ActiveX 控件误判为视频
                    return false;
                }

                // msoEmbeddedOLEObject = 7：嵌入式 OLE，旧版视频格式，MediaType 必须为 ppMediaTypeMovie = 3
                if (shape.Type == Microsoft.Office.Core.MsoShapeType.msoEmbeddedOLEObject)
                {
                    try
                    {
                        if ((int)(object)shape.MediaType == 3)  // ppMediaTypeMovie
                            return true;
                    }
                    catch { }
                }
            }
            catch { }

            return false;
        }

        private static bool HasHiddenSlides(Microsoft.Office.Interop.PowerPoint.Presentation pres)
        {
            try
            {
                foreach (Microsoft.Office.Interop.PowerPoint.Slide slide in pres.Slides)
                {
                    if (slide.SlideShowTransition.Hidden == Microsoft.Office.Core.MsoTriState.msoTrue)
                        return true;
                }
            }
            catch { }
            return false;
        }

        private static bool HasAutoPlayTimings(Microsoft.Office.Interop.PowerPoint.Presentation pres)
        {
            try
            {
                foreach (Microsoft.Office.Interop.PowerPoint.Slide slide in pres.Slides)
                {
                    if (slide.SlideShowTransition.AdvanceOnTime == Microsoft.Office.Core.MsoTriState.msoTrue &&
                        slide.SlideShowTransition.AdvanceTime > 0)
                        return true;
                }
            }
            catch { }
            return false;
        }
    }
}
