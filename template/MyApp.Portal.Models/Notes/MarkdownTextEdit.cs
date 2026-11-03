namespace MyApp.Portal.Models.Notes;

/// <summary>
/// A change to the editor text: replace <see cref="Start"/> to <see cref="End"/> with <see cref="Replacement"/>, then select
/// <see cref="SelectionStart"/> to <see cref="SelectionEnd"/> (offsets in the new text).
/// </summary>
public class MarkdownTextEdit
{
    /// <summary>
    /// Gets or sets the start of the range to replace.
    /// </summary>
    public int Start { get; set; }

    /// <summary>
    /// Gets or sets the end of the range to replace.
    /// </summary>
    public int End { get; set; }

    /// <summary>
    /// Gets or sets the text to insert in place of the range.
    /// </summary>
    public string Replacement { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the start of the selection after the edit.
    /// </summary>
    public int SelectionStart { get; set; }

    /// <summary>
    /// Gets or sets the end of the selection after the edit.
    /// </summary>
    public int SelectionEnd { get; set; }

    /// <summary>
    /// Applies the edit to the given text.
    /// </summary>
    /// <param name="text">The text before the edit.</param>
    /// <returns>The text after the edit.</returns>
    public string ApplyTo(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return string.Concat(text.AsSpan(0, Start), Replacement, text.AsSpan(End));
    }
}
