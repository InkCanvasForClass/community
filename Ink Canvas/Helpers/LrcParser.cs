using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// Represents a single timed character within a lyric line.
    /// Used for per-character highlight animation (already-sung / pending chars).
    /// </summary>
    public class LrcChar
    {
        /// <summary>Character glyph (may be a single Han character, Latin letter, or punctuation).</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Offset from the line start when this character starts being sung.</summary>
        public TimeSpan StartOffset { get; set; }

        /// <summary>Duration the character is held; <c>null</c> means the line end is used.</summary>
        public TimeSpan? Duration { get; set; }
    }

    /// <summary>
    /// Represents a single lyric line with timing information.
    /// </summary>
    public class LrcLine
    {
        /// <summary>Timestamp when this line should be displayed.</summary>
        public TimeSpan Time { get; set; }

        /// <summary>Primary lyric text.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Translated lyric text (if available).</summary>
        public string Translation { get; set; } = string.Empty;

        /// <summary>
        /// Per-character timing inside this line. Empty when the LRC only provides line-level
        /// timestamps; an evenly-distributed fallback can be computed on demand.
        /// </summary>
        public List<LrcChar> Chars { get; set; } = new List<LrcChar>();
    }

    /// <summary>
    /// Represents a parsed LRC file containing metadata and timed lyrics.
    /// </summary>
    public class LrcData
    {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public TimeSpan Offset { get; set; } = TimeSpan.Zero;
        public List<LrcLine> Lines { get; set; } = new List<LrcLine>();
    }

    /// <summary>
    /// Parses standard LRC and translated LRC files.
    /// </summary>
    public static class LrcParser
    {
        // Matches [mm:ss.xx] or [mm:ss.xxx] or [mm:ss]
        private static readonly Regex TimeTagRegex = new Regex(
            @"\[(\d{1,3}):(\d{1,2})(?:\.(\d{1,3}))?\]",
            RegexOptions.Compiled);

        // Matches metadata tags like [ti:Title], [ar:Artist], etc.
        private static readonly Regex MetaTagRegex = new Regex(
            @"\[(\w+):(.*?)\]",
            RegexOptions.Compiled);

        // Matches inline per-character timestamps like <12:34.56> or <34.56> following LRC extensions.
        // Captures: minutes (optional), seconds, fraction (optional).
        private static readonly Regex CharTimeTagRegex = new Regex(
            @"<(\d{1,3})?:?(\d{1,2})(?:\.(\d{1,3}))?>",
            RegexOptions.Compiled);

        // Splits the lyric body into "segments" of tagged or untagged runs.
        // Example: "你<00:00.50>好<00:01.20>世 界" → ["你", "<00:00.50>", "好", "<00:01.20>", "世 界"]
        private static readonly Regex CharSegmentRegex = new Regex(
            @"<[^>]+>|[^<]+",
            RegexOptions.Compiled);

        /// <summary>
        /// Parses an LRC file from the given path.
        /// Returns null if the file does not exist or cannot be parsed.
        /// </summary>
        public static LrcData ParseFile(string lrcPath)
        {
            if (string.IsNullOrEmpty(lrcPath) || !File.Exists(lrcPath))
                return null;

            try
            {
                var lines = File.ReadAllLines(lrcPath);
                return ParseLines(lines);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Parses LRC content from an array of lines.
        /// Uses Dictionary to ensure first-encounter-is-main, second-encounter-is-translation.
        /// </summary>
        public static LrcData ParseLines(string[] lines)
        {
            if (lines == null || lines.Length == 0)
                return null;

            var lrc = new LrcData();
            var lyricMap = new Dictionary<TimeSpan, LrcLine>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var trimmed = line.Trim();

                // Skip awlrc tags (Base64-encoded LRC data contains time-tag patterns)
                if (trimmed.Contains("awlrc"))
                    continue;

                // Check if the line starts with a time tag
                if (!trimmed.StartsWith("["))
                {
                    // Standalone "//" translation line for the previous entry
                    if (trimmed.StartsWith("//") && lyricMap.Count > 0)
                    {
                        var lastEntry = lyricMap.Values.Last();
                        if (string.IsNullOrEmpty(lastEntry.Translation))
                        {
                            lastEntry.Translation = trimmed.Substring(2).Trim();
                        }
                    }
                    continue;
                }

                // Find where the time tags end and text begins
                var timeMatches = TimeTagRegex.Matches(trimmed);
                if (timeMatches.Count == 0)
                {
                    // Try to parse as metadata tag
                    ParseMetaTag(trimmed, lrc);
                    continue;
                }

                // Extract text after the last time tag
                var lastBracketEnd = trimmed.LastIndexOf(']');
                var text = lastBracketEnd >= 0 && lastBracketEnd < trimmed.Length - 1
                    ? trimmed.Substring(lastBracketEnd + 1).Trim()
                    : string.Empty;

                // Handle "//" translation separator within the text
                string inlineTranslation = null;
                if (!string.IsNullOrEmpty(text))
                {
                    var slashIndex = text.IndexOf("//");
                    if (slashIndex >= 0)
                    {
                        inlineTranslation = text.Substring(slashIndex + 2).Trim();
                        text = text.Substring(0, slashIndex).Trim();
                    }
                }

                if (string.IsNullOrEmpty(text)) continue;

                // Create or merge an entry for each time tag
                foreach (Match match in timeMatches)
                {
                    var timeSpan = ParseTimeTag(match);
                    if (!timeSpan.HasValue) continue;

                    var time = timeSpan.Value;
                    var charTimings = ParseCharTimings(text);

                    if (!lyricMap.ContainsKey(time))
                    {
                        // First encounter → main lyrics
                        lyricMap[time] = new LrcLine
                        {
                            Time = time,
                            Text = text,
                            Translation = inlineTranslation ?? string.Empty,
                            Chars = charTimings
                        };
                    }
                    else
                    {
                        // Second encounter → translation (never overwrite main lyrics)
                        var entry = lyricMap[time];
                        if (string.IsNullOrEmpty(entry.Translation)
                            && !IsCreditLine(entry.Text)
                            && !IsCreditLine(text))
                        {
                            entry.Translation = text;
                        }
                    }
                }
            }

            // Apply offset
            if (lrc.Offset != TimeSpan.Zero)
            {
                foreach (var entry in lyricMap.Values)
                {
                    entry.Time = entry.Time + lrc.Offset;
                    if (entry.Time < TimeSpan.Zero)
                        entry.Time = TimeSpan.Zero;
                }
            }

            // Sort by time and return
            lrc.Lines = lyricMap.Values.OrderBy(e => e.Time).ToList();
            return lrc.Lines.Count > 0 ? lrc : null;
        }

        /// <summary>
        /// Parses inline per-character timestamps embedded in the lyric body.
        /// Expected format: literal text with optional "&lt;mm:ss.xx&gt;" tags interleaved before each
        /// character/segment, e.g. "&lt;00:00.50&gt;你&lt;00:01.20&gt;好 世&lt;00:01.80&gt;界".
        /// Returns an empty list when the body does not contain any inline timestamp tags
        /// (caller can then fall back to evenly-distributed timings).
        /// </summary>
        public static List<LrcChar> ParseCharTimings(string body)
        {
            var result = new List<LrcChar>();
            if (string.IsNullOrWhiteSpace(body)) return result;

            // First pass: scan tag/text segments and materialize them.
            var segments = new List<(bool IsTag, string Content)>();
            bool anyTag = false;
            foreach (Match seg in CharSegmentRegex.Matches(body))
            {
                var piece = seg.Value;
                if (string.IsNullOrEmpty(piece)) continue;
                var isTag = piece.Length >= 2 && piece[0] == '<' && piece[piece.Length - 1] == '>';
                if (isTag) anyTag = true;
                segments.Add((isTag, piece));
            }
            if (segments.Count == 0 || !anyTag) return result;

            // Second pass: walk segments. The first tag establishes the timeline origin;
            // each following tag shifts "currentOffset" forward to that timestamp. Every
            // text chunk between two tags shares the active offset, with characters in that
            // chunk given micro-offsets so adjacent chars have start times slightly different
            // (1/10 of the gap to the next tag), which preserves smooth per-char animation
            // even when an LRC author groups multi-char words under one tag.
            TimeSpan currentOffset = TimeSpan.Zero;
            TimeSpan nextTagOffset = TimeSpan.Zero;
            bool nextTagPending = false;

            for (int i = 0; i < segments.Count; i++)
            {
                var (isTag, content) = segments[i];

                if (isTag)
                {
                    var ts = ParseInlineCharTimeTag(content);
                    if (ts.HasValue)
                    {
                        // If we're sitting at the start of a chunk, this tag is the chunk's
                        // anchor; otherwise it becomes the next pending anchor.
                        if (result.Count == 0 || i == 0)
                        {
                            currentOffset = ts.Value;
                        }
                        else
                        {
                            nextTagOffset = ts.Value;
                            nextTagPending = true;
                        }
                    }
                    continue;
                }

                // Text chunk
                var chars = ToDisplayChars(content).ToList();
                if (chars.Count == 0) continue;

                // Span between current offset and the next tag (or end of string).
                TimeSpan chunkEnd = nextTagPending ? nextTagOffset : currentOffset + TimeSpan.FromMilliseconds(500);
                if (chunkEnd < currentOffset) chunkEnd = currentOffset + TimeSpan.FromMilliseconds(500);
                var chunkSpan = chunkEnd - currentOffset;
                var perChar = TimeSpan.FromMilliseconds(chars.Count > 0
                    ? chunkSpan.TotalMilliseconds / chars.Count
                    : 0);

                for (int k = 0; k < chars.Count; k++)
                {
                    result.Add(new LrcChar
                    {
                        Text = chars[k],
                        StartOffset = currentOffset + TimeSpan.FromMilliseconds(perChar.TotalMilliseconds * k)
                    });
                }

                currentOffset = chunkEnd;
                nextTagPending = false;
            }

            // Normalize so the first character starts at zero offset.
            if (result.Count > 0)
            {
                var first = result[0].StartOffset;
                if (first > TimeSpan.Zero)
                {
                    foreach (var c in result) c.StartOffset -= first;
                }
                else if (first < TimeSpan.Zero)
                {
                    foreach (var c in result) c.StartOffset = TimeSpan.Zero;
                }
            }

            // Compute per-character durations from successive offsets.
            for (int i = 0; i < result.Count; i++)
            {
                if (i + 1 < result.Count && result[i + 1].StartOffset > result[i].StartOffset)
                {
                    result[i].Duration = result[i + 1].StartOffset - result[i].StartOffset;
                }
            }

            return result;
        }

        private static TimeSpan? ParseInlineCharTimeTag(string piece)
        {
            var match = CharTimeTagRegex.Match(piece);
            if (!match.Success) return null;

            bool hasMinutes = !string.IsNullOrEmpty(match.Groups[1].Value);
            int minutes = 0;
            int seconds;
            int milliseconds = 0;

            if (hasMinutes && !int.TryParse(match.Groups[1].Value, out minutes)) return null;
            if (!int.TryParse(match.Groups[2].Value, out seconds)) return null;

            if (match.Groups[3].Success)
            {
                var msStr = match.Groups[3].Value;
                if (msStr.Length == 1) msStr += "00";
                else if (msStr.Length == 2) msStr += "0";
                int.TryParse(msStr, out milliseconds);
            }

            return new TimeSpan(0, 0, minutes, seconds, milliseconds);
        }

        /// <summary>
        /// Splits a segment into display characters using grapheme clusters when possible;
        /// falls back to enumerating the string char-by-char for surrogate pairs.
        /// </summary>
        private static IEnumerable<string> ToDisplayChars(string segment)
        {
            if (string.IsNullOrEmpty(segment)) yield break;

            var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(segment);
            while (enumerator.MoveNext())
            {
                yield return (string)enumerator.Current;
            }
        }

        /// <summary>
        /// Returns per-character timings for a line, evenly distributing across the next line's
        /// start (or <paramref name="defaultDuration"/> if no neighbour is available). Use when the
        /// LRC body itself does not provide inline "&lt;...&gt;" timestamps.
        /// </summary>
        public static void EnsureCharTimings(LrcLine line, TimeSpan? nextLineStart, TimeSpan defaultDuration)
        {
            if (line == null) return;

            // Inline timings already present? Nothing to do.
            if (line.Chars != null && line.Chars.Count > 0)
            {
                // Still fill in the trailing character duration if missing.
                if (line.Chars.Count > 0 && line.Chars[line.Chars.Count - 1].Duration == null)
                {
                    var lineDur = (nextLineStart ?? (line.Time + defaultDuration)) - line.Time;
                    if (lineDur < TimeSpan.Zero) lineDur = defaultDuration;
                    for (int i = 0; i < line.Chars.Count; i++)
                    {
                        var c = line.Chars[i];
                        if (c.Duration == null)
                        {
                            if (i + 1 < line.Chars.Count && line.Chars[i + 1].StartOffset > c.StartOffset)
                            {
                                c.Duration = line.Chars[i + 1].StartOffset - c.StartOffset;
                            }
                            else
                            {
                                c.Duration = lineDur - c.StartOffset;
                                if (c.Duration < TimeSpan.Zero) c.Duration = TimeSpan.Zero;
                            }
                        }
                    }
                }
                return;
            }

            var displayChars = ToDisplayChars(line.Text ?? string.Empty).ToList();
            if (displayChars.Count == 0)
            {
                line.Chars = new List<LrcChar>();
                return;
            }

            var lineDuration = defaultDuration;
            if (nextLineStart.HasValue && nextLineStart.Value > line.Time)
            {
                lineDuration = nextLineStart.Value - line.Time;
                if (lineDuration <= TimeSpan.Zero) lineDuration = defaultDuration;
            }

            // Average per-char slice
            var totalMs = lineDuration.TotalMilliseconds;
            var stepMs = totalMs / displayChars.Count;

            var chars = new List<LrcChar>(displayChars.Count);
            for (int i = 0; i < displayChars.Count; i++)
            {
                chars.Add(new LrcChar
                {
                    Text = displayChars[i],
                    StartOffset = TimeSpan.FromMilliseconds(stepMs * i),
                    Duration = TimeSpan.FromMilliseconds(i + 1 < displayChars.Count ? stepMs : Math.Max(0, totalMs - stepMs * i))
                });
            }
            line.Chars = chars;
        }

        /// <summary>
        /// Computes the per-character highlight progress for the current playback position.
        /// Returns a value in [0, 1] where 0 means "no chars sung yet" and 1 means "all chars sung".
        /// Useful for animating a sweep gradient inside the active line.
        /// </summary>
        public static double GetLineProgress(LrcLine line, TimeSpan position)
        {
            if (line == null) return 0;
            var relative = position - line.Time;
            if (relative <= TimeSpan.Zero) return 0;
            var last = line.Chars != null && line.Chars.Count > 0 ? line.Chars[line.Chars.Count - 1] : null;
            if (last == null) return 0;
            var lastDur = last.Duration ?? TimeSpan.Zero;
            var total = last.StartOffset + lastDur;
            if (total <= TimeSpan.Zero) return 1;
            var ratio = relative.TotalMilliseconds / total.TotalMilliseconds;
            if (ratio < 0) return 0;
            if (ratio > 1) return 1;
            return ratio;
        }

        /// <summary>
        /// Gets the index of the current lyric line for the given playback position.
        /// Returns -1 if no line matches.
        /// </summary>
        public static int GetCurrentLineIndex(List<LrcLine> lines, TimeSpan position)
        {
            if (lines == null || lines.Count == 0)
                return -1;

            // Binary search for the last line whose time <= position
            int lo = 0, hi = lines.Count - 1;
            int result = -1;

            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (lines[mid].Time <= position)
                {
                    result = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return result;
        }

        private static TimeSpan? ParseTimeTag(Match match)
        {
            if (!int.TryParse(match.Groups[1].Value, out int minutes))
                return null;
            if (!int.TryParse(match.Groups[2].Value, out int seconds))
                return null;

            int milliseconds = 0;
            if (match.Groups[3].Success)
            {
                var msStr = match.Groups[3].Value;
                // Normalize to 3 digits (e.g., "5" -> "500", "50" -> "500", "500" -> "500")
                if (msStr.Length == 1) msStr += "00";
                else if (msStr.Length == 2) msStr += "0";
                int.TryParse(msStr, out milliseconds);
            }

            return new TimeSpan(0, 0, minutes, seconds, milliseconds);
        }

        private static void ParseMetaTag(string line, LrcData lrc)
        {
            var match = MetaTagRegex.Match(line);
            if (!match.Success) return;

            var key = match.Groups[1].Value.ToLowerInvariant();
            var value = match.Groups[2].Value.Trim();

            switch (key)
            {
                case "ti":
                    lrc.Title = value;
                    break;
                case "ar":
                    lrc.Artist = value;
                    break;
                case "al":
                    lrc.Album = value;
                    break;
                case "offset":
                    if (double.TryParse(value, out double offsetMs))
                    {
                        lrc.Offset = TimeSpan.FromMilliseconds(offsetMs);
                    }
                    break;
            }
        }

        // Pattern to detect metadata/credit lines like "作词: xxx", "Composer: xxx"
        private static readonly Regex CreditLineRegex = new Regex(
            @"^(作词|作曲|编曲|制作人|出品|演唱|录音|混音|母带|音乐制作人|音乐监制|弦乐编写|配唱制作|人声|录音棚|录音师|混音师|母带制作|制谱|乐队|民谣吉他|电吉他|钢琴|Lyricist|Composer|Arranger|Producer|Vocalist|Singer|Recording|Mixing|Mastering|Studio|Engineer|Words|Music)\s*[:：]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static bool IsCreditLine(string text)
        {
            return !string.IsNullOrWhiteSpace(text) && CreditLineRegex.IsMatch(text);
        }
    }
}
