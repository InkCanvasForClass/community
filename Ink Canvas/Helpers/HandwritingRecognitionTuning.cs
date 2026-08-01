using System;
using System.Collections.Generic;
using System.Globalization;
using WinRtInk = global::Windows.UI.Input.Inking;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 集中管理 WinRT 手写识别的「识别器选择 / LCID 意图 / FOD 检测 / 缓存失效」。
    /// 任何后续优化应优先在此添加，再让 <see cref="WinRtHandwritingRecognizer"/> 调用，避免重复补丁式改动。
    /// </summary>
    /// <remarks>
    /// WinRT UWP <c>Windows.UI.Input.Inking.Analysis.InkAnalyzer</c> 未公开 <c>SetStrokeLanguageId</c>
    /// （该方法是 COM IInkAnalyzer 专属，已通过 Microsoft Learn 确认），因此语言匹配只能依赖
    /// <see cref="WinRtInk.InkRecognizerContainer.SetDefaultRecognizer"/> 选对识别器。本类仅记录 LCID
    /// 意图并据此挑选识别器，不直接设置逐笔语言 ID。
    /// </remarks>
    internal static class HandwritingRecognitionTuning
    {
        /// <summary>跟随系统语言（不覆盖）。</summary>
        public const int LcidFollowSystem = 0;

        private static readonly object _sync = new object();

        // 识别器枚举结果缓存：GetRecognizers() 在某些机型上每次调用都有百毫秒级开销，且结果在一次会话内稳定。
        private static IReadOnlyList<WinRtInk.InkRecognizer> _cachedRecognizers;
        private static bool _recognizersEnumerated;

        // 当前意图 LCID 与据此解析出的首选识别器。语言切换后通过 InvalidateCache() 清空重解析。
        private static int _intendedLcid = LcidFollowSystem;
        private static WinRtInk.InkRecognizer _preferredRecognizer;
        private static bool _preferredResolved;

        private static void LogTuning(string message, LogHelper.LogType logType = LogHelper.LogType.Info)
        {
            LogHelper.WriteLogToFile("[手写识别调优] " + message, logType);
        }

        /// <summary>把当前 settings 推到 Tuning。每次识别前调用，确保 LCID 覆盖与缓存一致。</summary>
        public static void ApplyFromSettings(int intendedLcid)
        {
            if (intendedLcid == _intendedLcid && _preferredResolved)
                return;

            // LCID 变化 → 失效已解析的识别器，下次识别重新按新 LCID 挑选。
            if (intendedLcid != _intendedLcid)
            {
                _intendedLcid = intendedLcid;
                _preferredRecognizer = null;
                _preferredResolved = false;
            }
        }

        /// <summary>当前意图 LCID（0=跟随系统）。供消费端判断 CJK 等语言相关行为。</summary>
        public static int CurrentIntendedLcid => _intendedLcid;

        /// <summary>
        /// 当前是否为 CJK（中/日/韩）识别意图。CJK 一字多笔、间距分词易拆字，消费端据此启用合并与跳过 Y 归一化。
        /// LCID=0（跟随系统）时按系统 UI/Current 文化推断。
        /// </summary>
        public static bool IsCjkRecognizerActive
        {
            get
            {
                var lcid = _intendedLcid;
                if (lcid == LcidFollowSystem)
                    return IsCjkCulture(PrimaryHandwritingCulture());

                // 常见 CJK LCID：zh-* (0x0804/0x0404/0x0C04/0x3404/0x0404…), ja (0x0411), ko (0x0412)
                try
                {
                    var ci = new CultureInfo(lcid);
                    return IsCjkCulture(ci);
                }
                catch
                {
                    return false;
                }
            }
        }

        private static bool IsCjkCulture(CultureInfo culture)
        {
            if (culture == null) return false;
            var lang = culture.TwoLetterISOLanguageName?.ToLowerInvariant() ?? string.Empty;
            return lang == "zh" || lang == "ja" || lang == "ko";
        }

        /// <summary>语言切换或 FOD 安装变化后调用，清空所有缓存强制重解析。</summary>
        public static void InvalidateCache()
        {
            lock (_sync)
            {
                _cachedRecognizers = null;
                _recognizersEnumerated = false;
                _preferredRecognizer = null;
                _preferredResolved = false;
            }
        }

        /// <summary>枚举系统已安装的手写识别器，结果在一次会话内缓存。</summary>
        public static IReadOnlyList<WinRtInk.InkRecognizer> EnumerateRecognizers(WinRtInk.InkRecognizerContainer container)
        {
            if (container == null)
                return Array.Empty<WinRtInk.InkRecognizer>();

            lock (_sync)
            {
                if (_recognizersEnumerated && _cachedRecognizers != null)
                    return _cachedRecognizers;

                try
                {
                    _cachedRecognizers = container.GetRecognizers() ?? Array.Empty<WinRtInk.InkRecognizer>();
                }
                catch (Exception ex)
                {
                    LogTuning("GetRecognizers 失败: " + ex.Message, LogHelper.LogType.Warning);
                    _cachedRecognizers = Array.Empty<WinRtInk.InkRecognizer>();
                }

                _recognizersEnumerated = true;
                return _cachedRecognizers;
            }
        }

        /// <summary>
        /// 按当前意图 LCID（0=跟随系统）解析最匹配的识别器。失败返回 null，调用方回落到系统默认。
        /// </summary>
        public static WinRtInk.InkRecognizer ResolvePrimaryRecognizer(WinRtInk.InkRecognizerContainer container)
        {
            if (container == null)
                return null;

            lock (_sync)
            {
                if (_preferredResolved)
                    return _preferredRecognizer;

                var all = EnumerateRecognizers(container);
                _preferredRecognizer = SelectByLcid(all, _intendedLcid);
                _preferredResolved = true;

                if (_preferredRecognizer != null)
                    LogTuning("已选用识别器 \"" + (_preferredRecognizer.Name ?? "?") + "\"，意图LCID=" + _intendedLcid);
                else if (all != null && all.Count > 0)
                    LogTuning("未匹配到与意图 LCID=" + _intendedLcid + " 对应的引擎，使用系统默认（共 " + all.Count + " 个）。");
                else
                    LogTuning("系统未安装任何手写识别器。", LogHelper.LogType.Warning);

                return _preferredRecognizer;
            }
        }

        /// <summary>把解析出的识别器设为容器默认；解析失败时不动容器（用系统默认）。</summary>
        public static void TryApplyPreferredRecognizer(WinRtInk.InkRecognizerContainer container, bool logDetail)
        {
            if (container == null)
                return;
            try
            {
                var recognizer = ResolvePrimaryRecognizer(container);
                if (recognizer != null)
                    container.SetDefaultRecognizer(recognizer);
            }
            catch (Exception ex)
            {
                LogTuning("SetDefaultRecognizer 失败: " + ex.Message, LogHelper.LogType.Warning);
            }
        }

        /// <summary>
        /// 检测简中(0804)/繁中(0404) 手写识别引擎是否安装。返回缺失的 LCID 列表（空表示齐全）。
        /// 供 UI 通知或日志引导用户去 ms-settings 安装 FOD。
        /// </summary>
        public static IReadOnlyList<int> GetMissingHandwritingLanguageLcids(WinRtInk.InkRecognizerContainer container)
        {
            var missing = new List<int>();
            if (container == null)
                return missing;

            var all = EnumerateRecognizers(container);
            bool hasZhHans = false, hasZhHant = false;
            foreach (var r in all)
            {
                var n = r?.Name;
                if (string.IsNullOrEmpty(n)) continue;
                var lower = n.ToLowerInvariant();
                if (lower.Contains("简体") || lower.Contains("簡體") ||
                    lower.Contains("simplified") || lower.Contains("hans") || lower.Contains("prc"))
                    hasZhHans = true;
                if (lower.Contains("繁体") || lower.Contains("繁體") ||
                    lower.Contains("traditional") || lower.Contains("hant") ||
                    lower.Contains("taiwan") || lower.Contains("hong kong"))
                    hasZhHant = true;
            }

            if (!hasZhHans) missing.Add(0x0804);
            if (!hasZhHant) missing.Add(0x0404);
            return missing;
        }

        /// <summary>按意图 LCID 挑识别器；LCID=0 时按系统 UI/当前区域文化推断。</summary>
        private static WinRtInk.InkRecognizer SelectByLcid(IReadOnlyList<WinRtInk.InkRecognizer> list, int intendedLcid)
        {
            if (list == null || list.Count == 0)
                return null;

            // 意图 LCID=0 → 跟随系统。沿用旧的「UI 优先、Current 兜底」文化推断逻辑。
            CultureInfo culture;
            bool wantZhHans, wantZhHant;
            string lang;

            if (intendedLcid == LcidFollowSystem)
            {
                culture = PrimaryHandwritingCulture();
                lang = (culture?.TwoLetterISOLanguageName ?? string.Empty).ToLowerInvariant();
                wantZhHans = IsZhHans(culture);
                wantZhHant = IsZhHant(culture);
            }
            else
            {
                try
                {
                    culture = new CultureInfo(intendedLcid);
                }
                catch
                {
                    culture = null;
                }
                lang = (culture?.TwoLetterISOLanguageName ?? string.Empty).ToLowerInvariant();
                wantZhHans = IsZhHans(culture);
                wantZhHant = IsZhHant(culture);
            }

            WinRtInk.InkRecognizer Pick(Func<string, bool> match)
            {
                foreach (var r in list)
                {
                    var n = r?.Name;
                    if (string.IsNullOrEmpty(n)) continue;
                    if (match(n)) return r;
                }
                return null;
            }

            // 1. 精确按 LCID 意图挑选（简体/繁体/日/英/韩/法/德）
            if (wantZhHans)
            {
                var r = Pick(n => IndexOfAny(n, "简体", "簡體") >= 0 ||
                                  (IndexOfAny(n, "中文", "Chinese") >= 0 &&
                                   IndexOfAny(n, "简体", "簡體", "Simplified", "Hans", "PRC") >= 0));
                if (r != null) return r;
                r = Pick(n => IndexOfAny(n, "中文", "Chinese") >= 0);
                if (r != null) return r;
            }
            else if (wantZhHant)
            {
                var r = Pick(n => IndexOfAny(n, "繁体", "繁體") >= 0 ||
                                  (IndexOfAny(n, "中文", "Chinese") >= 0 &&
                                   IndexOfAny(n, "繁体", "繁體", "Traditional", "Hant", "Taiwan", "Hong Kong") >= 0));
                if (r != null) return r;
                r = Pick(n => IndexOfAny(n, "中文", "Chinese") >= 0);
                if (r != null) return r;
            }
            else if (lang == "ja")
            {
                var r = Pick(n => IndexOfAny(n, "Japanese", "日本語", "日语") >= 0);
                if (r != null) return r;
            }
            else if (lang == "ko")
            {
                var r = Pick(n => IndexOfAny(n, "Korean", "한국", "韩语") >= 0);
                if (r != null) return r;
            }
            else if (lang == "en")
            {
                var r = Pick(n => n.IndexOf("English", StringComparison.OrdinalIgnoreCase) >= 0);
                if (r != null) return r;
            }
            else if (lang == "fr")
            {
                var r = Pick(n => IndexOfAny(n, "French", "Français", "法语") >= 0);
                if (r != null) return r;
            }
            else if (lang == "de")
            {
                var r = Pick(n => IndexOfAny(n, "German", "Deutsch", "德语") >= 0);
                if (r != null) return r;
            }

            // 2. 中文兜底：任何中文识别器
            if (lang == "zh")
            {
                var r = Pick(n => IndexOfAny(n, "中文", "Chinese") >= 0);
                if (r != null) return r;
            }

            return null;
        }

        private static CultureInfo PrimaryHandwritingCulture()
        {
            var ui = CultureInfo.CurrentUICulture;
            var ct = CultureInfo.CurrentCulture;
            if (string.Equals(ui.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase))
                return ui;
            if (string.Equals(ct.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase))
                return ct;
            return ui;
        }

        private static bool IsZhHans(CultureInfo culture)
        {
            if (culture == null) return false;
            var name = culture.Name ?? string.Empty;
            var lang = culture.TwoLetterISOLanguageName?.ToLowerInvariant() ?? string.Empty;
            if (lang != "zh") return false;
            return name.IndexOf("hans", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.Equals("zh-cn", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("zh-sg", StringComparison.OrdinalIgnoreCase) ||
                   (name.IndexOf("hant", StringComparison.OrdinalIgnoreCase) < 0 &&
                    !name.Equals("zh-tw", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("zh-hk", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("zh-mo", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsZhHant(CultureInfo culture)
        {
            if (culture == null) return false;
            var name = culture.Name ?? string.Empty;
            var lang = culture.TwoLetterISOLanguageName?.ToLowerInvariant() ?? string.Empty;
            if (lang != "zh") return false;
            return name.IndexOf("hant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.Equals("zh-tw", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("zh-hk", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("zh-mo", StringComparison.OrdinalIgnoreCase);
        }

        private static int IndexOfAny(string s, params string[] needles)
        {
            if (string.IsNullOrEmpty(s)) return -1;
            foreach (var needle in needles)
            {
                if (string.IsNullOrEmpty(needle)) continue;
                var idx = s.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0) return idx;
            }
            return -1;
        }
    }
}