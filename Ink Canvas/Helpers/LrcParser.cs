using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ink_Canvas.Helpers
{
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

                    if (!lyricMap.ContainsKey(time))
                    {
                        // First encounter → main lyrics
                        lyricMap[time] = new LrcLine
                        {
                            Time = time,
                            Text = text,
                            Translation = inlineTranslation ?? string.Empty
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
