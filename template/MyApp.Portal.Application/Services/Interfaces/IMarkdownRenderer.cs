namespace MyApp.Portal.Application.Services.Interfaces;

/// <summary>
/// Converts markdown to HTML for previewing.
/// </summary>
public interface IMarkdownRenderer
{
    /// <summary>
    /// Renders markdown as HTML. Raw HTML in the markdown is escaped and unsafe link URLs are removed,
    /// so the result can be displayed directly.
    /// </summary>
    /// <param name="markdown">The markdown.</param>
    /// <returns>The HTML.</returns>
    string ToHtml(string? markdown);
}
