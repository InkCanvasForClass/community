using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 轻量级 Markdown → <see cref="FlowDocument"/> 渲染器。专为插件说明文档设计，
    /// 不引入任何第三方依赖，支持：标题、加粗、斜体、行内代码、代码块、列表、
    /// 链接、引用、分隔线与简单表格。
    /// </summary>
    public class PluginReadmeRenderer
    {
        private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+(.+?)\s*#*\s*$", RegexOptions.Compiled);
        private static readonly Regex FenceRegex = new(@"^```(\w*)\s*$", RegexOptions.Compiled);
        private static readonly Regex TableDivider = new(@"^\s*\|?\s*:?-+:?(\s*\|\s*:?-+:?)+\s*\|?\s*$", RegexOptions.Compiled);

        /// <summary>
        /// 将 Markdown 文本渲染为 <see cref="FlowDocument"/>。
        /// </summary>
        public FlowDocument Render(string markdown)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei"),
                FontSize = 13,
                LineHeight = 20,
                PagePadding = new Thickness(0),
                TextAlignment = TextAlignment.Left
            };

            var lines = (markdown ?? "").Replace("\r\n", "\n").Split('\n');
            var i = 0;

            while (i < lines.Length)
            {
                var line = lines[i];

                // 围栏代码块
                if (FenceRegex.IsMatch(line))
                {
                    var lang = FenceRegex.Match(line).Groups[1].Value;
                    var codeLines = new List<string>();
                    i++;
                    while (i < lines.Length && !FenceRegex.IsMatch(lines[i]))
                    {
                        codeLines.Add(lines[i]);
                        i++;
                    }
                    if (i < lines.Length) i++;
                    AppendCodeBlock(doc, codeLines, lang);
                    continue;
                }

                // 标题
                var hm = HeadingRegex.Match(line);
                if (hm.Success)
                {
                    var level = hm.Groups[1].Value.Length;
                    var text = hm.Groups[2].Value;
                    AppendHeading(doc, text, level);
                    i++;
                    continue;
                }

                // 表格：以 | ... | 起头且下一行是分隔行
                if (IsTableStart(lines, i))
                {
                    i = AppendTable(doc, lines, i);
                    continue;
                }

                // 分隔线
                if (Regex.IsMatch(line, @"^\s*([-*_])\s*\1\s*\1[\s\1]*$"))
                {
                    doc.Blocks.Add(new BlockUIContainer(new System.Windows.Controls.Separator
                    {
                        Margin = new Thickness(0, 8, 0, 8)
                    }));
                    i++;
                    continue;
                }

                // 无序列表
                var um = Regex.Match(line, @"^(\s*)[-*+]\s+(.+)$");
                if (um.Success)
                {
                    var indentMatch = Regex.Match(line, @"^(\s*)");
                    var indent = indentMatch.Groups[1].Value.Length;
                    var list = new List();
                    var markerIndent = indent;
                    while (i < lines.Length)
                    {
                        var lm = Regex.Match(lines[i], @"^(\s*)([-*+]|\d+\.)\s+(.+)$");
                        if (!lm.Success) break;
                        var thisIndent = lm.Groups[1].Value.Length;
                        if (thisIndent != markerIndent) break;
                        var li = new ListItem();
                        var p = new Paragraph();
                        AppendInline(p, lm.Groups[3].Value);
                        li.Blocks.Add(p);
                        list.ListItems.Add(li);
                        i++;
                    }
                    list.Margin = new Thickness(markerIndent * 8, 4, 0, 4);
                    doc.Blocks.Add(list);
                    continue;
                }

                // 有序列表
                var om = Regex.Match(line, @"^(\s*)\d+\.\s+(.+)$");
                if (om.Success)
                {
                    var indentMatch = Regex.Match(line, @"^(\s*)");
                    var indent = indentMatch.Groups[1].Value.Length;
                    var list = new List();
                    while (i < lines.Length)
                    {
                        var lm = Regex.Match(lines[i], @"^(\s*)\d+\.\s+(.+)$");
                        if (!lm.Success) break;
                        var thisIndent = lm.Groups[1].Value.Length;
                        if (thisIndent != indent) break;
                        var li = new ListItem();
                        var p = new Paragraph();
                        AppendInline(p, lm.Groups[2].Value);
                        li.Blocks.Add(p);
                        list.ListItems.Add(li);
                        i++;
                    }
                    doc.Blocks.Add(list);
                    continue;
                }

                // 引用
                if (line.StartsWith("> "))
                {
                    var quoteLines = new List<string>();
                    while (i < lines.Length && lines[i].StartsWith("> "))
                    {
                        quoteLines.Add(lines[i].Substring(2));
                        i++;
                    }
                    AppendParagraph(doc, string.Join(Environment.NewLine, quoteLines), italic: true, foreground: GetBrush("TextFillColorSecondaryBrush"));
                    continue;
                }

                // 空行
                if (string.IsNullOrWhiteSpace(line))
                {
                    i++;
                    continue;
                }

                // 普通段落（视为直到下一个空行）
                var paragraph = new List<string>();
                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
                {
                    paragraph.Add(lines[i]);
                    i++;
                }
                AppendParagraph(doc, string.Join(" ", paragraph));
            }

            return doc;
        }

        private void AppendHeading(FlowDocument doc, string text, int level)
        {
            var p = new Paragraph
            {
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, level <= 2 ? 12 : 8, 0, 6)
            };
            var size = level switch
            {
                1 => 22.0,
                2 => 18.0,
                3 => 16.0,
                4 => 14.0,
                5 => 13.0,
                _ => 12.0,
            };
            p.FontSize = size;
            AppendInline(p, text);
            doc.Blocks.Add(p);
        }

        private void AppendParagraph(FlowDocument doc, string text, bool italic = false, Brush foreground = null)
        {
            var p = new Paragraph { Margin = new Thickness(0, 2, 0, 4) };
            if (foreground != null) p.Foreground = foreground;
            AppendInline(p, text, italic);
            doc.Blocks.Add(p);
        }

        private void AppendCodeBlock(FlowDocument doc, List<string> codeLines, string lang)
        {
            var border = new System.Windows.Controls.Border
            {
                Background = GetBrush("ControlFillColorDefaultBrush"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 4, 0, 8)
            };

            var tb = new System.Windows.Controls.TextBlock
            {
                FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New"),
                FontSize = 12,
                Foreground = GetBrush("TextFillColorPrimaryBrush"),
                Text = string.Join(Environment.NewLine, codeLines),
                TextWrapping = TextWrapping.NoWrap
            };

            border.Child = tb;

            doc.Blocks.Add(new BlockUIContainer(border));
        }

        private bool IsTableStart(string[] lines, int idx)
        {
            if (idx + 1 >= lines.Length) return false;
            if (string.IsNullOrWhiteSpace(lines[idx])) return false;
            if (!lines[idx].Contains('|')) return false;
            return TableDivider.IsMatch(lines[idx + 1]);
        }

        private int AppendTable(FlowDocument doc, string[] lines, int start)
        {
            var headerCells = SplitRow(lines[start]);
            start++;
            start++; // skip divider line

            var rows = new List<string[]>();
            while (start < lines.Length && !string.IsNullOrWhiteSpace(lines[start]) && lines[start].Contains('|'))
            {
                rows.Add(SplitRow(lines[start]));
                start++;
            }

            var table = new System.Windows.Documents.Table { CellSpacing = 0, Margin = new Thickness(0, 6, 0, 6) };

            var headerRowGroup = new System.Windows.Documents.TableRowGroup();
            var headerRow = new System.Windows.Documents.TableRow { Background = GetBrush("SubtleFillColorSecondaryBrush") };
            foreach (var h in headerCells)
            {
                var cellP = new Paragraph(new Run(h.Trim())) { FontWeight = FontWeights.SemiBold };
                var cell = new System.Windows.Documents.TableCell(cellP) { Padding = new Thickness(6, 4, 6, 4) };
                headerRow.Cells.Add(cell);
            }
            headerRowGroup.Rows.Add(headerRow);
            table.RowGroups.Add(headerRowGroup);

            var bodyGroup = new System.Windows.Documents.TableRowGroup();
            foreach (var row in rows)
            {
                var tr = new System.Windows.Documents.TableRow();
                for (var c = 0; c < headerCells.Length; c++)
                {
                    var cellP = new Paragraph(new Run(c < row.Length ? row[c].Trim() : ""));
                    var cell = new System.Windows.Documents.TableCell(cellP) { Padding = new Thickness(6, 4, 6, 4) };
                    tr.Cells.Add(cell);
                }
                bodyGroup.Rows.Add(tr);
            }
            table.RowGroups.Add(bodyGroup);
            doc.Blocks.Add(table);
            if (start < lines.Length && string.IsNullOrWhiteSpace(lines[start])) start++;
            return start;
        }

        private static string[] SplitRow(string line)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("|")) trimmed = trimmed.Substring(1);
            if (trimmed.EndsWith("|")) trimmed = trimmed.Substring(0, trimmed.Length - 1);
            return trimmed.Split('|');
        }

        /// <summary>
        /// 解析行内 Markdown（`**bold**`、`*italic*`、`` `code` ``、`[text](url)`）并插入到 paragraph。
        /// </summary>
        private void AppendInline(Paragraph paragraph, string text, bool italicDefault = false)
        {
            if (paragraph == null) paragraph = new Paragraph();
            int pos = 0;
            while (pos < text.Length)
            {
                int nextBold = text.IndexOf("**", pos, StringComparison.Ordinal);
                int nextItalic = text.IndexOf("*", pos, StringComparison.Ordinal);
                int nextCode = text.IndexOf('`', pos);
                int nextLink = text.IndexOf('[', pos);

                // pick closest token
                var candidates = new[] { nextBold, nextItalic, nextCode, nextLink }
                    .Where(c => c >= 0)
                    .ToArray();
                if (candidates.Length == 0)
                {
                    var r = new Run(text.Substring(pos));
                    if (italicDefault) r.FontStyle = FontStyles.Italic;
                    paragraph.Inlines.Add(r);
                    break;
                }

                var idx = candidates.Min();

                // 先输出 idx 之前的纯文本
                if (idx > pos)
                {
                    var r = new Run(text.Substring(pos, idx - pos));
                    if (italicDefault) r.FontStyle = FontStyles.Italic;
                    paragraph.Inlines.Add(r);
                    pos = idx;
                }

                // 处理下一个 token
                if (text.Substring(pos).StartsWith("**"))
                {
                    var end = text.IndexOf("**", pos + 2, StringComparison.Ordinal);
                    if (end > pos)
                    {
                        var inner = text.Substring(pos + 2, end - pos - 2);
                        paragraph.Inlines.Add(new Run(inner) { FontWeight = FontWeights.Bold });
                        pos = end + 2;
                        continue;
                    }
                }

                if (text[pos] == '*')
                {
                    var end = text.IndexOf('*', pos + 1);
                    if (end > pos)
                    {
                        var inner = text.Substring(pos + 1, end - pos - 1);
                        paragraph.Inlines.Add(new Run(inner) { FontStyle = FontStyles.Italic });
                        pos = end + 1;
                        continue;
                    }
                }

                if (text[pos] == '`')
                {
                    var end = text.IndexOf('`', pos + 1);
                    if (end > pos)
                    {
                        var inner = text.Substring(pos + 1, end - pos - 1);
                        var run = new Run(inner)
                        {
                            FontFamily = new FontFamily("Consolas, Cascadia Mono"),
                            Background = GetBrush("ControlFillColorDefaultBrush"),
                            Foreground = GetBrush("TextFillColorPrimaryBrush")
                        };
                        paragraph.Inlines.Add(run);
                        pos = end + 1;
                        continue;
                    }
                }

                if (text[pos] == '[')
                {
                    var labelEnd = text.IndexOf(']', pos + 1);
                    if (labelEnd > pos && labelEnd + 1 < text.Length && text[labelEnd + 1] == '(')
                    {
                        var urlEnd = text.IndexOf(')', labelEnd + 2);
                        if (urlEnd > labelEnd)
                        {
                            var label = text.Substring(pos + 1, labelEnd - pos - 1);
                            var url = text.Substring(labelEnd + 2, urlEnd - labelEnd - 2);
                            var hyperlink = new Hyperlink(new Run(label))
                            {
                                NavigateUri = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null
                            };
                            paragraph.Inlines.Add(hyperlink);
                            pos = urlEnd + 1;
                            continue;
                        }
                    }
                }

                // 兜底往前走一格
                var fall = new Run(text[pos].ToString());
                if (italicDefault) fall.FontStyle = FontStyles.Italic;
                paragraph.Inlines.Add(fall);
                pos++;
            }
        }

        private static Brush GetBrush(string key)
        {
            if (Application.Current?.Resources[key] is Brush b) return b;
            return Brushes.Gray;
        }
    }
}
