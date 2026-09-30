using System.Text.RegularExpressions;
using blazwiz.Portal.Models.Notes;

namespace blazwiz.Portal.Application.Helpers;

/// <summary>
/// Works out the text changes made by the note editor's formatting buttons. Inline styles and line prefixes toggle,
/// so pressing a button a second time removes the formatting, as in a word processor.
/// </summary>
public static partial class MarkdownFormatHelpers
{
    /// <summary>
    /// Works out the edit that applies a format to the selected text.
    /// </summary>
    /// <param name="text">The editor text.</param>
    /// <param name="selectionStart">The start of the selection.</param>
    /// <param name="selectionEnd">The end of the selection (equal to the start when nothing is selected).</param>
    /// <param name="format">The format to apply.</param>
    /// <returns>The edit to make.</returns>
    public static MarkdownTextEdit Apply(string text, int selectionStart, int selectionEnd, MarkdownFormat format)
    {
        ArgumentNullException.ThrowIfNull(text);

        var start = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
        var end = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);

        return format switch
        {
            MarkdownFormat.Bold => ToggleInline(text, start, end, "**", "bold text"),
            MarkdownFormat.Italic => ToggleInline(text, start, end, "_", "italic text"),
            MarkdownFormat.Strikethrough => ToggleInline(text, start, end, "~~", "strikethrough text"),
            MarkdownFormat.InlineCode => ToggleInline(text, start, end, "`", "code"),
            MarkdownFormat.Heading1 => ToggleHeading(text, start, end, 1),
            MarkdownFormat.Heading2 => ToggleHeading(text, start, end, 2),
            MarkdownFormat.Heading3 => ToggleHeading(text, start, end, 3),
            MarkdownFormat.BulletList => ToggleList(text, start, end, BulletPattern(), _ => "- "),
            MarkdownFormat.NumberedList => ToggleList(text, start, end, NumberedPattern(), i => $"{i + 1}. "),
            MarkdownFormat.TaskList => ToggleList(text, start, end, TaskPattern(), _ => "- [ ] "),
            MarkdownFormat.Quote => ToggleQuote(text, start, end),
            MarkdownFormat.CodeBlock => ToggleCodeBlock(text, start, end),
            MarkdownFormat.Link => InsertLink(text, start, end),
            MarkdownFormat.HorizontalRule => InsertHorizontalRule(text, start, end),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown format."),
        };
    }

    private static MarkdownTextEdit ToggleInline(string text, int start, int end, string marker, string placeholder)
    {
        var selected = text[start..end];
        var length = marker.Length;

        // The selection includes the markers, e.g. "**bold**": remove them.
        if (selected.Length >= length * 2 && selected.StartsWith(marker, StringComparison.Ordinal) && selected.EndsWith(marker, StringComparison.Ordinal))
        {
            var inner = selected[length..^length];
            return Edit(start, end, inner, start, start + inner.Length);
        }

        // The markers are either side of the selection: remove them.
        if (start >= length && end + length <= text.Length
            && string.CompareOrdinal(text, start - length, marker, 0, length) == 0
            && string.CompareOrdinal(text, end, marker, 0, length) == 0)
        {
            return Edit(start - length, end + length, selected, start - length, start - length + selected.Length);
        }

        // Keep surrounding whitespace outside the markers (double clicking a word often selects the space after it).
        var core = selected.Trim();
        if (core.Length == 0)
        {
            var inserted = marker + placeholder + marker;
            return Edit(end, end, inserted, end + length, end + length + placeholder.Length);
        }

        var leading = selected.Length - selected.TrimStart().Length;
        var trailing = selected.Length - selected.TrimEnd().Length;
        var replacement = selected[..leading] + marker + core + marker + selected[^trailing..];
        var coreStart = start + leading + length;
        return Edit(start, end, replacement, coreStart, coreStart + core.Length);
    }

    private static MarkdownTextEdit ToggleHeading(string text, int start, int end, int level)
    {
        var (lineStart, lineEnd) = ExpandToLines(text, start, end);
        var lines = text[lineStart..lineEnd].Split('\n');
        var prefix = new string('#', level) + " ";
        var contentLines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        var allAtLevel = contentLines.Count > 0 && contentLines.All(l => HeadingPattern().Match(l) is { Success: true } m && m.Groups[1].Length == level);

        var updated = lines.Select(line =>
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return line;
            }

            var withoutHeading = HeadingPattern().Replace(line, string.Empty, 1);
            return allAtLevel ? withoutHeading : prefix + withoutHeading;
        });

        return ReplaceLines(lineStart, lineEnd, string.Join('\n', updated), start == end);
    }

    private static MarkdownTextEdit ToggleList(string text, int start, int end, Regex pattern, Func<int, string> prefixFor)
    {
        var (lineStart, lineEnd) = ExpandToLines(text, start, end);
        var lines = text[lineStart..lineEnd].Split('\n');
        var contentLines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        var allHaveList = contentLines.Count > 0 && contentLines.All(l => pattern.IsMatch(l));

        var index = 0;
        var updated = lines.Select(line =>
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return line;
            }

            // Replace any other kind of list marker, so a bulleted list can be turned straight into a numbered one.
            var match = AnyListPattern().Match(line);
            var indent = match.Groups[1].Value;
            var body = line[match.Length..];
            return allHaveList ? indent + body : indent + prefixFor(index++) + body;
        });

        return ReplaceLines(lineStart, lineEnd, string.Join('\n', updated), start == end);
    }

    private static MarkdownTextEdit ToggleQuote(string text, int start, int end)
    {
        var (lineStart, lineEnd) = ExpandToLines(text, start, end);
        var lines = text[lineStart..lineEnd].Split('\n');
        var contentLines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        var allQuoted = contentLines.Count > 0 && contentLines.All(l => QuotePattern().IsMatch(l));

        var updated = lines.Select(line => allQuoted
            ? QuotePattern().Replace(line, string.Empty, 1)
            : "> " + line);

        return ReplaceLines(lineStart, lineEnd, string.Join('\n', updated), start == end);
    }

    private static MarkdownTextEdit ToggleCodeBlock(string text, int start, int end)
    {
        var selected = text[start..end];
        var fenced = FencedBlockPattern().Match(selected);
        if (fenced.Success)
        {
            var inner = fenced.Groups[1].Value;
            return Edit(start, end, inner, start, start + inner.Length);
        }

        var body = selected.Length > 0 ? selected.TrimEnd('\n') : "code";
        var before = start > 0 && text[start - 1] != '\n' ? "\n" : string.Empty;
        var after = end < text.Length && text[end] != '\n' ? "\n" : string.Empty;
        var replacement = before + "```\n" + body + "\n```" + after;
        var bodyStart = start + before.Length + 4;
        return Edit(start, end, replacement, bodyStart, bodyStart + body.Length);
    }

    private static MarkdownTextEdit InsertLink(string text, int start, int end)
    {
        var selected = text[start..end].Trim();
        if (selected.Length == 0)
        {
            const string label = "link text";
            return Edit(start, end, $"[{label}](https://)", start + 1, start + 1 + label.Length);
        }

        if (selected.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || selected.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            const string label = "link text";
            return Edit(start, end, $"[{label}]({selected})", start + 1, start + 1 + label.Length);
        }

        const string url = "https://";
        var urlStart = start + selected.Length + 3;
        return Edit(start, end, $"[{selected}]({url})", urlStart, urlStart + url.Length);
    }

    private static MarkdownTextEdit InsertHorizontalRule(string text, int start, int end)
    {
        var textBefore = text[..start];
        var before = textBefore.Length == 0 || textBefore.EndsWith("\n\n", StringComparison.Ordinal)
            ? string.Empty
            : textBefore.EndsWith('\n') ? "\n" : "\n\n";
        var after = end < text.Length && text[end] == '\n' ? "\n" : "\n\n";
        var replacement = before + "---" + after;
        var caret = start + replacement.Length;
        return Edit(start, end, replacement, caret, caret);
    }

    private static (int Start, int End) ExpandToLines(string text, int start, int end)
    {
        // A selection ending just after a line break (e.g. triple click) should not include the following line.
        var effectiveEnd = end > start && text[end - 1] == '\n' ? end - 1 : end;
        var lineStart = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
        var lineEnd = text.IndexOf('\n', effectiveEnd);
        return (lineStart, lineEnd < 0 ? text.Length : lineEnd);
    }

    private static MarkdownTextEdit ReplaceLines(int lineStart, int lineEnd, string replacement, bool caretOnly)
    {
        // With nothing selected, leave the caret at the end of the line, ready to type.
        return caretOnly
            ? Edit(lineStart, lineEnd, replacement, lineStart + replacement.Length, lineStart + replacement.Length)
            : Edit(lineStart, lineEnd, replacement, lineStart, lineStart + replacement.Length);
    }

    private static MarkdownTextEdit Edit(int start, int end, string replacement, int selectionStart, int selectionEnd)
    {
        return new MarkdownTextEdit
        {
            Start = start,
            End = end,
            Replacement = replacement,
            SelectionStart = selectionStart,
            SelectionEnd = selectionEnd,
        };
    }

    [GeneratedRegex(@"^(#{1,6})\s+")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^\s*[-*+]\s+(?!\[[ xX]\]\s)")]
    private static partial Regex BulletPattern();

    [GeneratedRegex(@"^\s*\d+[.)]\s+")]
    private static partial Regex NumberedPattern();

    [GeneratedRegex(@"^\s*[-*+]\s+\[[ xX]\]\s+")]
    private static partial Regex TaskPattern();

    [GeneratedRegex(@"^(\s*)(?:[-*+]\s+\[[ xX]\]\s+|[-*+]\s+|\d+[.)]\s+)?")]
    private static partial Regex AnyListPattern();

    [GeneratedRegex(@"^>\s?")]
    private static partial Regex QuotePattern();

    [GeneratedRegex(@"^```[^\n]*\n([\s\S]*?)\n?```\s*$")]
    private static partial Regex FencedBlockPattern();
}
