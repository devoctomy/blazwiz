using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MyApp.Portal.Application.Services.Interfaces;

namespace MyApp.Portal.Application.Services;

/// <summary>
/// Renders markdown with <see href="https://github.com/xoofx/markdig">Markdig</see>: GitHub style tables, task lists,
/// strikethrough and auto links, with raw HTML disabled.
/// </summary>
public class MarkdownRenderer : IMarkdownRenderer
{
    private static readonly string[] SafeSchemes = ["http", "https", "mailto"];

    // Extensions are listed one by one rather than UseAdvancedExtensions(), which includes generic attributes
    // ({onerror=...}) that would let a note add event handlers to the preview.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoIdentifiers()
        .UsePipeTables()
        .UseGridTables()
        .UseTaskLists()
        .UseEmphasisExtras()
        .UseListExtras()
        .UseFootnotes()
        .UseDefinitionLists()
        .UseAutoLinks()
        .DisableHtml()
        .Build();

    /// <inheritdoc/>
    public string ToHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var document = Markdown.Parse(markdown, Pipeline);
        foreach (var link in document.Descendants<LinkInline>())
        {
            if (!IsSafeUrl(link.Url))
            {
                link.Url = "#";
            }
        }

        foreach (var link in document.Descendants<AutolinkInline>())
        {
            if (!IsSafeUrl(link.Url))
            {
                link.Url = "#";
            }
        }

        return document.ToHtml(Pipeline);
    }

    /// <summary>
    /// Checks a link URL is relative, a fragment, or uses a safe scheme, so <c>javascript:</c> and similar links are never rendered.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <returns>True if the URL is safe to render.</returns>
    private static bool IsSafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        // Browsers ignore whitespace and control characters inside a scheme, e.g. "java\tscript:".
        var compact = new string(url.Where(c => !char.IsWhiteSpace(c) && !char.IsControl(c)).ToArray());
        var colon = compact.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            return true;
        }

        // A colon after a path, query or fragment character is not a scheme separator.
        var firstDelimiter = compact.IndexOfAny(['/', '?', '#']);
        if (firstDelimiter >= 0 && firstDelimiter < colon)
        {
            return true;
        }

        var scheme = compact[..colon];
        return SafeSchemes.Contains(scheme, StringComparer.OrdinalIgnoreCase);
    }
}
