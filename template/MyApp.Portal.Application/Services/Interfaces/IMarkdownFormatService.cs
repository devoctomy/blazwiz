using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Application.Services.Interfaces;

/// <summary>
/// Works out the text changes made by the note editor's formatting buttons. Inline styles and line prefixes toggle,
/// so pressing a button a second time removes the formatting, as in a word processor.
/// </summary>
public interface IMarkdownFormatService
{
    /// <summary>
    /// Works out the edit that applies a format to the selected text.
    /// </summary>
    /// <param name="text">The editor text.</param>
    /// <param name="selectionStart">The start of the selection.</param>
    /// <param name="selectionEnd">The end of the selection (equal to the start when nothing is selected).</param>
    /// <param name="format">The format to apply.</param>
    /// <returns>The edit to make.</returns>
    MarkdownTextEdit Apply(string text, int selectionStart, int selectionEnd, MarkdownFormat format);
}
